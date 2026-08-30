using PolyStrike.Core;
using UnityEngine;

namespace PolyStrike.Player
{
    public sealed class PlayerLook : MonoBehaviour
    {
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private float sensitivity = 0.085f;
        [SerializeField] private float standingEyeHeight = 1.62f;
        [SerializeField] private float crouchingEyeHeight = 0.98f;
        [SerializeField] private float eyeTransitionSpeed = 8f;

        private PlayerMovement movement;
        private float pitch;

        public void SetCamera(Transform targetCamera)
        {
            cameraTransform = targetCamera;
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
            cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            if (movement != null)
            {
                var targetHeight = movement.IsCrouching ? crouchingEyeHeight : standingEyeHeight;
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
