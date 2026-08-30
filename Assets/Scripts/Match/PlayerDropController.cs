using PolyStrike.Core;
using PolyStrike.Gameplay;
using PolyStrike.Player;
using UnityEngine;

namespace PolyStrike.Match
{
    [RequireComponent(typeof(MatchParticipant))]
    public sealed class PlayerDropController : MonoBehaviour
    {
        private MatchParticipant participant;
        private PlayerMovement movement;
        private HitscanWeapon weapon;
        private UtilityController utility;
        private C4Controller c4;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateInstaller()
        {
            var root = new GameObject("Player Drop Installer");
            Object.DontDestroyOnLoad(root);
            root.AddComponent<PlayerDropInstaller>();
        }

        private void Awake()
        {
            participant = GetComponent<MatchParticipant>();
            movement = GetComponent<PlayerMovement>();
            weapon = GetComponentInChildren<HitscanWeapon>();
            utility = GetComponent<UtilityController>();
            c4 = GetComponent<C4Controller>();
        }

        private void Update()
        {
            if (!participant.IsLocalPlayer || !participant.IsAlive || !GameInput.DropPressed)
                return;

            var match = MatchRoundManager.Instance;
            if (match == null || (match.Phase != RoundPhase.FreezeTime && match.Phase != RoundPhase.Live))
                return;

            c4 ??= GetComponent<C4Controller>();
            if (c4 != null && c4.IsBombEquipped && participant.CarriesBomb)
            {
                c4.DropCarriedBomb(true);
                return;
            }

            var origin = transform.position + Vector3.up * 1.0f + transform.forward * 0.4f;
            var inherited = movement != null ? movement.WorldVelocity : Vector3.zero;
            var toss = inherited + SourceUnit.ToMeters(transform.forward * 210f + Vector3.up * 75f);

            if (utility != null && utility.TryDropSelected(out var grenadeType))
            {
                DroppedMatchItem.SpawnGrenade(origin, toss, grenadeType);
                return;
            }

            if (weapon != null && weapon.IsPrimaryActive && weapon.TryDropPrimary(out var profileId, out var magazine, out var reserve))
                DroppedMatchItem.SpawnPrimaryRifle(origin, toss, profileId, magazine, reserve);
        }
    }

    public sealed class PlayerDropInstaller : MonoBehaviour
    {
        private void Start()
        {
            var participants = Object.FindObjectsByType<MatchParticipant>(FindObjectsSortMode.None);
            for (var i = 0; i < participants.Length; i++)
            {
                var participant = participants[i];
                if (participant == null || !participant.IsLocalPlayer)
                    continue;

                if (participant.GetComponent<PlayerDropController>() == null)
                    participant.gameObject.AddComponent<PlayerDropController>();
            }

            Destroy(gameObject);
        }
    }
}
