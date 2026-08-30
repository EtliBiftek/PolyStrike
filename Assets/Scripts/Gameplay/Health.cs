using System;
using PolyStrike.Player;
using UnityEngine;

namespace PolyStrike.Gameplay
{
    public sealed class Health : MonoBehaviour
    {
        private const float ArmorBonus = 0.5f;

        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float startingArmor = 100f;
        [SerializeField] private bool hasHelmet = true;
        [SerializeField] private bool disableOnDeath = true;

        private PlayerMovement movement;

        public float Current { get; private set; }
        public float Max => maxHealth;
        public float Armor { get; private set; }
        public bool HasHelmet => hasHelmet;
        public bool IsDead { get; private set; }

        public event Action<float, float> Changed;
        public event Action<float> ArmorChanged;
        public event Action Died;

        private void Awake()
        {
            Current = maxHealth;
            Armor = Mathf.Clamp(startingArmor, 0f, 100f);
            movement = GetComponent<PlayerMovement>();
        }

        public BulletDamageResult TakeBulletDamage(BulletDamage bullet)
        {
            if (IsDead || bullet.Damage <= 0f)
                return new BulletDamageResult(0, 0, IsDead);

            var scaledDamage = bullet.Damage * HitGroupRules.GetDamageMultiplier(bullet.HitGroup);
            var healthDamage = scaledDamage;
            var armorDamage = 0f;

            if (Armor > 0f && HitGroupRules.IsProtectedByArmor(bullet.HitGroup, hasHelmet))
            {
                var armorPenetration = Mathf.Clamp01(bullet.ArmorPenetration);
                healthDamage = scaledDamage * armorPenetration;
                armorDamage = (scaledDamage - healthDamage) * ArmorBonus;

                if (armorDamage > Armor)
                {
                    armorDamage = Armor;
                    healthDamage = scaledDamage - armorDamage / ArmorBonus;
                }
            }

            var dealt = Mathf.Max(0, Mathf.FloorToInt(healthDamage));
            var armorSpent = Mathf.Max(0, Mathf.FloorToInt(armorDamage));

            Current = Mathf.Max(0f, Current - dealt);
            Armor = Mathf.Max(0f, Armor - armorSpent);

            Changed?.Invoke(Current, maxHealth);
            if (armorSpent > 0)
                ArmorChanged?.Invoke(Armor);

            if (dealt > 0 && movement != null)
                movement.ApplyTag(bullet.TaggingBaseVsM4);

            if (Current <= 0f)
                Die();

            return new BulletDamageResult(dealt, armorSpent, IsDead);
        }

        public void TakeDamage(float amount)
        {
            if (IsDead || amount <= 0f)
                return;

            Current = Mathf.Max(0f, Current - Mathf.Floor(amount));
            Changed?.Invoke(Current, maxHealth);

            if (Current <= 0f)
                Die();
        }

        public void Restore(float amount)
        {
            if (IsDead || amount <= 0f)
                return;

            Current = Mathf.Min(maxHealth, Current + amount);
            Changed?.Invoke(Current, maxHealth);
        }

        public void SetEquipment(float armor, bool helmet)
        {
            Armor = Mathf.Clamp(armor, 0f, 100f);
            hasHelmet = helmet;
            ArmorChanged?.Invoke(Armor);
        }

        private void Die()
        {
            IsDead = true;
            Died?.Invoke();

            if (disableOnDeath)
                gameObject.SetActive(false);
        }
    }
}
