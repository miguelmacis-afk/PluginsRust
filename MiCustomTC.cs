using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("MiCustomTC", "☠ Ⓜ🅘ⓖ🅤ⓔ🅛 ☠", "12.1.0")]
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
        private const string SN_MARTILLO = "hammer";
        private const int ID_RODILLO = 1629564540;
        private const ulong SKIN_CONTAINER = 10221;

        private Dictionary<ulong, Dictionary<int, ulong>> preferenciasSkins = new Dictionary<ulong, Dictionary<int, ulong>>();
        private int[] itemIdsCache = new int[7];
        private Dictionary<ulong, uint> tcAbierto = new Dictionary<ulong, uint>();
        private Dictionary<ulong, HashSet<int>> materialesExcluidos = new Dictionary<ulong, HashSet<int>>();
        private Dictionary<ulong, HashSet<ulong>> skinsExcluidas = new Dictionary<ulong, HashSet<ulong>>();
        private Dictionary<ulong, List<BuildingBlock>> bloquesPendientes = new Dictionary<ulong, List<BuildingBlock>>();
        private Dictionary<ulong, KeyValuePair<ulong, Timer>> procesosActivos = new Dictionary<ulong, KeyValuePair<ulong, Timer>>();
        private Dictionary<ulong, float> lastRotate = new Dictionary<ulong, float>();
        private Dictionary<string, string> catalogoCache = new Dictionary<string, string>();
        private Dictionary<ulong, string> _nombreImagenCache = new Dictionary<ulong, string>();
        private Dictionary<ulong, MejoraData> estadoMejoras = new Dictionary<ulong, MejoraData>();

        class MejoraData
        {
            public int Total;
            public int Restantes;
            public string Material;
            public string Coste;
        }

        private static class UITheme
        {
            public const string FontBold = "robotocondensed-bold.ttf";
            public const string BgDark = "0.02 0.02 0.02 0.96";
            public const string BgHeader = "0.15 0.15 0.15 1.0";
            public const string BgPanel = "0.05 0.05 0.05 0.95";
            public const string ColorTextGray = "0.5 0.5 0.5 1";
            public const string ColorTextWhite = "0.8 0.8 0.8 1";
            public const string ColorClose = "0.7 0.2 0.2 1.0";
        }

        private string MenuProgreso = "MenuProgresoMejora";
        private string MenuColores = "MenuColores";
        private string MenuAlertaMejora = "MenuAlertaMejora";
        private string MenuMejoraTC = "MenuMejoraTC";
        private string SubMenuSkins = "SubMenuSkins";
        private string MenuSkinsMaster = "MenuSkinsMaster";

        private string floorTriangle = "floor.triangle";
        private string foundationTriangle = "foundation.triangle";
        private string genericFloor = "floor";
        private string genericFoundation = "foundation";
        private string genericWall = "wall";
        private string genericRoof = "roof";

        private static readonly FieldInfo wallpaperField = typeof(BuildingBlock).GetField("<wallpaperID>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly List<ulong> SkinsWallpaper = new List<ulong> { 
            3533776322, 3504835539, 3490858948, 3504824961, 3504766453, 3356317715, 3354244276, 3504823018, 
            3504820819, 3504818484, 3504816339, 3504764072, 3494348312, 3667006560, 3605218299, 3687900256, 
            3655561340, 3586137053, 3565743014, 3581160987, 3630888973, 3630898473, 3631610171, 3650013267, 
            3619929162, 3585686818, 3585535934, 3585535514, 3585534695, 3581164047, 3581153642, 3494326113, 
            3502068828, 3585529666, 3554384069, 3648505624, 3585536418, 3507213963, 3506198551, 3502067465, 
            3494247277, 3494243670, 3648718022, 3494331152, 3494250152, 3494249289, 3494251677, 3494245871, 
            3603575973, 3693542292, 3667693761, 3667592235, 3694418033, 3507926680, 3647121992, 3723732880, 
            3659386071, 3655559199, 3641634586, 3638686766
        };

        private readonly List<ulong> SkinsSuelo = new List<ulong> { 
            3488989152, 3612401817, 3609410737, 3536179251, 3612442626, 3612385488, 3515663186, 3497436292, 
            3495622788, 3648630794, 3504401940, 3565195753, 3564715606, 3495673853, 3490884961, 3586137712, 
            3505357378, 3539895972, 3683065793, 3597561712, 3698520219, 3616721101, 3588597109, 3712130855, 
            3709270212, 3494102293, 3504689720, 3554598937, 3593679701, 3495623743, 3551579695, 3616733803, 
            3496272072, 3574812090, 3554637771, 3504718416, 3504684886, 3658949698, 3633939281, 3633951325, 
            3616957640, 3567397518, 3567387398, 3551569615, 3551567358, 3550968653, 3550965458, 3551577457, 
            3551575286, 3575259635
        }; 

        private readonly List<ulong> SkinsTecho = new List<ulong> { 
            3495645149, 3536163911, 3495632162, 3495631196, 3575299620, 3493884033, 3491842568, 3593680010, 
            3595917595, 3564183047, 3494099849, 3564182431, 3562767441, 3586138061, 3574811116, 3554636782, 
            3505354220, 3551579135, 3551576556, 3494102455, 3678917020, 3575261534, 3683050416, 3658950577, 
            3633951917, 3633947362, 3633938499, 3564181616, 3564179614, 3551568744, 3551566647, 3550969422, 
            3550966753, 3496049170, 3633953313, 3551574214, 3496057466, 3604217491, 3493912545, 3495630765, 
            3565366241, 3556150438, 3505221207, 3567952894, 3567946950, 3564183387, 3495803510, 3553348389, 
            3645456487, 3567098152, 3556151238, 3556151065, 3565358888, 3556150661, 3554674736, 3496851248, 
            3683059177, 3683052763, 3574808774, 3564178997, 3683055149, 3496056607, 3496053480, 3496050429
        };

        private readonly Dictionary<int, Dictionary<string, ulong>> SkinsDisponibles = new Dictionary<int, Dictionary<string, ulong>>
        {
            { 1, new Dictionary<string, ulong> { { "NORMAL", 0 }, { "LEGACY", 10232 }, { "Gingerbread", 2 } }},
            { 2, new Dictionary<string, ulong> { { "NORMAL", 0 }, { "ADOBE", 10220 }, { "BRICK", 10223 }, { "BRUTALIST", 10225 }, { "JUMGLE", 10326 }, { "CRYPT", 10472 } }},
            { 3, new Dictionary<string, ulong> { { "NORMAL", 0 }, { "CONTAINER", SKIN_CONTAINER }, { "SCRAPKING", 10566 } }}, 
            { 4, new Dictionary<string, ulong> { { "NORMAL", 0 }, { "SPACE", 10430 } }}
        };

        private readonly Dictionary<string, uint> ColoresContainer = new Dictionary<string, uint>
        {
            { "AZUL", 1 }, { "VERDE", 2 }, { "MORADO", 3 }, { "MARRÓN", 4 }, { "NARANJA", 5 }, { "BLANCO", 6 }, { "NEGRO", 7 }, { "MARRÓN CLARO", 8 }, { "AZUL OSCURO", 9 },
            { "VERDE OLIVA", 10 }, { "ROJO", 11 }, { "BEIGE", 12 }, { "AMARILLO", 13 }, { "GRIS", 14 }, { "VERDE AZULADO", 15 }, { "GRIS CLARO", 16 }, { "ALEATORIO", 0 } 
        };

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

        #region Helper DRY
        private bool TryGetValidPlayerAndTC(ConsoleSystem.Arg arg, out BasePlayer player, out BuildingPrivlidge tc, bool checkAuth = true)
        {
            player = arg.Player();
            tc = null;
            if (player == null) return false;

            if (tcAbierto.TryGetValue(player.userID, out uint tcIDGuardado))
            {
                tc = BaseNetworkable.serverEntities.Find(new NetworkableId(tcIDGuardado)) as BuildingPrivlidge;
            }

            if (tc == null)
            {
                player.ChatMessage("<color=#FF0000><MiCustomTC></color> No tienes un armario activo seleccionado.");
                return false;
            }

            if (checkAuth && !tc.IsAuthed(player))
            {
                player.ChatMessage("<color=#FF0000><MiCustomTC></color> No tienes autorización en este armario.");
                return false;
            }

            return true;
        }
        #endregion

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
                Image = { Color = UITheme.BgDark }, 
                RectTransform = { AnchorMin = "0.15 0.84", AnchorMax = "0.85 0.94" },
                CursorEnabled = true
            }, "Overlay", MenuMejoraTC);

            elements.Add(new CuiPanel {
                Image = { Color = UITheme.BgHeader },
                RectTransform = { AnchorMin = "0.0 0.94", AnchorMax = "1.0 1.0" }
            }, MenuMejoraTC);

            elements.Add(new CuiLabel {
                Text = { Text = "GESTIÓN ESTRUCTURAL", FontSize = 9, Align = TextAnchor.MiddleCenter, Font = UITheme.FontBold, Color = UITheme.ColorTextGray },
                RectTransform = { AnchorMin = "0.0 0.94", AnchorMax = "1.0 1.0" }
            }, MenuMejoraTC);

            if (procesosActivos.ContainsKey(tcID))
            {
                elements.Add(new CuiButton
                {
                    Button = { Command = "mejorar.cancelar", Color = "0.8 0.2 0.2 0.9" },
                    RectTransform = { AnchorMin = "0.01 0.08", AnchorMax = "0.965 0.88" },
                    Text = { Text = "⚠ DETENER MEJORA EN CURSO ⚠", FontSize = 14, Align = TextAnchor.MiddleCenter, Font = UITheme.FontBold }
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
                    new { Label = "MADERA", Grado = 1 },
                    new { Label = "PIEDRA", Grado = 2 },
                    new { Label = "METAL",  Grado = 3 },
                    new { Label = "HQM",    Grado = 4 }
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
                            Font = UITheme.FontBold, 
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
                            Text = { Text = textoExcluir, FontSize = 7, Align = TextAnchor.MiddleCenter, Font = UITheme.FontBold }
                        }, MenuMejoraTC);
                    }

                    startX += ancho + spacing;
                }

                float martilloX = startX + 0.01f;
                float martilloAncho = 0.12f;
                
                string btnReparar = elements.Add(new CuiButton { 
                    Button = { Command = $"mejorar.reparar.todo {tcID}", Color = "0.25 0.35 0.25 0.9" }, 
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
                    Text = { Text = "REPARAR", FontSize = 10, Align = TextAnchor.MiddleLeft, Font = UITheme.FontBold, Color = UITheme.ColorTextWhite },
                    RectTransform = { AnchorMin = "0.40 0", AnchorMax = "1 1" }
                }, btnReparar); 

                float papelX = martilloX + martilloAncho + 0.02f;
                float papelAncho = (0.30f - (spacing * 2)) / 3;
                
                var papeles = new[] {
                    new { Label = "PARED", Cmd = "0", Color = "0.2 0.3 0.5 0.9" },
                    new { Label = "SUELO", Cmd = "2", Color = "0.2 0.5 0.3 0.9" },
                    new { Label = "TECHO", Cmd = "3", Color = "0.5 0.2 0.2 0.9" }
                };

                for (int i = 0; i < papeles.Length; i++)
                {
                    var p = papeles[i];
                    string pBtn = elements.Add(new CuiButton { 
                        Button = { Command = $"mejorar.menupapel {p.Cmd}", Color = "0.12 0.12 0.12 1.0" }, 
                        RectTransform = { AnchorMin = $"{papelX} 0.08", AnchorMax = $"{papelX + papelAncho} 0.88" },
                        Text = { Text = "" }
                    }, MenuMejoraTC);

                    elements.Add(new CuiLabel {
                        Text = { Text = p.Label, FontSize = 8, Align = TextAnchor.MiddleCenter, Font = UITheme.FontBold, Color = "0.6 0.6 0.6 1" },
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
                Button = { Command = "micustomtc.cerrar", Color = UITheme.ColorClose }, 
                RectTransform = { AnchorMin = "0.975 0.84", AnchorMax = "0.995 0.98" },
                Text = { Text = "×", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1", Font = UITheme.FontBold }
            }, MenuMejoraTC);
            
            CuiHelper.AddUi(player, elements);
        }
 
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
        #endregion

        #region Comandos de Papel y Skins
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
                bool tieneSkin = (s.Value == 0 || esAmigo || player.IsAdmin || player.blueprints.CheckSkinOwnership((int)s.Value, player));
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
                        Font = UITheme.FontBold,
                        Color = estaExcluida ? "0.3 0.3 0.3 1" : "0.9 0.9 0.9 1"
                    },
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" }
                }, btnName);

                string colorBotonEx = estaExcluida ? "0.5 0.1 0.1 0.9" : "0.18 0.18 0.18 0.7";
                string textoEx = estaExcluida ? "<color=#ff9999>EXCLUIDA</color>" : "EXCLUIR";

                elements.Add(new CuiButton {
                    Button = { Command = $"mejorar.alternarexcluirskin {grado} {skin.Value}", Color = colorBotonEx },
                    RectTransform = { AnchorMin = $"{x} 0.10", AnchorMax = $"{x + anchoBoton} 0.30" },
                    Text = { Text = textoEx, FontSize = 7, Align = TextAnchor.MiddleCenter, Font = UITheme.FontBold }
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

        private Dictionary<int, List<ulong>> ObtenerSkinsEnUso(BuildingPrivlidge tc)
        {
            var mapa = new Dictionary<int, List<ulong>> { {1, new List<ulong>()}, {2, new List<ulong>()}, {3, new List<ulong>()}, {4, new List<ulong>()} };
            if (tc == null || tc.GetBuilding() == null) return mapa;

            var building = tc.GetBuilding();
            if (building?.buildingBlocks != null)
            {
                var blocks = building.buildingBlocks.Values;
                int count = blocks.Count;
                for (int i = 0; i < count; i++)
                {
                    var b = blocks[i];
                    if (b == null) continue;
                    int g = (int)b.grade;
                    if (g >= 1 && g <= 4 && !mapa[g].Contains(b.skinID))
                    {
                        mapa[g].Add(b.skinID);
                    }
                }
            }
            return mapa;
        }

        private void RefreshSkinsCache()
        {
            catalogoCache.Clear();
            int[] tipos = { 0, 2, 3 };
            bool[] modos = { true, false };
            
            for (int i = 0; i < tipos.Length; i++)
            {
                int t = tipos[i];
                for (int j = 0; j < modos.Length; j++)
                {
                    bool m = modos[j];
                    catalogoCache[$"{t}_{m}"] = GenerarJsonCatalogo(t, m);
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
                Text = { Text = $"{titulo} {(esIndividual ? "(INDIVIDUAL)" : "(GLOBAL)")}", FontSize = 12, Align = TextAnchor.MiddleCenter, Font = UITheme.FontBold, Color = "0.7 0.7 0.7 1" },
                RectTransform = { AnchorMin = "0.0 0.94", AnchorMax = "1.0 1.0" }
            }, MenuSkinsMaster);

            elements.Add(new CuiButton {
                Button = { Command = "micustomtc.cerrar2", Color = UITheme.ColorClose }, 
                RectTransform = { AnchorMin = "0.98 0.945", AnchorMax = "0.997 0.995" },
                Text = { Text = "×", FontSize = 12, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" }
            }, MenuSkinsMaster);

            float xPos = 0.015f, yPos = 0.82f, sizeX = 0.065f, sizeY = 0.10f, spacingX = 0.008f, spacingY = 0.02f;
            string tipoStr = tipo.ToString();
            var culture = System.Globalization.CultureInfo.InvariantCulture;

            List<ulong> listaFinal = new List<ulong>();
            if (!esIndividual) listaFinal.Add(0);
            if (listaActual != null) listaFinal.AddRange(listaActual);

            for (int i = 0; i < listaFinal.Count; i++)
            {
                ulong skinID = listaFinal[i];
                string skinStr = skinID.ToString();
                string comando = esIndividual ? $"papel.aplicar.individual {skinStr} {tipoStr}" : $"mejorar.alerta 0 {skinStr} 0 {tipoStr}";

                string btn = elements.Add(new CuiButton {
                    Button = { Command = comando, Color = "0.1 0.1 0.1 0.8" },
                    RectTransform = { AnchorMin = $"{xPos.ToString("0.0000", culture)} {yPos.ToString("0.0000", culture)}", AnchorMax = $"{(xPos + sizeX).ToString("0.0000", culture)} {(yPos + sizeY).ToString("0.0000", culture)}" }
                }, MenuSkinsMaster);

                elements.Add(new CuiPanel {
                    Image = { Color = skinID == 0 ? "0.8 0.2 0.2 0.5" : "0.85 0.35 0.15 0.5" },
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.04" }
                }, btn);

                if (skinID == 0)
                {
                    elements.Add(new CuiLabel {
                        Text = { Text = "✖", FontSize = 28, Align = TextAnchor.MiddleCenter, Color = "0.8 0.2 0.2 1" },
                        RectTransform = { AnchorMin = "0.1 0.15", AnchorMax = "0.9 0.9" }
                    }, btn);
                }
                else if (skinID < 1000000) 
                {
                    string contenido = ObtenerContenidoImagen(skinID);
                    var rawImage = new CuiRawImageComponent { Sprite = "assets/content/effects/crossbreed/fx gradient skewed.png", Color = "1 1 1 1", FadeIn = 0.1f };
                    if (contenido != "0" && ulong.TryParse(contenido, out _)) rawImage.Png = contenido;
                    else rawImage.Url = $"https://raw.githubusercontent.com/bmgjet/WallPaperIcons/main/{skinStr}.png";

                    elements.Add(new CuiElement { Parent = btn, Components = { rawImage, new CuiRectTransformComponent { AnchorMin = "0.1 0.15", AnchorMax = "0.9 0.9" } } });
                }
                else 
                {
                    elements.Add(new CuiElement { Parent = btn, Components = { new CuiImageComponent { ItemId = ID_RODILLO, SkinId = skinID }, new CuiRectTransformComponent { AnchorMin = "0.1 0.15", AnchorMax = "0.9 0.9" } } });
                }

                xPos += sizeX + spacingX;
                if (xPos + sizeX > 0.99f) { xPos = 0.015f; yPos -= (sizeY + spacingY); }
            }
            return elements.ToJson();
        }

        void ConstruirMenuSkins(BasePlayer player, int tipoSuperficie, bool esIndividual)
        {
            CuiHelper.DestroyUi(player, MenuSkinsMaster);
            CuiHelper.DestroyUi(player, SubMenuSkins);
            string key = $"{tipoSuperficie}_{esIndividual}";
            if (catalogoCache.TryGetValue(key, out string jsonCacheado)) CuiHelper.AddUi(player, jsonCacheado);
            else CuiHelper.AddUi(player, GenerarJsonCatalogo(tipoSuperficie, esIndividual));
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
            var player = arg.Player(); if (player == null) return;
            int grado = arg.GetInt(0); 
            ulong skinID = arg.GetUInt64(1);

            CuiHelper.DestroyUi(player, MenuAlertaMejora);
            CuiHelper.DestroyUi(player, MenuColores);
            var listaColores = Facepunch.Pool.Get<List<KeyValuePair<string, uint>>>();
            listaColores.AddRange(ColoresContainer);

            if (listaColores.Count == 0) { Facepunch.Pool.FreeUnmanaged(ref listaColores); return; }

            var elements = new CuiElementContainer();
            elements.Add(new CuiPanel { Image = { Color = "0.05 0.05 0.05 0.98" }, RectTransform = { AnchorMin = "0.30 0.55", AnchorMax = "0.70 0.75" } }, "Overlay", MenuColores);
            
            int porFila = Mathf.CeilToInt(listaColores.Count / 2f); 
            float padding = 0.02f, spacing = 0.015f, anchoBoton = (1.0f - (padding * 2) - (spacing * (porFila - 1))) / porFila, altoBoton = 0.35f;

            for (int i = 0; i < listaColores.Count; i++)
            {
                int fila = i / porFila, columna = i % porFila;
                var color = listaColores[i];
                string rgba = GetColorRGBA(color.Key);
                float xMin = padding + (columna * (anchoBoton + spacing)), xMax = xMin + anchoBoton;
                float yMin = (fila == 0) ? 0.55f : 0.10f, yMax = yMin + altoBoton;

                string colorBtn = elements.Add(new CuiButton {
                    Button = { Command = $"mejorar.alerta {grado} {skinID} {color.Value}", Color = rgba },
                    RectTransform = { AnchorMin = $"{xMin} {yMin}", AnchorMax = $"{xMax} {yMax}" },
                    Text = { Text = "" }
                }, MenuColores);

                if (color.Value == 0 || color.Key == "ALEATORIO")
                {
                    elements.Add(new CuiLabel { Text = { Text = "?", FontSize = 16, Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.6" }, RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" } }, colorBtn);
                }
            }
            CuiHelper.AddUi(player, elements);
            Facepunch.Pool.FreeUnmanaged(ref listaColores);
        }
        #endregion

        #region Alerta y Preparación de Obra
        [ConsoleCommand("mejorar.alerta")]
        void CmdAlerta(ConsoleSystem.Arg arg)
        {
            var player = arg.Player(); if (player == null) return; 
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

            var todosLosBloques = tc.GetBuilding()?.buildingBlocks;
            if (todosLosBloques != null)
            {
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
                        if (name.Contains("frame", StringComparison.OrdinalIgnoreCase)) continue;
                        bool isWall = name.StartsWith(genericWall, StringComparison.Ordinal);
                        bool isFloor = name.StartsWith(genericFloor, StringComparison.Ordinal);
                        bool isFoundation = name.StartsWith(genericFoundation, StringComparison.Ordinal);
                        bool isRoof = name.StartsWith(genericRoof, StringComparison.Ordinal);

                        if (tipoFiltro == 0 && !isWall) continue;
                        if (tipoFiltro == 2 && !isFloor && !isFoundation) continue;
                        if (tipoFiltro == 3 && !isFloor && !isRoof) continue;

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
                        if (g == grado && b.skinID == skinID && (skinID != SKIN_CONTAINER || b.customColour == (uint)colorID)) continue;
                        bloques.Add(b);
                    }
                }
            }

            int cantidad = bloques.Count;
            if (cantidad == 0)
            {
                Facepunch.Pool.FreeUnmanaged(ref bloques);
                player.ChatMessage("<color=#FF0000><MiCustomTC></color> NO HAY BLOQUES PARA MEJORAR. \nRevisa las exclusiones activas"); 
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

            CuiHelper.DestroyUi(player, MenuAlertaMejora);
            var ui = new CuiElementContainer();

            ui.Add(new CuiPanel { Image = { Color = UITheme.BgPanel }, RectTransform = { AnchorMin = "0.30 0.35", AnchorMax = "0.70 0.65" }, CursorEnabled = true }, "Overlay", MenuAlertaMejora);
            ui.Add(new CuiPanel { Image = { Color = colorPanelAcento }, RectTransform = { AnchorMin = "0.0 0.92", AnchorMax = "1.0 1.0" } }, MenuAlertaMejora);
            ui.Add(new CuiLabel { Text = { Text = "CONFIRMACIÓN DE OBRA", FontSize = 14, Align = TextAnchor.MiddleCenter, Font = UITheme.FontBold }, RectTransform = { AnchorMin = "0.0 0.92", AnchorMax = "1.0 1.0" } }, MenuAlertaMejora);

            string cuerpo = (esPapel 
                ? (skinID == 0 ? $"Vas a quitar el papel de <color={hexColorTexto}>{cantidad}</color> secciones." : $"Vas a empapelar <color={hexColorTexto}>{cantidad}</color> secciones.")
                : $"Vas a actualizar <color={hexColorTexto}>{cantidad}</color> bloques a:\n<size=18><color={hexColorTexto}>{nombreSkinFull}</color></size>");

            ui.Add(new CuiLabel { Text = { Text = cuerpo, FontSize = 15, Align = TextAnchor.MiddleCenter }, RectTransform = { AnchorMin = "0.1 0.25", AnchorMax = "0.9 0.55" } }, MenuAlertaMejora);

            ui.Add(new CuiButton { Button = { Command = $"mejorar.ejecutar {grado} {skinID} {colorID} {tipoFiltro} {tcID}", Color = "0.35 0.45 0.25 1.0" }, RectTransform = { AnchorMin = "0.1 0.08", AnchorMax = "0.45 0.22" }, Text = { Text = "ACEPTAR", FontSize = 13, Align = TextAnchor.MiddleCenter, Font = UITheme.FontBold } }, MenuAlertaMejora);
            ui.Add(new CuiButton { Button = { Command = "micustomtc.cerrarAlert", Color = "0.65 0.3 0.3 1.0" }, RectTransform = { AnchorMin = "0.55 0.08", AnchorMax = "0.9 0.22" }, Text = { Text = "CANCELAR", FontSize = 13, Align = TextAnchor.MiddleCenter, Font = UITheme.FontBold } }, MenuAlertaMejora);

            CuiHelper.AddUi(player, ui);
        }
        #endregion

        #region Comandos de Ejecución y Reparación
        [ConsoleCommand("mejorar.reparar.todo")]
		void CmdRepararTodo(ConsoleSystem.Arg arg)
		{
			if (!TryGetValidPlayerAndTC(arg, out BasePlayer player, out BuildingPrivlidge tc)) return;

			uint tcID = (uint)tc.net.ID.Value;
			var building = tc.GetBuilding();
			if (building == null) return;

			// Recolectamos bloques que necesitan reparación
			List<BuildingBlock> bloquesAReparar = Facepunch.Pool.Get<List<BuildingBlock>>();
			var blocks = building.buildingBlocks?.Values;
			if (blocks != null)
			{
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

			// Si ya hay un proceso activo en este TC, lo detenemos
			if (procesosActivos.ContainsKey(tcID)) 
			{ 
				procesosActivos[tcID].Value?.Destroy(); 
				procesosActivos.Remove(tcID); 
			}

			Queue<BuildingBlock> colaReparacion = new Queue<BuildingBlock>(bloquesAReparar);
			Facepunch.Pool.FreeUnmanaged(ref bloquesAReparar); // Ya pasamos los datos a la cola, liberamos la lista

			int totalUI = colaReparacion.Count;

			// Usamos un proceso por tandas (similar al de mejoras) para optimizar el rendimiento del servidor
			procesosActivos[tcID] = new KeyValuePair<ulong, Timer>(player.userID, timer.Every(cooldownUpdateBuild, () => 
			{
				if (player == null || tc == null || tc.IsDestroyed || tc.inventory == null || colaReparacion.Count == 0)
				{
					FinalizarProcesoTC(tcID, tc);
					CuiHelper.DestroyUi(player, MenuProgreso);
					return;
				}

				if (RecibioDaño(tc, out float tiempoEspera))
				{
					PrintToChat(player, $"<color=red>Reparación interrumpida:</color> Daño reciente detectado.");
					FinalizarProcesoTC(tcID, tc);
					CuiHelper.DestroyUi(player, MenuProgreso);
					return;
				}

				// Procesamos una tanda por cada ciclo (ej: 10 bloques por vez)
				for (int t = 0; t < intTanda && colaReparacion.Count > 0; t++)
				{
					var block = colaReparacion.Dequeue();
					if (block == null || block.IsDestroyed || block.health >= block.MaxHealth()) continue;

					float missingFraction = 1f - (block.health / block.MaxHealth());
					var costesTotales = block.RepairCost(missingFraction);

					if (costesTotales != null && costesTotales.Count > 0)
					{
						float porcentajePosible = 1f;
						int cCount = costesTotales.Count;

						for (int j = 0; j < cCount; j++)
						{
							var item = costesTotales[j];
							if (item == null) continue;

							int disponible = tc.inventory.GetAmount(item.itemid, false);
							if (disponible < item.amount)
							{
								float ratio = (float)disponible / item.amount;
								if (ratio < porcentajePosible) porcentajePosible = ratio;
							}
							if (porcentajePosible <= 0f) break;
						}
						
						if (porcentajePosible <= 0.01f)
						{
							player.ChatMessage("<color=#FF0000><MiCustomTC></color> Reparación cancelada: Sin materiales en el TC.");
							FinalizarProcesoTC(tcID, tc);
							CuiHelper.DestroyUi(player, MenuProgreso);
							return;
						}
						
						for (int j = 0; j < cCount; j++)
						{
							var item = costesTotales[j];
							if (item == null) continue;
							int cantidadACobrar = Mathf.CeilToInt(item.amount * porcentajePosible);
							if (cantidadACobrar > 0) tc.inventory.Take(null, item.itemid, cantidadACobrar);
						}

						Effect.server.Run("assets/bundled/prefabs/fx/build/repair.prefab", block.transform.position);
						block.health = Mathf.Min(block.MaxHealth(), block.health + ((block.MaxHealth() - block.health) * porcentajePosible));
						block.SendNetworkUpdate();
					}
				}

				int hechos = totalUI - colaReparacion.Count;
				if (colaReparacion.Count == 0)
				{
					player.ChatMessage("<color=#00FF00><MiCustomTC></color> Reparación finalizada.");
					FinalizarProcesoTC(tcID, tc);
					CuiHelper.DestroyUi(player, MenuProgreso);
				}
				else
				{
					UI_ProgresoMejora(player, hechos, totalUI, "REPARANDO", "USANDO RECURSOS TC");
				}
			}));

			CerrarTodoUI(player);
		}

        [ConsoleCommand("mejorar.ejecutar")]
        void CmdEjecutarMejora(ConsoleSystem.Arg arg)
        {
            if (!TryGetValidPlayerAndTC(arg, out BasePlayer player, out BuildingPrivlidge tc)) return;
            CerrarTodoUI(player);
            ulong tcID = tc.net.ID.Value;

            if (procesosActivos.ContainsKey(tcID)) { PrintToChat(player, "<color=orange>Este Armario ya se está mejorando.</color>"); return; }
            if (!bloquesPendientes.TryGetValue(tcID, out List<BuildingBlock> bloquesLista)) { PrintToChat(player, "<color=red>Error:</color> No hay bloques seleccionados para este TC."); return; }

            if (RecibioDaño(tc, out float tiempoEspera))
            {
                PrintToChat(player, $"<color=red>No puedes mejorar:</color> Daño reciente. Espera {Mathf.CeilToInt(tiempoEspera / 60)} min.");
                LimpiarListaTC(tcID); 
                return;
            }

            int gradoDestino = arg.GetInt(0); 
            ulong skinID = arg.GetUInt64(1); 
            int colorRecibido = arg.GetInt(2);
            int tipoFiltro = arg.GetInt(3); 

            Vector3 tcPos = tc.transform.position;
            bloquesLista.Sort((a, b) => (a.transform.position - tcPos).sqrMagnitude.CompareTo((b.transform.position - tcPos).sqrMagnitude));
            
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
                    ProcesarPersistente(player, tc, cola, gradoDestino, skinID, colorRecibido, tipoFiltro, totalUI, totalUI - cola.Count, nombreMaterialUI);
                }

                if (cola.Count == 0) 
                {
                    tc.GetBuilding()?.Dirty(); 
                    FinalizarProcesoTC(tcID, tc);
                    CuiHelper.DestroyUi(player, MenuProgreso);
                    PrintToChat(player, $"<color=#55ff55>¡Trabajo completado!</color>\n Cambiaste: <color=#55ff55>{totalUI}</color> bloques!");
                }
            }));
            
            PrintToChat(player, $"<color=green>Mejora iniciada!</color> Puedes ir a otro TC mientras este termina.");
        }
        #endregion

        #region Procesamiento Modularizado de Bloques
        void ProcesarPersistente(BasePlayer player, BuildingPrivlidge tc, Queue<BuildingBlock> cola, int grado, ulong skinID, int colorID, int tipoFiltro, int totalUI, int hechosUI, string nombreMaterialUI)
        {
            if (tc == null || tc.IsDestroyed) { FinalizarTodo(player); return; }

            BuildingBlock bloque = null;
            bool bloqueValidoEncontrado = false;

            while (cola.Count > 0)
            {
                bloque = cola.Dequeue();
                if (bloque == null || bloque.IsDestroyed || bloque.net == null) continue;
                if (!ValidarBloqueParaMejora(player, bloque, grado, skinID, colorID, grado == 0)) continue;
                bloqueValidoEncontrado = true;
                break;
            }

            if (!bloqueValidoEncontrado || bloque == null) return;

            int gradoActual = (int)bloque.grade;
            bool esPapel = (grado == 0);

            if (!CobrarMaterialesTC(player, tc, gradoActual, grado, skinID, esPapel))
            {
                FinalizarTodo(player);
                return;
            }

            int unidadesRestantes = cola.Count;
            int costeUnidad = esPapel ? 5 : ((gradoActual < grado) ? GetCost(grado) : GetCostReskin(gradoActual));
            string matName = esPapel ? SN_TELA : (gradoActual < grado ? GetResourceShortname(grado) : GetResourceShortname(gradoActual));
            
            UI_ProgresoMejora(player, totalUI - unidadesRestantes, totalUI, nombreMaterialUI, $"{unidadesRestantes * costeUnidad} de {matName}");
            AplicarCambioVisual(bloque, grado, skinID, colorID, tipoFiltro, esPapel, cola.Count == 0);
        }

        private bool ValidarBloqueParaMejora(BasePlayer player, BuildingBlock bloque, int grado, ulong skinID, int colorID, bool esPapel)
        {
            int gradoActual = (int)bloque.grade;
            ulong llaveActual = (ulong)(gradoActual * 1000000) + bloque.skinID;

            if ((materialesExcluidos.ContainsKey(player.userID) && materialesExcluidos[player.userID].Contains(gradoActual)) ||
                (skinsExcluidas.ContainsKey(player.userID) && skinsExcluidas[player.userID].Contains(llaveActual)))
            {
                return false;
            }

            if (esPapel)
            {
                BaseNetworkable.SaveInfo saveInfo = new BaseNetworkable.SaveInfo { forDisk = false, msg = Facepunch.Pool.Get<ProtoBuf.Entity>() };
                bloque.Save(saveInfo);
                bool tienePapel = (saveInfo.msg?.buildingBlock != null && saveInfo.msg.buildingBlock.wallpaperID == skinID);
                if (saveInfo.msg != null) Facepunch.Pool.Free(ref saveInfo.msg);
                if (tienePapel) return false;
            }
            else
            {
                if (gradoActual == grado && bloque.skinID == skinID)
                {
                    if (skinID == SKIN_CONTAINER && bloque.customColour == (uint)colorID) return false;
                    if (skinID != SKIN_CONTAINER) return false;
                }
            }
            return true;
        }

        private bool CobrarMaterialesTC(BasePlayer player, BuildingPrivlidge tc, int gradoActual, int grado, ulong skinID, bool esPapel)
        {
            if (player != null && player.IsAdmin) return true;
            if (esPapel && skinID == 0) return true;

            string material = esPapel ? SN_TELA : ((gradoActual < grado) ? GetResourceShortname(grado) : GetResourceShortname(gradoActual));
            int coste = esPapel ? 5 : ((gradoActual < grado) ? GetCost(grado) : GetCostReskin(gradoActual));
            int idItem = esPapel ? itemIdsCache[5] : itemIdsCache[grado];
            ItemDefinition itemDef = ItemManager.FindItemDefinition(idItem);

            if (itemDef == null || Contar(tc, itemDef.shortname) < coste)
            {
                PrintToChat(player, $"<color=red>Falta material en el TC: {material}</color>");
                return false;
            }

            tc.inventory.Take(null, itemDef.itemid, coste);
            return true;
        }

        private void AplicarCambioVisual(BuildingBlock bloque, int grado, ulong skinID, int colorID, int tipoFiltro, bool esPapel, bool esUltimo)
        {
            string name = bloque.ShortPrefabName;
            if (esPapel)
            {
                if (name.StartsWith(genericWall, StringComparison.Ordinal))
                {
                    if (skinID == 0) { bloque.RemoveWallpaper(0); bloque.RemoveWallpaper(1); }
                    else { bloque.SetWallpaper(skinID, 0); bloque.SetWallpaper(skinID, 1); }
                }
                else if (tipoFiltro == 2)
                {
                    int x = name.StartsWith(genericFloor, StringComparison.Ordinal) ? 1 : 0;
                    if (skinID == 0) bloque.RemoveWallpaper(x); else bloque.SetWallpaper(skinID, x);
                }
                else if (tipoFiltro == 3)
                {
                    if (skinID == 0) bloque.RemoveWallpaper(0); else bloque.SetWallpaper(skinID, 0);
                }

                bloque.InvalidateNetworkCache();
                bloque.RefreshEntityLinks();

                if (esUltimo)
                {
                    bloque.SendNetworkUpdateImmediate();
                    bloque.ClientRPC(RpcTarget.NetworkGroup("RefreshSkin"));
                }
                else bloque.SendNetworkUpdate();

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

                if (esUltimo) bloque.SendNetworkUpdateImmediate();
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

        #region Truco del Martillo y Rotación
        void OnMeleeAttack(BasePlayer player, HitInfo info) 
        {
            if (player == null || info == null || player.GetActiveItem()?.info.shortname != "hammer") return;

            if (info.HitEntity is BuildingBlock block) 
            {
                string name = block.ShortPrefabName;
                if(name.StartsWith(genericFloor, StringComparison.Ordinal) || name.StartsWith(genericFoundation, StringComparison.Ordinal))
                {
                    if (lastRotate.TryGetValue(block.net.ID.Value, out float lastTime) && Time.realtimeSinceStartup - lastTime < cooldownHammer) return;
                    lastRotate[block.net.ID.Value] = Time.realtimeSinceStartup;
                    RotatePaperTrick(block);
                }
            }
        }

        private void RotatePaperTrick(BuildingBlock block)
        {
            if (block == null) return;

            if (wallpaperField != null)
            {
                ulong wallpaperId = (ulong)wallpaperField.GetValue(block);
                RotateOnlyBlock(block);
                if (wallpaperId != 0) { block.SetWallpaper(0, 0); block.SetWallpaper(wallpaperId, 0); }
            }
            else { RotateOnlyBlock(block); }

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
            if (shortname.StartsWith(floorTriangle, StringComparison.Ordinal) || shortname.StartsWith(foundationTriangle, StringComparison.Ordinal)) return;
              
            Vector3 currentEuler = block.transform.localEulerAngles;
            float nextY = (currentEuler.y + 90f) % 360f;
            block.transform.localRotation = Quaternion.Euler(currentEuler.x, nextY, currentEuler.z);
            
            block.InvalidateNetworkCache();
            block.SendNetworkUpdateImmediate();
            block.UpdateSurroundingEntities();
        }
        #endregion

        #region Métodos Auxiliares y Eventos de Jugador
        [ConsoleCommand("micustomtc.cerrar")]
        private void cmdCerrarCUI(ConsoleSystem.Arg arg) { var player = arg.Player(); if (player != null) CerrarTodoUI(player); }

        [ConsoleCommand("micustomtc.cerrar2")]
        private void cmdCerrarSkinsMasterCUI(ConsoleSystem.Arg arg) { var player = arg.Player(); if (player != null) CuiHelper.DestroyUi(player, MenuSkinsMaster); }

        [ConsoleCommand("micustomtc.cerrarAlert")]
        private void cmdCerrarAlert(ConsoleSystem.Arg arg) { var player = arg.Player(); if (player != null) CuiHelper.DestroyUi(player, MenuAlertaMejora); }

        [ConsoleCommand("mejorar.cancelar")]
        void CmdCancelarMejora(ConsoleSystem.Arg arg)
        {
            if (!TryGetValidPlayerAndTC(arg, out BasePlayer player, out BuildingPrivlidge tc)) return;
            uint tcID = (uint)tc.net.ID.Value;

            if (procesosActivos.TryGetValue(tcID, out var info))
            {
                info.Value?.Destroy();
                procesosActivos.Remove(tcID);
                PrintToChat(player, "<color=red>Has detenido la mejora del armario.</color>");
                BasePlayer iniciador = BasePlayer.FindByID(info.Key);
                if (iniciador != null && iniciador != player) PrintToChat(iniciador, $"<color=orange>{player.displayName} ha detenido tu proceso de mejora.</color>");
                CerrarTodoUI(player);
            }
        }

        void UI_ProgresoMejora(BasePlayer player, int actuales, int total, string material, string coste)
        {
            CuiHelper.DestroyUi(player, MenuProgreso);
            var elements = new CuiElementContainer();

            elements.Add(new CuiPanel { Image = { Color = "0.0 0.0 0.0 0.8" }, RectTransform = { AnchorMin = "0.35 0.12", AnchorMax = "0.65 0.22" } }, "Overlay", MenuProgreso);
            elements.Add(new CuiLabel { Text = { Text = $"MEJORANDO: <color=#ffd479>{material.ToUpper()}</color>\nPROGRESO: {actuales}/{total} ESTRUCTURAS", FontSize = 12, Align = TextAnchor.MiddleCenter, Font = UITheme.FontBold }, RectTransform = { AnchorMin = "0.0 0.5", AnchorMax = "1.0 0.9" } }, MenuProgreso);
            elements.Add(new CuiLabel { Text = { Text = $"COSTE TOTAL: <color=#a5ff79>{coste}</color>", FontSize = 10, Align = TextAnchor.MiddleCenter, Color = UITheme.ColorTextWhite }, RectTransform = { AnchorMin = "0.0 0.35", AnchorMax = "1.0 0.55" } }, MenuProgreso);
            elements.Add(new CuiButton { Button = { Command = "mejorar.cancelar", Color = "0.6 0.2 0.2 1" }, RectTransform = { AnchorMin = "0.1 0.05", AnchorMax = "0.9 0.3" }, Text = { Text = "CANCELAR MEJORA (O PULSA 'R')", FontSize = 10, Align = TextAnchor.MiddleCenter, Font = UITheme.FontBold } }, MenuProgreso);

            CuiHelper.AddUi(player, elements);
        }

        void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (input.WasJustPressed(BUTTON.RELOAD)) 
            {
                if (tcAbierto.TryGetValue(player.userID, out uint tcIDGuardado))
                {
                    BuildingPrivlidge tc = BaseNetworkable.serverEntities.Find(new NetworkableId(tcIDGuardado)) as BuildingPrivlidge;
                    if (tc != null)
                    {
                        uint tcID = (uint)tc.net.ID.Value;
                        if (procesosActivos.TryGetValue(tcID, out var infoProceso) && player.userID == infoProceso.Key)
                        {
                            infoProceso.Value?.Destroy();
                            procesosActivos.Remove(tcID);
                            PrintToChat(player, "<color=#FF0000><MiCustomTC></color> Has detenido tu proceso de mejora.");
                            CerrarTodoUI(player); 
                        }
                    }
                }
            }
        }

        void OnPlayerDisconnected(BasePlayer player)
        {
            preferenciasSkins.Remove(player.userID);
            tcAbierto.Remove(player.userID);
        }

        bool PuedeAbrirMenu(BuildingPrivlidge tc, int gradoObjetivo) => tc?.GetBuilding()?.buildingBlocks.Any(b => (int)b.grade <= gradoObjetivo) ?? false;

        void FinalizarTodo(BasePlayer player)
        {
            if (player != null && player.IsConnected) { CerrarTodoUI(player); DetenerProceso(player.userID); PrintToChat(player, "<color=#55ff55>¡Mejora interrumpida!</color>"); }
        }

        bool RecibioDaño(BuildingPrivlidge tc, out float tiempoRestante)
        {
            tiempoRestante = 0;
            var building = tc?.GetBuilding();
            if (building == null || building.buildingBlocks.Count == 0) return false;
            float ultimoAtaque = building.buildingBlocks.Max(b => b.lastAttackedTime);
            float diferencia = Time.realtimeSinceStartup - ultimoAtaque;
            if (diferencia < 600) tiempoRestante = 600 - diferencia;
            return tiempoRestante > 0;
        }

        void FinalizarProcesoTC(ulong tcID, BuildingPrivlidge tc = null)
        {
            if (procesosActivos.TryGetValue(tcID, out var info)) { info.Value?.Destroy(); procesosActivos.Remove(tcID); }
            LimpiarListaTC(tcID);
        }

        private void LimpiarListaTC(ulong tcID)
        {
            if (bloquesPendientes.TryGetValue(tcID, out List<BuildingBlock> lista))
            {
                if (lista != null) Facepunch.Pool.FreeUnmanaged(ref lista);
                bloquesPendientes.Remove(tcID);
            }
        }

        void DetenerProceso(ulong id) { if (procesosActivos.TryGetValue(id, out KeyValuePair<ulong, Timer> info)) info.Value?.Destroy(); procesosActivos.Remove(id); }

        void RefreshBlockConnections(BuildingBlock block) { block.UpdateSkin(); block.SendNetworkUpdate(BasePlayer.NetworkQueue.Update); }
        string GetFX(int g) => g == 0 || g == 1 ? "wood" : g == 2 ? "stone" : g == 3 ? "metal" : "toptier";

        int Contar(BuildingPrivlidge tc, string sn)
        {
            if (tc?.inventory?.itemList == null) return 0;
            int total = 0;
            var items = tc.inventory.itemList;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].info.shortname == sn) total += items[i].amount;
            }
            return total;
        }

        string GetResourceShortname(int g) => g switch { 1 => SN_MADERA, 2 => SN_PIEDRA, 3 => SN_METAL, _ => SN_HQM };
        int GetCost(int g) => g switch { 1 => 200, 2 => 300, 3 => 200, _ => 25 };
        int GetCostReskin(int grado) => Mathf.CeilToInt(GetCost(grado) * 0.5f);

        string GetColorRGBA(string name) => name switch
        {
            "AZUL" => "0.14 0.25 0.50 0.9", "VERDE" => "0.24 0.35 0.14 0.9", "MORADO" => "0.33 0.18 0.45 0.9",
            "MARRÓN" => "0.35 0.22 0.15 0.9", "NARANJA" => "0.75 0.35 0.10 0.9", "BLANCO" => "0.85 0.85 0.85 0.9",
            "NEGRO" => "0.05 0.05 0.05 0.9", "MARRÓN CLARO" => "0.50 0.35 0.25 0.9", "AZUL OSCURO" => "0.05 0.10 0.25 0.9",
            "VERDE OLIVA" => "0.25 0.28 0.15 0.9", "ROJO" => "0.65 0.10 0.10 0.9", "BEIGE" => "0.75 0.65 0.45 0.9",
            "AMARILLO" => "0.80 0.65 0.10 0.9", "GRIS" => "0.35 0.35 0.35 0.9", "VERDE AZULADO" => "0.10 0.45 0.40 0.9",
            "GRIS CLARO" => "0.55 0.55 0.55 0.9", "ALEATORIO" => "0.40 0.40 0.40 0.8", _ => "0.2 0.2 0.2 0.9"
        };

        private string RGBAtoHex(string rgba)
        {
            try {
                string[] p = rgba.Split(' ');
                return string.Format("#{0:X2}{1:X2}{2:X2}", (int)(float.Parse(p[0])*255), (int)(float.Parse(p[1])*255), (int)(float.Parse(p[2])*255));
            } catch { return "#ffffff"; }
        }

        void CerrarTodoUI(BasePlayer player) { CuiHelper.DestroyUi(player, MenuProgreso); CuiHelper.DestroyUi(player, MenuSkinsMaster); CuiHelper.DestroyUi(player, MenuColores); CuiHelper.DestroyUi(player, MenuMejoraTC); CuiHelper.DestroyUi(player, SubMenuSkins); CuiHelper.DestroyUi(player, MenuAlertaMejora); }
        void OnPlayerLootEnd(BasePlayer player) => CerrarTodoUI(player);
        void OnLootEntityEnd(BasePlayer player, BaseEntity entity) { if (entity is BuildingPrivlidge) CerrarTodoUI(player); }

        void Unload()
        {
            if (bloquesPendientes.Count > 0)
            {
                var keys = Facepunch.Pool.Get<List<ulong>>();
                keys.AddRange(bloquesPendientes.Keys);
                for (int i = 0; i < keys.Count; i++) DetenerProceso(keys[i]);
                Facepunch.Pool.FreeUnmanaged(ref keys);
                bloquesPendientes.Clear();
            }
            var harmony = new HarmonyLib.Harmony(this.Name);
            harmony.UnpatchAll(this.Name);
        }
        #endregion

        #region Harmony Patches
        [HarmonyPatch(typeof(BuildingBlock), "CheckWallpaper")]
        internal class BuildingBlock_CheckWallpaper
        {
            [HarmonyPrefix]
            static bool Prefix() => decayext; 
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
                    if (__instance.wallpaperHealth > 10) __instance.wallpaperHealth = __instance.health;
                    if (__instance.wallpaperHealth <= 0) __instance.RemoveWallpaper(0);
                    return false;
                }
                if (side == 1)
                {
                    if (__instance.wallpaperHealth2 > 10) __instance.wallpaperHealth2 = __instance.health;
                    if (__instance.wallpaperHealth2 <= 0) __instance.RemoveWallpaper(1);
                    return false;
                }
                return true;
            }
        }
        #endregion
    }
}