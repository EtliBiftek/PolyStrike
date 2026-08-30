using System.Collections;
using PolyStrike.Audio;
using PolyStrike.Core;
using PolyStrike.Gameplay;
using UnityEngine;

namespace PolyStrike.Player
{
    public sealed class UtilityController : MonoBehaviour
    {
        private const float JumpLatchDelay = 0.10f;
        private const float JumpLatchMaxAge = 0.20f;
        private const float JumpReleaseBeforeWindow = 6f / 64f;
        private const float JumpReleaseAfterWindow = 12f / 64f;

        private PlayerLook playerLook;
        private PlayerMovement movement;
        private HitscanWeapon weapon;
        private ViewmodelMotion viewmodel;
        private AudioSource handlingSource;

        private readonly int[] inventory = { 1, 1, 1, 1 };
        private GrenadeType selectedType;
        private bool utilityEquipped;
        private bool primed;
        private bool throwPending;
        private float armedStrength = 1f;

        private float observedJumpTime = -10f;
        private float pendingJumpLatchTime = -1f;
        private float jumpLatchCapturedAt = -10f;
        private ThrowPose jumpLatch;
        private bool hasJumpLatch;

        public bool IsEquipped => utilityEquipped;
        public bool IsPrimed => primed;
        public GrenadeType SelectedType => selectedType;
        public int SelectedCount => inventory[(int)selectedType];
        public float ThrowStrength => armedStrength;

        public void SetReferences(PlayerLook look, PlayerMovement playerMovement, HitscanWeapon hitscanWeapon, ViewmodelMotion viewmodelMotion)
        {
            playerLook = look;
            movement = playerMovement;
            weapon = hitscanWeapon;
            viewmodel = viewmodelMotion;
        }

        private void Awake()
        {
            handlingSource = gameObject.AddComponent<AudioSource>();
            handlingSource.playOnAwake = false;
            handlingSource.spatialBlend = 0f;
            handlingSource.dopplerLevel = 0f;
        }

        private void Update()
        {
            UpdateJumpLatch();

            // Release sonrası CS2 atışı tamamlar; bu kısa pencerede weapon switch throw'u iptal etmez.
            if (throwPending)
                return;

            HandleSelection();

            if (!utilityEquipped || Cursor.lockState != CursorLockMode.Locked)
                return;

            UpdatePriming();
        }

        private void HandleSelection()
        {
            if (GameInput.Weapon1Pressed || GameInput.Weapon2Pressed)
            {
                UnequipUtility();
                return;
            }

            if (GameInput.HeGrenadePressed)
                Equip(GrenadeType.HighExplosive);
            else if (GameInput.FlashbangPressed)
                Equip(GrenadeType.Flashbang);
            else if (GameInput.SmokePressed)
                Equip(GrenadeType.Smoke);
            else if (GameInput.MolotovPressed)
                Equip(GrenadeType.Molotov);
            else if (GameInput.UtilityPressed)
                CycleUtility();
        }

        private void UpdatePriming()
        {
            var primary = GameInput.FireHeld;
            var secondary = GameInput.SecondaryFireHeld;

            if (!primed && (GameInput.FirePressed || GameInput.SecondaryFirePressed))
            {
                primed = true;
                armedStrength = GetStrength(primary, secondary);
                handlingSource.PlayOneShot(UtilitySfxBank.PinPull(selectedType), 0.78f);
            }

            if (!primed)
                return;

            if (primary || secondary)
                armedStrength = GetStrength(primary, secondary);

            if (!GameInput.FireReleased && !GameInput.SecondaryFireReleased)
                return;

            var type = selectedType;
            var strength = armedStrength;
            var releaseTime = Time.time;
            primed = false;
            handlingSource.PlayOneShot(UtilitySfxBank.Throw(type), 0.72f);
            StartCoroutine(ThrowAfterConstructionDelay(type, strength, releaseTime));
        }

        private IEnumerator ThrowAfterConstructionDelay(GrenadeType type, float strength, float releaseTime)
        {
            throwPending = true;
            yield return new WaitForSeconds(GrenadeRules.ThrowConstructionDelay);

            if (!utilityEquipped || inventory[(int)type] <= 0)
            {
                throwPending = false;
                yield break;
            }

            var pose = ResolveConstructionPose(releaseTime);
            var launch = BuildLaunch(strength, pose);
            SpawnProjectile(type, launch.Position, launch.VelocitySourceUnits);
            inventory[(int)type]--;
            CompleteThrow();
        }

        private void UpdateJumpLatch()
        {
            if (movement == null)
                return;

            if (!Mathf.Approximately(observedJumpTime, movement.LastJumpTime))
            {
                observedJumpTime = movement.LastJumpTime;
                pendingJumpLatchTime = observedJumpTime + JumpLatchDelay;
                hasJumpLatch = false;
            }

            if (pendingJumpLatchTime < 0f || Time.time < pendingJumpLatchTime)
                return;

            jumpLatch = CapturePose();
            jumpLatchCapturedAt = Time.time;
            pendingJumpLatchTime = -1f;
            hasJumpLatch = true;
        }

        private ThrowPose ResolveConstructionPose(float releaseTime)
        {
            var releaseInJumpWindow = releaseTime >= observedJumpTime - JumpReleaseBeforeWindow &&
                                      releaseTime <= observedJumpTime + JumpReleaseAfterWindow;
            var latchFresh = hasJumpLatch && Time.time - jumpLatchCapturedAt <= JumpLatchMaxAge;

            return releaseInJumpWindow && latchFresh ? jumpLatch : CapturePose();
        }

        private ThrowPose CapturePose()
        {
            var rotation = playerLook != null ? playerLook.AimRotation : transform.rotation;
            var eye = playerLook != null ? playerLook.AimOrigin : transform.position + Vector3.up * 1.6f;
            var velocity = movement != null ? movement.WorldVelocity : Vector3.zero;
            return new ThrowPose(eye, rotation, velocity);
        }

        private LaunchState BuildLaunch(float strength, ThrowPose pose)
        {
            var euler = pose.Rotation.eulerAngles;
            var pitch = Mathf.DeltaAngle(0f, euler.x);
            var adjustedPitch = GrenadeRules.AdjustThrowPitch(pitch);
            var direction = Quaternion.Euler(adjustedPitch, euler.y, 0f) * Vector3.forward;

            var origin = pose.EyePosition + Vector3.down * SourceUnit.ToMeters((1f - strength) * 12f);
            var forwardDistance = SourceUnit.ToMeters(22f);
            var spawn = ResolveSpawnPoint(origin, direction, forwardDistance) - direction * SourceUnit.ToMeters(6f);
            var inheritedVelocity = SourceUnit.ToSourceUnits(pose.WorldVelocity) * GrenadeRules.PlayerVelocityInheritance;
            var launchVelocity = direction * GrenadeRules.GetThrowSpeed(strength) + inheritedVelocity;

            return new LaunchState(spawn, launchVelocity);
        }

        private Vector3 ResolveSpawnPoint(Vector3 origin, Vector3 direction, float maxDistance)
        {
            var hits = Physics.SphereCastAll(
                origin,
                SourceUnit.ToMeters(GrenadeRules.ProjectileRadius),
                direction,
                maxDistance,
                ~0,
                QueryTriggerInteraction.Ignore);

            var bestDistance = maxDistance;
            for (var i = 0; i < hits.Length; i++)
            {
                var collider = hits[i].collider;
                if (collider == null)
                    continue;

                var hitTransform = collider.transform;
                if (hitTransform == transform || hitTransform.IsChildOf(transform))
                    continue;

                bestDistance = Mathf.Min(bestDistance, hits[i].distance);
            }

            return origin + direction * bestDistance;
        }

        private void SpawnProjectile(GrenadeType type, Vector3 position, Vector3 velocitySourceUnits)
        {
            var grenade = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            grenade.name = GetObjectName(type);
            grenade.transform.position = position;
            grenade.transform.localScale = Vector3.one * SourceUnit.ToMeters(4f);

            var collider = grenade.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            var renderer = grenade.GetComponent<Renderer>();
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader != null)
                renderer.material = new Material(shader);

            var color = GetColor(type);
            if (renderer.material.HasProperty("_BaseColor"))
                renderer.material.SetColor("_BaseColor", color);
            else
                renderer.material.color = color;

            grenade.AddComponent<GrenadeProjectile>().Initialize(type, position, velocitySourceUnits, transform);
        }

        private void Equip(GrenadeType type)
        {
            if (inventory[(int)type] <= 0)
                return;

            selectedType = type;
            utilityEquipped = true;
            primed = false;
            weapon?.SetExternalInputBlocked(true);
            movement?.SetExternalMaxSpeed(GrenadeRules.EquippedMoveSpeed);
            viewmodel?.SetUtilityMode(true, selectedType);
            viewmodel?.PlayDeploy(0.72f);
            handlingSource.PlayOneShot(UtilitySfxBank.Draw(type), 0.62f);
        }

        private void UnequipUtility()
        {
            if (!utilityEquipped)
                return;

            StopAllCoroutines();
            CompleteThrow();
        }

        private void CompleteThrow()
        {
            utilityEquipped = false;
            primed = false;
            throwPending = false;
            weapon?.SetExternalInputBlocked(false);
            movement?.ClearExternalMaxSpeed();
            viewmodel?.SetUtilityMode(false);
            viewmodel?.PlayDeploy(0.35f);
        }

        private void CycleUtility()
        {
            var start = utilityEquipped ? (int)selectedType + 1 : 0;
            for (var offset = 0; offset < inventory.Length; offset++)
            {
                var index = (start + offset) % inventory.Length;
                if (inventory[index] <= 0)
                    continue;

                Equip((GrenadeType)index);
                return;
            }
        }

        private static float GetStrength(bool primary, bool secondary)
        {
            if (primary && secondary)
                return 0.5f;
            if (secondary)
                return 0f;
            return 1f;
        }

        private static Color GetColor(GrenadeType type)
        {
            switch (type)
            {
                case GrenadeType.HighExplosive:
                    return new Color(0.22f, 0.28f, 0.16f);
                case GrenadeType.Flashbang:
                    return new Color(0.62f, 0.64f, 0.66f);
                case GrenadeType.Smoke:
                    return new Color(0.25f, 0.36f, 0.29f);
                case GrenadeType.Molotov:
                    return new Color(0.42f, 0.20f, 0.08f);
                default:
                    return Color.gray;
            }
        }

        private static string GetObjectName(GrenadeType type)
        {
            switch (type)
            {
                case GrenadeType.HighExplosive:
                    return "HE Projectile";
                case GrenadeType.Flashbang:
                    return "Flash Projectile";
                case GrenadeType.Smoke:
                    return "Smoke Projectile";
                case GrenadeType.Molotov:
                    return "Molotov Projectile";
                default:
                    return "Grenade Projectile";
            }
        }

        private readonly struct ThrowPose
        {
            public Vector3 EyePosition { get; }
            public Quaternion Rotation { get; }
            public Vector3 WorldVelocity { get; }

            public ThrowPose(Vector3 eyePosition, Quaternion rotation, Vector3 worldVelocity)
            {
                EyePosition = eyePosition;
                Rotation = rotation;
                WorldVelocity = worldVelocity;
            }
        }

        private readonly struct LaunchState
        {
            public Vector3 Position { get; }
            public Vector3 VelocitySourceUnits { get; }

            public LaunchState(Vector3 position, Vector3 velocitySourceUnits)
            {
                Position = position;
                VelocitySourceUnits = velocitySourceUnits;
            }
        }
    }
}
