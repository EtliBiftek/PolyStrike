using PolyStrike.Gameplay;
using UnityEngine;

namespace PolyStrike.Match
{
    public sealed class BombDeathDropGuard : MonoBehaviour
    {
        private MatchParticipant participant;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateInstaller()
        {
            var root = new GameObject("Bomb Drop Installer");
            Object.DontDestroyOnLoad(root);
            root.AddComponent<BombDropInstaller>();
        }

        private void Awake()
        {
            participant = GetComponent<MatchParticipant>();
            if (participant != null)
                participant.Died += OnParticipantDied;
        }

        private void OnDestroy()
        {
            if (participant != null)
                participant.Died -= OnParticipantDied;
        }

        private void OnParticipantDied(MatchParticipant dead)
        {
            if (dead != participant || !participant.CarriesBomb)
                return;

            participant.GiveBomb(false);
            var origin = transform.position + Vector3.up * 0.55f;
            DroppedMatchItem.SpawnBomb(origin, Vector3.up * SourceUnit.ToMeters(55f));
        }
    }

    public sealed class BombDropInstaller : MonoBehaviour
    {
        private void Start()
        {
            var participants = Object.FindObjectsByType<MatchParticipant>(FindObjectsSortMode.None);
            for (var i = 0; i < participants.Length; i++)
            {
                var participant = participants[i];
                if (participant != null && participant.GetComponent<BombDeathDropGuard>() == null)
                    participant.gameObject.AddComponent<BombDeathDropGuard>();
            }

            Destroy(gameObject);
        }
    }
}
