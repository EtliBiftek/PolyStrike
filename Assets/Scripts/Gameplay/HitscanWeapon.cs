using System.Collections;
using PolyStrike.Core;
using PolyStrike.Player;
using UnityEngine;

namespace PolyStrike.Gameplay
{
    public sealed class HitscanWeapon : MonoBehaviour
    {
        [SerializeField] private LayerMask hitMask = ~0;

        private Camera shotCamera;
        private PlayerLook playerLook;
        private PlayerMovement movement;
        private WeaponTuning[] profiles;
        private int[] magazineAmmo;
        private int[] reserveAmmo;

        private int activeProfileIndex;
        private int sprayIndex;
        private float accuracyPenalty;
        private float nextShotTime;
        private float lastShotTime = -10f;
        private bool isReloading;

        private WeaponTuning Profile => profiles[activeProfileIndex];

        public int AmmoInMagazine => magazineAmmo[activeProfileIndex];
        public int ReserveAmmo => reserveAmmo[activeProfileIndex];
        public bool IsReloading => isReloading;
        public string DisplayName => Localization.Get(Profile.DisplayNameKey);
        public float MaxMoveSpeedSourceUnits => Profile.MaxMoveSpeedSourceUnits;
        public float CurrentInaccuracy { get; private set; }

        public void SetReferences(Camera cameraToUse, PlayerLook look, PlayerMovement playerMovement)
        {
            shotCamera = cameraToUse;
            playerLook = look;
            movement = playerMovement;
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
            if (isReloading || shotCamera == null || Time.time < nextShotTime)
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

            CurrentInaccuracy = CalculateCurrentInaccuracy();
            var direction = BuildShotDirection(recoilPoint, CurrentInaccuracy);
            var origin = playerLook != null ? playerLook.AimOrigin : shotCamera.transform.position;

            if (Physics.Raycast(origin, direction, out var hit, Profile.RangeMeters, hitMask, QueryTriggerInteraction.Ignore))
            {
                var health = hit.collider.GetComponentInParent<Health>();
                if (health != null)
                    health.TakeDamage(CalculateDamage(hit.distance));
            }

            accuracyPenalty += Profile.FireInaccuracy;
            sprayIndex++;
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

            // Yürüyüşteki ceza daha yumuşak; koşuda CS'nin keskin eğrisine yakın davranıyor.
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

        private float CalculateDamage(float distanceMeters)
        {
            var distanceSourceUnits = SourceUnit.ToSourceUnits(distanceMeters);
            var damage = Profile.Damage * Mathf.Pow(Profile.RangeModifier, distanceSourceUnits / 500f);
            return Mathf.Floor(damage);
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
            nextShotTime = Time.time + 0.2f;
        }

        private void TryStartReload()
        {
            if (isReloading || AmmoInMagazine >= Profile.MagazineSize || ReserveAmmo <= 0)
                return;

            StartCoroutine(Reload(activeProfileIndex));
        }

        private IEnumerator Reload(int profileIndex)
        {
            isReloading = true;
            var profile = profiles[profileIndex];
            yield return new WaitForSeconds(profile.ReloadTime);

            if (profileIndex != activeProfileIndex)
            {
                isReloading = false;
                yield break;
            }

            var needed = profile.MagazineSize - magazineAmmo[profileIndex];
            var loaded = Mathf.Min(needed, reserveAmmo[profileIndex]);
            magazineAmmo[profileIndex] += loaded;
            reserveAmmo[profileIndex] -= loaded;
            isReloading = false;
        }
    }
}
