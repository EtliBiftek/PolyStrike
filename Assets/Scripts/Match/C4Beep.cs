using UnityEngine;

namespace PolyStrike.Match
{
    public sealed class C4Beep : MonoBehaviour
    {
        private AudioSource source;
        private AudioClip beep;
        private float nextBeep;

        private void Awake()
        {
            source = gameObject.AddComponent<AudioSource>();
            source.spatialBlend = 1f;
            source.minDistance = 1.5f;
            source.maxDistance = 28f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.dopplerLevel = 0f;
            beep = BuildBeep();
        }

        private void Update()
        {
            var match = MatchRoundManager.Instance;
            if (match == null || match.Phase != RoundPhase.PostPlant || Time.time < nextBeep)
                return;

            var remaining = match.TimeRemaining;
            var interval = Mathf.Lerp(0.16f, 0.95f, Mathf.Clamp01(remaining / MatchRules.BombTimer));
            source.PlayOneShot(beep, 0.72f);
            nextBeep = Time.time + interval;
        }

        private static AudioClip BuildBeep()
        {
            const int sampleRate = 44100;
            const float duration = 0.065f;
            var count = Mathf.CeilToInt(sampleRate * duration);
            var data = new float[count];

            for (var i = 0; i < count; i++)
            {
                var t = i / (float)sampleRate;
                var envelope = Mathf.Exp(-t * 42f);
                data[i] = Mathf.Sin(t * Mathf.PI * 2f * 1420f) * envelope * 0.34f;
            }

            var clip = AudioClip.Create("C4 Beep", count, 1, sampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
