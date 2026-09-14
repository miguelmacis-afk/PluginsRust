using System;
using System.Collections.Generic;
using UnityEngine;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("MiEventClaimBlocker", "☠ Ⓜ🅘ⓖ🅤ⓔ🅛 ☠", "3.0.4")]
    [Description("Bloquea daño y looteo exclusivo para Helis, Bradley, Cajas Hackeables, Entregas Aéreas, Satélite y Excavadora con UI, Clanes y Amigos.")]
    public class MiEventClaimBlocker : RustPlugin
    {
        [PluginReference] private Plugin Clans;
        [PluginReference] private Plugin Friends;

        private const string PERM_ADMIN = "mieventclaimblocker.admin";
        private const string UI_PANEL_NAME = "MiEventClaimBlockerUI";

        #region Configuration
        private bool enableBradley;
        private bool enableHeli;
        private bool enableLockedCrate;
        private bool enableSupplyDrop;
        private bool enableSatelliteEvent;
        private bool enableExcavator; // Nuevo para la Excavadora

        private float defaultExclusivityMinutes;
        private float lockedCrateExclusivityMinutes;
        private float satelliteExclusivityMinutes;
        private float excavatorExclusivityMinutes; // Tiempo de exclusividad para los drops de excavadora
        private float claimTimeoutMinutes;

        private bool enableClans;
        private bool enableFriends;
        private bool enableUI;

        protected override void LoadDefaultConfig()
        {
            Config["EnableBradley"] = true;
            Config["EnableHeli"] = true;
            Config["EnableLockedCrate"] = true;
            Config["EnableSupplyDrop"] = true;
            Config["EnableSatelliteEvent"] = true;
            Config["EnableExcavator"] = true; // Por defecto activo

            Config["DefaultExclusivityMinutes"] = 15.0f;
            Config["LockedCrateExclusivityMinutes"] = 30.0f;
            Config["SatelliteExclusivityMinutes"] = 20.0f;
            Config["ExcavatorExclusivityMinutes"] = 20.0f; // Duración de los drops de excavadora
            Config["ClaimTimeoutMinutes"] = 5.0f;

            Config["EnableClansIntegration"] = true;
            Config["EnableFriendsIntegration"] = true;
            Config["EnableVisualUI"] = true;

            SaveConfig();
        }

        private void LoadConfigVariables()
        {
            enableBradley = GetConfig(true, "EnableBradley");
            enableHeli = GetConfig(true, "EnableHeli");
            enableLockedCrate = GetConfig(true, "EnableLockedCrate");
            enableSupplyDrop = GetConfig(true, "EnableSupplyDrop");
            enableSatelliteEvent = GetConfig(true, "EnableSatelliteEvent");
            enableExcavator = GetConfig(true, "EnableExcavator");

            defaultExclusivityMinutes = Convert.ToSingle(GetConfig(15.0f, "DefaultExclusivityMinutes"));
            lockedCrateExclusivityMinutes = Convert.ToSingle(GetConfig(30.0f, "LockedCrateExclusivityMinutes"));
            satelliteExclusivityMinutes = Convert.ToSingle(GetConfig(20.0f, "SatelliteExclusivityMinutes"));
            excavatorExclusivityMinutes = Convert.ToSingle(GetConfig(20.0f, "ExcavatorExclusivityMinutes"));
            claimTimeoutMinutes = Convert.ToSingle(GetConfig(5.0f, "ClaimTimeoutMinutes"));

            enableClans = GetConfig(true, "EnableClansIntegration");
            enableFriends = GetConfig(true, "EnableFriendsIntegration");
            enableUI = GetConfig(true, "EnableVisualUI");
        }

        private T GetConfig<T>(T defaultVal, params string[] path)
        {
            object val = Config;
            foreach (var key in path)
            {
                var dict = val as Dictionary<string, object>;
                if (dict == null || !dict.TryGetValue(key, out val))
                {
                    Config[path[0]] = defaultVal;
                    return defaultVal;
                }
            }
            return (T)Convert.ChangeType(val, typeof(T));
        }
        #endregion

        #region Data Classes
        class ClaimData
        {
            public ulong OwnerID;
            public ulong TeamID;
            public float LastHitTime;
        }

        class CrateClaimData
        {
            public ulong OwnerID;
            public ulong TeamID;
            public float SpawnTime;
            public float ExclusivityDuration;
        }

        class PendingCrateClaim
        {
            public ulong OwnerID;
            public ulong TeamID;
            public float DeathTime;
            public Vector3 Position;
            public bool IsHeli;
        }
        #endregion

        private Dictionary<ulong, ClaimData> activeClaims = new Dictionary<ulong, ClaimData>();
        private List<PendingCrateClaim> pendingClaims = new List<PendingCrateClaim>();
        private Dictionary<ulong, CrateClaimData> crateClaims = new Dictionary<ulong, CrateClaimData>();
        private Dictionary<ulong, ulong> supplySignalThrowers = new Dictionary<ulong, ulong>();

        // Rastreo de llamadas rápidas (Airfield / Satélite)
        private ulong lastMonumentCallerID = 0;
        private float lastMonumentCallTime = 0f;

        // Rastreo de sesión activa de la Excavadora (dura varios minutos mientras emite drops)
        private ulong activeExcavatorOwnerID = 0;
        private float excavatorSessionEndTime = 0f;

        const float BRADLEY_LINK_DISTANCE_SQR = 10000f; 

        private float DefaultExclusivitySeconds => defaultExclusivityMinutes * 60f;
        private float LockedCrateExclusivitySeconds => lockedCrateExclusivityMinutes * 60f;
        private float SatelliteExclusivitySeconds => satelliteExclusivityMinutes * 60f;
        private float ExcavatorExclusivitySeconds => excavatorExclusivityMinutes * 60f;
        private float ClaimTimeoutSeconds => claimTimeoutMinutes * 60f;

        void Init()
        {
            permission.RegisterPermission(PERM_ADMIN, this);
            LoadConfigVariables();
        }

        #region Integration Helpers
        private bool IsClanMember(ulong ownerId, ulong targetId)
        {
            if (!enableClans || Clans == null) return false;
            
            object res = Clans.Call("IsClanMember", ownerId.ToString(), targetId.ToString());
            if (res is bool b1 && b1) return true;

            res = Clans.Call("IsMemberOrSameClan", ownerId, targetId);
            if (res is bool b2 && b2) return true;

            res = Clans.Call("HasSameClan", ownerId.ToString(), targetId.ToString());
            if (res is bool b3 && b3) return true;

            return false;
        }

        private bool AreFriends(ulong ownerId, ulong targetId)
        {
            if (!enableFriends || Friends == null) return false;

            object res = Friends.Call("AreFriends", ownerId, targetId);
            if (res is bool b1 && b1) return true;

            res = Friends.Call("AreFriends", ownerId.ToString(), targetId.ToString());
            if (res is bool b2 && b2) return true;

            res = Friends.Call("IsFriend", ownerId, targetId);
            if (res is bool b3 && b3) return true;

            return false;
        }

        private bool IsAuthorized(ulong ownerId, ulong ownerTeamId, BasePlayer player)
        {
            if (player == null) return false;
            ulong playerId = player.userID;

            if (ownerId == playerId) return true;

            ulong playerTeamId = player.currentTeam;
            if (ownerTeamId != 0 && ownerTeamId == playerTeamId) return true;

            if (IsClanMember(ownerId, playerId)) return true;
            if (AreFriends(ownerId, playerId)) return true;

            return false;
        }
        #endregion

        #region User Interface (UI)
        private void ShowBlockedUI(BasePlayer player, string message, float duration = 3.5f)
        {
            if (!enableUI || player == null) return;

            CuiHelper.DestroyUi(player, UI_PANEL_NAME);

            var elements = new CuiElementContainer();
            var mainPanel = elements.Add(new CuiPanel
            {
                Image = { Color = "0.8 0.1 0.1 0.88" },
                RectTransform = { AnchorMin = "0.25 0.82", AnchorMax = "0.75 0.88" },
                CursorEnabled = false
            }, "Overlay", UI_PANEL_NAME);

            elements.Add(new CuiLabel
            {
                Text = { Text = message, FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
            }, mainPanel);

            CuiHelper.AddUi(player, elements);

            timer.Once(duration, () =>
            {
                if (player != null) CuiHelper.DestroyUi(player, UI_PANEL_NAME);
            });
        }
        #endregion

        #region Oxide Hooks
        
        // Detectar uso de terminales en monumentos y la computadora de la Excavadora
        object CanUseEntity(BasePlayer player, BaseEntity entity)
        {
            if (player == null || entity == null) return null;
            string prefab = entity.ShortPrefabName.ToLower();

            // Si interactúa con la computadora de la Excavadora
            if (prefab.Contains("excavator") && enableExcavator)
            {
                activeExcavatorOwnerID = player.userID;
                excavatorSessionEndTime = Time.realtimeSinceStartup + (excavatorExclusivityMinutes * 60f);
                player.ChatMessage("<color=#00ff00>Has activado la Excavadora.</color> Los suministros que emita serán exclusivos para ti y tu equipo.");
            }
            // Terminales de otros monumentos (Airfield / Lanzadera)
            else if (prefab.Contains("terminal") || prefab.Contains("computer") || prefab.Contains("button") || prefab.Contains("switch") || prefab.Contains("controlpanel"))
            {
                lastMonumentCallerID = player.userID;
                lastMonumentCallTime = Time.realtimeSinceStartup;
            }

            return null;
        }

        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return null;

            bool isBradley = entity is BradleyAPC && enableBradley;
            bool isHeli = entity is BaseHelicopter && enableHeli;

            if (!isBradley && !isHeli) return null;

            BasePlayer attacker = info.InitiatorPlayer;
            if (attacker == null) return null;

            ulong entId = entity.net.ID.Value;
            ulong attackerId = attacker.userID;
            ulong teamId = attacker.currentTeam;
            float currentTime = Time.realtimeSinceStartup;

            if (activeClaims.TryGetValue(entId, out ClaimData claim))
            {
                if (IsAuthorized(claim.OwnerID, claim.TeamID, attacker))
                {
                    claim.LastHitTime = currentTime;
                }
                else
                {
                    if (currentTime - claim.LastHitTime > ClaimTimeoutSeconds)
                    {
                        claim.OwnerID = attackerId;
                        claim.TeamID = teamId;
                        claim.LastHitTime = currentTime;
                        attacker.ChatMessage("<color=#ffaa00>Has reclamado este evento tras inactividad.</color>");
                    }
                    else
                    {
                        string ownerName = BasePlayer.FindByID(claim.OwnerID)?.displayName ?? $"Jugador ({claim.OwnerID})";
                        attacker.ChatMessage("<color=#ff0000>Este evento está siendo realizado por otro equipo.</color>");
                        ShowBlockedUI(attacker, $"⚠️ Evento en curso por {ownerName} / su equipo");
                        info.damageTypes.ScaleAll(0);
                        return true;
                    }
                }
            }
            else
            {
                activeClaims[entId] = new ClaimData { OwnerID = attackerId, TeamID = teamId, LastHitTime = currentTime };
                attacker.ChatMessage("<color=#00ff00>Has reclamado este evento.</color> Solo tú o tu grupo podéis dañarlo y lootearlo.");
            }

            return null;
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null) return;

            if ((entity is BradleyAPC && enableBradley) || (entity is BaseHelicopter && enableHeli))
            {
                ulong entId = entity.net.ID.Value;
                if (activeClaims.TryGetValue(entId, out ClaimData claim))
                {
                    pendingClaims.Add(new PendingCrateClaim
                    {
                        OwnerID = claim.OwnerID,
                        TeamID = claim.TeamID,
                        DeathTime = Time.realtimeSinceStartup,
                        Position = entity.transform.position,
                        IsHeli = entity is BaseHelicopter
                    });
                    activeClaims.Remove(entId);
                }

                pendingClaims.RemoveAll(p => Time.realtimeSinceStartup - p.DeathTime > ClaimTimeoutSeconds);
            }
        }

        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity == null) return;
            
            string prefab = entity.ShortPrefabName;
            float currentTime = Time.realtimeSinceStartup;

            if (prefab == "supply_signal" && enableSupplyDrop)
            {
                SupplySignal signal = entity as SupplySignal;
                if (signal != null)
                {
                    supplySignalThrowers[signal.net.ID.Value] = signal.OwnerID;
                }
            }

            if ((prefab == "heli_crate" && enableHeli) || (prefab == "bradley_crate" && enableBradley))
            {
                PendingCrateClaim bestClaim = null;

                foreach (var pending in pendingClaims)
                {
                    if (currentTime - pending.DeathTime > ClaimTimeoutSeconds) continue; 
                    
                    if (pending.IsHeli && prefab == "heli_crate")
                    {
                        bestClaim = pending;
                        break; 
                    }
                    else if (!pending.IsHeli && prefab == "bradley_crate")
                    {
                        if ((entity.transform.position - pending.Position).sqrMagnitude < BRADLEY_LINK_DISTANCE_SQR)
                        {
                            bestClaim = pending;
                            break;
                        }
                    }
                }

                if (bestClaim != null)
                {
                    crateClaims[entity.net.ID.Value] = new CrateClaimData 
                    { 
                        OwnerID = bestClaim.OwnerID, 
                        TeamID = bestClaim.TeamID,
                        SpawnTime = currentTime,
                        ExclusivityDuration = DefaultExclusivitySeconds
                    };
                }
            }
            // Entregas aéreas, Satélite y drops generados por la Excavadora
            else if ((prefab == "supply_drop" && enableSupplyDrop) || ((prefab.Contains("satellite") || prefab.Contains("launchsite") || prefab == "satellite_crate") && enableSatelliteEvent))
            {
                bool isSatellite = prefab.Contains("satellite") || prefab.Contains("launchsite") || prefab == "satellite_crate";
                ulong ownerId = 0;
                ulong teamId = 0;

                // 1. Verificar si fue llamado mediante bengala de suministros
                if (supplySignalThrowers.Count > 0)
                {
                    foreach (var kvp in supplySignalThrowers)
                    {
                        ownerId = kvp.Value;
                        break;
                    }
                }
                // 2. Si es una entrega de la excavadora y la sesión sigue activa
                else if (prefab == "supply_drop" && enableExcavator && currentTime <= excavatorSessionEndTime && activeExcavatorOwnerID != 0)
                {
                    ownerId = activeExcavatorOwnerID;
                }
                // 3. Verificar si un jugador usó una terminal de monumento recientemente (Airfield / Lanzadera)
                else if (Time.realtimeSinceStartup - lastMonumentCallTime < 30f && lastMonumentCallerID != 0)
                {
                    ownerId = lastMonumentCallerID;
                }

                if (ownerId != 0)
                {
                    BasePlayer ownerPlayer = BasePlayer.FindByID(ownerId);
                    if (ownerPlayer != null) teamId = ownerPlayer.currentTeam;

                    float duration = isSatellite ? SatelliteExclusivitySeconds : (activeExcavatorOwnerID == ownerId ? ExcavatorExclusivitySeconds : DefaultExclusivitySeconds);

                    crateClaims[entity.net.ID.Value] = new CrateClaimData
                    {
                        OwnerID = ownerId,
                        TeamID = teamId,
                        SpawnTime = currentTime,
                        ExclusivityDuration = duration
                    };
                }
            }
        }

        void OnCrateHack(HackableLockedCrate crate)
        {
            if (crate == null || !enableLockedCrate) return;

            ulong entityId = crate.net.ID.Value;
            if (!crateClaims.ContainsKey(entityId))
            {
                BasePlayer closestPlayer = null;
                float minDistance = float.MaxValue;

                foreach (var player in BasePlayer.activePlayerList)
                {
                    float dist = Vector3.Distance(player.transform.position, crate.transform.position);
                    if (dist < 5f && dist < minDistance)
                    {
                        minDistance = dist;
                        closestPlayer = player;
                    }
                }

                if (closestPlayer != null)
                {
                    crateClaims[entityId] = new CrateClaimData
                    {
                        OwnerID = closestPlayer.userID,
                        TeamID = closestPlayer.currentTeam,
                        SpawnTime = Time.realtimeSinceStartup,
                        ExclusivityDuration = LockedCrateExclusivitySeconds
                    };
                    closestPlayer.ChatMessage($"<color=#00ff00>Has iniciado el hackeo de esta caja.</color> Tienes {lockedCrateExclusivityMinutes} minutos de exclusividad.");
                }
            }
        }

        object CanLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (player == null || entity == null) return null;

            string prefab = entity.ShortPrefabName;
            ulong entityId = entity.net.ID.Value;
            float currentTime = Time.realtimeSinceStartup;

            bool isHeliOrBradleyCrate = (prefab == "heli_crate" && enableHeli) || (prefab == "bradley_crate" && enableBradley);
            bool isLockedCrate = prefab == "ch47_drop_crate" && enableLockedCrate;
            bool isSupplyDrop = prefab == "supply_drop" && enableSupplyDrop;
            bool isSatelliteCrate = (prefab.Contains("satellite") || prefab.Contains("launchsite") || prefab == "satellite_crate") && enableSatelliteEvent;

            if (!isHeliOrBradleyCrate && !isLockedCrate && !isSupplyDrop && !isSatelliteCrate) return null;

            // RED DE SEGURIDAD: Primer contacto si no tiene dueño previo
            if ((isLockedCrate || isSupplyDrop || isSatelliteCrate) && !crateClaims.ContainsKey(entityId))
            {
                float duration = isLockedCrate ? LockedCrateExclusivitySeconds : (isSatelliteCrate ? SatelliteExclusivitySeconds : DefaultExclusivitySeconds);
                float minutes = isLockedCrate ? lockedCrateExclusivityMinutes : (isSatelliteCrate ? satelliteExclusivityMinutes : defaultExclusivityMinutes);
                string eventName = isLockedCrate ? "la caja hackeable" : (isSatelliteCrate ? "el evento de satélite" : "la entrega aérea");

                crateClaims[entityId] = new CrateClaimData
                {
                    OwnerID = player.userID,
                    TeamID = player.currentTeam,
                    SpawnTime = currentTime,
                    ExclusivityDuration = duration
                };
                
                player.ChatMessage($"<color=#00ff00>Has reclamado {eventName}.</color> Tienes {minutes} minutos de exclusividad.");
                return null; 
            }

            if (crateClaims.TryGetValue(entityId, out CrateClaimData claim))
            {
                float timeSinceSpawn = currentTime - claim.SpawnTime;

                if (timeSinceSpawn <= claim.ExclusivityDuration)
                {
                    if (IsAuthorized(claim.OwnerID, claim.TeamID, player))
                    {
                        return null; 
                    }
                    else
                    {
                        float remainingSeconds = claim.ExclusivityDuration - timeSinceSpawn;
                        int minutes = (int)remainingSeconds / 60;
                        int seconds = (int)remainingSeconds % 60;

                        string ownerName = BasePlayer.FindByID(claim.OwnerID)?.displayName ?? $"Jugador ({claim.OwnerID})";

                        player.ChatMessage($"<color=#ff0000>No puedes lootear esto.</color> Exclusivo para {ownerName} / su equipo (Quedan {minutes:D2}:{seconds:D2}).");
                        ShowBlockedUI(player, $"⛔ Exclusivo para {ownerName} / equipo (Quedan {minutes:D2}:{seconds:D2})");
                        return false; 
                    }
                }
                else
                {
                    crateClaims.Remove(entityId);
                    return null;
                }
            }

            return null;
        }

        void OnEntityKill(BaseNetworkable entity)
        {
            if (entity == null) return;
            
            string prefab = entity.ShortPrefabName;
            if (prefab == "heli_crate" || prefab == "bradley_crate" || prefab == "ch47_drop_crate" || prefab == "supply_drop" || prefab.Contains("satellite") || prefab.Contains("launchsite"))
            {
                crateClaims.Remove(entity.net.ID.Value);
            }
            else if (prefab == "supply_signal")
            {
                supplySignalThrowers.Remove(entity.net.ID.Value);
            }
        }
        #endregion

        #region Administration Commands
        private BaseEntity GetLookEntity(BasePlayer player, float maxDistance = 10f)
        {
            RaycastHit hit;
            if (Physics.Raycast(player.eyes.HeadRay(), out hit, maxDistance))
            {
                return hit.GetEntity();
            }
            return null;
        }

        [ChatCommand("claimreset")]
        private void CmdClaimReset(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PERM_ADMIN))
            {
                player.ChatMessage("<color=#ff0000>No tienes permiso para usar este comando.</color>");
                return;
            }

            if (args != null && args.Length > 0 && args[0].ToLower() == "all")
            {
                activeClaims.Clear();
                pendingClaims.Clear();
                crateClaims.Clear();
                supplySignalThrowers.Clear();
                activeExcavatorOwnerID = 0;
                excavatorSessionEndTime = 0f;
                player.ChatMessage("<color=#00ff00>Se han reseteado TODAS las exclusividades activas del servidor.</color>");
                return;
            }

            BaseEntity entity = GetLookEntity(player);
            if (entity == null)
            {
                player.ChatMessage("<color=#ffaa00>No estás mirando ninguna entidad válida.</color>");
                return;
            }

            ulong entId = entity.net.ID.Value;
            bool removed = false;

            if (activeClaims.Remove(entId)) removed = true;
            if (crateClaims.Remove(entId)) removed = true;
            if (supplySignalThrowers.Remove(entId)) removed = true;

            if (removed)
            {
                player.ChatMessage($"<color=#00ff00>Exclusividad eliminada para [{entity.ShortPrefabName}] (ID: {entId}).</color>");
            }
            else
            {
                player.ChatMessage("<color=#ffaa00>La entidad que miras no tiene exclusividad activa.</color>");
            }
        }

        [ChatCommand("claiminfo")]
        private void CmdClaimInfo(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PERM_ADMIN))
            {
                player.ChatMessage("<color=#ff0000>No tienes permiso para usar este comando.</color>");
                return;
            }

            BaseEntity entity = GetLookEntity(player);
            if (entity == null)
            {
                player.ChatMessage("<color=#ffaa00>No estás mirando ninguna entidad válida.</color>");
                return;
            }

            ulong entId = entity.net.ID.Value;
            float currentTime = Time.realtimeSinceStartup;

            if (activeClaims.TryGetValue(entId, out ClaimData claim))
            {
                float activeTime = currentTime - claim.LastHitTime;
                string ownerName = BasePlayer.FindByID(claim.OwnerID)?.displayName ?? claim.OwnerID.ToString();
                player.ChatMessage($"<color=#00ffff>[EVENTO ACTIVO]</color> {entity.ShortPrefabName}\n" +
                                   $"Dueño: {ownerName} (ID: {claim.OwnerID}) | Team ID: {claim.TeamID}\n" +
                                   $"Último ataque hace: {Mathf.Max(0, (int)activeTime)}s");
                return;
            }

            if (crateClaims.TryGetValue(entId, out CrateClaimData crateClaim))
            {
                float elapsed = currentTime - crateClaim.SpawnTime;
                float remaining = crateClaim.ExclusivityDuration - elapsed;

                if (remaining > 0)
                {
                    int minutes = (int)remaining / 60;
                    int seconds = (int)remaining % 60;
                    string ownerName = BasePlayer.FindByID(crateClaim.OwnerID)?.displayName ?? crateClaim.OwnerID.ToString();
                    player.ChatMessage($"<color=#00ffff>[CAJA EXCLUSIVA]</color> {entity.ShortPrefabName}\n" +
                                       $"Dueño: {ownerName} (ID: {crateClaim.OwnerID}) | Team ID: {crateClaim.TeamID}\n" +
                                       $"Tiempo restante: {minutes:D2}:{seconds:D2}");
                }
                else
                {
                    player.ChatMessage("<color=#ffaa00>La exclusividad de esta caja ya ha expirado.</color>");
                }
                return;
            }

            player.ChatMessage("<color=#ffaa00>Esta entidad no tiene exclusividades registradas.</color>");
        }
        #endregion

        void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, UI_PANEL_NAME);
            }

            activeClaims.Clear();
            pendingClaims.Clear();
            crateClaims.Clear();
            supplySignalThrowers.Clear();
        }
    }
}