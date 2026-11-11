using System;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Oxide.Core;
using System.IO;
using UnityEngine.Networking;

namespace Oxide.Plugins
{
    [Info("Radtown Event", "☠ Ⓜ🅘ⓖ🅤ⓔ🅛 ☠", "2.4.3")]  
    [Description("Evento automatizado para Radtown con NPCs, caja hackeable, cajas élite, torretas, marcadores dinámicos y Helicópteros.")]
    public class MiRadTownEvent : RustPlugin
    {
        [PluginReference] private Plugin NpcSpawn;
        private PluginConfig config;
        private bool dev = false;
        
        private readonly List<BaseEntity> spawnedEntities = new List<BaseEntity>();
        private readonly HashSet<ulong> spawnedEntityIds = new HashSet<ulong>();
        private readonly List<BaseEntity> spawnedHelicopters = new List<BaseEntity>();
        
        private Coroutine spawnCoroutine;
        private MonumentInfo cachedRadtownMonument;
        private WaitForSeconds wait01 = new WaitForSeconds(0.1f);
        private WaitForSeconds wait005 = new WaitForSeconds(0.05f);

        private Timer eventTimer, autoStartTimer, heliTimer1, heliTimer2, heliDestinationTimer, markerUpdateTimer, mainLoopTimer, _uiUpdateTimer;

        private const float EventZoneRadius = 100f; 
        private readonly HashSet<ulong> playersInEventZone = new HashSet<ulong>();

        private int torretasVivas = 0;
        private int cajasVivas = 0;
        private int npcsVivos = 0;
        private bool isEventActive = false;
        private bool hasCrateBeenHacked = false;
        private float eventEndTime = 0f;

        private readonly List<string> availableWeapons = new List<string> { "smg.mp5", "rifle.ak", "rifle.lr300", "smg.thompson", "shotgun.pump", "lmg.m249", "rifle.bolt" };
        private readonly List<string> availableHelmets = new List<string> { "", "metal.facemask", "coffeecan.helmet", "heavy.plate.helmet", "hazmatsuit_scientist_nvgm", "scientistsuit_heavy", "deer.skull.mask" };
        private readonly List<string> availableTorsos = new List<string> { "", "metal.plate.torso", "roadsign.jacket", "jacket" };
        private readonly List<string> availableTorsos2 = new List<string> { "", "tshirt", "tshirt.long", "shirt.collared", "hoodie", "jacket" };
        private readonly List<string> availablePants = new List<string> { "", "roadsign.kilt" };
        private readonly List<string> availablePants2 = new List<string> { "", "pants.shorts", "attire.hide.pants", "pants", "burlap.trousers" };
        private readonly List<string> availableBoots = new List<string> { "", "shoes.boots", "burlap.shoes", "attire.hide.boots", "boots.frog" };
        private readonly List<string> availableGloves = new List<string> { "", "tactical.gloves", "burlap.gloves" };

        private const string BackgroundImageUrl = "https://raw.githubusercontent.com/miguelmacis-afk/rustImage/refs/heads/main/MiBackgroundImageName.png";
        private string imageId = "0";
        private int iconNpcId, iconTurretId, iconCrateId;
        private readonly Dictionary<ulong, string> _playerActiveNpcTab = new Dictionary<ulong, string>();

        private struct SpawnData
        {
            public Vector3 Pos;
            public Vector3 Rot;
            
            public SpawnData(Vector3 pos) { Pos = pos; Rot = Vector3.zero; }
            public SpawnData(Vector3 pos, Vector3 rot) { Pos = pos; Rot = rot; }
        }

        private readonly List<SpawnData> Event_Turrets = new List<SpawnData> 
        { 
            new SpawnData(new Vector3(31.41f, 3.83f, 14.58f), new Vector3(0f, 173.2f, 0f)),
            new SpawnData(new Vector3(29.8f, 1.01f, -19.42f), new Vector3(0f, 308.6f, 0f)),
            new SpawnData(new Vector3(-31.26f, 0.6f, -9.35f), new Vector3(0f, 58.1f, 0f)),
            new SpawnData(new Vector3(-14.97f, 4.88f, 10.54f), new Vector3(0f, 138.1f, 0f)),
            new SpawnData(new Vector3(-1.16f, 4.43f, -16.54f), new Vector3(0f, 182f, 0f)),
            new SpawnData(new Vector3(7.03f, 4.38f, -20.39f), new Vector3(0f, 227.1f, 0f))
        };

        private readonly Vector3 LocalCrateOffset = new Vector3(-12.97f, 0.04f, 7.48f);

        private readonly List<SpawnData> Event_EliteCrates = new List<SpawnData>
        {
            new SpawnData(new Vector3(-2.42f, 5.13f, -14.41f), new Vector3(0f, 358.1f, 0f)),
            new SpawnData(new Vector3(-5.69f, 4.5f, -14.69f), new Vector3(0f, 268.8f, 0f)),
            new SpawnData(new Vector3(2.97f, 4.38f, -14.84f), new Vector3(0f, 355.3f, 0f)),
            new SpawnData(new Vector3(0.36f, 4.38f, -13.34f), new Vector3(0f, 359.6f, 0f)),
            new SpawnData(new Vector3(21.32f, 1.48f, -5.05f), new Vector3(0f, 306.1f, 0f))
        };
        
        private List<Vector3> rutaBradley = new List<Vector3> 
        {
            new Vector3(-27.36f, 0.07f, -8.04f), 
            new Vector3(29.57f, -0.02f, 7.54f)
        };

        private readonly List<Vector3> Event_NPCs = new List<Vector3> 
        {
            new Vector3(25.59f, 7.85f, 15.08f), new Vector3(37.34f, 7.85f, 17.73f),
            new Vector3(-10.46f, 4.88f, 23.33f), new Vector3(-18.75f, 4.88f, 24.19f),
            new Vector3(-16.34f, 4.88f, 12.22f), new Vector3(-26.27f, 4.88f, 11.61f), 
            new Vector3(-42.97f, 4.88f, 11.45f), new Vector3(-47.73f, 6.71f, 5.86f), 
            new Vector3(-47.72f, 6.71f, -4.73f), new Vector3(22.5f, 0.63f, 30.79f),
            new Vector3(-31.56f, 0.03f, -17.8f), new Vector3(28.67f, 0.37f, 7.57f), 
            new Vector3(-2.47f, 0.63f, -15.82f), new Vector3(-3.61f, 4.38f, -16.86f), 
            new Vector3(30.09f, 0.15f, -32.18f), new Vector3(50.9f, 0.05f, -19.26f), 
            new Vector3(54.2f, 6.71f, -4.71f), new Vector3(54.21f, 6.71f, 5.99f),
            new Vector3(45.91f, 0.15f, 27.09f), new Vector3(21.18f, 0.14f, 25.51f), 
            new Vector3(-6.44f, 0.01f, 29.69f), new Vector3(39.89f, 5.79f, -20.54f),
            new Vector3(21.37f, 5.79f, -20.62f), new Vector3(0.74f, 8.74f, -13.41f),
            new Vector3(-31.9f, 0.03f, -19.62f), new Vector3(-13.18f, 0.2f, -24.54f), 
            new Vector3(-14.35f, 0.19f, 0.67f), new Vector3(-34.81f, 0.26f, 17.55f), 
            new Vector3(-19.42f, 0.05f, -8.96f)
        };

        private readonly List<SpawnData> Event_MetalDoor = new List<SpawnData> 
        { 
            new SpawnData(new Vector3(-5.68f, 4.27f, -19.73f), new Vector3(-1f, 266.6f, 0f)),
            new SpawnData(new Vector3(0.39f, 4.38f, -19.76f), new Vector3(0f, 267.1f, 0f)),
            new SpawnData(new Vector3(0.37f, 0.43f, -19.7f), new Vector3(0f, 273.3f, 0f)),
            new SpawnData(new Vector3(-1.36f, 0.53f, -20.47f), new Vector3(0f, 178.2f, 0f))
        };
 
        public class DifficultySettings
        {
            public string Name { get; set; } 
            public bool Enabled { get; set; }
            public float HealthMultiplier { get; set; }
            public float RangeMultiplier { get; set; }
            public float AimDamageMultiplier { get; set; }
            public bool Bradley { get; set; }
            public int BradleyCount { get; set; } 
            public bool HeliPatrol { get; set; }
            public bool HeliPatrol2 { get; set; }
            public int NPCs { get; set; }
            public int EliteFoxes { get; set; }
            public int HackCrates { get; set; }
            public ulong skinTurret { get; set; }
            public ulong skinDoor { get; set; }
            public string MarkerColor { get; set; }
        }
        
        private int CurrentDifficulty { get; set; } = 5; 
        
        private class PluginConfig
        {
            [JsonProperty("Ajustes de Dificultades")]
            public Dictionary<int, DifficultySettings> Difficulties { get; set; } 
            
            [JsonProperty("Activar Autostart Automático")]
            public bool EnableAutoStart { get; set; } = true;
            
            [JsonProperty("Activar Menu informacion del evento")]
            public bool EnableMenuInfo { get; set; } = true;
            
            [JsonProperty("Intervalo Mínimo Autostart (Horas)")]
            public float AutoStartIntervalHoursMin { get; set; } = 6f;

            [JsonProperty("Intervalo Máximo Autostart (Horas)")]
            public float AutoStartIntervalHoursMax { get; set; } = 12f;

            [JsonProperty("Duración del Evento (Minutos)")]
            public float EventDurationMinutes { get; set; } = 40f;

            [JsonProperty("Configuración de Perfiles de NPC")]
            public Dictionary<string, NpcProfile> NpcProfiles { get; set; } = new Dictionary<string, NpcProfile>
            {
                { "normal", new NpcProfile { Name = "Asalto", BaseHealth = 200f, Weapon = "rifle.ak", WeaponSkin = 3754181741, Helmet = "hazmatsuit_scientist_nvgm", HelmetSkin = 0 }},
                { "elite", new NpcProfile { Name = "Francotirador", BaseHealth = 250f, Weapon = "rifle.bolt", WeaponSkin = 3745658611, Helmet = "scientistsuit_heavy", HelmetSkin = 0 }},
                { "boss", new NpcProfile { Name = "Jefe Radtown", BaseHealth = 300f, Weapon = "lmg.m249", WeaponSkin = 2825534972, Helmet = "hazmatsuit_scientist_nvgm", HelmetSkin = 0 }}
            };
        }

        public class NpcProfile
        {
            public string Name { get; set; } public float BaseHealth { get; set; }
            public string Weapon { get; set; } public ulong WeaponSkin { get; set; }
            public string Helmet { get; set; } public ulong HelmetSkin { get; set; }
            public string Torso { get; set; } public ulong TorsoSkin { get; set; }
            public string Torso2 { get; set; } public ulong Torso2Skin { get; set; }
            public string Pants { get; set; } public ulong PantsSkin { get; set; }
            public string Pants2 { get; set; } public ulong Pants2Skin { get; set; }
            public string Boots { get; set; } public ulong BootsSkin { get; set; }
            public string Gloves { get; set; } public ulong GlovesSkin { get; set; }
        }

        protected override void LoadDefaultConfig()
        {
            config = new PluginConfig
            {
                Difficulties = new Dictionary<int, DifficultySettings>
                {
                    { 0, new DifficultySettings { Name = "easy", Enabled = true, Bradley = false, BradleyCount = 1, HeliPatrol = false, HeliPatrol2 = false, HealthMultiplier = 0.5f, RangeMultiplier = 0.5f, AimDamageMultiplier = 0.5f, NPCs = 10, HackCrates = 1, EliteFoxes = 1, skinTurret = 3732045248, skinDoor = 2940068880, MarkerColor = "#3cd834" } },
                    { 1, new DifficultySettings { Name = "normal", Enabled = true, Bradley = true, BradleyCount = 1, HeliPatrol = false, HeliPatrol2 = false, HealthMultiplier = 0.7f, RangeMultiplier = 0.7f, AimDamageMultiplier = 0.7f, NPCs = 15, HackCrates = 1, EliteFoxes = 1, skinTurret = 3732045248, skinDoor = 2940068880, MarkerColor = "#e57109" } },
                    { 2, new DifficultySettings { Name = "medium", Enabled = true, Bradley = true, BradleyCount = 1, HeliPatrol = true, HeliPatrol2 = false, HealthMultiplier = 0.9f, RangeMultiplier = 0.9f, AimDamageMultiplier = 0.9f, NPCs = 20, HackCrates = 1, EliteFoxes = 1, skinTurret = 3732045248, skinDoor = 2940068880, MarkerColor = "#e7c50a" } },
                    { 3, new DifficultySettings { Name = "expert", Enabled = true, Bradley = true, BradleyCount = 1, HeliPatrol = true, HeliPatrol2 = true, HealthMultiplier = 1.1f, RangeMultiplier = 1.1f, AimDamageMultiplier = 1.1f, NPCs = 25, HackCrates = 1, EliteFoxes = 2, skinTurret = 3732045248, skinDoor = 2940068880, MarkerColor = "#0000FF" } },
                    { 4, new DifficultySettings { Name = "hard", Enabled = true, Bradley = true, BradleyCount = 1, HeliPatrol = true, HeliPatrol2 = true, HealthMultiplier = 1.2f, RangeMultiplier = 1.2f, AimDamageMultiplier = 1.2f, NPCs = 27, HackCrates = 2, EliteFoxes = 3, skinTurret = 3732045248, skinDoor = 2940068880, MarkerColor = "#9c1825" } },
                    { 5, new DifficultySettings { Name = "nightmare", Enabled = true, Bradley = true, BradleyCount = 1, HeliPatrol = true, HeliPatrol2 = true, HealthMultiplier = 1.3f, RangeMultiplier = 1.3f, AimDamageMultiplier = 1.3f, NPCs = 29, HackCrates = 2, EliteFoxes = 4, skinTurret = 3732445397, skinDoor = 2940068880, MarkerColor = "#9236f5" } }
                }
            };
            SaveConfig();
        } 

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try {
                config = Config.ReadObject<PluginConfig>();
                if (config == null) LoadDefaultConfig();
            } catch {
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(config);
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["EventAlreadyActive"] = "<color=#ff4d4d>[RADTOWN]</color> <color=#ffffff>The event is already active.</color>",
                ["ErrorMonument"] = "<color=#ff4d4d>[ERROR]</color> <color=#ffffff>Radtown monument not found on this map.</color>",
                ["EventWarning"] = "<color=#ff4d4d>[RADTOWN]</color> <color=#ffffff>Radtown will be taken by </color><color=#ff4d4d>hostile forces</color><color=#ffffff>, stay away!</color>",
                ["EventStarted"] = "<color=#ff4d4d>[RADTOWN]</color> <color=#ffffff>Radtown has been captured by </color><color=#ff4d4d>hostile forces</color><color=#ffffff>!</color>",
                ["EventDifficulty"] = "<color=#ff4d4d>[RADTOWN]</color> <color=#ffffff>Event Difficulty: </color><color={0}>{1}</color>",
                ["EventEnded"] = "<color=#ff4d4d>[RADTOWN]</color> <color=#ffffff>The event time is over. Cleaning up area...</color>",
                ["EventCancelled"] = "<color=#ff4d4d>[RADTOWN]</color> <color=#ffffff>Event cancelled manually. The loot has been </color><color=#ff4d4d>destroyed</color><color=#ffffff>.</color>",
                ["NoActiveEvent"] = "<color=#ff4d4d>[RADTOWN]</color> <color=#ffffff>There is no active event.</color>",
                ["EventStoppedManual"] = "<color=#ff4d4d>[RADTOWN]</color> <color=#ffffff>Event manually stopped.</color>",
                ["CmdUsage"] = "<color=#ffeb3b>[HELP]</color> <color=#ffffff>Usage: </color><color=#4dff4d>/radtown start</color><color=#ffffff> or </color><color=#ff4d4d>/radtown stop</color>",
                ["NextEventTime"] = "<color=#ffeb3b>[RADTOWN]</color> <color=#ffffff>Next event in: </color><color=#4dff4d>{0}</color>",
                ["HeliApproaching"] = "<color=#ff4d4d>[RADTOWN]</color> <color=#ffffff>A heavy patrol helicopter is approaching to protect Radtown!</color>",
                ["HeliReinforcement"] = "<color=#ff4d4d>[RADTOWN]</color> <color=#ffffff>High threat level! A second reinforcement helicopter is closing in!</color>"
            }, this, "en");

            Dictionary<string, string> Es = new Dictionary<string, string>
            {
                ["EventAlreadyActive"] = "<color=#ff4d4d>[RADTOWN]</color> <color=#ffffff>El evento ya está activo.</color>",
                ["ErrorMonument"] = "<color=#ff4d4d>[ERROR]</color> <color=#ffffff>No se encontró el monumento Radtown en este mapa.</color>",
                ["EventWarning"] = "<color=#ff4d4d>[RADTOWN]</color> <color=#ffffff>¡Radtown será tomada por </color><color=#ff4d4d>fuerzas hostiles</color><color=#ffffff>, aléjate si no quieres morir!</color>",
                ["EventStarted"] = "<color=#ff4d4d>[RADTOWN]</color> <color=#ffffff>¡Radtown ha sido capturada por </color><color=#ff4d4d>fuerzas hostiles</color><color=#ffffff>!</color>",
                ["EventDifficulty"] = "<color=#ff4d4d>[RADTOWN]</color> <color=#ffffff>Dificultad del evento: </color><color={0}>{1}</color>",
                ["EventEnded"] = "<color=#ff4d4d>[RADTOWN]</color> <color=#ffffff>El tiempo del evento ha terminado. Limpiando zona...</color>",
                ["EventCancelled"] = "<color=#ff4d4d>[RADTOWN]</color> <color=#ffffff>Evento cancelado manualmente. El botín se ha </color><color=#ff4d4d>destruido</color><color=#ffffff>.</color>",
                ["NoActiveEvent"] = "<color=#ff4d4d>[RADTOWN]</color> <color=#ffffff>No hay evento activo.</color>",
                ["EventStoppedManual"] = "<color=#ff4d4d>[RADTOWN]</color> <color=#ffffff>Evento detenido manualmente.</color>",
                ["CmdUsage"] = "<color=#ffeb3b>[AYUDA]</color> <color=#ffffff>Uso: </color><color=#4dff4d>/radtown start [Dificultad(0-5)]</color><color=#ffffff> o </color><color=#ff4d4d>/radtown stop</color>",
                ["NextEventTime"] = "<color=#ffeb3b>[RADTOWN]</color> <color=#ffffff>Próximo evento en: </color><color=#4dff4d>{0}</color>",
                ["HeliApproaching"] = "<color=#ff4d4d>[RADTOWN]</color> <color=#ffffff>¡Un helicóptero de patrulla pesado se aproxima para proteger Radtown!</color>",
                ["HeliReinforcement"] = "<color=#ff4d4d>[RADTOWN]</color> <color=#ffffff>¡Nivel de amenaza alto! ¡Un helicóptero de refuerzo se aproxima a Radtown!</color>"
            };
            lang.RegisterMessages(Es, this, "es");
            lang.RegisterMessages(Es, this, "es-ES");
        }

        private void SendMsg(BasePlayer player, string key, params object[] args)
        {
            if (player == null) return;
            string message = lang.GetMessage(key, this, player.UserIDString);
            if (args != null && args.Length > 0) message = string.Format(message, args);
            player.ChatMessage(message);
        }

        private void BroadcastMsg(string key, params object[] args)
        {
            for (int i = 0; i < BasePlayer.activePlayerList.Count; i++)
            {
                BasePlayer player = BasePlayer.activePlayerList[i];
                if (player != null && player.IsConnected) 
                {
                    SendMsg(player, key, args);
                }
            }
        }

        private void CacheRadtownMonument()
        {
            if (TerrainMeta.Path != null && TerrainMeta.Path.Monuments != null)
            {
                foreach (var m in TerrainMeta.Path.Monuments)
                {
                    if (m != null && m.name.ToLower().Contains("radtown_1"))
                    {
                        cachedRadtownMonument = m;
                        Puts("Monumento radtown_1 encontrado y cacheado.");
                        break;
                    }
                }
            }
            if (cachedRadtownMonument == null) 
            {
                Puts("[ERROR] No se encontró el monumento 'radtown_1' en este mapa. PLUGIN DESCARTADO"); 
                rust.RunServerCommand("oxide.unload MiRadTownEvent");
            }
        }

        private void OnServerInitialized()
        {
            CacheRadtownMonument(); ScheduleAutoStart();
            iconNpcId = ItemManager.FindItemDefinition("skull.human")?.itemid ?? 0;
            iconTurretId = ItemManager.FindItemDefinition("autoturret")?.itemid ?? 0;
            iconCrateId = ItemManager.FindItemDefinition("box.wooden")?.itemid ?? 0;
            LoadImageLocally();
        }
		private void LoadImageLocally()
		{
			string dataPath = Path.Combine(Interface.Oxide.DataDirectory, "Images");
			
			if (!Directory.Exists(dataPath)) Directory.CreateDirectory(dataPath);
			string fullPath = Path.Combine(dataPath, "MiBackgroundImage.png");

			if (File.Exists(fullPath))
			{
				byte[] data = File.ReadAllBytes(fullPath);
				RegisterImage(data);
			}
			else
			{
				ServerMgr.Instance.StartCoroutine(DownloadImage(fullPath));
			}
		}

		private IEnumerator DownloadImage(string savePath)
		{
			using (UnityWebRequest www = UnityWebRequest.Get(BackgroundImageUrl))
			{
				www.SetRequestHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
				yield return www.SendWebRequest();

				if (www.result == UnityWebRequest.Result.Success)
				{
					byte[] bytes = www.downloadHandler.data;
					File.WriteAllBytes(savePath, bytes);
					RegisterImage(bytes);
				}
			}
		}

		private void RegisterImage(byte[] data)
		{
			uint crc = FileStorage.server.StoreByCrc(CommunityEntity.ServerInstance.net.ID, FileStorage.Type.png, data);
			imageId = crc.ToString();
		}

        private float nextEventTimestamp = 0f;
        
        private void ScheduleAutoStart()
        {
            if (config.AutoStartIntervalHoursMax <= 0) return;

            float randomHours = UnityEngine.Random.Range(config.AutoStartIntervalHoursMin, config.AutoStartIntervalHoursMax);
            float intervalSeconds = randomHours * 3600f;
            nextEventTimestamp = UnityEngine.Time.realtimeSinceStartup + intervalSeconds;
            Puts($"Próximo evento automático en {randomHours:F2} horas.");

            autoStartTimer?.Destroy();
            autoStartTimer = timer.Once(intervalSeconds, () =>
            {
                StartRadtownEvent(null, -1);
                ScheduleAutoStart();
            });
        }

        private BaseEntity redCircleMarker;
        private BaseEntity shopMapMarker;

        private void OnPlayerConnected(BasePlayer player)
        {
            if (!isEventActive || redCircleMarker == null || player == null) return;

            if (player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot))
            {
                timer.In(2f, () => OnPlayerConnected(player));
                return;
            }

            MapMarkerGenericRadius radiusMarker = redCircleMarker as MapMarkerGenericRadius;
            radiusMarker?.SendUpdate();
        }

        private void CreateEventMapMarkers(Vector3 position)
        {
            redCircleMarker = GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab", position);
            MapMarkerGenericRadius radiusMarker = redCircleMarker as MapMarkerGenericRadius;
            if (radiusMarker != null)
            {
                radiusMarker.Spawn();
                radiusMarker.radius = 1.0f;       
                radiusMarker.alpha = 0.35f;       
                ColorUtility.TryParseHtmlString(config.Difficulties[CurrentDifficulty].MarkerColor, out Color markerColor);

                radiusMarker.color1 = markerColor;  
                radiusMarker.color2 = markerColor;  
                
                radiusMarker.SendUpdate();
            }

            shopMapMarker = GameManager.server.CreateEntity("assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab", position);
            if (shopMapMarker != null)
            {
                VendingMachineMapMarker vendingMarker = shopMapMarker as VendingMachineMapMarker;
                if (vendingMarker != null)
                {
                    vendingMarker.markerShopName = "★ EVENTO: RADTOWN ★";
                    vendingMarker.flags |= BaseEntity.Flags.On;
                }
                
                shopMapMarker.Spawn();
                vendingMarker?.SendNetworkUpdate(); 
            }
        }

        private void KillAllTimers() 
        { 
            eventTimer?.Destroy(); 
            autoStartTimer?.Destroy(); 
            heliTimer1?.Destroy(); 
            heliTimer2?.Destroy(); 
            heliDestinationTimer?.Destroy(); 
            markerUpdateTimer?.Destroy(); 
            mainLoopTimer?.Destroy(); 
            _uiUpdateTimer?.Destroy(); 
        }

        public void RegisterEntity(BaseEntity entity, bool isNpc = false)
        {
            if (entity == null) return;
            spawnedEntities.Add(entity);
            spawnedEntityIds.Add(entity.net.ID.Value);
            
            if (isNpc || entity is NPCPlayer || entity is BaseNpc) npcsVivos++;
            else if (entity is AutoTurret) torretasVivas++;
            else if (entity is HackableLockedCrate || entity is LootContainer) cajasVivas++;
            
            QueueUIUpdate(); 
        }

        private void Unload()
        {
            CleanUpEvent(true); 
            KillAllTimers();
            if (spawnCoroutine != null) { ServerMgr.Instance.StopCoroutine(spawnCoroutine); spawnCoroutine = null; }
            foreach (var player in BasePlayer.activePlayerList) {
                CuiHelper.DestroyUi(player, "RadtownEventUI");
                CuiHelper.DestroyUi(player, "RadtownEventUI_Dynamic");
                CuiHelper.DestroyUi(player, "RadtownEventUI_Clock");
                CuiHelper.DestroyUi(player, "RadtownAdminUI");
            }
        }

        private void QueueUIUpdate()
        {
            if (_uiUpdateTimer != null && !_uiUpdateTimer.Destroyed) return;
            _uiUpdateTimer = timer.Once(0.5f, UpdateEventCUIForce);
        }

        private void OnEntityKill(BaseNetworkable netEntity)
        {
            if (!isEventActive || netEntity == null) return;
            if (spawnedEntityIds.Contains(netEntity.net.ID.Value)) {
                if (netEntity is NPCPlayer || netEntity is BaseNpc) npcsVivos--;
                else if (netEntity is AutoTurret) torretasVivas--;
                else if (netEntity is HackableLockedCrate || netEntity is LootContainer) cajasVivas--;
                
                QueueUIUpdate();
            }
        }

        private void MainEventLoop()
        {
            if (!isEventActive || cachedRadtownMonument == null) return;
            
            float remainingSeconds = Mathf.Max(eventEndTime - UnityEngine.Time.realtimeSinceStartup, 0f);
            TimeSpan timeSpan = TimeSpan.FromSeconds(remainingSeconds);
            string timeString = timeSpan.TotalHours >= 1 ? string.Format("{0:00}:{1:00}:{2:00}", (int)timeSpan.TotalHours, timeSpan.Minutes, timeSpan.Seconds) : string.Format("{0:00}:{1:00}", timeSpan.Minutes, timeSpan.Seconds);
            
            Vector3 monumentPos = cachedRadtownMonument.transform.position;
            float radiusSqr = EventZoneRadius * EventZoneRadius;
            string borderCuiColor = HexToCuiColor(config.Difficulties[CurrentDifficulty].MarkerColor, 0.85f);
            
            for (int i = 0; i < BasePlayer.activePlayerList.Count; i++)
            {
                BasePlayer player = BasePlayer.activePlayerList[i];
                if (player == null || !player.IsConnected) continue;

                bool isInZone = (player.transform.position - monumentPos).sqrMagnitude <= radiusSqr;
                bool hasUi = playersInEventZone.Contains(player.userID);

                if (isInZone) {
                    if (!hasUi) {
                        playersInEventZone.Add(player.userID);
                        if (config.EnableMenuInfo) {
                            DrawEventUI_Static(player, borderCuiColor);
                            DrawEventUI_Dynamic(player, Mathf.Max(0, npcsVivos), Mathf.Max(0, torretasVivas), Mathf.Max(0, cajasVivas));
                        }
                    }
                    if (config.EnableMenuInfo) DrawClockCUI(player, timeString);
                }
                else if (hasUi) {
                    playersInEventZone.Remove(player.userID);
                    if (config.EnableMenuInfo) {
                        CuiHelper.DestroyUi(player, "RadtownEventUI");
                        CuiHelper.DestroyUi(player, "RadtownEventUI_Dynamic");
                        CuiHelper.DestroyUi(player, "RadtownEventUI_Clock");
                    }
                }
            }
        }

        private void UpdateEventCUIForce()
        {
            if (!isEventActive || !config.EnableMenuInfo) return;
            for (int i = 0; i < BasePlayer.activePlayerList.Count; i++)
            {
                BasePlayer p = BasePlayer.activePlayerList[i];
                if (p != null && playersInEventZone.Contains(p.userID)) {
                    DrawEventUI_Dynamic(p, Mathf.Max(0, npcsVivos), Mathf.Max(0, torretasVivas), Mathf.Max(0, cajasVivas));
                }
            }
        }

        private void DrawEventUI_Static(BasePlayer player, string colorBorde)
        {
            CuiHelper.DestroyUi(player, "RadtownEventUI"); var container = new CuiElementContainer();
            container.Add(new CuiPanel { Image = { Color = "0 0 0 0" }, RectTransform = { AnchorMin = "0.36 0.90", AnchorMax = "0.64 0.93" } }, "Hud", "RadtownEventUI");
            container.Add(new CuiPanel { Image = { Color = colorBorde }, RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" } }, "RadtownEventUI", "RadtownEventUI_ColorBg"); 
            container.Add(new CuiElement { Name = "RadtownEventUI_ImageBg", Parent = "RadtownEventUI_ColorBg", Components = { new CuiRawImageComponent { Png = imageId, Color = "1 1 1 0.8" }, new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" } } });
            container.Add(new CuiElement { Parent = "RadtownEventUI", Components = { new CuiImageComponent { ItemId = iconNpcId }, new CuiRectTransformComponent { AnchorMin = "0.28 0.15", AnchorMax = "0.36 0.85" } } });
            container.Add(new CuiElement { Parent = "RadtownEventUI", Components = { new CuiImageComponent { ItemId = iconTurretId }, new CuiRectTransformComponent { AnchorMin = "0.53 0.15", AnchorMax = "0.61 0.85" } } });
            container.Add(new CuiElement { Parent = "RadtownEventUI", Components = { new CuiImageComponent { ItemId = iconCrateId }, new CuiRectTransformComponent { AnchorMin = "0.78 0.15", AnchorMax = "0.86 0.85" } } });
            CuiHelper.AddUi(player, container);
        } 

        private void DrawEventUI_Dynamic(BasePlayer player, int npcs, int torretas, int cajas)
        {
            CuiHelper.DestroyUi(player, "RadtownEventUI_Dynamic"); var container = new CuiElementContainer();
            container.Add(new CuiPanel { Image = { Color = "0 0 0 0" }, RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" } }, "RadtownEventUI", "RadtownEventUI_Dynamic");
            container.Add(new CuiElement { Parent = "RadtownEventUI_Dynamic", Components = { new CuiTextComponent { Text = $"<color=#ff4d4d>{npcs}</color>", FontSize = 16, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-bold.ttf" }, new CuiOutlineComponent { Distance = "1 -1", Color = "0 0 0 1" }, new CuiRectTransformComponent { AnchorMin = "0.38 0", AnchorMax = "0.50 1" } } });
            container.Add(new CuiElement { Parent = "RadtownEventUI_Dynamic", Components = { new CuiTextComponent { Text = $"<color=#ffeb3b>{torretas}</color>", FontSize = 16, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-bold.ttf" }, new CuiOutlineComponent { Distance = "1 -1", Color = "0 0 0 1" }, new CuiRectTransformComponent { AnchorMin = "0.63 0", AnchorMax = "0.75 1" } } });
            container.Add(new CuiElement { Parent = "RadtownEventUI_Dynamic", Components = { new CuiTextComponent { Text = $"<color=#4dff4d>{cajas}</color>", FontSize = 16, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-bold.ttf" }, new CuiOutlineComponent { Distance = "1 -1", Color = "0 0 0 1" }, new CuiRectTransformComponent { AnchorMin = "0.88 0", AnchorMax = "1 1" } } });
            CuiHelper.AddUi(player, container);
        }

        private void DrawClockCUI(BasePlayer player, string timeString = "00:00")
        {
            CuiHelper.DestroyUi(player, "RadtownEventUI_Clock"); var container = new CuiElementContainer();
            container.Add(new CuiPanel { Image = { Color = "0 0 0 0" }, RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" } }, "RadtownEventUI", "RadtownEventUI_Clock");
            container.Add(new CuiElement { Parent = "RadtownEventUI_Clock", Components = { new CuiTextComponent { Text = $"<color=#ffffff>⏳ {timeString}</color>", FontSize = 15, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" }, new CuiOutlineComponent { Distance = "1 -1", Color = "0 0 0 1" }, new CuiRectTransformComponent { AnchorMin = "0.02 0", AnchorMax = "0.26 1" } } });
            CuiHelper.AddUi(player, container);
        }

        private string HexToCuiColor(string hex, float alpha = 1.0f) {
            if (string.IsNullOrEmpty(hex)) return $"1 1 1 {alpha}"; hex = hex.Replace("#", "");
            if (hex.Length == 6) { try { float r = Convert.ToInt32(hex.Substring(0, 2), 16) / 255f; float g = Convert.ToInt32(hex.Substring(2, 2), 16) / 255f; float b = Convert.ToInt32(hex.Substring(4, 2), 16) / 255f; return $"{r} {g} {b} {alpha}"; } catch { return $"1 1 1 {alpha}"; } }
            return $"1 1 1 {alpha}";
        }
        
        private string GetNextItem(List<string> itemsList, string currentItem) { int currentIdx = itemsList.IndexOf(currentItem); return itemsList[(currentIdx + 1) >= itemsList.Count ? 0 : currentIdx + 1]; }

        [ConsoleCommand("radtown.cui")]
        private void CmdCuiAdmin(ConsoleSystem.Arg arg) 
        {
            var player = arg.Player(); if (player == null || !player.IsAdmin) return;
            string action = arg.GetString(0);
            if (action == "close") { CuiHelper.DestroyUi(player, "RadtownAdminUI"); return; }
            int diffId = arg.GetInt(1, 0); DifficultySettings diffSettings = null;
            if (diffId >= 0 && config.Difficulties.ContainsKey(diffId)) diffSettings = config.Difficulties[diffId];
            bool isRefresh = action != "view"; 

            switch (action) {
                case "view": break; 
                case "toggle_enabled": if (diffSettings != null) diffSettings.Enabled = !diffSettings.Enabled; break;
                case "toggle_bradley": if (diffSettings != null) diffSettings.Bradley = !diffSettings.Bradley; break;
                case "toggle_heli": if (diffSettings != null) diffSettings.HeliPatrol = !diffSettings.HeliPatrol; break;
                case "toggle_heli2": if (diffSettings != null) diffSettings.HeliPatrol2 = !diffSettings.HeliPatrol2; break;
                case "add_bradley": if (diffSettings != null) diffSettings.BradleyCount = Mathf.Clamp(diffSettings.BradleyCount + arg.GetInt(2), 0, 4); break;
                case "add_health": if (diffSettings != null) diffSettings.HealthMultiplier = Mathf.Clamp(diffSettings.HealthMultiplier + arg.GetFloat(2), 0.1f, 10f); break;
                case "add_dmg": if (diffSettings != null) diffSettings.AimDamageMultiplier = Mathf.Clamp(diffSettings.AimDamageMultiplier + arg.GetFloat(2), 0.1f, 10f); break;
                case "add_hackcrates": if (diffSettings != null) diffSettings.HackCrates = Mathf.Clamp(diffSettings.HackCrates + arg.GetInt(2), 0, 4); break;
                case "add_npc": if (diffSettings != null) diffSettings.NPCs = Mathf.Clamp(diffSettings.NPCs + arg.GetInt(2), 0, 29); break; 
                case "add_foxes": if (diffSettings != null) diffSettings.EliteFoxes = Mathf.Clamp(diffSettings.EliteFoxes + arg.GetInt(2), 0, 5); break; 
                case "toggle_autostart": config.EnableAutoStart = !config.EnableAutoStart; break;
                case "toggle_menuinfo": config.EnableMenuInfo = !config.EnableMenuInfo; destroyallcui(); break;
                case "set_turretskin": if (diffSettings != null) { ulong.TryParse(arg.GetString(2), out ulong newSkin); diffSettings.skinTurret = newSkin; } break;
                case "set_doorskin": if (diffSettings != null) { ulong.TryParse(arg.GetString(2), out ulong newSkin); diffSettings.skinDoor = newSkin; } break;
                case "add_automin": config.AutoStartIntervalHoursMin = Mathf.Max(0f, config.AutoStartIntervalHoursMin + arg.GetFloat(2)); break;
                case "add_automax": config.AutoStartIntervalHoursMax = Mathf.Max(0f, config.AutoStartIntervalHoursMax + arg.GetFloat(2)); break;
                case "add_duration": config.EventDurationMinutes = Mathf.Max(1f, config.EventDurationMinutes + arg.GetFloat(2)); break;
                case "npc_health": if (config.NpcProfiles.ContainsKey(arg.GetString(2))) config.NpcProfiles[arg.GetString(2)].BaseHealth = Mathf.Max(1f, config.NpcProfiles[arg.GetString(2)].BaseHealth + arg.GetFloat(3)); break;
                case "npc_cycle_item":
                    if (config.NpcProfiles.ContainsKey(arg.GetString(2))) {
                        var npc = config.NpcProfiles[arg.GetString(2)];
                        switch (arg.GetString(3)) {
                            case "weapon": npc.Weapon = GetNextItem(availableWeapons, npc.Weapon); break;
                            case "helmet": npc.Helmet = GetNextItem(availableHelmets, npc.Helmet); break;
                            case "torso":  npc.Torso  = GetNextItem(availableTorsos,  npc.Torso); break;
                            case "torso2": npc.Torso2 = GetNextItem(availableTorsos2, npc.Torso2); break;
                            case "pants":  npc.Pants  = GetNextItem(availablePants,  npc.Pants); break;
                            case "pants2": npc.Pants2 = GetNextItem(availablePants2, npc.Pants2); break;
                            case "boots":  npc.Boots  = GetNextItem(availableBoots,  npc.Boots); break;
                            case "gloves": npc.Gloves = GetNextItem(availableGloves, npc.Gloves); break;
                        }
                    }
                    break;
                case "npc_set_skin":
                    if (config.NpcProfiles.ContainsKey(arg.GetString(2))) {
                        var npc = config.NpcProfiles[arg.GetString(2)]; ulong.TryParse(arg.GetString(4), out ulong newSkin); 
                        switch (arg.GetString(3)) {
                            case "weapon": npc.WeaponSkin = newSkin; break; case "helmet": npc.HelmetSkin = newSkin; break;
                            case "torso":  npc.TorsoSkin  = newSkin; break; case "torso2": npc.Torso2Skin = newSkin; break;
                            case "pants":  npc.PantsSkin  = newSkin; break; case "pants2": npc.Pants2Skin = newSkin; break;
                            case "boots":  npc.BootsSkin  = newSkin; break; case "gloves": npc.GlovesSkin = newSkin; break;
                        }
                    }
                    break;
                case "npc_tab": _playerActiveNpcTab[player.userID] = arg.GetString(2); break;
                case "save": SaveConfig(); player.ChatMessage("<color=#4dff4d>[Radtown]</color> Configuración guardada correctamente."); break;
            }
            OpenAdminMenu(player, diffId, isRefresh); 
        }

        private void OpenAdminMenu(BasePlayer player, int diffId, bool isRefresh = false)
        {
            if (!isRefresh) {
                CuiHelper.DestroyUi(player, "RadtownAdminUI"); var baseContainer = new CuiElementContainer();
                baseContainer.Add(new CuiPanel { Image = { Color = "0.08 0.08 0.09 0.98" }, RectTransform = { AnchorMin = "0.15 0.15", AnchorMax = "0.85 0.85" }, CursorEnabled = true }, "Overlay", "RadtownAdminUI");
                baseContainer.Add(new CuiElement { Parent = "RadtownAdminUI", Components = { new CuiTextComponent { Text = "⚙☢☢ EVENTO RADTOWN - CONFIGURACIÓN ☢☢⚙", FontSize = 20, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", Color = "#e5a93c" }, new CuiOutlineComponent { Distance = "1 -1", Color = "0 0 0 0.8" }, new CuiRectTransformComponent { AnchorMin = "0 0.9", AnchorMax = "1 1" } } });
                baseContainer.Add(new CuiButton { Button = { Command = "radtown.cui close", Color = "0.70 0.22 0.22 1" }, RectTransform = { AnchorMin = "0.95 0.91", AnchorMax = "0.98 0.97" }, Text = { Text = "X", FontSize = 14, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" } }, "RadtownAdminUI");
                baseContainer.Add(new CuiButton { Button = { Command = $"radtown.cui save {diffId}", Color = "0.18 0.58 0.32 1" }, RectTransform = { AnchorMin = "0.80 0.91", AnchorMax = "0.94 0.97" }, Text = { Text = "GUARDAR", FontSize = 11, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" } }, "RadtownAdminUI");
				baseContainer.Add(new CuiPanel { Image = { Color = "0.05 0.05 0.06 1" }, RectTransform = { AnchorMin = "0.02 0.05", AnchorMax = "0.25 0.88" } }, "RadtownAdminUI", "RadtownAdminUI_Left");
                float currentY = 0.82f; float btnHeight = 0.10f; string basicColor = "0.68 0.38 0.16 1"; bool basicSelected = (diffId == -1);
                baseContainer.Add(new CuiButton { Button = { Command = "radtown.cui view -1", Color = basicSelected ? basicColor : "0.13 0.13 0.15 1" }, RectTransform = { AnchorMin = $"0.1 {currentY}", AnchorMax = $"0.9 {currentY + 0.10f}" }, Text = { Text = "AJUSTES BÁSICOS", FontSize = 12, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", Color = basicSelected ? "1 1 1 1" : basicColor } }, "RadtownAdminUI_Left");
                currentY -= 0.13f;

                foreach (var kvp in config.Difficulties) {
                    int id = kvp.Key; string name = kvp.Value.Name.ToUpper(); string diffColorRgba = HexToCuiColor(kvp.Value.MarkerColor); bool isSelected = (id == diffId);
                    string finalBgColor = isSelected ? diffColorRgba : "0.13 0.13 0.15 1"; string finalTextColor = isSelected ? "1 1 1 1" : diffColorRgba; float yMin = currentY; float yMax = yMin + btnHeight;
                    baseContainer.Add(new CuiButton { Button = { Command = $"radtown.cui view {id}", Color = finalBgColor }, RectTransform = { AnchorMin = $"0.1 {yMin}", AnchorMax = $"0.9 {yMax}" }, Text = { Text = name, FontSize = 12, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", Color = finalTextColor } }, "RadtownAdminUI_Left"); currentY -= (btnHeight + 0.015f);
                }
                baseContainer.Add(new CuiPanel { Image = { Color = "0.06 0.06 0.07 1" }, RectTransform = { AnchorMin = "0.27 0.05", AnchorMax = "0.98 0.88" } }, "RadtownAdminUI", "RadtownAdminUI_Right");
                CuiHelper.AddUi(player, baseContainer);
            }

            string contentPanelName = "RadtownAdminUI_Content"; CuiHelper.DestroyUi(player, contentPanelName);
            var contentContainer = new CuiElementContainer(); contentContainer.Add(new CuiPanel { Image = { Color = "0 0 0 0" }, RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" } }, "RadtownAdminUI_Right", contentPanelName);

            int rowIndex = 0; float rowSpacing = 0.085f;
            Action<string, string, string, string, string> DrawToggleRow = (label, valText, valColor, cmdToggle, desc) => { float yMax = 0.96f - (rowIndex * rowSpacing); float yMin = yMax - 0.07f; contentContainer.Add(new CuiElement { Parent = contentPanelName, Components = { new CuiTextComponent { Text = label, FontSize = 13, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-bold.ttf" }, new CuiOutlineComponent { Distance = "1 -1", Color = "0 0 0 0.6" }, new CuiRectTransformComponent { AnchorMin = $"0.04 {yMin}", AnchorMax = $"0.3 {yMax}" } } }); contentContainer.Add(new CuiButton { Button = { Command = cmdToggle, Color = valColor }, RectTransform = { AnchorMin = $"0.3 {yMin}", AnchorMax = $"0.45 {yMax}" }, Text = { Text = valText, FontSize = 11, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" } }, contentPanelName); contentContainer.Add(new CuiElement { Parent = contentPanelName, Components = { new CuiTextComponent { Text = desc, FontSize = 11, Color = "0.65 0.65 0.65 1", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf" }, new CuiOutlineComponent { Distance = "1 -1", Color = "0 0 0 0.4" }, new CuiRectTransformComponent { AnchorMin = $"0.48 {yMin}", AnchorMax = $"0.95 {yMax}" } } }); rowIndex++; };
            Action<string, string, string, string> DrawNumberRow = (label, valText, cmdMinus, cmdPlus) => { float yMax = 0.96f - (rowIndex * rowSpacing); float yMin = yMax - 0.07f; contentContainer.Add(new CuiElement { Parent = contentPanelName, Components = { new CuiTextComponent { Text = label, FontSize = 13, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-bold.ttf" }, new CuiOutlineComponent { Distance = "1 -1", Color = "0 0 0 0.6" }, new CuiRectTransformComponent { AnchorMin = $"0.04 {yMin}", AnchorMax = $"0.3 {yMax}" } } }); contentContainer.Add(new CuiButton { Button = { Command = cmdMinus, Color = "0.65 0.25 0.25 1" }, RectTransform = { AnchorMin = $"0.3 {yMin}", AnchorMax = $"0.35 {yMax}" }, Text = { Text = "-", FontSize = 14, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" } }, contentPanelName); string valBg = "valBg_" + rowIndex; contentContainer.Add(new CuiPanel { Image = { Color = "0.11 0.11 0.13 1" }, RectTransform = { AnchorMin = $"0.36 {yMin}", AnchorMax = $"0.44 {yMax}" } }, contentPanelName, valBg); contentContainer.Add(new CuiElement { Parent = valBg, Components = { new CuiTextComponent { Text = valText, FontSize = 12, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", Color = "#ffeb3b" }, new CuiOutlineComponent { Distance = "1 -1", Color = "0 0 0 0.5" }, new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" } } }); contentContainer.Add(new CuiButton { Button = { Command = cmdPlus, Color = "0.25 0.55 0.25 1" }, RectTransform = { AnchorMin = $"0.45 {yMin}", AnchorMax = $"0.5 {yMax}" }, Text = { Text = "+", FontSize = 14, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" } }, contentPanelName); rowIndex++; };

            if (diffId == -1) {
                DrawToggleRow("Temporizador Autostart:", config.EnableAutoStart ? "ACTIVADO" : "DESACTIVADO", config.EnableAutoStart ? "0.20 0.55 0.30 1" : "0.65 0.25 0.25 1", $"radtown.cui toggle_autostart {diffId}", "Habilita o deshabilita por completo que el evento se lance solo periódicamente."); DrawNumberRow("Autostart Mínimo (Horas):", config.AutoStartIntervalHoursMin.ToString("0.0"), $"radtown.cui add_automin {diffId} -0.5", $"radtown.cui add_automin {diffId} 0.5"); DrawNumberRow("Autostart Máximo (Horas):", config.AutoStartIntervalHoursMax.ToString("0.0"), $"radtown.cui add_automax {diffId} -0.5", $"radtown.cui add_automax {diffId} 0.5"); DrawNumberRow("Duración Evento (Minutos):", config.EventDurationMinutes.ToString("0.0"), $"radtown.cui add_duration {diffId} -5", $"radtown.cui add_duration {diffId} 5");
                DrawToggleRow("Menu info Evento:", config.EnableMenuInfo ? "ACTIVADO" : "DESACTIVADO", config.EnableMenuInfo ? "0.20 0.55 0.30 1" : "0.65 0.25 0.25 1", $"radtown.cui toggle_menuinfo {diffId}", "Habilita o deshabilita el menu de informacion en el evento."); 
                float npcStartY = 0.96f - (rowIndex * rowSpacing) - 0.01f; contentContainer.Add(new CuiElement { Parent = contentPanelName, Components = { new CuiTextComponent { Text = "⚔ PERSONALIZACIÓN DE NPC ⚔", FontSize = 11, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", Color = "#6aa1d4" }, new CuiOutlineComponent { Distance = "1 -1", Color = "0 0 0 0.8" }, new CuiRectTransformComponent { AnchorMin = $"0.04 {npcStartY - 0.04f}", AnchorMax = $"0.96 {npcStartY}" } } }); npcStartY -= 0.045f;
                if (!_playerActiveNpcTab.TryGetValue(player.userID, out string selectedKey)) { selectedKey = "normal"; _playerActiveNpcTab[player.userID] = selectedKey; }
                if (!config.NpcProfiles.TryGetValue(selectedKey, out NpcProfile npc)) npc = new NpcProfile { Name = "Asalto", BaseHealth = 120f, Weapon = "smg.mp5" };

                float tabYMax = npcStartY; float tabYMin = tabYMax - 0.045f; string[] profileKeys = { "normal", "elite", "boss" }; string[] profileLabels = { "⚙ COMPAÑÍA ASALTO", "☠ GUARDIA ÉLITE", "👑 JEFE DE RADTOWN" }; float tabWidth = 0.18f; float tabStartO = 0.40f;
                for (int t = 0; t < profileKeys.Length; t++) { string pKey = profileKeys[t]; string pLabel = profileLabels[t]; string tabColor = (pKey == selectedKey) ? "0.18 0.44 0.68 1" : "0.13 0.13 0.15 1"; contentContainer.Add(new CuiButton { Button = { Command = $"radtown.cui npc_tab {diffId} {pKey}", Color = tabColor }, RectTransform = { AnchorMin = $"{tabStartO + (t * (tabWidth + 0.01f))} {tabYMin}", AnchorMax = $"{tabStartO + tabWidth + (t * (tabWidth + 0.01f))} {tabYMax}" }, Text = { Text = pLabel, FontSize = 9, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" } }, contentPanelName); }
                contentContainer.Add(new CuiElement { Parent = contentPanelName, Components = { new CuiTextComponent { Text = "Vida Base:", FontSize = 11, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-bold.ttf", Color = "0.85 0.85 0.85 1" }, new CuiRectTransformComponent { AnchorMin = $"0.04 {tabYMin}", AnchorMax = $"0.15 {tabYMax}" } } }); contentContainer.Add(new CuiButton { Button = { Command = $"radtown.cui npc_health {diffId} {selectedKey} -25", Color = "0.55 0.2 0.2 1" }, RectTransform = { AnchorMin = $"0.15 {tabYMin}", AnchorMax = $"0.18 {tabYMax}" }, Text = { Text = "-", FontSize = 12 } }, contentPanelName); contentContainer.Add(new CuiPanel { Image = { Color = "0.15 0.15 0.17 1" }, RectTransform = { AnchorMin = $"0.185 {tabYMin}", AnchorMax = $"0.295 {tabYMax}" } }, contentPanelName, "hp_container"); contentContainer.Add(new CuiElement { Parent = "hp_container", Components = { new CuiTextComponent { Text = npc.BaseHealth.ToString("0"), FontSize = 11, Align = TextAnchor.MiddleCenter, Color = "#ffeb3b", Font = "robotocondensed-bold.ttf" } } }); contentContainer.Add(new CuiButton { Button = { Command = $"radtown.cui npc_health {diffId} {selectedKey} 25", Color = "0.2 0.5 0.2 1" }, RectTransform = { AnchorMin = $"0.30 {tabYMin}", AnchorMax = $"0.33 {tabYMax}" }, Text = { Text = "+", FontSize = 12 } }, contentPanelName);

                npcStartY = tabYMin - 0.02f;
                contentContainer.Add(new CuiElement { Parent = contentPanelName, Components = { new CuiTextComponent { Text = "SLOT DE CUERPO", FontSize = 10, Color = "0.5 0.5 0.5 1", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-bold.ttf" }, new CuiRectTransformComponent { AnchorMin = $"0.04 {npcStartY - 0.025f}", AnchorMax = $"0.25 {npcStartY}" } } }); contentContainer.Add(new CuiElement { Parent = contentPanelName, Components = { new CuiTextComponent { Text = "ARTÍCULO (SHORTNAME)", FontSize = 10, Color = "0.5 0.5 0.5 1", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" }, new CuiRectTransformComponent { AnchorMin = $"0.27 {npcStartY - 0.025f}", AnchorMax = $"0.57 {npcStartY}" } } }); contentContainer.Add(new CuiElement { Parent = contentPanelName, Components = { new CuiTextComponent { Text = "PEGA SKIN Y PULSA ENTER ↵", FontSize = 10, Color = "0.5 0.5 0.5 1", Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" }, new CuiRectTransformComponent { AnchorMin = $"0.60 {npcStartY - 0.025f}", AnchorMax = $"0.96 {npcStartY}" } } });
                npcStartY -= 0.03f; float rowHeight = 0.040f; float rowGap = 0.007f;
                string[] slots = { "weapon", "helmet", "torso", "torso2", "pants", "pants2", "boots", "gloves" }; string[] slotLabels = { "Arma / Primaria", "Casco / Cabeza", "Chaqueta / Pecho 1", "Camisa / Pecho 2", "Pantalón 1", "Pantalón 2", "Botas / Calzado", "Guantes / Manos" };

                for (int i = 0; i < slots.Length; i++) {
                    string slot = slots[i]; string label = slotLabels[i]; float yMax = npcStartY - (i * (rowHeight + rowGap)); float yMin = yMax - rowHeight;
                    contentContainer.Add(new CuiPanel { Image = { Color = "0.12 0.12 0.14 0.4" }, RectTransform = { AnchorMin = $"0.03 {yMin}", AnchorMax = $"0.97 {yMax}" } }, contentPanelName);
                    contentContainer.Add(new CuiElement { Parent = contentPanelName, Components = { new CuiTextComponent { Text = label.ToUpper(), FontSize = 10, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-bold.ttf", Color = "0.85 0.85 0.85 1" }, new CuiRectTransformComponent { AnchorMin = $"0.04 {yMin}", AnchorMax = $"0.25 {yMax}" } } });
                    string itemShortname = "VACÍO"; ulong itemSkin = 0;
                    switch (slot) { case "weapon": itemShortname = npc.Weapon; itemSkin = npc.WeaponSkin; break; case "helmet": itemShortname = npc.Helmet; itemSkin = npc.HelmetSkin; break; case "torso":  itemShortname = npc.Torso;  itemSkin = npc.TorsoSkin; break; case "torso2": itemShortname = npc.Torso2; itemSkin = npc.Torso2Skin; break; case "pants":  itemShortname = npc.Pants;  itemSkin = npc.PantsSkin; break; case "pants2": itemShortname = npc.Pants2; itemSkin = npc.Pants2Skin; break; case "boots":  itemShortname = npc.Boots;  itemSkin = npc.BootsSkin; break; case "gloves": itemShortname = npc.Gloves; itemSkin = npc.GlovesSkin; break; }
                    if (string.IsNullOrEmpty(itemShortname)) itemShortname = "NINGUNO"; else itemShortname = itemShortname.Replace("rifle.", "").Replace("smg.", "").Replace("lmg.", "").Replace("shoes.", "").ToUpper();
                    contentContainer.Add(new CuiButton { Button = { Command = $"radtown.cui npc_cycle_item {diffId} {selectedKey} {slot}", Color = "0.22 0.35 0.55 1" }, RectTransform = { AnchorMin = $"0.27 {yMin}", AnchorMax = $"0.57 {yMax}" }, Text = { Text = itemShortname, FontSize = 10, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" } }, contentPanelName);
                    string sBox = $"sb_{selectedKey}_{slot}"; contentContainer.Add(new CuiPanel { Image = { Color = "0.15 0.15 0.17 1" }, RectTransform = { AnchorMin = $"0.60 {yMin}", AnchorMax = $"0.85 {yMax}" } }, contentPanelName, sBox);
                    contentContainer.Add(new CuiElement { Parent = sBox, Components = { new CuiInputFieldComponent { Text = itemSkin.ToString(), FontSize = 11, Align = TextAnchor.MiddleCenter, Color = "#ffeb3b", Command = $"radtown.cui npc_set_skin {diffId} {selectedKey} {slot} " }, new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" } } });
                    contentContainer.Add(new CuiElement { Parent = contentPanelName, Components = { new CuiTextComponent { Text = "INTRO ↵", FontSize = 9, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", Color = "0.4 0.8 0.4 1" }, new CuiRectTransformComponent { AnchorMin = $"0.86 {yMin}", AnchorMax = $"0.95 {yMax}" } } });
                }
            }
            else if (config.Difficulties.ContainsKey(diffId)) {
                var diff = config.Difficulties[diffId];
                DrawToggleRow("Estado de Dificultad:", diff.Enabled ? "ACTIVADO" : "DESACTIVADO", diff.Enabled ? "0.20 0.55 0.30 1" : "0.65 0.25 0.25 1", $"radtown.cui toggle_enabled {diffId}", "Si está apagado, este nivel de dificultad nunca será seleccionado de forma aleatoria."); 
                DrawToggleRow("Tanque Bradley:", diff.Bradley ? "HABILITADO" : "DESHABILITADO", diff.Bradley ? "0.20 0.55 0.30 1" : "0.65 0.25 0.25 1", $"radtown.cui toggle_bradley {diffId}", ""); 
                DrawToggleRow("Helicóptero Patrulla:", diff.HeliPatrol ? "HABILITADO" : "DESHABILITADO", diff.HeliPatrol ? "0.20 0.55 0.30 1" : "0.65 0.25 0.25 1", $"radtown.cui toggle_heli {diffId}", "Envía un soporte aéreo a los 5 minutos de iniciarse el hackeo."); 
                DrawToggleRow("Helicóptero Patrulla 2:", diff.HeliPatrol2 ? "HABILITADO" : "DESHABILITADO", diff.HeliPatrol2 ? "0.20 0.55 0.30 1" : "0.65 0.25 0.25 1", $"radtown.cui toggle_heli2 {diffId}", "Envía un segundo soporte aéreo a los 10 minutos de iniciarse el hackeo."); 
                DrawNumberRow("Multiplicador Vida (NPC):", diff.HealthMultiplier.ToString("0.0"), $"radtown.cui add_health {diffId} -0.1", $"radtown.cui add_health {diffId} 0.1"); 
                DrawNumberRow("Multiplicador Daño (NPC):", diff.AimDamageMultiplier.ToString("0.0"), $"radtown.cui add_dmg {diffId} -0.1", $"radtown.cui add_dmg {diffId} 0.1"); 
                DrawNumberRow("Cajas Codelock (Hack):", diff.HackCrates.ToString(), $"radtown.cui add_hackcrates {diffId} -1", $"radtown.cui add_hackcrates {diffId} 1"); 
                DrawNumberRow("NPCs de Custodia:", diff.NPCs.ToString(), $"radtown.cui add_npc {diffId} -1", $"radtown.cui add_npc {diffId} 1");  
                DrawNumberRow("Cajas de Élite:", diff.EliteFoxes.ToString(), $"radtown.cui add_foxes {diffId} -1", $"radtown.cui add_foxes {diffId} 1");      

                float yMaxTurret = 0.96f - (rowIndex * rowSpacing); float yMinTurret = yMaxTurret - 0.07f;
                contentContainer.Add(new CuiElement { Parent = contentPanelName, Components = { new CuiTextComponent { Text = "Skin Torretas:", FontSize = 13, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-bold.ttf" }, new CuiOutlineComponent { Distance = "1 -1", Color = "0 0 0 0.6" }, new CuiRectTransformComponent { AnchorMin = $"0.04 {yMinTurret}", AnchorMax = $"0.3 {yMaxTurret}" } } });
                string turretBox = "turret_skin_box";
                contentContainer.Add(new CuiPanel { Image = { Color = "0.15 0.15 0.17 1" }, RectTransform = { AnchorMin = $"0.3 {yMinTurret}", AnchorMax = $"0.55 {yMaxTurret}" } }, contentPanelName, turretBox);
                contentContainer.Add(new CuiElement { Parent = turretBox, Components = { new CuiInputFieldComponent { Text = diff.skinTurret.ToString(), FontSize = 11, Align = TextAnchor.MiddleCenter, Color = "#ffeb3b", Command = $"radtown.cui set_turretskin {diffId} " }, new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" } } });
                contentContainer.Add(new CuiElement { Parent = contentPanelName, Components = { new CuiTextComponent { Text = "ENTER ↵", FontSize = 9, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-bold.ttf", Color = "0.4 0.8 0.4 1" }, new CuiRectTransformComponent { AnchorMin = $"0.57 {yMinTurret}", AnchorMax = $"0.95 {yMaxTurret}" } } });
                rowIndex++;

                float yMaxDoor = 0.96f - (rowIndex * rowSpacing); float yMinDoor = yMaxDoor - 0.07f;
                contentContainer.Add(new CuiElement { Parent = contentPanelName, Components = { new CuiTextComponent { Text = "Skin Puertas:", FontSize = 13, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-bold.ttf" }, new CuiOutlineComponent { Distance = "1 -1", Color = "0 0 0 0.6" }, new CuiRectTransformComponent { AnchorMin = $"0.04 {yMinDoor}", AnchorMax = $"0.3 {yMaxDoor}" } } });
                string doorBox = "door_skin_box";
                contentContainer.Add(new CuiPanel { Image = { Color = "0.15 0.15 0.17 1" }, RectTransform = { AnchorMin = $"0.3 {yMinDoor}", AnchorMax = $"0.55 {yMaxDoor}" } }, contentPanelName, doorBox);
                contentContainer.Add(new CuiElement { Parent = doorBox, Components = { new CuiInputFieldComponent { Text = diff.skinDoor.ToString(), FontSize = 11, Align = TextAnchor.MiddleCenter, Color = "#ffeb3b", Command = $"radtown.cui set_doorskin {diffId} " }, new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" } } });
                contentContainer.Add(new CuiElement { Parent = contentPanelName, Components = { new CuiTextComponent { Text = "ENTER ↵", FontSize = 9, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-bold.ttf", Color = "0.4 0.8 0.4 1" }, new CuiRectTransformComponent { AnchorMin = $"0.57 {yMinDoor}", AnchorMax = $"0.95 {yMaxDoor}" } } });
                rowIndex++;
            }

            CuiHelper.AddUi(player, contentContainer);
        }

        [ConsoleCommand("radtown.start")]
        private void CmdConsoleStart(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null && !arg.Player().IsAdmin) {
                arg.ReplyWith("No tienes permisos para usar este comando.");
                return;
            }

            if (isEventActive) { arg.ReplyWith("El evento ya está activo."); return; }
            if (cachedRadtownMonument == null) { arg.ReplyWith("Error: No se encontró el monumento Radtown en este mapa."); return; }

            int dificultad = -1;
            if (arg.HasArgs() && int.TryParse(arg.Args[0], out int parsedDificultad)) {
                if (parsedDificultad >= 0 && parsedDificultad <= 5) dificultad = parsedDificultad;
                else { arg.ReplyWith("Dificultad inválida. Usa un número del 0 al 5."); return; }
            }

            StartRadtownEvent(arg.Player(), dificultad);
            
            string diffName = dificultad == -1 ? "Aleatoria" : config.Difficulties[dificultad].Name.ToUpper();
            arg.ReplyWith($"[Radtown Event] Iniciando evento manualmente con dificultad: {diffName}");
        }

        [ChatCommand("radtown")]
        private void CmdRadtown(BasePlayer player, string command, string[] args)
        {
            if (player == null || !player.IsAdmin) return;

            if (args.Length == 0) { SendMsg(player, "CmdUsage"); return; }
            string action = args[0].ToLower();

            if (action == "stop") { CleanUpEvent(true); BroadcastMsg("EventStoppedManual"); return; } 
            else if (action == "start") {
                int dificultad = -1; 
                if (args.Length > 1 && int.TryParse(args[1], out int parsedDificultad)) dificultad = parsedDificultad;
                StartRadtownEvent(player, dificultad);
                return;
            } 
            else if (action == "info") {
                if (isEventActive) { SendMsg(player, "EventAlreadyActive"); return; }
                if (autoStartTimer != null) {
                    string timeFormatted = "Desactivado";
					if(config.EnableAutoStart) {
						double remainingSeconds = Math.Max(nextEventTimestamp - UnityEngine.Time.realtimeSinceStartup, 0);
						TimeSpan time = TimeSpan.FromSeconds(remainingSeconds);
						timeFormatted = time.TotalHours >= 1 ? $"{(int)time.TotalHours}h {time.Minutes}min" : $"{time.Minutes}min {time.Seconds}s";
					}
                    SendMsg(player, "NextEventTime", timeFormatted);
                } else { SendMsg(player, "NoActiveEvent"); }
            }
            else if (action == "config" || action == "menu") OpenAdminMenu(player, -1);
        }

        private void StartRadtownEvent(BasePlayer triggerPlayer, int Dificultad)
        {
            if (isEventActive) { if (triggerPlayer != null) SendMsg(triggerPlayer, "EventAlreadyActive"); return; }
            if (cachedRadtownMonument == null) { if (triggerPlayer != null) SendMsg(triggerPlayer, "ErrorMonument"); return; }

            isEventActive = true;
            hasCrateBeenHacked = false;

            Puts("Iniciando Evento en Radtown...");
            BroadcastMsg("EventWarning");

            if (config?.Difficulties == null) return;
            
            var availableDifficulties = config.Difficulties.Where(kvp => kvp.Value != null && kvp.Value.Enabled).Select(kvp => kvp.Key).ToList();
            if (availableDifficulties.Count == 0) {
                Puts("Advertencia: No hay dificultades 'Enabled' disponibles. Forzando el uso por defecto.");
                availableDifficulties = config.Difficulties.Keys.ToList();
            }
            
            CurrentDifficulty = (Dificultad == -1 || Dificultad < 0 || Dificultad > 5) ? availableDifficulties[UnityEngine.Random.Range(0, availableDifficulties.Count)] : Dificultad;
            
            var diff = config.Difficulties[CurrentDifficulty];
            Transform rootTransform = cachedRadtownMonument.transform;
            
            timer.Once((dev ? 1f : 180f), () => 
            { 
				eventEndTime = UnityEngine.Time.realtimeSinceStartup + (config.EventDurationMinutes * 60f);
                torretasVivas = cajasVivas = npcsVivos = 0;
                if (spawnCoroutine != null) ServerMgr.Instance.StopCoroutine(spawnCoroutine);
                spawnCoroutine = ServerMgr.Instance.StartCoroutine(SpawnEventDefensesCoroutine(rootTransform));
                
                CreateEventMapMarkers(rootTransform.position);
                UpdateVendingMarkerTime();
                
                markerUpdateTimer = timer.Every(10f, UpdateVendingMarkerTime);
                mainLoopTimer = timer.Every(1f, MainEventLoop);
                
                BroadcastMsg("EventStarted");
                BroadcastMsg("EventDifficulty", diff.MarkerColor, diff.Name.ToUpper());
                
                eventTimer = timer.Once(config.EventDurationMinutes * 60f, () => 
                {
                    BroadcastMsg("EventEnded");
                    CleanUpEvent(false);
                });
            });
        }

        private IEnumerator SpawnEventDefensesCoroutine(Transform rootTransform)
        {
            var diff = config.Difficulties[CurrentDifficulty];
            for (int u = 0; u < diff.HackCrates; u++)
            {
                Vector3 cratePos = rootTransform.TransformPoint(LocalCrateOffset);
                SpawnEventEntity("assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab", cratePos, Quaternion.identity);
                yield return wait01;
            }

			int maxCratesToSpawn = Math.Min(diff.EliteFoxes, Event_EliteCrates.Count);
			if (maxCratesToSpawn > 0)
			{
				List<int> shuffledIndices = Enumerable.Range(0, Event_EliteCrates.Count).ToList();
                for (int i = 0; i < shuffledIndices.Count; i++) {
                    int temp = shuffledIndices[i]; int randomIndex = UnityEngine.Random.Range(i, shuffledIndices.Count);
                    shuffledIndices[i] = shuffledIndices[randomIndex]; shuffledIndices[randomIndex] = temp;
                }
				
				for (int i = 0; i < maxCratesToSpawn; i++)
				{
					int selectedIndex = shuffledIndices[i];
					var data = Event_EliteCrates[selectedIndex];
					
					Vector3 elitePos = rootTransform.TransformPoint(data.Pos);
					Quaternion eliteRot = rootTransform.rotation * Quaternion.Euler(data.Rot);

					string prefab = (i % 2 == 0) ? "assets/bundled/prefabs/radtown/crate_elite.prefab" : "assets/prefabs/npc/m2bradley/bradley_crate.prefab";
					var entity = SpawnEventEntity(prefab, elitePos, eliteRot);
					yield return wait005;
				}
			}

            foreach (var data in Event_Turrets)
            {
                Vector3 turretPos = rootTransform.TransformPoint(data.Pos);
                Quaternion rotation = (data.Rot == Vector3.zero) 
                    ? Quaternion.LookRotation(turretPos - rootTransform.position) 
                    : rootTransform.rotation * Quaternion.Euler(data.Rot);

                SpawnEventEntity("assets/prefabs/npc/autoturret/autoturret_deployed.prefab", turretPos, rotation, diff.skinTurret);
                yield return wait005; 
            }

            if (NpcSpawn != null)
            {
                for (int i = 0; i < diff.NPCs; i++)
                {
                    Vector3 npcPos = rootTransform.position;
                    if (Event_NPCs.Count > 0) {
                        npcPos = rootTransform.TransformPoint(Event_NPCs[i % Event_NPCs.Count]);
                    } else {
                        npcPos = rootTransform.TransformPoint(LocalCrateOffset) + new Vector3(UnityEngine.Random.Range(-10f, 10f), 0.5f, UnityEngine.Random.Range(-10f, 10f));
                    }

                    JObject npcProfile = GetNpcProfile(i);
                    object npcEntity = NpcSpawn.Call("SpawnCustom", npcPos, npcProfile) ?? NpcSpawn.Call("SpawnNpc", npcPos, npcProfile);
                    if (npcEntity is BaseEntity entity) RegisterEntity(entity, true); 
                    yield return wait01;
                }
            }
            
            foreach (var data in Event_MetalDoor)
            {
                Vector3 doorPos = rootTransform.TransformPoint(data.Pos);
                Quaternion doorRot = (data.Rot == Vector3.zero) ? Quaternion.LookRotation(doorPos - rootTransform.position) : rootTransform.rotation * Quaternion.Euler(data.Rot);

                if (GameManager.server.CreateEntity("assets/prefabs/building/door.hinged/door.hinged.metal.prefab", doorPos, doorRot) is Door door)
                {
                    door.skinID = diff.skinDoor; door.Spawn();
                    if (door is StabilityEntity stab) stab.grounded = true;
                    RegisterEntity(door); 

                    if (GameManager.server.CreateEntity("assets/prefabs/locks/keypad/lock.code.prefab", Vector3.zero, Quaternion.identity) is CodeLock codeLock)
                    {
                        codeLock.code = $"{UnityEngine.Random.Range(0, 10000):D4}"; codeLock.Spawn();
                        codeLock.SetParent(door, door.GetSlotAnchorName(BaseEntity.Slot.Lock));
                        door.SetSlot(BaseEntity.Slot.Lock, codeLock); codeLock.flags |= BaseEntity.Flags.Locked;
                    }
                }
                yield return wait005; 
            }
            
            if(diff.Bradley && rutaBradley.Count > 0)
            {
                List<Vector3> worldPath = new List<Vector3>();
                for (int i = 0; i < rutaBradley.Count; i++) worldPath.Add(rootTransform.TransformPoint(rutaBradley[i]));
                if (worldPath.Count > 1) worldPath.Add(worldPath[0]);

                if (GameManager.server.CreateEntity("assets/prefabs/npc/m2bradley/bradleyapc.prefab", worldPath[0]) is BradleyAPC bradley)
                {
                    bradley.currentPath = worldPath; bradley.Spawn();
                    RegisterEntity(bradley); bradley.Invoke("TriggerPatrol", 1.0f);
                }
                yield return wait005;
            }
            
            spawnCoroutine = null;
        }

        private BaseEntity SpawnEventEntity(string prefab, Vector3 position, Quaternion rotation, ulong skinId = 0)
        {
            BaseEntity entity = GameManager.server.CreateEntity(prefab, position, rotation);
            if (entity == null) return null;

            if (skinId != 0) entity.skinID = skinId;
            entity.Spawn(); 
            RegisterEntity(entity);

            if (entity is AutoTurret turret) 
            {
                turret.flags &= ~BaseEntity.Flags.On; turret.OwnerID = 0;
                NextTick(() => {
                    if (turret == null || turret.IsDestroyed) return;
                    turret.inventory.Clear();
                    
                    var weapon = ItemManager.CreateByName("rifle.ak", 1); 
                    if (weapon != null && !weapon.MoveToContainer(turret.inventory, 0)) { weapon.SetParent(turret.inventory); weapon.position = 0; }
                    
                    var ammo = ItemManager.CreateByName("ammo.rifle", 1028); 
                    if (ammo != null && !ammo.MoveToContainer(turret.inventory, 1)) { ammo.SetParent(turret.inventory); ammo.position = 1; }
                    
                    turret.UpdateTotalAmmo(); turret.inventory.MarkDirty(); 
                    turret.flags |= BaseEntity.Flags.On; turret.SendNetworkUpdate();
                });
            }
            return entity;
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity is AutoTurret turret && spawnedEntityIds.Contains(turret.net.ID.Value)) {
                if (turret.inventory != null) { turret.inventory.Clear(); turret.inventory.MarkDirty(); }
            }
        }

        private JObject GetNpcProfile(int index)
        {
            var diff = config.Difficulties[CurrentDifficulty];
            int role = index % 3; 
            string profileKey = role == 0 ? "boss" : (role == 2 ? "elite" : "normal"); 

            if (!config.NpcProfiles.TryGetValue(profileKey, out NpcProfile profile))
                profile = new NpcProfile { Name = "Defensor", BaseHealth = 150f, Weapon = "rifle.lr300", Torso = "hazmatsuit" };

            string number = (dev ? $" {index}" : "");
            string name = $"{profile.Name}{number}";
            string primaryWeapon = profile.Weapon;
            
            var wearItems = new JArray();
            if (!string.IsNullOrEmpty(profile.Helmet)) wearItems.Add(new JObject { ["ShortName"] = profile.Helmet, ["SkinID"] = profile.HelmetSkin });
            if (!string.IsNullOrEmpty(profile.Torso))  wearItems.Add(new JObject { ["ShortName"] = profile.Torso,  ["SkinID"] = profile.TorsoSkin });
            if (!string.IsNullOrEmpty(profile.Torso2)) wearItems.Add(new JObject { ["ShortName"] = profile.Torso2, ["SkinID"] = profile.Torso2Skin }); 
            if (!string.IsNullOrEmpty(profile.Pants))  wearItems.Add(new JObject { ["ShortName"] = profile.Pants,  ["SkinID"] = profile.PantsSkin });
            if (!string.IsNullOrEmpty(profile.Pants2)) wearItems.Add(new JObject { ["ShortName"] = profile.Pants2, ["SkinID"] = profile.Pants2Skin }); 
            if (!string.IsNullOrEmpty(profile.Boots))  wearItems.Add(new JObject { ["ShortName"] = profile.Boots,  ["SkinID"] = profile.BootsSkin });
            if (!string.IsNullOrEmpty(profile.Gloves)) wearItems.Add(new JObject { ["ShortName"] = profile.Gloves, ["SkinID"] = profile.GlovesSkin });

            return new JObject {
                ["Name"] = name, ["Health"] = profile.BaseHealth * diff.HealthMultiplier,
                ["RoamRange"] = 15f * diff.RangeMultiplier, ["ChaseRange"] = 40f * diff.RangeMultiplier,
                ["AttackRangeMultiplier"] = 2.0f * diff.RangeMultiplier, ["IsAggressive"] = true,
                ["SenseRange"] = 40f * diff.RangeMultiplier, ["DamageScale"] = 1.0f * diff.AimDamageMultiplier,
                ["Speed"] = 5.5f, ["IsRemoveCorpse"] = true,
                ["States"] = new JArray { "RoamState", "ChaseState", "CombatState" },
                ["WearItems"] = wearItems,
                ["BeltItems"] = new JArray { new JObject { ["ShortName"] = primaryWeapon, ["Amount"] = 1, ["SkinID"] = profile.WeaponSkin } }
            };
        }

        object OnTurretTarget(AutoTurret turret, BaseEntity target)
        {
			if (turret?.net == null) return null;
            if (!isEventActive || turret == null || target == null) return null;
            if (spawnedEntityIds.Contains(turret.net.ID.Value))
            {
                if (target is BaseNpc || target is NPCPlayer) return false;
            }
            return null;
        }

        private void OnCrateHack(HackableLockedCrate crate)
		{
			if (!isEventActive || crate == null || hasCrateBeenHacked) return;
			if (!spawnedEntityIds.Contains(crate.net.ID.Value)) return; 

			hasCrateBeenHacked = true;
			float remainingSeconds = eventEndTime - UnityEngine.Time.realtimeSinceStartup;

			if (remainingSeconds < 900f)
			{
				eventTimer?.Destroy();
				float nuevosMinutos = 16f;
				eventEndTime = UnityEngine.Time.realtimeSinceStartup + (nuevosMinutos * 60f);
				eventTimer = timer.Once(nuevosMinutos * 60f, () => {
					BroadcastMsg("EventEnded"); 
					CleanUpEvent(false);
				});

				UpdateVendingMarkerTime();
				Puts($"[Cupula Event] Caja hackeada quedando menos de 15 min. Evento ampliado a {nuevosMinutos} minutos.");
			}
			
			if (config.Difficulties[CurrentDifficulty].HeliPatrol) {
				heliTimer1 = timer.Once(300f, () => { SpawnPatrolHelicopter(); BroadcastMsg("HeliApproaching"); });
			}
			if (config.Difficulties[CurrentDifficulty].HeliPatrol2) {
				heliTimer2 = timer.Once(600f, () => { SpawnPatrolHelicopter(); BroadcastMsg("HeliReinforcement"); });
			}
		}

        private void SpawnPatrolHelicopter()
        {
            if (!isEventActive || cachedRadtownMonument == null) return;

            Vector3 radtownPos = cachedRadtownMonument.transform.position;
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            Vector3 direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            Vector3 spawnPos = radtownPos + (direction * 1000f);

            float terrainHeight = TerrainMeta.HeightMap != null ? TerrainMeta.HeightMap.GetHeight(spawnPos) : 30f;
            spawnPos.y = terrainHeight + 80f;

            BaseEntity heli = GameManager.server.CreateEntity("assets/prefabs/npc/patrol helicopter/patrolhelicopter.prefab", spawnPos, Quaternion.identity);
            if (heli == null) return;

            heli.Spawn();
            spawnedEntities.Add(heli);
            spawnedEntityIds.Add(heli.net.ID.Value);
            spawnedHelicopters.Add(heli);

            PatrolHelicopterAI ai = heli.GetComponent<PatrolHelicopterAI>();
            if (ai != null) ai.SetTargetDestination(radtownPos + new Vector3(0, 17.5f, 0));

            StartHeliTrackingLoop();
        }

        private void StartHeliTrackingLoop()
        {
            if (heliDestinationTimer != null) return;

            heliDestinationTimer = timer.Every(10f, () =>
            {
                if (!isEventActive || spawnedHelicopters.Count == 0 || cachedRadtownMonument == null) {
                    heliDestinationTimer?.Destroy(); heliDestinationTimer = null; return;
                }

                Vector3 targetPos = cachedRadtownMonument.transform.position + new Vector3(0, 20f, 0);

                for (int i = spawnedHelicopters.Count - 1; i >= 0; i--) {
                    var heli = spawnedHelicopters[i];
                    if (heli == null || heli.IsDestroyed) { spawnedHelicopters.RemoveAt(i); continue; }

                    PatrolHelicopterAI ai = heli.GetComponent<PatrolHelicopterAI>();
                    if (ai != null) ai.SetTargetDestination(targetPos);
                }
            });
        }

        private object CanHelicopterTarget(PatrolHelicopterAI heli, BasePlayer player)
        {
            if (!isEventActive || heli == null || heli.helicopterBase == null || player == null) return null;
            if (spawnedEntityIds.Contains(heli.helicopterBase.net.ID.Value))
            {
                if (player.IsConnected && !player.IsDead() && !player.IsSleeping()) {
                    if (player is NPCPlayer || player.IsNpc) return false;
                    return true;
                }
            }
            return null;
        }

        private void UpdateVendingMarkerTime()
        {
            if (!isEventActive || shopMapMarker == null || shopMapMarker.IsDestroyed) return;

            VendingMachineMapMarker vendingMarker = shopMapMarker as VendingMachineMapMarker;
            if (vendingMarker == null) return;

            float remainingSeconds = Mathf.Max(eventEndTime - UnityEngine.Time.realtimeSinceStartup, 0f);
            TimeSpan time = TimeSpan.FromSeconds(remainingSeconds);

            string timeStr = time.TotalHours >= 1 
                ? string.Format("{0:00}:{1:00}:{2:00}", (int)time.TotalHours, time.Minutes, time.Seconds)
                : string.Format("{0:00}:{1:00}", time.Minutes, time.Seconds);

            vendingMarker.markerShopName = $"★ RADTOWN [{config.Difficulties[CurrentDifficulty].Name.ToUpper()}] ★ TIEMPO: {timeStr} ★";
            vendingMarker.SendNetworkUpdate();
        } 

        private void destroyallcui()
        {
            foreach (ulong userId in playersInEventZone) {
                BasePlayer player = BasePlayer.FindByID(userId);
                if (player != null) { CuiHelper.DestroyUi(player, "RadtownEventUI"); CuiHelper.DestroyUi(player, "RadtownEventUI_Dynamic"); CuiHelper.DestroyUi(player, "RadtownEventUI_Clock"); }
            }
            playersInEventZone.Clear();
        }

        private void CleanUpEvent(bool manualStop)
        {
            if (!isEventActive) return;

            if (redCircleMarker != null && !redCircleMarker.IsDestroyed) redCircleMarker.Kill();
            if (shopMapMarker != null && !shopMapMarker.IsDestroyed) shopMapMarker.Kill();
            redCircleMarker = null; shopMapMarker = null;

            if (spawnCoroutine != null) { ServerMgr.Instance.StopCoroutine(spawnCoroutine); spawnCoroutine = null; }

            KillAllTimers();
            destroyallcui();
           
            for (int i = spawnedEntities.Count - 1; i >= 0; i--) {
                var entity = spawnedEntities[i];
                if (entity != null && !entity.IsDestroyed) entity.Kill();
            }
            
            spawnedEntities.Clear();
            spawnedEntityIds.Clear();
            spawnedHelicopters.Clear();

            isEventActive = false;
            hasCrateBeenHacked = false;
        }

        private object OnTurretConsumeAmmo(AutoTurret turret, Item ammo)
        {
            if (!isEventActive || turret == null) return null;
            if (spawnedEntityIds.Contains(turret.net.ID.Value)) return false;
            return null;
        }

        private object CanTurretTarget(AutoTurret turret, BaseCombatEntity target)
        {
			if (!isEventActive || turret == null) return null;
            if (spawnedEntityIds.Contains(turret.net.ID.Value)) {
                if (target is NPCPlayer || target.IsNpc) return false;
            }
            return null;
        }
    }
}
