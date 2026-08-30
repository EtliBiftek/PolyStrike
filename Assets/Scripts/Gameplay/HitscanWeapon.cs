using System.Collections;
using PolyStrike.Core;
using UnityEngine;

namespace PolyStrike.Gameplay
{
    public sealed class HitscanWeapon : MonoBehaviour
    {
        [SerializeField] private Camera shotCamera;
        [SerializeField] private float damage = 34f;
        [SerializeField] private float range = 120f;
        [SerializeField] private float roundsPerMinute = 600f;
        [SerializeField] private int magazineSize = 30;
        [SerializeField] private int reserveAmmo = 90;
        [SerializeField] private float reloadTime = 2.25f;
        [SerializeField] private LayerMask hitMask = ~0;

        private int ammoInMagazine;
        private float nextShotTime;
        private bool isReloading;

        public int AmmoInMagazine => ammoInMagazine;
        public int ReserveAmmo => reserveAmmo;
        public bool IsReloading => isReloading;

        public void SetCamera(Camera cameraToUse)
        {
            shotCamera = cameraToUse;
        }

        private void Awake()
        {
            ammoInMagazine = magazineSize;
        }

        private void Update()
        {
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

            if (ammoInMagazine <= 0)
            {
                TryStartReload();
                return;
            }

            var secondsPerShot = 60f / roundsPerMinute;
            nextShotTime = Time.time + secondsPerShot;
            ammoInMagazine--;

            var ray = new Ray(shotCamera.transform.position, shotCamera.transform.forward);
            if (!Physics.Raycast(ray, out var hit, range, hitMask, QueryTriggerInteraction.Ignore))
                return;

            var health = hit.collider.GetComponentInParent<Health>();
            if (health != null)
                health.TakeDamage(damage);
        }

        private void TryStartReload()
        {
            if (isReloading || ammoInMagazine >= magazineSize || reserveAmmo <= 0)
                return;

            StartCoroutine(Reload());
        }

        private IEnumerator Reload()
        {
            isReloading = true;
            yield return new WaitForSeconds(reloadTime);

            var needed = magazineSize - ammoInMagazine;
            var loaded = Mathf.Min(needed, reserveAmmo);
            ammoInMagazine += loaded;
            reserveAmmo -= loaded;
            isReloading = false;
        }
    }
}
