using System.Collections;
using System.Collections.Generic;
using PolyStrike.Match;
using UnityEngine;

namespace PolyStrike.Gameplay
{
    public sealed class InfernoArea : MonoBehaviour
    {
        private const int FlameCount = 16;
        private const float PeakSpreadTime = 1.83f;
        private const float TeammateOtherDamageScale = 0.4f;

        private static readonly List<InfernoArea> Active = new List<InfernoArea>();
        private static Material flameMaterial;

        private readonly List<Transform> flames = new List<Transform>();
        private readonly List<Vector3> flamePositions = new List<Vector3>();
        private MatchParticipant owner;
        private float spawnTime;
        private float radiusMeters;
        private float lifetime;

        public static InfernoArea Spawn(Vector3 position, MatchParticipant thrower = null, bool incendiary = false)
        {
            var root = new GameObject(incendiary ? "Incendiary Alanı" : "Molotof Alanı");
            root.transform.position = position;
            var inferno = root.AddComponent<InfernoArea>();
            inferno.Build(thrower, incendiary);
            return inferno;
        }

        private void Build(MatchParticipant thrower, bool incendiary)
        {
            owner = thrower;
            spawnTime = Time.time;
            lifetime = incendiary ? GrenadeRules.IncendiaryLifetime : GrenadeRules.MolotovLifetime;
            var radiusUnits = incendiary ? GrenadeRules.IncendiaryMaxRange : GrenadeRules.MolotovMaxRange;
            radiusMeters = SourceUnit.ToMeters(radiusUnits);
            Active.Add(this);

            for (var i = 0; i < FlameCount; i++)
            {
                var fraction = i / (float)(FlameCount - 1);
                var angle = i * 2.3999632f;
                var radius = Mathf.Sqrt(fraction) * radiusMeters;
                var offset = new Vector3(Mathf.Cos(angle) * radius, 0.035f, Mathf.Sin(angle) * radius);
                flamePositions.Add(transform.position + offset);

                var flame = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                flame.name = "Alev";
                flame.transform.SetParent(transform, false);
                flame.transform.localPosition = offset;
                flame.transform.localScale = new Vector3(0.24f, 0.035f, 0.24f);

                var collider = flame.GetComponent<Collider>();
                if (collider != null)
                    Destroy(collider);

                flame.GetComponent<Renderer>().material = GetFlameMaterial();
                flames.Add(flame.transform);
            }

            StartCoroutine(DamageLoop());
        }

        private void Update()
        {
            var age = Time.time - spawnTime;
            if (age >= lifetime)
            {
                Destroy(gameObject);
                return;
            }

            var spread = Mathf.Clamp01(age / PeakSpreadTime);
            var fade = Mathf.InverseLerp(lifetime, lifetime - 1f, age);

            for (var i = 0; i < flames.Count; i++)
            {
                var activation = i / (float)FlameCount;
                var active = spread >= activation;
                flames[i].gameObject.SetActive(active);
                if (!active)
                    continue;

                var pulse = 0.82f + Mathf.Sin(Time.time * 10f + i * 1.7f) * 0.12f;
                flames[i].localScale = new Vector3(0.24f, 0.035f + pulse * 0.06f, 0.24f) * Mathf.Clamp01(fade);
            }
        }

        private IEnumerator DamageLoop()
        {
            var wait = new WaitForSeconds(GrenadeRules.InfernoDamageTick);
            var victims = new HashSet<Health>();

            while (this != null && Time.time - spawnTime < lifetime)
            {
                victims.Clear();
                var age = Time.time - spawnTime;
                var activeFraction = Mathf.Clamp01(age / PeakSpreadTime);
                var activeFlames = Mathf.Max(1, Mathf.FloorToInt(activeFraction * FlameCount));

                for (var i = 0; i < activeFlames && i < flamePositions.Count; i++)
                {
                    var colliders = Physics.OverlapSphere(flamePositions[i], SourceUnit.ToMeters(31f), ~0, QueryTriggerInteraction.Ignore);
                    for (var c = 0; c < colliders.Length; c++)
                    {
                        var health = colliders[c].GetComponentInParent<Health>();
                        if (health != null && !health.IsDead)
                            victims.Add(health);
                    }
                }

                foreach (var health in victims)
                {
                    var scale = GetDamageScale(health.GetComponent<MatchParticipant>());
                    health.TakeDamage(GrenadeRules.InfernoDamagePerSecond * GrenadeRules.InfernoDamageTick * scale);
                }

                yield return wait;
            }
        }

        private float GetDamageScale(MatchParticipant victim)
        {
            if (owner == null || victim == null || owner.Team != victim.Team || owner == victim)
                return 1f;

            return TeammateOtherDamageScale;
        }

        private void OnDestroy()
        {
            Active.Remove(this);
        }

        public static void CheckSmokeExtinguish(SmokeCloud smoke)
        {
            if (smoke == null)
                return;

            for (var i = Active.Count - 1; i >= 0; i--)
            {
                var inferno = Active[i];
                if (inferno == null)
                    continue;

                if (inferno.GetSmokeCoverage(smoke) > 1f / 3f)
                    Destroy(inferno.gameObject);
            }
        }

        public static void ShockwaveExtinguish(Vector3 center, float radiusMeters)
        {
            if (radiusMeters <= 0f)
                return;

            var radiusSquared = radiusMeters * radiusMeters;
            for (var i = Active.Count - 1; i >= 0; i--)
            {
                var inferno = Active[i];
                if (inferno == null)
                    continue;

                if (inferno.IsTouchedByShockwave(center, radiusSquared))
                    Destroy(inferno.gameObject);
            }
        }

        private bool IsTouchedByShockwave(Vector3 center, float radiusSquared)
        {
            for (var i = 0; i < flamePositions.Count; i++)
            {
                if ((flamePositions[i] - center).sqrMagnitude <= radiusSquared)
                    return true;
            }

            return false;
        }

        private float GetSmokeCoverage(SmokeCloud smoke)
        {
            var covered = 0;
            for (var i = 0; i < flamePositions.Count; i++)
            {
                if (smoke.ContainsPoint(flamePositions[i]))
                    covered++;
            }

            return flamePositions.Count == 0 ? 0f : covered / (float)flamePositions.Count;
        }

        private static Material GetFlameMaterial()
        {
            if (flameMaterial != null)
                return flameMaterial;

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");

            flameMaterial = new Material(shader);
            var color = new Color(1f, 0.24f, 0.025f, 0.92f);
            if (flameMaterial.HasProperty("_BaseColor"))
                flameMaterial.SetColor("_BaseColor", color);
            else
                flameMaterial.color = color;

            return flameMaterial;
        }
    }
}
