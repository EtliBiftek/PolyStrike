using System.Collections;
using PolyStrike.Core;
using PolyStrike.Player;
using UnityEngine;

namespace PolyStrike.Gameplay
{
    public sealed class HitscanWeapon : MonoBehaviour
    {
        private const int MaxPenetrations = 4;
        private const float ExitPadding = 0.01f;

        [SerializeField] private LayerMask hitMask = ~0;

        private Camera shotCamera;
        private PlayerLook playerLook;
        private PlayerMovement movement;
        private ViewmodelMotion viewmodel;
        private WeaponTuning[] profiles;
        private int[] magazineAmmo;
        private int[] reserveAmmo;

        private int activeProfileIndex;
        private int sprayIndex;
        private float accuracyPenalty;
        private float nextShotTime;
        private float deployUntil;
        private float lastShotTime = -10f;
        private bool isReloading;

        private WeaponTuning Profile => profiles[activeProfileIndex];

        public int AmmoInMagazine => magazineAmmo[activeProfileIndex];
        public int ReserveAmmo => reserveAmmo[activeProfileIndex];
        public bool IsReloading => isReloading;
        public bool IsDeploying => Time.time < deployUntil;
        public string DisplayName => Localization.Get(Profile.DisplayNameKey);
        public float MaxMoveSpeedSourceUnits => Profile.MaxMoveSpeedSourceUnits;
        public float CurrentInaccuracy { get; private set; }

        public void SetReferences(Camera cameraToUse, PlayerLook look, PlayerMovement playerMovement, ViewmodelMotion viewmodelMotion)
        {
            shotCamera = cameraToUse;
            playerLook = look;
            movement = playerMovement;
            viewmodel = viewmodelMotion;
        }

        private void Awake()
        {
            profiles = new[]
            {
                WeaponTuning.CreateTRifle(),
                WeaponTuning.CreateCTRifle()
            };

            magazineAmmo = new int[profiles.Length];
            reserveAmmo = new int[profiles.Length];

            for (var i = 0; i < profiles.Length; i++)
            {
                magazineAmmo[i] = profiles[i].MagazineSize;
                reserveAmmo[i] = profiles[i].ReserveAmmo;
            }
        }

        private void Start()
        {
            deployUntil = Time.time + Profile.DeployTime;
            nextShotTime = deployUntil;
            viewmodel?.PlayDeploy(Profile.DeployTime);
        }

        private void Update()
        {
            if (GameInput.Weapon1Pressed)
                SwitchProfile(0);
            else if (GameInput.Weapon2Pressed)
                SwitchProfile(1);

            RecoverAccuracy();
            CurrentInaccuracy = CalculateCurrentInaccuracy();

            if (Time.time - lastShotTime > Mathf.Max(0.35f, Profile.StandingRecoveryTime))
                sprayIndex = 0;

            if (GameInput.ReloadPressed)
                TryStartReload();

            if (!GameInput.FireHeld || Cursor.lockState != CursorLockMode.Locked)
                return;

            TryFire();
        }

        private void TryFire()
        {
            if (isReloading || IsDeploying || shotCamera == null || Time.time < nextShotTime)
                return;

            if (AmmoInMagazine <= 0)
            {
                TryStartReload();
                return;
            }

            var secondsPerShot = 60f / Profile.RoundsPerMinute;
            nextShotTime = Time.time + secondsPerShot;
            lastShotTime = Time.time;
            magazineAmmo[activeProfileIndex]--;

            var patternIndex = Mathf.Clamp(sprayIndex, 0, Profile.SprayPattern.Length - 1);
            var recoilPoint = Profile.SprayPattern[patternIndex];
            var previousPoint = patternIndex > 0 ? Profile.SprayPattern[patternIndex - 1] : Vector2.zero;
            var recoilStep = recoilPoint - previousPoint;

            playerLook?.AddCameraRecoil(recoilStep * 0.68f);
            viewmodel?.PlayShot(recoilStep);

            CurrentInaccuracy = CalculateCurrentInaccuracy();
            var direction = BuildShotDirection(recoilPoint, CurrentInaccuracy);
            var origin = playerLook != null ? playerLook.AimOrigin : shotCamera.transform.position;

            FireBullet(origin, direction);

            accuracyPenalty += Profile.FireInaccuracy;
            sprayIndex++;
        }

        private void FireBullet(Vector3 origin, Vector3 direction)
        {
            var currentDamage = Profile.Damage;
            var remainingRange = Profile.RangeMeters;
            var traceOrigin = origin;
            var penetrationsLeft = MaxPenetrations;

            while (currentDamage >= 1f && remainingRange > 0f)
            {
                if (!Physics.Raycast(traceOrigin, direction, out var hit, remainingRange, hitMask, QueryTriggerInteraction.Ignore))
                    return;

                var airDistanceUnits = SourceUnit.ToSourceUnits(hit.distance);
                currentDamage *= Mathf.Pow(Profile.RangeModifier, airDistanceUnits / 500f);
                remainingRange -= hit.distance;

                var hitbox = hit.collider.GetComponent<PlayerHitbox>();
                if (hitbox != null && hitbox.Health != null)
                {
                    var result = hitbox.Health.TakeBulletDamage(new BulletDamage(
                        currentDamage,
                        Profile.ArmorPenetration,
                        Profile.TaggingBaseVsM4,
                        hitbox.HitGroup,
                        direction));

                    if (!result.Killed)
                        hit.collider.GetComponent<HitReaction>()?.React(direction, result.HealthDamage);
                    return;
                }

                var health = hit.collider.GetComponentInParent<Health>();
                if (health != null)
                {
                    var result = health.TakeBulletDamage(new BulletDamage(
                        currentDamage,
                        Profile.ArmorPenetration,
                        Profile.TaggingBaseVsM4,
                        HitGroup.Chest,
                        direction));

                    if (!result.Killed)
                        hit.collider.GetComponent<HitReaction>()?.React(direction, result.HealthDamage);
                    return;
                }

                if (penetrationsLeft <= 0)
                    return;

                var surface = hit.collider.GetComponent<PenetrableSurface>();
                if (surface == null)
                    surface = hit.collider.GetComponentInParent<PenetrableSurface>();

                if (surface == null || !TryPenetrate(hit, surface, direction, ref traceOrigin, ref remainingRange, ref currentDamage))
                    return;

                penetrationsLeft--;
            }
        }

        private bool TryPenetrate(
            RaycastHit entryHit,
            PenetrableSurface surface,
            Vector3 direction,
            ref Vector3 traceOrigin,
            ref float remainingRange,
            ref float currentDamage)
        {
            var hitCollider = entryHit.collider;
            var probeDistance = hitCollider.bounds.size.magnitude + 0.2f;
            var probeOrigin = entryHit.point + direction * probeDistance;
            var backRay = new Ray(probeOrigin, -direction);

            if (!hitCollider.Raycast(backRay, out var exitHit, probeDistance + 0.25f))
                return false;

            var thicknessMeters = Vector3.Distance(entryHit.point, exitHit.point);
            if (thicknessMeters <= 0.0001f || thicknessMeters >= remainingRange)
                return false;

            var thicknessUnits = SourceUnit.ToSourceUnits(thicknessMeters);
            var penetrationModifier = Mathf.Max(surface.SameMaterialModifier, 0.01f);
            var inverseModifier = 1f / penetrationModifier;

            var lostDamage = currentDamage * surface.DamageLossModifier;
            lostDamage += (3.75f / Mathf.Max(Profile.PenetrationPower, 0.01f)) * (inverseModifier * 3f);
            lostDamage += inverseModifier * thicknessUnits * thicknessUnits / 24f;

            currentDamage -= Mathf.Max(lostDamage, 0f);
            if (currentDamage < 1f)
                return false;

            currentDamage *= Mathf.Pow(Profile.RangeModifier, thicknessUnits / 500f);
            remainingRange -= thicknessMeters;
            traceOrigin = exitHit.point + direction * ExitPadding;
            return true;
        }

        private Vector3 BuildShotDirection(Vector2 recoilPoint, float inaccuracy)
        {
            var aimRotation = playerLook != null ? playerLook.AimRotation : shotCamera.transform.rotation;
            var recoilRotation = Quaternion.Euler(-recoilPoint.y, recoilPoint.x, 0f);

            var radius = Mathf.Sqrt(Random.value) * (Profile.BaseSpread + inaccuracy);
            var angle = Random.value * Mathf.PI * 2f;
            var spreadX = Mathf.Cos(angle) * radius * Mathf.Rad2Deg;
            var spreadY = Mathf.Sin(angle) * radius * Mathf.Rad2Deg;
            var spreadRotation = Quaternion.Euler(-spreadY, spreadX, 0f);

            return aimRotation * recoilRotation * spreadRotation * Vector3.forward;
        }

        private float CalculateCurrentInaccuracy()
        {
            var stance = movement != null ? movement.DuckAmount : 0f;
            var result = Mathf.Lerp(Profile.StandingInaccuracy, Profile.CrouchingInaccuracy, stance);
            result += accuracyPenalty;

            if (movement == null)
                return result;

            if (!movement.IsGrounded)
                result += 0.35f;

            var speedFraction = movement.SpeedSourceUnits / Mathf.Max(Profile.MaxMoveSpeedSourceUnits, 1f);
            if (speedFraction <= 0.34f)
                return result;

            var movementFactor = Mathf.Clamp01((speedFraction - 0.34f) / (0.95f - 0.34f));

            if (!GameInput.WalkHeld)
                movementFactor = Mathf.Pow(movementFactor, 0.25f);

            result += Profile.MovingInaccuracy * movementFactor;
            return result;
        }

        private void RecoverAccuracy()
        {
            if (accuracyPenalty <= 0f)
                return;

            var crouched = movement != null && movement.DuckAmount > 0.5f;
            var recoveryTime = crouched ? Profile.CrouchingRecoveryTime : Profile.StandingRecoveryTime;
            var recoveryPerSecond = Profile.FireInaccuracy / Mathf.Max(recoveryTime, 0.01f);
            accuracyPenalty = Mathf.MoveTowards(accuracyPenalty, 0f, recoveryPerSecond * Time.deltaTime);
        }

        private void SwitchProfile(int index)
        {
            if (index < 0 || index >= profiles.Length || index == activeProfileIndex)
                return;

            if (isReloading)
            {
                StopAllCoroutines();
                isReloading = false;
            }

            activeProfileIndex = index;
            sprayIndex = 0;
            accuracyPenalty = 0f;

            deployUntil = Time.time + Profile.DeployTime;
            nextShotTime = deployUntil;
            viewmodel?.PlayDeploy(Profile.DeployTime);
        }

        private void TryStartReload()
        {
            if (isReloading || IsDeploying || AmmoInMagazine >= Profile.MagazineSize || ReserveAmmo <= 0)
                return;

            StartCoroutine(Reload(activeProfileIndex));
        }

        private IEnumerator Reload(int profileIndex)
        {
            isReloading = true;
            var profile = profiles[profileIndex];
            viewmodel?.PlayReloadKick();

            yield return new WaitForSeconds(profile.ReloadClipReadyTime);

            if (profileIndex != activeProfileIndex)
            {
                isReloading = false;
                yield break;
            }

            var needed = profile.MagazineSize - magazineAmmo[profileIndex];
            var loaded = Mathf.Min(needed, reserveAmmo[profileIndex]);
            magazineAmmo[profileIndex] += loaded;
            reserveAmmo[profileIndex] -= loaded;

            var tail = Mathf.Max(0f, profile.ReloadFireReadyTime - profile.ReloadClipReadyTime);
            if (tail > 0f)
                yield return new WaitForSeconds(tail);

            if (profileIndex == activeProfileIndex)
                isReloading = false;
        }
    }
}
