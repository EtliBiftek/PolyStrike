using System.Collections.Generic;
using PolyStrike.Gameplay;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;

namespace PolyStrike.Networking
{
    [DisallowMultipleComponent]
    public sealed class NetworkUtilityPresentation : MonoBehaviour
    {
        private readonly Dictionary<Entity, uint> lastThrowSequence = new Dictionary<Entity, uint>();
        private readonly Dictionary<Entity, uint> lastDetonateSequence = new Dictionary<Entity, uint>();
        private readonly Dictionary<Entity, GameObject> activeProjectiles = new Dictionary<Entity, GameObject>();
        private readonly List<Entity> staleEntities = new List<Entity>();

        private NetworkFlashState localFlash;
        private bool hasLocalFlash;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (FindFirstObjectByType<NetworkUtilityPresentation>() != null)
                return;

            var root = new GameObject("PolyStrike Utility Presentation");
            DontDestroyOnLoad(root);
            root.AddComponent<NetworkUtilityPresentation>();
        }

        private void Update()
        {
            var world = ClientServerBootstrap.ClientWorld;
            if (world == null || !world.IsCreated)
            {
                ClearProjectiles();
                hasLocalFlash = false;
                return;
            }

            var entityManager = world.EntityManager;
            var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkUtilityPresentationState>());
            var entities = query.ToEntityArray(Allocator.Temp);

            staleEntities.Clear();
            foreach (var pair in lastThrowSequence)
                staleEntities.Add(pair.Key);

            hasLocalFlash = false;
            for (var i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                var utility = entityManager.GetComponentData<NetworkUtilityPresentationState>(entity);
                staleEntities.Remove(entity);

                if (!lastThrowSequence.TryGetValue(entity, out var lastThrow))
                {
                    lastThrowSequence[entity] = utility.ThrowSequence;
                    lastDetonateSequence[entity] = utility.DetonateSequence;
                }
                else
                {
                    if (utility.ThrowSequence != lastThrow)
                    {
                        lastThrowSequence[entity] = utility.ThrowSequence;
                        SpawnProjectile(entity, in utility);
                    }

                    var lastDetonate = lastDetonateSequence.TryGetValue(entity, out var sequence) ? sequence : 0u;
                    if (utility.DetonateSequence != lastDetonate)
                    {
                        lastDetonateSequence[entity] = utility.DetonateSequence;
                        PresentDetonation(entity, in utility);
                    }
                }

                if (entityManager.HasComponent<GhostOwnerIsLocal>(entity) &&
                    entityManager.HasComponent<NetworkFlashState>(entity))
                {
                    localFlash = entityManager.GetComponentData<NetworkFlashState>(entity);
                    hasLocalFlash = localFlash.Remaining > 0f && localFlash.Intensity > 0f;
                }
            }

            for (var i = 0; i < staleEntities.Count; i++)
                ForgetEntity(staleEntities[i]);

            entities.Dispose();
            query.Dispose();
        }

        private void OnGUI()
        {
            if (!hasLocalFlash)
                return;

            var previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(localFlash.Intensity));
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private void SpawnProjectile(Entity owner, in NetworkUtilityPresentationState state)
        {
            if (activeProjectiles.TryGetValue(owner, out var previous) && previous != null)
                Destroy(previous);

            var visual = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual.name = "Network Grenade";
            visual.transform.position = ToVector3(state.ThrowPosition);
            visual.transform.localScale = Vector3.one * 0.16f;

            var collider = visual.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            var renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
                SetRendererColor(renderer, GrenadeColor((GrenadeType)state.ThrowType));

            var motion = visual.AddComponent<NetworkGrenadeVisual>();
            motion.Initialize(ToVector3(state.ThrowVelocity));
            activeProjectiles[owner] = visual;
        }

        private void PresentDetonation(Entity owner, in NetworkUtilityPresentationState state)
        {
            if (activeProjectiles.TryGetValue(owner, out var projectile) && projectile != null)
                Destroy(projectile);
            activeProjectiles.Remove(owner);

            var position = ToVector3(state.DetonatePosition);
            switch ((GrenadeType)state.DetonateType)
            {
                case GrenadeType.HighExplosive:
                    SpawnBurst(position, new Color(1f, 0.47f, 0.10f), 1.1f, 0.14f, 8f);
                    break;
                case GrenadeType.Flashbang:
                    SpawnBurst(position, Color.white, 0.9f, 0.10f, 10f);
                    break;
                case GrenadeType.Smoke:
                    SpawnArea(position, new Color(0.34f, 0.36f, 0.38f), new Vector3(4.4f, 3.5f, 4.4f), GrenadeRules.SmokeDuration, "Network Smoke");
                    break;
                case GrenadeType.Molotov:
                    SpawnArea(position + Vector3.up * 0.035f, new Color(1f, 0.20f, 0.03f), new Vector3(4.8f, 0.07f, 4.8f), GrenadeRules.MolotovLifetime, "Network Inferno");
                    SpawnBurst(position, new Color(1f, 0.22f, 0.03f), 0.7f, 0.16f, 6f);
                    break;
            }
        }

        private static void SpawnBurst(Vector3 position, Color color, float scale, float lifetime, float lightIntensity)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            root.name = "Network Utility Burst";
            root.transform.position = position;
            root.transform.localScale = Vector3.one * scale;

            var collider = root.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            var renderer = root.GetComponent<Renderer>();
            if (renderer != null)
                SetRendererColor(renderer, color);

            var light = root.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 7f;
            light.intensity = lightIntensity;
            light.color = color;
            light.shadows = LightShadows.None;

            var timed = root.AddComponent<NetworkTimedUtilityVisual>();
            timed.Lifetime = lifetime;
        }

        private static void SpawnArea(Vector3 position, Color color, Vector3 scale, float lifetime, string name)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            root.name = name;
            root.transform.position = position;
            root.transform.localScale = scale;

            var collider = root.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            var renderer = root.GetComponent<Renderer>();
            if (renderer != null)
                SetRendererColor(renderer, color);

            var timed = root.AddComponent<NetworkTimedUtilityVisual>();
            timed.Lifetime = lifetime;
        }

        private static Color GrenadeColor(GrenadeType type)
        {
            return type switch
            {
                GrenadeType.HighExplosive => new Color(0.20f, 0.30f, 0.16f),
                GrenadeType.Flashbang => new Color(0.76f, 0.76f, 0.72f),
                GrenadeType.Smoke => new Color(0.42f, 0.45f, 0.47f),
                GrenadeType.Molotov => new Color(0.38f, 0.18f, 0.08f),
                _ => Color.gray
            };
        }

        private static void SetRendererColor(Renderer renderer, Color color)
        {
            var material = renderer.material;
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            else
                material.color = color;
        }

        private void ForgetEntity(Entity entity)
        {
            lastThrowSequence.Remove(entity);
            lastDetonateSequence.Remove(entity);
            if (activeProjectiles.TryGetValue(entity, out var projectile) && projectile != null)
                Destroy(projectile);
            activeProjectiles.Remove(entity);
        }

        private void ClearProjectiles()
        {
            foreach (var pair in activeProjectiles)
            {
                if (pair.Value != null)
                    Destroy(pair.Value);
            }

            activeProjectiles.Clear();
            lastThrowSequence.Clear();
            lastDetonateSequence.Clear();
        }

        private static Vector3 ToVector3(float3 value) => new Vector3(value.x, value.y, value.z);
    }

    public sealed class NetworkGrenadeVisual : MonoBehaviour
    {
        private const float SourceUnitsPerMeter = 39.37f;
        private Vector3 velocity;

        public void Initialize(Vector3 initialVelocity)
        {
            velocity = initialVelocity;
        }

        private void Update()
        {
            velocity.y -= GrenadeRules.Gravity / SourceUnitsPerMeter * Time.deltaTime;
            transform.position += velocity * Time.deltaTime;

            var floor = NetworkSandlineCollision.GroundY + 0.06f;
            if (transform.position.y >= floor || velocity.y >= 0f)
                return;

            var position = transform.position;
            position.y = floor;
            transform.position = position;
            velocity.y = -velocity.y * GrenadeRules.BounceScale;
            velocity.x *= 0.82f;
            velocity.z *= 0.82f;
        }
    }

    public sealed class NetworkTimedUtilityVisual : MonoBehaviour
    {
        public float Lifetime { get; set; }

        private void Update()
        {
            Lifetime -= Time.deltaTime;
            if (Lifetime <= 0f)
                Destroy(gameObject);
        }
    }
}
