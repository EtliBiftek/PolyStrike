using System;
using System.Collections.Generic;
using PolyStrike.Gameplay;
using UnityEngine;

namespace PolyStrike.Audio
{
    public static class ProceduralSfxBank
    {
        private const int SampleRate = 44100;
        private static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();

        public static AudioClip WeaponShot(int style)
        {
            var key = $"weapon_{style}";
            if (Clips.TryGetValue(key, out var cached))
                return cached;

            var seed = style == 0 ? 73041 : 73042;
            var length = style == 0 ? 0.22f : 0.20f;
            var lowTone = style == 0 ? 92f : 108f;
            var crackTone = style == 0 ? 1420f : 1560f;
            var clip = BuildWeaponShot(key, seed, length, lowTone, crackTone);
            Clips[key] = clip;
            return clip;
        }

        public static AudioClip ReloadStart(int style)
        {
            return GetMechanical($"reload_start_{style}", 0.115f, style == 0 ? 7701 : 7702, 280f, 0.70f);
        }

        public static AudioClip ReloadInsert(int style)
        {
            return GetMechanical($"reload_insert_{style}", 0.09f, style == 0 ? 7801 : 7802, 520f, 0.82f);
        }

        public static AudioClip Deploy(int style)
        {
            return GetMechanical($"deploy_{style}", 0.13f, style == 0 ? 7901 : 7902, 360f, 0.62f);
        }

        public static AudioClip Footstep(SurfaceMaterial material, int variant)
        {
            variant &= 3;
            var key = $"step_{material}_{variant}";
            if (Clips.TryGetValue(key, out var cached))
                return cached;

            var clip = BuildSurfaceSound(key, material, variant, false);
            Clips[key] = clip;
            return clip;
        }

        public static AudioClip Landing(SurfaceMaterial material)
        {
            var key = $"land_{material}";
            if (Clips.TryGetValue(key, out var cached))
                return cached;

            var clip = BuildSurfaceSound(key, material, 8, true);
            Clips[key] = clip;
            return clip;
        }

        public static AudioClip SurfaceImpact(SurfaceMaterial material)
        {
            var key = $"impact_{material}";
            if (Clips.TryGetValue(key, out var cached))
                return cached;

            var clip = BuildImpact(key, material);
            Clips[key] = clip;
            return clip;
        }

        public static AudioClip FleshImpact(bool headshot)
        {
            var key = headshot ? "hit_head" : "hit_body";
            if (Clips.TryGetValue(key, out var cached))
                return cached;

            var clip = BuildFleshImpact(key, headshot);
            Clips[key] = clip;
            return clip;
        }

        public static AudioClip Jump()
        {
            const string key = "jump";
            if (Clips.TryGetValue(key, out var cached))
                return cached;

            var clip = BuildNoiseBurst(key, 0.09f, 9917, 0.24f, 11f, 170f);
            Clips[key] = clip;
            return clip;
        }

        private static AudioClip BuildWeaponShot(string name, int seed, float duration, float lowTone, float crackTone)
        {
            var samples = Mathf.CeilToInt(duration * SampleRate);
            var data = new float[samples];
            var random = new System.Random(seed);

            for (var i = 0; i < samples; i++)
            {
                var t = i / (float)SampleRate;
                var bodyEnvelope = Mathf.Exp(-t * 17f);
                var crackEnvelope = Mathf.Exp(-t * 72f);
                var tailEnvelope = Mathf.Exp(-t * 9f);
                var noise = (float)(random.NextDouble() * 2.0 - 1.0);
                var body = Mathf.Sin(t * Mathf.PI * 2f * lowTone) * bodyEnvelope * 0.42f;
                var crack = Mathf.Sin(t * Mathf.PI * 2f * crackTone) * crackEnvelope * 0.28f;
                var blast = noise * (crackEnvelope * 0.68f + tailEnvelope * 0.18f);

                data[i] = Mathf.Clamp(body + crack + blast, -1f, 1f) * 0.78f;
            }

            return CreateClip(name, data);
        }

        private static AudioClip BuildSurfaceSound(string name, SurfaceMaterial material, int variant, bool landing)
        {
            var duration = landing ? 0.16f : 0.105f;
            var samples = Mathf.CeilToInt(duration * SampleRate);
            var data = new float[samples];
            var random = new System.Random(4100 + (int)material * 37 + variant * 11);

            GetSurfaceTone(material, out var tone, out var noiseAmount, out var brightness);
            var strength = landing ? 1f : 0.66f;

            for (var i = 0; i < samples; i++)
            {
                var t = i / (float)SampleRate;
                var envelope = Mathf.Exp(-t * (landing ? 18f : 28f));
                var noise = (float)(random.NextDouble() * 2.0 - 1.0);
                var baseTone = Mathf.Sin(t * Mathf.PI * 2f * tone) * 0.34f;
                var upperTone = Mathf.Sin(t * Mathf.PI * 2f * tone * brightness) * 0.17f;
                var grit = noise * noiseAmount;
                data[i] = Mathf.Clamp((baseTone + upperTone + grit) * envelope * strength, -1f, 1f);
            }

            return CreateClip(name, data);
        }

        private static AudioClip BuildImpact(string name, SurfaceMaterial material)
        {
            GetSurfaceTone(material, out var tone, out var noiseAmount, out var brightness);
            var duration = material == SurfaceMaterial.Glass ? 0.16f : 0.09f;
            var samples = Mathf.CeilToInt(duration * SampleRate);
            var data = new float[samples];
            var random = new System.Random(6200 + (int)material * 71);

            for (var i = 0; i < samples; i++)
            {
                var t = i / (float)SampleRate;
                var envelope = Mathf.Exp(-t * (material == SurfaceMaterial.Glass ? 20f : 45f));
                var noise = (float)(random.NextDouble() * 2.0 - 1.0);
                var ring = Mathf.Sin(t * Mathf.PI * 2f * tone * brightness) * 0.42f;
                var click = Mathf.Sin(t * Mathf.PI * 2f * tone * 3.1f) * Mathf.Exp(-t * 90f) * 0.22f;
                data[i] = Mathf.Clamp((ring + click + noise * noiseAmount) * envelope * 0.72f, -1f, 1f);
            }

            return CreateClip(name, data);
        }

        private static AudioClip BuildFleshImpact(string name, bool headshot)
        {
            var duration = headshot ? 0.10f : 0.085f;
            var samples = Mathf.CeilToInt(duration * SampleRate);
            var data = new float[samples];
            var random = new System.Random(headshot ? 8809 : 8801);

            for (var i = 0; i < samples; i++)
            {
                var t = i / (float)SampleRate;
                var envelope = Mathf.Exp(-t * (headshot ? 36f : 42f));
                var noise = (float)(random.NextDouble() * 2.0 - 1.0);
                var low = Mathf.Sin(t * Mathf.PI * 2f * (headshot ? 210f : 145f)) * 0.38f;
                var snap = headshot ? Mathf.Sin(t * Mathf.PI * 2f * 1750f) * Mathf.Exp(-t * 95f) * 0.34f : 0f;
                data[i] = Mathf.Clamp((low + snap + noise * 0.42f) * envelope * 0.74f, -1f, 1f);
            }

            return CreateClip(name, data);
        }

        private static AudioClip GetMechanical(string key, float duration, int seed, float tone, float level)
        {
            if (Clips.TryGetValue(key, out var cached))
                return cached;

            var clip = BuildNoiseBurst(key, duration, seed, level, 32f, tone);
            Clips[key] = clip;
            return clip;
        }

        private static AudioClip BuildNoiseBurst(string name, float duration, int seed, float noiseAmount, float decay, float tone)
        {
            var samples = Mathf.CeilToInt(duration * SampleRate);
            var data = new float[samples];
            var random = new System.Random(seed);

            for (var i = 0; i < samples; i++)
            {
                var t = i / (float)SampleRate;
                var envelope = Mathf.Exp(-t * decay);
                var noise = (float)(random.NextDouble() * 2.0 - 1.0) * noiseAmount;
                var ring = Mathf.Sin(t * Mathf.PI * 2f * tone) * 0.35f;
                data[i] = Mathf.Clamp((noise + ring) * envelope, -1f, 1f);
            }

            return CreateClip(name, data);
        }

        private static void GetSurfaceTone(SurfaceMaterial material, out float tone, out float noiseAmount, out float brightness)
        {
            switch (material)
            {
                case SurfaceMaterial.Metal:
                    tone = 520f;
                    noiseAmount = 0.20f;
                    brightness = 2.15f;
                    break;
                case SurfaceMaterial.Wood:
                    tone = 165f;
                    noiseAmount = 0.38f;
                    brightness = 1.65f;
                    break;
                case SurfaceMaterial.Cardboard:
                    tone = 105f;
                    noiseAmount = 0.48f;
                    brightness = 1.35f;
                    break;
                case SurfaceMaterial.Plastic:
                    tone = 245f;
                    noiseAmount = 0.27f;
                    brightness = 1.85f;
                    break;
                case SurfaceMaterial.Glass:
                    tone = 830f;
                    noiseAmount = 0.13f;
                    brightness = 2.8f;
                    break;
                case SurfaceMaterial.Grate:
                    tone = 690f;
                    noiseAmount = 0.24f;
                    brightness = 2.35f;
                    break;
                default:
                    tone = 135f;
                    noiseAmount = 0.56f;
                    brightness = 1.42f;
                    break;
            }
        }

        private static AudioClip CreateClip(string name, float[] data)
        {
            var clip = AudioClip.Create(name, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
