using PolyStrike.Core;
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
        private float lastRecoilTime = -10f;
        private float pitch;

        public Quaternion AimRotation => Quaternion.Euler(pitch, transform.eulerAngles.y, 0f);
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

            cameraTransform.localRotation = Quaternion.Euler(
                pitch - cameraRecoil.y,
                cameraRecoil.x,
                0f);

            if (movement != null)
            {
                var targetHeight = Mathf.Lerp(standingEyeHeight, crouchingEyeHeight, movement.DuckAmount);
                var position = cameraTransform.localPosition;
                position.y = Mathf.MoveTowards(position.y, targetHeight, eyeTransitionSpeed * Time.deltaTime);
                cameraTransform.localPosition = position;
            }
        }

        private static void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}
