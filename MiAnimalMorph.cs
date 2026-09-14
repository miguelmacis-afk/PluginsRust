// Reference: UnityEngine.AnimationModule
using UnityEngine;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Animal Morph", "Assistant", "1.1.12")]
    class MiAnimalMorph : RustPlugin
    {
        private Dictionary<ulong, BaseEntity> morphedPlayers = new Dictionary<ulong, BaseEntity>();
        private Dictionary<ulong, Timer> morphTimers = new Dictionary<ulong, Timer>();
        private string[] availableAnimals = { "boar", "wolf", "bear", "tiger", "panther", "crocodile", "chicken" };

		
        [ChatCommand("morph")]
        void cmdMorph(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;

            if (args.Length == 0)
            {
                player.ChatMessage("Uso: /morph <boar|wolf|bear|chicken> o /morph off");
                return;
            }

            string animalName = args[0].ToLower();

            if (animalName == "off")
            {
                RemoveMorph(player);
                return;
            }

            MorphPlayer(player, animalName);
        }
 
        private void MorphPlayer(BasePlayer player, string animalName)
        {
            // Rutas estrictas y oficiales de Rust
            string prefabPath = "";
            switch (animalName)
            {
                                case "boar": prefabPath = "assets/rust.ai/agents/boar/boar.prefab"; break;
                case "wolf": prefabPath = "assets/rust.ai/agents/wolf/wolf2.prefab"; break;
                case "bear": prefabPath = "assets/rust.ai/agents/bear/bear.prefab"; break;
                case "tiger": prefabPath = "assets/rust.ai/agents/tiger/tiger.prefab"; break;
                case "panther": prefabPath = "assets/rust.ai/agents/panther/panther.prefab"; break;
                case "crocodile": prefabPath = "assets/rust.ai/agents/crocodile/crocodile.prefab"; break;
                case "chicken": prefabPath = "assets/rust.ai/agents/chicken/chicken.prefab"; break;
                default:
                    player.ChatMessage("Animal no válido. Usa: boar, wolf, bear, chicken.");
                    return;
            }

            RemoveMorph(player); // Limpiar transformaciones previas

            BaseEntity animal = GameManager.server.CreateEntity(prefabPath, player.transform.position, player.transform.rotation);
            if (animal == null) return;
            animal.Spawn();

            // --- DESACTIVACIÓN DE IA Y NAVEGACIÓN (CONSERVANDO SISTEMAS DE ATAQUE) ---
            foreach (var comp in animal.GetComponentsInChildren<MonoBehaviour>())
            {
                if (comp == null) continue;
                string name = comp.GetType().Name;
                
                if (name == "BaseEntity" || name == "BaseCombatEntity" || name == "Animator") continue;

                // Solo apagamos la IA, el cerebro, los sentidos y la navegación autónoma del lobo
                if (name.Contains("AI") || name.Contains("Brain") || name.Contains("Behavior") || 
                    name.Contains("Target") || name.Contains("Senses") || name.Contains("Wolf") || 
                    name.Contains("Navigator") || name.Contains("Nav") || name.Contains("Move") || 
                    name.Contains("Path") || name.Contains("Agent"))
                {
                    comp.enabled = false;
                }
            }

            // Ignorar colisiones físicas y desactivar triggers internos de daño por proximidad
            Collider[] animalColliders = animal.GetComponentsInChildren<Collider>();
            Collider[] playerColliders = player.GetComponentsInChildren<Collider>();
            
            foreach (var ac in animalColliders)
            {
                if (ac != null)
                {
                    if (ac.isTrigger)
                    {
                        ac.enabled = false;
                    }

                    foreach (var pc in playerColliders)
                    {
                        if (pc != null)
                        {
                            Physics.IgnoreCollision(ac, pc, true);
                        }
                    }
                }
            }

            player.limitNetworking = true;
            morphedPlayers[player.userID] = animal;

            Rigidbody rb = animal.GetComponent<Rigidbody>();
            Animator anim = animal.GetComponentInChildren<Animator>();

            // --- SISTEMA DE SEGUIMIENTO Y MOVIMIENTO MUNDIAL ---
            morphTimers[player.userID] = timer.Repeat(0.05f, 0, () =>
            {
                if (player == null || player.IsDead() || animal == null || animal.IsDestroyed) return;

                // 1. Sincronizar posición exacta con el jugador
                animal.transform.position = player.transform.position;

                // 2. Rotación fluida hacia donde mira la cámara
                Vector3 lookDir = player.eyes.BodyForward();
                lookDir.y = 0; 
                if (lookDir != Vector3.zero)
                {
                    animal.transform.rotation = Quaternion.LookRotation(lookDir);
                }

                // 3. Pasar velocidad al Rigidbody y al Animator
                if (rb != null)
                {
                    rb.velocity = player.estimatedVelocity;
                }

                if (anim != null)
                {
                    bool isMoving = player.GetLocalVelocity().sqrMagnitude > 0.05f;
                    anim.SetBool("isMoving", isMoving);
                    anim.SetFloat("forward", isMoving ? 1f : 0f);
                    anim.SetFloat("velocity", isMoving ? 2f : 0f);
                    anim.SetFloat("speed", isMoving ? 2f : 0f);
                }

                animal.SendNetworkUpdate();
            });

            player.ChatMessage($"Te has transformado en {animalName}.");
        }

        private void RemoveMorph(BasePlayer player)
        {
            if (morphTimers.TryGetValue(player.userID, out Timer t))
            {
                t?.Destroy();
                morphTimers.Remove(player.userID);
            }

            if (morphedPlayers.TryGetValue(player.userID, out BaseEntity animal))
            {
                if (animal != null && !animal.IsDestroyed)
                {
                    animal.Kill();
                }
                morphedPlayers.Remove(player.userID);
                
                player.limitNetworking = false; 
                player.ChatMessage("Has vuelto a tu forma normal.");
            }
        }

        // --- SISTEMA DE ATAQUE (3 METROS) ---
        void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (!morphedPlayers.TryGetValue(player.userID, out BaseEntity animal)) return;
            if (animal == null || animal.IsDestroyed) return;

            if (input.WasJustPressed(BUTTON.FIRE_PRIMARY))
            {
                animal.SignalBroadcast(BaseEntity.Signal.Attack, string.Empty);

                List<BasePlayer> nearbyPlayers = Facepunch.Pool.Get<List<BasePlayer>>();
				
                Vis.Entities<BasePlayer>(player.transform.position, 3f, nearbyPlayers);

                foreach (BasePlayer target in nearbyPlayers)
                {
                    if (target == player || target.IsDead()) continue;

                    Vector3 directionToTarget = (target.transform.position - player.transform.position).normalized;
                    if (Vector3.Dot(player.eyes.BodyForward(), directionToTarget) > 0.4f)
                    {
                        target.Hurt(25f, Rust.DamageType.Bite, player);
                        break; 
                    }
                }
                Facepunch.Pool.FreeUnmanaged(ref nearbyPlayers);
            }
        }
        
        // --- SISTEMA DE RESPAWN CON CADÁVER FARMEABLE ---
        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            BasePlayer player = entity as BasePlayer;

            if (player != null && morphedPlayers.TryGetValue(player.userID, out BaseEntity currentAnimal))
            {
                if (player.health - (info != null ? info.damageTypes.Total() : 0f) <= 0f)
                {
                    HandleMorphDeath(player, currentAnimal as BaseCombatEntity);
                    return true; 
                }
                return null;
            }

            if (entity != null)
            {
                foreach (var kvp in morphedPlayers)
                {
                    if (kvp.Value == entity)
                    {
                        BasePlayer owner = BasePlayer.FindByID(kvp.Key);
                        if (owner != null)
                        {
                            if (entity.health - (info != null ? info.damageTypes.Total() : 0f) <= 0f)
                            {
                                HandleMorphDeath(owner, entity);
                                return true; 
                            }
                        }
                        break;
                    }
                }
            }

            return null;
        }

        private void HandleMorphDeath(BasePlayer player, BaseCombatEntity oldAnimal)
        {
            if (morphTimers.TryGetValue(player.userID, out Timer t))
            {
                t?.Destroy();
                morphTimers.Remove(player.userID);
            }

            if (oldAnimal != null && !oldAnimal.IsDestroyed)
            {
                morphedPlayers.Remove(player.userID); 
                oldAnimal.DieInstantly(); 
            }
            else
            {
                morphedPlayers.Remove(player.userID);
            }

            player.health = player.MaxHealth();
            if (player.metabolism != null)
            {
                player.metabolism.bleeding.value = 0f;
                player.metabolism.radiation_poison.value = 0f;
            }

            Vector2 randomDirection = UnityEngine.Random.insideUnitCircle.normalized * 50f;
            Vector3 newPos = player.transform.position + new Vector3(randomDirection.x, 0, randomDirection.y);
            
            float terrainHeight = TerrainMeta.HeightMap.GetHeight(newPos);
            float waterHeight = TerrainMeta.WaterMap.GetHeight(newPos);
            newPos.y = Mathf.Max(terrainHeight, waterHeight); 

            player.Teleport(newPos);

            string nextAnimal = availableAnimals[UnityEngine.Random.Range(0, availableAnimals.Length)];
            MorphPlayer(player, nextAnimal);
            
            player.ChatMessage($"¡Tu animal ha muerto dejando un cadáver! Has resurgido a 50 metros siendo un {nextAnimal}.");
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            RemoveMorph(player);
        }
        
        void Unload()
        {
            foreach (var playerID in new List<ulong>(morphedPlayers.Keys))
            {
                BasePlayer player = BasePlayer.FindByID(playerID);
                if (player != null) RemoveMorph(player);
            }
        }
    }
}