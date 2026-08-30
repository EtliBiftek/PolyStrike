using UnityEngine;

namespace PolyStrike.Gameplay
{
    public sealed class HitReaction : MonoBehaviour
    {
        private Vector3 rotationOffset;
        private Vector3 positionOffset;
        private Quaternion baseRotation;
        private Vector3 basePosition;

        private void Awake()
        {
            baseRotation = transform.localRotation;
            basePosition = transform.localPosition;
        }

        public void React(Vector3 bulletDirection, int healthDamage)
        {
            if (healthDamage <= 0)
                return;

            var localDirection = transform.InverseTransformDirection(bulletDirection.normalized);
            var strength = Mathf.Clamp(healthDamage * 0.055f, 0.35f, 4.5f);

            rotationOffset += new Vector3(
                -localDirection.y * strength,
                localDirection.x * strength,
                -localDirection.x * strength * 0.55f);

            positionOffset += -localDirection * Mathf.Min(healthDamage * 0.00022f, 0.008f);
        }

        private void LateUpdate()
        {
            rotationOffset = Vector3.Lerp(rotationOffset, Vector3.zero, 1f - Mathf.Exp(-18f * Time.deltaTime));
            positionOffset = Vector3.Lerp(positionOffset, Vector3.zero, 1f - Mathf.Exp(-22f * Time.deltaTime));

            transform.localRotation = baseRotation * Quaternion.Euler(rotationOffset);
            transform.localPosition = basePosition + positionOffset;
        }
    }
}
