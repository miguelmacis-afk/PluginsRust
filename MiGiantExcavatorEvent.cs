using System;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
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
    [Info("Giant Excavator Event", "☠ Ⓜ🅘ⓖ🅤ⓔ🅛 ☠", "31.1.0")]
    [Description("Automatiza el evento con corrutinas, UI dinámica y panel de administración avanzado.")]
    public class MiGiantExcavatorEvent : RustPlugin
    {
        [PluginReference] private Plugin NpcSpawn;

        private PluginConfig config;
        private bool dev = false;
        private readonly List<BaseEntity> spawnedEntities = new List<BaseEntity>();
        private readonly HashSet<ulong> spawnedEntityIds = new HashSet<ulong>();
        private readonly List<StorageContainer> cachedOutputPiles = new List<StorageContainer>();
        private readonly HashSet<ulong> cachedOutputPileIds = new HashSet<ulong>();

        private DieselEngine cachedEngine;
        private MonoBehaviour cachedArm;
        private Coroutine spawnCoroutine;
        private BaseEntity cachedSignalComputer;
        private Component cachedSignalComputerComponent;
        private MonumentInfo cachedExcavatorMonument;

        private FieldInfo miningIndexField;
        private MethodInfo beginMiningMethod;
        private MethodInfo stopMiningMethod;
        private MethodInfo engineOnMethod;
        private MethodInfo engineSetIsOnMethod;
        private MethodInfo engineMarkDirtyMethod;
        private MethodInfo requestSuppliesMethod;

        private readonly List<Timer> activeTimers = new List<Timer>();
        private Timer eventTimer, autoStartTimer, delayTimer, monitoringTimer, cooldownTimer, markerUpdateTimer, clockTimer, playerCheckTimer;

        private const float EventZoneRadius = 200f; 
        private readonly HashSet<ulong> playersInEventZone = new HashSet<ulong>();

        private int torretasVivas = 0;
        private int cajasVivas = 0;
        private int npcsVivos = 0;
        private bool isEventActive = false;
        private bool isLooteable = false;
        private Dictionary<ulong, string> hopperLooters = new Dictionary<ulong, string>();

        // --- LISTAS DE ROPA Y CUI ---
        private readonly List<string> availableWeapons = new List<string> { "smg.mp5", "rifle.ak", "rifle.lr300", "smg.thompson", "shotgun.pump", "lmg.m249", "rifle.bolt", "minigun" };
        private readonly List<string> availableHelmets = new List<string> { "", "metal.facemask", "coffeecan.helmet", "heavy.plate.helmet", "hazmatsuit_scientist_nvgm", "scientistsuit_heavy", "deer.skull.mask" };
        private readonly List<string> availableTorsos = new List<string> { "", "metal.plate.torso", "roadsign.jacket", "jacket", "hazmatsuit_scientist_nvgm", "scientistsuit_heavy" };
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

        private readonly Vector3 LocalCrateOffset = new Vector3(87.03f, -2.59f, -18.48f);

        private readonly List<SpawnData> Event_Turrets = new List<SpawnData> 
        { 
            new SpawnData(new Vector3(66f, 2.09f, 41.37f), new Vector3(0f, 21.8f, 0f)), 
            new SpawnData(new Vector3(60.26f, 5.56f, 57.01f), new Vector3(0f, 67.2f, 0f)),
            new SpawnData(new Vector3(-35.32f, 6.66f, -11.94f), new Vector3(0f, 302.4f, 0f)),
            new SpawnData(new Vector3(-65.64f, 4.47f, 0.24f), new Vector3(0f, 271.7f, 0f)),
            new SpawnData( new Vector3(90.88f, -1.48f, -8.75f), new Vector3(0f, 217.7f, 0f)),
            new SpawnData(new Vector3(92.81f, -0.53f, -26.44f), new Vector3(0f, 318.8f, 0f)),
            new SpawnData(new Vector3(54.31f, 7.02f, -39.65f), new Vector3(0f, 283.2f, 0f)),
            new SpawnData(new Vector3(-7.34f, 5.43f, 55.35f), new Vector3(0f, 168.3f, 0f)), 
            new SpawnData(new Vector3(41.18f, 6.01f, -37.85f), new Vector3(0f, 89.7f, 0f)) 
        };

        private readonly List<Vector3> Event_NPCs = new List<Vector3> 
        {
            new Vector3(61.6f, 0.8f, 40.3f), new Vector3(63.3f, 1.3f, 39.2f),
            new Vector3(67.9f, 1.6f, 38.1f), new Vector3(69.5f, 1f, 38.5f),
            new Vector3(57.6f, 1.2f, 53.5f), new Vector3(61.4f, 1.2f, 53.2f),
            new Vector3(63.5f, 1.2f, 52.6f), new Vector3(65.5f, 0.9f, 51.7f),
            new Vector3(67.52f, 3.02f, -31.41f), new Vector3(19.95f, 16.52f, 2.11f),
            new Vector3(8.18f, 18.57f, 13.75f), new Vector3(2.08f, 24.23f, 20.36f),
            new Vector3(-4.52f, 24.23f, 14.86f), new Vector3(4.35f, 22.46f, 3.78f),
            new Vector3(15.08f, 20.38f, -9.05f), new Vector3(21f, 14.71f, -16.01f),
            new Vector3(-68f, 5.4f, 21.9f), new Vector3(-17.4f, 4.3f, 13.1f),
			new Vector3(-62.3f, 1.1f, 26.5f), new Vector3(-37.9f, 1.2f, 27.9f),
            new Vector3(-61.7f, 1.4f, 8.4f), new Vector3(-40.9f, 1.4f, 34.3f),
            new Vector3(-49.3f, 0.7f, 18.9f), new Vector3(-60.8f, 0.8f, 11.7f),
            new Vector3(96.23f, -6.42f, -26.65f), new Vector3(95.05f, -6.24f, -20.04f),
            new Vector3(92.87f, -6.11f, -15.31f),  new Vector3(94.2f, -6.43f, -8.33f),
            new Vector3(88.88f, -6.54f, -4.65f), new Vector3(81.45f, -6.36f, -24.56f),
            new Vector3(79.36f, -6.51f, -17.93f),  new Vector3(81.79f, -6.35f, -10.91f),
            new Vector3(39.1f, 2f, -40.4f), new Vector3(54.09f, 7.02f, -45.92f),
            new Vector3(56.39f, 7.02f, -33.52f), new Vector3(37.2f, 2f, -28.2f),
            new Vector3(51.2f, 4.5f, -38.7f), new Vector3(41.8f, -1.5f, -54.1f),
			new Vector3(48f, -1.5f, -22.2f)
        };

        public class DifficultySettings
        {
            public string Name { get; set; } 
            public bool Enabled { get; set; }
            public int Resource { get; set; }
            public int MinDiesel { get; set; }
            public int MaxDiesel { get; set; }
            public float HealthMultiplier { get; set; }
            public float RangeMultiplier { get; set; }
            public float AimDamageMultiplier { get; set; }
            public ulong skinTurret { get; set; }
            public string MarkerColor { get; set; }
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

        private class PluginConfig
        {
            [JsonProperty("Dificultad Actual (0=easy, 1=normal, 2=medium, 3=hard, 4=expert, 5=nightmare)")]
            public int CurrentDifficulty { get; set; } = 5; 

            [JsonProperty("Activar Menu informacion del evento")]
            public bool EnableMenuInfo { get; set; } = true;

            [JsonProperty("Activar Autostart Automático")]
            public bool EnableAutoStart { get; set; } = true;

            [JsonProperty("Ajustes de Dificultades")]
            public Dictionary<int, DifficultySettings> Difficulties { get; set; } 

            [JsonProperty("Intervalo MINIMO de inicio automático (horas)")]
            public float AutoStartIntervalHoursMin { get; set; } = 7f;
            [JsonProperty("Intervalo MAXIMO de inicio automático (horas)")]
            public float AutoStartIntervalHoursMax { get; set; } = 11f;

            [JsonProperty("Configuración de Perfiles de NPC")]
            public Dictionary<string, NpcProfile> NpcProfiles { get; set; } = new Dictionary<string, NpcProfile>
            {
                { "normal", new NpcProfile { Name = "Soldado Excavadora", BaseHealth = 150f, Weapon = "rifle.lr300", Torso = "scientistsuit_heavy" }},
                { "elite", new NpcProfile { Name = "Defensor Asalto", BaseHealth = 120f, Weapon = "minigun", Torso = "hazmatsuit_scientist_nvgm" }},
                { "boss", new NpcProfile { Name = "Jefe de Zona", BaseHealth = 250f, Weapon = "lmg.m249", Torso = "hazmatsuit_scientist_nvgm" }}
            };
        } 

        protected override void LoadDefaultConfig()
        {
            config = new PluginConfig
            {
                Difficulties = new Dictionary<int, DifficultySettings>
                {
                    { 0, new DifficultySettings { Name = "easy", Enabled = true, Resource = 4, MinDiesel = 5, MaxDiesel = 10, HealthMultiplier = 1.0f, RangeMultiplier = 1.0f, AimDamageMultiplier = 1.0f, skinTurret = 3732045248, MarkerColor = "#3cd834" } },
                    { 1, new DifficultySettings { Name = "normal", Enabled = true, Resource = 4, MinDiesel = 10, MaxDiesel = 15, HealthMultiplier = 1.2f, RangeMultiplier = 1.2f, AimDamageMultiplier = 1.2f, skinTurret = 3732045248, MarkerColor = "#e57109" } },
                    { 2, new DifficultySettings { Name = "medium", Enabled = true, Resource = 4, MinDiesel = 15, MaxDiesel = 20, HealthMultiplier = 1.5f, RangeMultiplier = 1.5f, AimDamageMultiplier = 1.5f, skinTurret = 3732045248, MarkerColor = "#e7c50a" } },
                    { 3, new DifficultySettings { Name = "hard", Enabled = true, Resource = 4, MinDiesel = 20, MaxDiesel = 25, HealthMultiplier = 2.0f, RangeMultiplier = 2.0f, AimDamageMultiplier = 2.0f, skinTurret = 3732045248, MarkerColor = "#9c1825" } },
                    { 4, new DifficultySettings { Name = "expert", Enabled = true, Resource = 4, MinDiesel = 25, MaxDiesel = 30, HealthMultiplier = 2.5f, RangeMultiplier = 2.5f, AimDamageMultiplier = 2.5f, skinTurret = 3732045248, MarkerColor = "#0000FF" } },
                    { 5, new DifficultySettings { Name = "nightmare", Enabled = true, Resource = 4, MinDiesel = 30, MaxDiesel = 35, HealthMultiplier = 3.5f, RangeMultiplier = 3.5f, AimDamageMultiplier = 3.5f, skinTurret = 3732445397, MarkerColor = "#9236f5" } }
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
                ["EventAlreadyActive"] = "<color=#ff4d4d>[EXCAVATOR]</color> <color=#ffffff>The event is already active.</color>",
                ["ErrorComponents"] = "<color=#ff4d4d>[ERROR]</color> <color=#ffffff>Excavator components not found.</color>",
                ["EventWarning"] = "<color=#ff4d4d>[EXCAVATOR]</color> <color=#ffffff>The Giant Excavator will be taken by </color><color=#ff4d4d>hostile forces</color><color=#ffffff>, stay away!</color>",
                ["EventAborted"] = "<color=#ff4d4d>[ABORTED]</color> <color=#ffffff>The Excavator is already active and being used by a player.</color>",
                ["MiningStart"] = "<color=#4dff4d>[MINING]</color> <color=#ffffff>Mining </color><color=#ffeb3b>{0}</color> <color=#ffffff>| Diesel: </color><color=#ffeb3b>{1}</color><color=#ffffff>!</color>",
                ["EventStarted"] = "<color=#ff4d4d>[EXCAVATOR]</color> <color=#ffffff>The Giant Excavator has been activated by </color><color=#ff4d4d>hostile forces</color><color=#ffffff>!</color>",
                ["EventDifficulty"] = "<color=#ff4d4d>[EXCAVATOR]</color> <color=#ffffff>Event Difficulty: </color><color={0}>{1}</color>",
                ["EventNoFuel"] = "<color=#ffeb3b>[EXCAVATOR]</color> <color=#ffffff>The Excavator ran out of fuel! Grab the </color><color=#4dff4d>loot</color><color=#ffffff>.</color>",
                ["EventCooldown"] = "<color=#f97316>[EXCAVATOR]</color> <color=#ffffff>You have </color><color=#ffeb3b>10 minutes</color><color=#ffffff> to collect the hoppers!</color>",
                ["EventEnded"] = "<color=#ff4d4d>[EXCAVATOR]</color> <color=#ffffff>Collection time is over. Cleaning up...</color>",
                ["EventCancelled"] = "<color=#ff4d4d>[EXCAVATOR]</color> <color=#ffffff>Event manually cancelled.</color>",
                ["CannotExtract"] = "<color=#ff4d4d>[EXCAVATOR]</color> <color=#ffffff>You cannot extract items until event finishes.</color>",
                ["CannotInsert"] = "<color=#ff4d4d>[EXCAVATOR]</color> <color=#ffffff>You cannot store items here.</color>",
                ["NoActiveEvent"] = "<color=#ff4d4d>[EXCAVATOR]</color> <color=#ffffff>No active event.</color>",
                ["CmdUsage"] = "<color=#ffeb3b>[HELP]</color> <color=#ffffff>Usage: </color><color=#4dff4d>/excavator start [ID]</color><color=#ffffff> or </color><color=#ff4d4d>/excavator stop</color>",
                ["NextEventTime"] = "<color=#ffeb3b>[INFO]</color> <color=#ffffff>Next event in: </color><color=#4dff4d>{0}</color>",
                ["LootingHopper"] = "<color=#4dff4d>[EXCAVATOR]</color> <color=#ffffff>Opening hopper...</color>"
            }, this, "en");

			Dictionary<string, string> Es = new Dictionary<string, string>
            {
                ["EventAlreadyActive"] = "<color=#ff4d4d>[EXCAVADORA]</color> <color=#ffffff>El evento ya está activo.</color>",
                ["ErrorComponents"] = "<color=#ff4d4d>[ERROR]</color> <color=#ffffff>No se encontraron los componentes de la Excavadora.</color>",
                ["EventWarning"] = "<color=#ff4d4d>[EXCAVADORA]</color> <color=#ffffff>¡La Excavadora será tomada por </color><color=#ff4d4d>fuerzas hostiles</color><color=#ffffff>, aléjate!</color>",
                ["EventAborted"] = "<color=#ff4d4d>[ABORTADO]</color> <color=#ffffff>La Excavadora ya está activa y siendo utilizada por un jugador.</color>",
                ["MiningStart"] = "<color=#4dff4d>[MINERÍA]</color> <color=#ffffff>¡Minando </color><color=#ffeb3b>{0}</color> <color=#ffffff>| Diesel: </color><color=#ffeb3b>{1}</color><color=#ffffff>!</color>",
                ["EventStarted"] = "<color=#ff4d4d>[EXCAVADORA]</color> <color=#ffffff>¡La Excavadora ha sido activada por </color><color=#ff4d4d>fuerzas hostiles</color><color=#ffffff>!</color>",
                ["EventDifficulty"] = "<color=#ff4d4d>[EXCAVADORA]</color> <color=#ffffff>Dificultad del evento: </color><color={0}>{1}</color>",
                ["EventNoFuel"] = "<color=#ffeb3b>[EXCAVADORA]</color> <color=#ffffff>¡Se acabó el combustible! Recoge el </color><color=#4dff4d>botín</color><color=#ffffff>.</color>",
                ["EventCooldown"] = "<color=#f97316>[EXCAVADORA]</color> <color=#ffffff>¡Tienen </color><color=#ffeb3b>10 minutos</color><color=#ffffff> para recolectar las tolvas!</color>",
                ["EventEnded"] = "<color=#ff4d4d>[EXCAVADORA]</color> <color=#ffffff>Tiempo de recolección terminado. Limpiando zona...</color>",
                ["EventCancelled"] = "<color=#ff4d4d>[EXCAVADORA]</color> <color=#ffffff>Evento cancelado manualmente.</color>",
                ["CannotExtract"] = "<color=#ff4d4d>[EXCAVADORA]</color> <color=#ffffff>No puedes extraer objetos hasta que el evento finalice.</color>",
                ["CannotInsert"] = "<color=#ff4d4d>[EXCAVADORA]</color> <color=#ffffff>¡No puedes guardar objetos aquí!</color>",
                ["NoActiveEvent"] = "<color=#ff4d4d>[EXCAVADORA]</color> <color=#ffffff>No hay evento activo.</color>",
                ["CmdUsage"] = "<color=#ffeb3b>[AYUDA]</color> <color=#ffffff>Uso: </color><color=#4dff4d>/excavator start [ID]</color><color=#ffffff> o </color><color=#ff4d4d>/excavator stop</color>",
                ["NextEventTime"] = "<color=#ffeb3b>[INFO]</color> <color=#ffffff>Próximo evento en: </color><color=#4dff4d>{0}</color>",
                ["LootingHopper"] = "<color=#4dff4d>[EXCAVADORA]</color> <color=#ffffff>Abriendo tolva...</color>"
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
			foreach (var player in BasePlayer.activePlayerList)
			{
				if (player != null && player.IsConnected) SendMsg(player, key, args);
			}
		}
		private static bool reflectionInitialized = false;

		private void InitializeReflection()
		{
			if (reflectionInitialized) return;

			Type armType = Type.GetType("ExcavatorArm, Assembly-CSharp");
			if (armType != null)
			{
				miningIndexField = armType.GetField("resourceMiningIndex", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
				beginMiningMethod = armType.GetMethod("BeginMining", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				stopMiningMethod = armType.GetMethod("StopMining", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			}

			Type engineType = typeof(DieselEngine);
			engineOnMethod = engineType.GetMethod("EngineOn", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			engineSetIsOnMethod = engineType.GetMethod("SetIsOn", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			engineMarkDirtyMethod = engineType.GetMethod("MarkDirty", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			reflectionInitialized = true; 
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
		private void CacheStaticExcavatorEntities()
		{
			InitializeReflection();
			LoadImageLocally();
			cachedOutputPiles.Clear();
			cachedOutputPileIds.Clear();
			cachedSignalComputer = null;
			cachedEngine = null;
			cachedArm = null;

			cachedExcavatorMonument = TerrainMeta.Path?.Monuments?.FirstOrDefault(m => m.name.ToLower().Contains("excavator"));
			if (cachedExcavatorMonument == null)
			{
				Puts("[ERROR] Monumento Excavator no encontrado.");
				rust.RunServerCommand("oxide.unload MiGiantExcavatorEvent");
				return;
			}

			List<BaseEntity> nearbyEntities = new List<BaseEntity>();
			Vis.Entities(cachedExcavatorMonument.transform.position, 100f, nearbyEntities);

			foreach (var entity in nearbyEntities)
			{
				if (entity == null) continue;

				if (cachedArm == null && entity.GetComponent("ExcavatorArm") != null)
				{
					cachedArm = entity.GetComponent("ExcavatorArm") as MonoBehaviour;
				}

				if (cachedSignalComputer == null && entity.GetComponent("ExcavatorSignalComputer") != null)
				{
					cachedSignalComputer = entity;
					cachedSignalComputerComponent = entity.GetComponent("ExcavatorSignalComputer");
					
					if (requestSuppliesMethod == null)
						requestSuppliesMethod = cachedSignalComputerComponent.GetType().GetMethod("RequestSupplies", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				}

				if (entity is DieselEngine engine && engine.PrefabName != null && engine.PrefabName.Contains("excavator"))
				{
					cachedEngine = engine;
				}

				if (entity is StorageContainer container && entity.ShortPrefabName == "excavator_output_pile")
				{
					cachedOutputPiles.Add(container);
					cachedOutputPileIds.Add(container.net.ID.Value);
				}
			}

			if (cachedArm == null) Puts("¡Alerta! ExcavatorArm no detectada en la zona.");
			if (cachedSignalComputer == null) Puts("¡Alerta! SignalComputer no detectada en la zona.");
			if (cachedOutputPiles.Count < 2) Puts($"¡Alerta! Solo se encontraron {cachedOutputPiles.Count/2} tolvas.");
		}

		private void OnServerInitialized()
		{
			CacheStaticExcavatorEntities();
			ScheduleAutoStart();

            iconNpcId = ItemManager.FindItemDefinition("skull.human")?.itemid ?? 0;
            iconTurretId = ItemManager.FindItemDefinition("autoturret")?.itemid ?? 0;
            iconCrateId = ItemManager.FindItemDefinition("box.wooden")?.itemid ?? 0;
		}

		private float nextEventTimestamp = 0f;
        private void ScheduleAutoStart()
        {
            if (!config.EnableAutoStart || config.AutoStartIntervalHoursMax <= 0) return;

            float randomHours = UnityEngine.Random.Range(config.AutoStartIntervalHoursMin, config.AutoStartIntervalHoursMax);
            float intervalSeconds = randomHours * 3600f;
			nextEventTimestamp = UnityEngine.Time.realtimeSinceStartup + intervalSeconds;
            Puts($"Próximo evento automático de Excavadora en {randomHours:F2} horas.");

            autoStartTimer = CreateTimer(intervalSeconds, () =>
            {
                StartExcavatorEvent(null, 4, -1, -1);
                ScheduleAutoStart();
            });
        }

		private float timeLeft = 600f; 
		private bool isExtended = false; 
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
			(redCircleMarker as MapMarkerGenericRadius)?.SendUpdate();
		}

		private void CreateEventMapMarkers(Vector3 position)
		{
			redCircleMarker = GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab", position);
			if (redCircleMarker is MapMarkerGenericRadius radiusMarker)
			{
				radiusMarker.Spawn();
				radiusMarker.radius = 2.0f;       
				radiusMarker.alpha = 0.35f;       
				ColorUtility.TryParseHtmlString(config.Difficulties[config.CurrentDifficulty].MarkerColor, out Color markerColor);
				radiusMarker.color1 = markerColor;  
				radiusMarker.color2 = markerColor;  
				radiusMarker.SendUpdate();
			}

			shopMapMarker = GameManager.server.CreateEntity("assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab", position);
			if (shopMapMarker is VendingMachineMapMarker vendingMarker)
			{
				vendingMarker.markerShopName = $"★ EVENTO EXCAVADORA ★ {config.Difficulties[config.CurrentDifficulty].Name.ToUpper()} ★";
				vendingMarker.SetFlag(BaseEntity.Flags.On, true);
				shopMapMarker.Spawn();
				vendingMarker.SendNetworkUpdate(); 
			}
		}

        private Timer CreateTimer(float delay, Action action) { Timer t = timer.Once(delay, action); activeTimers.Add(t); return t; }
        private Timer CreateTimerRepeat(float delay, Action action) { Timer t = timer.Every(delay, action); activeTimers.Add(t); return t; }
        private void KillAllTimers() { foreach (var t in activeTimers) { if (t != null) t.Destroy(); } activeTimers.Clear(); eventTimer = autoStartTimer = delayTimer = monitoringTimer = cooldownTimer = markerUpdateTimer = clockTimer = playerCheckTimer = null; }

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

        private void Unload()
        {
            CleanUpEvent(true); 
            KillAllTimers();
            if (spawnCoroutine != null) { ServerMgr.Instance.StopCoroutine(spawnCoroutine); spawnCoroutine = null; }
            foreach (var player in BasePlayer.activePlayerList) {
                CuiHelper.DestroyUi(player, "ExcavatorEventUI");
                CuiHelper.DestroyUi(player, "ExcavatorEventUI_Dynamic");
                CuiHelper.DestroyUi(player, "ExcavatorEventUI_Clock");
                CuiHelper.DestroyUi(player, "ExcavatorAdminUI");
            }
        }

        private void OnEntityKill(BaseNetworkable netEntity)
        {
            if (!isEventActive || netEntity == null) return;
            if (spawnedEntityIds.Contains(netEntity.net.ID.Value)) {
                if (netEntity is NPCPlayer || netEntity is BaseNpc) npcsVivos--;
                else if (netEntity is AutoTurret) torretasVivas--;
                else if (netEntity is HackableLockedCrate || netEntity is LootContainer) cajasVivas--;
                UpdateEventCUIForce();
            }
        }

        private void ProcessPlayerCUI()
        {
            if (!isEventActive || cachedExcavatorMonument == null || !config.EnableMenuInfo) return;
            Vector3 monumentPos = cachedExcavatorMonument.transform.position;
            float radiusSqr = EventZoneRadius * EventZoneRadius;
            string borderCuiColor = HexToCuiColor(config.Difficulties[config.CurrentDifficulty].MarkerColor, 0.85f);
            
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
                        DrawClockCUI(player, isLooteable ? "00:00" : "MINANDO"); 
                    }
                }
                else if (hasUi) {
                    playersInEventZone.Remove(player.userID);
                    CuiHelper.DestroyUi(player, "ExcavatorEventUI"); CuiHelper.DestroyUi(player, "ExcavatorEventUI_Dynamic"); CuiHelper.DestroyUi(player, "ExcavatorEventUI_Clock");
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
			timeLeft = timeLeft-1f;
			float remainingSeconds = Mathf.Max(timeLeft, 0f);
 
			TimeSpan timeSpan = TimeSpan.FromSeconds(remainingSeconds);
			string timeString = timeSpan.TotalHours >= 1 
				? string.Format("{0:00}:{1:00}:{2:00}", (int)timeSpan.TotalHours, timeSpan.Minutes, timeSpan.Seconds) 
				: string.Format("{0:00}:{1:00}", timeSpan.Minutes, timeSpan.Seconds);
			if(!isLooteable)
				timeString = "✦"+timeString; 
			foreach (ulong userId in playersInEventZone) 
			{ 
				BasePlayer p = BasePlayer.FindByID(userId); 
				if (p != null) DrawClockCUI(p, timeString); 
			}
		}
		
        private void DrawEventUI_Static(BasePlayer player, string colorBorde)
        {
            CuiHelper.DestroyUi(player, "ExcavatorEventUI"); var container = new CuiElementContainer();
            container.Add(new CuiPanel { Image = { Color = "0 0 0 0" }, RectTransform = { AnchorMin = "0.36 0.90", AnchorMax = "0.64 0.93" } }, "Hud", "ExcavatorEventUI");
            container.Add(new CuiPanel { Image = { Color = colorBorde }, RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" } }, "ExcavatorEventUI", "ExcavatorEventUI_ColorBg"); 
            container.Add(new CuiElement { Name = "ExcavatorEventUI_ImageBg", Parent = "ExcavatorEventUI_ColorBg", Components = { new CuiRawImageComponent { Png = imageId, Color = "1 1 1 0.8" }, new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" } } });
            container.Add(new CuiElement { Parent = "ExcavatorEventUI", Components = { new CuiImageComponent { ItemId = iconNpcId }, new CuiRectTransformComponent { AnchorMin = "0.28 0.15", AnchorMax = "0.36 0.85" } } });
            container.Add(new CuiElement { Parent = "ExcavatorEventUI", Components = { new CuiImageComponent { ItemId = iconTurretId }, new CuiRectTransformComponent { AnchorMin = "0.53 0.15", AnchorMax = "0.61 0.85" } } });
            container.Add(new CuiElement { Parent = "ExcavatorEventUI", Components = { new CuiImageComponent { ItemId = iconCrateId }, new CuiRectTransformComponent { AnchorMin = "0.78 0.15", AnchorMax = "0.86 0.85" } } });
            CuiHelper.AddUi(player, container);
        } 

        private void DrawEventUI_Dynamic(BasePlayer player, int npcs, int torretas, int cajas)
        {
            CuiHelper.DestroyUi(player, "ExcavatorEventUI_Dynamic"); var container = new CuiElementContainer();
            container.Add(new CuiPanel { Image = { Color = "0 0 0 0" }, RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" } }, "ExcavatorEventUI", "ExcavatorEventUI_Dynamic");
            container.Add(new CuiElement { Parent = "ExcavatorEventUI_Dynamic", Components = { new CuiTextComponent { Text = $"<color=#ff4d4d>{npcs}</color>", FontSize = 16, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-bold.ttf" }, new CuiOutlineComponent { Distance = "1 -1", Color = "0 0 0 1" }, new CuiRectTransformComponent { AnchorMin = "0.38 0", AnchorMax = "0.50 1" } } });
            container.Add(new CuiElement { Parent = "ExcavatorEventUI_Dynamic", Components = { new CuiTextComponent { Text = $"<color=#ffeb3b>{torretas}</color>", FontSize = 16, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-bold.ttf" }, new CuiOutlineComponent { Distance = "1 -1", Color = "0 0 0 1" }, new CuiRectTransformComponent { AnchorMin = "0.63 0", AnchorMax = "0.75 1" } } });
            container.Add(new CuiElement { Parent = "ExcavatorEventUI_Dynamic", Components = { new CuiTextComponent { Text = $"<color=#4dff4d>{cajas}</color>", FontSize = 16, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-bold.ttf" }, new CuiOutlineComponent { Distance = "1 -1", Color = "0 0 0 1" }, new CuiRectTransformComponent { AnchorMin = "0.88 0", AnchorMax = "1 1" } } });
            CuiHelper.AddUi(player, container);
        }

        private void DrawClockCUI(BasePlayer player, string timeString = "MINANDO")
        {
            CuiHelper.DestroyUi(player, "ExcavatorEventUI_Clock"); var container = new CuiElementContainer();
            container.Add(new CuiPanel { Image = { Color = "0 0 0 0" }, RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" } }, "ExcavatorEventUI", "ExcavatorEventUI_Clock");
            container.Add(new CuiElement { Parent = "ExcavatorEventUI_Clock", Components = { new CuiTextComponent { Text = $"<color=#ffffff>⏳{timeString}</color>", FontSize = 13, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" }, new CuiOutlineComponent { Distance = "1 -1", Color = "0 0 0 1" }, new CuiRectTransformComponent { AnchorMin = "0.02 0", AnchorMax = "0.26 1" } } });
            CuiHelper.AddUi(player, container);
        }

        private string HexToCuiColor(string hex, float alpha = 1.0f) {
            if (string.IsNullOrEmpty(hex)) return $"1 1 1 {alpha}"; hex = hex.Replace("#", "");
            if (hex.Length == 6) { try { float r = Convert.ToInt32(hex.Substring(0, 2), 16) / 255f; float g = Convert.ToInt32(hex.Substring(2, 2), 16) / 255f; float b = Convert.ToInt32(hex.Substring(4, 2), 16) / 255f; return $"{r} {g} {b} {alpha}"; } catch { return $"1 1 1 {alpha}"; } }
            return $"1 1 1 {alpha}";
        }

        private string GetNextItem(List<string> itemsList, string currentItem) { int currentIdx = itemsList.IndexOf(currentItem); return itemsList[(currentIdx + 1) >= itemsList.Count ? 0 : currentIdx + 1]; }

        [ConsoleCommand("excavator.cui")]
        private void CmdCuiAdmin(ConsoleSystem.Arg arg) 
        {
            var player = arg.Player(); if (player == null || !player.IsAdmin) return;
            string action = arg.GetString(0);
            if (action == "close") { CuiHelper.DestroyUi(player, "ExcavatorAdminUI"); return; }
            int diffId = arg.GetInt(1, 0); DifficultySettings diffSettings = null;
            if (diffId >= 0 && config.Difficulties.ContainsKey(diffId)) diffSettings = config.Difficulties[diffId];
            bool isRefresh = action != "view"; 

            switch (action) {
                case "view": break; 
                case "toggle_enabled": if (diffSettings != null) diffSettings.Enabled = !diffSettings.Enabled; break;
                case "add_mindiesel": if (diffSettings != null) diffSettings.MinDiesel = Mathf.Max(0, diffSettings.MinDiesel + arg.GetInt(2)); break;
                case "add_maxdiesel": if (diffSettings != null) diffSettings.MaxDiesel = Mathf.Max(0, diffSettings.MaxDiesel + arg.GetInt(2)); break;
                case "add_health": if (diffSettings != null) diffSettings.HealthMultiplier = Mathf.Clamp(diffSettings.HealthMultiplier + arg.GetFloat(2), 0.1f, 10f); break;
                case "add_dmg": if (diffSettings != null) diffSettings.AimDamageMultiplier = Mathf.Clamp(diffSettings.AimDamageMultiplier + arg.GetFloat(2), 0.1f, 10f); break;
                case "toggle_autostart": config.EnableAutoStart = !config.EnableAutoStart; break;
                case "toggle_menuinfo": config.EnableMenuInfo = !config.EnableMenuInfo; destroyallcui(); break;
                case "set_turretskin": if (diffSettings != null) { ulong.TryParse(arg.GetString(2), out ulong newSkin); diffSettings.skinTurret = newSkin; } break;
                case "add_automin": config.AutoStartIntervalHoursMin = Mathf.Max(0f, config.AutoStartIntervalHoursMin + arg.GetFloat(2)); break;
                case "add_automax": config.AutoStartIntervalHoursMax = Mathf.Max(0f, config.AutoStartIntervalHoursMax + arg.GetFloat(2)); break;
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
                case "save": SaveConfig(); player.ChatMessage("<color=#4dff4d>[Excavator]</color> Configuración guardada correctamente."); break;
            }
            OpenAdminMenu(player, diffId, isRefresh); 
        }

        private void OpenAdminMenu(BasePlayer player, int diffId, bool isRefresh = false)
        {
            if (!isRefresh) {
                CuiHelper.DestroyUi(player, "ExcavatorAdminUI"); var baseContainer = new CuiElementContainer();
                baseContainer.Add(new CuiPanel { Image = { Color = "0.08 0.08 0.09 0.98" }, RectTransform = { AnchorMin = "0.15 0.15", AnchorMax = "0.85 0.85" }, CursorEnabled = true }, "Overlay", "ExcavatorAdminUI");
                baseContainer.Add(new CuiElement { Parent = "ExcavatorAdminUI", Components = { new CuiTextComponent { Text = "⚙ $$ EVENTO EXCAVADORA - CONFIGURACIÓN $$ ⚙", FontSize = 20, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", Color = "#e5a93c" }, new CuiOutlineComponent { Distance = "1 -1", Color = "0 0 0 0.8" }, new CuiRectTransformComponent { AnchorMin = "0 0.9", AnchorMax = "1 1" } } });
                baseContainer.Add(new CuiButton { Button = { Command = "excavator.cui close", Color = "0.70 0.22 0.22 1" }, RectTransform = { AnchorMin = "0.95 0.91", AnchorMax = "0.98 0.97" }, Text = { Text = "X", FontSize = 14, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" } }, "ExcavatorAdminUI");
                baseContainer.Add(new CuiButton { 
					Button = { Command = $"excavator.cui save {diffId}", Color = "0.18 0.58 0.32 1" }, 
					RectTransform = { AnchorMin = "0.80 0.91", AnchorMax = "0.94 0.97" },  
					Text = { Text = "GUARDAR", FontSize = 11, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" } 
				}, "ExcavatorAdminUI");
				baseContainer.Add(new CuiPanel { Image = { Color = "0.05 0.05 0.06 1" }, RectTransform = { AnchorMin = "0.02 0.05", AnchorMax = "0.25 0.88" } }, "ExcavatorAdminUI", "ExcavatorAdminUI_Left");
                
                float currentY = 0.82f; float btnHeight = 0.10f; string basicColor = "0.68 0.38 0.16 1"; bool basicSelected = (diffId == -1);
                baseContainer.Add(new CuiButton { Button = { Command = "excavator.cui view -1", Color = basicSelected ? basicColor : "0.13 0.13 0.15 1" }, RectTransform = { AnchorMin = $"0.1 {currentY}", AnchorMax = $"0.9 {currentY + 0.10f}" }, Text = { Text = "AJUSTES BÁSICOS", FontSize = 12, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", Color = basicSelected ? "1 1 1 1" : basicColor } }, "ExcavatorAdminUI_Left");
                currentY -= 0.13f;

                foreach (var kvp in config.Difficulties) {
                    int id = kvp.Key; string name = kvp.Value.Name.ToUpper(); string diffColorRgba = HexToCuiColor(kvp.Value.MarkerColor); bool isSelected = (id == diffId);
                    string finalBgColor = isSelected ? diffColorRgba : "0.13 0.13 0.15 1"; string finalTextColor = isSelected ? "1 1 1 1" : diffColorRgba; float yMin = currentY; float yMax = yMin + btnHeight;
                    baseContainer.Add(new CuiButton { Button = { Command = $"excavator.cui view {id}", Color = finalBgColor }, RectTransform = { AnchorMin = $"0.1 {yMin}", AnchorMax = $"0.9 {yMax}" }, Text = { Text = name, FontSize = 12, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", Color = finalTextColor } }, "ExcavatorAdminUI_Left"); currentY -= (btnHeight + 0.015f);
                }
                baseContainer.Add(new CuiPanel { Image = { Color = "0.06 0.06 0.07 1" }, RectTransform = { AnchorMin = "0.27 0.05", AnchorMax = "0.98 0.88" } }, "ExcavatorAdminUI", "ExcavatorAdminUI_Right");
                CuiHelper.AddUi(player, baseContainer);
            }

            string contentPanelName = "ExcavatorAdminUI_Content"; CuiHelper.DestroyUi(player, contentPanelName);
            var contentContainer = new CuiElementContainer(); contentContainer.Add(new CuiPanel { Image = { Color = "0 0 0 0" }, RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" } }, "ExcavatorAdminUI_Right", contentPanelName);

            int rowIndex = 0; float rowSpacing = 0.085f;
            Action<string, string, string, string, string> DrawToggleRow = (label, valText, valColor, cmdToggle, desc) => { float yMax = 0.96f - (rowIndex * rowSpacing); float yMin = yMax - 0.07f; contentContainer.Add(new CuiElement { Parent = contentPanelName, Components = { new CuiTextComponent { Text = label, FontSize = 13, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-bold.ttf" }, new CuiOutlineComponent { Distance = "1 -1", Color = "0 0 0 0.6" }, new CuiRectTransformComponent { AnchorMin = $"0.04 {yMin}", AnchorMax = $"0.3 {yMax}" } } }); contentContainer.Add(new CuiButton { Button = { Command = cmdToggle, Color = valColor }, RectTransform = { AnchorMin = $"0.3 {yMin}", AnchorMax = $"0.45 {yMax}" }, Text = { Text = valText, FontSize = 11, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" } }, contentPanelName); contentContainer.Add(new CuiElement { Parent = contentPanelName, Components = { new CuiTextComponent { Text = desc, FontSize = 11, Color = "0.65 0.65 0.65 1", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf" }, new CuiOutlineComponent { Distance = "1 -1", Color = "0 0 0 0.4" }, new CuiRectTransformComponent { AnchorMin = $"0.48 {yMin}", AnchorMax = $"0.95 {yMax}" } } }); rowIndex++; };
            Action<string, string, string, string> DrawNumberRow = (label, valText, cmdMinus, cmdPlus) => { float yMax = 0.96f - (rowIndex * rowSpacing); float yMin = yMax - 0.07f; contentContainer.Add(new CuiElement { Parent = contentPanelName, Components = { new CuiTextComponent { Text = label, FontSize = 13, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-bold.ttf" }, new CuiOutlineComponent { Distance = "1 -1", Color = "0 0 0 0.6" }, new CuiRectTransformComponent { AnchorMin = $"0.04 {yMin}", AnchorMax = $"0.3 {yMax}" } } }); contentContainer.Add(new CuiButton { Button = { Command = cmdMinus, Color = "0.65 0.25 0.25 1" }, RectTransform = { AnchorMin = $"0.3 {yMin}", AnchorMax = $"0.35 {yMax}" }, Text = { Text = "-", FontSize = 14, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" } }, contentPanelName); string valBg = "valBg_" + rowIndex; contentContainer.Add(new CuiPanel { Image = { Color = "0.11 0.11 0.13 1" }, RectTransform = { AnchorMin = $"0.36 {yMin}", AnchorMax = $"0.44 {yMax}" } }, contentPanelName, valBg); contentContainer.Add(new CuiElement { Parent = valBg, Components = { new CuiTextComponent { Text = valText, FontSize = 12, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", Color = "#ffeb3b" }, new CuiOutlineComponent { Distance = "1 -1", Color = "0 0 0 0.5" }, new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" } } }); contentContainer.Add(new CuiButton { Button = { Command = cmdPlus, Color = "0.25 0.55 0.25 1" }, RectTransform = { AnchorMin = $"0.45 {yMin}", AnchorMax = $"0.5 {yMax}" }, Text = { Text = "+", FontSize = 14, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" } }, contentPanelName); rowIndex++; };

            if (diffId == -1) {
                DrawToggleRow("Temporizador Autostart:", config.EnableAutoStart ? "ACTIVADO" : "DESACTIVADO", config.EnableAutoStart ? "0.20 0.55 0.30 1" : "0.65 0.25 0.25 1", $"excavator.cui toggle_autostart {diffId}", "Habilita que el evento se lance solo periódicamente."); DrawNumberRow("Autostart Mínimo (Horas):", config.AutoStartIntervalHoursMin.ToString("0.0"), $"excavator.cui add_automin {diffId} -0.5", $"excavator.cui add_automin {diffId} 0.5"); DrawNumberRow("Autostart Máximo (Horas):", config.AutoStartIntervalHoursMax.ToString("0.0"), $"excavator.cui add_automax {diffId} -0.5", $"excavator.cui add_automax {diffId} 0.5"); 
                DrawToggleRow("Menu info Evento:", config.EnableMenuInfo ? "ACTIVADO" : "DESACTIVADO", config.EnableMenuInfo ? "0.20 0.55 0.30 1" : "0.65 0.25 0.25 1", $"excavator.cui toggle_menuinfo {diffId}", "Habilita la información de CUI en pantalla."); 
                float npcStartY = 0.96f - (rowIndex * rowSpacing) - 0.01f; contentContainer.Add(new CuiElement { Parent = contentPanelName, Components = { new CuiTextComponent { Text = "⚔ PERSONALIZACIÓN DE NPC ⚔", FontSize = 11, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", Color = "#6aa1d4" }, new CuiOutlineComponent { Distance = "1 -1", Color = "0 0 0 0.8" }, new CuiRectTransformComponent { AnchorMin = $"0.04 {npcStartY - 0.04f}", AnchorMax = $"0.96 {npcStartY}" } } }); npcStartY -= 0.045f;
                if (!_playerActiveNpcTab.TryGetValue(player.userID, out string selectedKey)) { selectedKey = "normal"; _playerActiveNpcTab[player.userID] = selectedKey; }
                if (!config.NpcProfiles.TryGetValue(selectedKey, out NpcProfile npc)) npc = new NpcProfile { Name = "Soldado", BaseHealth = 150f, Weapon = "rifle.lr300" };

                float tabYMax = npcStartY; float tabYMin = tabYMax - 0.045f; string[] profileKeys = { "normal", "elite", "boss" }; string[] profileLabels = { "⚙ SOLDADO NORMAL", "☠ DEFENSOR ÉLITE", "👑 JEFE EXCAVADORA" }; float tabWidth = 0.18f; float tabStartO = 0.40f;
                for (int t = 0; t < profileKeys.Length; t++) { string pKey = profileKeys[t]; string pLabel = profileLabels[t]; string tabColor = (pKey == selectedKey) ? "0.18 0.44 0.68 1" : "0.13 0.13 0.15 1"; contentContainer.Add(new CuiButton { Button = { Command = $"excavator.cui npc_tab {diffId} {pKey}", Color = tabColor }, RectTransform = { AnchorMin = $"{tabStartO + (t * (tabWidth + 0.01f))} {tabYMin}", AnchorMax = $"{tabStartO + tabWidth + (t * (tabWidth + 0.01f))} {tabYMax}" }, Text = { Text = pLabel, FontSize = 9, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" } }, contentPanelName); }
                contentContainer.Add(new CuiElement { Parent = contentPanelName, Components = { new CuiTextComponent { Text = "Vida Base:", FontSize = 11, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-bold.ttf", Color = "0.85 0.85 0.85 1" }, new CuiRectTransformComponent { AnchorMin = $"0.04 {tabYMin}", AnchorMax = $"0.15 {tabYMax}" } } }); contentContainer.Add(new CuiButton { Button = { Command = $"excavator.cui npc_health {diffId} {selectedKey} -25", Color = "0.55 0.2 0.2 1" }, RectTransform = { AnchorMin = $"0.15 {tabYMin}", AnchorMax = $"0.18 {tabYMax}" }, Text = { Text = "-", FontSize = 12 } }, contentPanelName); contentContainer.Add(new CuiPanel { Image = { Color = "0.15 0.15 0.17 1" }, RectTransform = { AnchorMin = $"0.185 {tabYMin}", AnchorMax = $"0.295 {tabYMax}" } }, contentPanelName, "hp_container"); contentContainer.Add(new CuiElement { Parent = "hp_container", Components = { new CuiTextComponent { Text = npc.BaseHealth.ToString("0"), FontSize = 11, Align = TextAnchor.MiddleCenter, Color = "#ffeb3b", Font = "robotocondensed-bold.ttf" } } }); contentContainer.Add(new CuiButton { Button = { Command = $"excavator.cui npc_health {diffId} {selectedKey} 25", Color = "0.2 0.5 0.2 1" }, RectTransform = { AnchorMin = $"0.30 {tabYMin}", AnchorMax = $"0.33 {tabYMax}" }, Text = { Text = "+", FontSize = 12 } }, contentPanelName);

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
                    contentContainer.Add(new CuiButton { Button = { Command = $"excavator.cui npc_cycle_item {diffId} {selectedKey} {slot}", Color = "0.22 0.35 0.55 1" }, RectTransform = { AnchorMin = $"0.27 {yMin}", AnchorMax = $"0.57 {yMax}" }, Text = { Text = itemShortname, FontSize = 10, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" } }, contentPanelName);
                    string sBox = $"sb_{selectedKey}_{slot}"; contentContainer.Add(new CuiPanel { Image = { Color = "0.15 0.15 0.17 1" }, RectTransform = { AnchorMin = $"0.60 {yMin}", AnchorMax = $"0.85 {yMax}" } }, contentPanelName, sBox);
                    contentContainer.Add(new CuiElement { Parent = sBox, Components = { new CuiInputFieldComponent { Text = itemSkin.ToString(), FontSize = 11, Align = TextAnchor.MiddleCenter, Color = "#ffeb3b", Command = $"excavator.cui npc_set_skin {diffId} {selectedKey} {slot} " }, new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" } } });
                    contentContainer.Add(new CuiElement { Parent = contentPanelName, Components = { new CuiTextComponent { Text = "INTRO ↵", FontSize = 9, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", Color = "0.4 0.8 0.4 1" }, new CuiRectTransformComponent { AnchorMin = $"0.86 {yMin}", AnchorMax = $"0.95 {yMax}" } } });
                }
            }
            else if (config.Difficulties.ContainsKey(diffId)) {
				var diff = config.Difficulties[diffId];
				DrawToggleRow("Estado de Dificultad:", diff.Enabled ? "ACTIVADO" : "DESACTIVADO", diff.Enabled ? "0.20 0.55 0.30 1" : "0.65 0.25 0.25 1", $"excavator.cui toggle_enabled {diffId}", "Si está apagado, este nivel de dificultad no saldrá."); 
				DrawNumberRow("Diésel Mínimo (Loot):", diff.MinDiesel.ToString(), $"excavator.cui add_mindiesel {diffId} -1", $"excavator.cui add_mindiesel {diffId} 1"); 
				DrawNumberRow("Diésel Máximo (Loot):", diff.MaxDiesel.ToString(), $"excavator.cui add_maxdiesel {diffId} -1", $"excavator.cui add_maxdiesel {diffId} 1"); 
				DrawNumberRow("Multiplicador Vida (NPC):", diff.HealthMultiplier.ToString("0.0"), $"excavator.cui add_health {diffId} -0.1", $"excavator.cui add_health {diffId} 0.1"); 
				DrawNumberRow("Multiplicador Daño (NPC):", diff.AimDamageMultiplier.ToString("0.0"), $"excavator.cui add_dmg {diffId} -0.1", $"excavator.cui add_dmg {diffId} 0.1"); 

				float yMaxTurret = 0.96f - (rowIndex * rowSpacing); float yMinTurret = yMaxTurret - 0.07f;
				contentContainer.Add(new CuiElement { Parent = contentPanelName, Components = { new CuiTextComponent { Text = "Skin Torretas:", FontSize = 13, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-bold.ttf" }, new CuiOutlineComponent { Distance = "1 -1", Color = "0 0 0 0.6" }, new CuiRectTransformComponent { AnchorMin = $"0.04 {yMinTurret}", AnchorMax = $"0.3 {yMaxTurret}" } } });
				string turretBox = "turret_skin_box";
				contentContainer.Add(new CuiPanel { Image = { Color = "0.15 0.15 0.17 1" }, RectTransform = { AnchorMin = $"0.3 {yMinTurret}", AnchorMax = $"0.55 {yMaxTurret}" } }, contentPanelName, turretBox);
				contentContainer.Add(new CuiElement { Parent = turretBox, Components = { new CuiInputFieldComponent { Text = diff.skinTurret.ToString(), FontSize = 11, Align = TextAnchor.MiddleCenter, Color = "#ffeb3b", Command = $"excavator.cui set_turretskin {diffId} " }, new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" } } });
				contentContainer.Add(new CuiElement { Parent = contentPanelName, Components = { new CuiTextComponent { Text = "ENTER ↵", FontSize = 9, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-bold.ttf", Color = "0.4 0.8 0.4 1" }, new CuiRectTransformComponent { AnchorMin = $"0.57 {yMinTurret}", AnchorMax = $"0.95 {yMaxTurret}" } } });
			}
            CuiHelper.AddUi(player, contentContainer);
        }

		[ChatCommand("excavator")]
		private void CmdExcavator(BasePlayer player, string command, string[] args)
		{
			if (player == null || !player.IsAdmin) return;

			if (args.Length == 0)
			{
				SendMsg(player, "CmdUsage");
				return;
			}

			string action = args[0].ToLower();

			if (action == "stop")
			{
				CleanUpEvent(true);
				return;
			} 
            else if (action == "start")
			{
				int material = -2; 
				int barriles = -2; 
				int dificultad = -2; 

				if (args.Length > 1 && int.TryParse(args[1], out int parsedRoute)) material = (parsedRoute == 4 || parsedRoute < 0 || parsedRoute > 3) ? UnityEngine.Random.Range(0, 4) : parsedRoute;
				if (args.Length > 2 && int.TryParse(args[2], out int parsedBarriles)) barriles = parsedBarriles;
				if (args.Length > 3 && int.TryParse(args[3], out int parsedDificultad)) dificultad = parsedDificultad;

				bool Ok = true;
				if(material < 0) { player.ChatMessage("Numero de Material incorrecto 0 = Hq, 1 Azufre, 2 Metal, 3 Piedra."); Ok = false; }
				if(barriles < -1 || barriles > 59) { Ok = false; player.ChatMessage("Numero de Barriles incorrecto."); }
				if(dificultad > 6 || dificultad < -1) { Ok = false; player.ChatMessage("La dificultad va de 0 a 6."); }
				
				if(Ok) StartExcavatorEvent(player, material, barriles, dificultad);
				return;
			} 
            else if (action == "info")
			{
				if (isEventActive) { SendMsg(player, "EventAlreadyActive"); return; }
				if (autoStartTimer != null && config.EnableAutoStart)
				{
					double remainingSeconds = Math.Max(nextEventTimestamp - UnityEngine.Time.realtimeSinceStartup, 0);
					TimeSpan time = TimeSpan.FromSeconds(remainingSeconds);
					string timeFormatted = time.TotalHours >= 1 ? $"{(int)time.TotalHours}h {time.Minutes}min" : $"{time.Minutes}min {time.Seconds}s";
					SendMsg(player, "NextEventTime", timeFormatted);
				}
				else SendMsg(player, "NoActiveEvent");
			}
            else if (action == "config" || action == "menu") OpenAdminMenu(player, -1);
		}
        private bool hasCrateBeenHacked = false;
        private string[] resourceTypes = { "HQ", "Azufre", "Piedra", "Fragmentos de Metal" };
        private bool isShuttingDown = false;
        private void StartExcavatorEvent(BasePlayer triggerPlayer, int newIndex, int Barriles, int Dificultad)
        {
			if (isShuttingDown) return;
            if (isEventActive)
            {
                if (triggerPlayer != null) SendMsg(triggerPlayer, "EventAlreadyActive");
                return;
            }

            isEventActive = true;
            Puts("Iniciando Evento en la Excavadora Gigante...");
            
            if (cachedEngine == null || cachedArm == null)
            {
                if (triggerPlayer != null) SendMsg(triggerPlayer, "ErrorComponents");
                isEventActive = false;
                return;
            }

            BroadcastMsg("EventWarning");
            
            delayTimer = CreateTimer(1f, () => 
            {
                if (cachedEngine.IsOn())
                {
                    if (triggerPlayer != null) SendMsg(triggerPlayer, "EventAborted");
                    else Puts("El evento automático no se inició porque un jugador ya activó la Excavadora.");
                    isEventActive = false;
                    return;
                }

                if (config?.Difficulties == null) return;

				var availableDifficulties = config.Difficulties
					.Where(kvp => kvp.Value != null && kvp.Value.Enabled)
					.Select(kvp => kvp.Key)
					.ToList(); 
				
                if (availableDifficulties.Count == 0)
                {
                    availableDifficulties = config.Difficulties.Keys.ToList();
                }

				if (Dificultad < 0 || Dificultad > 5) Dificultad = -1;
				else config.CurrentDifficulty = Dificultad;
				
                if (Dificultad == -1) config.CurrentDifficulty = availableDifficulties[UnityEngine.Random.Range(0, availableDifficulties.Count)];
				
                var diff = config.Difficulties[config.CurrentDifficulty];
                if (newIndex < 0 || newIndex > 3) newIndex = UnityEngine.Random.Range(0, 4);
				hasCrateBeenHacked = false;
                hopperLooters.Clear(); 
                cachedEngine.inventory?.Clear();
                int dieselAmount = (Barriles == -1 ? UnityEngine.Random.Range(diff.MinDiesel, diff.MaxDiesel + 1) : Barriles);
                Item diesel = ItemManager.CreateByName("diesel_barrel", dieselAmount);
                if (diesel != null) diesel.MoveToContainer(cachedEngine.inventory);
				timeLeft = dieselAmount * 150f;
                cachedEngine.SendNetworkUpdateImmediate();

                Transform rootTransform = cachedExcavatorMonument != null ? cachedExcavatorMonument.transform : cachedEngine.transform;
                miningIndexField?.SetValue(cachedArm, newIndex);
                engineOnMethod?.Invoke(cachedEngine, null);

                timer.Once(1f, () => 
				{
					if (cachedArm == null) return;
					foreach (var container in cachedOutputPiles)
					{
						if (container != null && container.inventory != null) { container.inventory.SetFlag(ItemContainer.Flag.IsLocked, true); container.SendNetworkUpdate(); }
					}
					if (cachedEngine != null && cachedEngine.inventory != null)
					{
						cachedEngine.inventory.SetFlag(ItemContainer.Flag.IsLocked, true);
						cachedEngine.SendNetworkUpdate();
					}
					
					beginMiningMethod?.Invoke(cachedArm, null);
					if (triggerPlayer != null) SendMsg(triggerPlayer, "MiningStart", resourceTypes[newIndex], dieselAmount);
				});

                isLooteable = false;
                torretasVivas = cajasVivas = npcsVivos = 0; 
                
                monitoringTimer = CreateTimerRepeat(10f, CheckEngineStatus);

                if (spawnCoroutine != null) ServerMgr.Instance.StopCoroutine(spawnCoroutine);
                spawnCoroutine = ServerMgr.Instance.StartCoroutine(SpawnEventDefensesCoroutine(rootTransform));
                
                Vector3 markerPos = cachedExcavatorMonument != null ? cachedExcavatorMonument.transform.position : cachedEngine.transform.position;
                CreateEventMapMarkers(markerPos);
                
                playerCheckTimer = CreateTimerRepeat(2f, ProcessPlayerCUI); 
                clockTimer = CreateTimerRepeat(5f, UpdateClockCUI);
                
                BroadcastMsg("EventStarted");
                BroadcastMsg("EventDifficulty", diff.MarkerColor, diff.Name.ToUpper());
            });
        }
		private readonly WaitForSeconds wait05 = new WaitForSeconds(0.05f);
		private readonly WaitForSeconds wait10 = new WaitForSeconds(0.1f);
		private IEnumerator SpawnEventDefensesCoroutine(Transform rootTransform)
        {
            var diff = config.Difficulties[config.CurrentDifficulty];

            Vector3 cratePos = GetValidPositionOnExcavator(rootTransform, LocalCrateOffset);
            SpawnEventEntity("assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab", cratePos, Quaternion.identity);
            yield return wait10;

            foreach (var data in Event_Turrets)
            {
                Vector3 turretPos = GetValidPositionOnExcavator(rootTransform, data.Pos);
                Quaternion rotation = (data.Rot == Vector3.zero) 
                    ? Quaternion.LookRotation(turretPos - rootTransform.position) 
                    : rootTransform.rotation * Quaternion.Euler(data.Rot);

                SpawnEventEntity("assets/prefabs/npc/autoturret/autoturret_deployed.prefab", turretPos, rotation, diff.skinTurret);
                yield return wait05; 
            }

            if (NpcSpawn != null)
            {
                for (int i = 0; i < Event_NPCs.Count; i++)
                {
                    Vector3 npcPos = GetValidPositionOnExcavator(rootTransform, Event_NPCs[i]); 
                    JObject npcProfile = GetNpcProfile(i);
                    
                    object npcEntity = NpcSpawn.Call("SpawnCustom", npcPos, npcProfile) ?? NpcSpawn.Call("SpawnNpc", npcPos, npcProfile);
                    if (npcEntity is BaseEntity entity)
                    {
                        RegisterEntity(entity, true);
                    }
                    yield return wait10;
                }
            }
            spawnCoroutine = null;
        }

        private void CheckEngineStatus()
        {
            if (cachedEngine == null || !cachedEngine.IsOn())
            {
                if (monitoringTimer != null) { activeTimers.Remove(monitoringTimer); monitoringTimer.Destroy(); monitoringTimer = null; }
                StartLootCooldown();
				return;
            }

            if (cachedSignalComputerComponent != null)
				requestSuppliesMethod?.Invoke(cachedSignalComputerComponent, new object[] { null });
        }

		private void OnEntityEnter(TriggerBase trigger, BaseEntity entity)
		{
			if (isLooteable && isEventActive && entity is BasePlayer && trigger.gameObject.name.Contains("hackable"))
			{
				if (!isExtended && timeLeft < 960f) 
				{
					timeLeft += 960f;
					isExtended = true;
				}
			}
		}

        private void StartLootCooldown()
        {
            BroadcastMsg("EventNoFuel");
			BroadcastMsg("EventCooldown");
			isLooteable = true;
			timeLeft = 600f; 
			isExtended = false;

            if (cachedArm != null) stopMiningMethod?.Invoke(cachedArm, null);

            foreach (var container in cachedOutputPiles)
            {
                if (container != null && !container.IsDestroyed && container.inventory != null) container.inventory.SetFlag(ItemContainer.Flag.IsLocked, false);
            }

            if (cooldownTimer != null) { activeTimers.Remove(cooldownTimer); cooldownTimer.Destroy(); }
			cooldownTimer = CreateTimerRepeat(1f, () => 
			{
				timeLeft -= 1f;
				if (timeLeft <= 0 || !isEventActive) 
				{
                    cooldownTimer?.Destroy();
                    activeTimers.Remove(cooldownTimer);
					CleanUpEvent(false);
					return;
				}
				UpdateVendingMarker(timeLeft);
			});
        }
        
        private void UpdateVendingMarker(float timeL)
        {
            if (shopMapMarker == null || shopMapMarker.IsDestroyed) return;

            int min = Mathf.FloorToInt(timeL / 60);
            int sec = Mathf.FloorToInt(timeL % 60);
            
            int hoppersUntouched = 0;
            int hoppersLooted = 0;
            string lastLooter = "";

            for (int i = 0; i < cachedOutputPiles.Count; i++)
			{
				var hopper = cachedOutputPiles[i];
				if (hopper != null && !hopper.IsDestroyed && hopper.inventory != null)
				{
                    if (hopperLooters.ContainsKey(hopper.net.ID.Value))
                    {
                        hoppersLooted++;
                        lastLooter = hopperLooters[hopper.net.ID.Value];
                    }
                    else hoppersUntouched++;
                }
            }

            if (shopMapMarker is VendingMachineMapMarker vendingMarker)
            {
                string status = $"⏱ {min:00}:{sec:00} | Intactas: {hoppersUntouched}";
                if (hoppersLooted > 0)
                {
                    status += $" | Looteadas: {hoppersLooted}";
                    if (!string.IsNullOrEmpty(lastLooter)) status += $" [{lastLooter}]";
                }
                vendingMarker.markerShopName = status;
                vendingMarker.SendNetworkUpdate();
            }
        }
		
		private void OnCrateHack(HackableLockedCrate crate)
        {
            if (!isEventActive || crate == null || hasCrateBeenHacked) return;
            if (!spawnedEntityIds.Contains(crate.net.ID.Value)) return; 

            hasCrateBeenHacked = true;
			if (timeLeft < 900f)
			{
				float nuevosMinutos = 16f;
				timeLeft = (nuevosMinutos * 60f);
				Puts($"[Excavator Event] Caja hackeada quedando menos de 15 min. Evento ampliado a {nuevosMinutos} minutos.");
				UpdateVendingMarker(timeLeft);
			}
			/*return;
			if(config.Difficulties[CurrentDifficulty].HeliPatrol)
			{
				heliTimer1 = timer.Once(300f, () =>
				{
					//SpawnPatrolHelicopter();
					BroadcastMsg("HeliApproaching");
				});
			}

            // Si el nivel es superior a Medium (es decir: 3 = Expert, 4 = Hard, 5 = Nightmare)
            if (config.Difficulties[CurrentDifficulty].HeliPatrol2)
            {
                // Timer de 10 minutos para el segundo helicóptero (600 segundos)
                heliTimer2 = timer.Once(600f, () =>
                {
                    //SpawnPatrolHelicopter();
                    BroadcastMsg("HeliReinforcement");
                });
            }*/
        }

        private void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (isLooteable && isEventActive && entity != null && player != null)
            {
                if (cachedOutputPileIds.Contains(entity.net.ID.Value))
                {
                    hopperLooters[entity.net.ID.Value] = player.displayName;
                    SendMsg(player, "LootingHopper");
                }
            }
        }

        private JObject GetNpcProfile(int index)
        {
            var diff = config.Difficulties[config.CurrentDifficulty];
            
            int role = index % 4; 
            string profileKey = role == 0 ? "boss" : (role == 1 ? "elite" : "normal"); 

            if (!config.NpcProfiles.TryGetValue(profileKey, out NpcProfile profile))
                profile = new NpcProfile { Name = "Defensor", BaseHealth = 150f, Weapon = "rifle.lr300", Torso = "hazmatsuit" };

            string name = $"{profile.Name} {index}";
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

        private Vector3 GetValidPositionOnExcavator(Transform rootTransform, Vector3 localOffset)
        {
            return rootTransform.TransformPoint(localOffset);
        }

        private void SpawnEventEntity(string prefab, Vector3 position, Quaternion rotation, ulong skinId = 0)
        {
            BaseEntity entity = GameManager.server.CreateEntity(prefab, position, rotation);
            if (entity == null) return;

            if (skinId != 0) entity.skinID = skinId;

            entity.Spawn();
            RegisterEntity(entity);

            if (entity is AutoTurret turret)
            {
                turret.SetFlag(BaseEntity.Flags.On, false);
                turret.OwnerID = 0;

                NextTick(() =>
                {
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
                    turret.SendNetworkUpdateImmediate();
                });
            }
        }

        object OnTurretTarget(AutoTurret turret, BaseEntity target)
        {
			if (turret?.net == null) return null;
            if (spawnedEntityIds.Contains(turret.net.ID.Value))
            {
                if (target is BaseNpc || target is NPCPlayer) return false;
            }
            return null;
        }

        private void destroyallcui()
        {
            foreach (ulong userId in playersInEventZone) {
                BasePlayer player = BasePlayer.FindByID(userId);
                if (player != null) { CuiHelper.DestroyUi(player, "ExcavatorEventUI"); CuiHelper.DestroyUi(player, "ExcavatorEventUI_Dynamic"); CuiHelper.DestroyUi(player, "ExcavatorEventUI_Clock"); }
            }
            playersInEventZone.Clear();
        }

        private void CleanUpEvent(bool manualStop)
        {
            if (!isEventActive) return;

            if (redCircleMarker != null && !redCircleMarker.IsDestroyed) redCircleMarker.Kill();
            if (shopMapMarker != null && !shopMapMarker.IsDestroyed) shopMapMarker.Kill();
            redCircleMarker = null;
            shopMapMarker = null;

            if (spawnCoroutine != null)
            {
                ServerMgr.Instance.StopCoroutine(spawnCoroutine);
                spawnCoroutine = null;
            }

            if (cachedEngine != null && !cachedEngine.IsDestroyed && cachedEngine.IsOn())
            {
                if (cachedEngine.inventory != null)
                {
                    cachedEngine.inventory.Clear(); 
                    cachedEngine.inventory.MarkDirty();
                }
                try
                {
                    var fuelTickMethod = typeof(DieselEngine).GetMethod("EngineFuelTick", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                    if (fuelTickMethod != null) fuelTickMethod.Invoke(cachedEngine, null);
                }
                catch { }
            }

            for (int i = spawnedEntities.Count - 1; i >= 0; i--)
            {
                var entity = spawnedEntities[i];
                if (entity != null && !entity.IsDestroyed) entity.Kill();
            }

            foreach (var container in cachedOutputPiles)
            {
                if (container != null && container.inventory != null)
                {
                    container.inventory.SetFlag(ItemContainer.Flag.IsLocked, false);
                    container.inventory.Clear();
                    container.inventory.MarkDirty();
                    container.SendNetworkUpdate();
                }
            }

            if (cachedEngine != null && cachedEngine.inventory != null) cachedEngine.inventory.SetFlag(ItemContainer.Flag.IsLocked, false);
            
            destroyallcui();

            if (playerCheckTimer != null) { activeTimers.Remove(playerCheckTimer); playerCheckTimer.Destroy(); playerCheckTimer = null; }
            if (clockTimer != null) { activeTimers.Remove(clockTimer); clockTimer.Destroy(); clockTimer = null; }
            if (markerUpdateTimer != null) { activeTimers.Remove(markerUpdateTimer); markerUpdateTimer.Destroy(); markerUpdateTimer = null; }
            if (monitoringTimer != null) { activeTimers.Remove(monitoringTimer); monitoringTimer.Destroy(); monitoringTimer = null; }
            if (cooldownTimer != null) { activeTimers.Remove(cooldownTimer); cooldownTimer.Destroy(); cooldownTimer = null; }
            if (delayTimer != null) { activeTimers.Remove(delayTimer); delayTimer.Destroy(); delayTimer = null; }
            
            hopperLooters.Clear();
            spawnedEntities.Clear();
            spawnedEntityIds.Clear();
            isEventActive = false;

            if (manualStop) {
                BroadcastMsg("EventCancelled");
                if (playerCheckTimer != null) SendMsg(null, "EventStoppedManual");
            } else {
                BroadcastMsg("EventEnded");
            }
        }
		
        private object CanMoveItem(Item item, PlayerInventory playerLoot, ItemContainerId targetContainer, int targetSlot, int amount)
		{
			if (!isEventActive || isLooteable) return null;
			if (item == null || item.parent == null) return null;

			ulong sourceUid = item.parent.uid.Value;
			ulong targetUid = targetContainer.Value;
			ulong engineUid = (cachedEngine != null && cachedEngine.inventory != null) ? cachedEngine.inventory.uid.Value : 0;

			bool sourceIsProtected = cachedOutputPileIds.Contains(sourceUid) || (engineUid != 0 && sourceUid == engineUid);
			bool targetIsProtected = cachedOutputPileIds.Contains(targetUid) || (engineUid != 0 && targetUid == engineUid);
			
			if (sourceIsProtected && targetUid != sourceUid)
			{
				var player = playerLoot?.GetComponent<BasePlayer>();
				if (player != null) SendMsg(player, "CannotExtract");
				return false;
			}

			if (targetIsProtected)
			{
				var player = playerLoot?.GetComponent<BasePlayer>();
				if (player != null) SendMsg(player, "CannotInsert");
				return false; 
			}

			return null;
		}

        private object OnTurretConsumeAmmo(AutoTurret turret, Item ammo)
        {
			if (turret?.net == null) return null;
            if (isEventActive && turret != null && spawnedEntityIds.Contains(turret.net.ID.Value)) return false;
            return null;
        }
		private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity is AutoTurret turret && spawnedEntityIds.Contains(turret.net.ID.Value))
            {
                if (turret.inventory != null)
                {
                    turret.inventory.Clear();
                    turret.inventory.MarkDirty();
                }
            }
        }

        private object CanTurretTarget(AutoTurret turret, BaseCombatEntity target)
        {
			if (!isEventActive || turret == null) return null;
            if (target != null && spawnedEntityIds.Contains(turret.net.ID.Value))
            {
                if (target is NPCPlayer || target.IsNpc) return false;
            }
            return null;
        }
    }
}