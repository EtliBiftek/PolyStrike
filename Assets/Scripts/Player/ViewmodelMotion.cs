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
        private Renderer[] weaponRenderers;
        private GameObject utilityModel;
        private Renderer utilityRenderer;
        private Vector3 recoilPosition;
        private Vector3 recoilRotation;
        private float deployUntil;
        private float deployDuration = 1f;
        private float bobTime;
        private bool utilityMode;

        private void Awake()
        {
            movement = GetComponentInParent<PlayerMovement>();
            weaponRenderers = GetComponentsInChildren<Renderer>(true);
            CreateUtilityModel();
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

        public void SetUtilityMode(bool enabled, GrenadeType type = GrenadeType.HighExplosive)
        {
            utilityMode = enabled;

            for (var i = 0; i < weaponRenderers.Length; i++)
            {
                if (weaponRenderers[i] != null)
                    weaponRenderers[i].enabled = !enabled;
            }

            if (utilityModel != null)
                utilityModel.SetActive(enabled);

            if (enabled)
                ApplyUtilityAppearance(type);
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

            var modePosition = utilityMode ? new Vector3(-0.02f, -0.10f, 0.30f) : basePosition;
            var modeRotation = utilityMode ? new Vector3(10f, 2f, -4f) : baseRotation;
            transform.localPosition = modePosition + bob * (utilityMode ? 0.72f : 1f) + sway + recoilPosition + deployOffset;
            transform.localRotation = Quaternion.Euler(modeRotation + swayAngles + recoilRotation + deployAngles);
        }

        private void CreateUtilityModel()
        {
            utilityModel = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            utilityModel.name = "Utility Viewmodel";
            utilityModel.layer = gameObject.layer;
            utilityModel.transform.SetParent(transform, false);
            utilityModel.transform.localPosition = new Vector3(0.06f, -0.02f, 0.06f);
            utilityModel.transform.localScale = new Vector3(0.095f, 0.11f, 0.095f);

            var collider = utilityModel.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            utilityRenderer = utilityModel.GetComponent<Renderer>();
            utilityRenderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            utilityModel.SetActive(false);
        }

        private void ApplyUtilityAppearance(GrenadeType type)
        {
            if (utilityRenderer == null)
                return;

            var color = type switch
            {
                GrenadeType.HighExplosive => new Color(0.22f, 0.28f, 0.16f),
                GrenadeType.Flashbang => new Color(0.62f, 0.64f, 0.66f),
                GrenadeType.Smoke => new Color(0.25f, 0.36f, 0.29f),
                GrenadeType.Molotov => new Color(0.42f, 0.20f, 0.08f),
                _ => Color.gray
            };

            if (utilityRenderer.material.HasProperty("_BaseColor"))
                utilityRenderer.material.SetColor("_BaseColor", color);
            else
                utilityRenderer.material.color = color;
        }
    }
}
