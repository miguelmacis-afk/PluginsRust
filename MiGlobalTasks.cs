using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("MiGlobalTasks", "☠ Ⓜ🅘ⓖ🅤ⓔ🅛 ☠", "1.1.9")]
    public class MiGlobalTasks : RustPlugin
    {
		[PluginReference]
		private Plugin ImageLibrary;
        private bool eventActive = false;
        private TaskData currentTask;
        private Dictionary<ulong, float> scores = new Dictionary<ulong, float>();
        private Dictionary<ulong, Vector3> lastPositions = new Dictionary<ulong, Vector3>();
        private HashSet<ulong> hiddenUI = new HashSet<ulong>();
        private Timer eventTimer;
        private Timer moveTimer;
        private Timer durationTimer;
        private Dictionary<ulong, float> lastUIUpdate = new Dictionary<ulong, float>();
        
		private const string UIStatic = "TaskUI_Static";
		private const string UIDynamic = "TaskUI_Dynamic";
        private const int MinPlayersRequired = 1;
        private const float EventDuration = 600f; // 10 Minutos

        class TaskData
        {
            public string Name;
            public string Shortname; 
            public string Sprite;
            public TaskType Type;
        }

        public enum TaskType { Gather, Kill, Move, Loot }
        private List<TaskData> taskList = new List<TaskData>();

        void OnServerInitialized()
        {
            LoadAllTasks();
            eventTimer = timer.Every(3000f, TryStartEvent);
            TryStartEvent(); 
        }

        void Unload()
        {
            if (eventTimer != null) eventTimer.Destroy();
            if (moveTimer != null) moveTimer.Destroy();
            if (durationTimer != null) durationTimer.Destroy();
            foreach (var p in BasePlayer.activePlayerList) {
				CuiHelper.DestroyUi(p, UIStatic);
				CuiHelper.DestroyUi(p, UIDynamic);
			}
        }
		
		// Diccionario temporal para trackear qué había en el inventario antes de saquear
		private Dictionary<ulong, Dictionary<string, int>> inventorySnapshot = new Dictionary<ulong, Dictionary<string, int>>();

		// Cuando el jugador empieza a saquear, guardamos lo que ya tiene
		void OnLootEntity(BasePlayer player, BaseEntity entity)
		{
			if (!eventActive || currentTask?.Type != TaskType.Loot) return;
			
			var items = new Dictionary<string, int>();
			// Recorremos los contenedores principales (Main y Belt)
			foreach (var item in player.inventory.containerMain.itemList)
				UpdateItemDict(items, item);
			foreach (var item in player.inventory.containerBelt.itemList)
				UpdateItemDict(items, item);
				
			inventorySnapshot[player.userID] = items;
		}

		// Cuando cierra la caja, comparamos la diferencia
		void OnLootEntityEnd(BasePlayer player, BaseCombatEntity entity)
		{
			if (!eventActive || currentTask?.Type != TaskType.Loot) return;
			if (!inventorySnapshot.ContainsKey(player.userID)) return;

			if (entity is PlayerCorpse || entity is LootableCorpse) 
			{
				inventorySnapshot.Remove(player.userID);
				return;
			}

			int currentAmount = 0;
			// Sumamos lo actual
			foreach (var item in player.inventory.containerMain.itemList)
				if (item.info.shortname == currentTask.Shortname) currentAmount += item.amount;
			foreach (var item in player.inventory.containerBelt.itemList)
				if (item.info.shortname == currentTask.Shortname) currentAmount += item.amount;

			int oldAmount = 0;
			inventorySnapshot[player.userID].TryGetValue(currentTask.Shortname, out oldAmount);

			if (currentAmount > oldAmount)
			{
				CheckProgress(player, currentAmount - oldAmount);
			}

			inventorySnapshot.Remove(player.userID);
		}
		void UpdateItemDict(Dictionary<string, int> dict, Item item)
		{
			if (item == null) return;
			if (!dict.ContainsKey(item.info.shortname)) dict[item.info.shortname] = 0;
			dict[item.info.shortname] += item.amount;
		}

        void LoadAllTasks()
        {
            taskList.Clear();
            // --- 1. CATEGORÍA: CAZA (Kill) ---
            AddTask("Cazar \nCocodrilo", "crocodile", "https://raw.githubusercontent.com/miguelmacis-afk/rustImage/refs/heads/main/crocodile.png", TaskType.Kill);
            AddTask("Cazar \nOsos", "bear", "https://raw.githubusercontent.com/miguelmacis-afk/rustImage/refs/heads/main/bear.png", TaskType.Kill);
            AddTask("Cazar \nOso Polar", "polarbear", "https://raw.githubusercontent.com/miguelmacis-afk/rustImage/refs/heads/main/polar-bear.png", TaskType.Kill);
            AddTask("Cazar \nLobo", "wolf", "https://raw.githubusercontent.com/miguelmacis-afk/rustImage/refs/heads/main/wolf.png", TaskType.Kill);
            AddTask("Cazar \nJabalí", "boar", "https://raw.githubusercontent.com/miguelmacis-afk/rustImage/refs/heads/main/boar.png", TaskType.Kill);
            AddTask("Cazar \nVenado", "stag", "https://raw.githubusercontent.com/miguelmacis-afk/rustImage/refs/heads/main/stag.png", TaskType.Kill);
            AddTask("Cazar \nGallina", "chicken", "https://raw.githubusercontent.com/miguelmacis-afk/rustImage/refs/heads/main/chicken.png", TaskType.Kill);
            AddTask("Cazar \nCaballo", "horse", "https://raw.githubusercontent.com/miguelmacis-afk/rustImage/refs/heads/main/ridable-horse%20(1).png", TaskType.Kill); 
            AddTask("Cazar \nTiburón", "simpleshark", "https://raw.githubusercontent.com/miguelmacis-afk/rustImage/refs/heads/main/shark.png", TaskType.Kill);
            AddTask("Cazar \nSuperviviente", "player", "https://raw.githubusercontent.com/miguelmacis-afk/rustImage/refs/heads/main/Rust-Titel-780x438.jpg", TaskType.Kill);
            AddTask("Cazar \nCientífico", "scientist", "https://raw.githubusercontent.com/miguelmacis-afk/rustImage/refs/heads/main/oubreak_scientist.png", TaskType.Kill);
            //?AddTask("Científico Túnel", "scientist_npc_underground", "assets/icons/target", TaskType.Kill);
            //?AddTask("Científico Oilrig", "scientistnpc_oilrig", "assets/icons/target", TaskType.Kill);
            //?AddTask("Científico Ártico", "scientist_npc_arctic", "assets/icons/target", TaskType.Kill);
            //?AddTask("Científico Excavadora", "scientist_npc_excavator", "assets/icons/target", TaskType.Kill);
            //?AddTask("Científico Pesado", "scientistnpc_heavy", "assets/icons/target", TaskType.Kill);
            //?AddTask("Científico Silo", "scientist_npc_silo", "assets/icons/target", TaskType.Kill);
            //?AddTask("Científico Laboratorio", "scientist_npc_lab", "assets/icons/target", TaskType.Kill); 
            //?AddTask("Científico Junkpile", "scientistnpc_junkpile", "assets/icons/target", TaskType.Kill);
            AddTask("Cazar \nEspantapájaros", "scarecrow", "https://raw.githubusercontent.com/miguelmacis-afk/rustImage/refs/heads/main/scarecrow.suit.png", TaskType.Kill);
            AddTask("Romper \nBarriles", "barrel", "https://raw.githubusercontent.com/miguelmacis-afk/rustImage/refs/heads/main/barrel.png", TaskType.Kill); 
 
            // --- 2. CATEGORÍA: RECOLECCIÓN NATURAL (Gather) ---
			AddTask("Talar \nMadera", "wood", "", TaskType.Gather);
            AddTask("Picar \nPiedra", "stones", "", TaskType.Gather);
            AddTask("Extraer \nHierro", "metal.ore", "", TaskType.Gather);
            AddTask("Extraer \nAzufre", "sulfur.ore", "", TaskType.Gather);
            AddTask("Extraer \nHQM", "hq.metal.ore", "", TaskType.Gather); 
            AddTask("Recoger \nCáñamo", "cloth", "", TaskType.Gather);
            AddTask("Recolectar \nCuero", "leather", "", TaskType.Gather);
            AddTask("Recolectar \nGrasa Animal", "fat.animal", "", TaskType.Gather);
            AddTask("Recolectar \nHuesos Animales", "bone.fragments", "", TaskType.Gather);
            AddTask("Recolectar \nCráneo Humano", "skull.human", "", TaskType.Gather);
            AddTask("Recolectar \nCráneo de Lobo", "skull.wolf", "", TaskType.Gather);
            //?AddTask("Recolectar Carbón", "charcoal", "", TaskType.Gather);NO
            AddTask("Cosecha \nMaíz", "corn", "", TaskType.Gather);
            AddTask("Cosecha \nPatatas", "potato", "", TaskType.Gather);
            AddTask("Cosecha \nCalabazas", "pumpkin", "", TaskType.Gather);
            AddTask("Cosecha \nSetas", "mushroom", "", TaskType.Gather);
            AddTask("Cosecha \nCactus", "cactusflesh", "", TaskType.Gather);
            AddTask("Cosecha \nBayas Azules", "blue.berry", "", TaskType.Gather);
            AddTask("Cosecha \nBayas Blancas", "white.berry", "", TaskType.Gather);
            AddTask("Cosecha \nBayas Verdes", "green.berry", "", TaskType.Gather);
            AddTask("Cosecha \nBayas Rojas", "red.berry", "", TaskType.Gather);
            AddTask("Cosecha \nBayas Amarillas", "yellow.berry", "", TaskType.Gather);
  
            // --- 3. CATEGORÍA: COMPONENTES Y LOOT (Gather/Item) ---
            //AddTask("Recuperar Chatarra", "scrap", "", TaskType.Loot);
            //AddTask("Engranaje", "gears", "", TaskType.Loot);
            //AddTask("Muelle", "metalspring", "", TaskType.Loot);
            //AddTask("Señal de Tráfico", "roadsigns", "", TaskType.Loot);
            //AddTask("Tubería", "metalpipe", "", TaskType.Loot);
            //AddTask("Hoja de Metal", "metalblade", "", TaskType.Loot);
            //AddTask("Chapa de Metal", "sheetmetal", "", TaskType.Loot);
            //AddTask("Componente Tech", "techparts", "", TaskType.Loot);
            //AddTask("Cuerda", "rope", "", TaskType.Loot);
            //AddTask("Lona", "tarp", "", TaskType.Loot);
            //AddTask("Cuerpo de Semi", "semibody", "", TaskType.Loot);
            //AddTask("Cuerpo de SMG", "smgbody", "", TaskType.Loot);
            //AddTask("Cuerpo de Fusil", "riflebody", "", TaskType.Loot);
            //AddTask("Tanque Propano", "propanetank", "", TaskType.Loot);
            //AddTask("Cámara CCTV", "cctv.camera", "", TaskType.Loot);
            //AddTask("Ordenador", "targeting.computer", "", TaskType.Loot);
            //AddTask("Fusible", "fuse", "", TaskType.Loot);
            //AddTask("Barril Diésel", "diesel_barrel", "", TaskType.Loot);
            //AddTask("Tarjeta Verde", "keycard_green", "", TaskType.Loot);
            //AddTask("Tarjeta Azul", "keycard_blue", "", TaskType.Loot);
            //AddTask("Tarjeta Roja", "keycard_red", "", TaskType.Loot);
			//AddTask("Lata de Atún", "can.tuna", "", TaskType.Loot);
            //AddTask("Lata de Alubias", "can.beans", "", TaskType.Loot);
            //AddTask("Manzana", "apple", "", TaskType.Loot);
            //AddTask("Binoculares", "tool.binoculars", "", TaskType.Loot);
            //AddTask("Panel Solar", "electric.solarpanel.large", "", TaskType.Loot);
			
            // --- 4. CATEGORÍA: CAJAS Y BARRILES  (Loot/Barrel) ---
            AddTask("Abre Cajas \nMilitares", "crate_normal", "https://raw.githubusercontent.com/miguelmacis-afk/rustImage/refs/heads/main/military-crate.png", TaskType.Loot);
            AddTask("Abre Cajas \nÉlite", "crate_elite", "https://raw.githubusercontent.com/miguelmacis-afk/rustImage/refs/heads/main/elite-crate.png", TaskType.Loot);
            AddTask("Abre Cajas \nComunes", "crate_normal_2", "https://raw.githubusercontent.com/miguelmacis-afk/rustImage/refs/heads/main/cratecostume.png", TaskType.Loot);
            AddTask("Abre Cajas \nHerramientas", "crate_tools", "https://raw.githubusercontent.com/miguelmacis-afk/rustImage/refs/heads/main/tool-crate.png", TaskType.Loot);
            AddTask("Abre Cajas \nde Comida", "foodbox", "https://raw.githubusercontent.com/miguelmacis-afk/rustImage/refs/heads/main/foodbox.png", TaskType.Loot);
            AddTask("Abre AirDrop", "supply_drop", "https://raw.githubusercontent.com/miguelmacis-afk/rustImage/refs/heads/main/supply-drop.png", TaskType.Loot);
 
            // --- 5. CATEGORÍA: VEHÍCULOS (Move) ---
            AddTask("Viaja en \nMinicopter", "minicopter.entity", "https://raw.githubusercontent.com/miguelmacis-afk/rustImage/refs/heads/main/minicopter.png", TaskType.Move);
            AddTask("Viaja en \nScrapHeli", "scraptransporthelicopter", "https://raw.githubusercontent.com/miguelmacis-afk/rustImage/refs/heads/main/scrap-heli.png", TaskType.Move);
            AddTask("Viaja en \nBote", "rowboat", "https://raw.githubusercontent.com/miguelmacis-afk/rustImage/refs/heads/main/rowboat.png", TaskType.Move); 
            AddTask("Viaja en \nLancha Cargo", "rhib", "https://raw.githubusercontent.com/miguelmacis-afk/rustImage/refs/heads/main/rhib.png", TaskType.Move); 
            AddTask("Viaja en \nCaballo", "ridablehorse", "https://raw.githubusercontent.com/miguelmacis-afk/rustImage/refs/heads/main/ridable-horse%20(1).png", TaskType.Move);
            AddTask("Viaja en \nTren", "workcart", "https://raw.githubusercontent.com/miguelmacis-afk/rustImage/refs/heads/main/workcart.png", TaskType.Move);
            AddTask("Viaja en \nMoto", "motorbike", "https://raw.githubusercontent.com/miguelmacis-afk/rustImage/refs/heads/main/motorbike.png", TaskType.Move);
            AddTask("Viaja en \nCoche Modular", "modular", "https://raw.githubusercontent.com/miguelmacis-afk/rustImage/refs/heads/main/modular-vehicle-2.png", TaskType.Move);
			AddTask("Viaja en \nSubmarino Duo", "submarineduo.entity", "https://raw.githubusercontent.com/miguelmacis-afk/rustImage/refs/heads/main/submarine-duo.png", TaskType.Move);
			AddTask("Viaja en \nSubmarino Solo", "submarinesolo.entity", "https://raw.githubusercontent.com/miguelmacis-afk/rustImage/refs/heads/main/submarine-solo.png", TaskType.Move);

            
			
			foreach (var task in taskList)
			{
				if (!string.IsNullOrEmpty(task.Sprite) && task.Sprite.StartsWith("http"))
				{
					// Esto le dice a ImageLibrary que descargue la imagen y la nombre "Task_Shortname"
					ImageLibrary?.Call("AddImage", task.Sprite, "Task2_" + task.Shortname);
				}
			}
        } 

		void OnCollectiblePickup(CollectibleEntity collectible, BasePlayer player)
		{
			if (!eventActive || currentTask == null || player == null || collectible == null) return;

			if (currentTask.Type == TaskType.Gather)//1
			{
				// Esto recorre lo que la planta DA realmente (ej: cloth y seeds)
				foreach (var itemAmount in collectible.itemList)
				{
					if (itemAmount.itemDef.shortname == currentTask.Shortname)
					{
						CheckProgress(player, itemAmount.amount);
					}
				}
			}
		}


         void AddTask(string n, string s, string sprite, TaskType t) => taskList.Add(new TaskData { Name = n, Shortname = s, Sprite = sprite, Type = t });

        void TryStartEvent()
        {
            if (BasePlayer.activePlayerList.Count < MinPlayersRequired) return;
            StartNewEvent();
        }
		void OnPlayerInit(BasePlayer player)
		{
			if (hiddenUI.Contains(player.userID))
				hiddenUI.Remove(player.userID);
		}
		private double eventEndTime;
        void StartNewEvent()
		{
			if (taskList.Count == 0) return;

			// 1. Limpieza de procesos antiguos (CRÍTICO para evitar LAG)
			moveTimer?.Destroy();
			durationTimer?.Destroy();
			// Debes declarar una variable global: private Timer refreshTimer;
			refreshTimer?.Destroy(); 
			foreach (var p in BasePlayer.activePlayerList) { 
				CuiHelper.DestroyUi(p, UIStatic);
				CuiHelper.DestroyUi(p, UIDynamic); 
			}

			// 2. Reinicio de datos
			lootedEntities.Clear();
			scores.Clear();
			lastPositions.Clear();
			hiddenUI.Clear();
			lastUIUpdate.Clear();
			lastItemTime.Clear();
			eventActive = true;
			eventEndTime = Interface.Oxide.Now + EventDuration; // Interface.Oxide.Now nos da el tiempo actual en segundos
			// 3. Selección de tarea
			currentTask = taskList[UnityEngine.Random.Range(0, taskList.Count)];
			foreach (var p in BasePlayer.activePlayerList) 
			{
				if (!hiddenUI.Contains(p.userID))
					DrawStaticUI(p);
			}

			// 4. Configuración de Timers
			// Guardamos el timer en la variable para poder destruirlo al terminar
			refreshTimer = timer.Every(1.0f, () => {
				if (!eventActive) {
					refreshTimer?.Destroy();
					return;
				}
				foreach (var p in BasePlayer.activePlayerList) 
				{
					if (!hiddenUI.Contains(p.userID))
						DrawDynamicUI(p); // <--- SOLO LA DINÁMICA
				}
			});

			if (currentTask.Type == TaskType.Move) 
				moveTimer = timer.Every(1.0f, UpdateMoveProgress);

			durationTimer = timer.Once(EventDuration, EndEvent);
			string taskNameClean = currentTask.Name.Replace("\n", "");
			// 5. Anuncio
			PrintToChat($"<color=#55AAFF>[EVENTO]</color> Tarea: <color=#FFFF00>{taskNameClean}</color>.\n ¡Los Top 3 ganan premios en 10 min!");
		}

		private Dictionary<string, float> lastItemTime = new Dictionary<string, float>();

		void CheckProgress(BasePlayer player, float amount)
		{
			if (!eventActive || player == null) return;
			
			// Si el jugador tenía la UI oculta, se la volvemos a mostrar al progresar
			if (hiddenUI.Contains(player.userID)) { DrawStaticUI(player); hiddenUI.Remove(player.userID);}

			// Sistema de Cooldown para evitar doble conteo (Ghost hits)
			string cooldownKey = $"{player.userID}_{currentTask.Shortname}";
			if (lastItemTime.TryGetValue(cooldownKey, out float lastTime) && Time.time - lastTime < 0.1f)
				return;

			// Registro de puntos
			if (!scores.ContainsKey(player.userID)) scores[player.userID] = 0;
			scores[player.userID] += amount;
			lastItemTime[cooldownKey] = Time.time;

			// ACTUALIZACIÓN RÁPIDA: Refresco instantáneo solo para el interesado
			DrawDynamicUI(player);
		}
		

        // --- HOOKS ---
		void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
		{
			// 1. Verificaciones de seguridad básicas
			if (!eventActive || currentTask?.Type != TaskType.Gather) return;


			// 2. Intentar obtener al jugador (la entidad que recolecta)
			BasePlayer player = entity as BasePlayer;
			if (player == null) return;

			// 3. ¡ESTA ES LA CLAVE!: Comprobar el shortname ANTES que el TaskType
			// Esto permite que el cráneo se detecte aunque el Type sea Gather o Loot
			if (item.info.shortname == currentTask.Shortname)
			{
				CheckProgress(player, item.amount);
				return; 
			}
		}
		
		void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
		{
			// Filtro ultra-rápido: si no es farmeo, ni leemos lo que hay dentro
			if (!eventActive || currentTask?.Type != TaskType.Gather || player == null) return;

			if (item.info.shortname == currentTask.Shortname)
			{
				CheckProgress(player, item.amount);
			}
		}

        void OnEntityDeath(BaseCombatEntity e, HitInfo h) 
		{
			if (!eventActive || currentTask?.Type != TaskType.Kill) return; 

			var p = h?.Initiator as BasePlayer;
			if (p == null || p == e) return; 

			string prefab = e.ShortPrefabName.ToLower();
			string target = currentTask.Shortname.ToLower();
			bool isMatch = false;

			// 1. Diferenciar JUGADORES reales de NPCs
			if (target == "player")
			{
				// Un jugador real es BasePlayer pero NO es NPCPlayer
				isMatch = (e is BasePlayer && !(e is NPCPlayer));
			}
			// 2. Diferenciar CIENTÍFICOS y NPCs Humanos
			else if (e is NPCPlayer)
			{
				// Esto separa por ejemplo 'scientist' de 'scarecrow'
				isMatch = prefab.Contains(target);
			}
			// 3. Diferenciar ANIMALES y BARRILES (Entidades no humanas)
			else 
			{
				isMatch = prefab.Contains(target);
			}

			if (isMatch)
			{
				CheckProgress(p, 1);
			}
		}
 
		private HashSet<ulong> lootedEntities = new HashSet<ulong>();

		// 2. En tu función StartNewEvent() añade esta línea para limpiar la lista:
		// 
		void OnLootEntity(BasePlayer p, LootContainer container) 
		{
			if (!eventActive || currentTask == null || p == null || container == null) return;

			if (currentTask.Type == TaskType.Loot)
			{
				// 3. Verificamos si la caja ya fue contada en ESTE evento usando su ID de red
				if (lootedEntities.Contains(container.net.ID.Value)) return;

				string prefabName = container.ShortPrefabName.ToLower();
				string targetTask = currentTask.Shortname.ToLower();

				if (prefabName.Contains(targetTask))
				{
					// Marcamos la caja como "ya contada"
					lootedEntities.Add(container.net.ID.Value); 
					CheckProgress(p, 1);
				}
			}
		}

		void UpdateMoveProgress()
		{
			if (!eventActive || currentTask?.Type != TaskType.Move) return;

			foreach (var player in BasePlayer.activePlayerList)
			{
				// 1. Filtro ultra-rápido: Si no está montado en NADA, saltamos al siguiente.
				if (player == null || !player.IsConnected || !player.isMounted) continue;
				//if (hiddenUI.Contains(player.userID)) continue;

				var mounted = player.GetMounted();
				BaseEntity vehicle = mounted?.GetParentEntity() ?? mounted;
				//Puts(vehicle.ShortPrefabName);
				if (vehicle == null) continue;

				// 2. Simplificación de la comparación
				if (!vehicle.ShortPrefabName.Contains(currentTask.Shortname.Replace(".entity", ""))) continue;

				// 3. Cálculo de distancia
				Vector3 currentPos = player.transform.position;
				if (!lastPositions.ContainsKey(player.userID)) 
				{
					lastPositions[player.userID] = currentPos;
					continue;
				}

				float dist = Vector3.Distance(currentPos, lastPositions[player.userID]);
				
				if (dist > 1.0f && dist < 150f) 
				{
					CheckProgress(player, dist);
					lastPositions[player.userID] = currentPos;
				}
			}
		}
		private Timer refreshTimer;
        void EndEvent()
        {
            eventActive = false;
			moveTimer?.Destroy();
			durationTimer?.Destroy();
			refreshTimer?.Destroy(); // Detiene el refresco global
            foreach (var p in BasePlayer.activePlayerList) 
			{
				CuiHelper.DestroyUi(p, UIStatic);
				CuiHelper.DestroyUi(p, UIDynamic);
			}

            var top = scores.OrderByDescending(x => x.Value).Take(3).ToList();
			if(top.Count == 0) return;
            PrintToChat("★★★<color=#FFAA00> EVENTO FINALIZADO - RESULTADOS </color>★★★");

            for (int i = 0; i < top.Count; i++)
            {
                ulong userId = top[i].Key;
                BasePlayer p = BasePlayer.FindByID(userId);
                string playerName = p != null ? p.displayName : $"ID:{userId}";
                if (p != null)
                {
                    if (i == 0)
					{ 
						GiveReward(p, "supply.signal", 2);
						string steamId = p.UserIDString;
						int puntosPremio = 1000;
						rust.RunServerCommand($"givexp {steamId} {puntosPremio} true");
					}
                    if (i == 1) GiveReward(p, "supply.signal", 1);
                    if (i == 2) GiveReward(p, "scrap", 150);
                }
                PrintToChat($"<color=#AAAAAA>#{i+1}</color> <color=#FFFFFF>{playerName}</color> - {Math.Floor(top[i].Value)}");
            }
        }

        void GiveReward(BasePlayer player, string s, int a) => player.GiveItem(ItemManager.CreateByName(s, a));

        [ConsoleCommand("tasks.close")]
        void cmdCloseUI(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;
            hiddenUI.Add(player.userID);
            CuiHelper.DestroyUi(player, UIStatic);
			CuiHelper.DestroyUi(player, UIDynamic);
        }
		
		void OnPlayerSleepEnded(BasePlayer player)
		{
			if (eventActive && currentTask != null)
			{
				// Aseguramos que no esté en la lista de ocultos por si acaso
				if (hiddenUI.Contains(player.userID)) 
					hiddenUI.Remove(player.userID);
				DrawStaticUI(player);
				// Un pequeño retraso de 1.5s ayuda a que la UI de Rust no ignore el comando de dibujado
				timer.Once(2.0f, () =>
				{
					if (player != null && player.IsConnected)
						DrawDynamicUI(player);
				});
			}
		}

		// PARTE ESTÁTICA: Fondo, Título, Icono y Botón de cerrar
		void DrawStaticUI(BasePlayer player)
		{
			// Solo necesitamos enviarla una vez. Si ya existe, no la recreamos para evitar parpadeos
			// Podemos usar una verificación simple o simplemente llamarla en momentos clave
			CuiHelper.DestroyUi(player, UIStatic);

			var container = new CuiElementContainer();

			string mainPanel = container.Add(new CuiPanel
			{
				Image = { Color = "0 0 0 0.85" },
				RectTransform = { AnchorMin = "0.84 0.68", AnchorMax = "0.98 0.92" }
			}, "Hud", UIStatic);

			// Imagen de fondo (Icono de la tarea)
			var def = ItemManager.FindItemDefinition(currentTask.Shortname);

			// 1. Si es un item nativo de Rust (Madera, Piedra, etc.)
			if (def != null && !currentTask.Sprite.StartsWith("http"))
			{
				container.Add(new CuiElement {
					Parent = mainPanel,
					Components = {
						new CuiImageComponent { ItemId = def.itemid, Color = "1 1 1 0.4" },
						new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
					}
				});
			}
			// 2. Si es una imagen externa (Caché de ImageLibrary)
			else
			{
				// Obtenemos el ID de la imagen guardada en el servidor
				string imageId = (string)ImageLibrary?.Call("GetImage", "Task2_" + currentTask.Shortname);
				
				container.Add(new CuiElement {
					Parent = mainPanel,
					Components = {
						// Usamos Png en lugar de Url para evitar parpadeos
						new CuiRawImageComponent { Png = imageId, Color = "1 1 1 0.4" },
						new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
					}
				});
			}

			// Capa de oscurecimiento y Título
			container.Add(new CuiPanel { Image = { Color = "0 0 0 0.55" }, RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" } }, mainPanel);
			
			container.Add(new CuiLabel {
				Text = { Text = currentTask.Name.ToUpper(), FontSize = 12, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" },
				RectTransform = { AnchorMin = "0 0.82", AnchorMax = "1 0.98" }
			}, mainPanel);

			container.Add(new CuiPanel { Image = { Color = "1 1 1 0.15" }, RectTransform = { AnchorMin = "0.1 0.8", AnchorMax = "0.9 0.805" } }, mainPanel);

			// Botón Cerrar (Es estático porque el comando no cambia)
			container.Add(new CuiButton {
				Button = { Command = "tasks.close", Color = "0.5 0.5 0.5 0.1" },
				RectTransform = { AnchorMin = "0.92 0.92", AnchorMax = "0.99 0.99" },
				Text = { Text = "×", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.6" }
			}, mainPanel);

			CuiHelper.AddUi(player, container);
		}

		// PARTE DINÁMICA: Los nombres y puntajes
		void DrawDynamicUI(BasePlayer player)
		{
			CuiHelper.DestroyUi(player, UIDynamic);
			var container = new CuiElementContainer();

			string dataPanel = container.Add(new CuiPanel
			{
				Image = { Color = "0 0 0 0" },
				RectTransform = { AnchorMin = "0.84 0.68", AnchorMax = "0.98 0.91" }
			}, "Hud", UIDynamic);

			// --- CÁLCULO DEL TIEMPO ---
			double timeLeft = eventEndTime - Interface.Oxide.Now;
			if (timeLeft < 0) timeLeft = 0;
			
			TimeSpan t = TimeSpan.FromSeconds(timeLeft);
			string timeStr = string.Format("{0:D2}:{1:D2}", t.Minutes, t.Seconds);

			// 1. LISTADO TOP 3 (Se mantiene igual)
			var topPlayers = scores.OrderByDescending(x => x.Value).Take(3).ToList();
			float yOffset = 0.65f; 

			for (int i = 0; i < 3; i++)
			{
				string rankColor = (i == 0) ? "#FFD700" : (i == 1) ? "#C0C0C0" : "#CD7F32";
				string pName = "---";
				string pScore = "0";

				if (i < topPlayers.Count)
				{
					BasePlayer p = BasePlayer.FindByID(topPlayers[i].Key);
					pName = p != null ? (p.displayName.Length > 12 ? p.displayName.Substring(0, 12) : p.displayName) : "Offline";
					pScore = Math.Floor(topPlayers[i].Value).ToString();
				}

				container.Add(new CuiLabel {
					Text = { Text = $"<color={rankColor}>#{i+1}</color>  {pName}", FontSize = 10, Align = TextAnchor.MiddleLeft },
					RectTransform = { AnchorMin = $"0.08 {yOffset}", AnchorMax = $"0.7 {yOffset + 0.08f}" }
				}, dataPanel);

				container.Add(new CuiLabel {
					Text = { Text = pScore, FontSize = 10, Align = TextAnchor.MiddleRight, Font = "robotocondensed-bold.ttf" },
					RectTransform = { AnchorMin = $"0.7 {yOffset}", AnchorMax = $"0.92 {yOffset + 0.08f}" }
				}, dataPanel);

				yOffset -= 0.11f;
			}

			// 2. TU PROGRESO (Subido de 0.05 a 0.14)
			float myScore = scores.ContainsKey(player.userID) ? (float)Math.Floor(scores[player.userID]) : 0;
			container.Add(new CuiLabel {
				Text = { Text = $"TU PROGRESO: <color=#FFFF00>{myScore}</color>", FontSize = 10, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" },
				RectTransform = { AnchorMin = "0 0.14", AnchorMax = "1 0.21" }
			}, dataPanel);

			// 3. TIEMPO RESTANTE (Ubicado justo debajo del progreso)
			container.Add(new CuiLabel {
				Text = { Text = $"TIEMPO: <color=#FFFF00>{timeStr}</color>", FontSize = 10, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" },
				RectTransform = { AnchorMin = "0 0.04", AnchorMax = "1 0.11" }
			}, dataPanel);

			CuiHelper.AddUi(player, container);
		}			
    }
}
