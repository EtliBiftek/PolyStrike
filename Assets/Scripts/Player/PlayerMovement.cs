using PolyStrike.Core;
using PolyStrike.Gameplay;
using UnityEngine;

namespace PolyStrike.Player
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMovement : MonoBehaviour
    {
        private const float GroundAcceleration = 5.5f;
        private const float AirAcceleration = 12f;
        private const float GroundFriction = 5.2f;
        private const float StopSpeed = 80f;
        private const float AirWishSpeedCap = 30f;
        private const float Gravity = 800f;
        private const float JumpImpulse = 301.99338f;
        private const float WalkMultiplier = 0.52f;
        private const float DuckMultiplier = 0.34f;
        private const float DuckRate = 6.4f;

        private const float TagDelaySeconds = 2f / 64f;
        private const float TagRecoveryPerSecond = 0.4f;
        private const float RapidTagWindow = 0.5f;
        private const float TargetMobilitySlope = 0.002725f;

        [Header("Boyut")]
        [SerializeField] private float standingHeight = 1.829f;
        [SerializeField] private float crouchingHeight = 1.372f;

        private CharacterController controller;
        private HitscanWeapon heldWeapon;
        private Vector3 planarVelocity;
        private float verticalVelocity;
        private float duckAmount;

        private float velocityModifier = 1f;
        private float pendingTagFactor = 1f;
        private float pendingTagApplyTime = -1f;
        private float lastTagTime = -10f;
        private int rapidTagHits;

        public bool IsGrounded => controller != null && controller.isGrounded;
        public bool IsCrouching => duckAmount > 0.5f;
        public float DuckAmount => duckAmount;
        public Vector3 PlanarVelocity => planarVelocity;
        public float VerticalVelocity => verticalVelocity;
        public float SpeedSourceUnits => SourceUnit.ToSourceUnits(planarVelocity.magnitude);
        public float MaxSpeedSourceUnits => heldWeapon != null ? heldWeapon.MaxMoveSpeedSourceUnits : 250f;
        public float VelocityModifier => velocityModifier;

        public void SetHeldWeapon(HitscanWeapon weapon)
        {
            heldWeapon = weapon;
        }

        public void ApplyTag(float newSpeedVsM4)
        {
            var now = Time.time;
            rapidTagHits = now - lastTagTime <= RapidTagWindow ? rapidTagHits + 1 : 1;
            lastTagTime = now;

            var mobilityAdjustment = (MaxSpeedSourceUnits - 225f) * TargetMobilitySlope;
            var firstHitFactor = Mathf.Clamp(newSpeedVsM4 + mobilityAdjustment, 0.15f, 0.5f);

            // Valve'ın örneklerindeki ardışık hitler hızla bir tabana yaklaşıyor.
            var cumulativeFactor = firstHitFactor * (0.65f + 0.35f * Mathf.Pow(0.25f, rapidTagHits - 1));
            pendingTagFactor = pendingTagApplyTime < 0f
                ? cumulativeFactor
                : Mathf.Min(pendingTagFactor, cumulativeFactor);
            pendingTagApplyTime = now + TagDelaySeconds;
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            controller.height = standingHeight;
            controller.center = Vector3.up * (standingHeight * 0.5f);
        }

        private void Update()
        {
            UpdateTagging();
            UpdateDuck();

            var input = GameInput.Movement;
            var inputLength = Mathf.Clamp01(input.magnitude);
            var wishDirection = transform.forward * input.y + transform.right * input.x;
            if (wishDirection.sqrMagnitude > 0.001f)
                wishDirection.Normalize();

            if (controller.isGrounded)
            {
                if (verticalVelocity < 0f)
                    verticalVelocity = -0.5f;

                ApplyGroundFriction();

                var wishSpeed = GetGroundWishSpeed(inputLength);
                Accelerate(wishDirection, wishSpeed, GroundAcceleration);

                if (GameInput.JumpPressed && duckAmount < 0.95f)
                    verticalVelocity = SourceUnit.ToMeters(JumpImpulse);
            }
            else
            {
                ApplyAirAcceleration(wishDirection, inputLength);
            }

            verticalVelocity -= SourceUnit.ToMeters(Gravity) * Time.deltaTime;

            var velocity = planarVelocity + Vector3.up * verticalVelocity;
            var collision = controller.Move(velocity * Time.deltaTime);

            if ((collision & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
                verticalVelocity = 0f;
        }

        private void UpdateTagging()
        {
            if (pendingTagApplyTime >= 0f && Time.time >= pendingTagApplyTime)
            {
                velocityModifier = Mathf.Min(velocityModifier, pendingTagFactor);
                pendingTagApplyTime = -1f;
                pendingTagFactor = 1f;

                var taggedMaxSpeed = SourceUnit.ToMeters(MaxSpeedSourceUnits) * velocityModifier;
                if (planarVelocity.magnitude > taggedMaxSpeed)
                    planarVelocity = planarVelocity.normalized * taggedMaxSpeed;
            }

            if (!controller.isGrounded || pendingTagApplyTime >= 0f || Time.time - lastTagTime < 0.1f)
                return;

            velocityModifier = Mathf.MoveTowards(velocityModifier, 1f, TagRecoveryPerSecond * Time.deltaTime);
        }

        private float GetGroundWishSpeed(float inputLength)
        {
            var maxSpeed = SourceUnit.ToMeters(MaxSpeedSourceUnits) * velocityModifier;

            if (GameInput.WalkHeld)
                maxSpeed *= WalkMultiplier;

            maxSpeed *= Mathf.Lerp(1f, DuckMultiplier, duckAmount);
            return maxSpeed * inputLength;
        }

        private void ApplyGroundFriction()
        {
            var speed = planarVelocity.magnitude;
            if (speed < 0.001f)
            {
                planarVelocity = Vector3.zero;
                return;
            }

            var stopSpeed = SourceUnit.ToMeters(StopSpeed);
            var control = Mathf.Max(speed, stopSpeed);
            var drop = control * GroundFriction * Time.deltaTime;
            var nextSpeed = Mathf.Max(speed - drop, 0f);

            planarVelocity *= nextSpeed / speed;
        }

        private void Accelerate(Vector3 direction, float wishSpeed, float acceleration)
        {
            if (direction.sqrMagnitude < 0.001f || wishSpeed <= 0f)
                return;

            var currentSpeed = Vector3.Dot(planarVelocity, direction);
            var speedToAdd = wishSpeed - currentSpeed;
            if (speedToAdd <= 0f)
                return;

            var accelerationStep = acceleration * wishSpeed * Time.deltaTime;
            planarVelocity += direction * Mathf.Min(accelerationStep, speedToAdd);
        }

        private void ApplyAirAcceleration(Vector3 direction, float inputLength)
        {
            if (direction.sqrMagnitude < 0.001f || inputLength <= 0f)
                return;

            var uncappedWishSpeed = SourceUnit.ToMeters(MaxSpeedSourceUnits) * velocityModifier * inputLength;
            var cappedWishSpeed = Mathf.Min(uncappedWishSpeed, SourceUnit.ToMeters(AirWishSpeedCap));
            var currentSpeed = Vector3.Dot(planarVelocity, direction);
            var speedToAdd = cappedWishSpeed - currentSpeed;

            if (speedToAdd <= 0f)
                return;

            // Source'un hava hareketinde cap sadece speedToAdd hesabında kullanılıyor.
            var accelerationStep = AirAcceleration * uncappedWishSpeed * Time.deltaTime;
            planarVelocity += direction * Mathf.Min(accelerationStep, speedToAdd);
        }

        private void UpdateDuck()
        {
            var target = GameInput.CrouchHeld ? 1f : 0f;
            duckAmount = Mathf.MoveTowards(duckAmount, target, DuckRate * Time.deltaTime);

            var height = Mathf.Lerp(standingHeight, crouchingHeight, duckAmount);
            controller.height = height;
            controller.center = Vector3.up * (height * 0.5f);
        }
    }
}
