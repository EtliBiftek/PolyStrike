using System.Collections.Generic;
using PolyStrike.Gameplay;
using UnityEngine;

namespace PolyStrike.Audio
{
    public static class UtilitySfxBank
    {
        private const int SampleRate = 44100;
        private static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();

        public static AudioClip Draw(GrenadeType type)
        {
            return GetMechanical($"utility_draw_{type}", type, 0, 0.11f, 330f, 0.55f);
        }

        public static AudioClip PinPull(GrenadeType type)
        {
            return GetMechanical($"utility_pin_{type}", type, 1, 0.13f, 920f, 0.72f);
        }

        public static AudioClip Throw(GrenadeType type)
        {
            return GetMechanical($"utility_throw_{type}", type, 2, 0.10f, 245f, 0.60f);
        }

        private static AudioClip GetMechanical(string key, GrenadeType type, int action, float duration, float tone, float strength)
        {
            if (Clips.TryGetValue(key, out var cached))
                return cached;

            var samples = Mathf.CeilToInt(duration * SampleRate);
            var data = new float[samples];
            var random = new System.Random(12000 + (int)type * 101 + action * 17);
            var typePitch = 1f + (int)type * 0.045f;

            for (var i = 0; i < samples; i++)
            {
                var t = i / (float)SampleRate;
                var envelope = Mathf.Exp(-t * (action == 1 ? 27f : 34f));
                var noise = (float)(random.NextDouble() * 2.0 - 1.0) * 0.32f;
                var metal = Mathf.Sin(t * Mathf.PI * 2f * tone * typePitch) * 0.42f;
                var click = Mathf.Sin(t * Mathf.PI * 2f * tone * 3.4f) * Mathf.Exp(-t * 90f) * 0.22f;
                data[i] = Mathf.Clamp((metal + click + noise) * envelope * strength, -1f, 1f);
            }

            var clip = AudioClip.Create(key, samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            Clips[key] = clip;
            return clip;
        }
    }
}
