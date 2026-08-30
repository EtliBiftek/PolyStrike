using System.Collections;
using PolyStrike.Gameplay;
using UnityEngine;

namespace PolyStrike.Player
{
    [RequireComponent(typeof(Health))]
    public sealed class PlayerDeathResponse : MonoBehaviour
    {
        private Health health;
        private PlayerMovement movement;
        private PlayerLook look;
        private HitscanWeapon weapon;
        private Camera playerCamera;

        private void Awake()
        {
            health = GetComponent<Health>();
            movement = GetComponent<PlayerMovement>();
            look = GetComponent<PlayerLook>();
            health.Died += OnDeath;
        }

        public void SetCamera(Camera cameraToUse)
        {
            playerCamera = cameraToUse;
        }

        public void SetWeapon(HitscanWeapon weaponToUse)
        {
            weapon = weaponToUse;
        }

        private void OnDestroy()
        {
            if (health != null)
                health.Died -= OnDeath;
        }

        private void OnDeath()
        {
            if (movement != null)
                movement.enabled = false;
            if (look != null)
                look.enabled = false;
            if (weapon != null)
                weapon.enabled = false;

            StartCoroutine(PlayDeathView());
        }

        private IEnumerator PlayDeathView()
        {
            if (playerCamera == null)
                yield break;

            var cameraTransform = playerCamera.transform;
            var startPosition = cameraTransform.localPosition;
            var startRotation = cameraTransform.localRotation;
            var targetPosition = startPosition + new Vector3(0f, -0.55f, 0.08f);
            var targetRotation = startRotation * Quaternion.Euler(20f, 0f, 14f);
            var elapsed = 0f;

            while (elapsed < 0.28f)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.SmoothStep(0f, 1f, elapsed / 0.28f);
                cameraTransform.localPosition = Vector3.Lerp(startPosition, targetPosition, t);
                cameraTransform.localRotation = Quaternion.Slerp(startRotation, targetRotation, t);
                yield return null;
            }
        }
    }
}
