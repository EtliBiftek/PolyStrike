using UnityEngine;

namespace PolyStrike.Player
{
    public sealed class FlashEffect : MonoBehaviour
    {
        private static AudioClip ringingClip;

        private Camera playerCamera;
        private AudioSource ringingSource;
        private float flashStart;
        private float fullWhiteUntil;
        private float flashEnd;
        private float maxAlpha;

        public Vector3 EyePosition => playerCamera != null ? playerCamera.transform.position : transform.position;
        public Vector3 ViewForward => playerCamera != null ? playerCamera.transform.forward : transform.forward;

        public void SetCamera(Camera cameraToUse)
        {
            playerCamera = cameraToUse;
        }

        private void Awake()
        {
            ringingSource = gameObject.AddComponent<AudioSource>();
            ringingSource.playOnAwake = false;
            ringingSource.loop = true;
            ringingSource.spatialBlend = 0f;
            ringingSource.dopplerLevel = 0f;
        }

        public void Apply(float duration, float intensity)
        {
            if (duration <= 0f || intensity <= 0f)
                return;

            var now = Time.time;
            var clampedIntensity = Mathf.Clamp01(intensity);
            var newEnd = now + duration;

            if (newEnd > flashEnd)
            {
                flashStart = now;
                flashEnd = newEnd;
                fullWhiteUntil = now + duration * Mathf.Lerp(0.18f, 0.48f, clampedIntensity);
            }

            maxAlpha = Mathf.Max(maxAlpha, clampedIntensity);

            ringingSource.clip = GetRingingClip();
            ringingSource.volume = Mathf.Lerp(0.10f, 0.42f, clampedIntensity);
            if (!ringingSource.isPlaying)
                ringingSource.Play();
        }

        private void Update()
        {
            if (Time.time >= flashEnd)
            {
                maxAlpha = 0f;
                if (ringingSource.isPlaying)
                    ringingSource.Stop();
                return;
            }

            var remaining = Mathf.InverseLerp(flashEnd, fullWhiteUntil, Time.time);
            ringingSource.volume = Mathf.Min(ringingSource.volume, Mathf.Lerp(0f, 0.42f, remaining));
        }

        private void OnGUI()
        {
            if (Time.time >= flashEnd || maxAlpha <= 0f)
                return;

            var alpha = maxAlpha;
            if (Time.time > fullWhiteUntil)
                alpha *= Mathf.InverseLerp(flashEnd, fullWhiteUntil, Time.time);

            var previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private static AudioClip GetRingingClip()
        {
            if (ringingClip != null)
                return ringingClip;

            const int sampleRate = 44100;
            const float duration = 1.2f;
            var sampleCount = Mathf.CeilToInt(sampleRate * duration);
            var data = new float[sampleCount];

            for (var i = 0; i < sampleCount; i++)
            {
                var t = i / (float)sampleRate;
                var envelope = Mathf.Exp(-t * 1.6f);
                var tone = Mathf.Sin(t * Mathf.PI * 2f * 3450f) * 0.62f;
                var upper = Mathf.Sin(t * Mathf.PI * 2f * 5180f) * 0.16f;
                data[i] = (tone + upper) * envelope;
            }

            ringingClip = AudioClip.Create("Flash Ring", sampleCount, 1, sampleRate, false);
            ringingClip.SetData(data, 0);
            return ringingClip;
        }
    }
}
