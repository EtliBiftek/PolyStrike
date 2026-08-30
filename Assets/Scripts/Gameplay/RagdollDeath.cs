using UnityEngine;

namespace PolyStrike.Gameplay
{
    [RequireComponent(typeof(Health))]
    public sealed class RagdollDeath : MonoBehaviour
    {
        [SerializeField] private float impulse = 2.8f;
        [SerializeField] private float upwardImpulse = 0.55f;

        private Health health;
        private Rigidbody[] bodies;
        private Vector3[] initialPositions;
        private Quaternion[] initialRotations;

        private void Awake()
        {
            health = GetComponent<Health>();
            bodies = GetComponentsInChildren<Rigidbody>(true);
            initialPositions = new Vector3[bodies.Length];
            initialRotations = new Quaternion[bodies.Length];

            for (var i = 0; i < bodies.Length; i++)
            {
                var body = bodies[i];
                initialPositions[i] = body.transform.localPosition;
                initialRotations[i] = body.transform.localRotation;
                body.isKinematic = true;
                body.useGravity = false;
                body.interpolation = RigidbodyInterpolation.Interpolate;
            }

            health.Died += OnDeath;
            health.RoundReset += OnRoundReset;
        }

        private void OnDestroy()
        {
            if (health == null)
                return;

            health.Died -= OnDeath;
            health.RoundReset -= OnRoundReset;
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
                struckBody.AddForce(direction * impulse + Vector3.up * upwardImpulse, ForceMode.Impulse);
        }

        private void OnRoundReset()
        {
            for (var i = 0; i < bodies.Length; i++)
            {
                var body = bodies[i];
                body.isKinematic = true;
                body.useGravity = false;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.transform.localPosition = initialPositions[i];
                body.transform.localRotation = initialRotations[i];
            }
        }
    }
}
