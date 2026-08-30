using PolyStrike.Audio;
using PolyStrike.Core;
using PolyStrike.Gameplay;
using UnityEngine;

namespace PolyStrike.Player
{
    [RequireComponent(typeof(PlayerMovement))]
    public sealed class PlayerFootstepAudio : MonoBehaviour
    {
        private const float MinStepSpeed = 45f;
        private const float RunStrideUnits = 74f;
        private const float WalkStrideUnits = 92f;
        private const float LandingThresholdUnits = 145f;

        private PlayerMovement movement;
        private AudioSource source;
        private Vector3 lastPosition;
        private float stepTravelUnits;
        private float previousVerticalVelocity;
        private bool wasGrounded;
        private int stepVariant;

        private void Awake()
        {
            movement = GetComponent<PlayerMovement>();
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0.08f;
            source.volume = 0.72f;
            source.dopplerLevel = 0f;
        }

        private void Start()
        {
            lastPosition = transform.position;
            wasGrounded = movement.IsGrounded;
            previousVerticalVelocity = movement.VerticalVelocity;
        }

        private void LateUpdate()
        {
            var grounded = movement.IsGrounded;
            var currentPosition = transform.position;
            var horizontalDelta = currentPosition - lastPosition;
            horizontalDelta.y = 0f;

            if (wasGrounded && !grounded && movement.VerticalVelocity > 0f)
                PlayJump();

            if (!wasGrounded && grounded)
                PlayLanding(Mathf.Abs(SourceUnit.ToSourceUnits(previousVerticalVelocity)));

            if (grounded)
                UpdateSteps(horizontalDelta);
            else
                stepTravelUnits = 0f;

            lastPosition = currentPosition;
            wasGrounded = grounded;
            previousVerticalVelocity = movement.VerticalVelocity;
        }

        private void UpdateSteps(Vector3 horizontalDelta)
        {
            var speed = movement.SpeedSourceUnits;
            if (speed < MinStepSpeed)
            {
                stepTravelUnits = Mathf.Min(stepTravelUnits, 12f);
                return;
            }

            stepTravelUnits += SourceUnit.ToSourceUnits(horizontalDelta.magnitude);

            var quietMovement = GameInput.WalkHeld || movement.DuckAmount > 0.55f;
            var speedFraction = Mathf.Clamp01(speed / Mathf.Max(movement.MaxSpeedSourceUnits, 1f));
            var stride = Mathf.Lerp(WalkStrideUnits, RunStrideUnits, speedFraction);

            if (stepTravelUnits < stride)
                return;

            stepTravelUnits %= stride;
            var surface = ResolveSurface();
            var clip = ProceduralSfxBank.Footstep(surface, stepVariant++);

            source.pitch = Random.Range(0.97f, 1.035f);
            source.PlayOneShot(clip, quietMovement ? 0.30f : Mathf.Lerp(0.52f, 0.76f, speedFraction));
        }

        private void PlayJump()
        {
            source.pitch = Random.Range(0.98f, 1.02f);
            source.PlayOneShot(ProceduralSfxBank.Jump(), 0.34f);
        }

        private void PlayLanding(float impactSpeedUnits)
        {
            if (impactSpeedUnits < LandingThresholdUnits)
                return;

            var strength = Mathf.InverseLerp(LandingThresholdUnits, 620f, impactSpeedUnits);
            source.pitch = Random.Range(0.96f, 1.015f);
            source.PlayOneShot(ProceduralSfxBank.Landing(ResolveSurface()), Mathf.Lerp(0.42f, 0.92f, strength));
            stepTravelUnits = 0f;
        }

        private SurfaceMaterial ResolveSurface()
        {
            var origin = transform.position + Vector3.up * 0.18f;
            if (!Physics.Raycast(origin, Vector3.down, out var hit, 2.3f, ~0, QueryTriggerInteraction.Ignore))
                return SurfaceMaterial.Concrete;

            var surface = hit.collider.GetComponent<PenetrableSurface>();
            if (surface == null)
                surface = hit.collider.GetComponentInParent<PenetrableSurface>();

            return surface != null ? surface.Material : SurfaceMaterial.Concrete;
        }
    }
}
