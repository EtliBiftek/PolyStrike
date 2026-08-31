using PolyStrike.Gameplay;
using PolyStrike.Player;
using UnityEngine;

namespace PolyStrike.Core
{
    [RequireComponent(typeof(CharacterController), typeof(PlayerMovement))]
    public sealed class DeveloperNoclip : MonoBehaviour
    {
        private CharacterController controller;
        private PlayerMovement movement;

        public bool Enabled { get; private set; }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            movement = GetComponent<PlayerMovement>();
            enabled = false;
        }

        public void Toggle()
        {
            SetEnabled(!Enabled);
        }

        public void SetEnabled(bool value)
        {
            Enabled = value;
            enabled = value;
            movement.enabled = !value;
            controller.enabled = !value;

            if (!value)
                movement.ResetRoundMotion();
        }

        private void Update()
        {
            if (!Enabled || DeveloperConsole.IsOpen)
                return;

            var cameraTransform = Camera.main != null ? Camera.main.transform : transform;
            var input = GameInput.Movement;
            var forward = cameraTransform.forward;
            var right = cameraTransform.right;
            var direction = forward * input.y + right * input.x;

            if (GameInput.JumpPressed)
                direction += Vector3.up;
            if (GameInput.CrouchHeld)
                direction += Vector3.down;

            if (direction.sqrMagnitude > 1f)
                direction.Normalize();

            var speed = SourceUnit.ToMeters(GameInput.WalkHeld ? 250f : 650f);
            transform.position += direction * speed * Time.unscaledDeltaTime;
        }

        private void OnDisable()
        {
            if (!Enabled)
                return;

            Enabled = false;
            if (movement != null)
                movement.enabled = true;
            if (controller != null)
                controller.enabled = true;
        }
    }
}
