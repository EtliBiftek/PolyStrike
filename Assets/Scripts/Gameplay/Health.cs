using System;
using UnityEngine;

namespace PolyStrike.Gameplay
{
    public sealed class Health : MonoBehaviour
    {
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private bool disableOnDeath = true;

        public float Current { get; private set; }
        public float Max => maxHealth;
        public bool IsDead { get; private set; }

        public event Action<float, float> Changed;
        public event Action Died;

        private void Awake()
        {
            Current = maxHealth;
        }

        public void TakeDamage(float amount)
        {
            if (IsDead || amount <= 0f)
                return;

            Current = Mathf.Max(0f, Current - amount);
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

        private void Die()
        {
            IsDead = true;
            Died?.Invoke();

            if (disableOnDeath)
                gameObject.SetActive(false);
        }
    }
}
