using PolyStrike.Gameplay;
using UnityEngine;

namespace PolyStrike.Match
{
    public enum DroppedItemKind
    {
        Bomb,
        DefuseKit,
        PrimaryRifle,
        Grenade
    }

    public sealed class DroppedMatchItem : MonoBehaviour
    {
        private const float PickupDelay = 0.32f;

        private DroppedItemKind kind;
        private GrenadeType grenadeType;
        private int weaponProfileId = -1;
        private int magazineAmmo;
        private int reserveAmmo;
        private int spawnRoundNumber = -1;
        private RoundPhase spawnPhase = RoundPhase.Live;
        private float pickupAvailableAt;

        private void Update()
        {
            var match = MatchRoundManager.Instance;
            if (match == null || match.Phase != RoundPhase.FreezeTime)
                return;

            var belongsToCurrentFreeze = spawnPhase == RoundPhase.FreezeTime && spawnRoundNumber == match.RoundNumber;
            if (!belongsToCurrentFreeze)
                Destroy(gameObject);
        }

        public static DroppedMatchItem SpawnBomb(Vector3 position, Vector3 velocity)
        {
            return Spawn(DroppedItemKind.Bomb, position, velocity, new Vector3(0.28f, 0.12f, 0.20f));
        }

        public static DroppedMatchItem SpawnDefuseKit(Vector3 position, Vector3 velocity)
        {
            return Spawn(DroppedItemKind.DefuseKit, position, velocity, new Vector3(0.18f, 0.08f, 0.14f));
        }

        public static DroppedMatchItem SpawnPrimaryRifle(Vector3 position, Vector3 velocity, int profileId, int magazine, int reserve)
        {
            var item = Spawn(DroppedItemKind.PrimaryRifle, position, velocity, new Vector3(0.10f, 0.08f, 0.70f));
            item.weaponProfileId = profileId;
            item.magazineAmmo = Mathf.Max(0, magazine);
            item.reserveAmmo = Mathf.Max(0, reserve);
            return item;
        }

        public static DroppedMatchItem SpawnGrenade(Vector3 position, Vector3 velocity, GrenadeType type)
        {
            var item = Spawn(DroppedItemKind.Grenade, position, velocity, new Vector3(0.10f, 0.13f, 0.10f));
            item.grenadeType = type;
            return item;
        }

        public bool TryPickup(MatchParticipant participant)
        {
            if (participant == null || !participant.IsAlive || Time.time < pickupAvailableAt)
                return false;

            var picked = false;
            switch (kind)
            {
                case DroppedItemKind.Bomb:
                    picked = TryPickupBomb(participant);
                    break;
                case DroppedItemKind.DefuseKit:
                    picked = TryPickupDefuseKit(participant);
                    break;
                case DroppedItemKind.PrimaryRifle:
                    picked = participant.TryPickupPrimary(weaponProfileId, magazineAmmo, reserveAmmo);
                    break;
                case DroppedItemKind.Grenade:
                    picked = participant.TryPickupGrenade(grenadeType);
                    break;
            }

            if (picked)
                Destroy(gameObject);

            return picked;
        }

        private bool TryPickupBomb(MatchParticipant participant)
        {
            if (participant.Team != MatchTeam.Terrorists || participant.CarriesBomb)
                return false;

            var match = MatchRoundManager.Instance;
            if (match == null || (match.Phase != RoundPhase.FreezeTime && match.Phase != RoundPhase.Live))
                return false;

            participant.GiveBomb(true);
            return true;
        }

        private static bool TryPickupDefuseKit(MatchParticipant participant)
        {
            if (participant.Team != MatchTeam.CounterTerrorists || participant.HasDefuseKit)
                return false;

            participant.SetDefuseKit(true);
            return true;
        }

        private static DroppedMatchItem Spawn(DroppedItemKind itemKind, Vector3 position, Vector3 velocity, Vector3 scale)
        {
            var root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = "Dropped Match Item";
            root.transform.position = position;
            root.transform.localScale = scale;

            var item = root.AddComponent<DroppedMatchItem>();
            item.kind = itemKind;
            item.pickupAvailableAt = Time.time + PickupDelay;

            var match = MatchRoundManager.Instance;
            if (match != null)
            {
                item.spawnRoundNumber = match.RoundNumber;
                item.spawnPhase = match.Phase;
            }

            var body = root.AddComponent<Rigidbody>();
            body.mass = itemKind == DroppedItemKind.PrimaryRifle ? 2.2f : 0.7f;
            body.linearVelocity = velocity;
            body.angularVelocity = new Vector3(2.5f, 5f, 3f);
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.interpolation = RigidbodyInterpolation.Interpolate;

            var pickup = new GameObject("Pickup Trigger");
            pickup.transform.SetParent(root.transform, false);
            pickup.transform.localScale = new Vector3(
                1f / Mathf.Max(scale.x, 0.01f),
                1f / Mathf.Max(scale.y, 0.01f),
                1f / Mathf.Max(scale.z, 0.01f));

            var trigger = pickup.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.72f;
            pickup.AddComponent<DroppedMatchItemTrigger>().SetItem(item);
            return item;
        }
    }

    public sealed class DroppedMatchItemTrigger : MonoBehaviour
    {
        private DroppedMatchItem item;

        public void SetItem(DroppedMatchItem droppedItem)
        {
            item = droppedItem;
        }

        private void OnTriggerEnter(Collider other)
        {
            var participant = other.GetComponentInParent<MatchParticipant>();
            if (participant != null)
                item?.TryPickup(participant);
        }
    }
}
