using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Configuration; 
using Rust;
using Rust.Ai;
using Newtonsoft.Json.Linq; 
using Oxide.Core.Plugins;   

namespace Oxide.Plugins
{
    [Info("MiHordaNocturna", "☠ Ⓜ🅘ⓖ🅤ⓔ🅛 ☠", "1.2.2")]
    [Description("IA Colmena: Sistema de doble dialéctica de terror con guardianes que protegen zona y atacan a distancia.")]
    class MiHordaNocturna : RustPlugin
    { 
        [PluginReference] Plugin NpcSpawn;
          
        private Configuration config;
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) LoadDefaultConfig();
            }
            catch
            {
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config, true);
        } 

        private int indiceActualizacion = 0;
        private List<Vector3> puntosSpawnAdmin = new List<Vector3>();
        private List<BaseNpc> hordaAsedio = new List<BaseNpc>();
        private Dictionary<ulong, HordeGroup> NpcsEnGrupo = new Dictionary<ulong, HordeGroup>();
        private List<HordeGroup> listaGrupos = new List<HordeGroup>();
        private Dictionary<ulong, bool> jugadoresEnCaza = new Dictionary<ulong, bool>();
        private Dictionary<ulong, int> killsSesionActual = new Dictionary<ulong, int>();
        private Dictionary<ulong, int> killsParaMostrarAlRespawn = new Dictionary<ulong, int>();
        private Dictionary<ulong, float> tiempoEnBase = new Dictionary<ulong, float>();
        
        // Control de ataque de NPCs
        private Dictionary<ulong, float> ultimoAtaqueBot = new Dictionary<ulong, float>();
        private const string PREFAB_SATCHEL = "assets/prefabs/weapons/satchelcharge/explosive.satchel.deployed.prefab";
        
        private Dictionary<string, List<ScientistNPC>> botsPorMonumento = new Dictionary<string, List<ScientistNPC>>();
        private Dictionary<string, float> cooldownMonumentos = new Dictionary<string, float>();
        private int mascaraConstruccion;
        private int mascaraSuelo;
        private int mascaraRocas;
        private int mascaraDañables;
        // Registro global para evitar huérfanos
        private HashSet<ScarecrowNPC> todosLosZombies = new HashSet<ScarecrowNPC>();
         
        // Capas y topologías extraídas de NpcSpawn para evitar rocas y subsuelo
        private const int CapasSuelo = 1 << 4 | 1 << 10 | 1 << 16 | 1 << 23 | 1 << 25; 
        private const int TopologiaBloqueada = (int)(TerrainTopology.Enum.Cliff | TerrainTopology.Enum.Cliffside |
                                          TerrainTopology.Enum.Ocean | TerrainTopology.Enum.Oceanside |
                                          TerrainTopology.Enum.Monument | TerrainTopology.Enum.Building |
                                          TerrainTopology.Enum.River | TerrainTopology.Enum.Riverside |
                                          TerrainTopology.Enum.Lake | TerrainTopology.Enum.Lakeside);
        private DynamicConfigFile dataFile;

        private Dictionary<ulong, JugadorData> rankingKillsNoche = new Dictionary<ulong, JugadorData>();
        public class JugadorData
        {
            public string Nombre;
            public int Kills;
        }

        private bool NocheActivada = false;

        private Dictionary<ulong, Timer> timersActivos = new Dictionary<ulong, Timer>();
        
        private string GetRandomLangMessage(string prefix, string userId)
        {
            string playerLang = lang.GetLanguage(userId) ?? "en";
            var allMessages = lang.GetMessages(playerLang, this);
            if (allMessages == null || allMessages.Count == 0) 
                return "¡NO CIERRES LOS OJOS! Queremos que veas lo que viene.";

            List<string> filteredKeys = Facepunch.Pool.Get<List<string>>();

            try
            {
                var enumerador = allMessages.GetEnumerator();
                while (enumerador.MoveNext())
                {
                    if (enumerador.Current.Key.StartsWith(prefix))
                    {
                        filteredKeys.Add(enumerador.Current.Key);
                    }
                }

                if (filteredKeys.Count == 0) 
                    return "¡NO CIERRES LOS OJOS! Queremos que veas lo que viene.";

                int randomIndex = UnityEngine.Random.Range(0, filteredKeys.Count);
                return lang.GetMessage(filteredKeys[randomIndex], this, userId);
            }
            finally
            {
                filteredKeys.Clear();
                Facepunch.Pool.FreeUnmanaged(ref filteredKeys);
            }
        }
        
        protected override void LoadDefaultMessages()
        {
            var esMessages = new Dictionary<string, string>
            {
                ["Announce.Nigth"] = "<color=#ff4d4d>LA NOCHE HA LLEGADO:</color> La horda se despertó hambrienta.",
                ["Announce.Day"] = "<color=#55aaff>LA HORDA:</color> La horda se retira a las sombras.",
                ["Announce.BaseScent"] = "<color=red>⚠️ LA HORDA HA OLIDO UNA BASE ⚠️</color>",
                ["Announce.RunToBase"] = "<color=#ff4d4d>Se dirigen hacia la zona de</color> <color=white>{0}</color>",
                ["Announce.TopKills"] = "<color=yellow>★ TOP ASESINOS DE LA NOCHE:</color>\n",
                ["Player.Wave"] = "<color=#ff4d4d>[HORDE]</color> ¡Oleada {0} eliminada!",
                ["Player.NextWave"] = "<color=#55aaff>Siguiente desafío:</color> Oleada {0} en 30 segundos...",
                ["Player.CombateCaido"] = "<color=#ff4d4d>[HORDA]</color> Caíste en combate. Eliminaste a <color=yellow>{0}</color> enemigos.",
                ["Player.AsedioRoto"] = "<color=#55ff55>[HORDA]</color> Has roto el asedio. Los supervivientes se han retirado.",
                ["Player.AsedioLive"] = "<color=#55aaff>[HORDA]</color> Has sobrevivido al asedio. Por ahora...",
                ["Player.AsedioBase"] = "<color=#ff4d4d>[HORDA]</color> ¡Has permanecido demasiado tiempo oculto! El asedio comienza...",
                ["Admin.ShowHorda"] = "<color=#55aaff>Mostrando punto {0}...</color>",
                ["Admin.AddHorda"] = "<color=#55ff55>Punto añadido.</color> Total: {0}",
                ["Admin.RemHorda"] = "<color=#ff5555>Punto de spawn eliminado correctamente.</color>",
                ["Admin.EmpyPoints"] = "<color=#ff5555>No hay puntos de horda configurados.</color>",
                ["Admin.RemHordaFail"] = "<color=#ffff55>No hay ningún punto de spawn lo suficientemente cerca para borrarlo.</color> (Acércate a menos de 5m)",
                ["Admin.Clean"] = "<color=#ff4d4d>[HORDA]</color> Limpieza completada. Se eliminaron <color=yellow>{0}</color> NPCs y se resetearon los datos.",
                ["Admin.HordaPlayerAdmin"] = "<color=#ff4d4d>[HORDA]</color> Caza INFINITA iniciada para {0}.",
                ["Admin.NoPlayer"] = "Jugador no encontrado.",
                ["Admin.HordaPlayerTarget"] = "<color=#ff4d4d>[HORDA]</color> La noche te ha marcado. No habrá descanso hasta el amanecer o tu muerte.",
                ["Admin.HordaPlayerEnd"] = "<color=#55aaff>[HORDA]</color> Caza finalizada para {0}.",

                ["Detection.1"] = "¡TE VEMOS! Corre mientras tus piernas aún funcionen.",
                ["Detection.2"] = "Carne fresca detectada... la cacería comienza AHORA.",
                ["Detection.3"] = "No deberías estar aquí a estas horas, humano.",
                ["Detection.4"] = "¡MÍRANOS! Somos tu final. ¡HUYE!",
                ["Detection.5"] = "El olor de tu sangre nos guía... ¡CORRE!",
                ["Detection.6"] = "¡AHÍ ESTÁS! Tus latidos te delatan.",
                ["Detection.7"] = "No hay sombra que pueda ocultar tu miedo.",
                ["Detection.8"] = "¡TE ENCONTRAMOS! La cena está servida.",
                ["Detection.9"] = "¡SANGRE CALIENTE! Mi favorita...",
                ["Detection.10"] = "¿Oyes eso? Es el sonido de tu vida escapándose.",
                ["Detection.11"] = "¡NO CIERRES LOS OJOS! Queremos que veas lo que viene.",
                ["Detection.12"] = "¡RATA DETECTADA! Es hora de limpiar este lugar.",
                ["Detection.13"] = "Tus pasos son ruidosos... y deliciosos.",
                ["Detection.14"] = "¡TE TENGO! No me hagas perseguirte demasiado.",
                ["Detection.15"] = "Esa piel se ve tan... suave. ¡VAMOS A RASGARLA!",
                ["Detection.16"] = "¡MÍRAME A LA CARA! Soy lo último que recordarás.",
                ["Detection.17"] = "El pánico te sienta bien, humano.",
                ["Detection.18"] = "¡SÁCALE LAS ENTRAÑAS! ¡QUE NO SE ESCAPE!",
                ["Detection.19"] = "¿A dónde vas? Si la fiesta acaba de empezar.",
                ["Detection.20"] = "¡OTRA VÍCTIMA! El banquete nunca termina.",
                ["Detection.21"] = "¡TE VEO! Tu alma brilla como una vela en el viento.",
                ["Detection.22"] = "¡GRITA! Nadie te oirá, pero a nosotros nos encanta.",
                ["Detection.23"] = "¡CERCA, ESTÁ MUY CERCA! ¡A POR ÉL!",
                ["Detection.24"] = "¿Creías que eras invisible? Qué tierno.",
                ["Detection.25"] = "Tu sudor apesta a cobardía... ¡Y ME ENCANTA!",
                ["Detection.26"] = "¡ALTO! Deja que probemos un trozo de ti.",
                ["Detection.27"] = "¡CORRE, PEQUEÑO! Haz que la caza sea divertida.",
                ["Detection.28"] = "Tus pulmones arderán antes de que logres escapar.",
                ["Detection.29"] = "¡TE ENCONTRÉ! No te muevas, dolerá menos.",
                ["Detection.30"] = "¡CUIDADO! La muerte tiene ojos... y son los míos.",
                ["Detection.31"] = "¿Siente ese escalofrío? Soy yo, justo detrás de ti.",
                ["Detection.32"] = "¡MÁS CARNE PARA EL MATADERO!",
                ["Detection.33"] = "¡TUS HUESOS CRUJIRÁN BAJO MIS PIES!",
                ["Detection.34"] = "No hay donde esconderse cuando el hambre manda.",
                ["Detection.35"] = "¡AHÍ ESTÁ! ¡CORTADLE EL PASO!",
                ["Detection.36"] = "Tu viaje termina en este rincón oscuro.",
                ["Detection.37"] = "¡TU CORAZÓN GOLPEA TU PECHO COMO UN TAMBOR!",
                ["Detection.38"] = "¡POR FIN! Alguien con quien jugar...",

                ["Siege.1"] = "¿Crees que esa altura te salvará? La gravedad es nuestra amiga.",
                ["Siege.2"] = "Esos muros se sienten seguros ahora, pero el metal se pudre.",
                ["Siege.3"] = "Te esperaremos aquí abajo. No puedes quedarte ahí para siempre.",
                ["Siege.4"] = "La zona segura no es eterna. El tiempo corre en tu contra.",
                ["Siege.5"] = "El hambre es paciente. Nosotros también.",
                ["Siege.6"] = "Esa puerta es de madera, y nosotros somos el fuego.",
                ["Siege.7"] = "Disfruta de tu pequeña jaula... pronto será tu tumba.",
                ["Siege.8"] = "Podemos oler tu pánico a través de las grietas.",
                ["Siege.9"] = "Puedo rasguñar este muro toda la noche si es necesario.",
                ["Siege.10"] = "Esa luz se apagará pronto... y nosotros estaremos en la oscuridad.",
                ["Siege.11"] = "Tarde o temprano, tendrás que salir a por aire.",
                ["Siege.12"] = "Estamos afilando los dientes con los cimientos de tu casa.",
                ["Siege.13"] = "¿Escuchas eso? Es el sonido de tu refugio cediendo.",
                ["Siege.14"] = "No estamos encerrados contigo, tú estás encerrado con nosotros.",
                ["Siege.15"] = "Incluso las fortalezas más altas tienen grietas.",
                ["Siege.16"] = "El amanecer está lejos, y nosotros somos muy pacientes.",
                ["Siege.17"] = "¿Qué harás cuando se te acaben las balas?",
                ["Siege.18"] = "Tu escondite huele a miedo y desesperación.",
                ["Siege.19"] = "Golpea la puerta todo lo que quieras, nadie vendrá a ayudarte.",
                ["Siege.20"] = "Solo eres un regalo envuelto en piedra y metal.",
                ["Siege.21"] = "Estamos contando cada uno de tus suspiros desde aquí abajo.",
                ["Siege.22"] = "Ese techo no detendrá nuestra hambre.",
                ["Siege.23"] = "Tus defensas son solo una sugerencia para nosotros.",
                ["Siege.24"] = "¿Te sientes seguro? Mira hacia arriba... siempre hay un hueco.",
                ["Siege.25"] = "El silencio no te salvará; sabemos que sigues ahí.",
                ["Siege.26"] = "Cada segundo que pasas ahí dentro, nos volvemos más salvajes.",
                ["Siege.27"] = "Podemos esperar. El hambre es un maestro de la paciencia.",
                ["Siege.28"] = "Esa cerradura no aguantará nuestro peso mucho más.",
                ["Siege.29"] = "¿Estás rezando? Tus dioses no pueden entrar aquí.",
                ["Siege.30"] = "Tus muros son fuertes, pero tu voluntad es débil.",
                ["Siege.31"] = "Sentimos el calor de tu cuerpo a través del concreto.",
                ["Siege.32"] = "Disfruta de tu último santuario antes de que lo convirtamos en polvo.",
                ["Siege.33"] = "Estamos excavando... la tierra también es nuestra.",
                ["Siege.34"] = "No eres el primero que intenta esconderse aquí. Todos fallaron.",
                ["Siege.35"] = "El metal llora bajo nuestras garras. ¿Lo oyes?",
                ["Siege.36"] = "Tu refugio es solo un ataúd con mejores vistas.",
                ["Siege.37"] = "Sal y acepta tu destino. Es más rápido así.",
                ["Siege.38"] = "Pronto solo quedarán tus ecos en estas paredes."
            };
            lang.RegisterMessages(esMessages, this, "es");
            lang.RegisterMessages(esMessages, this, "es-ES");
        }

        private string[] monumentosProhibidos = {
            "bandit", "excavator", "silo", "military", "lab",
            "oil_rig", "outpost", "ranch", "fishing", "stable"
        };
        
        public enum HordeType
        {
            Nocturno,   // Spawn ambiental nocturno
            Asedio,     // El castigo por estar mucho tiempo en base
            Oleadas     // Eventos de nivel (HordaPlayer o similares)
        }

        private int contadorNoches = 0; // Añade esta variable global

		class HordeGroup
		{	
			public float CreationTime = Time.realtimeSinceStartup;
			public List<ScarecrowNPC> Members = new List<ScarecrowNPC>();
			public HordeType Type = HordeType.Nocturno;
			public BasePlayer Target;
			public string MonumentoId;
			public int WaveLevel = 1;
			public int DifficultyTier = 0;
			public float LastMessageTime = 0f;
			public float LastExplosiveTime = 0f;
			public Vector3 LastAnchorPos;
			public float LastLeaderDeathTime = 0f;
			public float LastAlertTime;
			public float LastTargetScanTime;
			public int SpawnNight = 0; // <-- NUEVO: Rastrea la noche en la que spawneó
			
			public bool IsPlayerOleadas => Type == HordeType.Oleadas;
		}

        private Dictionary<ulong, int> ultimaNocheAsedio = new Dictionary<ulong, int>();

        // ==========================================
        // INICIALIZACIÓN
        // ==========================================
        private List<BasePlayer> jugadoresValidos = new List<BasePlayer>();
        // Borra tu variable "monumentosCache" y cámbiala por esto:
		private class MonumentData 
		{
			public MonumentInfo Info;
			public string Id;
		}
		private List<MonumentData> monumentosCache = new List<MonumentData>();
        
        void OnServerInitialized()
        {
            monumentosCache.Clear();
            NpcsEnGrupo.Clear();
            listaGrupos.Clear();

            string[] listaConfig = config.MonumentosGuardianes; 
            var todosLosMonumentos = TerrainMeta.Path.Monuments;

            if (listaConfig != null && todosLosMonumentos != null)
            {
                foreach (var m in todosLosMonumentos)
                {
                    string nombreMonumento = m.name.ToLower();
                    for (int j = 0; j < listaConfig.Length; j++) 
                    {
                        if (string.IsNullOrEmpty(listaConfig[j])) continue;

                        if (nombreMonumento.Contains(listaConfig[j].ToLower()))
                        {
                            // Calculamos el ID aquí y lo guardamos para siempre
                            string id = $"mon_{nombreMonumento}_{Mathf.FloorToInt(m.transform.position.x)}_{Mathf.FloorToInt(m.transform.position.z)}";
                            monumentosCache.Add(new MonumentData { Info = m, Id = id });
                            break;
                        }
                    }
                }
            }

            mascaraConstruccion = LayerMask.GetMask("Construction", "Deployed", "Default", "Terrain", "World");
            mascaraSuelo = LayerMask.GetMask("Terrain", "World", "Default", "Construction", "Deployed");
            mascaraRocas = LayerMask.GetMask("World");
            mascaraDañables = LayerMask.GetMask("Construction", "Default", "Deployed", "Player (Server)", "AI");

            timerLoopPrincipal = timer.Every(10f, () => {timerIAColmena = timer.Repeat(1.0f, 0, ActualizarIAColmena);
                jugadoresValidos.Clear();
                
                var listaActiva = BasePlayer.activePlayerList;
                for (int i = 0; i < listaActiva.Count; i++)
                {
                    BasePlayer p = listaActiva[i];
                    if (p != null && p.IsConnected && !p.IsSleeping() && !p.IsAlive() == false && !p.InSafeZone())
                    {
                        jugadoresValidos.Add(p);
                    }
                }
                
                RevisarTiempo();
                if (config.Guardianes) SpawnGuardianesDeMonumento();
                if (config.Asedio) ActualizarTiemposBase();
                
                for (int i = listaGrupos.Count - 1; i >= 0; i--)
                {
                    var g = listaGrupos[i];
                    if (g == null || g.Members == null)
                    {
                        listaGrupos.RemoveAt(i);
                        continue;
                    }

                    bool todosMuertos = true; 
                    for (int j = 0; j < g.Members.Count; j++)
                    {
                        var m = g.Members[j];
                        if (m != null && !m.IsDead())
                        {
                            todosMuertos = false;
                            break;
                        }
                    }

                    if (todosMuertos)
                    {
                        listaGrupos.RemoveAt(i);
                    }
                }

                float tiempoActual = Time.realtimeSinceStartup;
                List<string> clavesEliminar = Facepunch.Pool.Get<List<string>>();
                
                try {
                    var enumerador = cooldownMonumentos.GetEnumerator();
                    while (enumerador.MoveNext())
                    {
                        if (enumerador.Current.Value <= tiempoActual - 1800f)
                            clavesEliminar.Add(enumerador.Current.Key);
                    }

                    for (int i = 0; i < clavesEliminar.Count; i++)
                    {
                        cooldownMonumentos.Remove(clavesEliminar[i]);
                    }
                }
                finally {
                    clavesEliminar.Clear();
                    Facepunch.Pool.FreeUnmanaged(ref clavesEliminar);
                }
            });
            
            timerIAColmena = timer.Repeat(1.0f, 0, ActualizarIAColmena);
            Puts($"MiHordaNocturna: {monumentosCache.Count} monumentos registrados. IA iniciada.");
        }
		private Timer timerLoopPrincipal;
		private Timer timerIAColmena;
		
		void OnServerSave()
		{
			SaveData();
		}
		
        void Loaded()
        {
            var dataFileSystem = Interface.Oxide.DataFileSystem;
            dataFile = dataFileSystem.GetFile("HordaNocturna_Puntos");
            
            LoadConfig();

            try 
            {
                var data = dataFile.ReadObject<PluginData>();
                if (data != null)
                {
                    if (data.Puntos != null)
                    {
                        puntosSpawnAdmin.Clear();
                        for (int i = 0; i < data.Puntos.Count; i++)
                        {
                            var v = data.Puntos[i];
                            puntosSpawnAdmin.Add(new Vector3(v.x, v.y, v.z));
                        }
                    }
                    else 
                    {
                        puntosSpawnAdmin = new List<Vector3>();
                    }

                    ultimaNocheAsedio = data.CooldownsAsedio ?? new Dictionary<ulong, int>();
                }
                else 
                {
                    ResetearDatos();
                }
            }
            catch (Exception)
            {
                PrintWarning("Archivo de datos corrupto o incompatible. Creando uno nuevo...");
                ResetearDatos();
            }
        }
        
        void ResetearDatos()
        {
            puntosSpawnAdmin = new List<Vector3>();
            ultimaNocheAsedio = new Dictionary<ulong, int>();
            SaveData();
        }

        void OnNewSave(string filename)
        {
            PrintWarning("Nuevo Wipe detectado. Limpiando datos de hordas y treguas...");
            ResetearDatos();
        }

		void Unload()
		{
			int eliminados = 0;
			timerLoopPrincipal?.Destroy();
			timerIAColmena?.Destroy();

			// 1. Limpieza de los NPCs de los Monumentos
			if (botsPorMonumento != null)
			{
				foreach (var entry in botsPorMonumento)
				{
					var listaBots = entry.Value;
					if (listaBots == null) continue;

					// Iteración hacia atrás para eliminación segura
					for (int i = listaBots.Count - 1; i >= 0; i--)
					{
						var npc = listaBots[i];
						if (npc != null && !npc.IsDestroyed)
						{
							npc.Kill();
							eliminados++;
						}
					}
				}
				botsPorMonumento.Clear();
			}

			// 2. Limpieza de Zombies (Tu lógica original)
			if (todosLosZombies != null)
			{
				for (int i = todosLosZombies.Count - 1; i >= 0; i--)
				{
					var zombie = todosLosZombies.ElementAtOrDefault(i);
					if (zombie != null && !zombie.IsDestroyed)
					{
						zombie.Kill();
						eliminados++;
					}
				}
				todosLosZombies.Clear();
			}

			// 3. Limpieza de Timers
			if (timersActivos != null)
			{
				foreach (var timerEntry in timersActivos)
				{
					timerEntry.Value?.Destroy();
				}
				timersActivos.Clear();
			}

			// 4. Limpieza de colecciones de estado
			NpcsEnGrupo?.Clear();
			listaGrupos?.Clear();
			
			// Limpieza de cooldowns si los tienes guardados en memoria
			if (cooldownMonumentos != null)
			{
				cooldownMonumentos.Clear();
			}
			
			PrintWarning($"Limpieza completa: {eliminados} NPCs (Guardianes y Zombies) eliminados de forma segura.");
		}

        void SaveData()
        {
            List<Vector3Serialized> puntosSerializados = Facepunch.Pool.Get<List<Vector3Serialized>>();

            try
            {
                for (int i = 0; i < puntosSpawnAdmin.Count; i++)
                {
                    Vector3 v = puntosSpawnAdmin[i];
                    puntosSerializados.Add(new Vector3Serialized { x = v.x, y = v.y, z = v.z });
                }

                var data = new PluginData 
                {
                    Puntos = puntosSerializados,
                    CooldownsAsedio = ultimaNocheAsedio
                };

                dataFile.WriteObject(data);
            }
            finally
            {
                puntosSerializados.Clear();
                Facepunch.Pool.FreeUnmanaged(ref puntosSerializados);
            }
        }

        // ==========================================
        // SISTEMA DE TIEMPO Y BASES
        // ==========================================
        void ActualizarTiemposBase()
		{
			if (!NocheActivada) return;

			// 1. Actualización de tiempos
			foreach (var player in jugadoresValidos)
			{
				if (player == null || !player.IsAlive() || player.IsSleeping()) continue;

				if (player.GetBuildingPrivilege() != null)
				{
					// TryGetValue es más eficiente que ContainsKey + Acceso
					if (!tiempoEnBase.TryGetValue(player.userID, out float tiempo))
						tiempo = 0f;
					
					tiempoEnBase[player.userID] = tiempo + 10f; 
				}
				else
				{
					if (tiempoEnBase.TryGetValue(player.userID, out float tiempo))
					{
						// Math.Max evita el if anidado y es más limpio
						tiempoEnBase[player.userID] = Math.Max(0f, tiempo - 10f);
					}
				}
			}

			// 2. Limpieza de jugadores desconectados (Sin crear nuevos diccionarios)
			// Usamos una lista temporal solo para las llaves, evitando recrear todo el diccionario
			List<ulong> paraEliminar = null;
			
			foreach (var kvp in tiempoEnBase)
			{
				if (BasePlayer.FindByID(kvp.Key) == null)
				{
					if (paraEliminar == null) paraEliminar = new List<ulong>();
					paraEliminar.Add(kvp.Key);
				}
			}

			if (paraEliminar != null)
			{
				foreach (var id in paraEliminar)
				{
					tiempoEnBase.Remove(id);
				}
			}
		}
				
		private void BroadcastLocalized(string key, params object[] args)
		{
			foreach (var player in BasePlayer.activePlayerList)
			{
				if (player == null || !player.IsConnected) continue;

				string message = lang.GetMessage(key, this, player.UserIDString);

				if (message == key)
				{
					message = lang.GetMessage(key, this, "en");
				}

				string finalMessage = (args.Length > 0) ? string.Format(message, args) : message;
				SendReply(player, finalMessage);
			}
		}
				
        private int bonusActual = 0;
        
        void RevisarTiempo()
		{
			if (TOD_Sky.Instance == null) return;
			float h = TOD_Sky.Instance.Cycle.Hour;
			bool noche = (h >= 19.5f || h < 8.0f);

			if (noche && !NocheActivada)
			{
				NocheActivada = true;
				contadorNoches++; // <-- Incrementamos la noche
				BroadcastLocalized("Announce.Nigth");

				// <-- NUEVO: Despawn de asedios de noches anteriores
				for (int i = listaGrupos.Count - 1; i >= 0; i--)
				{
					var g = listaGrupos[i];
					if (g != null && g.Type == HordeType.Asedio && g.SpawnNight < contadorNoches)
					{
						EliminarGrupoEfecto(g);
					}
				}

				if (config.NocturnosPorDia != 0)
				{
					int diasSeparacion = Math.Max(1, config.CadaXdiasDeWipeMasNocturnos);
					TimeSpan tiempoDesdeWipe = DateTime.Now - SaveRestore.SaveCreatedTime;
					bonusActual = Math.Min((tiempoDesdeWipe.Days / diasSeparacion) * config.NocturnosPorDia, 50);
				}

				timer.Once(400f, () =>
				{
					if (!NocheActivada) return;
					var target = BuscarJugadorMasTiempoEnCasa();
					if (target != null && target.IsConnected)
					{
						BroadcastLocalized("Announce.BaseScent");
						BroadcastLocalized("Announce.RunToBase", target.displayName);
						SpawnGrupoColmena(target.transform.position, target, HordeType.Asedio);
						SendReply(target, lang.GetMessage("Player.AsedioBase", this, target.UserIDString));
						Effect.server.Run("assets/bundled/prefabs/fx/player/howl.prefab", target.transform.position);
					}
				});
			}
			else if (!noche && NocheActivada)
			{
				NocheActivada = false;
				BroadcastLocalized("Announce.Day");
				LimpiarHordasAmbientales();
				tiempoEnBase.Clear();

				if (rankingKillsNoche.Count > 0)
				{
					// OPTIMIZACIÓN: En lugar de LINQ, usamos una lista para ordenar manualmente.
					// Esto genera mucha menos basura que OrderByDescending + Take + Select.
					var listaOrdenada = rankingKillsNoche.Values.ToList();
					listaOrdenada.Sort((a, b) => b.Kills.CompareTo(a.Kills));

					// Construimos el string de forma eficiente usando StringBuilder
					var sb = new System.Text.StringBuilder();
					for (int i = 0; i < Math.Min(3, listaOrdenada.Count); i++)
					{
						sb.Append($"<color=#ff4d4d>•</color> {listaOrdenada[i].Nombre}: <color=#55aaff>{listaOrdenada[i].Kills}</color>\n");
					}
					string cuerpoRanking = sb.ToString().TrimEnd();

					// Cacheamos solo el título por idioma
					Dictionary<string, string> titulosCache = new Dictionary<string, string>();

					foreach (var player in BasePlayer.activePlayerList)
					{
						if (player == null || !player.IsConnected) continue;

						string langCode = player.net?.connection?.language ?? "en";
						if (!titulosCache.TryGetValue(langCode, out string titulo))
						{
							titulo = lang.GetMessage("Announce.TopKills", this, player.UserIDString);
							titulosCache[langCode] = titulo;
						}
						SendReply(player, $"{titulo}\n{cuerpoRanking}");
					}
					rankingKillsNoche.Clear();
				}
			}

			// Mantenemos el timer para evitar ejecuciones bloqueantes en el mismo frame
			timer.Once(2f, () => {
				if (NocheActivada)
				{
					SpawnHordasAdmin();
					ChequearRespawnNocturno();
				}
			});
		}

        // ==========================================
        // LÓGICA DE SPAWN
        // ==========================================
		void SpawnHordasAdmin()
		{
			// Verificación rápida
			if (puntosSpawnAdmin == null || puntosSpawnAdmin.Count == 0) return;

			// OPTIMIZACIÓN: Usamos un contador para evitar saturar el servidor si hay muchos puntos.
			// También usamos un bucle 'for' para evitar el enumerador de 'foreach' (menos basura en GC).
			for (int i = 0; i < puntosSpawnAdmin.Count; i++)
			{
				Vector3 punto = puntosSpawnAdmin[i];
				
				// El SamplePosition es una operación costosa. Si tienes muchos puntos,
				// no ejecutes todos en el mismo frame.
				NavMeshHit hit;
				if (NavMesh.SamplePosition(punto, out hit, 10f, NavMesh.AllAreas))
				{
					// Pasamos una pequeña demora para que los spawns no ocurran en el mismo milisegundo
					timer.Once(0.5f * i, () => 
					{
						SpawnGrupoColmena(hit.position, null, HordeType.Nocturno);
					});
				}
			}
		}

		// Método para escanear el equipamiento del jugador y devolver el Nivel de Amenaza (1-6)
		private int CalcularNivelAmenaza(BasePlayer player)
		{
			if (player == null) return 2; 
			float score = 0;

			// 1. Evaluar el arma activa 
			// (Mantenemos esto estático, ya que el daño base del arma es el mejor indicador del potencial de fuego)
			Item armaActiva = player.GetActiveItem();
			if (armaActiva != null && armaActiva.info != null)
			{
				string shortnameArma = armaActiva.info.shortname;
				
				switch (shortnameArma)
				{
					case "rocket.launcher": case "lmg.m249": case "hmlmg": score += 45; break;
					case "rifle.ak": case "rifle.lr300": case "shotgun.m4": case "rifle.bolt": case "rifle.l96": score += 35; break;
					case "rifle.semiauto": case "smg.thompson": case "smg.2": score += 20; break;
					case "pistol.revolver": case "shotgun.double": case "pistol.semiauto": case "shotgun.pump": score += 10; break;
					case "bow.hunting": case "bow.compound": case "crossbow": case "pistol.eoka": score += 5; break;
				}
			}

			// 2. Evaluar armadura (MODIFICADO: Cálculo dinámico de protección real)
			if (player.inventory?.containerWear != null)
			{
				float proteccionBalaTotal = 0f;

				// Usamos un bucle 'for' en lugar de 'foreach' para mejor rendimiento (menos Garbage Collection)
				for (int i = 0; i < player.inventory.containerWear.itemList.Count; i++)
				{
					Item item = player.inventory.containerWear.itemList[i];
					if (item?.info == null) continue;
					
					// Extraemos el componente de armadura del objeto
					ItemModWearable wearable = item.info.GetComponent<ItemModWearable>();
					if (wearable != null && wearable.protectionProperties != null)
					{
						// Obtenemos el decimal de protección (Ej: Chest de metal da 0.25, que es 25%)
						proteccionBalaTotal += wearable.protectionProperties.Get(DamageType.Bullet);
					}
				}

				// CONVERSIÓN DE PUNTOS:
				// Un set Full Metal suma ~1.15 en total de protección a balas. Un Hazmat suma 0.30.
				// Multiplicamos este valor bruto por 30f para equipararlo a tu antigua escala de puntos.
				// Ej: Full Metal (1.15 * 30 = ~34.5 pts) | Hazmat (0.30 * 30 = 9 pts) | Primitivo (~5 pts)
				score += (proteccionBalaTotal * 30f); 
			}
			//SendReply(player, $"Puntos de Amenaza:\n{score}");
			
			// 3. Rangos de corte finales
			if (score <= 5)   return 1;
			if (score <= 15)  return 2;
			if (score <= 30)  return 3;
			if (score <= 45)  return 4;
			if (score <= 60)  return 5;
			return 6; 
		}

		// Método rápido para encontrar al jugador activo más cercano al monumento
		private BasePlayer ObtenerJugadorMasCercano(Vector3 posicion, float radio)
		{
			BasePlayer jugadorCercano = null;
			float distanciaMinima = radio;

			foreach (BasePlayer player in BasePlayer.activePlayerList)
			{
				if (player == null || !player.IsConnected || player.IsSleeping() || !player.IsAlive()) continue;
 
				float distancia = Vector3.Distance(posicion, player.transform.position);
				if (distancia < distanciaMinima)
				{
					distanciaMinima = distancia;
					jugadorCercano = player;
				}
			}
			return jugadorCercano;
		}
		private Dictionary<string, int> spawnOriginalCount = new Dictionary<string, int>();

		void SpawnGuardianesDeMonumento() 
{
	if (monumentosCache == null || monumentosCache.Count == 0) return;
	float tiempoActual = Time.realtimeSinceStartup;

	for (int i = 0; i < monumentosCache.Count; i++)
	{
		var mData = monumentosCache[i];
		if (mData == null || mData.Info == null) continue;

		string id = mData.Id;

		if (!botsPorMonumento.TryGetValue(id, out List<ScientistNPC> listaBots))
		{
			listaBots = new List<ScientistNPC>();
			botsPorMonumento[id] = listaBots;
		}

		// 1. Limpieza de muertos
		listaBots.RemoveAll(x => x == null || x.IsDestroyed || x.IsDead());

		// 2. ¿Jugador cerca?
		bool jugadorCerca = false;
		foreach (var p in BasePlayer.activePlayerList)
		{
			if (p != null && p.IsConnected && !p.IsSleeping() && p.IsAlive() && !p.InSafeZone())
			{
				if ((p.transform.position - mData.Info.transform.position).sqrMagnitude < 14400f)
				{
					jugadorCerca = true;
					break;
				}
			}
		}

		// 3. LÓGICA DE CONTROL (Cuando el jugador se aleja)
		if (!jugadorCerca)
		{
			if (listaBots.Count > 0)
			{
				int totalInicial = spawnOriginalCount.ContainsKey(id) ? spawnOriginalCount[id] : config.CantidadGuardianesPorGrupo;
				int muertos = totalInicial - listaBots.Count;

				if (muertos > (totalInicial / 2))
				{
					cooldownMonumentos[id] = tiempoActual + 600f; // 10 minutos
				}
				else
				{
					cooldownMonumentos.Remove(id);
				}

				for (int j = listaBots.Count - 1; j >= 0; j--)
				{
					if (listaBots[j] != null) listaBots[j].Kill();
				}
				listaBots.Clear();
				spawnOriginalCount.Remove(id); // Limpiamos rastro
			}
			continue; 
		}

		// 4. LÓGICA DE DETECCIÓN DE VICTORIA DEL JUGADOR
		if (listaBots.Count == 0 && spawnOriginalCount.ContainsKey(id))
		{
			// Forzamos el cooldown inmediatamente para bloquear el respawn infinito
			cooldownMonumentos[id] = tiempoActual + 600f;
			spawnOriginalCount.Remove(id); // Ya registramos el cooldown, quitamos la marca
			continue; // Saltamos para que no spawnee nada en este tick
		}

		// 5. LÓGICA DE ONDA
		if (listaBots.Count > 0) continue;

		// Verificamos el cooldown
		if (cooldownMonumentos.TryGetValue(id, out float cTime) && tiempoActual < cTime)
		{
			continue; 
		}

		// 6. SPAWN TOTAL
		spawnOriginalCount[id] = config.CantidadGuardianesPorGrupo;

		Vector3 monumentPos = mData.Info.transform.position;
		BasePlayer jugadorObjetivo = ObtenerJugadorMasCercano(monumentPos, 150f);
		int nivelAmenaza = CalcularNivelAmenaza(jugadorObjetivo);
		
		float multiplicadorVida = 1.0f, modificadorDaño = 0.8f, velocidadNpc = 6.5f, dispersionBala = 0.8f, escalaVisual = 1.0f;
		string prefijoNombre = "Guardian", armaBotShortname = config.ArmaGuardian.shortname;
		ulong armaBotSkin = config.ArmaGuardian.skin;

		switch (nivelAmenaza)
		{
			case 1: prefijoNombre = "亗 Guardian Recluta"; multiplicadorVida = 0.75f; modificadorDaño = 0.5f; velocidadNpc = 5.5f; dispersionBala = 1.3f; escalaVisual = 0.95f; armaBotShortname = config.ArmaGuardian.shortname; armaBotSkin = config.ArmaGuardian.skin; break;
			case 2: prefijoNombre = "☆ Guardian Común"; multiplicadorVida = 1.0f; modificadorDaño = 0.8f; velocidadNpc = 6.5f; dispersionBala = 0.8f; escalaVisual = 1.0f; armaBotShortname = "pistol.semiauto"; armaBotSkin = 0; break;
			case 3: prefijoNombre = "㋡ Guardian Avanzado"; multiplicadorVida = 1.35f; modificadorDaño = 1.1f; velocidadNpc = 7.2f; dispersionBala = 0.6f; escalaVisual = 1.06f; armaBotShortname = "smg.2"; armaBotSkin = 0; break;
			case 4: prefijoNombre = "メ Guardian Élite"; multiplicadorVida = 1.85f; modificadorDaño = 1.5f; velocidadNpc = 7.8f; dispersionBala = 0.4f; escalaVisual = 1.12f; armaBotShortname = "smg.thompson"; armaBotSkin = 0; break;
			case 5: prefijoNombre = "♤ Guardian Veterano"; multiplicadorVida = 2.8f; modificadorDaño = 2.1f; velocidadNpc = 8.5f; dispersionBala = 0.2f; escalaVisual = 1.18f; armaBotShortname = "rifle.semiauto"; armaBotSkin = 0; break;
			case 6: prefijoNombre = "★ GUARDIAN BOSS"; multiplicadorVida = 4.5f; modificadorDaño = 2.8f; velocidadNpc = 9.2f; dispersionBala = 0.05f; escalaVisual = 1.35f; armaBotShortname = "rifle.ak"; armaBotSkin = 0; break;
		} 

		for (int u = 0; u < config.CantidadGuardianesPorGrupo; u++)
		{
			Vector3 spawnPos = monumentPos;
			bool found = false;

			for (int attempt = 0; attempt < 25; attempt++)
			{
				Vector2 circle = UnityEngine.Random.insideUnitCircle * 25f;
				Vector3 testPos = new Vector3(monumentPos.x + circle.x, monumentPos.y + 100f, monumentPos.z + circle.y);
				if (Physics.Raycast(testPos, Vector3.down, out RaycastHit hitFloor, 200f, mascaraSuelo))
				{
					Vector3 groundPos = hitFloor.point;
					if (WaterLevel.GetWaterDepth(groundPos, true, true, null) > 0.1f) continue;
					if (Physics.Raycast(groundPos + new Vector3(0, 0.5f, 0), Vector3.up, out RaycastHit hitRoof, 10f, mascaraConstruccion | mascaraSuelo | mascaraRocas)) continue;
					int topology = TerrainMeta.TopologyMap.GetTopology(groundPos);
					if ((topology & (int)(TerrainTopology.Enum.Ocean | TerrainTopology.Enum.Cliff | TerrainTopology.Enum.Lake | TerrainTopology.Enum.River)) != 0) continue;
					NavMeshHit navHit;
					if (NavMesh.SamplePosition(groundPos, out navHit, 3f, NavMesh.AllAreas))
					{
						spawnPos = navHit.position;
						found = true;
						break;
					}
				}
			}
			if (!found) { if (NavMesh.SamplePosition(monumentPos, out NavMeshHit failSafeHit, 15f, NavMesh.AllAreas)) spawnPos = failSafeHit.position; else spawnPos = monumentPos + new Vector3(0, 1.5f, 0); }
			else { spawnPos.y += 0.2f; }

			var wearItemsArray = new JArray();
			foreach (var item in config.RopaGuardian) { wearItemsArray.Add(new JObject { ["ShortName"] = item.Key, ["SkinID"] = item.Value }); }

			JObject NPC = new JObject
			{
				["Name"] = $"{prefijoNombre} [Lvl {nivelAmenaza}]",
				["Health"] = config.SaludGuardian * multiplicadorVida,
				["Respawn"] = false, // <--- CORRECCIÓN APLICADA: Evita el respawn automático externo
				["RoamRange"] = 30f, ["ChaseRange"] = 200f, ["AttackRangeMultiplier"] = 2.5f, ["IsAggressive"] = true,
				["SenseRange"] = 80f, ["DamageScale"] = modificadorDaño, ["Speed"] = velocidadNpc,
				["IsRemoveCorpse"] = true, ["CanUseHealing"] = true, ["IsHostile"] = true, ["MaxDistance"] = 150f,
				["AimConeScale"] = dispersionBala, ["WearItems"] = wearItemsArray,
				["BeltItems"] = new JArray { new JObject { ["ShortName"] = armaBotShortname, ["SkinID"] = armaBotSkin, ["Amount"] = 1 }, new JObject { ["ShortName"] = "syringe.medical", ["Amount"] = 5 }, new JObject { ["ShortName"] = "grenade.f1", ["Amount"] = 10 } },
				["States"] = new JArray { "IdleState", "RoamState", "ChaseState", "CombatState" }
			};
			
			ScientistNPC npc = NpcSpawn?.Call("SpawnNpc", spawnPos, NPC) as ScientistNPC;
			if (npc != null) { if (escalaVisual != 1.0f) { npc.transform.localScale = new Vector3(escalaVisual, escalaVisual, escalaVisual); npc.SendNetworkUpdate(); } botsPorMonumento[id].Add(npc); }
		}
	}
}	
		private Vector3 GetRandomPointAround(Vector3 centro, float distMin, float distMax)
        {
            Vector2 randomDir = UnityEngine.Random.insideUnitCircle.normalized;
            float distanciaAleatoria = UnityEngine.Random.Range(distMin, distMax);
            Vector3 puntoCandidato = centro + new Vector3(randomDir.x * distanciaAleatoria, 0, randomDir.y * distanciaAleatoria);

            puntoCandidato = AjustarAlturaSuelo(puntoCandidato);

            if ((TerrainMeta.TopologyMap.GetTopology(puntoCandidato) & TopologiaBloqueada) != 0)
                return Vector3.zero;

            if (NavMesh.SamplePosition(puntoCandidato, out NavMeshHit hit, 4f, NavMesh.AllAreas))
            { 
                if (EstaEnElAgua(hit.position)) return Vector3.zero;
                return hit.position;
            }

            return Vector3.zero;
        }

        void ChequearRespawnNocturno()
		{ 
			if (!NocheActivada) return;

			int zombiesActuales = NpcsEnGrupo.Count;
			int cupoDisponible = config.LimiteZombiesGlobal - zombiesActuales;

			// SOLUCIÓN 1: Si ya no hay espacio real, nos retiramos. 
			// Pero si queda aunque sea 1 cupo, permitimos que intente spawnear.
			if (cupoDisponible <= 0) return;

			// Calculamos de forma flotante cuántos grupos reales se necesitan para llenar lo que queda
			int tamañoGrupo = config.CantidadBaseNocturnos > 0 ? config.CantidadBaseNocturnos : 1;
			int gruposNecesarios = Mathf.CeilToInt((float)cupoDisponible / tamañoGrupo);
			
			// Mantenemos tu límite de máximo 3 grupos por ciclo para evitar caídas de FPS
			int gruposASpawnear = Math.Min(3, gruposNecesarios);

			for (int i = 0; i < gruposASpawnear; i++)
			{
				BasePlayer jugadorObjetivo = (jugadoresValidos != null && jugadoresValidos.Count > 0) ? jugadoresValidos.GetRandom() : null;
				Vector3 puntoSpawn;

				if (jugadorObjetivo != null)
				{
					// SOLUCIÓN 2: Reducimos el radio. 40m a 75m es la distancia perfecta.
					// Está lo suficientemente lejos para que no lo vean aparecer en su cara,
					// pero lo suficientemente cerca (menos de 1 Grid) para que la IA se active y vaya a por ellos.
					puntoSpawn = GetRandomPointAround(jugadorObjetivo.transform.position, 40f, 75f);
				}
				else
				{
					puntoSpawn = GetRandomNavPos();
				}

				if (puntoSpawn != Vector3.zero)
				{
					// Clona la variable local para evitar problemas de contexto/clonación en el timer asíncrono
					Vector3 posicionFinal = puntoSpawn;

					timer.Once(0.5f * i, () => {
						if (!NocheActivada) return;
						
						// Doble verificación: si otro timer ya llenó el cupo en estos milisegundos, frena.
						if (NpcsEnGrupo.Count >= config.LimiteZombiesGlobal) return;

						SpawnGrupoColmena(posicionFinal, null, HordeType.Nocturno);
					});
				}
			}
		}
		
		[ChatCommand("horda")]
        void cmdHordaInfo(BasePlayer p, string cmd, string[] args)
        {
            // 1. Contar NPCs spameados (Guardianes en monumentos)
            int guardianes = 0;
            foreach (var listaBots in botsPorMonumento.Values)
            {
                if (listaBots != null)
                {
                    foreach (var npc in listaBots)
                    {
                        if (npc != null && !npc.IsDestroyed && !npc.IsDead()) guardianes++;
                    }
                }
            }

            // Contar Zombies según su categoría (Grupos Horda)
            int nocturnos = 0;
            int asedios = 0;
            int oleadas = 0; 

            foreach (var g in listaGrupos)
            {
                if (g == null || g.Members == null) continue;
                
                int vivos = 0;
                foreach (var m in g.Members)
                {
                    if (m != null && !m.IsDestroyed && !m.IsDead()) vivos++;
                }

                if (g.Type == HordeType.Nocturno) nocturnos += vivos;
                else if (g.Type == HordeType.Asedio) asedios += vivos;
                else if (g.Type == HordeType.Oleadas) oleadas += vivos;
            }

            // 2. Calcular Top 5 Jugadores en Base
            var topCampers = tiempoEnBase
                .OrderByDescending(kvp => kvp.Value)
                .Take(5)
                .ToList();

            // 3. Construir y enviar el mensaje con colores
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<size=16><color=#ff4d4d>☠ ESTADO DE LA HORDA ☠</color></size>");
            sb.AppendLine($"<color=#55aaff>Guardianes (Monumentos):</color> <color=white>{guardianes}</color>");
            sb.AppendLine($"<color=#55ff55>Zombies Nocturnos:</color> <color=white>{nocturnos}</color>/<color=white>{config.LimiteZombiesGlobal}</color>");
            sb.AppendLine($"<color=#ffaa55>Zombies de Asedio:</color> <color=white>{asedios}</color>"); 
            sb.AppendLine($"<color=#ff55ff>Zombies de Oleadas:</color> <color=white>{oleadas}</color>");
            sb.AppendLine("\n<size=14><color=#ffff55>⛺ TOP 5 TIEMPO EN BASE</color></size>");

            if (topCampers.Count == 0)
            {
                sb.AppendLine("<color=#aaaaaa>Nadie está campeando en su base actualmente.</color>");
            }
            else
            {
                int rank = 1;
                foreach (var kvp in topCampers)
                {
                    BasePlayer camper = BasePlayer.FindByID(kvp.Key);
                    string nombre = camper != null ? camper.displayName : "Desconocido";
                    
                    int mins = Mathf.FloorToInt(kvp.Value / 60f);
                    int secs = Mathf.FloorToInt(kvp.Value % 60f);
                    
                    sb.AppendLine($"<color=yellow>{rank}.</color> <color=white>{nombre}</color> - <color=#aaaaaa>{mins}m {secs}s</color>");
                    rank++;
                }
            }

            SendReply(p, sb.ToString());
        }
		
		private void EquiparNocturno(ScarecrowNPC z)
		{
			if (z == null || z.inventory == null || z.inventory.containerWear == null) return;

			// Si en el JSON pusiste {}, Count será 0, saltará esto y mantendrá su ropa vanilla
			if (config.RopaNocturno == null || config.RopaNocturno.Count == 0) return;

			// (El resto de tu código de limpieza y equipamiento de ropa aquí abajo...)
			for (int i = z.inventory.containerWear.itemList.Count - 1; i >= 0; i--)
			{
				Item item = z.inventory.containerWear.itemList[i];
				item.RemoveFromContainer();
				item.Remove();
			}

			// 2. Equipar la ropa definida en config.RopaNocturno
			foreach (var ropa in config.RopaNocturno)
			{
				Item newItem = ItemManager.CreateByName(ropa.Key, 1, ropa.Value);
				if (newItem != null)
				{
					if (!newItem.MoveToContainer(z.inventory.containerWear))
					{
						newItem.Remove();
					}
				}
			}
		}
        private Vector3 CalcularPosicionSpawn(HordeType tipo, Vector3 sPos, string idMonumento)//DESACTIVADO
        {
            Vector3 puntoBusqueda;
            
            
            Vector2 circulo = UnityEngine.Random.insideUnitCircle * 20f;
            puntoBusqueda = new Vector3(sPos.x + circulo.x, 500f, sPos.z + circulo.y);

            // Raycast como NpcSpawn para no quedar bajo tierra/rocas
            if (!Physics.Raycast(puntoBusqueda, Vector3.down, out RaycastHit hit, 1000f, CapasSuelo))
                return Vector3.zero;

            puntoBusqueda = hit.point;

            // Bloquear topologías prohibidas (rocas, ríos, acantilados)
            if ((TerrainMeta.TopologyMap.GetTopology(puntoBusqueda) & TopologiaBloqueada) != 0)
                return Vector3.zero;

            // Carreteras y Vías (8 = Road | 16 = Rail -> 24)
            if ((TerrainMeta.TopologyMap.GetTopology(puntoBusqueda) & 24) != 0)
                return Vector3.zero;

            if (BasePlayer.activePlayerList.Any(p => p.Distance(puntoBusqueda) < 5f))
                return Vector3.zero;

            // Muestrear NavMesh al final como hace NpcSpawn
            if (NavMesh.SamplePosition(puntoBusqueda, out NavMeshHit hitNav, 4f, NavMesh.AllAreas))
            {
                if (WaterLevel.GetWaterLevel(hitNav.position, true) > hitNav.position.y) return Vector3.zero;
                return hitNav.position;
            }

            return Vector3.zero;
        }

        Vector3 AjustarAlturaSuelo(Vector3 pos)
        {
            RaycastHit hit;
            // NpcSpawn lanza el rayo desde 500f de altura hacia abajo para pillar el suelo más alto real y evitar cuevas o bases de rocas
            Vector3 origenRayo = new Vector3(pos.x, 500f, pos.z);
            
            if (Physics.Raycast(origenRayo, Vector3.down, out hit, 1000f, CapasSuelo))
            {
                float nivelAgua = WaterLevel.GetWaterLevel(hit.point, true);
                if (hit.point.y < nivelAgua)
                {
                    return new Vector3(pos.x, TerrainMeta.HeightMap.GetHeight(pos), pos.z);
                }
                return hit.point;
            }
            return new Vector3(pos.x, TerrainMeta.HeightMap.GetHeight(pos), pos.z);
        }

        void SpawnGrupoColmena(Vector3 sPos, BasePlayer target, HordeType tipo, int wave = 0, int tier = 0, string idMonumento = "")
		{
			if (tipo == HordeType.Nocturno)
			{
				int npcsHordaActuales = listaGrupos
					.Where(g => g.Type == HordeType.Nocturno || g.Type == HordeType.Oleadas)
					.Sum(g => g.Members.Count);

				if (npcsHordaActuales >= config.LimiteZombiesGlobal) return;
			}

			if (tipo != HordeType.Oleadas && tipo != HordeType.Asedio)
			{
				if (sPos == Vector3.zero || EstaEnElAgua(sPos) || EstaCercaDeMonumentoProtegido(sPos)) return;
			}

			HordeGroup g = new HordeGroup {
				Target = target,
				Type = tipo,
				MonumentoId = idMonumento,
				WaveLevel = wave,
				DifficultyTier = tier,
				LastAnchorPos = sPos,
				SpawnNight = contadorNoches // <-- AÑADE ESTO
			};
			listaGrupos.Add(g);

			if (target != null)
			{
				timer.Once(1.5f, () => {
					if (g != null && g.Target != null) AlertarGrupo(g, target);
				});
			}

			int cantidad = tipo switch {
				HordeType.Oleadas  => config.CantidadBaseEvento + (wave * 2),
				HordeType.Asedio   => CalcularCantidadPorBase(target?.GetBuildingPrivilege()),
				HordeType.Nocturno => config.CantidadBaseNocturnos + bonusActual,
				_ => 3
			};

			for (int i = 0; i < cantidad; i++)
			{
				int index = i;
				timer.Once(0.2f * i, () =>
				{
					if (this == null || !this.IsLoaded) return;
					if ((tipo == HordeType.Oleadas || tipo == HordeType.Asedio) && (target == null || !target.IsConnected)) return;
					if (tipo == HordeType.Nocturno && !NocheActivada) return;
					// Lógica de búsqueda de posición segura en NavMesh
					Vector3 variacion = UnityEngine.Random.insideUnitSphere * 4f;
					Vector3 puntoBase = new Vector3(sPos.x + variacion.x, sPos.y + 10f, sPos.z + variacion.z);
					
					Vector3 spawnFinal = Vector3.zero;
					bool found = false;

					for (int attempt = 0; attempt < 10; attempt++)
					{
						NavMeshHit hit;
						if (NavMesh.SamplePosition(puntoBase, out hit, 5f, NavMesh.AllAreas))
						{
							spawnFinal = hit.position;
							found = true;
							break;
						}
						puntoBase += UnityEngine.Random.insideUnitSphere * 2f;
					}

					if (!found) return; // Si no hay terreno válido, abortamos este spawn
					spawnFinal.y += 0.5f; // Ajuste fino para no spawnear bajo tierra

					bool esBoss = (tipo == HordeType.Oleadas && wave > 0 && wave % 5 == 0 && index == 0);
					var z = CrearZombie(spawnFinal, tier, esBoss, tipo, wave, idMonumento);
					
					if (z?.net != null)
					{
						NpcsEnGrupo[z.net.ID.Value] = g;
						g.Members.Add(z);
						ConfigurarIA(z, spawnFinal, tipo);
					}
				});
			}
		}

        private void ConfigurarIA(ScarecrowNPC z, Vector3 pos, HordeType tipo)
        {
            if (z == null || z.NavAgent == null) return;

            z.NavAgent.Warp(pos);
            z.NavAgent.acceleration = 20f; 
            z.NavAgent.stoppingDistance = 4.5f; 
            z.NavAgent.autoBraking = true;

            if (z.NavAgent.isActiveAndEnabled && z.NavAgent.isOnNavMesh)
            {
                switch (tipo)
                {
                    case HordeType.Asedio:
                    case HordeType.Oleadas:
                        z.NavAgent.speed = config.VelocidadCazaLejana;
                        z.NavAgent.isStopped = false;
                        break;
                        

                    case HordeType.Nocturno:
                        z.NavAgent.speed = config.VelocidadBase;
                        z.NavAgent.isStopped = false;
                        break;
                }
            }

            z.SendNetworkUpdate();
        }

        private ScarecrowNPC CrearZombie(Vector3 pos, int tier, bool boss, HordeType tipo, int wave, string idMonumento)
        {
            ScarecrowNPC z = (ScarecrowNPC)GameManager.server.CreateEntity("assets/prefabs/npc/scarecrow/scarecrow.prefab", pos);
            if (z == null) return null;

            var stats = tipo switch
            {
                HordeType.Oleadas when boss => (nombre: "HERALDO RADIACTIVO", vida: config.SaludBoss),
                HordeType.Oleadas => (nombre: "CAZADOR MALDITO", vida: config.SaludBaseWave + (wave * config.EscaladoVidaWave)),
                HordeType.Asedio => (nombre: "ZOMBIE DE ASEDIO", vida: 250f), 
                _ => (nombre: "ZOMBIE NOCTURNO", vida: config.SaludZombieNocturno)
            };

            z.startHealth = stats.vida;
            z.InitializeHealth(stats.vida, stats.vida); 
            z.Spawn();
			EquiparNocturno(z);
            z.gameObject.SetLayerRecursive(17);
            
            if (boss) z.transform.localScale = new Vector3(1.25f, 1.25f, 1.25f);

            z.displayName = stats.nombre;
            
            timer.Once(0.5f, () => { 
                if (z == null || z.IsDead()) return;

                if (z.Brain != null) 
                {
                    z.Brain.enabled = true;
                    z.Brain.SetEnabled(true); 
                    z.Brain.Think(0f); 
                }
            });

            z.SendNetworkUpdate();
            
            // Registramos el zombie para evitar huérfanos
            todosLosZombies.Add(z);
            
            return z;
        }

        // ==========================================
        // UTILIDADES Y CÁLCULOS
        // ==========================================
        int CalcularCantidadSegura(int baseCantidad)
        {
            float lagFactor = Time.smoothDeltaTime / (1f / 60f); 
            if (lagFactor <= 1f) return baseCantidad; 
            if (lagFactor <= 2f) return Mathf.Max(1, baseCantidad / 2); 
            return Mathf.Max(1, baseCantidad / 4); 
        }

        private int CalcularCantidadPorBase(BuildingPrivlidge privilege)
        {
            return 10;
        }

        bool EstaEnElAgua(Vector3 pos)
        {
            return WaterLevel.GetWaterDepth(pos, true, true, null) > 0.1f;
        }

        bool EstaCercaDeMonumentoProtegido(Vector3 pos)
        {
            if (TerrainMeta.Path?.Monuments == null) return false;

            float radioSqr = 40000f; 

            foreach (var mono in TerrainMeta.Path.Monuments)
            {
                if ((pos - mono.transform.position).sqrMagnitude < radioSqr)
                {
                    string nombre = mono.name.ToLower();
                    if (monumentosProhibidos.Any(m => nombre.Contains(m)))
                        return true;
                }
            }
            return false;
        }

        bool AnyPlayerNearby(Vector3 pos, float dist)
        {
            float distSqr = dist * dist;
            foreach (var player in jugadoresValidos)
            {
                if (player == null || !player.IsConnected) continue;
                if ((player.transform.position - pos).sqrMagnitude < distSqr)
                    return true;
            }
            return false;
        }

        Vector3 GetRandomNavPos()
        {
            for (int i = 0; i < 10; i++)
            {
                float halfSizeX = TerrainMeta.Size.x / 2;
                float halfSizeZ = TerrainMeta.Size.z / 2;
                
                Vector3 randomPos = new Vector3(
                    UnityEngine.Random.Range(-halfSizeX, halfSizeX), 
                    500f, 
                    UnityEngine.Random.Range(-halfSizeZ, halfSizeZ)
                );

                if (!Physics.Raycast(randomPos, Vector3.down, out RaycastHit hit, 1000f, CapasSuelo))
                    continue;

                Vector3 finalPos = hit.point;

                if ((TerrainMeta.TopologyMap.GetTopology(finalPos) & TopologiaBloqueada) != 0)
                    continue;

                if (NavMesh.SamplePosition(finalPos, out NavMeshHit hitNav, 10f, NavMesh.AllAreas))
                {
                    finalPos = hitNav.position;
                    
                    if (!EstaEnElAgua(finalPos) && 
                        !EstaCercaDeMonumentoProtegido(finalPos) && 
                        !AnyPlayerNearby(finalPos, 10f)) 
                    {
                        return finalPos;
                    }
                }
            }
            return Vector3.zero;
        }

		BasePlayer BuscarJugadorMasTiempoEnCasa()
		{
			if (TOD_Sky.Instance == null) return null;
			int diaRustActual = TOD_Sky.Instance.Cycle.Day;

			List<BasePlayer> candidatos = Facepunch.Pool.Get<List<BasePlayer>>();

			try
			{
				// 1. Filtrado y adición a la lista de candidatos
				for (int i = 0; i < jugadoresValidos.Count; i++)
				{
					BasePlayer p = jugadoresValidos[i];
					if (p == null || !p.IsConnected || p.IsDead() || p.IsSleeping()) continue;

					var priv = p.GetBuildingPrivilege();
					if (priv == null || !priv.IsAuthed(p)) continue;

					int ultimaNoche;
					if (ultimaNocheAsedio.TryGetValue(p.userID, out ultimaNoche))
						if (diaRustActual < (ultimaNoche + config.DiasDeCooldownAsedio)) continue;
					
					float tiempoAcumulado;
					if (!tiempoEnBase.TryGetValue(p.userID, out tiempoAcumulado) || tiempoAcumulado < config.TiempoMinimoAsedio) continue;

					candidatos.Add(p);
				}

				if (candidatos.Count == 0) return null;

				int randomIndex = UnityEngine.Random.Range(0, candidatos.Count);
				BasePlayer seleccionado = candidatos[randomIndex];

				if (seleccionado != null)
				{
					ultimaNocheAsedio[seleccionado.userID] = diaRustActual;
				}

				return seleccionado;
			}
			finally
			{
				candidatos.Clear();
				Facepunch.Pool.FreeUnmanaged(ref candidatos);
			}
		}

        private void LanzarBeancan(ScarecrowNPC emisor, Vector3 objetivo)
        {
            if (emisor == null || emisor.IsDead()) return;

            string prefab = (UnityEngine.Random.Range(0, 100) > 30) ? PREFAB_SATCHEL : "assets/prefabs/weapons/beancan grenade/grenade.beancan.deployed.prefab";
            
            BaseEntity grenade = GameManager.server.CreateEntity(prefab, emisor.eyes.position + emisor.eyes.BodyForward() * 0.5f);
            if (grenade == null) return;

            Vector3 vectorHaciaObjetivo = objetivo - grenade.transform.position;
            float distancia = vectorHaciaObjetivo.magnitude;
            Vector3 direccion = vectorHaciaObjetivo.normalized;

            TimedExplosive explosive = grenade.GetComponent<TimedExplosive>();
            if (explosive != null)
            {
                explosive.timerAmountMin = 2.5f;
                explosive.timerAmountMax = 3.5f;
                explosive.canStick = true; 
            }

            grenade.Spawn();

            float fuerzaUp = Mathf.Clamp(distancia * 0.15f, 0.5f, 2f); 
            float multiplicadorFuerza = Mathf.Clamp(distancia * 1.5f, 5f, 15f); 

            Vector3 fuerzaLanzamiento = (direccion + Vector3.up * fuerzaUp).normalized * multiplicadorFuerza;
            
            grenade.SetVelocity(fuerzaLanzamiento);
            grenade.SetAngularVelocity(new Vector3(UnityEngine.Random.Range(-10, 10), 10f, 5f));

            emisor.SignalBroadcast(BaseEntity.Signal.Throw);
        }

        void LimpiarHordasAmbientales()
        {
            // 1. Limpiamos directamente desde el registro maestro para evitar huérfanos perdidos
            var zombiesArray = todosLosZombies.ToArray(); 

            for (int i = 0; i < zombiesArray.Length; i++)
            {
                var z = zombiesArray[i];
                
                // Limpieza de nulos o destruidos por Rust
                if (z == null || z.IsDestroyed) 
                {
                    todosLosZombies.Remove(z);
                    continue;
                }

                // Determinamos si es un zombie que debe morir al amanecer
                bool esNocturnoOHuerfano = true;
                if (z.net != null && NpcsEnGrupo.TryGetValue(z.net.ID.Value, out HordeGroup grupo))
                {
                    // Perdonamos a los de Asedio y Oleadas (si quieres que mueran también, quita este if)
                    if (grupo.Type != HordeType.Nocturno) 
                    {
                        esNocturnoOHuerfano = false;
                    }
                }

                // Si es nocturno o un huérfano sin grupo asignado, se elimina
                if (esNocturnoOHuerfano)
                {
                    if (z.net != null)
                    {
                        NpcsEnGrupo.Remove(z.net.ID.Value);
                        ultimoAtaqueBot.Remove(z.net.ID.Value);
                    }

                    if (!z.IsDead())
                    {
                        //Effect.server.Run("assets/prefabs/tools/smoke grenade/effects/smokegrenade.prefab", z.transform.position);
                        z.Kill();
                    }
                    todosLosZombies.Remove(z);
                }
            }

            // 2. Limpiar las colecciones lógicas de los grupos
            for (int i = listaGrupos.Count - 1; i >= 0; i--)
            {
                var g = listaGrupos[i];
                if (g != null && g.Type == HordeType.Nocturno)
                {
                    if (g.Members != null) g.Members.Clear();
                    listaGrupos.RemoveAt(i);
                }
            }
            
            Puts("Horda Nocturna: Se han retirado los zombies nocturnos al amanecer (Cero huérfanos).");
        }

        // ==========================================
        // EVENTOS DE JUEGO (HOOKS)
        // ==========================================
        void OnPlayerRespawn(BasePlayer player)
        {
            if (player == null || player.IsNpc) return;
            
            ulong userId = player.userID;

            if (killsParaMostrarAlRespawn.TryGetValue(userId, out int bajas))
            {
                timer.Once(2.5f, () => 
                {
                    if (player == null || !player.IsConnected || !player.IsAlive()) return;

                    string mensaje = lang.GetMessage("Player.CombateCaido", this, player.UserIDString);
                    SendReply(player, string.Format(mensaje, bajas));

                    killsParaMostrarAlRespawn.Remove(userId);
                    
                    if (killsSesionActual.ContainsKey(userId))
                    {
                        killsSesionActual[userId] = 0;
                    }
                });
            }
        }
        
        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (player == null) return;
            ulong id = player.userID;

            tiempoEnBase.Remove(id);
            killsSesionActual.Remove(id); 
            jugadoresEnCaza.Remove(id);

            Timer timer;
            if (timersActivos.TryGetValue(id, out timer))
            {
                timer?.Destroy();
                timersActivos.Remove(id);
            }
        }
						
		void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
		{
			// 1. Guardias de seguridad iniciales
			if (entity?.net == null) return;
			ulong entityId = entity.net.ID.Value;

			// Si no está en nuestro registro, no hacemos nada
			if (!NpcsEnGrupo.TryGetValue(entityId, out HordeGroup grupo)) return;

			// 2. Gestión del atacante (Ranking)
			BasePlayer atacante = info?.InitiatorPlayer;
			if (atacante != null && !atacante.IsNpc)
			{
				ProcessPlayerKill(atacante);
			}

			// 3. Lógica específica de tipo NPC
			// Usamos 'is' para castear y comprobar en un solo paso
			if (entity is ScarecrowNPC npc)
			{
				todosLosZombies.Remove(npc);

				// SUGERENCIA: Usa una constante para el nombre del boss para evitar errores de escritura
				if (npc.displayName == "HERALDO RADIACTIVO")
				{
					EjecutarExplosionBoss(entity);
				}
			}

			// 4. Limpieza de registros
			NpcsEnGrupo.Remove(entityId);
			ultimoAtaqueBot.Remove(entityId);

			// 5. Gestión del Grupo
			if (grupo?.Members != null)
			{
				grupo.Members.RemoveAll(m => m == null || m.net?.ID.Value == entityId);
				
				if (grupo.Members.Count == 0)
				{
					listaGrupos.Remove(grupo);
				}
			}
		}

		// Método auxiliar para limpiar el código principal
		private void ProcessPlayerKill(BasePlayer atacante)
		{
			ulong userId = atacante.userID;
			
			// Ranking Global
			if (!rankingKillsNoche.TryGetValue(userId, out JugadorData data))
			{
				rankingKillsNoche[userId] = new JugadorData { Nombre = atacante.displayName, Kills = 1 };
			}
			else
			{
				data.Kills++;
				data.Nombre = atacante.displayName; 
			}

			// Ranking Sesión
			killsSesionActual[userId] = killsSesionActual.GetValueOrDefault(userId, 0) + 1;
			killsParaMostrarAlRespawn[userId] = killsSesionActual[userId];
		}

        private void EjecutarExplosionBoss(BaseCombatEntity boss)
        {
            if (boss == null) return;

            Vector3 centroExplosion = boss.transform.position;
            Effect.server.Run("assets/prefabs/npc/m2_bradley/effects/bradley_explosion.prefab", centroExplosion);

            List<BaseCombatEntity> cercanas = Facepunch.Pool.Get<List<BaseCombatEntity>>();
            Vis.Entities(centroExplosion, 8f, cercanas, mascaraDañables);

            foreach (var ent in cercanas)
            {
                if (ent == null || ent.IsDead() || ent == boss) continue;

                float distancia = Vector3.Distance(centroExplosion, ent.transform.position);
                float factorCercania = Mathf.Clamp01(1 - (distancia / 8f));

                float dañoBase = (ent is BuildingBlock) ? 200f : 80f;
                float dañoFinal = dañoBase * factorCercania;

                ent.Hurt(dañoFinal, Rust.DamageType.Explosion, null, true);
            }
            Facepunch.Pool.FreeUnmanaged(ref cercanas);
        }

        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null || entity.net == null) return null;

            ulong victimId = entity.net.ID.Value;

            if (NpcsEnGrupo.TryGetValue(victimId, out HordeGroup grupoHerido))
            {
                var npcHerido = entity as ScarecrowNPC;
                if (npcHerido != null && npcHerido.Brain != null)
                {
                    if (!npcHerido.Brain.enabled)
                    {
                        npcHerido.Brain.SetEnabled(true); 
                    }
                }

                if (grupoHerido.LastAlertTime + 2f > Time.realtimeSinceStartup) return null; 
                grupoHerido.LastAlertTime = Time.realtimeSinceStartup;

                var atacanteNpc = info.Initiator as ScarecrowNPC;
                if (atacanteNpc != null && atacanteNpc.net != null)
                {
                    if (NpcsEnGrupo.ContainsKey(atacanteNpc.net.ID.Value))
                    { 
                        return true; 
                    }
                }

                BasePlayer atacanteJugador = info.InitiatorPlayer;
                if (atacanteJugador != null)
                {
                    foreach (var miembro in grupoHerido.Members)
                    {
                        if (miembro != null && miembro.Brain != null && !miembro.Brain.enabled)
                        {
                            miembro.Brain.SetEnabled(true);
                        }
                    }

                    if (npcHerido != null && npcHerido.Brain != null)
                    {
                        npcHerido.Brain.Senses.Memory.SetKnown(atacanteJugador, npcHerido, null);
                    }

                    AlertarGrupo(grupoHerido, atacanteJugador, npcHerido);

                    
                        Vector3 posicionHerido = entity.transform.position;
                        float radioAlertaSqr = 2500f; 

                        foreach (var grupoCercano in listaGrupos)
                        {
                            if (grupoCercano == null || grupoCercano == grupoHerido) continue;

                            if ((grupoCercano.LastAnchorPos - posicionHerido).sqrMagnitude < radioAlertaSqr)
                            {
                                foreach (var m in grupoCercano.Members)
                                {
                                    if (m != null && m.Brain != null) m.Brain.SetEnabled(true);
                                }
                                AlertarGrupo(grupoCercano, atacanteJugador);
                            }
                        }
                    
                }
            }
            return null;
        }

        void AlertarGrupo(HordeGroup grupo, BasePlayer objetivo, ScarecrowNPC miembroAtacado = null)
        {
            if (grupo == null || objetivo == null || !objetivo.IsConnected) return;

            grupo.Target = objetivo;
            grupo.LastAnchorPos = objetivo.transform.position;

            if (miembroAtacado != null && grupo.Members.Count > 0)
            {
                int index = grupo.Members.IndexOf(miembroAtacado);
                if (index > 0)
                {
                    var temp = grupo.Members[0];
                    grupo.Members[0] = miembroAtacado;
                    grupo.Members[index] = temp;
                }
            }

            foreach (var miembro in grupo.Members)
            {
                if (miembro == null || miembro.IsDead()) continue;

                if (miembro.Brain != null)
                {
                    miembro.Brain.Events.Memory.Entity.Set(objetivo, 1);
                    miembro.Brain.SwitchToState(AIState.Combat);
                    miembro.Brain.Think(0f);
                }

                if (miembro.NavAgent != null && miembro.NavAgent.isActiveAndEnabled)
                {
                    if (miembro.NavAgent.isOnNavMesh)
                    {
                        miembro.NavAgent.isStopped = false;
                        miembro.NavAgent.speed = (grupo.Type == HordeType.Nocturno) 
                            ? config.VelocidadCazaLejana 
                            : config.VelocidadBase;
                        
                        miembro.NavAgent.SetDestination(objetivo.transform.position);
                    }
                }
                
                miembro.SendMessage("AttackTarget", objetivo, SendMessageOptions.DontRequireReceiver);
            }
        }

        private void FinalizarAsedio(BasePlayer target)
        {
            if (target == null) return;
            
            if (jugadoresEnCaza.ContainsKey(target.userID))
                jugadoresEnCaza.Remove(target.userID);
            
            if (tiempoEnBase.ContainsKey(target.userID))
                tiempoEnBase[target.userID] = 0f; 
            
            ultimaNocheAsedio[target.userID] = TOD_Sky.Instance.Cycle.Day; 
            SaveData(); 
            SendReply(target, lang.GetMessage("Player.AsedioRoto", this, target.UserIDString));
        }

        // ==========================================
        // CEREBRO PRINCIPAL - IA
        // ==========================================

        void ActualizarIAColmena()
        {
            int iaActivaActual = 0;
            float tiempoActual = UnityEngine.Time.time;

            for (int i = listaGrupos.Count - 1; i >= 0; i--)
            {
                var grupo = listaGrupos[i];
				if (grupo == null || (grupo.Members.Count == 0 && !grupo.IsPlayerOleadas && (UnityEngine.Time.realtimeSinceStartup - grupo.CreationTime > 5f)))
				{
					listaGrupos.RemoveAt(i);
				}
            }

            for (int i = listaGrupos.Count - 1; i >= 0; i--)
            {
                if (i % 4 != indiceActualizacion % 4) continue;
                
                var grupo = listaGrupos[i];
                if (grupo == null) continue;

                ProcesarEstadoMiembros(grupo, tiempoActual);
                if (grupo.Members.Count == 0) { ManejarMuerteGrupo(grupo); continue; }

                if (grupo.Target != null && grupo.Target.InSafeZone())
                {
                    EliminarGrupoEfecto(grupo);
                    continue;
                }

                if (iaActivaActual >= config.MaxIAActiva) break;
                
                bool jugadorCerca = false;

                if (grupo.Members.Count > 0 && grupo.Members[0] != null)
                {
                    Vector3 posGrupo = grupo.Members[0].transform.position;

                    for (int j = 0; j < jugadoresValidos.Count; j++)
					{
						BasePlayer p = jugadoresValidos[j];
						if (p == null || !p.IsConnected) continue;
						
						if ((posGrupo - p.transform.position).sqrMagnitude <= 40000f)
						{
							jugadorCerca = true;
							break;
						}
					}

                    foreach (var npc in grupo.Members)
                    {
                        if (npc == null || npc.Brain == null) continue;

                        bool estaActivo = npc.Brain.enabled;
                        
                        if (jugadorCerca && !estaActivo)
                        {
                            npc.Brain.SetEnabled(true);
                        }
                        else if (!jugadorCerca && estaActivo)
                        {
                            npc.Brain.SetEnabled(false);
                        }
                    }
                }

                if (!jugadorCerca) continue; 

                iaActivaActual++;
                
                if (tiempoActual - grupo.LastTargetScanTime >= 2.0f)
                {
                    ActualizarObjetivoGrupo(grupo);
                    
                    
                    grupo.LastTargetScanTime = tiempoActual;
                }

                bool hayTarget = grupo.Target != null && grupo.Target.IsAlive() && grupo.Target.IsConnected;

                if (hayTarget)
                {
                    EjecutarRutinaCombate(grupo, tiempoActual);
                }
                else
                {
                    RetornarAlAncla(grupo);
                }
            }
            indiceActualizacion = (indiceActualizacion + 1) % 4;
        }

        private void ProcesarEstadoMiembros(HordeGroup grupo, float tiempoActual)
        {
            bool tieneTargetActivo = grupo.Target != null && grupo.Target.IsAlive();
            bool jugadorCercaAncla = AnyPlayerNearby(grupo.LastAnchorPos, config.DistanciaActivacionIA);

            for (int j = grupo.Members.Count - 1; j >= 0; j--)
            {
                var bot = grupo.Members[j];
                if (bot == null || bot.IsDestroyed || bot.IsDead())
                {
                    if (j == 0 && grupo.Members.Count > 1) {
                        grupo.LastLeaderDeathTime = tiempoActual;
                        Effect.server.Run("assets/prefabs/npc/murderer/sound/death.prefab", bot != null ? bot.transform.position : grupo.LastAnchorPos);
                    }
                    if (bot?.net != null) {
                        NpcsEnGrupo.Remove(bot.net.ID.Value);
                        ultimoAtaqueBot.Remove(bot.net.ID.Value);
                    }
                    grupo.Members.RemoveAt(j);
                    continue; 
                }

                float distAlAnclaSq = (bot.transform.position - grupo.LastAnchorPos).sqrMagnitude;
                bool estaEnCasa = distAlAnclaSq < 25f;
                bool debeHibernar = grupo.Type != HordeType.Asedio && !tieneTargetActivo && !jugadorCercaAncla;

                ControlarEstadoIA(bot, !debeHibernar);
            }
        }

        private void EjecutarRutinaCombate(HordeGroup grupo, float tiempoActual)
        {
            BasePlayer target = grupo.Target;
            Vector3 targetPos = target.transform.position;
            bool esCampero = target.GetBuildingPrivilege() != null;
            float multFrenesi = (tiempoActual - grupo.LastLeaderDeathTime < 3f) ? 1.5f : 1.0f;
            float velocidadFinal = config.VelocidadCazaLejana * multFrenesi;

            for (int j = 0; j < grupo.Members.Count; j++)
            {
                var bot = grupo.Members[j];
                if (!EsAgenteOperativo(bot)) continue;

                float distSq = (bot.transform.position - targetPos).sqrMagnitude;
                float uAtaque = ultimoAtaqueBot.ContainsKey(bot.net.ID.Value) ? ultimoAtaqueBot[bot.net.ID.Value] : 0f;

                bot.NavAgent.isStopped = false;
                bot.NavAgent.speed = velocidadFinal;

                if (j == 0) 
                {
                    if (!bot.NavAgent.pathPending && ((Vector3)bot.NavAgent.destination - targetPos).sqrMagnitude > 9f)
                    {
                        bot.NavAgent.SetDestination(targetPos);
                    }
                } 
                else 
                {
                    var lider = grupo.Members[0];
                    if (lider != null)
                    {
                        if ((bot.transform.position - lider.transform.position).sqrMagnitude > 16f)
                        {
                            if (!bot.NavAgent.pathPending) bot.NavAgent.SetDestination(lider.transform.position);
                            bot.NavAgent.isStopped = false;
                        }
                        else 
                        {
                            bot.NavAgent.isStopped = true;
                        }
                    }
                }

                if (bot.Brain != null && bot.Brain.Senses != null) {
                    bot.Brain.Senses.Memory.SetKnown(target, bot, null);
                }
                bool atacandoEstructura = false; 

                if (grupo.Type == HordeType.Asedio) 
                {
					if (distSq < 100f || ((Vector3)bot.NavAgent.velocity).sqrMagnitude < 0.2f)
                    {
                        bot.ServerRotation = Quaternion.LookRotation((targetPos - bot.transform.position).WithY(0));
                        atacandoEstructura = IntentarAsedio(bot, target, grupo, uAtaque, tiempoActual, distSq);
                    }
                }

                // El resto del código de salto vertical sigue igual
                if (targetPos.y - bot.transform.position.y > 4.0f && distSq < 4f && !atacandoEstructura) {
                    GestionarSaltoVertical(bot, targetPos);
                }

                if (j == 0 && distSq < 1600f && tiempoActual - grupo.LastMessageTime > 30f) {
                    EnviarMensajeHorda(grupo, target, esCampero);
                }
            }
        }

        private void EnviarMensajeHorda(HordeGroup grupo, BasePlayer target, bool esCampero)
        {
            Effect.server.Run("assets/bundled/prefabs/fx/player/beartrap_scream.prefab", grupo.Members[0].transform.position);
            
            string tipoMensaje = esCampero ? "Siege" : "Detection";
            string frase = GetRandomLangMessage(tipoMensaje, target.UserIDString);
            
            SendReply(target, $"<color=#ff4d4d>[HORDA]</color> <size=14><i>{frase}</i></size>");
            grupo.LastMessageTime = UnityEngine.Time.time;
        }

        private void GestionarSaltoVertical(ScarecrowNPC bot, Vector3 targetPos)
        {
            Vector3 jumpPos = targetPos + (bot.transform.position - targetPos).normalized * 1.2f;
            jumpPos.y = targetPos.y;

            NavMeshHit navHit;
            if (NavMesh.SamplePosition(jumpPos, out navHit, 1.5f, NavMesh.AllAreas))
            {
                if (bot.NavAgent.isOnNavMesh) bot.NavAgent.isStopped = true;
                
                bot.NavAgent.Warp(navHit.position);
                Effect.server.Run("assets/bundled/prefabs/fx/impacts/jump-land/boots/dirt/jump-land-dirt.prefab", navHit.position);
                
                timer.Once(0.3f, () => {
                    if (bot != null && !bot.IsDestroyed && !bot.IsDead() && bot.NavAgent != null && bot.NavAgent.isOnNavMesh)
                    {
                        bot.NavAgent.isStopped = false;
                    }
                });
            }
        }

        private void EjecutarAtaqueMelee(ScarecrowNPC bot, BasePlayer target, float uAtaque, float tiempo)
        {
            bot.NavAgent.isStopped = true;
            if (tiempo - uAtaque > 1.2f)
            {
                ultimoAtaqueBot[bot.net.ID.Value] = tiempo;
                bot.SignalBroadcast(BaseEntity.Signal.Attack);
                target.Hurt(25f, Rust.DamageType.Bite, bot, true);
                Effect.server.Run("assets/bundled/prefabs/fx/takedamage_hit.prefab", target.transform.position + Vector3.up);
            }
        }

        private void RefrescarMunicion(ScarecrowNPC bot)
        {
            BaseProjectile arma = bot.GetHeldEntity() as BaseProjectile;
            if (arma != null && arma.primaryMagazine.contents < 1)
            {
                arma.primaryMagazine.contents = arma.primaryMagazine.capacity;
                arma.SendNetworkUpdate();
            }
        }

        private void ControlarEstadoIA(ScarecrowNPC bot, bool activa)
        {
            if (activa)
            {
                if (bot.NavAgent != null && !bot.NavAgent.enabled)
                {
                    bot.NavAgent.enabled = true;
                    if (bot.Brain != null) bot.Brain.enabled = true;
                }
            }
            else
            {
                if (bot.Brain != null && bot.Brain.enabled) bot.Brain.enabled = false;
                if (bot.NavAgent != null && bot.NavAgent.enabled)
                {
                    if (bot.NavAgent.isOnNavMesh) bot.NavAgent.isStopped = true;
                    bot.NavAgent.enabled = false;
                }
            }
        }

        private void ActualizarObjetivoGrupo(HordeGroup grupo)
        {
            if (grupo.Target != null && grupo.Target.IsAlive()) return;

            float radioDeteccionSqr = 900f;
            BasePlayer presa = null;
            float minDist = float.MaxValue;
            Vector3 centro = grupo.Members.Count > 0 ? grupo.Members[0].transform.position : grupo.LastAnchorPos;

            foreach (var jugador in jugadoresValidos)
            {
                if (jugador == null || !jugador.IsConnected || jugador.IsSleeping()) continue;
                float d = (jugador.transform.position - centro).sqrMagnitude;
                if (d < radioDeteccionSqr && d < minDist)
                {
                    minDist = d;
                    presa = jugador;
                }
            }
            
            if (presa != null)
            {
                AlertarGrupo(grupo, presa);
            }
        }

        private void RetornarAlAncla(HordeGroup grupo)
        {
            foreach (var bot in grupo.Members)
            {
                if (!EsAgenteOperativo(bot)) continue;
                float distAnclaSq = (bot.transform.position - grupo.LastAnchorPos).sqrMagnitude;
                
                if (distAnclaSq > 9f) 
                {
                    bot.NavAgent.isStopped = false;
                    bot.NavAgent.speed = config.VelocidadBase;
                    bot.NavAgent.SetDestination(grupo.LastAnchorPos);
                }
                else { bot.NavAgent.isStopped = true; }
            }
        }

		private bool IntentarAsedio(ScarecrowNPC bot, BasePlayer target, HordeGroup grupo, float uAtaque, float tiempoActual, float distSq)
        {
            if (bot == null || bot.net == null || target == null) return false;

            if (distSq > 100f && ((Vector3)bot.NavAgent.velocity).sqrMagnitude > 0.5f) return false;

            Vector3 origen = bot.transform.position + new Vector3(0, 1.2f, 0); 
            Vector3 vectorHaciaObjetivo = target.transform.position - bot.transform.position;
            Vector3 dirHaciaObjetivo = vectorHaciaObjetivo.normalized; 

            RaycastHit hit;
            bool estructuraEncontrada = false;

            if (Physics.SphereCast(origen, 0.25f, bot.transform.forward, out hit, 2.5f, mascaraConstruccion))
            {
                estructuraEncontrada = true;
            }
            else if (Physics.SphereCast(origen, 0.25f, dirHaciaObjetivo, out hit, 3.5f, mascaraConstruccion))
            {
                estructuraEncontrada = true;
            }

            if (estructuraEncontrada)
            {
                var entidad = hit.GetEntity() as BaseCombatEntity;
                if (entidad != null && !(entidad is BasePlayer) && entidad.health > 0)
                {
                    if (bot.NavAgent != null && bot.NavAgent.isOnNavMesh)
                        bot.NavAgent.isStopped = true;

                    Vector3 lookDir = (hit.point - bot.transform.position).WithY(0);
                    if (lookDir != Vector3.zero) bot.ServerRotation = Quaternion.LookRotation(lookDir);

                    if (tiempoActual - uAtaque > 1.5f) 
                    {
                        ultimoAtaqueBot[bot.net.ID.Value] = tiempoActual;
                        bot.SignalBroadcast(BaseEntity.Signal.Attack);
                        
                        entidad.Hurt(25f, DamageType.Slash, bot, true);
                        Effect.server.Run("assets/bundled/prefabs/fx/impacts/additive/fire.prefab", hit.point);
                    }

                    if (tiempoActual - grupo.LastExplosiveTime > 6f && distSq > 4f) 
                    {
                        LanzarBeancan(bot, hit.point);
                        grupo.LastExplosiveTime = tiempoActual;
                    }

                    return true;
                }
            }
            
            if (bot.NavAgent != null && bot.NavAgent.isOnNavMesh && bot.NavAgent.isStopped)
                bot.NavAgent.isStopped = false;

            return false;
        }

        private bool EsAgenteOperativo(ScarecrowNPC bot)
        {
            return bot != null && 
                   bot.net != null &&          
                   !bot.IsDestroyed &&         
                   !bot.IsDead() && 
                   bot.NavAgent != null && 
                   bot.NavAgent.isActiveAndEnabled && 
                   bot.NavAgent.isOnNavMesh;
        }

        private void EliminarGrupoEfecto(HordeGroup grupo)
        {
            if (grupo == null || grupo.Members == null) return;

            for (int i = grupo.Members.Count - 1; i >= 0; i--)
            {
                var bot = grupo.Members[i];
                if (bot != null && !bot.IsDestroyed)
                {
                    Effect.server.Run("assets/prefabs/tools/smoke grenade/effects/smokegrenade.prefab", bot.transform.position);

                    if (bot.net != null)
                    {
                        NpcsEnGrupo.Remove(bot.net.ID.Value);
                        ultimoAtaqueBot.Remove(bot.net.ID.Value);
                    }

                    bot.Kill();
                }
            }
            
            grupo.Members.Clear();
            ManejarMuerteGrupo(grupo);
        }
                
        private void ManejarMuerteGrupo(HordeGroup grupo)
        {
            if (grupo == null) return;

            if (grupo.Target == null)
            {
                LimpiarReferenciaGrupo(grupo);
                return;
            }

            BasePlayer target = grupo.Target;

            if (grupo.IsPlayerOleadas && grupo.WaveLevel >= 1)
            {
                int nivelFinalizado = grupo.WaveLevel;
                int siguienteNivel = nivelFinalizado + 1;
                
                LimpiarReferenciaGrupo(grupo);

                SendReply(target, string.Format(lang.GetMessage("Player.Wave", this, target.UserIDString), nivelFinalizado));
                SendReply(target, string.Format(lang.GetMessage("Player.NextWave", this, target.UserIDString), siguienteNivel));

                if (timersActivos.ContainsKey(target.userID))
                {
                    timersActivos[target.userID]?.Destroy();
                }

                timersActivos[target.userID] = timer.Once(30f, () =>
                {
                    if (target != null && target.IsConnected && jugadoresEnCaza.ContainsKey(target.userID))
                    {
                        SpawnGrupoColmena(target.transform.position, target, HordeType.Oleadas, siguienteNivel, siguienteNivel / 3);
                    }
                    timersActivos.Remove(target.userID);
                }); 
            }
            else if (grupo.IsPlayerOleadas && grupo.WaveLevel == 0)
            {
                SendReply(target, lang.GetMessage("Player.AsedioLive", this, target.UserIDString));
                FinalizarAsedio(target);
                LimpiarReferenciaGrupo(grupo);
            }
            else
            {
                LimpiarReferenciaGrupo(grupo);
            }
        }

        private void LimpiarReferenciaGrupo(HordeGroup grupo)
        {
            if (listaGrupos.Contains(grupo))
                listaGrupos.Remove(grupo);
        }

        // ==========================================
        // COMANDOS DE CHAT (ADMIN)
        // ==========================================
        [ChatCommand("verhorda")]
        void cmdVerHorda(BasePlayer p)
        {
            if (!p.IsAdmin) return;
            if (puntosSpawnAdmin.Count == 0)
            {
                SendReply(p, lang.GetMessage("Admin.EmpyPoints", this, p.UserIDString));
                return;
            }
            int indice = 0;
            timer.Repeat(15f, puntosSpawnAdmin.Count, () =>
            {
                if (p == null || !p.IsConnected) return;
                
                Vector3 punto = puntosSpawnAdmin[indice];
                p.Teleport(punto);
                
                p.SendConsoleCommand("ddraw.sphere", 5f, Color.cyan, punto, 2f);
                p.SendConsoleCommand("ddraw.text", 5f, Color.white, punto + Vector3.up * 2, $"PUNTO #{indice + 1}");
                SendReply(p, string.Format(lang.GetMessage("Admin.ShowHorda", this, p.UserIDString), $"{indice + 1}/{puntosSpawnAdmin.Count}"));
                indice++;
            });
        }

        [ChatCommand("remhorda")]
        void cmdRemHorda(BasePlayer p)
        {
            if (!p.IsAdmin) return;
                if (puntosSpawnAdmin.Count == 0) { SendReply(p, lang.GetMessage("Admin.EmpyPoints", this, p.UserIDString)); return; }

            Vector3 posJugador = p.transform.position;
            Vector3? puntoMasCercano = null;
            float distanciaMinimaSqr = 25f; 

            foreach (var punto in puntosSpawnAdmin)
            {
                float distSqr = (posJugador - punto).sqrMagnitude;
                if (distSqr < distanciaMinimaSqr)
                {
                    distanciaMinimaSqr = distSqr;
                    puntoMasCercano = punto;
                }
            }

            if (puntoMasCercano.HasValue)
            {
                puntosSpawnAdmin.Remove(puntoMasCercano.Value);
                SaveData();
                p.SendConsoleCommand("ddraw.sphere", 2f, Color.red, puntoMasCercano.Value, 5f);
                SendReply(p, lang.GetMessage("Admin.RemHorda", this, p.UserIDString));
            }
            else
            {
                SendReply(p, lang.GetMessage("Admin.RemHordaFail", this, p.UserIDString));
            }
        }

        [ChatCommand("addhorda")]
        void cmdAddHorda(BasePlayer p)
        {
            if (!p.IsAdmin) return;

            RaycastHit hit;
            if (Physics.Raycast(p.eyes.HeadRay(), out hit, 50f, mascaraConstruccion))
            {
                Vector3 punto = hit.point;
                punto.y += 0.1f; 
                
                puntosSpawnAdmin.Add(punto);
                SaveData();
                SendReply(p, string.Format(lang.GetMessage("Admin.AddHorda", this, p.UserIDString), puntosSpawnAdmin.Count));
                
                p.SendConsoleCommand("ddraw.sphere", 20f, Color.red, punto, 1f);
            }
        }

        
		[ChatCommand("hordaclean")]
		void cmdHordaClean(BasePlayer p)
		{
			if (p == null || !p.IsAdmin) return;
			
			int eliminados = 0;

			for (int i = todosLosZombies.Count - 1; i >= 0; i--)
			{
				var zombie = todosLosZombies.ElementAtOrDefault(i);
				if (zombie != null && !zombie.IsDestroyed)
				{
					zombie.Kill();
					eliminados++;
				}
			}
			
			todosLosZombies.Clear();
			NpcsEnGrupo.Clear();
			ultimoAtaqueBot.Clear();

			for (int i = 0; i < listaGrupos.Count; i++)
			{
				var grupo = listaGrupos[i];
				if (grupo?.Members != null)
				{
					grupo.Members.Clear();
				}
			}
			listaGrupos.Clear();
			
			SendReply(p, $"Se han eliminado {eliminados} NPCs y limpiado los grupos correctamente.");
		}

		[ChatCommand("hordaplayer")]
		void cmdHorda(BasePlayer p, string cmd, string[] args)
		{
			if (p == null || !p.IsAdmin) return;
			
			BasePlayer t = args.Length > 0 ? BasePlayer.Find(args[0]) : p;
			if (t == null) 
			{ 
				SendReply(p, lang.GetMessage("Admin.NoPlayer", this, p.UserIDString)); 
				return; 
			}

			if (jugadoresEnCaza.ContainsKey(t.userID))
			{
				jugadoresEnCaza.Remove(t.userID);
				
				for (int i = listaGrupos.Count - 1; i >= 0; i--)
				{
					var g = listaGrupos[i];
					if (g.Target != null && g.Target.userID == t.userID && g.IsPlayerOleadas)
					{
						for (int j = 0; j < g.Members.Count; j++)
						{
							if (g.Members[j] != null && !g.Members[j].IsDestroyed)
							{
								g.Members[j].Kill();
							}
						}
						listaGrupos.RemoveAt(i);
					}
				}
				
				SendReply(p, string.Format(lang.GetMessage("Admin.HordaPlayerEnd", this, p.UserIDString), t.displayName));
			}
			else
			{
				jugadoresEnCaza[t.userID] = true;
				SpawnGrupoColmena(t.transform.position, t, HordeType.Oleadas, 1, 1);
				
				SendReply(p, string.Format(lang.GetMessage("Admin.HordaPlayerAdmin", this, p.UserIDString), t.displayName));
				SendReply(t, lang.GetMessage("Admin.HordaPlayerTarget", this, t.UserIDString));    
			}
		}
	}
    // ==========================================
    // CLASES DE DATOS
    // ==========================================
    public class Vector3Serialized 
    { 
        public float x; 
        public float y; 
        public float z; 
    }

    class PluginData
    {
        public List<Vector3Serialized> Puntos = new List<Vector3Serialized>();
        public Dictionary<ulong, int> CooldownsAsedio = new Dictionary<ulong, int>();
    }
    public class ArmaConfig
    {
        public string shortname;
        public ulong skin;
    }
    
    class Configuration
    {
        public float SaludZombieNocturno = 200f;
       
        public bool Guardianes = true;
        public string[] MonumentosGuardianes = { 
            "harbor", "quarry", "plant", "satellite_dish", "military_tunnel", "ziggurat",
            "sphere", "supermarket", "oxum", "gas_station", "radtown", "mining_outpost", 
            "power_sub_big", "lighthouse", "warehouse", "junkyard", "branch", "water_treatment"
        };
        public int GruposGuardianesPorMonumento = 1;
        public int CantidadGuardianesPorGrupo = 1;
        public float SaludGuardian = 100f;
        public float SaludBaseWave = 150f;
        public float SaludBoss = 4500f;
        public float EscaladoVidaWave = 50f;
        public float VelocidadBase = 3.0f;
        public float VelocidadCazaLejana = 14.0f;
        [Newtonsoft.Json.JsonProperty(ObjectCreationHandling = Newtonsoft.Json.ObjectCreationHandling.Replace)]
        public Dictionary<string, ulong> RopaGuardian = new Dictionary<string, ulong>
        {
            { "metal.plate.torso", 3701498327 },
            { "pants", 3709919537 },
            { "hoodie", 3709921505 },
            { "leather.gloves", 3739054296 }
        };
        
        [Newtonsoft.Json.JsonProperty(ObjectCreationHandling = Newtonsoft.Json.ObjectCreationHandling.Replace)]
		public Dictionary<string, ulong> RopaNocturno = new Dictionary<string, ulong>
		{ 

		};
        
        public ArmaConfig ArmaGuardian = new ArmaConfig { shortname = "pistol.revolver", skin = 3706310455 };
        public bool TopNoche = true;
 
        public bool Asedio = true;
        public float TiempoMinimoAsedio = 300f;
        public int DiasDeCooldownAsedio = 1;
        public int CantidadBaseNocturnos = 3;
        public int CadaXdiasDeWipeMasNocturnos = 3;
        public int NocturnosPorDia = 1;
        public int CantidadBaseEvento = 3; 
        
        public float DistanciaActivacionIA = 200f;
        public int LimiteZombiesGlobal = 100;
        public int MaxIAActiva = 30;
    }
    public class NpcData
    {
        public string Name;
        public float Health;
        public float Speed;
        public bool Stationary;
        public List<ItemData> ClothingItems = new List<ItemData>();
        public List<ItemData> BeltItems = new List<ItemData>();
    }

    public class ItemData
    {
        public string ShortName;
        public ulong SkinID;
        public int Amount = 1;
    }
}