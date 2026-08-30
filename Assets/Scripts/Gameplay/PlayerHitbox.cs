using UnityEngine;

namespace PolyStrike.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class PlayerHitbox : MonoBehaviour
    {
        [SerializeField] private HitGroup hitGroup = HitGroup.Chest;

        private Health health;

        public HitGroup HitGroup => hitGroup;
        public Health Health => health != null ? health : GetComponentInParent<Health>();

        public void Configure(Health owner, HitGroup group)
        {
            health = owner;
            hitGroup = group;
        }
    }
}
