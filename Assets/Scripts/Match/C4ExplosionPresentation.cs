using System.Collections;
using System.Collections.Generic;
using PolyStrike.Gameplay;
using UnityEngine;

namespace PolyStrike.Match
{
    public sealed class C4ExplosionPresentation : MonoBehaviour
    {
        private static Material shockwaveMaterial;
        private static AudioClip explosionClip;

        private readonly HashSet<Rigidbody> pushedBodies = new HashSet<Rigidbody>();

        public static void Play(Vector3 position)
        {
            var root = new GameObject("C4 Patlama Efekti");
            root.transform.position = position;
            root.AddComponent<C4ExplosionPresentation>().Begin();
        }

        private void Begin()
        {
            PlaySound();
            StartCoroutine(AnimateExplosion());
        }

        private void PlaySound()
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.clip = GetExplosionClip();
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 4f;
            source.maxDistance = 100f;
            source.dopplerLevel = 0f;
            source.volume = 1f;
            source.Play();
        }

        private IEnumerator AnimateExplosion()
        {
            var light = gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.shadows = LightShadows.None;
            light.color = new Color(1f, 0.48f, 0.13f);
            light.range = 32f;
            light.intensity = 16f;

            var shockwave = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            shockwave.name = "C4 Şok Dalgası";
            shockwave.transform.SetParent(transform, false);
            shockwave.transform.localScale = Vector3.one * 0.15f;

            var collider = shockwave.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            shockwave.GetComponent<Renderer>().material = GetShockwaveMaterial();
            SpawnDebris();

            var elapsed = 0f;
            const float expansionTime = 0.34f;
            const float maxRadius = 18f;

            while (elapsed < expansionTime)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / expansionTime);
                var eased = 1f - Mathf.Pow(1f - t, 3f);
                var radius = Mathf.Lerp(0.15f, maxRadius, eased);

                shockwave.transform.localScale = Vector3.one * radius;
                light.intensity = Mathf.Lerp(16f, 0f, t);

                SmokeCloud.ShockwaveClear(transform.position, radius);
                InfernoArea.ShockwaveExtinguish(transform.position, radius);
                PushDroppedItems(radius);
                yield return null;
            }

            Destroy(shockwave);
            light.intensity = 0f;
            Destroy(gameObject, Mathf.Max(0.05f, GetExplosionClip().length - expansionTime));
        }

        private void PushDroppedItems(float radius)
        {
            var colliders = Physics.OverlapSphere(transform.position, radius, ~0, QueryTriggerInteraction.Collide);
            for (var i = 0; i < colliders.Length; i++)
            {
                var item = colliders[i].GetComponentInParent<DroppedMatchItem>();
                if (item == null)
                    continue;

                var body = item.GetComponent<Rigidbody>();
                if (body == null || !pushedBodies.Add(body))
                    continue;

                var delta = body.worldCenterOfMass - transform.position;
                var direction = delta.sqrMagnitude > 0.001f ? delta.normalized : Vector3.up;
                direction = (direction + Vector3.up * 0.28f).normalized;
                body.AddForce(direction * 8.5f, ForceMode.Impulse);
            }
        }

        private void SpawnDebris()
        {
            var particles = gameObject.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 0.35f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.85f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(8f, 18f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.12f);
            main.gravityModifier = 1.2f;
            main.maxParticles = 64;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.5f;

            particles.Emit(48);
            particles.Play();
        }

        private static Material GetShockwaveMaterial()
        {
            if (shockwaveMaterial != null)
                return shockwaveMaterial;

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            shockwaveMaterial = new Material(shader);
            var color = new Color(1f, 0.52f, 0.18f, 0.18f);
            if (shockwaveMaterial.HasProperty("_BaseColor"))
                shockwaveMaterial.SetColor("_BaseColor", color);
            else
                shockwaveMaterial.color = color;

            if (shockwaveMaterial.HasProperty("_Surface"))
                shockwaveMaterial.SetFloat("_Surface", 1f);
            if (shockwaveMaterial.HasProperty("_ZWrite"))
                shockwaveMaterial.SetFloat("_ZWrite", 0f);
            shockwaveMaterial.renderQueue = 3000;
            return shockwaveMaterial;
        }

        private static AudioClip GetExplosionClip()
        {
            if (explosionClip != null)
                return explosionClip;

            const int sampleRate = 44100;
            const float duration = 1.35f;
            var count = Mathf.CeilToInt(sampleRate * duration);
            var data = new float[count];
            var random = new System.Random(14072026);

            for (var i = 0; i < count; i++)
            {
                var t = i / (float)sampleRate;
                var blastEnvelope = Mathf.Exp(-t * 3.8f);
                var crackEnvelope = Mathf.Exp(-t * 34f);
                var noise = (float)(random.NextDouble() * 2.0 - 1.0);
                var low = Mathf.Sin(t * Mathf.PI * 2f * 48f) * 0.52f;
                var mid = Mathf.Sin(t * Mathf.PI * 2f * 112f) * 0.25f;
                var crack = noise * crackEnvelope * 0.8f;
                data[i] = Mathf.Clamp((low + mid + noise * 0.30f) * blastEnvelope + crack, -1f, 1f) * 0.82f;
            }

            explosionClip = AudioClip.Create("C4 Explosion", count, 1, sampleRate, false);
            explosionClip.SetData(data, 0);
            return explosionClip;
        }
    }
}
