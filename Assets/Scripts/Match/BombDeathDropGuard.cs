using PolyStrike.Gameplay;
using UnityEngine;

namespace PolyStrike.Match
{
    [RequireComponent(typeof(MatchParticipant))]
    public sealed class BombDeathDropGuard : MonoBehaviour
    {
        private MatchParticipant participant;

        private void Awake()
        {
            participant = GetComponent<MatchParticipant>();
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
}
