using UnityEngine;

namespace PolyStrike.Audio
{
    [RequireComponent(typeof(AudioSource), typeof(AudioLowPassFilter))]
    public sealed class SpatialSoundEmitter : MonoBehaviour
    {
        private AudioSource source;
        private AudioLowPassFilter lowPass;
        private float clearVolume;
        private Transform listener;

        private void Awake()
        {
            source = GetComponent<AudioSource>();
            lowPass = GetComponent<AudioLowPassFilter>();
            lowPass.cutoffFrequency = 22000f;
        }

        public void Play(AudioClip clip, float volume, float minDistance, float maxDistance)
        {
            clearVolume = volume;
            source.clip = clip;
            source.volume = volume;
            source.spatialBlend = 1f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = minDistance;
            source.maxDistance = maxDistance;
            source.dopplerLevel = 0f;
            source.spread = 0f;
            source.spatialize = true;
            source.Play();

            var mainCamera = Camera.main;
            listener = mainCamera != null ? mainCamera.transform : null;
            Destroy(gameObject, clip.length + 0.25f);
        }

        private void Update()
        {
            if (listener == null)
            {
                var mainCamera = Camera.main;
                listener = mainCamera != null ? mainCamera.transform : null;
                if (listener == null)
                    return;
            }

            var toListener = listener.position - transform.position;
            if (toListener.sqrMagnitude < 0.001f)
                return;

            var start = transform.position + toListener.normalized * 0.04f;
            var blocked = Physics.Linecast(start, listener.position, ~0, QueryTriggerInteraction.Ignore);
            var targetCutoff = blocked ? 3100f : 22000f;
            var targetVolume = clearVolume * (blocked ? 0.62f : 1f);
            var blend = 1f - Mathf.Exp(-18f * Time.deltaTime);

            lowPass.cutoffFrequency = Mathf.Lerp(lowPass.cutoffFrequency, targetCutoff, blend);
            source.volume = Mathf.Lerp(source.volume, targetVolume, blend);
        }
    }
}
