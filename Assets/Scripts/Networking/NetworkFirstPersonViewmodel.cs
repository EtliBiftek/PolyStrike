using PolyStrike.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace PolyStrike.Networking
{
    [DisallowMultipleComponent]
    public sealed class NetworkFirstPersonViewmodel : MonoBehaviour
    {
        private GameObject root;
        private Transform weapon;
        private Transform leftArm;
        private Transform rightArm;
        private Renderer muzzleFlash;
        private Camera targetCamera;
        private uint lastShotSequence;
        private byte lastWeapon = byte.MaxValue;
        private float recoilPitch;
        private float recoilBack;
        private float deployOffset;
        private float bobPhase;
        private float muzzleRemaining;
        private Vector2 sway;

        private static readonly Color SleeveColor = new Color(0.12f, 0.14f, 0.16f);
        private static readonly Color SkinColor = new Color(0.72f, 0.55f, 0.42f);
        private static readonly Color WeaponColor = new Color(0.07f, 0.08f, 0.09f);

        private void LateUpdate()
        {
            if (!TryGetLocalState(out var player, out var combat))
            {
                SetVisible(false);
                return;
            }

            var alive = (player.Flags & NetworkPlayerFlags.Alive) != 0;
            if (!alive || player.ActiveWeapon is not (1 or 2))
            {
                SetVisible(false);
                return;
            }

            EnsureViewmodel();
            SetVisible(true);

            if (combat.ShotSequence != lastShotSequence)
            {
                lastShotSequence = combat.ShotSequence;
                recoilPitch = Mathf.Min(8.5f, recoilPitch + (player.ActiveWeapon == 1 ? 2.7f : 3.9f));
                recoilBack = Mathf.Min(0.13f, recoilBack + (player.ActiveWeapon == 1 ? 0.055f : 0.075f));
                muzzleRemaining = 0.035f;
            }

            if (lastWeapon != player.ActiveWeapon)
            {
                lastWeapon = player.ActiveWeapon;
                deployOffset = 0.22f;
                ApplyWeaponShape(player.ActiveWeapon);
            }

            recoilPitch = Mathf.MoveTowards(recoilPitch, 0f, Time.deltaTime * 24f);
            recoilBack = Mathf.MoveTowards(recoilBack, 0f, Time.deltaTime * 0.55f);
            deployOffset = Mathf.MoveTowards(deployOffset, 0f, Time.deltaTime * 0.72f);
            muzzleRemaining = Mathf.Max(0f, muzzleRemaining - Time.deltaTime);
            if (muzzleFlash != null)
                muzzleFlash.enabled = muzzleRemaining > 0f;

            var speed = new Vector2(player.Velocity.x, player.Velocity.z).magnitude;
            var grounded = (player.Flags & NetworkPlayerFlags.Grounded) != 0;
            var move = Mathf.Clamp01(speed / 5.2f);
            if (grounded && speed > 0.2f)
                bobPhase += Time.deltaTime * Mathf.Lerp(7f, 12.5f, move);

            var mouse = GameInput.MouseDelta;
            var targetSway = new Vector2(
                Mathf.Clamp(-mouse.x * 0.0018f, -0.035f, 0.035f),
                Mathf.Clamp(-mouse.y * 0.0014f, -0.028f, 0.028f));
            sway = Vector2.Lerp(sway, targetSway, 1f - Mathf.Exp(-Time.deltaTime * 14f));

            var bobX = grounded ? Mathf.Sin(bobPhase) * 0.012f * move : 0f;
            var bobY = grounded ? Mathf.Abs(Mathf.Cos(bobPhase)) * 0.010f * move : -0.014f;
            var crouch = player.CrouchAmount * 0.012f;

            root.transform.localPosition = new Vector3(
                0.29f + sway.x + bobX,
                -0.27f + sway.y - bobY - crouch - deployOffset,
                0.53f - recoilBack);
            root.transform.localRotation = Quaternion.Euler(
                -6f - recoilPitch + sway.y * 80f,
                2f + sway.x * 55f,
                -1.5f - sway.x * 25f);
        }

        private void EnsureViewmodel()
        {
            if (root != null && targetCamera != null)
                return;

            var cameraObject = GameObject.Find("Network Oyuncu Kamerası");
            if (cameraObject == null)
                return;

            targetCamera = cameraObject.GetComponent<Camera>();
            if (targetCamera == null)
                return;

            root = new GameObject("First Person Viewmodel");
            root.transform.SetParent(targetCamera.transform, false);

            weapon = CreatePart(root.transform, PrimitiveType.Cube, "Weapon", new Vector3(0f, 0f, 0f), new Vector3(0.10f, 0.12f, 0.70f), WeaponColor).transform;
            leftArm = CreatePart(root.transform, PrimitiveType.Cube, "Left Arm", new Vector3(-0.13f, -0.07f, -0.02f), new Vector3(0.11f, 0.12f, 0.54f), SleeveColor).transform;
            rightArm = CreatePart(root.transform, PrimitiveType.Cube, "Right Arm", new Vector3(0.14f, -0.11f, 0.02f), new Vector3(0.11f, 0.12f, 0.56f), SleeveColor).transform;

            var leftHand = CreatePart(root.transform, PrimitiveType.Cube, "Left Hand", new Vector3(-0.10f, -0.05f, 0.29f), new Vector3(0.12f, 0.11f, 0.13f), SkinColor);
            var rightHand = CreatePart(root.transform, PrimitiveType.Cube, "Right Hand", new Vector3(0.12f, -0.08f, 0.25f), new Vector3(0.12f, 0.11f, 0.13f), SkinColor);
            leftHand.transform.localRotation = Quaternion.Euler(12f, 0f, 0f);
            rightHand.transform.localRotation = Quaternion.Euler(10f, 0f, 0f);

            var flash = CreatePart(root.transform, PrimitiveType.Sphere, "Muzzle Flash", new Vector3(0f, 0f, 0.39f), new Vector3(0.08f, 0.08f, 0.04f), new Color(1f, 0.72f, 0.18f));
            muzzleFlash = flash.GetComponent<Renderer>();
            muzzleFlash.enabled = false;
            ApplyWeaponShape(lastWeapon == byte.MaxValue ? (byte)1 : lastWeapon);
        }

        private void ApplyWeaponShape(byte slot)
        {
            if (weapon == null)
                return;

            if (slot == 1)
            {
                weapon.localScale = new Vector3(0.10f, 0.12f, 0.70f);
                weapon.localPosition = new Vector3(0f, 0f, 0f);
                leftArm.localPosition = new Vector3(-0.13f, -0.07f, -0.02f);
                rightArm.localPosition = new Vector3(0.14f, -0.11f, 0.02f);
            }
            else
            {
                weapon.localScale = new Vector3(0.09f, 0.11f, 0.32f);
                weapon.localPosition = new Vector3(0.04f, -0.01f, 0.12f);
                leftArm.localPosition = new Vector3(-0.10f, -0.11f, -0.08f);
                rightArm.localPosition = new Vector3(0.12f, -0.08f, 0.05f);
            }
        }

        private void SetVisible(bool visible)
        {
            if (root != null && root.activeSelf != visible)
                root.SetActive(visible);
        }

        private static GameObject CreatePart(Transform parent, PrimitiveType type, string name, Vector3 position, Vector3 scale, Color color)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;

            var collider = part.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            var renderer = part.GetComponent<Renderer>();
            if (renderer != null)
            {
                var material = renderer.material;
                if (material.HasProperty("_BaseColor"))
                    material.SetColor("_BaseColor", color);
                else
                    material.color = color;
            }

            return part;
        }

        private static bool TryGetLocalState(out NetworkPlayerState player, out NetworkCombatPresentationState combat)
        {
            player = default;
            combat = default;
            var world = ClientServerBootstrap.ClientWorld;
            if (world == null || !world.IsCreated)
                return false;

            var entityManager = world.EntityManager;
            var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<NetworkPlayerState>(),
                ComponentType.ReadOnly<NetworkCombatPresentationState>());
            var entities = query.ToEntityArray(Allocator.Temp);
            var found = false;

            for (var i = 0; i < entities.Length; i++)
            {
                if (!entityManager.HasComponent<GhostOwnerIsLocal>(entities[i]))
                    continue;

                player = entityManager.GetComponentData<NetworkPlayerState>(entities[i]);
                combat = entityManager.GetComponentData<NetworkCombatPresentationState>(entities[i]);
                found = true;
                break;
            }

            entities.Dispose();
            query.Dispose();
            return found;
        }

        private void OnDestroy()
        {
            if (root != null)
                Destroy(root);
        }
    }
}
