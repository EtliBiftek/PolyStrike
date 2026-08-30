using PolyStrike.Core;
using PolyStrike.Gameplay;
using UnityEngine;

namespace PolyStrike.Player
{
    public sealed class PlayerLook : MonoBehaviour
    {
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private float sensitivity = 0.085f;
        [SerializeField] private float standingEyeHeight = 1.62f;
        [SerializeField] private float crouchingEyeHeight = 1.18f;
        [SerializeField] private float eyeTransitionSpeed = 8f;
        [SerializeField] private float recoilReturnDelay = 0.12f;
        [SerializeField] private float recoilReturnSpeed = 7f;

        private PlayerMovement movement;
        private Vector2 cameraRecoil;
        private Vector2 externalAimPunch;
        private Vector2 aimPunchVelocity;
        private float lastRecoilTime = -10f;
        private float pitch;

        public Quaternion AimRotation => Quaternion.Euler(
            pitch - externalAimPunch.y,
            transform.eulerAngles.y + externalAimPunch.x,
            0f);

        public Vector3 AimOrigin => cameraTransform != null ? cameraTransform.position : transform.position;

        public void SetCamera(Transform targetCamera)
        {
            cameraTransform = targetCamera;
        }

        public void AddCameraRecoil(Vector2 recoilDelta)
        {
            cameraRecoil += recoilDelta;
            lastRecoilTime = Time.time;
        }

        public void ApplyExternalAimPunch(int healthDamage, HitGroup hitGroup, bool armorProtected, Vector3 bulletDirection)
        {
            if (healthDamage <= 0)
                return;

            var groupScale = hitGroup switch
            {
                HitGroup.Head => 1.35f,
                HitGroup.LeftLeg => 0.65f,
                HitGroup.RightLeg => 0.65f,
                _ => 1f
            };

            var armorScale = armorProtected ? 0.38f : 1f;
            var magnitude = Mathf.Min(healthDamage * 0.075f * groupScale * armorScale, 12f);

            var incoming = bulletDirection.sqrMagnitude > 0.0001f
                ? transform.InverseTransformDirection(-bulletDirection.normalized)
                : Vector3.forward;

            var side = Mathf.Abs(incoming.x) > 0.03f ? Mathf.Sign(incoming.x) : (Random.value < 0.5f ? -1f : 1f);
            var yawKick = magnitude * 0.22f * side;
            var pitchKick = magnitude * (hitGroup == HitGroup.Head ? 1f : 0.68f);

            externalAimPunch += new Vector2(yawKick, pitchKick);
            externalAimPunch = Vector2.ClampMagnitude(externalAimPunch, 90f);
            aimPunchVelocity += new Vector2(yawKick, pitchKick) * 3.5f;
        }

        private void Awake()
        {
            movement = GetComponent<PlayerMovement>();
        }

        private void Start()
        {
            LockCursor();
        }

        private void Update()
        {
            if (GameInput.EscapePressed)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                return;
            }

            if (Cursor.lockState != CursorLockMode.Locked)
            {
                if (GameInput.FirePressed)
                    LockCursor();

                return;
            }

            if (cameraTransform == null)
                return;

            var delta = GameInput.MouseDelta * sensitivity;
            pitch = Mathf.Clamp(pitch - delta.y, -89f, 89f);
            transform.Rotate(0f, delta.x, 0f, Space.Self);

            if (Time.time - lastRecoilTime >= recoilReturnDelay)
                cameraRecoil = Vector2.MoveTowards(cameraRecoil, Vector2.zero, recoilReturnSpeed * Time.deltaTime);

            UpdateAimPunch();

            cameraTransform.localRotation = Quaternion.Euler(
                pitch - cameraRecoil.y - externalAimPunch.y,
                cameraRecoil.x + externalAimPunch.x,
                0f);

            if (movement != null)
            {
                var targetHeight = Mathf.Lerp(standingEyeHeight, crouchingEyeHeight, movement.DuckAmount);
                var position = cameraTransform.localPosition;
                position.y = Mathf.MoveTowards(position.y, targetHeight, eyeTransitionSpeed * Time.deltaTime);
                cameraTransform.localPosition = position;
            }
        }

        private void UpdateAimPunch()
        {
            if (externalAimPunch.sqrMagnitude < 0.00001f && aimPunchVelocity.sqrMagnitude < 0.00001f)
            {
                externalAimPunch = Vector2.zero;
                aimPunchVelocity = Vector2.zero;
                return;
            }

            var deltaTime = Time.deltaTime;
            aimPunchVelocity += -externalAimPunch * (42f * deltaTime);
            aimPunchVelocity *= Mathf.Exp(-11f * deltaTime);
            externalAimPunch += aimPunchVelocity * deltaTime;
            externalAimPunch = Vector2.ClampMagnitude(externalAimPunch, 90f);
        }

        private static void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}
