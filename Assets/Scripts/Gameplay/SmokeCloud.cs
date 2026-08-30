using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PolyStrike.Gameplay
{
    public sealed class SmokeCloud : MonoBehaviour
    {
        private const float BulletHoleDuration = 1.0f;
        private const float HeClearDuration = 2.0f;
        private const float FadeDuration = 2.5f;
        private const int CellCount = 24;

        private static readonly List<SmokeCloud> Active = new List<SmokeCloud>();
        private static Material smokeMaterial;

        private readonly List<Transform> cells = new List<Transform>();
        private readonly List<Renderer> renderers = new List<Renderer>();
        private float[] hiddenUntil;
        private float spawnTime;
        private float radiusMeters;

        public Vector3 Center => transform.position;
        public float RadiusMeters => radiusMeters;

        public static SmokeCloud Spawn(Vector3 position)
        {
            var root = new GameObject("Duman Bulutu");
            root.transform.position = position;
            var cloud = root.AddComponent<SmokeCloud>();
            cloud.Build();
            return cloud;
        }

        private void Build()
        {
            spawnTime = Time.time;
            radiusMeters = SourceUnit.ToMeters(144f);
            hiddenUntil = new float[CellCount];

            var goldenAngle = Mathf.PI * (3f - Mathf.Sqrt(5f));
            for (var i = 0; i < CellCount; i++)
            {
                var y = 1f - (i / (float)(CellCount - 1)) * 2f;
                var horizontal = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                var angle = goldenAngle * i;
                var normalized = new Vector3(Mathf.Cos(angle) * horizontal, y * 0.62f, Mathf.Sin(angle) * horizontal);

                var cell = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                cell.name = "Duman Hücresi";
                cell.transform.SetParent(transform, false);
                cell.transform.localPosition = normalized * radiusMeters * 0.58f + Vector3.up * radiusMeters * 0.32f;
                cell.transform.localScale = Vector3.one * radiusMeters * Random.Range(0.72f, 0.96f);

                var collider = cell.GetComponent<Collider>();
                if (collider != null)
                    Destroy(collider);

                var renderer = cell.GetComponent<Renderer>();
                renderer.sharedMaterial = GetSmokeMaterial();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                cells.Add(cell.transform);
                renderers.Add(renderer);
            }

            Active.Add(this);
            InfernoArea.CheckSmokeExtinguish(this);
        }

        private void Update()
        {
            var age = Time.time - spawnTime;
            if (age >= GrenadeRules.SmokeDuration)
            {
                Destroy(gameObject);
                return;
            }

            var fade = age <= GrenadeRules.SmokeDuration - FadeDuration
                ? 1f
                : Mathf.InverseLerp(GrenadeRules.SmokeDuration, GrenadeRules.SmokeDuration - FadeDuration, age);

            for (var i = 0; i < cells.Count; i++)
            {
                var visible = Time.time >= hiddenUntil[i];
                renderers[i].enabled = visible;

                if (!visible)
                    continue;

                var pulse = 1f + Mathf.Sin(age * 0.8f + i * 1.71f) * 0.025f;
                cells[i].localScale = Vector3.one * radiusMeters * 0.84f * pulse * Mathf.Lerp(0.78f, 1f, fade);
            }
        }

        private void OnDestroy()
        {
            Active.Remove(this);
        }

        public bool ContainsPoint(Vector3 point)
        {
            var flat = point - Center;
            flat.y *= 0.75f;
            return flat.sqrMagnitude <= radiusMeters * radiusMeters;
        }

        public static bool IsPointInsideAny(Vector3 point)
        {
            for (var i = Active.Count - 1; i >= 0; i--)
            {
                var cloud = Active[i];
                if (cloud != null && cloud.ContainsPoint(point))
                    return true;
            }

            return false;
        }

        public static void PunchLine(Vector3 start, Vector3 end)
        {
            for (var i = Active.Count - 1; i >= 0; i--)
            {
                var cloud = Active[i];
                if (cloud != null)
                    cloud.PunchCellsAlongLine(start, end);
            }
        }

        public static void BlastClear(Vector3 center)
        {
            var blastRadius = SourceUnit.ToMeters(350f);
            for (var i = Active.Count - 1; i >= 0; i--)
            {
                var cloud = Active[i];
                if (cloud != null)
                    cloud.ClearCellsInRadius(center, blastRadius);
            }
        }

        private void PunchCellsAlongLine(Vector3 start, Vector3 end)
        {
            var segment = end - start;
            var lengthSquared = segment.sqrMagnitude;
            if (lengthSquared < 0.0001f)
                return;

            for (var i = 0; i < cells.Count; i++)
            {
                var point = cells[i].position;
                var t = Mathf.Clamp01(Vector3.Dot(point - start, segment) / lengthSquared);
                var nearest = start + segment * t;
                if ((point - nearest).sqrMagnitude <= 0.58f * 0.58f)
                    hiddenUntil[i] = Mathf.Max(hiddenUntil[i], Time.time + BulletHoleDuration);
            }
        }

        private void ClearCellsInRadius(Vector3 blastCenter, float blastRadius)
        {
            var radiusSquared = blastRadius * blastRadius;
            for (var i = 0; i < cells.Count; i++)
            {
                if ((cells[i].position - blastCenter).sqrMagnitude <= radiusSquared)
                    hiddenUntil[i] = Mathf.Max(hiddenUntil[i], Time.time + HeClearDuration);
            }
        }

        private static Material GetSmokeMaterial()
        {
            if (smokeMaterial != null)
                return smokeMaterial;

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            if (shader == null)
                return null;

            smokeMaterial = new Material(shader);
            var color = new Color(0.34f, 0.36f, 0.38f, 0.64f);

            smokeMaterial.SetOverrideTag("RenderType", "Transparent");
            if (smokeMaterial.HasProperty("_Surface"))
                smokeMaterial.SetFloat("_Surface", 1f);
            if (smokeMaterial.HasProperty("_SrcBlend"))
                smokeMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (smokeMaterial.HasProperty("_DstBlend"))
                smokeMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (smokeMaterial.HasProperty("_ZWrite"))
                smokeMaterial.SetFloat("_ZWrite", 0f);
            if (smokeMaterial.HasProperty("_BaseColor"))
                smokeMaterial.SetColor("_BaseColor", color);
            else if (smokeMaterial.HasProperty("_Color"))
                smokeMaterial.SetColor("_Color", color);

            smokeMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            smokeMaterial.renderQueue = (int)RenderQueue.Transparent;
            return smokeMaterial;
        }
    }
}
