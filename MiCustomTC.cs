using Oxide.Game.Rust.Cui;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using HarmonyLib;
using System.Reflection;
using System;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("MiCustomTC", "☠ Ⓜ🅘ⓖ🅤ⓔ🅛 ☠", "12.0.8")]
    class MiCustomTC : RustPlugin
    {
		[PluginReference]
		private Plugin ImageLibrary;
		public static bool decayext = false;
		private float cooldownHammer = 2.5f;
		private float cooldownUpdateBuild = 0.5f;
		private int intTanda = 10;
		
		private const string PermisoAmigos = "micustomtc.amigos";
		private const string PermisoUso = "micustomtc.use";
        private const string SN_MADERA = "wood";
        private const string SN_PIEDRA = "stones";
        private const string SN_METAL = "metal.fragments";
        private const string SN_HQM = "metal.refined";
		private const string SN_TELA = "cloth";
        private const int ID_RODILLO = 1629564540;
        private const ulong SKIN_CONTAINER = 10221;
		private const string SN_MARTILLO = "hammer";
		private Dictionary<ulong, Dictionary<int, ulong>> preferenciasSkins = new Dictionary<ulong, Dictionary<int, ulong>>();
		private string cacheMenuPrincipal = string.Empty;
        //private Dictionary<ulong, Timer> monitoresUI = new Dictionary<ulong, Timer>();
		private int[] itemIdsCache = new int[7];
		private readonly List<ulong> SkinsWallpaper = new List<ulong> { 
			3533776322, 3504835539, 3490858948, 3504824961, 3504766453, 3356317715, 3354244276, 3504823018, 
			3504820819, 3504818484, 3504816339, 3504764072, 3494348312, 3667006560, 3605218299, 3687900256, 3655561340, 3586137053, 3565743014, 3581160987, 3630888973, 
			3630898473, 3631610171, 3650013267, 3619929162, 3585686818, 3585535934, 3585535514, 3585534695, 3581164047, 3581153642, 3494326113, 3502068828, 3585529666,
			3554384069, 3648505624, 3585536418, 3507213963, 3506198551, 3502067465, 3494247277, 3494243670, 3648718022, 3494331152, 3494250152, 3494249289, 3494251677, 
			3494245871, 3603575973, 3693542292, 3667693761, 3667592235, 3694418033, 3507926680, 3647121992, 3723732880, 3659386071, 3655559199, 3641634586, 3638686766
			
			
			
			//3378300435, 3378292526, 3449141896, 3449133412, 3416482039, 3382785060, 3373460705, 3496093974, 3453875949, 3452630258, 3495262812, 3499275252, 3327641853, 3358013862, 3420667975, 3410069980, 3315857753, 3354260838, 3316086605, 3494402060, 3347208168, 3358012663, 3353131011, 3410072749, 3364118939, 3386941486, 3494249723, 3494247704, 3386942948, 3364151267, 3364120684, 3364119422, 3363427834, 3449144606, 3386337703, 3453873723, 3452635382, 3449141896, 3449134446, 3449131847, 3449148265, 3410962122, 3413270461, 3406860212, 3406855135, 3408787790, 3471932273, 10242, 10243, 10246,
			// Desactivadas
		};

		private readonly List<ulong> SkinsSuelo = new List<ulong> { 
			3488989152, 3612401817, 3609410737, 3536179251, 3612442626, 3612385488, 3515663186, 3497436292, 3495622788, 3648630794, 3504401940, 3565195753, 3564715606, 
			3495673853, 3490884961, 3586137712, 3505357378, 3539895972, 3683065793, 3597561712, 3698520219, 3616721101, 3588597109, 3712130855, 3709270212, 3494102293, 3504689720, 3554598937, 3593679701, 3495623743, 3551579695, 3616733803, 3496272072, 3574812090, 3554637771, 3504718416, 3504684886, 
			3658949698, 3633939281, 3633951325, 3616957640, 3567397518, 3567387398, 3551569615, 3551567358, 3550968653, 3550965458, 3551577457, 3551575286, 3575259635 
			// Desactivadas
			//3633930586
		}; 

		private readonly List<ulong> SkinsTecho = new List<ulong> { 
			3495645149, 3536163911, 3495632162, 3495631196, 3575299620, 3493884033, 3491842568, 3593680010, 3595917595, 3564183047, 3494099849, 3564182431, 3562767441, 3586138061, 3574811116, 3554636782, 3505354220, 3551579135, 3551576556, 3494102455, 3678917020, 3575261534, 3683050416, 3658950577, 3633951917, 3633947362, 3633938499, 3564181616, 3564179614, 3551568744, 3551566647, 3550969422, 3550966753, 3496049170, 3633953313, 3551574214, 3496057466, 3604217491, 3493912545, 3495630765, 3565366241, 3556150438, 3505221207, 3567952894, 3567946950, 3564183387, 3495803510, 3553348389, 3645456487, 3567098152, 3556151238, 3556151065, 3565358888, 3556150661, 3554674736, 3496851248, 3683059177, 3683052763, 3574808774, 3564178997, 3683055149, 3496056607, 3496053480, 3496050429
			// Desactivadas 
		};
        private readonly Dictionary<int, Dictionary<string, ulong>> SkinsDisponibles = new Dictionary<int, Dictionary<string, ulong>>
        {
            { 1, new Dictionary<string, ulong> { { "NORMAL", 0 }, { "FRONTIER", 10232 }, { "Gingerbread", 2 } }},
            { 2, new Dictionary<string, ulong> { { "NORMAL", 0 }, { "ADOBE", 10220 }, { "BRICK", 10223 }, { "BRUTALIST", 10225 }, { "JUMGLE", 10326 }, { "CRYPT", 10472 } }},
            { 3, new Dictionary<string, ulong> { { "NORMAL", 0 }, { "CONTAINER", SKIN_CONTAINER } }},
            { 4, new Dictionary<string, ulong> { { "NORMAL", 0 }, { "SPACE", 10430 } }}
        };
		private int GetCostReskin(int grado) 
		{
			int costeMejora = GetCost(grado);
			return Mathf.CeilToInt(costeMejora * 0.5f); 
		}

        private readonly Dictionary<string, uint> ColoresContainer = new Dictionary<string, uint>
		{
			{ "AZUL", 1 }, { "VERDE", 2 }, { "MORADO", 3 }, { "MARRÓN", 4 }, { "NARANJA", 5 }, { "BLANCO", 6 }, { "NEGRO", 7 }, { "MARRÓN CLARO", 8 }, { "AZUL OSCURO", 9 },
			{ "VERDE OLIVA", 10 }, { "ROJO", 11 }, { "BEIGE", 12 }, { "AMARILLO", 13 }, { "GRIS", 14 }, { "VERDE AZULADO", 15 }, { "GRIS CLARO", 16 }, { "ALEATORIO", 0 } 
		};
		private string GetColorRGBA(string name)
		{
			switch (name)
			{
				case "AZUL": return "0.14 0.25 0.50 0.9"; case "VERDE": return "0.24 0.35 0.14 0.9"; case "MORADO": return "0.33 0.18 0.45 0.9";
				case "MARRÓN": return "0.35 0.22 0.15 0.9"; case "NARANJA": return "0.75 0.35 0.10 0.9"; case "BLANCO": return "0.85 0.85 0.85 0.9";
				case "NEGRO": return "0.05 0.05 0.05 0.9"; case "MARRÓN CLARO": return "0.50 0.35 0.25 0.9"; case "AZUL OSCURO": return "0.05 0.10 0.25 0.9";
				case "VERDE OLIVA": return "0.25 0.28 0.15 0.9"; case "ROJO": return "0.65 0.10 0.10 0.9"; case "BEIGE": return "0.75 0.65 0.45 0.9";
				case "AMARILLO": return "0.80 0.65 0.10 0.9"; case "GRIS": return "0.35 0.35 0.35 0.9"; case "VERDE AZULADO": return "0.10 0.45 0.40 0.9";
				case "GRIS CLARO": return "0.55 0.55 0.55 0.9"; case "ALEATORIO": return "0.40 0.40 0.40 0.8"; default: return "0.2 0.2 0.2 0.9";
			}
		}
		
		private Dictionary<ulong, MejoraData> estadoMejoras = new Dictionary<ulong, MejoraData>();

		class MejoraData
		{
			public int Total;
			public int Restantes;
			public string Material;
			public string Coste;
		}

		private string MenuProgreso = "MenuProgresoMejora";
		private string MenuColores = "MenuColores";
		private string MenuAlertaMejora = "MenuAlertaMejora";
		private string MenuMejoraTC = "MenuMejoraTC";
		private string SubMenuSkins = "SubMenuSkins";
		private string MenuSkinsMaster = "MenuSkinsMaster";
		
        void Init()
		{
			permission.RegisterPermission(PermisoUso, this);
			permission.RegisterPermission(PermisoAmigos, this);
			var harmony = new HarmonyLib.Harmony(this.Name);
            harmony.PatchAll();
			itemIdsCache[1] = ItemManager.FindItemDefinition(SN_MADERA)?.itemid ?? 0;
			itemIdsCache[2] = ItemManager.FindItemDefinition(SN_PIEDRA)?.itemid ?? 0;
			itemIdsCache[3] = ItemManager.FindItemDefinition(SN_METAL)?.itemid ?? 0;
			itemIdsCache[4] = ItemManager.FindItemDefinition(SN_HQM)?.itemid ?? 0;
			itemIdsCache[5] = ItemManager.FindItemDefinition(SN_TELA)?.itemid ?? 0;
			itemIdsCache[6] = ItemManager.FindItemDefinition(SN_MARTILLO)?.itemid ?? 0;
		}

        #region UI y Menús
		void MenuPrincipal(BasePlayer player)
		{
			CerrarTodoUI(player);
			CuiHelper.DestroyUi(player, MenuAlertaMejora);
			
			BuildingPrivlidge tc = null;
			if (tcAbierto.TryGetValue(player.userID, out uint tcIDGuardado))
			{
				tc = BaseNetworkable.serverEntities.Find(new NetworkableId(tcIDGuardado)) as BuildingPrivlidge;
			}
			if (tc == null) return; 

			uint tcID = (uint)tc.net.ID.Value;
			
			var elements = new CuiElementContainer();
			
			elements.Add(new CuiPanel { 
				Image = { Color = "0.02 0.02 0.02 0.96" }, 
				RectTransform = { AnchorMin = "0.15 0.84", AnchorMax = "0.85 0.94" },
				CursorEnabled = true
			}, "Overlay", MenuMejoraTC);

			elements.Add(new CuiPanel {
				Image = { Color = "0.15 0.15 0.15 1.0" },
				RectTransform = { AnchorMin = "0.0 0.94", AnchorMax = "1.0 1.0" }
			}, MenuMejoraTC);

			elements.Add(new CuiLabel {
				Text = { Text = "GESTIÓN ESTRUCTURAL", FontSize = 9, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", Color = "0.5 0.5 0.5 1" },
				RectTransform = { AnchorMin = "0.0 0.94", AnchorMax = "1.0 1.0" }
			}, MenuMejoraTC);

			if (procesosActivos.ContainsKey(tcID))
			{
				elements.Add(new CuiButton
				{
					Button = { Command = "mejorar.cancelar", Color = "0.8 0.2 0.2 0.9" },
					RectTransform = { AnchorMin = "0.01 0.08", AnchorMax = "0.965 0.88" },
					Text = { Text = "⚠ DETENER MEJORA EN CURSO ⚠", FontSize = 14, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" }
				}, MenuMejoraTC);
			}
			else
			{
				var skinsEnBase = ObtenerSkinsEnUso(tc); 
				
				if (!materialesExcluidos.TryGetValue(player.userID, out HashSet<int> playerExcluidos))
				{
					playerExcluidos = new HashSet<int>();
					materialesExcluidos[player.userID] = playerExcluidos;
				}
					
				var infoBotones = new[] {
					new { Label = "MADERA", Grado = 1, Color = "0.36 0.26 0.19 0.8" },
					new { Label = "PIEDRA", Grado = 2, Color = "0.45 0.45 0.45 0.8" },
					new { Label = "METAL",  Grado = 3, Color = "0.35 0.40 0.45 0.8" },
					new { Label = "HQM",    Grado = 4, Color = "0.75 0.55 0.15 0.8" }
				};

				float startX = 0.01f; 
				float spacing = 0.005f;
				float ancho = (0.52f - (spacing * 4)) / 4;
				
				int count = infoBotones.Length; 
				
				for (int i = 0; i < count; i++)
				{
					var btn = infoBotones[i];

					bool estaExcluido = playerExcluidos.Contains(btn.Grado);
					bool canClick = PuedeAbrirMenu(tc, btn.Grado);
					bool materialEnBase = skinsEnBase[btn.Grado].Count > 0;
					
					string colorCard = estaExcluido ? "0.05 0.05 0.05 0.8" : (canClick ? "0.12 0.12 0.12 1.0" : "0.05 0.05 0.05 0.5");
					string comando = (canClick && !estaExcluido) ? $"mejorar.abrirmenu {btn.Grado} {tcID}" : "";

					string btnName = elements.Add(new CuiButton { 
						Button = { Command = comando, Color = colorCard }, 
						RectTransform = { AnchorMin = $"{startX} 0.35", AnchorMax = $"{startX + ancho} 0.88" },
						Text = { Text = "" } 
					}, MenuMejoraTC);

					int realID = itemIdsCache[btn.Grado];
					if (realID != 0) {
						elements.Add(new CuiElement { 
							Parent = btnName,
							Components = {
								new CuiImageComponent { ItemId = realID, Color = (canClick && !estaExcluido) ? "1 1 1 1" : "1 1 1 0.1" }, 
								new CuiRectTransformComponent { AnchorMin = "0.05 0.15", AnchorMax = "0.35 0.85" }
							}
						});
					}

					elements.Add(new CuiLabel {
						Text = { 
							Text = btn.Label, 
							FontSize = 10, 
							Align = TextAnchor.MiddleLeft, 
							Font = "robotocondensed-bold.ttf", 
							Color = (canClick && !estaExcluido) ? "0.8 0.8 0.8 1" : "0.3 0.3 0.3 0.8" 
						},
						RectTransform = { AnchorMin = "0.40 0", AnchorMax = "1 1" }
					}, btnName);

					if (materialEnBase && !estaExcluido)
					{
						elements.Add(new CuiLabel {
							Text = { Text = "●", FontSize = 7, Align = TextAnchor.MiddleCenter, Color = "0.4 0.9 0.4 0.5" },
							RectTransform = { AnchorMin = "0.88 0.70", AnchorMax = "0.98 0.95" }
						}, btnName);
					}

					if (btn.Grado != 4)
					{
						string colorExcluir = estaExcluido ? "0.5 0.1 0.1 0.9" : "0.18 0.18 0.18 0.7";
						string textoExcluir = estaExcluido ? "<color=#ff9999>EXCLUIDO</color>" : "EXCLUIR";

						elements.Add(new CuiButton {
							Button = { Command = $"mejorar.alternarexcluir {btn.Grado}", Color = colorExcluir },
							RectTransform = { AnchorMin = $"{startX} 0.08", AnchorMax = $"{startX + ancho} 0.30" },
							Text = { Text = textoExcluir, FontSize = 7, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" }
						}, MenuMejoraTC);
					}

					startX += ancho + spacing;
				}

				float martilloX = startX + 0.01f;
				float martilloAncho = 0.12f;
				
				string btnReparar = elements.Add(new CuiButton { 
					Button = { Command = $"mejorar.reparar.todo  {tcID}", Color = "0.25 0.35 0.25 0.9" }, 
					RectTransform = { AnchorMin = $"{martilloX} 0.35", AnchorMax = $"{martilloX + martilloAncho} 0.88" },
					Text = { Text = "" } 
				}, MenuMejoraTC);

				int martilloID = itemIdsCache[6]; 
				elements.Add(new CuiElement { 
					Parent = btnReparar,
					Components = {
						new CuiImageComponent { ItemId = martilloID, Color = "1 1 1 1" }, 
						new CuiRectTransformComponent { AnchorMin = "0.05 0.15", AnchorMax = "0.35 0.85" }
					} 
				});

				elements.Add(new CuiLabel {
					Text = { Text = "REPARAR", FontSize = 10, Align = TextAnchor.MiddleLeft, Font = "robotocondensed-bold.ttf", Color = "0.8 0.8 0.8 1" },
					RectTransform = { AnchorMin = "0.40 0", AnchorMax = "1 1" }
				}, btnReparar); 

				float papelX = martilloX + martilloAncho + 0.02f;
				float papelAncho = (0.30f - (spacing * 2)) / 3;
				
				var papeles = new[] {
					new { Label = "PARED", Cmd = "0", Color = "0.2 0.3 0.5 0.9" },
					new { Label = "SUELO", Cmd = "2", Color = "0.2 0.5 0.3 0.9" },
					new { Label = "TECHO", Cmd = "3", Color = "0.5 0.2 0.2 0.9" }
				};

				int countPapeles = papeles.Length; 
				for (int i = 0; i < countPapeles; i++)
				{
					var p = papeles[i];

					string pBtn = elements.Add(new CuiButton { 
						Button = { Command = $"mejorar.menupapel {p.Cmd}", Color = "0.12 0.12 0.12 1.0" }, 
						RectTransform = { AnchorMin = $"{papelX} 0.08", AnchorMax = $"{papelX + papelAncho} 0.88" },
						Text = { Text = "" }
					}, MenuMejoraTC);

					elements.Add(new CuiLabel {
						Text = { Text = p.Label, FontSize = 8, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", Color = "0.6 0.6 0.6 1" },
						RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
					}, pBtn);

					elements.Add(new CuiPanel {
						Image = { Color = p.Color },
						RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.06" }
					}, pBtn);

					papelX += papelAncho + spacing;
				}
			}

			elements.Add(new CuiButton {
				Button = { Command = "micustomtc.cerrar", Color = "0.7 0.2 0.2 1.0" }, 
				RectTransform = { 
					AnchorMin = "0.975 0.84", 
					AnchorMax = "0.995 0.98"  
				},
				Text = { 
					Text = "×", 
					FontSize = 10, 
					Align = TextAnchor.MiddleCenter, 
					Color = "1 1 1 1", 
					Font = "robotocondensed-bold.ttf" 
				}
			}, MenuMejoraTC);
			
			CuiHelper.AddUi(player, elements);
		}
		private Dictionary<ulong, uint> tcAbierto = new Dictionary<ulong, uint>();
		void OnLootEntity(BasePlayer player, BaseEntity entity)
		{
			if (player == null || entity == null) return;

			if (entity is BuildingPrivlidge tc)
			{
				if (!permission.UserHasPermission(player.UserIDString, PermisoUso)) return;
				tcAbierto[player.userID] = (uint)tc.net.ID.Value;
				MenuPrincipal(player);
			}
		}
		
		void OnServerInitialized()
		{
			if (ImageLibrary == null)
			{
				PrintError("ImageLibrary no está cargado. Las imágenes no funcionarán.");
				return;
			}

			CargarListaEnImageLibrary(SkinsWallpaper, "wallpaper");
			CargarListaEnImageLibrary(SkinsSuelo, "wallpaper");
			CargarListaEnImageLibrary(SkinsTecho, "wallpaper");
			RefreshSkinsCache();
		}

		private void CargarListaEnImageLibrary(List<ulong> listaSkins, string prefijo)
        {
			if (listaSkins == null) return;
			
			int count = listaSkins.Count;
			for (int i = 0; i < count; i++)
			{
				ulong skinId = listaSkins[i];
				string nombreImagen = $"{prefijo}_{skinId}";

				if (skinId < 1000000)
				{
					string url = $"https://raw.githubusercontent.com/bmgjet/WallPaperIcons/main/{skinId}.png";
					ImageLibrary.Call("AddImage", url, nombreImagen, skinId);
				}
				else
				{
					ImageLibrary.Call("AddImage", "", nombreImagen, skinId);
				}
			}
        }

		private Dictionary<ulong, string> _nombreImagenCache = new Dictionary<ulong, string>();

		private string ObtenerContenidoImagen(ulong skinId)
        {
			if (!_nombreImagenCache.TryGetValue(skinId, out string nombreImagen))
			{
				nombreImagen = $"wallpaper_{skinId}";
				_nombreImagenCache[skinId] = nombreImagen;
			}

			if (ImageLibrary != null)
			{
				object result = ImageLibrary.Call("GetImage", nombreImagen);
				if (result != null && (string)result != "0")
				{
					return (string)result;
				}
			}
			
			if (skinId < 1000000)
			{
				return $"https://raw.githubusercontent.com/bmgjet/WallPaperIcons/main/{skinId}.png";
			}
			
			return skinId.ToString();
        } 
		
		[ConsoleCommand("papel.aplicar.individual")]
		void CmdAplicarIndividual(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null) return;

			ulong skinID = arg.GetUInt64(0);
			int tipoSuperficie = arg.GetInt(1); 

			if (!preferenciasSkins.ContainsKey(player.userID))
				preferenciasSkins[player.userID] = new Dictionary<int, ulong>();
			
			preferenciasSkins[player.userID][tipoSuperficie] = skinID;
			Item rodillo = player.inventory.containerBelt.FindItemByItemID(ID_RODILLO) ?? player.inventory.containerMain.FindItemByItemID(ID_RODILLO);

			if (rodillo != null)
			{
				ConfigurarRodilloInteligente(player, rodillo);
			}
			else
			{
				rodillo = ItemManager.CreateByItemID(ID_RODILLO, 1);
				if (rodillo != null)
				{
					rodillo.name = "RODILLO MAESTRO";
					ConfigurarRodilloInteligente(player, rodillo);
					if (!player.inventory.GiveItem(rodillo))
					{
						rodillo.Remove();
						return;
					}
				}
			}

			timer.Once(0.1f, () => {
				if (player != null && rodillo != null && rodillo.uid.Value != 0)
				{
					player.Command("inventory.selectitem", rodillo.uid.Value);
				}
			});

			player.ChatMessage("<color=#FF0000><MiCustomTC></color>  <color=#75ccff>¡Diseño seleccionado y equipado!</color>");
			CerrarTodoUI(player);
		}
		
		void ConfigurarRodilloInteligente(BasePlayer player, Item item)
		{
			var held = item.GetHeldEntity() as WallpaperPlanner;
			if (held == null) return;

			if (preferenciasSkins.TryGetValue(player.userID, out var misSkins))
			{
				if (misSkins.TryGetValue(0, out ulong sPared)) held.wallSkinID = sPared;
				if (misSkins.TryGetValue(2, out ulong sSuelo)) held.flooringSkinID = sSuelo;
				if (misSkins.TryGetValue(3, out ulong sTecho)) held.ceilingSkinID = sTecho;
				held.skinID = misSkins.Values.LastOrDefault(); 
				held.SendNetworkUpdate();
			}
		}
		
		[ConsoleCommand("mejorar.reparar.todo")]
		void CmdRepararTodo(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null) return;

			uint tcID = (uint)arg.GetUInt(0); 
			BuildingPrivlidge tc = BaseNetworkable.serverEntities.Find(new NetworkableId(tcID)) as BuildingPrivlidge;
			
			if (tc == null || !tc.IsAuthed(player)) // Añadimos la comprobación de autorización
			{
				player.ChatMessage("<color=#FF0000><MiCustomTC></color> No tienes acceso a este armario.");
				return;
			}

			var building = tc.GetBuilding();
			if (building == null) return;

			List<BuildingBlock> bloquesAReparar = Facepunch.Pool.Get<List<BuildingBlock>>();
			
			if (building?.buildingBlocks != null)
			{
				var blocks = building.buildingBlocks.Values;
				int count = blocks.Count;

				for (int i = 0; i < count; i++)
				{
					var b = blocks[i];
					
					if (b != null && !b.IsDestroyed && b.health < b.MaxHealth())
					{
						bloquesAReparar.Add(b);
					}
				}
			}

			if (bloquesAReparar.Count == 0)
			{
				CuiHelper.DestroyUi(player, MenuProgreso);
				player.ChatMessage("<color=#00FF00><MiCustomTC></color> Todo está reparado.");
				Facepunch.Pool.FreeUnmanaged(ref bloquesAReparar);
				return;
			}

			if (procesosActivos.ContainsKey(tcID)) { procesosActivos[tcID].Value?.Destroy(); procesosActivos.Remove(tcID); }

			int indice = 0;
			int total = bloquesAReparar.Count;

			procesosActivos[tcID] = new KeyValuePair<ulong, Timer>(player.userID, timer.Repeat(0.75f, total, () => 
			{
				if (player == null || tc == null || tc.IsDestroyed || tc.inventory == null || bloquesAReparar == null)
				{
					FinalizarProcesoTC(tcID);
					if (bloquesAReparar != null) { Facepunch.Pool.FreeUnmanaged(ref bloquesAReparar); bloquesAReparar = null; }
					return;
				}

				float tiempoEspera;
				if (RecibioDaño(tc, out tiempoEspera))
				{
					PrintToChat(player, $"<color=red>Reparación interrumpida:</color> Daño reciente detectado.");
					FinalizarProcesoTC(tcID);
					CuiHelper.DestroyUi(player, MenuProgreso);
					
					Facepunch.Pool.FreeUnmanaged(ref bloquesAReparar);
					bloquesAReparar = null;
					return;
				}

				if (indice < total)
				{
					var block = bloquesAReparar[indice];
					if (block != null && !block.IsDestroyed && block.health < block.MaxHealth())
					{ 
						float missingFraction = 1f - (block.health / block.MaxHealth());
						var costesTotales = block.RepairCost(missingFraction);

						if (costesTotales != null && costesTotales.Count > 0)
						{
							float porcentajePosible = 1f;

							int countCostes = costesTotales != null ? costesTotales.Count : 0; 
							for (int i = 0; i < countCostes; i++)
							{
								var item = costesTotales[i];
								if (item == null) continue;

								int disponible = tc.inventory.GetAmount(item.itemid, false);
								if (disponible < item.amount)
								{
									float ratio = (float)disponible / item.amount;
									if (ratio < porcentajePosible) 
									{
										porcentajePosible = ratio;
									}
								}

								if (porcentajePosible <= 0f)
								{
									porcentajePosible = 0f;
									break; 
								}
							}
							
							if (porcentajePosible <= 0.01f)
							{
								player.ChatMessage("<color=#FF0000><MiCustomTC></color> Reparación cancelada: Sin materiales.");
								FinalizarProcesoTC(tcID);
								CuiHelper.DestroyUi(player, MenuProgreso);
								
								Facepunch.Pool.FreeUnmanaged(ref bloquesAReparar);
								bloquesAReparar = null;
								return;
							}
							
							for (int i = 0; i < countCostes; i++)
							{
								var item = costesTotales[i];
								if (item == null) continue;

								int cantidadACobrar = Mathf.CeilToInt(item.amount * porcentajePosible);
								if (cantidadACobrar > 0)
								{
									tc.inventory.Take(null, item.itemid, cantidadACobrar);
								}
							}

							Effect.server.Run("assets/bundled/prefabs/fx/build/repair.prefab", block.transform.position);
							block.health = Mathf.Min(block.MaxHealth(), block.health + ((block.MaxHealth() - block.health) * porcentajePosible));
							block.SendNetworkUpdate();
						}
					}
					indice++;
				} 

				if (indice >= total)
				{
					player.ChatMessage("<color=#00FF00><MiCustomTC></color> Reparación finalizada.");
					FinalizarProcesoTC(tcID);
					CuiHelper.DestroyUi(player, MenuProgreso);
					
					if (bloquesAReparar != null) { Facepunch.Pool.FreeUnmanaged(ref bloquesAReparar); bloquesAReparar = null; }
				}
				else
				{
					UI_ProgresoMejora(player, indice, total, "REPARANDO", "USANDO RECURSOS TC");
				} 
			}));
			CerrarTodoUI(player);
		}
		
		private Dictionary<ulong, HashSet<ulong>> skinsExcluidas = new Dictionary<ulong, HashSet<ulong>>();
		[ConsoleCommand("mejorar.alternarexcluirskin")]
		void CmdAlternarExcluirSkin(ConsoleSystem.Arg arg)
		{
			var player = arg.Player(); if (player == null) return;
			int grado = arg.GetInt(0);
			ulong skinID = arg.GetUInt64(1);

			if (!skinsExcluidas.ContainsKey(player.userID))
				skinsExcluidas[player.userID] = new HashSet<ulong>();

			ulong claveUnica = (ulong)(grado * 1000000) + skinID;

			if (skinsExcluidas[player.userID].Contains(claveUnica))
				skinsExcluidas[player.userID].Remove(claveUnica);
			else
				skinsExcluidas[player.userID].Add(claveUnica);
			if (tcAbierto.TryGetValue(player.userID, out uint tcID))
			{
				// Reconstruimos los argumentos correctamente: Grado y ID del TC
				//arg.Args = new string[] { grado.ToString(), tcID.ToString() };
				arg.Args = new Facepunch.StringView[] { grado.ToString(), tcID.ToString() };
				CmdAbrirMenuSkins(arg);
			}
		}

		[ConsoleCommand("mejorar.abrirmenu")]
		void CmdAbrirMenuSkins(ConsoleSystem.Arg arg)
		{
			var player = arg.Player(); 
			if (player == null) return;
			
			int grado = arg.GetInt(0);
			uint tcID = (uint)arg.GetUInt(1); 
    
			BuildingPrivlidge tc = BaseNetworkable.serverEntities.Find(new NetworkableId(tcID)) as BuildingPrivlidge;
			if (tc == null) return;
			
			var skinsEnGrado = ObtenerSkinsEnUso(tc)[grado];

			CuiHelper.DestroyUi(player, SubMenuSkins);
			CuiHelper.DestroyUi(player, MenuColores);
			CuiHelper.DestroyUi(player, MenuSkinsMaster);

			if (!skinsExcluidas.TryGetValue(player.userID, out HashSet<ulong> playerExcluidas))
			{
				playerExcluidas = new HashSet<ulong>();
				skinsExcluidas[player.userID] = playerExcluidas;
			}

			bool esAmigo = permission.UserHasPermission(player.UserIDString, PermisoAmigos);
			
			List<KeyValuePair<string, ulong>> skinsPoseidas = Facepunch.Pool.Get<List<KeyValuePair<string, ulong>>>();
			List<KeyValuePair<string, ulong>> skinsList = Facepunch.Pool.Get<List<KeyValuePair<string, ulong>>>();
			
			var skinsDict = SkinsDisponibles[grado];
			skinsList.AddRange(skinsDict); 
			
			int countSkins = skinsList.Count;
			for (int i = 0; i < countSkins; i++)
			{
				var s = skinsList[i]; 
				bool tieneSkin = false;
				
				if (s.Value == 0 || esAmigo || player.IsAdmin) tieneSkin = true;
				//else if (s.Value == 2 || s.Value == 10220 || s.Value == SKIN_CONTAINER) tieneSkin = true;
				else tieneSkin = player.blueprints.CheckSkinOwnership((int)s.Value, player);

				if (tieneSkin) skinsPoseidas.Add(s);
			}

			Facepunch.Pool.FreeUnmanaged(ref skinsList);

			int countPoseidas = skinsPoseidas.Count;
			if (countPoseidas == 0)
			{
				Facepunch.Pool.FreeUnmanaged(ref skinsPoseidas);
				return;
			}

			var elements = new CuiElementContainer();
			elements.Add(new CuiPanel { 
				Image = { Color = "0.02 0.02 0.02 0.96" }, 
				RectTransform = { AnchorMin = "0.25 0.72", AnchorMax = "0.75 0.82" },
				CursorEnabled = true
			}, "Overlay", SubMenuSkins);
			
			float x = 0.01f; 
			float spacing = 0.005f;
			float anchoBoton = (0.98f - (spacing * (countPoseidas - 1))) / countPoseidas;

			for (int i = 0; i < countPoseidas; i++)
			{
				var skin = skinsPoseidas[i]; 

				bool estaEnBase = skinsEnGrado.Contains(skin.Value);
				ulong claveUnicaCheck = (ulong)(grado * 1000000) + skin.Value;
				
				bool estaExcluida = playerExcluidas.Contains(claveUnicaCheck);

				string colorBoton = estaExcluida ? "0.05 0.05 0.05 0.8" : (estaEnBase ? "0.85 0.35 0.15 1" : "0.15 0.15 0.15 0.9");
				string cmd = (skin.Value == SKIN_CONTAINER) ? $"mejorar.menucontainer {grado} {skin.Value}" : $"mejorar.alerta {grado} {skin.Value} 0 0";
				
				string btnName = elements.Add(new CuiButton { 
					Button = { Command = estaExcluida ? "" : cmd, Color = colorBoton }, 
					RectTransform = { AnchorMin = $"{x} 0.35", AnchorMax = $"{x + anchoBoton} 0.90" }, 
					Text = { Text = "" } 
				}, SubMenuSkins);

				string prefijo = estaEnBase ? "<color=#ffd479>✓</color> " : "";
				elements.Add(new CuiLabel {
					Text = { 
						Text = $"{prefijo}{skin.Key.ToUpper()}", 
						FontSize = 9, 
						Align = TextAnchor.MiddleCenter, 
						Font = "robotocondensed-bold.ttf",
						Color = estaExcluida ? "0.3 0.3 0.3 1" : "0.9 0.9 0.9 1"
					},
					RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
				}, btnName);

				string colorBotonEx = estaExcluida ? "0.5 0.1 0.1 0.9" : "0.18 0.18 0.18 0.7";
				string textoEx = estaExcluida ? "<color=#ff9999>EXCLUIDA</color>" : "EXCLUIR";

				elements.Add(new CuiButton {
					Button = { Command = $"mejorar.alternarexcluirskin {grado} {skin.Value}", Color = colorBotonEx },
					RectTransform = { AnchorMin = $"{x} 0.10", AnchorMax = $"{x + anchoBoton} 0.30" },
					Text = { Text = textoEx, FontSize = 7, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" }
				}, SubMenuSkins);

				x += anchoBoton + spacing;
			}
			
			CuiHelper.AddUi(player, elements);
			Facepunch.Pool.FreeUnmanaged(ref skinsPoseidas);
		}

		[ConsoleCommand("mejorar.alternarexcluir")]
		void CmdAlternarExcluir(ConsoleSystem.Arg arg)
		{
			var player = arg.Player(); if (player == null) return;
			int grado = arg.GetInt(0);

			if (!materialesExcluidos.ContainsKey(player.userID))
				materialesExcluidos[player.userID] = new HashSet<int>();

			if (materialesExcluidos[player.userID].Contains(grado))
				materialesExcluidos[player.userID].Remove(grado);
			else
				materialesExcluidos[player.userID].Add(grado);
			MenuPrincipal(player);
		}
		
		[ChatCommand("papel")]
		void ChatPapel(BasePlayer player, string command, string[] args)
		{
			RaycastHit hit;
			if (Physics.Raycast(player.eyes.HeadRay(), out hit, 4f, 1 << 21))
			{
				BuildingBlock bloque = hit.GetEntity() as BuildingBlock;
				if (bloque == null) return;

				int tipo = 0; 
				string name = bloque.ShortPrefabName;
				
				if (name.StartsWith(genericFloor, StringComparison.Ordinal) || name.StartsWith(genericFoundation, StringComparison.Ordinal))
				{
					tipo = (hit.normal.y < 0) ? 3 : 2;
				}
				else if (name.StartsWith(genericRoof, StringComparison.Ordinal))
				{
					tipo = 3;
				}
				AbrirMenuIndividual(player, tipo);
				player.ChatMessage("<color=#FF0000><MiCustomTC></color>  <color=#75ccff>Selecciona un diseño para este tipo de superficie.</color>");
			}
			else 
			{ 
				player.ChatMessage("<color=#FF0000><MiCustomTC></color>  Mira una pared, suelo o techo primero."); 
			}
		}
		
		private Dictionary<ulong, HashSet<int>> materialesExcluidos = new Dictionary<ulong, HashSet<int>>();
		private Dictionary<int, List<ulong>> ObtenerSkinsEnUso(BuildingPrivlidge tc)
		{
			var mapa = new Dictionary<int, List<ulong>> { {1, new List<ulong>()}, {2, new List<ulong>()}, {3, new List<ulong>()}, {4, new List<ulong>()} };
			
			if (tc == null || tc.GetBuilding() == null) return mapa;

			var building = tc?.GetBuilding();
			if (building?.buildingBlocks != null)
			{
				var blocks = building.buildingBlocks.Values;
				int count = blocks.Count;

				for (int i = 0; i < count; i++)
				{
					var b = blocks[i];
					if (b == null) continue;

					int g = (int)b.grade;
					if (g >= 1 && g <= 4)
					{
						if (!mapa[g].Contains(b.skinID))
						{
							mapa[g].Add(b.skinID);
						}
					}
				}
			}
			return mapa;
		}

		private Dictionary<string, string> catalogoCache = new Dictionary<string, string>();
		private readonly string[] tiposNombres = { "pared", "suelo", "techo" };

		private void RefreshSkinsCache()
		{
			catalogoCache.Clear();
			int[] tipos = { 0, 2, 3 };
			bool[] modos = { true, false };
			
			int tiposCount = tipos.Length;
			int modosCount = modos.Length;
			
			for (int i = 0; i < tiposCount; i++)
			{
				int t = tipos[i];
				for (int j = 0; j < modosCount; j++)
				{
					bool m = modos[j];
					string key = $"{t}_{m}";
					catalogoCache[key] = GenerarJsonCatalogo(t, m);
				}
			}
			Puts("Caché de catálogos de skins regenerado correctamente.");
		}
		
		private string GenerarJsonCatalogo(int tipo, bool esIndividual)
		{
			var elements = new CuiElementContainer();
			
			List<ulong> listaActual = (tipo == 2) ? SkinsSuelo : (tipo == 3) ? SkinsTecho : SkinsWallpaper;
			string titulo = (tipo == 2) ? "CATÁLOGO: SUELOS" : (tipo == 3) ? "CATÁLOGO: TECHOS" : "CATÁLOGO: PAREDES";

			elements.Add(new CuiPanel { 
				Image = { Color = "0.02 0.02 0.02 0.98" }, 
				RectTransform = { AnchorMin = "0.15 0.05", AnchorMax = "0.85 0.80" }, 
				CursorEnabled = true 
			}, "Overlay", MenuSkinsMaster); 

			elements.Add(new CuiPanel {
				Image = { Color = "0.15 0.15 0.15 1.0" },
				RectTransform = { AnchorMin = "0.0 0.94", AnchorMax = "1.0 1.0" }
			}, MenuSkinsMaster);

			elements.Add(new CuiLabel {
				Text = { Text = $"{titulo} {(esIndividual ? "(INDIVIDUAL)" : "(GLOBAL)")}", FontSize = 12, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf", Color = "0.7 0.7 0.7 1" },
				RectTransform = { AnchorMin = "0.0 0.94", AnchorMax = "1.0 1.0" }
			}, MenuSkinsMaster);

			elements.Add(new CuiButton {
				Button = { Command = "micustomtc.cerrar2", Color = "0.7 0.2 0.2 1.0" }, 
				RectTransform = { AnchorMin = "0.98 0.945", AnchorMax = "0.997 0.995" },
				Text = { Text = "×", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
			}, MenuSkinsMaster);

			float xPos = 0.015f; 
			float yPos = 0.82f; 
			float sizeX = 0.065f; 
			float sizeY = 0.10f; 
			float spacingX = 0.008f;
			float spacingY = 0.02f;

			string tipoStr = tipo.ToString();
			var culture = System.Globalization.CultureInfo.InvariantCulture;

			// 1. Preparamos la lista filtrando por la variable esIndividual
			List<ulong> listaFinal = new List<ulong>();

			// Solo añadimos el ID 0 (botón de borrar) si el menú es GLOBAL (!esIndividual)
			if (!esIndividual)
			{
				listaFinal.Add(0);
			}

			// Añadimos el resto de skins normales
			if (listaActual != null) 
			{
				listaFinal.AddRange(listaActual);
			}

			int count = listaFinal.Count;

			for (int i = 0; i < count; i++)
			{
				ulong skinID = listaFinal[i];
				string skinStr = skinID.ToString();
				
				string comando = esIndividual 
					? $"papel.aplicar.individual {skinStr} {tipoStr}" 
					: $"mejorar.alerta 0 {skinStr} 0 {tipoStr}";

				string xMin = xPos.ToString("0.0000", culture);
				string yMin = yPos.ToString("0.0000", culture);
				string xMax = (xPos + sizeX).ToString("0.0000", culture);
				string yMax = (yPos + sizeY).ToString("0.0000", culture);

				string btn = elements.Add(new CuiButton {
					Button = { Command = comando, Color = "0.1 0.1 0.1 0.8" },
					RectTransform = { AnchorMin = $"{xMin} {yMin}", AnchorMax = $"{xMax} {yMax}" }
				}, MenuSkinsMaster);

				// Barra inferior (roja si es borrar, naranja si es skin)
				elements.Add(new CuiPanel {
					Image = { Color = skinID == 0 ? "0.8 0.2 0.2 0.5" : "0.85 0.35 0.15 0.5" },
					RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.04" }
				}, btn);

				// 2. Evaluamos qué dibujar
				if (skinID == 0)
				{
					// Dibujamos la X roja solo para el ID 0
					elements.Add(new CuiLabel {
						Text = { Text = "✖", FontSize = 28, Align = TextAnchor.MiddleCenter, Color = "0.8 0.2 0.2 1" },
						RectTransform = { AnchorMin = "0.1 0.15", AnchorMax = "0.9 0.9" }
					}, btn);
				}
				else if (skinID < 1000000) 
				{
					string contenido = ObtenerContenidoImagen(skinID);
					
					var rawImage = new CuiRawImageComponent {
						Sprite = "assets/content/effects/crossbreed/fx gradient skewed.png",
						Color = "1 1 1 1",
						FadeIn = 0.1f
					};

					if (contenido != "0" && ulong.TryParse(contenido, out _)) 
					{
						rawImage.Png = contenido;
					}
					else 
					{
						rawImage.Url = $"https://raw.githubusercontent.com/bmgjet/WallPaperIcons/main/{skinStr}.png";
					}

					elements.Add(new CuiElement { 
						Parent = btn, 
						Components = { rawImage, new CuiRectTransformComponent { AnchorMin = "0.1 0.15", AnchorMax = "0.9 0.9" } } 
					});
				}
				else 
				{
					elements.Add(new CuiElement { 
						Parent = btn, 
						Components = { 
							new CuiImageComponent { ItemId = ID_RODILLO, SkinId = skinID }, 
							new CuiRectTransformComponent { AnchorMin = "0.1 0.15", AnchorMax = "0.9 0.9" } 
						} 
					});
				}

				xPos += sizeX + spacingX;
				if (xPos + sizeX > 0.99f) {
					xPos = 0.015f;
					yPos -= (sizeY + spacingY);
				}
			}			return elements.ToJson();
		}
		
		void ConstruirMenuSkins(BasePlayer player, int tipoSuperficie, bool esIndividual)
		{
			CuiHelper.DestroyUi(player, MenuSkinsMaster);
			CuiHelper.DestroyUi(player, SubMenuSkins);
			string key = $"{tipoSuperficie}_{esIndividual}";
			
			if (catalogoCache.TryGetValue(key, out string jsonCacheado))
			{
				CuiHelper.AddUi(player, jsonCacheado);
			}
			else
			{
				string fallback = GenerarJsonCatalogo(tipoSuperficie, esIndividual);
				CuiHelper.AddUi(player, fallback);
			}
		}

		void AbrirMenuIndividual(BasePlayer player, int tipoSuperficie) => ConstruirMenuSkins(player, tipoSuperficie, true);

		[ConsoleCommand("mejorar.menupapel")]
		void CmdMenuPapel(ConsoleSystem.Arg arg)
		{
			var player = arg.Player(); if (player == null) return;
			ConstruirMenuSkins(player, arg.GetInt(0), false);
		}

		[ConsoleCommand("mejorar.menucontainer")]
		void CmdMenuContainer(ConsoleSystem.Arg arg)
		{
			var player = arg.Player(); 
			if (player == null) return;
			
			int grado = arg.GetInt(0); 
			ulong skinID = arg.GetUInt64(1);

			CuiHelper.DestroyUi(player, MenuAlertaMejora);
			CuiHelper.DestroyUi(player, MenuColores);
			var listaColores = Facepunch.Pool.Get<List<KeyValuePair<string, uint>>>();
			
			listaColores.AddRange(ColoresContainer);

			int total = listaColores.Count;
			if (total == 0)
			{
				Facepunch.Pool.FreeUnmanaged(ref listaColores);
				return;
			}
			var elements = new CuiElementContainer();
			
			elements.Add(new CuiPanel { 
				Image = { Color = "0.05 0.05 0.05 0.98" }, 
				RectTransform = { AnchorMin = "0.30 0.55", AnchorMax = "0.70 0.75" } 
			}, "Overlay", MenuColores);
			
			int porFila = Mathf.CeilToInt(total / 2f); 
			float padding = 0.02f; 
			float spacing = 0.015f; 
			float anchoBoton = (1.0f - (padding * 2) - (spacing * (porFila - 1))) / porFila;
			float altoBoton = 0.35f;

			for (int i = 0; i < total; i++)
			{
				int fila = i / porFila; 
				int columna = i % porFila;
				var color = listaColores[i];
				string rgba = GetColorRGBA(color.Key);
				float xMin = padding + (columna * (anchoBoton + spacing));
				float xMax = xMin + anchoBoton;
				float yMin = (fila == 0) ? 0.55f : 0.10f;
				float yMax = yMin + altoBoton;

				string colorBtn = elements.Add(new CuiButton
				{
					Button = { Command = $"mejorar.alerta {grado} {skinID} {color.Value}", Color = rgba },
					RectTransform = { AnchorMin = $"{xMin} {yMin}", AnchorMax = $"{xMax} {yMax}" },
					Text = { Text = "" }
				}, MenuColores);

				if (color.Value == 0 || color.Key == "ALEATORIO")
				{
					elements.Add(new CuiLabel {
						Text = { Text = "?", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.6" },
						RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
					}, colorBtn);
				}
			}
			CuiHelper.AddUi(player, elements);
			Facepunch.Pool.FreeUnmanaged(ref listaColores);
		}
		
		private Dictionary<ulong, List<BuildingBlock>> bloquesPendientes = new Dictionary<ulong, List<BuildingBlock>>();
		private Dictionary<ulong, KeyValuePair<ulong, Timer>> procesosActivos = new Dictionary<ulong, KeyValuePair<ulong, Timer>>();
		[ConsoleCommand("mejorar.alerta")]
		void CmdAlerta(ConsoleSystem.Arg arg)
		{
			var player = arg.Player(); 
			if (player == null) return; 
			
			int grado = arg.GetInt(0);
			ulong skinID = arg.GetUInt64(1);
			int colorID = arg.GetInt(2);
			int tipoFiltro = arg.GetInt(3); 
			bool esPapel = (grado == 0); 

			BuildingPrivlidge tc = null;
			if (tcAbierto.TryGetValue(player.userID, out uint tcIDGuardado))
			{
				tc = BaseNetworkable.serverEntities.Find(new NetworkableId(tcIDGuardado)) as BuildingPrivlidge;
			}
			if (tc == null) return;

			ulong tcID = tc.net.ID.Value;
			List<BuildingBlock> bloques = Facepunch.Pool.Get<List<BuildingBlock>>();
			if (bloquesPendientes.ContainsKey(tcID)) {
				var listaAntigua = bloquesPendientes[tcID];
				
				Facepunch.Pool.FreeUnmanaged(ref listaAntigua);
				bloquesPendientes.Remove(tcID);
			}
			var todosLosBloques = tc.GetBuilding().buildingBlocks;

			for (int i = 0; i < todosLosBloques.Count; i++)
			{
				BuildingBlock b = todosLosBloques[i];
				if (b == null) continue;

				int g = (int)b.grade;
				string name = b.ShortPrefabName;
				
				if (materialesExcluidos.ContainsKey(player.userID) && materialesExcluidos[player.userID].Contains(g)) continue;
				if (skinsExcluidas.ContainsKey(player.userID) && skinsExcluidas[player.userID].Contains((ulong)(g * 1000000) + b.skinID)) continue;

				if (esPapel)
				{
					bool isFrame = name.Contains("frame", StringComparison.OrdinalIgnoreCase);
					if (isFrame) continue;

					bool isWall = name.StartsWith(genericWall, StringComparison.Ordinal);
					bool isFloor = name.StartsWith(genericFloor, StringComparison.Ordinal);
					bool isFoundation = name.StartsWith(genericFoundation, StringComparison.Ordinal);
					bool isRoof = name.StartsWith(genericRoof, StringComparison.Ordinal);

					if (tipoFiltro == 0 && !isWall) continue;
					if (tipoFiltro == 2 && !isFloor && !isFoundation) continue;
					if (tipoFiltro == 3 && !isFloor && !isRoof) continue;
					var blocks = tc.GetBuilding().buildingBlocks;
					bool hayBloquesParaCambiar = false;

					if (wallpaperField != null)
					{
						ulong currentWallpaperId = (ulong)wallpaperField.GetValue(b);
						if (currentWallpaperId == skinID) continue;
					}
					bloques.Add(b);
				}
				else
				{

					if (g > grado) continue;
					if (g == grado && b.skinID == skinID)
					{
						if (skinID == SKIN_CONTAINER && b.customColour == (uint)colorID) continue;
					}
					
					bloques.Add(b);
				}
			}

			int cantidad = bloques.Count;
			if (cantidad == 0)
			{
				Facepunch.Pool.FreeUnmanaged(ref bloques);
				player.ChatMessage("<color=#FF0000><MiCustomTC></color>  NO HAY BLOQUES PARA MEJORAR. \nRevisa las exclusiones activas"); 
				return;
			}

			bloquesPendientes[tcID] = bloques;
			string colorPanelAcento = "0.25 0.25 0.25 0.8"; 
			string nombreSkinFull = "Original";
			string hexColorTexto = "#ffd479";

			if (esPapel) {
				nombreSkinFull = "Papel Pintado";
			} else if (skinID == SKIN_CONTAINER) {
				string nombreColor = ColoresContainer.FirstOrDefault(x => x.Value == (uint)colorID).Key ?? "ALEATORIO";
				colorPanelAcento = GetColorRGBA(nombreColor); 
				hexColorTexto = RGBAtoHex(colorPanelAcento); 
				nombreSkinFull = $"Contenedor ({nombreColor})";
			} else if (SkinsDisponibles.ContainsKey(grado)) {
				nombreSkinFull = SkinsDisponibles[grado].FirstOrDefault(x => x.Value == skinID).Key ?? "Skin Especial";
			}

			int costeTotal = 0;
			string shortnamePrincipal = esPapel ? SN_TELA : GetResourceShortname(grado);
			if (!(esPapel && skinID == 0))
			{
				int count = bloques != null ? bloques.Count : 0; 
				for (int i = 0; i < count; i++)
				{
					var b = bloques[i];
					if (b == null) continue;

					costeTotal += esPapel ? 5 : ((int)b.grade < grado ? GetCost(grado) : GetCostReskin(grado));
				}
			}

			ItemDefinition materialDef = ItemManager.FindItemDefinition(shortnamePrincipal);

			CuiHelper.DestroyUi(player, MenuAlertaMejora);
			var ui = new CuiElementContainer();

			ui.Add(new CuiPanel {
				Image = { Color = "0.05 0.05 0.05 0.95" },
				RectTransform = { AnchorMin = "0.30 0.35", AnchorMax = "0.70 0.65" },
				CursorEnabled = true 
			}, "Overlay", MenuAlertaMejora);

			ui.Add(new CuiPanel {
				Image = { Color = colorPanelAcento },
				RectTransform = { AnchorMin = "0.0 0.92", AnchorMax = "1.0 1.0" }
			}, MenuAlertaMejora);

			ui.Add(new CuiLabel {
				Text = { Text = "CONFIRMACIÓN DE OBRA", FontSize = 14, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" },
				RectTransform = { AnchorMin = "0.0 0.92", AnchorMax = "1.0 1.0" }
			}, MenuAlertaMejora);

			if (materialDef != null) {
				ui.Add(new CuiElement {
					Parent = MenuAlertaMejora,
					Components = {
						new CuiImageComponent { ItemId = materialDef.itemid },
						new CuiRectTransformComponent { AnchorMin = "0.42 0.68", AnchorMax = "0.58 0.88" }
					}
				});
				ui.Add(new CuiLabel {
					Text = { Text = $"Coste: {costeTotal} {materialDef.displayName.translated}", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "0.8 0.8 0.8 1" },
					RectTransform = { AnchorMin = "0.0 0.58", AnchorMax = "1.0 0.68" } 
				}, MenuAlertaMejora);
			}

			string cuerpo = (esPapel 
				? (skinID == 0  ? $"Vas a quitar el papel de <color={hexColorTexto}>{cantidad}</color> secciones." : $"Vas a empapelar <color={hexColorTexto}>{cantidad}</color> secciones.")
				: $"Vas a actualizar <color={hexColorTexto}>{cantidad}</color> bloques a:\n<size=18><color={hexColorTexto}>{nombreSkinFull}</color></size>");

			ui.Add(new CuiLabel {
				Text = { Text = cuerpo, FontSize = 15, Align = TextAnchor.MiddleCenter },
				RectTransform = { AnchorMin = "0.1 0.25", AnchorMax = "0.9 0.55" }
			}, MenuAlertaMejora);

			ui.Add(new CuiButton { 
					Button = { Command = $"mejorar.ejecutar {grado} {skinID} {colorID} {tipoFiltro} {tcID}", Color = "0.35 0.45 0.25 1.0" }, 
					RectTransform = { AnchorMin = "0.1 0.08", AnchorMax = "0.45 0.22" }, 
					Text = { Text = "ACEPTAR", FontSize = 13, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" } 
				}, MenuAlertaMejora);

			ui.Add(new CuiButton { 
				Button = { Command = "micustomtc.cerrarAlert", Color = "0.65 0.3 0.3 1.0" }, 
				RectTransform = { AnchorMin = "0.55 0.08", AnchorMax = "0.9 0.22" }, 
				Text = { Text = "CANCELAR", FontSize = 13, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" } 
			}, MenuAlertaMejora);

			CuiHelper.AddUi(player, ui);
		}

		private string RGBAtoHex(string rgba)
		{
			try {
				string[] p = rgba.Split(' ');
				return string.Format("#{0:X2}{1:X2}{2:X2}", (int)(float.Parse(p[0])*255), (int)(float.Parse(p[1])*255), (int)(float.Parse(p[2])*255));
			} catch { return "#ffffff"; }
		}
        #endregion

        #region Proceso de Mejora
[ConsoleCommand("mejorar.ejecutar")]
void CmdEjecutarMejora(ConsoleSystem.Arg arg)
{
    BasePlayer player = arg.Player(); 
    if (player == null) return;
    
    CerrarTodoUI(player);

    BuildingPrivlidge tc = null;
    if (tcAbierto.TryGetValue(player.userID, out uint tcIDGuardado))
    {
        tc = BaseNetworkable.serverEntities.Find(new NetworkableId(tcIDGuardado)) as BuildingPrivlidge;
    }
    if (tc == null || !tc.IsAuthed(player)) return;

    ulong tcID = tc.net.ID.Value;

    if (procesosActivos.ContainsKey(tcID)) { 
        PrintToChat(player, "<color=orange>Este Armario ya se está mejorando.</color>");
        return;
    }
    
    List<BuildingBlock> bloquesLista;
    if (!bloquesPendientes.TryGetValue(tcID, out bloquesLista)) 
    {
        PrintToChat(player, "<color=red>Error:</color> No hay bloques seleccionados para este TC.");
        return;
    }

    float tiempoEspera;
    if (RecibioDaño(tc, out tiempoEspera))
    {
        int minutos = Mathf.CeilToInt(tiempoEspera / 60);
        PrintToChat(player, $"<color=red>No puedes mejorar:</color> Daño reciente. Espera {minutos} min.");
        LimpiarListaTC(tcID); 
        return;
    }

    int gradoDestino = arg.GetInt(0); 
    ulong skinID = arg.GetUInt64(1); 
    int colorRecibido = arg.GetInt(2);
    int tipoFiltro = arg.GetInt(3); 

	Vector3 tcPos = tc.transform.position;
	bloquesLista.Sort((a, b) => (a.transform.position - tcPos).sqrMagnitude
					 .CompareTo((b.transform.position - tcPos).sqrMagnitude));
    
    Queue<BuildingBlock> cola = new Queue<BuildingBlock>(bloquesLista);
    bloquesPendientes.Remove(tcID); 
    string nombreMaterialUI = ((BuildingGrade.Enum)gradoDestino).ToString();
    
    int totalUI = bloquesLista.Count;
    
    procesosActivos[tcID] = new KeyValuePair<ulong, Timer>(player.userID, timer.Every(cooldownUpdateBuild, () => 
    { 
        if (cola.Count == 0 || tc == null || tc.IsDestroyed) 
        {
            FinalizarProcesoTC(tcID, tc);
            CuiHelper.DestroyUi(player, MenuProgreso);
            return;
        }

        for (int i = 0; i < intTanda && cola.Count > 0; i++)
        {
            int hechosUI = totalUI - cola.Count; 
            ProcesarPersistente(player, tc, cola, gradoDestino, skinID, colorRecibido, tipoFiltro, totalUI, hechosUI, nombreMaterialUI);
        }

        if (cola.Count == 0) 
        {
            var building = tc.GetBuilding();
            if (building != null) building.Dirty(); 
            FinalizarProcesoTC(tcID, tc);
            CuiHelper.DestroyUi(player, MenuProgreso);
			PrintToChat(player, $"<color=#55ff55>¡Trabajo completado!</color>\n Cambiaste: <color=#55ff55>{totalUI}</color> bloques!");
        }
    }));
    
    PrintToChat(player, $"<color=green>Mejora iniciada!</color> Puedes ir a otro TC mientras este termina.");
}
		private void LimpiarListaTC(ulong tcID)
		{
			if (bloquesPendientes.ContainsKey(tcID))
			{
				var lista = bloquesPendientes[tcID];
				if (lista != null)
				{
					Facepunch.Pool.FreeUnmanaged(ref lista);
				}
				bloquesPendientes.Remove(tcID);
			}
		}
		void FinalizarProcesoTC(ulong tcID, BuildingPrivlidge tc = null)
		{
			if (procesosActivos.TryGetValue(tcID, out var info))
			{
				info.Value?.Destroy(); 
				procesosActivos.Remove(tcID);
			}
			
			if (bloquesPendientes.ContainsKey(tcID))
			{
				var lista = bloquesPendientes[tcID];
				if (lista != null) Facepunch.Pool.FreeUnmanaged(ref lista);
				bloquesPendientes.Remove(tcID);
			}
		}

		[ConsoleCommand("micustomtc.cerrar")]
		private void cmdCerrarCUI(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null) return;
			CerrarTodoUI(player);
		}
		
		[ConsoleCommand("micustomtc.cerrar2")]
		private void cmdCerrarSkinsMasterCUI(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null) return;
			CuiHelper.DestroyUi(player, MenuSkinsMaster); 
		}
		[ConsoleCommand("micustomtc.cerrarAlert")]
		private void cmdCerrarAlert(ConsoleSystem.Arg arg)
		{
			var player = arg.Player();
			if (player == null) return;
			CuiHelper.DestroyUi(player, MenuAlertaMejora); 
		}
		
		[ConsoleCommand("mejorar.cancelar")]
		void CmdCancelarMejora(ConsoleSystem.Arg arg)
		{
			BasePlayer player = arg.Player();
			if (player == null) return;

			BuildingPrivlidge tc = null;
			if (tcAbierto.TryGetValue(player.userID, out uint tcIDGuardado))
			{
				tc = BaseNetworkable.serverEntities.Find(new NetworkableId(tcIDGuardado)) as BuildingPrivlidge;
			}
			if (tc == null || !tc.IsAuthed(player)) return;

			uint tcID = (uint)tc.net.ID.Value;

			if (procesosActivos.TryGetValue(tcID, out var info))
			{
				info.Value?.Destroy();
				procesosActivos.Remove(tcID);
				PrintToChat(player, "<color=red>Has detenido la mejora del armario.</color>");
				BasePlayer iniciador = BasePlayer.FindByID(info.Key);
				if (iniciador != null && iniciador != player)
				{
					PrintToChat(iniciador, $"<color=orange>{player.displayName} ha detenido tu proceso de mejora.</color>");
				}

				CerrarTodoUI(player);
			}
		}
		 
		void DetenerMejora(BasePlayer player)
		{
			if (procesosActivos.ContainsKey(player.userID))
			{
				if (procesosActivos.TryGetValue(player.userID, out KeyValuePair<ulong, Timer> infoProceso))
				{
					infoProceso.Value?.Destroy();
					procesosActivos.Remove(player.userID);
				}
				estadoMejoras.Remove(player.userID);
				CuiHelper.DestroyUi(player, MenuProgreso);
				player.ChatMessage("<color=#FF0000><MiCustomTC></color>  <color=#ff4d4d>Mejora cancelada correctamente.</color>");
			}
		}
		
		void UI_ProgresoMejora(BasePlayer player, int actuales, int total, string material, string coste)
		{
			CuiHelper.DestroyUi(player, MenuProgreso);
			var elements = new CuiElementContainer();

			elements.Add(new CuiPanel {
				Image = { Color = "0.0 0.0 0.0 0.8" },
				RectTransform = { AnchorMin = "0.35 0.12", AnchorMax = "0.65 0.22" }
			}, "Overlay", MenuProgreso);

			elements.Add(new CuiLabel {
				Text = { Text = $"MEJORANDO: <color=#ffd479>{material.ToUpper()}</color>\nPROGRESO: {actuales}/{total} ESTRUCTURAS", FontSize = 12, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" },
				RectTransform = { AnchorMin = "0.0 0.5", AnchorMax = "1.0 0.9" }
			}, MenuProgreso);

			elements.Add(new CuiLabel {
				Text = { Text = $"COSTE TOTAL: <color=#a5ff79>{coste}</color>", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "0.8 0.8 0.8 1" },
				RectTransform = { AnchorMin = "0.0 0.35", AnchorMax = "1.0 0.55" }
			}, MenuProgreso);

			elements.Add(new CuiButton {
				Button = { Command = "mejorar.cancelar", Color = "0.6 0.2 0.2 1" },
				RectTransform = { AnchorMin = "0.1 0.05", AnchorMax = "0.9 0.3" },
				Text = { Text = "CANCELAR MEJORA (O PULSA 'R')", FontSize = 10, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" }
			}, MenuProgreso);

			CuiHelper.AddUi(player, elements);
		}
		
		void OnPlayerInput(BasePlayer player, InputState input)
		{
			if (input.WasJustPressed(BUTTON.RELOAD)) 
			{
				BuildingPrivlidge tc = null;
				if (tcAbierto.TryGetValue(player.userID, out uint tcIDGuardado))
				{
					tc = BaseNetworkable.serverEntities.Find(new NetworkableId(tcIDGuardado)) as BuildingPrivlidge;
				}
				if (tc == null) return;

				uint tcID = (uint)tc.net.ID.Value;

				if (procesosActivos.TryGetValue(tcID, out var infoProceso))
				{
					ulong idDueño = infoProceso.Key;
					Timer timerActivo = infoProceso.Value;
					if (player.userID == idDueño)
					{
						timerActivo?.Destroy();
						procesosActivos.Remove(tcID);
						
						PrintToChat(player, "<color=#FF0000><MiCustomTC></color> Has detenido tu proceso de mejora.");
						CerrarTodoUI(player); 
					}
				}
			}
		}
		void OnPlayerDisconnected(BasePlayer player)
		{
			preferenciasSkins.Remove(player.userID);
			tcAbierto.Remove(player.userID);
		}
		private string floorTriangle = "floor.triangle";
		private string foundationTriangle = "foundation.triangle";
		private string genericFloor = "floor";
		private string genericFoundation = "foundation";
		private string genericWall = "wall";
		private string genericRoof = "roof";
		
		
		
		private static readonly FieldInfo wallpaperField = typeof(BuildingBlock).GetField("<wallpaperID>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

		void ProcesarPersistente(BasePlayer player, BuildingPrivlidge tc, Queue<BuildingBlock> cola, int grado, ulong skinID, int colorID, int tipoFiltro, int totalUI = 0, int hechosUI = 0, string nombreMaterialUI = "")
		{
			if (tc == null || tc.IsDestroyed) 
			{ 
				FinalizarTodo(player); 
				return;
			}

			BuildingBlock bloque = null;
			int gradoActual = 0;
			bool esPapel = (grado == 0);
			bool bloqueValidoEncontrado = false;

			// 1. Vaciamos la cola con un WHILE hasta encontrar un bloque que realmente necesite mejora
			while (cola.Count > 0)
			{
				bloque = cola.Dequeue();

				// Si no existe, pasamos al siguiente inmediatamente
				if (bloque == null || bloque.IsDestroyed || bloque.net == null) continue;

				gradoActual = (int)bloque.grade;
				ulong llaveActual = (ulong)(gradoActual * 1000000) + bloque.skinID;

				// Si está excluido, pasamos al siguiente
				if ((materialesExcluidos.ContainsKey(player.userID) && materialesExcluidos[player.userID].Contains(gradoActual)) ||
					(skinsExcluidas.ContainsKey(player.userID) && skinsExcluidas[player.userID].Contains(llaveActual)))
				{
					continue;
				}

				bool yaTieneLaMismaSkin = false;
				
				if (esPapel)
				{
					BaseNetworkable.SaveInfo saveInfo = new BaseNetworkable.SaveInfo
					{ 
						forDisk = false,
						msg = Facepunch.Pool.Get<ProtoBuf.Entity>()
					};
					bloque.Save(saveInfo);
					if (saveInfo.msg?.buildingBlock != null && saveInfo.msg.buildingBlock.wallpaperID == skinID) yaTieneLaMismaSkin = true;
					if (saveInfo.msg != null) Facepunch.Pool.Free(ref saveInfo.msg);
				}
				else
				{
					if (gradoActual == grado && bloque.skinID == skinID)
					{
						if (skinID == SKIN_CONTAINER)
							yaTieneLaMismaSkin = (bloque.customColour == (uint)colorID);
						else
							yaTieneLaMismaSkin = true;
					}
				}

				// Si ya tiene la skin, pasamos al siguiente
				if (yaTieneLaMismaSkin) continue;

				// Si pasamos todos los filtros, ¡tenemos un bloque válido! Rompemos el bucle.
				bloqueValidoEncontrado = true;
				break;
			}

			// 2. Si la cola se vació y no encontramos nada, simplemente terminamos la función
			if (!bloqueValidoEncontrado || bloque == null) return;

			bool esUltimoEnTanda = (cola.Count == 0);
			string name = bloque.ShortPrefabName;

			// --- LÓGICA DE COBRO ---
			string materialACobrar;
			int cantidadACobrar;

			if (esPapel)
			{
				materialACobrar = SN_TELA;
				cantidadACobrar = 5;
			}
			else
			{
				materialACobrar = (gradoActual < grado) ? GetResourceShortname(grado) : GetResourceShortname(gradoActual);
				cantidadACobrar = (gradoActual < grado) ? GetCost(grado) : GetCostReskin(gradoActual);
			}
			
			if (player != null && !player.IsAdmin && !(esPapel && skinID == 0))
			{
				
				int idBuscado = esPapel ? itemIdsCache[5] : itemIdsCache[grado];
				ItemDefinition itemDef = ItemManager.FindItemDefinition(idBuscado);
				
				if (itemDef == null || Contar(tc, itemDef.shortname) < cantidadACobrar)
				{ 
					PrintToChat(player, $"<color=red>Falta material en el TC: {materialACobrar}</color>");
					FinalizarTodo(player);
					return; 
				} 
				tc.inventory.Take(null, itemDef.itemid, cantidadACobrar);
			}

			int unidadesRestantes = cola.Count;
			hechosUI = totalUI - unidadesRestantes;
			int cantidadRestanteTotal = unidadesRestantes * cantidadACobrar;
			string infoMaterial = $"{cantidadRestanteTotal} de {materialACobrar}";
			UI_ProgresoMejora(player, hechosUI, totalUI, nombreMaterialUI, infoMaterial);
			
			// --- APLICACIÓN DE CAMBIOS ---
			if (esPapel)
			{
				if (name.StartsWith(genericWall, StringComparison.Ordinal))
				{
					if(skinID == 0)
					{
						bloque.RemoveWallpaper(0);
						bloque.RemoveWallpaper(1);
					} else
					{ 
						bloque.SetWallpaper(skinID, 0);
						bloque.SetWallpaper(skinID, 1);
					}
				}
				else if (tipoFiltro == 2)
				{
					int x = (name.StartsWith(genericFloor, StringComparison.Ordinal) ? 1 : 0);
					if (skinID == 0)
						bloque.RemoveWallpaper(x);
					else
						bloque.SetWallpaper(skinID, x);
				}
				else if (tipoFiltro == 3)
				{
					if (skinID == 0)
						bloque.RemoveWallpaper(0);
					else
						bloque.SetWallpaper(skinID, 0);
				}
				
				bloque.InvalidateNetworkCache();
				bloque.RefreshEntityLinks();

				if (esUltimoEnTanda)
				{
					bloque.SendNetworkUpdateImmediate();
					bloque.ClientRPC(RpcTarget.NetworkGroup("RefreshSkin"));
				}
				else
				{
					bloque.SendNetworkUpdate();
				}
				Effect.server.Run("assets/prefabs/deployable/repair bench/effects/skinchange_spraypaint.prefab", bloque.transform.position);
			}
			else
			{
				if (skinID > 0) 
				{
					bloque.SetFlagLocal(BaseEntity.Flags.Reserved1, true);
					bloque.SendNetworkUpdate_Flags();
					bloque.skinID = skinID;
					if (skinID == SKIN_CONTAINER) bloque.customColour = (uint)colorID;
				} 
				else 
				{
					bloque.SetFlagLocal(BaseEntity.Flags.Reserved1, false);
					bloque.SendNetworkUpdate_Flags();
					bloque.skinID = 0; 
					bloque.customColour = 0;
				}

				bloque.ChangeGrade((BuildingGrade.Enum)grado, true, true);
				bloque.health = bloque.MaxHealth();

				if (esUltimoEnTanda) bloque.SendNetworkUpdateImmediate();
				else bloque.SendNetworkUpdate();

				timer.Once(0.1f, () => {
					if (bloque == null || bloque.IsDestroyed) return;
					
					if (skinID == SKIN_CONTAINER) {
						bloque.customColour = (uint)colorID;
						bloque.SendNetworkUpdateImmediate();
						bloque.ClientRPC(RpcTarget.NetworkGroup("RefreshSkin"));
					} 
					
					bloque.RefreshEntityLinks();
					bloque.UpdateSkin();
				});

				Effect.server.Run($"assets/bundled/prefabs/fx/build/promote_{GetFX(grado)}.prefab", bloque.transform.position);
			}
		}
        #endregion

        #region Auxiliares
	
		bool PuedeAbrirMenu(BuildingPrivlidge tc, int gradoObjetivo) { if (tc == null) return false; var building = tc.GetBuilding(); if (building == null) return false; return building.buildingBlocks.Any(b => (int)b.grade <= gradoObjetivo); }

        void FinalizarTodo(BasePlayer player)
		{
            if (player != null && player.IsConnected) { CerrarTodoUI(player); DetenerProceso(player.userID); PrintToChat(player, "<color=#55ff55>¡Mejora interumpida!</color>"); }
        }
		
		bool RecibioDaño(BuildingPrivlidge tc, out float tiempoRestante)
		{
			tiempoRestante = 0;
			var building = tc.GetBuilding();
			if (building == null || building.buildingBlocks.Count == 0) return false;
			float ultimoAtaque = building.buildingBlocks.Max(b => b.lastAttackedTime);
			float diferencia = Time.realtimeSinceStartup - ultimoAtaque;
			if (diferencia < 600) tiempoRestante = 600 - diferencia;
			return tiempoRestante > 0;
		}
		
		void Unload()
		{
			if (bloquesPendientes != null && bloquesPendientes.Count > 0)
			{
				var keys = Facepunch.Pool.Get<List<ulong>>();
				keys.AddRange(bloquesPendientes.Keys);
				
				int countKeys = keys.Count;
				for (int i = 0; i < countKeys; i++) 
				{
					DetenerProceso(keys[i]); 
				}
				
				Facepunch.Pool.FreeUnmanaged(ref keys);
				bloquesPendientes.Clear();
			}

			if (procesosActivos != null && procesosActivos.Count > 0)
			{
				var values = Facepunch.Pool.Get<List<KeyValuePair<ulong, Timer>>>();
				values.AddRange(procesosActivos.Values);
				
				int countValues = values.Count;
				for (int i = 0; i < countValues; i++)
				{
					values[i].Value?.Destroy();
				}
				
				Facepunch.Pool.FreeUnmanaged(ref values);
				procesosActivos.Clear();
			}

			var harmony = new HarmonyLib.Harmony(this.Name);
			harmony.UnpatchAll(this.Name);
		}
		
		private Dictionary<ulong, float> lastRotate = new Dictionary<ulong, float>();

		void OnMeleeAttack(BasePlayer player, HitInfo info) 
		{
			if (player == null || info == null || player.GetActiveItem()?.info.shortname != "hammer") 
				return;

			if (info.HitEntity is BuildingBlock block) 
			{
				string name = block.ShortPrefabName;
				if(name.StartsWith(genericFloor, StringComparison.Ordinal) || 
				name.StartsWith(genericFoundation, StringComparison.Ordinal))
				{
					float lastTime;
					if (lastRotate.TryGetValue(block.net.ID.Value, out lastTime) && Time.realtimeSinceStartup - lastTime < cooldownHammer)
						return;
					
					lastRotate[block.net.ID.Value] = Time.realtimeSinceStartup;
					RotatePaperTrick(block);
				}
			}
		}
		//aqui
		private void RotatePaperTrick(BuildingBlock block)
		{
			if (block == null) return;

			if (wallpaperField != null)
			{
				ulong wallpaperId = (ulong)wallpaperField.GetValue(block);
				
				RotateOnlyBlock(block);
				
				if (wallpaperId != 0)
				{
					block.SetWallpaper(0, 0);
					block.SetWallpaper(wallpaperId, 0);
				}
			}
			else
			{
				RotateOnlyBlock(block);
			}

			block.ClientRPC(RpcTarget.NetworkGroup("RefreshSkin"));
			block.SendNetworkUpdate();
			RefreshBlockConnections(block);
			block.InvalidateNetworkCache();
			block.SendNetworkUpdateImmediate();
			block.UpdateSurroundingEntities();
			block.RefreshEntityLinks(); 
			block.UpdateSkin();
		}

		private void RotateOnlyBlock(BuildingBlock block)
		{
			string shortname = block.ShortPrefabName;
			 if(shortname.StartsWith(floorTriangle, StringComparison.Ordinal) || 
                  shortname.StartsWith(foundationTriangle, StringComparison.Ordinal))
				  return;
				//nextY = (currentEuler.y + 120f) % 360f;
				  
			Vector3 currentEuler = block.transform.localEulerAngles;
			float nextY = (currentEuler.y + 90f) % 360f;
				 
			block.transform.localRotation = Quaternion.Euler(currentEuler.x, nextY, currentEuler.z);
			
			block.InvalidateNetworkCache();
			block.SendNetworkUpdateImmediate();
			block.UpdateSurroundingEntities();
		}

        void DetenerProceso(ulong id) 
		{ 
			if (procesosActivos.TryGetValue(id, out KeyValuePair<ulong, Timer> info)) 
			{ 
				info.Value?.Destroy(); 
			} 
			procesosActivos.Remove(id); 
		}
        void RefreshBlockConnections(BuildingBlock block) { block.UpdateSkin(); block.SendNetworkUpdate(BasePlayer.NetworkQueue.Update); }
        string GetFX(int g){ if (g == 0) return "wood"; return g == 1 ? "wood" : g == 2 ? "stone" : g == 3 ? "metal" : "toptier"; }
        int Contar(BuildingPrivlidge tc, string sn)
		{
			if (tc?.inventory?.itemList == null) return 0;

			int total = 0;
			var items = tc.inventory.itemList;
			int count = items.Count;

			for (int i = 0; i < count; i++)
			{
				if (items[i].info.shortname == sn)
				{
					total += items[i].amount;
				}
			}
			return total;
		}
		string GetResourceShortname(int g) => g switch
		{
			1 => SN_MADERA,
			2 => SN_PIEDRA,
			3 => SN_METAL,
			_ => SN_HQM // El guión bajo (_) es el "else" (por defecto HQM si no es 1, 2 o 3)
		};

		int GetCost(int g) => g switch
		{
			1 => 200,
			2 => 300,
			3 => 200,
			_ => 25 // Por defecto 25 si es grado 4 (HQM) u otro valor
		};
        void CerrarTodoUI(BasePlayer player) { CuiHelper.DestroyUi(player, MenuProgreso); CuiHelper.DestroyUi(player, MenuSkinsMaster); CuiHelper.DestroyUi(player, MenuColores); CuiHelper.DestroyUi(player, MenuMejoraTC); CuiHelper.DestroyUi(player, SubMenuSkins); CuiHelper.DestroyUi(player, MenuAlertaMejora); CuiHelper.DestroyUi(player, MenuColores); }
        //void IniciarMonitorUI(BasePlayer player) { if (monitoresUI.ContainsKey(player.userID)) monitoresUI[player.userID].Destroy(); monitoresUI[player.userID] = timer.Every(0.5f, () => { if (player == null || !player.IsConnected || player.inventory.loot.entitySource == null) { CerrarTodoUI(player); DetenerMonitorUI(player.userID); } }); }
        //void DetenerMonitorUI(ulong id) { if (monitoresUI.TryGetValue(id, out Timer t)) t?.Destroy(); monitoresUI.Remove(id); }
        void OnPlayerLootEnd(BasePlayer player) { CerrarTodoUI(player); }
		void OnLootEntityEnd(BasePlayer player, BaseEntity entity){ if (entity is BuildingPrivlidge){ CerrarTodoUI(player); }}
        #endregion
			
		#region Harmony
        [HarmonyPatch(typeof(BuildingBlock), "CheckWallpaper")]
        internal class BuildingBlock_CheckWallpaper
        {
            [HarmonyPrefix]
            static bool Prefix() 
            {
                return decayext; 
            }
        }

        [HarmonyPatch(typeof(BuildingBlock), "DamageWallpaper", typeof(float), typeof(int))]
        internal class BuildingBlock_DamageWallpaper
        {
            [HarmonyPrefix]
            static bool Prefix(float totalDamage, int side, BuildingBlock __instance)
            {
                if (__instance == null) return true;
                if (side == 0)
                {
                    if (__instance.wallpaperHealth > 10) { __instance.wallpaperHealth = __instance.health; }
                    if (__instance.wallpaperHealth <= 0) { __instance.RemoveWallpaper(0); }
                    return false;
                }
                if (side == 1)
                {
                    if (__instance.wallpaperHealth2 > 10) { __instance.wallpaperHealth2 = __instance.health; }
                    if (__instance.wallpaperHealth2 <= 0) { __instance.RemoveWallpaper(1); }
                    return false;
                }
                return true;
            }
        }
        #endregion
    }
}