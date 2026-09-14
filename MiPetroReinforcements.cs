using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("MiPetroReinforcements", "☠ Ⓜ🅘ⓖ🅤ⓔ🅛 ☠", "2.3.0")]
    [Description("Asigna dificultades aleatorias, CUI de configuración, marcadores en mapa y spawnea refuerzos.")]
    public class MiPetroReinforcements : RustPlugin
    { 
        private Configuration config;
        private List<BaseEntity> markersActivos = new List<BaseEntity>();
        private Dictionary<MonumentInfo, string> petroDificultades = new Dictionary<MonumentInfo, string>();
        private List<MonumentInfo> _oilRigsCachados = new List<MonumentInfo>();
        private HashSet<Vector3> cajasSpawneadas = new HashSet<Vector3>();

        // Las rutas se mantienen exactamente idénticas a las originales
        private List<Vector3> rutaPetroPequena = new List<Vector3> 
        {
            new Vector3(45.48f, 31.51f, -16.59f), new Vector3(32.72f, 31.51f, -17.44f),
			new Vector3(31.54f, 31.51f, -7.08f), new Vector3(45.45f, 31.51f, -5.15f)
        };

        private List<Vector3> rutaPetroGrande = new List<Vector3> 
        {
            new Vector3(22.26f, 45.13f, -1.78f), new Vector3(22.92f, 45.13f, 10.63f),
            new Vector3(33.71f, 45.13f, 11.24f), new Vector3(33.88f, 45.13f, -0.98f)
        };

        private readonly List<string> availablePrefabs = new List<string> 
        { 
            "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_oilrig.prefab", 
            "assets/rust.ai/agents/npcplayer/humannpc/scientist/gen2/scientist2.prefab", 
            "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_bradley_heavy.prefab", 
            "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_heavy.prefab",
            "assets/rust.ai/agents/npcplayer/humannpc/tunneldweller/tunneldweller.prefab"
        };

        private class Configuration
        {
            [JsonProperty("Configuracion de Dificultades")]
            public Dictionary<string, DatosDificultad> Niveles = new Dictionary<string, DatosDificultad>();
        }

        private class DatosDificultad
        {
            public int BotsExtras;
            public string PrefabBot;
            public bool Bradley;
            public bool ExtraCrate;
            public string MarkerColor;
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
            config.Niveles.Add("Easy", new DatosDificultad { BotsExtras = 2, PrefabBot = availablePrefabs[0], Bradley = false, ExtraCrate = false, MarkerColor = "#3cd834" });
            config.Niveles.Add("Normal", new DatosDificultad { BotsExtras = 4, PrefabBot = availablePrefabs[1], Bradley = false, ExtraCrate = false, MarkerColor = "#e57109" });
            config.Niveles.Add("Medium", new DatosDificultad { BotsExtras = 6, PrefabBot = availablePrefabs[1], Bradley = true, ExtraCrate = true, MarkerColor = "#e7c50a" });
            config.Niveles.Add("Expert", new DatosDificultad { BotsExtras = 6, PrefabBot = availablePrefabs[2], Bradley = true, ExtraCrate = true, MarkerColor = "#9c1825" });
            config.Niveles.Add("Hard", new DatosDificultad { BotsExtras = 8, PrefabBot = availablePrefabs[2], Bradley = true, ExtraCrate = true, MarkerColor = "#0000FF" });
            config.Niveles.Add("Nightmare", new DatosDificultad { BotsExtras = 12, PrefabBot = availablePrefabs[3], Bradley = true, ExtraCrate = true, MarkerColor = "#9236f5" });
            SaveConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig(); 
            config = Config.ReadObject<Configuration>();
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        void OnServerInitialized()
        {
            CachearMonumentos();
            RotarDificultades();
        }

        private void CachearMonumentos()
        {
            _oilRigsCachados.Clear();
            foreach (MonumentInfo monument in TerrainMeta.Path.Monuments)
            {
                if (monument != null && (monument.name.Contains("oil_rig", StringComparison.OrdinalIgnoreCase) || monument.name.Contains("oilrig", StringComparison.OrdinalIgnoreCase)))
                {
                    _oilRigsCachados.Add(monument);
                }
            }
        }

        private void OnPlayerConnected(BasePlayer player) 
        {
            if (player == null) return;
            timer.Once(2f, () => {
                if (player == null || !player.IsConnected) return;
                
                int count = markersActivos.Count;
                for (int i = 0; i < count; i++)
                {
                    BaseEntity marker = markersActivos[i];
                    if (marker != null && !marker.IsDestroyed)
                    {
                        marker.SendNetworkUpdate();
                        var genericRadius = marker as MapMarkerGenericRadius;
                        if (genericRadius != null) genericRadius.SendUpdate();
                    }
                }
            });
        }
		
        void Unload() 
        {
            LimpiarMarcadores();
            foreach (var player in BasePlayer.activePlayerList) CuiHelper.DestroyUi(player, "PetroAdminUI");
        }

        void OnEntitySpawned(BaseEntity entity)
		{
			var crate = entity as HackableLockedCrate;
			if (crate == null) return;

			foreach (var monument in _oilRigsCachados)
			{
				if (monument != null && Vector3.Distance(crate.transform.position, monument.transform.position) < 50f)
				{
					if (cajasSpawneadas.Contains(monument.transform.position)) return;

					RotarDificultades();
					Puts($"[MiPetroReinforcements] Nuevo evento detectado en {monument.name}. Dificultades rotadas.");
					break;
				}
			}
		}

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity is HackableLockedCrate)
            {
                Vector3 entityPos = entity.transform.position;
                foreach (var kvp in petroDificultades)
                {
                    Vector3 monuPos = kvp.Key.transform.position;
                    if ((entityPos - monuPos).sqrMagnitude < 2500f)
                    {
                        cajasSpawneadas.Remove(monuPos);
                        RotarDificultades();
                        Puts($"[MiPetroReinforcements] Evento finalizado en {monuPos}. Dificultades rotadas para el próximo ciclo.");
                        break; 
                    }
                }
            }
        }

        private void RotarDificultades()
        {
            petroDificultades.Clear();
            cajasSpawneadas.Clear(); 
            List<string> disponibles = new List<string>(config.Niveles.Keys);
            
            for (int i = 0; i < disponibles.Count; i++)
            {
                string temp = disponibles[i];
                int randomIndex = UnityEngine.Random.Range(0, disponibles.Count);
                disponibles[i] = disponibles[randomIndex];
                disponibles[randomIndex] = temp;
            }

            int index = 0;
            for (int i = 0; i < _oilRigsCachados.Count; i++)
            {
                MonumentInfo monument = _oilRigsCachados[i];
                if (monument != null)
                {
                    petroDificultades.Add(monument, disponibles[index % disponibles.Count]);
                    index++;
                }
            }
            
            ActualizarMarcadoresEnMapa();
        }

        private void ActualizarMarcadoresEnMapa()
        {
            LimpiarMarcadores();
            foreach (var kvp in petroDificultades)
            {
                Vector3 pos = kvp.Key.transform.position;
                string dificultad = kvp.Value;
                
                if (!config.Niveles.TryGetValue(dificultad, out DatosDificultad diffData)) continue;

                MapMarkerGenericRadius circulo = GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab", pos) as MapMarkerGenericRadius;
                if (circulo != null)
                {
                    circulo.alpha = 0.4f;
                    circulo.color2 = HexToUnityColor(diffData.MarkerColor);
                    circulo.radius = 0.4f;
                    circulo.Spawn();
                    markersActivos.Add(circulo);
                    circulo.SendUpdate();
                }

                VendingMachineMapMarker vending = GameManager.server.CreateEntity("assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab", pos) as VendingMachineMapMarker;
                if (vending != null)
                {
                    vending.markerShopName = $"PETRO [{dificultad.ToUpper()}]";
                    vending.Spawn();
                    markersActivos.Add(vending);
                }
            }
        }

        private void LimpiarMarcadores()
        {
            int count = markersActivos.Count;
            for (int i = 0; i < count; i++)
            {
                if (markersActivos[i] != null) markersActivos[i].Kill();
            }
            markersActivos.Clear();
        }

        private void OnCrateHack(HackableLockedCrate crate)
        {
            if (crate == null) return;

            MonumentInfo monumento = null;
            string dificultad = "Easy";
            float minSqrDistance = 10000f; 

            Vector3 cratePos = crate.transform.position;

            foreach (var kvp in petroDificultades)
            {
                float sqrDistance = (cratePos - kvp.Key.transform.position).sqrMagnitude;
                if (sqrDistance < minSqrDistance)
                {
                    minSqrDistance = sqrDistance;
                    dificultad = kvp.Value;
                    monumento = kvp.Key; 
                }
            }

            if (monumento == null) return;

            Puts($"[MiPetroReinforcements] Hackeo en {monumento.name}. Refuerzos en 55s.");

            timer.Once(55f, () => { 
                if (crate != null && !crate.IsDestroyed)
                    AplicarRefuerzos(monumento, dificultad);
            }); 
        }

        private void AplicarRefuerzos(MonumentInfo monumento, string dificultad)
		{
			if (!config.Niveles.TryGetValue(dificultad, out DatosDificultad datos)) return;

			Vector3 monuPos = monumento.transform.position;
			bool esPeque = monumento.name.Contains("oilrig_2", StringComparison.OrdinalIgnoreCase);
			
			Vector3 offset = esPeque ? new Vector3(37.98f, 31.52f, -10.42f) : new Vector3(27.93f, 45.13f, 4.68f);
			Vector3 posFinal = monumento.transform.TransformPoint(offset);
			
			// Spawnea la cantidad exacta de bots según la configuración (solo una vez)
			for (int i = 0; i < datos.BotsExtras; i++)
			{
				Vector3 spawnPos = posFinal + new Vector3(UnityEngine.Random.Range(-2f, 2f), 0f, UnityEngine.Random.Range(-2f, 2f));
				BaseEntity bot = GameManager.server.CreateEntity(datos.PrefabBot, spawnPos, Quaternion.identity);
				if (bot != null) bot.Spawn();
			}

			// Spawnea el Bradley solo una vez si está habilitado
			if (datos.Bradley)
			{
				timer.Once(5f, () => { 
					// Se removió la llamada recursiva a AplicarRefuerzos
					List<Vector3> rutaLocal = esPeque ? rutaPetroPequena : rutaPetroGrande;
					List<Vector3> worldPath = new List<Vector3>();
					
					int rutaCount = rutaLocal.Count;
					for (int i = 0; i < rutaCount; i++)
					{
						worldPath.Add(monumento.transform.TransformPoint(rutaLocal[i]));
					}
						
					if (worldPath.Count > 1) worldPath.Add(worldPath[0]);
					Vector3 posBradley = worldPath[0];

					var bradley = GameManager.server.CreateEntity("assets/prefabs/npc/m2bradley/bradleyapc.prefab", posBradley) as BradleyAPC;
					if (bradley != null)
					{
						bradley.currentPath = worldPath;
						bradley.Spawn();
						bradley.Invoke("TriggerPatrol", 1.0f);
					} 
				}); 
			}

			// Spawnea la caja hackeable extra
			if (datos.ExtraCrate && !cajasSpawneadas.Contains(monuPos))
			{
				string cratePrefab = "assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab";
				Vector3 localPos = esPeque ? new Vector3(27.85f, 31.51f, -9.69f) : new Vector3(14.3f, 45.13f, 5.33f);
				Vector3 localRot = esPeque ? new Vector3(0f, 84.4f, 0f) : new Vector3(0f, 87.5f, 0f);
					
				Vector3 spawnPosCrate = monumento.transform.TransformPoint(localPos);
				Quaternion spawnRotCrate = monumento.transform.rotation * Quaternion.Euler(localRot);

				BaseEntity crate = GameManager.server.CreateEntity(cratePrefab, spawnPosCrate, spawnRotCrate);
				if (crate != null)
				{
					crate.Spawn();
					var hackable = crate.GetComponent<HackableLockedCrate>();
					if (hackable != null) hackable.flags |= BaseEntity.Flags.Locked;
						
					cajasSpawneadas.Add(monuPos);
				}
			}
		}

        // =========================================================================
        // UTILIDADES DE COLOR
        // =========================================================================
        private Color HexToUnityColor(string hex)
        {
            Color color;
            if (ColorUtility.TryParseHtmlString(hex, out color)) return color;
            return Color.white;
        }

        private string HexToCuiColor(string hex, float alpha = 1.0f) {
            if (string.IsNullOrEmpty(hex)) return $"1 1 1 {alpha}"; hex = hex.Replace("#", "");
            if (hex.Length == 6) { try { float r = Convert.ToInt32(hex.Substring(0, 2), 16) / 255f; float g = Convert.ToInt32(hex.Substring(2, 2), 16) / 255f; float b = Convert.ToInt32(hex.Substring(4, 2), 16) / 255f; return $"{r} {g} {b} {alpha}"; } catch { return $"1 1 1 {alpha}"; } }
            return $"1 1 1 {alpha}";
        }

        private string GetNextItem(List<string> itemsList, string currentItem) 
        { 
            int currentIdx = itemsList.IndexOf(currentItem); 
            return itemsList[(currentIdx + 1) >= itemsList.Count ? 0 : currentIdx + 1]; 
        }

        // =========================================================================
        // SISTEMA CUI - CONFIGURACIÓN
        // =========================================================================
        [ChatCommand("petro")]
        private void CmdPetroMenu(BasePlayer player, string command, string[] args)
        {
            if (player == null || !player.IsAdmin) return;
            if (args.Length > 0 && (args[0].ToLower() == "config" || args[0].ToLower() == "menu")) 
            {
                OpenAdminMenu(player, "Easy");
            }
            else
            {
                player.ChatMessage("<color=#ffeb3b>[AYUDA]</color> <color=#ffffff>Uso: </color><color=#4dff4d>/petro config</color>");
            }
        }

        [ConsoleCommand("petro.cui")]
        private void CmdCuiAdmin(ConsoleSystem.Arg arg) 
        {
            var player = arg.Player(); if (player == null || !player.IsAdmin) return;
            string action = arg.GetString(0);
            if (action == "close") { CuiHelper.DestroyUi(player, "PetroAdminUI"); return; }
            
            string diffId = arg.GetString(1, "Easy"); 
            DatosDificultad diffSettings = null;
            if (config.Niveles.ContainsKey(diffId)) diffSettings = config.Niveles[diffId];
            
            bool isRefresh = action != "view"; 

            switch (action) {
                case "view": break; 
                case "add_bots": if (diffSettings != null) diffSettings.BotsExtras = Mathf.Clamp(diffSettings.BotsExtras + arg.GetInt(2), 0, 50); break;
                case "toggle_bradley": if (diffSettings != null) diffSettings.Bradley = !diffSettings.Bradley; break;
                case "toggle_crate": if (diffSettings != null) diffSettings.ExtraCrate = !diffSettings.ExtraCrate; break;
                case "cycle_prefab": if (diffSettings != null) diffSettings.PrefabBot = GetNextItem(availablePrefabs, diffSettings.PrefabBot); break;
                case "save": 
                    SaveConfig(); 
                    player.ChatMessage("<color=#4dff4d>[Petro]</color> Configuración guardada correctamente."); 
                    ActualizarMarcadoresEnMapa(); // Refresca colores en tiempo real
                    break;
            }
            OpenAdminMenu(player, diffId, isRefresh); 
        }

        private void OpenAdminMenu(BasePlayer player, string diffId, bool isRefresh = false)
        {
            if (!isRefresh) {
                CuiHelper.DestroyUi(player, "PetroAdminUI"); var baseContainer = new CuiElementContainer();
                baseContainer.Add(new CuiPanel { Image = { Color = "0.08 0.08 0.09 0.98" }, RectTransform = { AnchorMin = "0.15 0.15", AnchorMax = "0.85 0.85" }, CursorEnabled = true }, "Overlay", "PetroAdminUI");
                baseContainer.Add(new CuiElement { Parent = "PetroAdminUI", Components = { new CuiTextComponent { Text = "⚙⬤⬤ EVENTO PETRO - CONFIGURACIÓN ⬤⬤⚙", FontSize = 20, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", Color = "#e5a93c" }, new CuiOutlineComponent { Distance = "1 -1", Color = "0 0 0 0.8" }, new CuiRectTransformComponent { AnchorMin = "0 0.9", AnchorMax = "1 1" } } });
                baseContainer.Add(new CuiButton { Button = { Command = "petro.cui close", Color = "0.70 0.22 0.22 1" }, RectTransform = { AnchorMin = "0.95 0.91", AnchorMax = "0.98 0.97" }, Text = { Text = "X", FontSize = 14, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" } }, "PetroAdminUI");
                baseContainer.Add(new CuiButton { 
					Button = { Command = $"petro.cui save {diffId}", Color = "0.18 0.58 0.32 1" }, 
					RectTransform = { AnchorMin = "0.80 0.91", AnchorMax = "0.94 0.97" },  
					Text = { Text = "GUARDAR", FontSize = 11, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" } 
				}, "PetroAdminUI");
				baseContainer.Add(new CuiPanel { Image = { Color = "0.05 0.05 0.06 1" }, RectTransform = { AnchorMin = "0.02 0.05", AnchorMax = "0.25 0.88" } }, "PetroAdminUI", "PetroAdminUI_Left");
                
                float currentY = 0.82f; float btnHeight = 0.10f; 
                
                foreach (var kvp in config.Niveles) {
                    string id = kvp.Key; string name = kvp.Key.ToUpper(); string diffColorRgba = HexToCuiColor(kvp.Value.MarkerColor); bool isSelected = (id == diffId);
                    string finalBgColor = isSelected ? diffColorRgba : "0.13 0.13 0.15 1"; string finalTextColor = isSelected ? "1 1 1 1" : diffColorRgba; float yMin = currentY; float yMax = yMin + btnHeight;
                    baseContainer.Add(new CuiButton { Button = { Command = $"petro.cui view {id}", Color = finalBgColor }, RectTransform = { AnchorMin = $"0.1 {yMin}", AnchorMax = $"0.9 {yMax}" }, Text = { Text = name, FontSize = 12, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", Color = finalTextColor } }, "PetroAdminUI_Left"); currentY -= (btnHeight + 0.015f);
                }
                baseContainer.Add(new CuiPanel { Image = { Color = "0.06 0.06 0.07 1" }, RectTransform = { AnchorMin = "0.27 0.05", AnchorMax = "0.98 0.88" } }, "PetroAdminUI", "PetroAdminUI_Right");
                CuiHelper.AddUi(player, baseContainer);
            }

            string contentPanelName = "PetroAdminUI_Content"; CuiHelper.DestroyUi(player, contentPanelName);
            var contentContainer = new CuiElementContainer(); contentContainer.Add(new CuiPanel { Image = { Color = "0 0 0 0" }, RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" } }, "PetroAdminUI_Right", contentPanelName);

            int rowIndex = 0; float rowSpacing = 0.085f;
            Action<string, string, string, string, string> DrawToggleRow = (label, valText, valColor, cmdToggle, desc) => { float yMax = 0.96f - (rowIndex * rowSpacing); float yMin = yMax - 0.07f; contentContainer.Add(new CuiElement { Parent = contentPanelName, Components = { new CuiTextComponent { Text = label, FontSize = 13, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-bold.ttf" }, new CuiOutlineComponent { Distance = "1 -1", Color = "0 0 0 0.6" }, new CuiRectTransformComponent { AnchorMin = $"0.04 {yMin}", AnchorMax = $"0.3 {yMax}" } } }); contentContainer.Add(new CuiButton { Button = { Command = cmdToggle, Color = valColor }, RectTransform = { AnchorMin = $"0.3 {yMin}", AnchorMax = $"0.45 {yMax}" }, Text = { Text = valText, FontSize = 11, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" } }, contentPanelName); contentContainer.Add(new CuiElement { Parent = contentPanelName, Components = { new CuiTextComponent { Text = desc, FontSize = 11, Color = "0.65 0.65 0.65 1", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf" }, new CuiOutlineComponent { Distance = "1 -1", Color = "0 0 0 0.4" }, new CuiRectTransformComponent { AnchorMin = $"0.48 {yMin}", AnchorMax = $"0.95 {yMax}" } } }); rowIndex++; };
            Action<string, string, string, string> DrawNumberRow = (label, valText, cmdMinus, cmdPlus) => { float yMax = 0.96f - (rowIndex * rowSpacing); float yMin = yMax - 0.07f; contentContainer.Add(new CuiElement { Parent = contentPanelName, Components = { new CuiTextComponent { Text = label, FontSize = 13, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-bold.ttf" }, new CuiOutlineComponent { Distance = "1 -1", Color = "0 0 0 0.6" }, new CuiRectTransformComponent { AnchorMin = $"0.04 {yMin}", AnchorMax = $"0.3 {yMax}" } } }); contentContainer.Add(new CuiButton { Button = { Command = cmdMinus, Color = "0.65 0.25 0.25 1" }, RectTransform = { AnchorMin = $"0.3 {yMin}", AnchorMax = $"0.35 {yMax}" }, Text = { Text = "-", FontSize = 14, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" } }, contentPanelName); string valBg = "valBg_" + rowIndex; contentContainer.Add(new CuiPanel { Image = { Color = "0.11 0.11 0.13 1" }, RectTransform = { AnchorMin = $"0.36 {yMin}", AnchorMax = $"0.44 {yMax}" } }, contentPanelName, valBg); contentContainer.Add(new CuiElement { Parent = valBg, Components = { new CuiTextComponent { Text = valText, FontSize = 12, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", Color = "#ffeb3b" }, new CuiOutlineComponent { Distance = "1 -1", Color = "0 0 0 0.5" }, new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" } } }); contentContainer.Add(new CuiButton { Button = { Command = cmdPlus, Color = "0.25 0.55 0.25 1" }, RectTransform = { AnchorMin = $"0.45 {yMin}", AnchorMax = $"0.5 {yMax}" }, Text = { Text = "+", FontSize = 14, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" } }, contentPanelName); rowIndex++; };

            if (config.Niveles.ContainsKey(diffId)) {
				var diff = config.Niveles[diffId];
				DrawToggleRow("Tanque Bradley:", diff.Bradley ? "HABILITADO" : "DESHABILITADO", diff.Bradley ? "0.20 0.55 0.30 1" : "0.65 0.25 0.25 1", $"petro.cui toggle_bradley {diffId}", "Si se habilita, patrullará las rutas fijas del Monumento."); 
				DrawToggleRow("Caja Hackeable Extra:", diff.ExtraCrate ? "HABILITADO" : "DESHABILITADO", diff.ExtraCrate ? "0.20 0.55 0.30 1" : "0.65 0.25 0.25 1", $"petro.cui toggle_crate {diffId}", "Genera una segunda caja Hack."); 
				DrawNumberRow("Bots Adicionales:", diff.BotsExtras.ToString(), $"petro.cui add_bots {diffId} -1", $"petro.cui add_bots {diffId} 1"); 

                float yMaxPrefab = 0.96f - (rowIndex * rowSpacing); float yMinPrefab = yMaxPrefab - 0.07f;
				contentContainer.Add(new CuiElement { Parent = contentPanelName, Components = { new CuiTextComponent { Text = "Tipo de Científico:", FontSize = 13, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-bold.ttf" }, new CuiOutlineComponent { Distance = "1 -1", Color = "0 0 0 0.6" }, new CuiRectTransformComponent { AnchorMin = $"0.04 {yMinPrefab}", AnchorMax = $"0.3 {yMaxPrefab}" } } });
                
                string prefabDisplay = diff.PrefabBot.Split('/').Last().Replace(".prefab", "").ToUpper();
				contentContainer.Add(new CuiButton { Button = { Command = $"petro.cui cycle_prefab {diffId}", Color = "0.22 0.35 0.55 1" }, RectTransform = { AnchorMin = $"0.3 {yMinPrefab}", AnchorMax = $"0.85 {yMaxPrefab}" }, Text = { Text = prefabDisplay, FontSize = 11, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" } }, contentPanelName);
				rowIndex++;
			}

            CuiHelper.AddUi(player, contentContainer);
        } 
    }
}