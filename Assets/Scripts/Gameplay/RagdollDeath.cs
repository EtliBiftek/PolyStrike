using UnityEngine;

namespace PolyStrike.Gameplay
{
    [RequireComponent(typeof(Health))]
    public sealed class RagdollDeath : MonoBehaviour
    {
        [SerializeField] private float impulse = 2.8f;
        [SerializeField] private float upwardImpulse = 0.55f;
        [SerializeField] private float cleanupDelay = 8f;

        private Health health;
        private Rigidbody[] bodies;

        private void Awake()
        {
            health = GetComponent<Health>();
            bodies = GetComponentsInChildren<Rigidbody>(true);

            for (var i = 0; i < bodies.Length; i++)
            {
                bodies[i].isKinematic = true;
                bodies[i].useGravity = false;
                bodies[i].interpolation = RigidbodyInterpolation.Interpolate;
            }

            health.Died += OnDeath;
        }

        private void OnDestroy()
        {
            if (health != null)
                health.Died -= OnDeath;
        }

        private void OnDeath()
        {
            var direction = health.LastBulletDirection.sqrMagnitude > 0.0001f
                ? health.LastBulletDirection.normalized
                : transform.forward;

            Rigidbody struckBody = null;

            for (var i = 0; i < bodies.Length; i++)
            {
                var body = bodies[i];
                body.isKinematic = false;
                body.useGravity = true;

                var hitbox = body.GetComponent<PlayerHitbox>();
                if (hitbox != null && hitbox.HitGroup == health.LastHitGroup)
                    struckBody = body;
            }

            if (struckBody == null && bodies.Length > 0)
                struckBody = bodies[0];

            if (struckBody != null)
            {
                var force = direction * impulse + Vector3.up * upwardImpulse;
                struckBody.AddForce(force, ForceMode.Impulse);
            }

            Destroy(gameObject, cleanupDelay);
        }
    }
}
