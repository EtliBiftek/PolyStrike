using PolyStrike.Core;
using PolyStrike.Gameplay;
using UnityEngine;

namespace PolyStrike.Player
{
    public sealed class ViewmodelMotion : MonoBehaviour
    {
        private const float BobCycle = 0.98f;
        private const float LateralBob = 0.012f;
        private const float VerticalBob = 0.008f;
        private const float RunLowering = 0.035f;

        [SerializeField] private Vector3 basePosition = new Vector3(0.18f, -0.16f, 0.42f);
        [SerializeField] private Vector3 baseRotation = new Vector3(2f, -4f, 0f);
        [SerializeField] private float swayAmount = 0.00065f;
        [SerializeField] private float swayRotation = 0.035f;
        [SerializeField] private float returnSpeed = 16f;

        private PlayerMovement movement;
        private Vector3 recoilPosition;
        private Vector3 recoilRotation;
        private float deployUntil;
        private float deployDuration = 1f;
        private float bobTime;

        private void Awake()
        {
            movement = GetComponentInParent<PlayerMovement>();
        }

        public void PlayShot(Vector2 recoilStep)
        {
            recoilPosition += new Vector3(-recoilStep.x * 0.0012f, -0.008f, -0.028f);
            recoilRotation += new Vector3(-1.6f - recoilStep.y * 0.10f, recoilStep.x * 0.24f, recoilStep.x * 0.12f);
        }

        public void PlayDeploy(float duration)
        {
            deployDuration = Mathf.Max(duration, 0.01f);
            deployUntil = Time.time + deployDuration;
        }

        public void PlayReloadKick()
        {
            recoilPosition += new Vector3(0.012f, -0.015f, -0.018f);
            recoilRotation += new Vector3(2.5f, -2f, 4f);
        }

        private void LateUpdate()
        {
            var speedFraction = movement == null
                ? 0f
                : Mathf.Clamp01(movement.SpeedSourceUnits / Mathf.Max(movement.MaxSpeedSourceUnits, 1f));

            if (movement != null && movement.IsGrounded && speedFraction > 0.05f)
                bobTime += Time.deltaTime * (8f / BobCycle) * Mathf.Lerp(0.65f, 1f, speedFraction);

            var bobWeight = movement != null && movement.IsGrounded ? speedFraction : 0f;
            var bob = new Vector3(
                Mathf.Sin(bobTime) * LateralBob * bobWeight,
                Mathf.Abs(Mathf.Cos(bobTime * 2f)) * VerticalBob * bobWeight,
                -RunLowering * Mathf.SmoothStep(0f, 1f, speedFraction));

            var mouse = GameInput.MouseDelta;
            var sway = new Vector3(-mouse.x * swayAmount, -mouse.y * swayAmount, 0f);
            var swayAngles = new Vector3(mouse.y * swayRotation, -mouse.x * swayRotation, mouse.x * swayRotation * 0.35f);

            var deployOffset = Vector3.zero;
            var deployAngles = Vector3.zero;
            if (Time.time < deployUntil)
            {
                var progress = 1f - (deployUntil - Time.time) / deployDuration;
                var eased = 1f - Mathf.Pow(1f - Mathf.Clamp01(progress), 3f);
                deployOffset = Vector3.Lerp(new Vector3(0.05f, -0.22f, -0.10f), Vector3.zero, eased);
                deployAngles = Vector3.Lerp(new Vector3(24f, 12f, -18f), Vector3.zero, eased);
            }

            recoilPosition = Vector3.Lerp(recoilPosition, Vector3.zero, 1f - Mathf.Exp(-returnSpeed * Time.deltaTime));
            recoilRotation = Vector3.Lerp(recoilRotation, Vector3.zero, 1f - Mathf.Exp(-returnSpeed * 0.8f * Time.deltaTime));

            transform.localPosition = basePosition + bob + sway + recoilPosition + deployOffset;
            transform.localRotation = Quaternion.Euler(baseRotation + swayAngles + recoilRotation + deployAngles);
        }
    }
}
