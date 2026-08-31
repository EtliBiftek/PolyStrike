using System;
using System.Collections;
using System.Collections.Generic;
using PolyStrike.Match;
using PolyStrike.Player;
using UnityEngine;

namespace PolyStrike.Gameplay
{
    public sealed class GrenadeEffects : MonoBehaviour
    {
        private const float HeRadiusUnits = 350f;
        private const float HeBaseDamage = 99f;
        private const float FlashRadiusUnits = 1500f;
        private const float FlashMaxDuration = 5.07f;
        private const float TeammateGrenadeDamageScale = 0.85f;

        private static GrenadeEffects instance;
        private static readonly Dictionary<SurfaceMaterial, AudioClip> BounceClips = new Dictionary<SurfaceMaterial, AudioClip>();
        private static AudioClip explosionClip;
        private static AudioClip flashClip;
        private static AudioClip smokeClip;
        private static AudioClip igniteClip;

        public static event Action<Vector3> FlashDetonated;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
        }

        public static void EnsureExists()
        {
            if (instance != null)
                return;

            var root = new GameObject("Utility Efektleri");
            instance = root.AddComponent<GrenadeEffects>();
        }

        public static void PlayBounce(Vector3 position, float speedSourceUnits, SurfaceMaterial surface)
        {
            EnsureExists();
            if (speedSourceUnits < 35f)
                return;

            var volume = Mathf.Lerp(0.12f, 0.58f, Mathf.InverseLerp(35f, 700f, speedSourceUnits));
            PlayWorldClip(GetBounceClip(surface), position, volume, 18f);
        }

        public static void Detonate(
            GrenadeType type,
            Vector3 position,
            MatchParticipant owner = null,
            bool incendiary = false)
        {
            EnsureExists();

            switch (type)
            {
                case GrenadeType.HighExplosive:
                    instance.DetonateHe(position, owner);
                    break;
                case GrenadeType.Flashbang:
                    instance.DetonateFlash(position);
                    break;
                case GrenadeType.Smoke:
                    instance.DetonateSmoke(position);
                    break;
                case GrenadeType.Molotov:
                    instance.DetonateMolotov(position, owner, incendiary);
                    break;
            }
        }

        private void DetonateHe(Vector3 position, MatchParticipant owner)
        {
            PlayWorldClip(GetExplosionClip(), position, 1f, 48f);
            StartCoroutine(ExplosionFlash(position, new Color(1f, 0.53f, 0.18f), 5.6f, 0.085f));
            SmokeCloud.BlastClear(position);

            var victims = Object.FindObjectsByType<Health>(FindObjectsSortMode.None);
            for (var i = 0; i < victims.Length; i++)
            {
                var health = victims[i];
                if (health == null || health.IsDead)
                    continue;

                var damagePoint = GetClosestDamagePoint(health, position);
                var distanceUnits = SourceUnit.ToSourceUnits(Vector3.Distance(position, damagePoint));
                if (distanceUnits >= HeRadiusUnits || !HasLineOfSight(position, damagePoint, health.transform))
                    continue;

                var rawDamage = HeBaseDamage * (1f - distanceUnits / HeRadiusUnits);
                rawDamage *= GetGrenadeDamageScale(owner, health.GetComponent<MatchParticipant>());

                var direction = (damagePoint - position).normalized;
                health.TakeGrenadeDamage(rawDamage, direction);
            }
        }

        private void DetonateFlash(Vector3 position)
        {
            PlayWorldClip(GetFlashClip(), position, 0.95f, 42f);
            StartCoroutine(ExplosionFlash(position, Color.white, 8.5f, 0.055f));
            FlashDetonated?.Invoke(position);

            var players = Object.FindObjectsByType<FlashEffect>(FindObjectsSortMode.None);
            for (var i = 0; i < players.Length; i++)
            {
                var flash = players[i];
                if (flash == null)
                    continue;

                var eye = flash.EyePosition;
                var distanceUnits = SourceUnit.ToSourceUnits(Vector3.Distance(position, eye));
                if (distanceUnits >= FlashRadiusUnits || !HasLineOfSight(position, eye, flash.transform))
                    continue;

                var toFlash = (position - eye).normalized;
                var facing = Vector3.Dot(flash.ViewForward, toFlash);
                var angleFactor = Mathf.Lerp(0.12f, 1f, Mathf.InverseLerp(-0.35f, 0.92f, facing));
                var distanceFactor = 1f - Mathf.Clamp01(distanceUnits / FlashRadiusUnits);
                distanceFactor = Mathf.Sqrt(distanceFactor);

                var intensity = Mathf.Clamp01(angleFactor * Mathf.Lerp(0.42f, 1f, distanceFactor));
                var duration = FlashMaxDuration * distanceFactor * Mathf.Lerp(0.22f, 1f, angleFactor);
                if (duration >= 0.08f)
                    flash.Apply(duration, intensity);
            }
        }

        private void DetonateSmoke(Vector3 position)
        {
            PlayWorldClip(GetSmokeClip(), position, 0.70f, 26f);
            SmokeCloud.Spawn(position);
        }

        private void DetonateMolotov(Vector3 position, MatchParticipant owner, bool incendiary)
        {
            if (SmokeCloud.IsPointInsideAny(position))
            {
                PlayWorldClip(GetSmokeClip(), position, 0.48f, 20f);
                return;
            }

            PlayWorldClip(GetIgniteClip(), position, 0.84f, 32f);
            StartCoroutine(ExplosionFlash(position, new Color(1f, 0.20f, 0.03f), 3.2f, 0.07f));
            InfernoArea.Spawn(position, owner, incendiary);
        }

        private static float GetGrenadeDamageScale(MatchParticipant owner, MatchParticipant victim)
        {
            if (owner == null || victim == null || owner.Team != victim.Team)
                return 1f;

            if (owner == victim)
                return 1f;

            return TeammateGrenadeDamageScale;
        }

        private static Vector3 GetClosestDamagePoint(Health health, Vector3 explosion)
        {
            var colliders = health.GetComponentsInChildren<Collider>();
            var bestPoint = health.transform.position + Vector3.up;
            var bestDistance = float.PositiveInfinity;

            for (var i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] == null)
                    continue;

                var point = colliders[i].ClosestPoint(explosion);
                var distance = (point - explosion).sqrMagnitude;
                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                bestPoint = point;
            }

            return bestPoint;
        }

        private static bool HasLineOfSight(Vector3 start, Vector3 end, Transform targetRoot)
        {
            var delta = end - start;
            var distance = delta.magnitude;
            if (distance <= 0.01f)
                return true;

            var hits = Physics.RaycastAll(start + delta.normalized * 0.02f, delta / distance, distance, ~0, QueryTriggerInteraction.Ignore);
            var nearestDistance = float.PositiveInfinity;
            Collider nearest = null;

            for (var i = 0; i < hits.Length; i++)
            {
                if (hits[i].distance >= nearestDistance)
                    continue;

                nearestDistance = hits[i].distance;
                nearest = hits[i].collider;
            }

            if (nearest == null)
                return true;

            var hitTransform = nearest.transform;
            return hitTransform == targetRoot || hitTransform.IsChildOf(targetRoot);
        }

        private IEnumerator ExplosionFlash(Vector3 position, Color color, float intensity, float duration)
        {
            var lightObject = new GameObject("Utility Parlaması");
            lightObject.transform.position = position;
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 7f;
            light.color = color;
            light.shadows = LightShadows.None;
            light.intensity = intensity;

            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                light.intensity = Mathf.Lerp(intensity, 0f, elapsed / duration);
                yield return null;
            }

            Destroy(lightObject);
        }

        private static void PlayWorldClip(AudioClip clip, Vector3 position, float volume, float maxDistance)
        {
            var sound = new GameObject("Utility Sesi");
            sound.transform.position = position;
            var source = sound.AddComponent<AudioSource>();
            source.clip = clip;
            source.volume = volume;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 1.2f;
            source.maxDistance = maxDistance;
            source.dopplerLevel = 0f;
            source.Play();
            Destroy(sound, clip.length + 0.15f);
        }

        private static AudioClip GetBounceClip(SurfaceMaterial surface)
        {
            if (BounceClips.TryGetValue(surface, out var clip))
                return clip;

            var frequency = surface == SurfaceMaterial.Metal || surface == SurfaceMaterial.Grate ? 1850f : 720f;
            if (surface == SurfaceMaterial.Wood || surface == SurfaceMaterial.Cardboard)
                frequency = 410f;

            clip = MakeBurst("Grenade Bounce", frequency, 0.055f, 0.55f, 22f);
            BounceClips[surface] = clip;
            return clip;
        }

        private static AudioClip GetExplosionClip()
        {
            return explosionClip ??= MakeBurst("HE Explosion", 115f, 0.42f, 0.95f, 4.6f);
        }

        private static AudioClip GetFlashClip()
        {
            return flashClip ??= MakeBurst("Flash Pop", 2450f, 0.14f, 0.72f, 17f);
        }

        private static AudioClip GetSmokeClip()
        {
            return smokeClip ??= MakeBurst("Smoke Deploy", 240f, 0.34f, 0.44f, 7.2f);
        }

        private static AudioClip GetIgniteClip()
        {
            return igniteClip ??= MakeBurst("Fire Grenade Ignite", 510f, 0.26f, 0.62f, 8.5f);
        }

        private static AudioClip MakeBurst(string name, float frequency, float duration, float amplitude, float decay)
        {
            const int sampleRate = 44100;
            var count = Mathf.CeilToInt(sampleRate * duration);
            var data = new float[count];
            var phase = Random.Range(0f, Mathf.PI * 2f);

            for (var i = 0; i < count; i++)
            {
                var t = i / (float)sampleRate;
                var envelope = Mathf.Exp(-decay * t);
                var tone = Mathf.Sin(phase + t * Mathf.PI * 2f * frequency);
                var noise = Random.Range(-1f, 1f) * 0.48f;
                data[i] = (tone * 0.52f + noise) * envelope * amplitude;
            }

            var clip = AudioClip.Create(name, count, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
