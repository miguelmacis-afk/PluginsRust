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
    [Info("Airfield Event", "☠ Ⓜ🅘ⓖ🅤ⓔ🅛 ☠", "2.2.2")]
    [Description("Evento automatizado para Airfield con Avión rasante, múltiples Bradleys, NPCs, torretas y CUI dinámico de alto rendimiento.")]
    public class MiAirfieldEvent : RustPlugin
    {
        [PluginReference] private Plugin NpcSpawn;

        internal PluginConfig config;
		private bool dev = false;
        private readonly List<BaseEntity> spawnedEntities = new List<BaseEntity>();
        private readonly HashSet<ulong> spawnedEntityIds = new HashSet<ulong>();
        private readonly List<BaseEntity> spawnedHelicopters = new List<BaseEntity>();

        private Coroutine spawnCoroutine;
        private MonumentInfo cachedAirfieldMonument;
        private CargoPlane activePlane;

        private readonly List<Timer> activeTimers = new List<Timer>();
        private Timer eventTimer, autoStartTimer, heliTimer1, heliTimer2, heliDestinationTimer, markerUpdateTimer, clockTimer, playerCheckTimer;

        private const float EventZoneRadius = 180f;
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

        const string cargoPlanePrefab = "assets/prefabs/npc/cargo plane/cargo_plane.prefab";
        const string hackableCratePrefab = "assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab";
        private const string BackgroundImageUrl = "https://raw.githubusercontent.com/miguelmacis-afk/rustImage/refs/heads/main/MiBackgroundImageName.png";
        private string imageId = "0";

        private struct SpawnData
        {
            public Vector3 Pos;
            public Vector3 Rot;
            public SpawnData(Vector3 pos) { Pos = pos; Rot = Vector3.zero; }
            public SpawnData(Vector3 pos, Vector3 rot) { Pos = pos; Rot = rot; }
        }

        private readonly List<SpawnData> Event_MetalDoor = new List<SpawnData> 
        { 
            new SpawnData(new Vector3(111.50f, 3.03f, 18.90f), new Vector3(0f, 91.0f, 0f)), 
            new SpawnData(new Vector3(-111.00f, 3.25f, 58.85f), new Vector3(0f, 89f, 0f)),
            new SpawnData(new Vector3(98.25f, 3.23f, -97.90f), new Vector3(0f, 91.0f, 0f)),
            new SpawnData(new Vector3(-41.40f, 3.27f, -80.80f), new Vector3(0f, 91.0f, 0f)),
            new SpawnData(new Vector3(-104.85f, 3.23f, -105.80f), new Vector3(0f, 91.0f, 0f))
        };
        
        private readonly List<SpawnData> Event_Turrets = new List<SpawnData>();
        private readonly List<SpawnData> Event_EliteCrates = new List<SpawnData> 
        { 
            new SpawnData(new Vector3(-113.24f, 18.28f, 62.93f), new Vector3(0f, 268.5f, 0f)), 
            new SpawnData(new Vector3(116.73f, 19.97f, 18.54f), new Vector3(0f, 111.3f, 0f)),
            new SpawnData(new Vector3(-110.24f, 15.28f, -106.93f), new Vector3(0f, 268.5f, 0f)), 
            new SpawnData(new Vector3(99.03f, 18.27f, -103.54f), new Vector3(0f, 111.3f, 0f)),
            new SpawnData(new Vector3(-42.24f, 24.18f, -82.03f), new Vector3(0f, 268.5f, 0f))
        };
        private readonly List<Vector3> Event_NPCs = new List<Vector3>();
        private int iconNpcId, iconTurretId, iconCrateId;
        
        private List<Vector3> rutaBradley = new List<Vector3> 
        {
            new Vector3(138.83f, 0.3f, -36.28f), new Vector3(37.92f, 0.3f, -39.08f), 
            new Vector3(-17.54f, 0.3f, -57.57f), new Vector3(-103.88f, 0.3f, -39.02f), 
            new Vector3(-92.49f, 0.3f, -8.97f), new Vector3(-4.23f, 0.3f, -12.95f), 
            new Vector3(64.58f, 0.3f, -6.88f), new Vector3(137.58f, 0.29f, -7.66f)
        };
		private readonly WaitForSeconds wait05 = new WaitForSeconds(0.05f);
		private readonly WaitForSeconds wait10 = new WaitForSeconds(0.1f);
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
            public int NpcForBox { get; set; }
            public int EliteFoxes { get; set; }
            public int HackCrates { get; set; }
            public ulong skinTurret { get; set; }
            public ulong skinDoor { get; set; }
			
            public string MarkerColor { get; set; }
        }
        
        internal int CurrentDifficulty { get; set; } = 5; 
        
        internal class PluginConfig
        {
            [JsonProperty("Ajustes de Dificultades")]
            public Dictionary<int, DifficultySettings> Difficulties { get; set; } 
            
            [JsonProperty("Activar Autostart Automático")]
            public bool EnableAutoStart { get; set; } = true;
			
			[JsonProperty("Activar Menu informacion del evento")]
            public bool EnableMenuInfo { get; set; } = true;
			
            [JsonProperty("Intervalo Mínimo Autostart (Horas)")]
            public float AutoStartIntervalHoursMin { get; set; } = 2f;

            [JsonProperty("Intervalo Máximo Autostart (Horas)")]
            public float AutoStartIntervalHoursMax { get; set; } = 4f;

            [JsonProperty("Duración del Evento (Minutos)")]
            public float EventDurationMinutes { get; set; } = 30f;

            [JsonProperty("Configuración de Perfiles de NPC")]
            public Dictionary<string, NpcProfile> NpcProfiles { get; set; } = new Dictionary<string, NpcProfile>
            {
                { "normal", new NpcProfile { Name = "Asalto", BaseHealth = 200f, Weapon = "rifle.ak", WeaponSkin = 3754181741, Helmet = "hazmatsuit_scientist_nvgm", HelmetSkin = 0 }},
                { "elite", new NpcProfile { Name = "Francotirador", BaseHealth = 250f, Weapon = "rifle.bolt", WeaponSkin = 3745658611, Helmet = "scientistsuit_heavy", HelmetSkin = 0 }},
                { "boss", new NpcProfile { Name = "Jefe Airfield", BaseHealth = 300f, Weapon = "lmg.m249", WeaponSkin = 2825534972, Helmet = "hazmatsuit_scientist_nvgm", HelmetSkin = 0 }}
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
            config = new PluginConfig {
                Difficulties = new Dictionary<int, DifficultySettings> {
                    { 0, new DifficultySettings { Name = "easy", Enabled = true, Bradley = false, BradleyCount = 0, HeliPatrol = false, HeliPatrol2 = false, HealthMultiplier = 1.0f, RangeMultiplier = 1.0f, AimDamageMultiplier = 1.0f, NpcForBox = 4, HackCrates = 1, EliteFoxes = 0, skinTurret = 3732045248, skinDoor = 2940068880, MarkerColor = "#3cd834" } },
                    { 1, new DifficultySettings { Name = "normal", Enabled = true, Bradley = true, BradleyCount = 1, HeliPatrol = false, HeliPatrol2 = false, HealthMultiplier = 1.2f, RangeMultiplier = 1.2f, AimDamageMultiplier = 1.2f, NpcForBox = 4, HackCrates = 1, EliteFoxes = 0, skinTurret = 3732045248, skinDoor = 2940068880, MarkerColor = "#e57109" } },
                    { 2, new DifficultySettings { Name = "medium", Enabled = true, Bradley = false, BradleyCount = 0, HeliPatrol = true, HeliPatrol2 = false, HealthMultiplier = 1.5f, RangeMultiplier = 1.5f, AimDamageMultiplier = 1.5f, NpcForBox = 4, HackCrates = 2, EliteFoxes = 0, skinTurret = 3732045248, skinDoor = 2940068880, MarkerColor = "#e7c50a" } },
                    { 3, new DifficultySettings { Name = "expert", Enabled = true, Bradley = true, BradleyCount = 2, HeliPatrol = true, HeliPatrol2 = false, HealthMultiplier = 2.0f, RangeMultiplier = 2.0f, AimDamageMultiplier = 2.0f, NpcForBox = 6, HackCrates = 2, EliteFoxes = 1, skinTurret = 3732045248, skinDoor = 2940068880, MarkerColor = "#9c1825" } },
                    { 4, new DifficultySettings { Name = "hard", Enabled = true, Bradley = true, BradleyCount = 3, HeliPatrol = true, HeliPatrol2 = true, HealthMultiplier = 2.5f, RangeMultiplier = 2.5f, AimDamageMultiplier = 2.5f, NpcForBox = 8, HackCrates = 3, EliteFoxes = 2, skinTurret = 3732045248, skinDoor = 2940068880, MarkerColor = "#0000FF" } },
                    { 5, new DifficultySettings { Name = "nightmare", Enabled = true, Bradley = true, BradleyCount = 4, HeliPatrol = true, HeliPatrol2 = true, HealthMultiplier = 3.5f, RangeMultiplier = 3.5f, AimDamageMultiplier = 3.5f, NpcForBox = 10, HackCrates = 4, EliteFoxes = 3, skinTurret = 3732445397, skinDoor = 2940068880, MarkerColor = "#9236f5" } }
                }
            };
            SaveConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try { config = Config.ReadObject<PluginConfig>(); if (config == null) LoadDefaultConfig(); } 
            catch { LoadDefaultConfig(); }
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        private Timer CreateTimer(float delay, Action action)
        {
            Timer t = timer.Once(delay, action);
            activeTimers.Add(t);
            return t;
        }

        private Timer CreateTimerRepeat(float delay, Action action)
        {
            Timer t = timer.Every(delay, action);
            activeTimers.Add(t);
            return t;
        }

        private void KillAllTimers()
        {
            foreach (var t in activeTimers) { if (t != null) t.Destroy(); }
            activeTimers.Clear();
            eventTimer = autoStartTimer = heliTimer1 = heliTimer2 = heliDestinationTimer = markerUpdateTimer = clockTimer = playerCheckTimer = null;
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string> {
                ["EventAlreadyActive"] = "<color=#ff4d4d>[AIRFIELD]</color> <color=#ffffff>The event is already active.</color>",
                ["ErrorMonument"] = "<color=#ff4d4d>[ERROR]</color> <color=#ffffff>Airfield monument not found on this map.</color>",
                ["EventWarning"] = "<color=#ff4d4d>[AIRFIELD]</color> <color=#ffffff>The Airfield will be taken by </color><color=#ff4d4d>hostile forces</color><color=#ffffff>, stay away!</color>",
                ["EventStarted"] = "<color=#ff4d4d>[AIRFIELD]</color> <color=#ffffff>The Airfield has been captured! An event plane is approaching.</color>",
                ["EventDifficulty"] = "<color=#ff4d4d>[AIRFIELD]</color> <color=#ffffff>Event Difficulty: </color><color={0}>{1}</color>",
                ["EventEnded"] = "<color=#ff4d4d>[AIRFIELD]</color> <color=#ffffff>The event time is over. Cleaning up area...</color>",
                ["NoActiveEvent"] = "<color=#ff4d4d>[AIRFIELD]</color> <color=#ffffff>There is no active event.</color>",
                ["EventStoppedManual"] = "<color=#ff4d4d>[AIRFIELD]</color> <color=#ffffff>Event manually stopped.</color>",
                ["CmdUsage"] = "<color=#ffeb3b>[HELP]</color> <color=#ffffff>Usage: </color><color=#4dff4d>/airfield start [0-5]</color><color=#ffffff> or </color><color=#ff4d4d>/airfield stop</color>",
                ["NextEventTime"] = "<color=#ffeb3b>[INFO]</color> <color=#ffffff>Next event in: </color><color=#4dff4d>{0}</color>",
                ["HeliApproaching"] = "<color=#ff4d4d>[AIRFIELD]</color> <color=#ffffff>A heavy patrol helicopter is approaching the Airfield!</color>",
                ["HeliReinforcement"] = "<color=#ff4d4d>[AIRFIELD]</color> <color=#ffffff>High threat level! A second reinforcement patrol helicopter is closing in!</color>"
            }, this, "en");

            Dictionary<string, string> Es = new Dictionary<string, string> {
                ["EventAlreadyActive"] = "<color=#ff4d4d>[AIRFIELD]</color> <color=#ffffff>El evento ya está activo.</color>",
                ["ErrorMonument"] = "<color=#ff4d4d>[ERROR]</color> <color=#ffffff>No se encontró el monumento Airfield en este mapa.</color>",
                ["EventWarning"] = "<color=#ff4d4d>[AIRFIELD]</color> <color=#ffffff>¡El Airfield será tomado por </color><color=#ff4d4d>fuerzas hostiles</color><color=#ffffff>, aléjate si no quieres morir!</color>",
                ["EventStarted"] = "<color=#ff4d4d>[AIRFIELD]</color> <color=#ffffff>¡El Airfield ha sido capturado! Un avión se aproxima para soltar el botín.</color>",
                ["EventDifficulty"] = "<color=#ff4d4d>[AIRFIELD]</color> <color=#ffffff>Dificultad del evento: </color><color={0}>{1}</color>",
                ["EventEnded"] = "<color=#ff4d4d>[AIRFIELD]</color> <color=#ffffff>El tiempo del evento ha terminado. Limpiando zona...</color>",
                ["NoActiveEvent"] = "<color=#ff4d4d>[AIRFIELD]</color> <color=#ffffff>No hay evento activo.</color>",
                ["EventStoppedManual"] = "<color=#ff4d4d>[AIRFIELD]</color> <color=#ffffff>Evento detenido manualmente.</color>",
                ["CmdUsage"] = "<color=#ffeb3b>[AYUDA]</color> <color=#ffffff>Uso: </color><color=#4dff4d>/airfield start [Dificultad(0-5)]</color><color=#ffffff> o </color><color=#ff4d4d>/airfield stop</color>",
                ["NextEventTime"] = "<color=#ffeb3b>[INFO]</color> <color=#ffffff>Próximo evento en: </color><color=#4dff4d>{0}</color>",
                ["HeliApproaching"] = "<color=#ff4d4d>[AIRFIELD]</color> <color=#ffffff>¡Un helicóptero de patrulla pesado se aproxima para proteger el Airfield!</color>",
                ["HeliReinforcement"] = "<color=#ff4d4d>[AIRFIELD]</color> <color=#ffffff>¡Nivel de amenaza alto! ¡Un segundo helicóptero de refuerzo se aproxima a baja altura!</color>"
            };
            lang.RegisterMessages(Es, this, "es");
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
            foreach (BasePlayer player in BasePlayer.activePlayerList)
                if (player != null && player.IsConnected) SendMsg(player, key, args);
        }

        private void CacheMonument()
        {
            cachedAirfieldMonument = TerrainMeta.Path?.Monuments?.FirstOrDefault(m => m != null && m.name.ToLower().Contains("airfield"));
            if (cachedAirfieldMonument == null) {
                Puts("[ERROR] No se encontró el monumento 'airfield'. PLUGIN DESCARTADO."); 
                rust.RunServerCommand("oxide.unload MiAirfieldEvent");
            } else {
                Puts("Monumento Airfield cacheado exitosamente.");
            }
        }

        private void OnServerInitialized()
        {
            CacheMonument();
            ScheduleAutoStart();
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
			uint crc = FileStorage.server.Store(data, FileStorage.Type.png, default(NetworkableId));
			imageId = crc.ToString();
		}

        private float nextEventTimestamp = 0f;
        
        private void ScheduleAutoStart()
        {
            if (config.AutoStartIntervalHoursMax <= 0 || !config.EnableAutoStart) return;
            float randomHours = UnityEngine.Random.Range(config.AutoStartIntervalHoursMin, config.AutoStartIntervalHoursMax);
            float intervalSeconds = randomHours * 3600f;
            nextEventTimestamp = UnityEngine.Time.realtimeSinceStartup + intervalSeconds;
            
            autoStartTimer = CreateTimer(intervalSeconds, () => { StartEvent(null, -1); ScheduleAutoStart(); });
        }

        private BaseEntity redCircleMarker;
        private BaseEntity shopMapMarker;

        private void CreateEventMapMarkers(Vector3 position)
        {
            redCircleMarker = GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab", position);
            if (redCircleMarker is MapMarkerGenericRadius radiusMarker) {
                radiusMarker.Spawn(); radiusMarker.radius = 1.5f; radiusMarker.alpha = 0.35f;       
                ColorUtility.TryParseHtmlString(config.Difficulties[CurrentDifficulty].MarkerColor, out Color markerColor);
                radiusMarker.color1 = markerColor; radiusMarker.color2 = markerColor; radiusMarker.SendUpdate();
            }

            shopMapMarker = GameManager.server.CreateEntity("assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab", position);
            if (shopMapMarker is VendingMachineMapMarker vendingMarker) {
                vendingMarker.markerShopName = "★ EVENTO: AIRFIELD ★"; vendingMarker.SetFlag(BaseEntity.Flags.On, true);
                shopMapMarker.Spawn(); vendingMarker.SendNetworkUpdate(); 
            }
        }

        private void UpdateVendingMarkerTime()
        {
            if (!isEventActive || shopMapMarker == null || shopMapMarker.IsDestroyed) return;
            if (shopMapMarker is VendingMachineMapMarker vendingMarker) {
                float remainingSeconds = Mathf.Max(eventEndTime - UnityEngine.Time.realtimeSinceStartup, 0f);
                TimeSpan time = TimeSpan.FromSeconds(remainingSeconds);
                string timeStr = time.TotalHours >= 1 ? string.Format("{0:00}:{1:00}:{2:00}", (int)time.TotalHours, time.Minutes, time.Seconds) : string.Format("{0:00}:{1:00}", time.Minutes, time.Seconds);
                vendingMarker.markerShopName = $"★ AIRFIELD [{config.Difficulties[CurrentDifficulty].Name.ToUpper()}] ★ TIEMPO: {timeStr} ★";
                vendingMarker.SendNetworkUpdate();
            }
        }

        private void Unload()
        {
            CleanUpEvent(true); 
            KillAllTimers();
            if (spawnCoroutine != null) { ServerMgr.Instance.StopCoroutine(spawnCoroutine); spawnCoroutine = null; }
            foreach (var player in BasePlayer.activePlayerList) {
                CuiHelper.DestroyUi(player, "AirfieldEventUI");
                CuiHelper.DestroyUi(player, "AirfieldEventUI_Dynamic");
                CuiHelper.DestroyUi(player, "AirfieldEventUI_Clock");
                CuiHelper.DestroyUi(player, "AirfieldAdminUI");
            }
        }

        [ConsoleCommand("airfield.start")]
        private void CmdConsoleStart(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null && !arg.Player().IsAdmin) { arg.ReplyWith("Sin permisos."); return; }
            if (isEventActive) { arg.ReplyWith("El evento ya está activo."); return; }
            if (cachedAirfieldMonument == null) return;

            int dificultad = -1;
            if (arg.HasArgs() && int.TryParse(arg.Args[0], out int parsedDificultad))
                if (parsedDificultad >= 0 && parsedDificultad <= 5) dificultad = parsedDificultad;

            StartEvent(arg.Player(), dificultad);
        }

        [ChatCommand("airfield")]
        private void CmdEventChat(BasePlayer player, string command, string[] args)
        {
            if (player == null || !player.IsAdmin) return;
            if (args.Length == 0) { SendMsg(player, "CmdUsage"); return; }
            string action = args[0].ToLower();

            if (action == "stop") { CleanUpEvent(true); BroadcastMsg("EventStoppedManual"); } 
            else if (action == "start") {
                int dificultad = -1; 
                if (args.Length > 1 && int.TryParse(args[1], out int pDiff)) dificultad = pDiff;
                StartEvent(player, dificultad);
            } 
            else if (action == "info") {
                if (isEventActive) { SendMsg(player, "EventAlreadyActive"); return; }
                if (autoStartTimer != null) {
                    string timeFormatted = "Desactivado";
					if(config.EnableAutoStart)
					{
						double remainingSeconds = Math.Max(nextEventTimestamp - UnityEngine.Time.realtimeSinceStartup, 0);
						TimeSpan time = TimeSpan.FromSeconds(remainingSeconds);
						timeFormatted = time.TotalHours >= 1 
							? $"{(int)time.TotalHours}h {time.Minutes}min" 
							: $"{time.Minutes}min {time.Seconds}s";
					}
                    SendMsg(player, "NextEventTime", timeFormatted);
                } else SendMsg(player, "NoActiveEvent"); 
            }
            else if (action == "config" || action == "menu") OpenAdminMenu(player, -1);
        }

        private class BradleyEventAI : MonoBehaviour
        {
            private BradleyAPC bradley;
            private List<Vector3> originalPath;
            private bool isInvestigating = false;

            private void Awake() { bradley = GetComponent<BradleyAPC>(); }
            public void Init(List<Vector3> path) { originalPath = new List<Vector3>(path); }
            public void Investigate(Vector3 targetPos) {
                if (isInvestigating || bradley == null) return;
                isInvestigating = true;
                bradley.CancelInvoke("UpdateMovement_Patrol");
                Vector3 safeTarget = targetPos;
                if (TerrainMeta.HeightMap != null) safeTarget.y = TerrainMeta.HeightMap.GetHeight(safeTarget);
                bradley.currentPath = new List<Vector3> { bradley.transform.position, safeTarget };
                bradley.currentPathIndex = 0;
                bradley.Invoke("TriggerPatrol", 0.5f);
                Invoke(nameof(ResumeOriginalPath), 20f); 
            }
            private void ResumeOriginalPath() {
                if (bradley == null || bradley.IsDestroyed) return;
                isInvestigating = false;
                int closestIdx = 0; float minDist = float.MaxValue;
                for (int i = 0; i < originalPath.Count; i++) {
                    float dist = Vector3.Distance(bradley.transform.position, originalPath[i]);
                    if (dist < minDist) { minDist = dist; closestIdx = i; }
                }
                bradley.currentPath = new List<Vector3>(originalPath);
                bradley.currentPathIndex = closestIdx;
                bradley.Invoke("TriggerPatrol", 0.5f);
            }
            private void OnDestroy() { CancelInvoke(nameof(ResumeOriginalPath)); }
        }

        public void TrySpawnBradley(int index, int totalBradleys)
		{
			if (cachedAirfieldMonument == null || totalBradleys <= 0 || rutaBradley.Count == 0) return;
			Transform rootTransform = cachedAirfieldMonument.transform;
			
			List<Vector3> worldPath = new List<Vector3>(rutaBradley.Count);
			for (int i = 0; i < rutaBradley.Count; i++)
			{
				worldPath.Add(rootTransform.TransformPoint(rutaBradley[i]));
			}

			int spacing = Mathf.Max(1, worldPath.Count / totalBradleys);
			int startIndex = (index * spacing) % worldPath.Count;

			List<Vector3> shiftedPath = new List<Vector3>(worldPath.Count + 1);
			for(int i = 0; i < worldPath.Count; i++) 
			{
				shiftedPath.Add(worldPath[(startIndex + i) % worldPath.Count]);
			}
			if (shiftedPath.Count > 1) shiftedPath.Add(shiftedPath[0]);

			if (GameManager.server.CreateEntity("assets/prefabs/npc/m2bradley/bradleyapc.prefab", shiftedPath[0]) is BradleyAPC bradley) {
				bradley.currentPath = shiftedPath;
				bradley.Spawn(); 
				RegisterEntity(bradley, false);
				var aiController = bradley.gameObject.AddComponent<BradleyEventAI>();
				aiController.Init(shiftedPath);
				bradley.Invoke("TriggerPatrol", 1.0f);
			}
		}
		
        public void SpawnNpcsForCrate(Vector3 cratePos)
        {
            if (NpcSpawn == null) return;
            int npcCount = config.Difficulties[CurrentDifficulty].NpcForBox;
            if (npcCount <= 0) return;
            
            for (int i = 0; i < npcCount; i++) {
                float angle = i * (Mathf.PI * 2f / 4f);
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 3f;
                Vector3 npcPos = cratePos + offset;
                if (TerrainMeta.HeightMap != null) npcPos.y = TerrainMeta.HeightMap.GetHeight(npcPos);

                JObject npcProfile = GetNpcProfile(i);
                object npcEntity = NpcSpawn.Call("SpawnCustom", npcPos, npcProfile) ?? NpcSpawn.Call("SpawnNpc", npcPos, npcProfile);
                if (npcEntity is BaseEntity entity) RegisterEntity(entity, true); 
            }
        }

        private void SpawnEventPlane(Transform airfieldTransform)
        {
            activePlane = (CargoPlane)GameManager.server.CreateEntity(cargoPlanePrefab, new Vector3(), new Quaternion(), true);
            if (activePlane == null) return;
            activePlane.Spawn();
            var controller = activePlane.gameObject.AddComponent<AirfieldPlaneController>();
            controller.InitializePath(this, airfieldTransform);
        }

        public void RegisterEntity(BaseEntity entity, bool isNpc = false)
        {
            if (entity == null) return;
            spawnedEntities.Add(entity);
            spawnedEntityIds.Add(entity.net.ID.Value);
            
            if (isNpc || entity is NPCPlayer || entity is BaseNpc) npcsVivos++;
            else if (entity is AutoTurret) torretasVivas++;
            else if (entity is HackableLockedCrate || entity is LootContainer) cajasVivas++;
            
            UpdateEventCUIForce(); 
        }

        private class AirfieldPlaneController : MonoBehaviour
        {
            private MiAirfieldEvent plugin;
            private CargoPlane plane;
            private Vector3 p0, p1, p2, p3, runwayStart, runwayEnd;
            private float progress, speed = 60f, curveLength;
            private int cratesDropped;
            private bool droppingStarted;

            private void Awake() { plane = GetComponent<CargoPlane>(); if (plane != null) plane.enabled = false; plane.dropped = true; }

            public void InitializePath(MiAirfieldEvent hostPlugin, Transform airfieldTransform)
            {
                plugin = hostPlugin;
                Vector3 rawPoint1 = airfieldTransform.TransformPoint(new Vector3(135.18f, 0.28f, -42.44f));
                Vector3 rawPoint2 = airfieldTransform.TransformPoint(new Vector3(-104.66f, 0.30f, -43.56f));
                Vector3 direction = (rawPoint2 - rawPoint1).normalized; float mapSize = TerrainMeta.Size.x;
                runwayStart = new Vector3(rawPoint1.x, TerrainMeta.HeightMap.GetHeight(rawPoint1) + 1f, rawPoint1.z);
                runwayEnd = new Vector3(rawPoint2.x, TerrainMeta.HeightMap.GetHeight(rawPoint2) + 1f, rawPoint2.z);
                p1 = runwayStart - (direction * 5f); p1.y = 5f; p2 = runwayEnd + (direction * 5f); p2.y = 5f;
                p0 = runwayStart - direction * (mapSize * 0.3f); p0.y = 150f; p3 = runwayEnd + direction * (mapSize * 0.3f); p3.y = 150f;
                curveLength = EstimateCurveLength(); transform.position = p0; progress = 0f;
            }
			private bool isOne = false;
            private void Update()
            {
                if (plane == null) return;
                float distanceStep = speed * Time.deltaTime; progress += distanceStep / curveLength;
                if (progress >= 1f) { plane.Kill(); return; }
                Vector3 currentPos = GetBezierPoint(progress); Vector3 futurePos = GetBezierPoint(Mathf.Clamp01(progress + 0.0025f));
                transform.position = currentPos; transform.rotation = Quaternion.LookRotation((futurePos - currentPos).normalized);
				
                HandleCrates(currentPos);
            }

            private void HandleCrates(Vector3 currentPos)
            {
                Vector3 runwayDir = (runwayEnd - runwayStart).normalized; float runwayLength = Vector3.Distance(runwayStart, runwayEnd);
                float projection = Vector3.Dot(currentPos - runwayStart, runwayDir);
                bool overRunway = projection >= 0f && projection <= runwayLength;

                if (overRunway && !droppingStarted) { droppingStarted = true; InvokeRepeating(nameof(DropEventCrate), 0f, 1.5f); }
                else if (!overRunway && droppingStarted) { droppingStarted = false; CancelInvoke(nameof(DropEventCrate)); }
            }
			private int totalDropAttempts = 0;
            private void DropEventCrate()
			{
				var diff = plugin.config.Difficulties[plugin.CurrentDifficulty];
				int maxCrates = diff.HackCrates;

				if (cratesDropped >= maxCrates) 
				{ 
					CancelInvoke(nameof(DropEventCrate)); 
					return; 
				}

				totalDropAttempts++; 

				if (maxCrates == 1 && totalDropAttempts < 2) 
				{
					return; 
				}

				if (maxCrates == 2 && totalDropAttempts < 2) 
				{
					return; 
				}

				Vector3 pos = transform.position; 
				pos.y = TerrainMeta.HeightMap.GetHeight(pos);
				
				BaseEntity crate = GameManager.server.CreateEntity(hackableCratePrefab, pos, Quaternion.identity, true);
				if (crate == null) return;
				
				crate.Spawn(); 
				plugin?.RegisterEntity(crate);
				
				int totalBradleys = diff.BradleyCount;
				if (cratesDropped < totalBradleys) 
				{
					plugin?.TrySpawnBradley(cratesDropped, totalBradleys);
				}
				
				plugin?.SpawnNpcsForCrate(pos);
				
				cratesDropped++;
			}
            private Vector3 GetBezierPoint(float t) { float u = 1f - t; return u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3; }
            private float EstimateCurveLength() {
                float length = 0f; Vector3 previous = GetBezierPoint(0f);
                for (int i = 1; i <= 200; i++) { float t = i / 200f; Vector3 current = GetBezierPoint(t); length += Vector3.Distance(previous, current); previous = current; }
                return length;
            }
            private void OnDestroy() => CancelInvoke(nameof(DropEventCrate));
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
            if(config.Difficulties[CurrentDifficulty].HeliPatrol) heliTimer1 = CreateTimer(300f, () => { SpawnPatrolHelicopter(); BroadcastMsg("HeliApproaching"); });
            if (config.Difficulties[CurrentDifficulty].HeliPatrol2) heliTimer2 = CreateTimer(600f, () => { SpawnPatrolHelicopter(); BroadcastMsg("HeliReinforcement"); });
        }

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
		{
			if (!isEventActive || entity == null || info == null || info.InitiatorPlayer == null) return; 
			
			if (entity.net != null && spawnedEntityIds.Contains(entity.net.ID.Value))
			{
				if (entity is BradleyAPC bradley)
				{
					var ai = bradley.GetComponent<BradleyEventAI>();
					if (ai != null)
					{
						ai.Investigate(info.InitiatorPlayer.transform.position);
					}
				}
			}
		}

        private bool pendingCuiUpdate = false;
		private void OnEntityKill(BaseNetworkable netEntity)
		{
			if (!isEventActive || netEntity == null) return;
			
			if (spawnedEntityIds.Contains(netEntity.net.ID.Value))
			{
				if (netEntity is NPCPlayer || netEntity is BaseNpc) npcsVivos--;
				else if (netEntity is AutoTurret) torretasVivas--;
				else if (netEntity is HackableLockedCrate || netEntity is LootContainer) cajasVivas--;
				
				spawnedEntityIds.Remove(netEntity.net.ID.Value);
				pendingCuiUpdate = true;
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
            string ammoShortName = "ammo.rifle";
            if (primaryWeapon.Contains("smg") || primaryWeapon.Contains("pistol") || primaryWeapon.Contains("m92") || primaryWeapon.Contains("python")) ammoShortName = "ammo.pistol";
            else if (primaryWeapon.Contains("shotgun") || primaryWeapon.Contains("pump")) ammoShortName = "ammo.shotgun";

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

        private BaseEntity SpawnEventEntity(string prefab, Vector3 position, Quaternion rotation, ulong skinId = 0)
		{
			BaseEntity entity = GameManager.server.CreateEntity(prefab, position, rotation);
			if (entity == null) return null; 

			if (skinId != 0) 
			{
				entity.skinID = skinId;
			}

			entity.Spawn(); 
			RegisterEntity(entity);

			if (entity is AutoTurret turret) 
			{
				turret.SetFlag(BaseEntity.Flags.On, false); 
				turret.OwnerID = 0;
				
				NextTick(() => {
					if (turret == null || turret.IsDestroyed) return;
					
					turret.inventory.Clear();
					
					var weapon = ItemManager.CreateByName("rifle.ak", 1); 
					if (weapon != null && !weapon.MoveToContainer(turret.inventory, 0)) 
					{ 
						weapon.SetParent(turret.inventory); 
						weapon.position = 0; 
					}
					
					var ammo = ItemManager.CreateByName("ammo.rifle", 1028); 
					if (ammo != null && !ammo.MoveToContainer(turret.inventory, 1)) 
					{ 
						ammo.SetParent(turret.inventory); 
						ammo.position = 1; 
					}
					
					turret.UpdateTotalAmmo(); 
					turret.inventory.MarkDirty(); 
					turret.SetFlag(BaseEntity.Flags.On, true); 
					turret.SendNetworkUpdate();
				});
			}

			return entity; 
		}

        private void SpawnPatrolHelicopter()
        {
            if (!isEventActive || cachedAirfieldMonument == null) return;
            Vector3 afPos = cachedAirfieldMonument.transform.position;
            float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            Vector3 direction = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            Vector3 spawnPos = afPos + (direction * 1000f);
            float terrainHeight = TerrainMeta.HeightMap != null ? TerrainMeta.HeightMap.GetHeight(spawnPos) : 30f;
            spawnPos.y = terrainHeight + 80f;

            BaseEntity heli = GameManager.server.CreateEntity("assets/prefabs/npc/patrol helicopter/patrolhelicopter.prefab", spawnPos, Quaternion.identity);
            if (heli == null) return;
            heli.Spawn(); RegisterEntity(heli); spawnedHelicopters.Add(heli);

            if (heli.GetComponent<PatrolHelicopterAI>() is PatrolHelicopterAI ai) ai.SetTargetDestination(afPos + new Vector3(0, 25f, 0));
            StartHeliTrackingLoop();
        }

        private void StartHeliTrackingLoop()
        {
            if (heliDestinationTimer != null) return;
            heliDestinationTimer = CreateTimerRepeat(10f, () => {
                if (!isEventActive || spawnedHelicopters.Count == 0 || cachedAirfieldMonument == null) { heliDestinationTimer?.Destroy(); heliDestinationTimer = null; return; }
                Vector3 targetPos = cachedAirfieldMonument.transform.position + new Vector3(0, 25f, 0);
                for (int i = spawnedHelicopters.Count - 1; i >= 0; i--) {
                    var heli = spawnedHelicopters[i];
                    if (heli == null || heli.IsDestroyed) { spawnedHelicopters.RemoveAt(i); continue; }
                    heli.GetComponent<PatrolHelicopterAI>()?.SetTargetDestination(targetPos);
                }
            });
        }

        private object CanHelicopterTarget(PatrolHelicopterAI heli, BasePlayer player) {
            if (isEventActive && heli?.helicopterBase != null && player != null && spawnedEntityIds.Contains(heli.helicopterBase.net.ID.Value))
                if (player.IsConnected && !player.IsDead() && !player.IsSleeping() && !(player is NPCPlayer || player.IsNpc)) return true; 
            return null;
        }

        object OnTurretTarget(AutoTurret turret, BaseEntity target) {
			if (turret?.net == null) return null;
            if (spawnedEntityIds.Contains(turret.net.ID.Value) && (target is BaseNpc || target is NPCPlayer)) return false; return null;
        }

        private object OnTurretConsumeAmmo(AutoTurret turret, Item ammo) {
			if (turret?.net == null) return null;
            if (isEventActive && turret != null && spawnedEntityIds.Contains(turret.net.ID.Value)) return false; return null;
        }

        private object CanTurretTarget(AutoTurret turret, BaseCombatEntity target) {
			if (!isEventActive || turret == null) return null;
            if (isEventActive && turret != null && target != null && spawnedEntityIds.Contains(turret.net.ID.Value) && (target is NPCPlayer || target.IsNpc)) return false; return null;
        }

        private void ProcessPlayerCUI()
        {
            if (!isEventActive || cachedAirfieldMonument == null || !config.EnableMenuInfo) return;
            Vector3 monumentPos = cachedAirfieldMonument.transform.position;
            float radiusSqr = EventZoneRadius * EventZoneRadius;
            string borderCuiColor = HexToCuiColor(config.Difficulties[CurrentDifficulty].MarkerColor, 0.85f);
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
				if (player == null || !player.IsConnected || player.IsSleeping()) continue;
                bool isInZone = (player.transform.position - monumentPos).sqrMagnitude <= radiusSqr;
                bool hasUi = playersInEventZone.Contains(player.userID);

                if (isInZone) {
                    if (!hasUi) {
                        playersInEventZone.Add(player.userID);
                        DrawEventUI_Static(player, borderCuiColor);
                        DrawEventUI_Dynamic(player, Mathf.Max(0, npcsVivos), Mathf.Max(0, torretasVivas), Mathf.Max(0, cajasVivas));
                        DrawClockCUI(player); 
                    }
                }
                else if (hasUi) {
                    playersInEventZone.Remove(player.userID);
                    CuiHelper.DestroyUi(player, "AirfieldEventUI");
                    CuiHelper.DestroyUi(player, "AirfieldEventUI_Dynamic");
                    CuiHelper.DestroyUi(player, "AirfieldEventUI_Clock");
                }
            }
        }

        private void UpdateEventCUIForce()
        {
            if (!isEventActive) return;
            foreach (ulong userId in playersInEventZone) {
                BasePlayer p = BasePlayer.FindByID(userId);
                if (p != null) DrawEventUI_Dynamic(p, Mathf.Max(0, npcsVivos), Mathf.Max(0, torretasVivas), Mathf.Max(0, cajasVivas));
            }
        }

        private void UpdateClockCUI()
		{
			if (!isEventActive) return;
			float remainingSeconds = Mathf.Max(eventEndTime - UnityEngine.Time.realtimeSinceStartup, 0f);
			TimeSpan timeSpan = TimeSpan.FromSeconds(remainingSeconds);
			string timeString = timeSpan.TotalHours >= 1 
				? string.Format("{0:00}:{1:00}:{2:00}", (int)timeSpan.TotalHours, timeSpan.Minutes, timeSpan.Seconds) 
				: string.Format("{0:00}:{1:00}", timeSpan.Minutes, timeSpan.Seconds);

			foreach (ulong userId in playersInEventZone) 
			{
				BasePlayer p = BasePlayer.FindByID(userId);
				if (p != null) 
				{
					DrawClockCUI(p, timeString);
					
					if (pendingCuiUpdate)
					{
						DrawEventUI_Dynamic(p, Mathf.Max(0, npcsVivos), Mathf.Max(0, torretasVivas), Mathf.Max(0, cajasVivas));
					}
				}
			}
			if (pendingCuiUpdate) pendingCuiUpdate = false;
		}

        private void DrawEventUI_Static(BasePlayer player, string colorBorde)
        {
            CuiHelper.DestroyUi(player, "AirfieldEventUI"); var container = new CuiElementContainer();
            container.Add(new CuiPanel { Image = { Color = "0 0 0 0" }, RectTransform = { AnchorMin = "0.36 0.90", AnchorMax = "0.64 0.93" } }, "Hud", "AirfieldEventUI");
            container.Add(new CuiPanel { Image = { Color = colorBorde }, RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" } }, "AirfieldEventUI", "AirfieldEventUI_ColorBg"); 
            container.Add(new CuiElement { Name = "AirfieldEventUI_ImageBg", Parent = "AirfieldEventUI_ColorBg", Components = { new CuiRawImageComponent { Png = imageId, Color = "1 1 1 0.8" }, new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" } } });
            container.Add(new CuiElement { Parent = "AirfieldEventUI", Components = { new CuiImageComponent { ItemId = iconNpcId }, new CuiRectTransformComponent { AnchorMin = "0.28 0.15", AnchorMax = "0.36 0.85" } } });
            container.Add(new CuiElement { Parent = "AirfieldEventUI", Components = { new CuiImageComponent { ItemId = iconTurretId }, new CuiRectTransformComponent { AnchorMin = "0.53 0.15", AnchorMax = "0.61 0.85" } } });
            container.Add(new CuiElement { Parent = "AirfieldEventUI", Components = { new CuiImageComponent { ItemId = iconCrateId }, new CuiRectTransformComponent { AnchorMin = "0.78 0.15", AnchorMax = "0.86 0.85" } } });
            CuiHelper.AddUi(player, container);
        } 

        private void DrawEventUI_Dynamic(BasePlayer player, int npcs, int torretas, int cajas)
        {
            CuiHelper.DestroyUi(player, "AirfieldEventUI_Dynamic"); var container = new CuiElementContainer();
            container.Add(new CuiPanel { Image = { Color = "0 0 0 0" }, RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" } }, "AirfieldEventUI", "AirfieldEventUI_Dynamic");
            container.Add(new CuiElement { Parent = "AirfieldEventUI_Dynamic", Components = { new CuiTextComponent { Text = $"<color=#ff4d4d>{npcs}</color>", FontSize = 16, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-bold.ttf" }, new CuiOutlineComponent { Distance = "1 -1", Color = "0 0 0 1" }, new CuiRectTransformComponent { AnchorMin = "0.38 0", AnchorMax = "0.50 1" } } });
            container.Add(new CuiElement { Parent = "AirfieldEventUI_Dynamic", Components = { new CuiTextComponent { Text = $"<color=#ffeb3b>{torretas}</color>", FontSize = 16, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-bold.ttf" }, new CuiOutlineComponent { Distance = "1 -1", Color = "0 0 0 1" }, new CuiRectTransformComponent { AnchorMin = "0.63 0", AnchorMax = "0.75 1" } } });
            container.Add(new CuiElement { Parent = "AirfieldEventUI_Dynamic", Components = { new CuiTextComponent { Text = $"<color=#4dff4d>{cajas}</color>", FontSize = 16, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-bold.ttf" }, new CuiOutlineComponent { Distance = "1 -1", Color = "0 0 0 1" }, new CuiRectTransformComponent { AnchorMin = "0.88 0", AnchorMax = "1 1" } } });
            CuiHelper.AddUi(player, container);
        }

        private void DrawClockCUI(BasePlayer player, string timeString = "00:00")
        {
            CuiHelper.DestroyUi(player, "AirfieldEventUI_Clock"); var container = new CuiElementContainer();
            container.Add(new CuiPanel { Image = { Color = "0 0 0 0" }, RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" } }, "AirfieldEventUI", "AirfieldEventUI_Clock");
            container.Add(new CuiElement { Parent = "AirfieldEventUI_Clock", Components = { new CuiTextComponent { Text = $"<color=#ffffff>⏳ {timeString}</color>", FontSize = 15, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" }, new CuiOutlineComponent { Distance = "1 -1", Color = "0 0 0 1" }, new CuiRectTransformComponent { AnchorMin = "0.02 0", AnchorMax = "0.26 1" } } });
            CuiHelper.AddUi(player, container);
        }

        private string HexToCuiColor(string hex, float alpha = 1.0f) {
            if (string.IsNullOrEmpty(hex)) return $"1 1 1 {alpha}";
            hex = hex.Replace("#", "");
            if (hex.Length == 6) {
                try {
                    float r = Convert.ToInt32(hex.Substring(0, 2), 16) / 255f;
                    float g = Convert.ToInt32(hex.Substring(2, 2), 16) / 255f;
                    float b = Convert.ToInt32(hex.Substring(4, 2), 16) / 255f;
                    return $"{r} {g} {b} {alpha}";
                } catch { return $"1 1 1 {alpha}"; }
            }
            return $"1 1 1 {alpha}";
        }

        private void StartEvent(BasePlayer triggerPlayer, int Dificultad)
        {
            if (isEventActive) { if (triggerPlayer != null) SendMsg(triggerPlayer, "EventAlreadyActive"); return; }
            if (cachedAirfieldMonument == null) { if (triggerPlayer != null) SendMsg(triggerPlayer, "ErrorMonument"); return; }

            isEventActive = true; hasCrateBeenHacked = false;
            torretasVivas = cajasVivas = npcsVivos = 0; 

            Puts("Iniciando Evento en el Airfield..."); BroadcastMsg("EventWarning");
            if (config?.Difficulties == null) return;
            
            var availableDifficulties = config.Difficulties.Where(kvp => kvp.Value != null && kvp.Value.Enabled).Select(kvp => kvp.Key).ToList();
            if (availableDifficulties.Count == 0) availableDifficulties = config.Difficulties.Keys.ToList();
            
            CurrentDifficulty = (Dificultad == -1 || Dificultad < 0 || Dificultad > 5) ? availableDifficulties[UnityEngine.Random.Range(0, availableDifficulties.Count)] : Dificultad;
            var diff = config.Difficulties[CurrentDifficulty]; Transform rootTransform = cachedAirfieldMonument.transform;
            
            CreateTimer((dev ? 1f : 180f), () => {
				eventEndTime = UnityEngine.Time.realtimeSinceStartup + (config.EventDurationMinutes * 60f);
				
                if (spawnCoroutine != null) ServerMgr.Instance.StopCoroutine(spawnCoroutine);
                spawnCoroutine = ServerMgr.Instance.StartCoroutine(SpawnEventDefensesCoroutine(rootTransform));
                
                CreateEventMapMarkers(rootTransform.position);
                UpdateVendingMarkerTime();
                markerUpdateTimer = CreateTimerRepeat(10f, UpdateVendingMarkerTime);
                
                playerCheckTimer = CreateTimerRepeat(2f, ProcessPlayerCUI); 
                clockTimer = CreateTimerRepeat(5f, UpdateClockCUI);

                SpawnEventPlane(rootTransform);
                BroadcastMsg("EventStarted"); BroadcastMsg("EventDifficulty", diff.MarkerColor, diff.Name.ToUpper());
                eventTimer = CreateTimer(config.EventDurationMinutes * 60f, () => { BroadcastMsg("EventEnded"); CleanUpEvent(false); });
            });
        }

        private IEnumerator SpawnEventDefensesCoroutine(Transform rootTransform)
        {
            int maxCratesToSpawn = Math.Min(config.Difficulties[CurrentDifficulty].EliteFoxes, Event_EliteCrates.Count);
            if (maxCratesToSpawn > 0) {
                List<int> availableIndices = Enumerable.Range(0, Event_EliteCrates.Count).ToList();
                bool radtownElite = false;
                for (int i = 0; i < maxCratesToSpawn; i++) {
                    int randomIndex = UnityEngine.Random.Range(0, availableIndices.Count); int selectedIndex = availableIndices[randomIndex]; availableIndices.RemoveAt(randomIndex);
                    var data = Event_EliteCrates[selectedIndex]; Vector3 elitePos = rootTransform.TransformPoint(data.Pos); Quaternion eliteRot = rootTransform.rotation * Quaternion.Euler(data.Rot);
                    string prefab = radtownElite ? "assets/bundled/prefabs/radtown/crate_elite.prefab" : "assets/prefabs/npc/m2bradley/bradley_crate.prefab";
                    SpawnEventEntity(prefab, elitePos, eliteRot); radtownElite = !radtownElite; yield return wait05;
                }
            }
			
            foreach (var data in Event_Turrets) {
                Vector3 turretPos = rootTransform.TransformPoint(data.Pos); Quaternion rotation = (data.Rot == Vector3.zero) ? Quaternion.LookRotation(turretPos - rootTransform.position) : rootTransform.rotation * Quaternion.Euler(data.Rot);
				ulong skinId = config.Difficulties[CurrentDifficulty].skinTurret;
				SpawnEventEntity("assets/prefabs/npc/autoturret/autoturret_deployed.prefab", turretPos, rotation, skinId); yield return wait05;
            }

            if (NpcSpawn != null) {
                for (int i = 0; i < Event_NPCs.Count; i++) {
                    Vector3 npcPos = rootTransform.TransformPoint(Event_NPCs[i]); JObject npcProfile = GetNpcProfile(i);
                    object npcEntity = NpcSpawn.Call("SpawnCustom", npcPos, npcProfile) ?? NpcSpawn.Call("SpawnNpc", npcPos, npcProfile);
                    if (npcEntity is BaseEntity entity) RegisterEntity(entity, true); yield return wait10;
                }
            }

            foreach (var data in Event_MetalDoor) {
                Vector3 doorPos = rootTransform.TransformPoint(data.Pos); Quaternion doorRot = (data.Rot == Vector3.zero) ? Quaternion.LookRotation(doorPos - rootTransform.position) : rootTransform.rotation * Quaternion.Euler(data.Rot);
                if (GameManager.server.CreateEntity("assets/prefabs/building/door.hinged/door.hinged.metal.prefab", doorPos, doorRot) is Door door) {
                    door.skinID = config.Difficulties[CurrentDifficulty].skinDoor; if (door is StabilityEntity stab) stab.grounded = true; 
                    door.Spawn(); RegisterEntity(door);
                    if (GameManager.server.CreateEntity("assets/prefabs/locks/keypad/lock.code.prefab", Vector3.zero, Quaternion.identity) is CodeLock codeLock) {
                        codeLock.code = $"{UnityEngine.Random.Range(0, 10000):D4}"; codeLock.Spawn(); 
                        codeLock.SetParent(door, door.GetSlotAnchorName(BaseEntity.Slot.Lock)); door.SetSlot(BaseEntity.Slot.Lock, codeLock); codeLock.SetFlag(BaseEntity.Flags.Locked, true);
                    }
                }
                yield return wait05; 
            }
            spawnCoroutine = null;
        }

        private void CleanUpEvent(bool manualStop)
        {
            if (!isEventActive) return;
            
            if (playerCheckTimer != null) { activeTimers.Remove(playerCheckTimer); playerCheckTimer.Destroy(); playerCheckTimer = null; }
            if (clockTimer != null) { activeTimers.Remove(clockTimer); clockTimer.Destroy(); clockTimer = null; }
            if (markerUpdateTimer != null) { activeTimers.Remove(markerUpdateTimer); markerUpdateTimer.Destroy(); markerUpdateTimer = null; }
            if (heliTimer1 != null) { activeTimers.Remove(heliTimer1); heliTimer1.Destroy(); heliTimer1 = null; }
            if (heliTimer2 != null) { activeTimers.Remove(heliTimer2); heliTimer2.Destroy(); heliTimer2 = null; }
            if (heliDestinationTimer != null) { activeTimers.Remove(heliDestinationTimer); heliDestinationTimer.Destroy(); heliDestinationTimer = null; }
            if (eventTimer != null) { activeTimers.Remove(eventTimer); eventTimer.Destroy(); eventTimer = null; }

            foreach (ulong userId in playersInEventZone) {
                BasePlayer player = BasePlayer.FindByID(userId);
                if (player != null) { CuiHelper.DestroyUi(player, "AirfieldEventUI"); CuiHelper.DestroyUi(player, "AirfieldEventUI_Dynamic"); CuiHelper.DestroyUi(player, "AirfieldEventUI_Clock"); }
            }
            playersInEventZone.Clear();
            
            if (redCircleMarker != null && !redCircleMarker.IsDestroyed) redCircleMarker.Kill();
            if (shopMapMarker != null && !shopMapMarker.IsDestroyed) shopMapMarker.Kill();
            redCircleMarker = null; shopMapMarker = null;

            if (spawnCoroutine != null) { ServerMgr.Instance.StopCoroutine(spawnCoroutine); spawnCoroutine = null; }
            if (activePlane != null && !activePlane.IsDestroyed) { activePlane.Kill(); activePlane = null; }

            for (int i = spawnedEntities.Count - 1; i >= 0; i--) {
                var entity = spawnedEntities[i];
                if (entity != null && !entity.IsDestroyed) entity.Kill();
            }
            spawnedEntities.Clear(); spawnedEntityIds.Clear(); spawnedHelicopters.Clear();
            isEventActive = false; hasCrateBeenHacked = false;
            torretasVivas = cajasVivas = npcsVivos = 0;
        }

        private readonly Dictionary<ulong, string> _playerActiveNpcTab = new Dictionary<ulong, string>();
        private string GetNextItem(List<string> itemsList, string currentItem) {
            int currentIdx = itemsList.IndexOf(currentItem);
            return itemsList[(currentIdx + 1) >= itemsList.Count ? 0 : currentIdx + 1];
        }

        [ConsoleCommand("airfield.cui")]
        private void CmdCuiAdmin(ConsoleSystem.Arg arg) 
        {
            var player = arg.Player(); if (player == null || !player.IsAdmin) return;
            string action = arg.GetString(0);
            if (action == "close") { CuiHelper.DestroyUi(player, "AirfieldAdminUI"); return; }
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
                case "add_npcbox": if (diffSettings != null) diffSettings.NpcForBox = Mathf.Clamp(diffSettings.NpcForBox + arg.GetInt(2), 0, 20); break; 
                case "add_foxes": if (diffSettings != null) diffSettings.EliteFoxes = Mathf.Clamp(diffSettings.EliteFoxes + arg.GetInt(2), 0, 5); break; 
                case "toggle_autostart": config.EnableAutoStart = !config.EnableAutoStart; break;
                case "toggle_menuinfo": config.EnableMenuInfo = !config.EnableMenuInfo; break;
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
                case "save": SaveConfig(); player.ChatMessage("<color=#4dff4d>[Airfield]</color> Configuración guardada correctamente."); break;
            }
            OpenAdminMenu(player, diffId, isRefresh); 
        }

        private void OpenAdminMenu(BasePlayer player, int diffId, bool isRefresh = false)
        {
            if (!isRefresh) {
                CuiHelper.DestroyUi(player, "AirfieldAdminUI"); var baseContainer = new CuiElementContainer();
                baseContainer.Add(new CuiPanel { Image = { Color = "0.08 0.08 0.09 0.98" }, RectTransform = { AnchorMin = "0.15 0.15", AnchorMax = "0.85 0.85" }, CursorEnabled = true }, "Overlay", "AirfieldAdminUI");
                baseContainer.Add(new CuiElement { Parent = "AirfieldAdminUI", Components = { new CuiTextComponent { Text = "⚙✈️✈️ AIRFIELD EVENT - CONFIGURACIÓN ✈️✈️⚙", FontSize = 20, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", Color = "#e5a93c" }, new CuiOutlineComponent { Distance = "1 -1", Color = "0 0 0 0.8" }, new CuiRectTransformComponent { AnchorMin = "0 0.9", AnchorMax = "1 1" } } });
                baseContainer.Add(new CuiButton { Button = { Command = "airfield.cui close", Color = "0.70 0.22 0.22 1" }, RectTransform = { AnchorMin = "0.95 0.91", AnchorMax = "0.98 0.97" }, Text = { Text = "X", FontSize = 14, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" } }, "AirfieldAdminUI");
                baseContainer.Add(new CuiButton { Button = { Command = $"airfield.cui save {diffId}", Color = "0.18 0.58 0.32 1" }, RectTransform = { AnchorMin = "0.80 0.91", AnchorMax = "0.94 0.97" }, Text = { Text = "GUARDAR", FontSize = 11, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" } }, "AirfieldAdminUI");
				baseContainer.Add(new CuiPanel { Image = { Color = "0.05 0.05 0.06 1" }, RectTransform = { AnchorMin = "0.02 0.05", AnchorMax = "0.25 0.88" } }, "AirfieldAdminUI", "AirfieldAdminUI_Left");
                float currentY = 0.82f; float btnHeight = 0.10f; string basicColor = "0.68 0.38 0.16 1"; bool basicSelected = (diffId == -1);
                baseContainer.Add(new CuiButton { Button = { Command = "airfield.cui view -1", Color = basicSelected ? basicColor : "0.13 0.13 0.15 1" }, RectTransform = { AnchorMin = $"0.1 {currentY}", AnchorMax = $"0.9 {currentY + 0.10f}" }, Text = { Text = "AJUSTES BÁSICOS", FontSize = 12, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", Color = basicSelected ? "1 1 1 1" : basicColor } }, "AirfieldAdminUI_Left");
                currentY -= 0.13f;

                foreach (var kvp in config.Difficulties) {
                    int id = kvp.Key; string name = kvp.Value.Name.ToUpper(); string diffColorRgba = HexToCuiColor(kvp.Value.MarkerColor); bool isSelected = (id == diffId);
                    string finalBgColor = isSelected ? diffColorRgba : "0.13 0.13 0.15 1"; string finalTextColor = isSelected ? "1 1 1 1" : diffColorRgba; float yMin = currentY; float yMax = yMin + btnHeight;
                    baseContainer.Add(new CuiButton { Button = { Command = $"airfield.cui view {id}", Color = finalBgColor }, RectTransform = { AnchorMin = $"0.1 {yMin}", AnchorMax = $"0.9 {yMax}" }, Text = { Text = name, FontSize = 12, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", Color = finalTextColor } }, "AirfieldAdminUI_Left"); currentY -= (btnHeight + 0.015f);
                }
                baseContainer.Add(new CuiPanel { Image = { Color = "0.06 0.06 0.07 1" }, RectTransform = { AnchorMin = "0.27 0.05", AnchorMax = "0.98 0.88" } }, "AirfieldAdminUI", "AirfieldAdminUI_Right");
                CuiHelper.AddUi(player, baseContainer);
            }

            string contentPanelName = "AirfieldAdminUI_Content"; CuiHelper.DestroyUi(player, contentPanelName);
            var contentContainer = new CuiElementContainer(); contentContainer.Add(new CuiPanel { Image = { Color = "0 0 0 0" }, RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" } }, "AirfieldAdminUI_Right", contentPanelName);

            int rowIndex = 0; float rowSpacing = 0.085f;
            Action<string, string, string, string, string> DrawToggleRow = (label, valText, valColor, cmdToggle, desc) => { float yMax = 0.96f - (rowIndex * rowSpacing); float yMin = yMax - 0.07f; contentContainer.Add(new CuiElement { Parent = contentPanelName, Components = { new CuiTextComponent { Text = label, FontSize = 13, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-bold.ttf" }, new CuiOutlineComponent { Distance = "1 -1", Color = "0 0 0 0.6" }, new CuiRectTransformComponent { AnchorMin = $"0.04 {yMin}", AnchorMax = $"0.3 {yMax}" } } }); contentContainer.Add(new CuiButton { Button = { Command = cmdToggle, Color = valColor }, RectTransform = { AnchorMin = $"0.3 {yMin}", AnchorMax = $"0.45 {yMax}" }, Text = { Text = valText, FontSize = 11, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" } }, contentPanelName); contentContainer.Add(new CuiElement { Parent = contentPanelName, Components = { new CuiTextComponent { Text = desc, FontSize = 11, Color = "0.65 0.65 0.65 1", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf" }, new CuiOutlineComponent { Distance = "1 -1", Color = "0 0 0 0.4" }, new CuiRectTransformComponent { AnchorMin = $"0.48 {yMin}", AnchorMax = $"0.95 {yMax}" } } }); rowIndex++; };
            Action<string, string, string, string> DrawNumberRow = (label, valText, cmdMinus, cmdPlus) => { float yMax = 0.96f - (rowIndex * rowSpacing); float yMin = yMax - 0.07f; contentContainer.Add(new CuiElement { Parent = contentPanelName, Components = { new CuiTextComponent { Text = label, FontSize = 13, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-bold.ttf" }, new CuiOutlineComponent { Distance = "1 -1", Color = "0 0 0 0.6" }, new CuiRectTransformComponent { AnchorMin = $"0.04 {yMin}", AnchorMax = $"0.3 {yMax}" } } }); contentContainer.Add(new CuiButton { Button = { Command = cmdMinus, Color = "0.65 0.25 0.25 1" }, RectTransform = { AnchorMin = $"0.3 {yMin}", AnchorMax = $"0.35 {yMax}" }, Text = { Text = "-", FontSize = 14, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" } }, contentPanelName); string valBg = "valBg_" + rowIndex; contentContainer.Add(new CuiPanel { Image = { Color = "0.11 0.11 0.13 1" }, RectTransform = { AnchorMin = $"0.36 {yMin}", AnchorMax = $"0.44 {yMax}" } }, contentPanelName, valBg); contentContainer.Add(new CuiElement { Parent = valBg, Components = { new CuiTextComponent { Text = valText, FontSize = 12, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", Color = "#ffeb3b" }, new CuiOutlineComponent { Distance = "1 -1", Color = "0 0 0 0.5" }, new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" } } }); contentContainer.Add(new CuiButton { Button = { Command = cmdPlus, Color = "0.25 0.55 0.25 1" }, RectTransform = { AnchorMin = $"0.45 {yMin}", AnchorMax = $"0.5 {yMax}" }, Text = { Text = "+", FontSize = 14, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" } }, contentPanelName); rowIndex++; };

            if (diffId == -1) {
                DrawToggleRow("Temporizador Autostart:", config.EnableAutoStart ? "ACTIVADO" : "DESACTIVADO", config.EnableAutoStart ? "0.20 0.55 0.30 1" : "0.65 0.25 0.25 1", $"airfield.cui toggle_autostart {diffId}", "Habilita o deshabilita por completo que el evento se lance solo periódicamente."); DrawNumberRow("Autostart Mínimo (Horas):", config.AutoStartIntervalHoursMin.ToString("0.0"), $"airfield.cui add_automin {diffId} -0.5", $"airfield.cui add_automin {diffId} 0.5"); DrawNumberRow("Autostart Máximo (Horas):", config.AutoStartIntervalHoursMax.ToString("0.0"), $"airfield.cui add_automax {diffId} -0.5", $"airfield.cui add_automax {diffId} 0.5"); DrawNumberRow("Duración Evento (Minutos):", config.EventDurationMinutes.ToString("0.0"), $"airfield.cui add_duration {diffId} -5", $"airfield.cui add_duration {diffId} 5");
                DrawToggleRow("Menu info Evento:", config.EnableMenuInfo ? "ACTIVADO" : "DESACTIVADO", config.EnableMenuInfo ? "0.20 0.55 0.30 1" : "0.65 0.25 0.25 1", $"airfield.cui toggle_menuinfo {diffId}", "Habilita o deshabilita el menu de informacion en el evento."); 
                float npcStartY = 0.96f - (rowIndex * rowSpacing) - 0.01f; contentContainer.Add(new CuiElement { Parent = contentPanelName, Components = { new CuiTextComponent { Text = "⚔ PERSONALIZACIÓN QUIRÚRGICA DE CLONES NPC (ROPA Y ARMAS) ⚔", FontSize = 11, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", Color = "#6aa1d4" }, new CuiOutlineComponent { Distance = "1 -1", Color = "0 0 0 0.8" }, new CuiRectTransformComponent { AnchorMin = $"0.04 {npcStartY - 0.04f}", AnchorMax = $"0.96 {npcStartY}" } } }); npcStartY -= 0.045f;
                if (!_playerActiveNpcTab.TryGetValue(player.userID, out string selectedKey)) { selectedKey = "normal"; _playerActiveNpcTab[player.userID] = selectedKey; }
                if (!config.NpcProfiles.TryGetValue(selectedKey, out NpcProfile npc)) npc = new NpcProfile { Name = "Asalto", BaseHealth = 120f, Weapon = "smg.mp5" };

                float tabYMax = npcStartY; float tabYMin = tabYMax - 0.045f; string[] profileKeys = { "normal", "elite", "boss" }; string[] profileLabels = { "⚙ COMPAÑÍA ASALTO", "☠ GUARDIA ÉLITE", "👑 JEFE DE BASE" }; float tabWidth = 0.18f; float tabStartO = 0.40f;
                for (int t = 0; t < profileKeys.Length; t++) { string pKey = profileKeys[t]; string pLabel = profileLabels[t]; string tabColor = (pKey == selectedKey) ? "0.18 0.44 0.68 1" : "0.13 0.13 0.15 1"; contentContainer.Add(new CuiButton { Button = { Command = $"airfield.cui npc_tab {diffId} {pKey}", Color = tabColor }, RectTransform = { AnchorMin = $"{tabStartO + (t * (tabWidth + 0.01f))} {tabYMin}", AnchorMax = $"{tabStartO + tabWidth + (t * (tabWidth + 0.01f))} {tabYMax}" }, Text = { Text = pLabel, FontSize = 9, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" } }, contentPanelName); }
                contentContainer.Add(new CuiElement { Parent = contentPanelName, Components = { new CuiTextComponent { Text = "Vida Base:", FontSize = 11, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-bold.ttf", Color = "0.85 0.85 0.85 1" }, new CuiRectTransformComponent { AnchorMin = $"0.04 {tabYMin}", AnchorMax = $"0.15 {tabYMax}" } } }); contentContainer.Add(new CuiButton { Button = { Command = $"airfield.cui npc_health {diffId} {selectedKey} -25", Color = "0.55 0.2 0.2 1" }, RectTransform = { AnchorMin = $"0.15 {tabYMin}", AnchorMax = $"0.18 {tabYMax}" }, Text = { Text = "-", FontSize = 12 } }, contentPanelName); contentContainer.Add(new CuiPanel { Image = { Color = "0.15 0.15 0.17 1" }, RectTransform = { AnchorMin = $"0.185 {tabYMin}", AnchorMax = $"0.295 {tabYMax}" } }, contentPanelName, "hp_container"); contentContainer.Add(new CuiElement { Parent = "hp_container", Components = { new CuiTextComponent { Text = npc.BaseHealth.ToString("0"), FontSize = 11, Align = TextAnchor.MiddleCenter, Color = "#ffeb3b", Font = "robotocondensed-bold.ttf" } } }); contentContainer.Add(new CuiButton { Button = { Command = $"airfield.cui npc_health {diffId} {selectedKey} 25", Color = "0.2 0.5 0.2 1" }, RectTransform = { AnchorMin = $"0.30 {tabYMin}", AnchorMax = $"0.33 {tabYMax}" }, Text = { Text = "+", FontSize = 12 } }, contentPanelName);

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
                    contentContainer.Add(new CuiButton { Button = { Command = $"airfield.cui npc_cycle_item {diffId} {selectedKey} {slot}", Color = "0.22 0.35 0.55 1" }, RectTransform = { AnchorMin = $"0.27 {yMin}", AnchorMax = $"0.57 {yMax}" }, Text = { Text = itemShortname, FontSize = 10, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" } }, contentPanelName);
                    string sBox = $"sb_{selectedKey}_{slot}"; contentContainer.Add(new CuiPanel { Image = { Color = "0.15 0.15 0.17 1" }, RectTransform = { AnchorMin = $"0.60 {yMin}", AnchorMax = $"0.85 {yMax}" } }, contentPanelName, sBox);
                    contentContainer.Add(new CuiElement { Parent = sBox, Components = { new CuiInputFieldComponent { Text = itemSkin.ToString(), FontSize = 11, Align = TextAnchor.MiddleCenter, Color = "#ffeb3b", Command = $"airfield.cui npc_set_skin {diffId} {selectedKey} {slot} " }, new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" } } });
                    contentContainer.Add(new CuiElement { Parent = contentPanelName, Components = { new CuiTextComponent { Text = "INTRO ↵", FontSize = 9, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", Color = "0.4 0.8 0.4 1" }, new CuiRectTransformComponent { AnchorMin = $"0.86 {yMin}", AnchorMax = $"0.95 {yMax}" } } });
                }
            }
            else if (config.Difficulties.ContainsKey(diffId)) {
				var diff = config.Difficulties[diffId];
				DrawToggleRow("Estado de Dificultad:", diff.Enabled ? "ACTIVADO" : "DESACTIVADO", diff.Enabled ? "0.20 0.55 0.30 1" : "0.65 0.25 0.25 1", $"airfield.cui toggle_enabled {diffId}", "Si está apagado, este nivel de dificultad nunca será seleccionado de forma aleatoria."); 
				DrawToggleRow("Tanque Bradley:", diff.Bradley ? "HABILITADO" : "DESHABILITADO", diff.Bradley ? "0.20 0.55 0.30 1" : "0.65 0.25 0.25 1", $"airfield.cui toggle_bradley {diffId}", "Determina si patrullarán tanques blindados Bradley en la zona de conflicto."); 
				DrawToggleRow("Helicóptero Patrulla:", diff.HeliPatrol ? "HABILITADO" : "DESHABILITADO", diff.HeliPatrol ? "0.20 0.55 0.30 1" : "0.65 0.25 0.25 1", $"airfield.cui toggle_heli {diffId}", "Envía un soporte aéreo pesado a los 5 minutos de iniciarse el hackeo."); 
				DrawToggleRow("Helicóptero Patrulla 2:", diff.HeliPatrol2 ? "HABILITADO" : "DESHABILITADO", diff.HeliPatrol2 ? "0.20 0.55 0.30 1" : "0.65 0.25 0.25 1", $"airfield.cui toggle_heli2 {diffId}", "Envía un segundo soporte aéreo a los 10 minutos de iniciarse el hackeo."); 
				DrawNumberRow("Cantidad Bradleys:", diff.BradleyCount.ToString(), $"airfield.cui add_bradley {diffId} -1", $"airfield.cui add_bradley {diffId} 1"); 
				DrawNumberRow("Multiplicador Vida (NPC):", diff.HealthMultiplier.ToString("0.0"), $"airfield.cui add_health {diffId} -0.1", $"airfield.cui add_health {diffId} 0.1"); 
				DrawNumberRow("Multiplicador Daño (NPC):", diff.AimDamageMultiplier.ToString("0.0"), $"airfield.cui add_dmg {diffId} -0.1", $"airfield.cui add_dmg {diffId} 0.1"); 
				DrawNumberRow("Cajas Codelock (Hack):", diff.HackCrates.ToString(), $"airfield.cui add_hackcrates {diffId} -1", $"airfield.cui add_hackcrates {diffId} 1"); 
				DrawNumberRow("NPCs de Custodia x Caja:", diff.NpcForBox.ToString(), $"airfield.cui add_npcbox {diffId} -1", $"airfield.cui add_npcbox {diffId} 1");  
				DrawNumberRow("Cajas de Suministro Élite:", diff.EliteFoxes.ToString(), $"airfield.cui add_foxes {diffId} -1", $"airfield.cui add_foxes {diffId} 1");      

				float yMaxTurret = 0.96f - (rowIndex * rowSpacing); float yMinTurret = yMaxTurret - 0.07f;
				contentContainer.Add(new CuiElement { Parent = contentPanelName, Components = { new CuiTextComponent { Text = "Skin Torretas:", FontSize = 13, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-bold.ttf" }, new CuiOutlineComponent { Distance = "1 -1", Color = "0 0 0 0.6" }, new CuiRectTransformComponent { AnchorMin = $"0.04 {yMinTurret}", AnchorMax = $"0.3 {yMaxTurret}" } } });
				string turretBox = "turret_skin_box";
				contentContainer.Add(new CuiPanel { Image = { Color = "0.15 0.15 0.17 1" }, RectTransform = { AnchorMin = $"0.3 {yMinTurret}", AnchorMax = $"0.55 {yMaxTurret}" } }, contentPanelName, turretBox);
				contentContainer.Add(new CuiElement { Parent = turretBox, Components = { new CuiInputFieldComponent { Text = diff.skinTurret.ToString(), FontSize = 11, Align = TextAnchor.MiddleCenter, Color = "#ffeb3b", Command = $"airfield.cui set_turretskin {diffId} " }, new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" } } });
				contentContainer.Add(new CuiElement { Parent = contentPanelName, Components = { new CuiTextComponent { Text = "ENTER ↵", FontSize = 9, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-bold.ttf", Color = "0.4 0.8 0.4 1" }, new CuiRectTransformComponent { AnchorMin = $"0.57 {yMinTurret}", AnchorMax = $"0.95 {yMaxTurret}" } } });
				rowIndex++;

				float yMaxDoor = 0.96f - (rowIndex * rowSpacing); float yMinDoor = yMaxDoor - 0.07f;
				contentContainer.Add(new CuiElement { Parent = contentPanelName, Components = { new CuiTextComponent { Text = "Skin Puertas:", FontSize = 13, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-bold.ttf" }, new CuiOutlineComponent { Distance = "1 -1", Color = "0 0 0 0.6" }, new CuiRectTransformComponent { AnchorMin = $"0.04 {yMinDoor}", AnchorMax = $"0.3 {yMaxDoor}" } } });
				string doorBox = "door_skin_box";
				contentContainer.Add(new CuiPanel { Image = { Color = "0.15 0.15 0.17 1" }, RectTransform = { AnchorMin = $"0.3 {yMinDoor}", AnchorMax = $"0.55 {yMaxDoor}" } }, contentPanelName, doorBox);
				contentContainer.Add(new CuiElement { Parent = doorBox, Components = { new CuiInputFieldComponent { Text = diff.skinDoor.ToString(), FontSize = 11, Align = TextAnchor.MiddleCenter, Color = "#ffeb3b", Command = $"airfield.cui set_doorskin {diffId} " }, new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" } } });
				contentContainer.Add(new CuiElement { Parent = contentPanelName, Components = { new CuiTextComponent { Text = "ENTER ↵", FontSize = 9, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-bold.ttf", Color = "0.4 0.8 0.4 1" }, new CuiRectTransformComponent { AnchorMin = $"0.57 {yMinDoor}", AnchorMax = $"0.95 {yMaxDoor}" } } });
				rowIndex++;
			}
            CuiHelper.AddUi(player, contentContainer);
        }
    }
}