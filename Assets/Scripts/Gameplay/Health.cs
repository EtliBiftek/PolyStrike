using System;
using PolyStrike.Player;
using UnityEngine;

namespace PolyStrike.Gameplay
{
    public sealed class Health : MonoBehaviour
    {
        private const float ArmorBonus = 0.5f;
        private const float HeArmorRatio = 0.60f;

        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private float startingArmor;
        [SerializeField] private bool hasHelmet;
        [SerializeField] private bool disableOnDeath = true;

        private PlayerMovement movement;
        private PlayerLook playerLook;

        public float Current { get; private set; }
        public float Max => maxHealth;
        public float Armor { get; private set; }
        public bool HasHelmet => hasHelmet;
        public bool IsDead { get; private set; }
        public int DamageRevision { get; private set; }
        public Vector3 LastBulletDirection { get; private set; }
        public HitGroup LastHitGroup { get; private set; } = HitGroup.Chest;

        public event Action<float, float> Changed;
        public event Action<float> ArmorChanged;
        public event Action Died;
        public event Action RoundReset;

        private void Awake()
        {
            Current = maxHealth;
            Armor = Mathf.Clamp(startingArmor, 0f, 100f);
            movement = GetComponent<PlayerMovement>();
            playerLook = GetComponent<PlayerLook>();
        }

        public BulletDamageResult TakeBulletDamage(BulletDamage bullet)
        {
            if (IsDead || bullet.Damage <= 0f)
                return new BulletDamageResult(0, 0, IsDead);

            LastBulletDirection = bullet.Direction;
            LastHitGroup = bullet.HitGroup;

            var scaledDamage = bullet.Damage * HitGroupRules.GetDamageMultiplier(bullet.HitGroup);
            var armorProtected = Armor > 0f && HitGroupRules.IsProtectedByArmor(bullet.HitGroup, hasHelmet);
            var result = ApplyArmoredDamage(scaledDamage, bullet.ArmorPenetration, armorProtected);

            if (result.HealthDamage > 0)
            {
                ResolvePlayerReferences();
                movement?.ApplyTag(bullet.TaggingBaseVsM4);
                playerLook?.ApplyExternalAimPunch(result.HealthDamage, bullet.HitGroup, armorProtected, bullet.Direction);
            }

            if (Current <= 0f)
                Die();

            return new BulletDamageResult(result.HealthDamage, result.ArmorDamage, IsDead);
        }

        public int TakeGrenadeDamage(float rawDamage, Vector3 blastDirection)
        {
            if (IsDead || rawDamage <= 0f)
                return 0;

            LastBulletDirection = blastDirection;
            LastHitGroup = HitGroup.Chest;

            var armorProtected = Armor > 0f;
            var result = ApplyArmoredDamage(rawDamage, HeArmorRatio, armorProtected);

            if (result.HealthDamage > 0)
            {
                ResolvePlayerReferences();
                playerLook?.ApplyExternalAimPunch(result.HealthDamage, HitGroup.Chest, armorProtected, blastDirection);
            }

            if (Current <= 0f)
                Die();

            return result.HealthDamage;
        }

        public void TakeDamage(float amount)
        {
            if (IsDead || amount <= 0f)
                return;

            var dealt = Mathf.Max(0, Mathf.FloorToInt(amount));
            if (dealt <= 0)
                return;

            Current = Mathf.Max(0f, Current - dealt);
            DamageRevision++;
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

        public void ResetForRound(bool clearEquipment)
        {
            IsDead = false;
            Current = maxHealth;
            DamageRevision = 0;
            LastBulletDirection = Vector3.zero;
            LastHitGroup = HitGroup.Chest;

            if (clearEquipment)
            {
                Armor = 0f;
                hasHelmet = false;
            }

            Changed?.Invoke(Current, maxHealth);
            ArmorChanged?.Invoke(Armor);
            RoundReset?.Invoke();

            if (!gameObject.activeSelf)
                gameObject.SetActive(true);
        }

        public void SetEquipment(float armor, bool helmet)
        {
            Armor = Mathf.Clamp(armor, 0f, 100f);
            hasHelmet = Armor > 0f && helmet;
            ArmorChanged?.Invoke(Armor);
        }

        public void SetDisableOnDeath(bool value)
        {
            disableOnDeath = value;
        }

        private (int HealthDamage, int ArmorDamage) ApplyArmoredDamage(float rawDamage, float armorRatio, bool armorProtected)
        {
            var healthDamage = rawDamage;
            var armorDamage = 0f;

            if (armorProtected)
            {
                healthDamage = rawDamage * Mathf.Clamp01(armorRatio);
                armorDamage = (rawDamage - healthDamage) * ArmorBonus;

                if (armorDamage > Armor)
                {
                    armorDamage = Armor;
                    healthDamage = rawDamage - armorDamage / ArmorBonus;
                }
            }

            var dealt = Mathf.Max(0, Mathf.FloorToInt(healthDamage));
            var armorSpent = Mathf.Max(0, Mathf.FloorToInt(armorDamage));

            Current = Mathf.Max(0f, Current - dealt);
            Armor = Mathf.Max(0f, Armor - armorSpent);
            if (Armor <= 0f)
                hasHelmet = false;

            if (dealt > 0 || armorSpent > 0)
                DamageRevision++;

            Changed?.Invoke(Current, maxHealth);
            if (armorSpent > 0)
                ArmorChanged?.Invoke(Armor);

            return (dealt, armorSpent);
        }

        private void ResolvePlayerReferences()
        {
            if (movement == null)
                movement = GetComponent<PlayerMovement>();
            if (playerLook == null)
                playerLook = GetComponent<PlayerLook>();
        }

        private void Die()
        {
            if (IsDead)
                return;

            IsDead = true;
            Died?.Invoke();

            if (disableOnDeath)
                gameObject.SetActive(false);
        }
    }
}
