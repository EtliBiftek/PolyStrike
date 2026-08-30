using PolyStrike.Audio;
using PolyStrike.Gameplay;
using UnityEngine;

namespace PolyStrike.Player
{
    [RequireComponent(typeof(UtilityController))]
    public sealed class UtilityHandlingAudio : MonoBehaviour
    {
        private UtilityController utility;
        private AudioSource source;
        private bool previousEquipped;
        private bool previousPrimed;
        private GrenadeType previousType;

        private void Awake()
        {
            utility = GetComponent<UtilityController>();
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.dopplerLevel = 0f;
        }

        private void Update()
        {
            if (utility == null)
                return;

            var equipped = utility.IsEquipped;
            var type = utility.SelectedType;
            var primed = utility.IsPrimed;

            if (equipped && (!previousEquipped || type != previousType))
                source.PlayOneShot(UtilitySfxBank.Draw(type), 0.62f);

            if (equipped && primed && !previousPrimed)
                source.PlayOneShot(UtilitySfxBank.PinPull(type), 0.78f);

            if (equipped && !primed && previousPrimed)
                source.PlayOneShot(UtilitySfxBank.Throw(type), 0.72f);

            previousEquipped = equipped;
            previousPrimed = primed;
            previousType = type;
        }
    }
}
