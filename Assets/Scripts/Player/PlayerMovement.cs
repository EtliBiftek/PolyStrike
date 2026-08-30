using UnityEngine;
using UnityEngine.InputSystem;

namespace PolyStrike.Player
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMovement : MonoBehaviour
    {
        [Header("Hız")]
        [SerializeField] private float maxGroundSpeed = 5.5f;
        [SerializeField] private float crouchSpeed = 3.4f;
        [SerializeField] private float groundAcceleration = 16f;
        [SerializeField] private float airAcceleration = 4f;
        [SerializeField] private float groundFriction = 7f;

        [Header("Zıplama")]
        [SerializeField] private float jumpHeight = 1.15f;
        [SerializeField] private float gravity = -25f;

        [Header("Çömelme")]
        [SerializeField] private float standingHeight = 1.8f;
        [SerializeField] private float crouchingHeight = 1.15f;
        [SerializeField] private float crouchTransitionSpeed = 8f;

        private CharacterController controller;
        private Vector3 planarVelocity;
        private float verticalVelocity;

        public bool IsCrouching { get; private set; }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            controller.height = standingHeight;
            controller.center = Vector3.up * (standingHeight * 0.5f);
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            IsCrouching = keyboard.leftCtrlKey.isPressed || keyboard.cKey.isPressed;
            UpdateControllerHeight();

            var input = ReadMovementInput(keyboard);
            var wishDirection = transform.forward * input.y + transform.right * input.x;
            if (wishDirection.sqrMagnitude > 1f)
                wishDirection.Normalize();

            var grounded = controller.isGrounded;
            if (grounded)
            {
                if (verticalVelocity < 0f)
                    verticalVelocity = -2f;

                ApplyGroundFriction();

                var targetSpeed = IsCrouching ? crouchSpeed : maxGroundSpeed;
                Accelerate(wishDirection, targetSpeed, groundAcceleration);

                if (keyboard.spaceKey.wasPressedThisFrame && !IsCrouching)
                    verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
            else
            {
                Accelerate(wishDirection, maxGroundSpeed, airAcceleration);
            }

            verticalVelocity += gravity * Time.deltaTime;

            var velocity = planarVelocity + Vector3.up * verticalVelocity;
            var collision = controller.Move(velocity * Time.deltaTime);

            if ((collision & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
                verticalVelocity = 0f;
        }

        private static Vector2 ReadMovementInput(Keyboard keyboard)
        {
            var x = 0f;
            var y = 0f;

            if (keyboard.aKey.isPressed) x -= 1f;
            if (keyboard.dKey.isPressed) x += 1f;
            if (keyboard.sKey.isPressed) y -= 1f;
            if (keyboard.wKey.isPressed) y += 1f;

            return new Vector2(x, y);
        }

        private void ApplyGroundFriction()
        {
            var speed = planarVelocity.magnitude;
            if (speed < 0.01f)
            {
                planarVelocity = Vector3.zero;
                return;
            }

            var drop = speed * groundFriction * Time.deltaTime;
            var nextSpeed = Mathf.Max(speed - drop, 0f);
            planarVelocity *= nextSpeed / speed;
        }

        private void Accelerate(Vector3 direction, float targetSpeed, float acceleration)
        {
            if (direction.sqrMagnitude < 0.001f)
                return;

            var currentSpeed = Vector3.Dot(planarVelocity, direction);
            var speedToAdd = targetSpeed - currentSpeed;
            if (speedToAdd <= 0f)
                return;

            var accelerationStep = acceleration * targetSpeed * Time.deltaTime;
            planarVelocity += direction * Mathf.Min(accelerationStep, speedToAdd);
        }

        private void UpdateControllerHeight()
        {
            var targetHeight = IsCrouching ? crouchingHeight : standingHeight;
            controller.height = Mathf.MoveTowards(
                controller.height,
                targetHeight,
                crouchTransitionSpeed * Time.deltaTime);
            controller.center = Vector3.up * (controller.height * 0.5f);
        }
    }
}
