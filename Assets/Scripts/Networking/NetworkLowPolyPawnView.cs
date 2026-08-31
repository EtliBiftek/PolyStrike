using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace PolyStrike.Networking
{
    public sealed class NetworkLowPolyPawnView
    {
        private readonly Transform pelvis;
        private readonly Transform torso;
        private readonly Transform head;
        private readonly Transform leftArm;
        private readonly Transform rightArm;
        private readonly Transform leftLeg;
        private readonly Transform rightLeg;
        private readonly Transform weapon;
        private readonly Renderer[] teamRenderers;

        private float stridePhase;
        private float moveBlend;
        private float crouchBlend;
        private float leanBlend;
        private byte lastTeam = byte.MaxValue;
        private byte lastWeapon = byte.MaxValue;

        public GameObject Root { get; }
        public Renderer HeadRenderer { get; }

        private static readonly Color TerroristCloth = new Color(0.42f, 0.28f, 0.15f);
        private static readonly Color CounterTerroristCloth = new Color(0.12f, 0.25f, 0.40f);
        private static readonly Color Skin = new Color(0.72f, 0.55f, 0.42f);
        private static readonly Color Gear = new Color(0.10f, 0.11f, 0.12f);
        private static readonly Color WeaponMetal = new Color(0.08f, 0.09f, 0.10f);

        public NetworkLowPolyPawnView()
        {
            Root = new GameObject("Network Remote Player");
            var renderers = new List<Renderer>(10);

            pelvis = CreatePart(Root.transform, PrimitiveType.Cube, "Pelvis", new Vector3(0f, 0.86f, 0f), new Vector3(0.42f, 0.24f, 0.28f), renderers);
            torso = CreatePart(Root.transform, PrimitiveType.Cube, "Torso", new Vector3(0f, 1.23f, 0f), new Vector3(0.50f, 0.58f, 0.30f), renderers);
            head = CreatePart(Root.transform, PrimitiveType.Cube, "Head", new Vector3(0f, 1.70f, 0f), new Vector3(0.30f, 0.32f, 0.30f), null);
            HeadRenderer = head.GetComponent<Renderer>();
            ApplyColor(HeadRenderer, Skin);

            leftArm = CreatePart(Root.transform, PrimitiveType.Cube, "Left Arm", new Vector3(-0.34f, 1.28f, 0.03f), new Vector3(0.16f, 0.58f, 0.18f), renderers);
            rightArm = CreatePart(Root.transform, PrimitiveType.Cube, "Right Arm", new Vector3(0.34f, 1.28f, 0.03f), new Vector3(0.16f, 0.58f, 0.18f), renderers);
            leftLeg = CreatePart(Root.transform, PrimitiveType.Cube, "Left Leg", new Vector3(-0.14f, 0.42f, 0f), new Vector3(0.20f, 0.72f, 0.22f), renderers);
            rightLeg = CreatePart(Root.transform, PrimitiveType.Cube, "Right Leg", new Vector3(0.14f, 0.42f, 0f), new Vector3(0.20f, 0.72f, 0.22f), renderers);

            var vest = CreatePart(Root.transform, PrimitiveType.Cube, "Vest", new Vector3(0f, 1.25f, -0.03f), new Vector3(0.54f, 0.44f, 0.34f), null);
            ApplyColor(vest.GetComponent<Renderer>(), Gear);

            weapon = CreatePart(Root.transform, PrimitiveType.Cube, "Weapon", new Vector3(0.18f, 1.29f, 0.34f), new Vector3(0.10f, 0.10f, 0.66f), null);
            ApplyColor(weapon.GetComponent<Renderer>(), WeaponMetal);

            teamRenderers = renderers.ToArray();
        }

        public void Update(in NetworkPlayerState state, in LocalTransform transform, float deltaTime)
        {
            var alive = (state.Flags & NetworkPlayerFlags.Alive) != 0;
            Root.SetActive(alive);
            if (!alive)
                return;

            Root.transform.SetPositionAndRotation(
                new Vector3(transform.Position.x, transform.Position.y, transform.Position.z),
                Quaternion.Euler(0f, state.Yaw, 0f));

            var planarVelocity = new Vector3(state.Velocity.x, 0f, state.Velocity.z);
            var speed = planarVelocity.magnitude;
            var grounded = (state.Flags & NetworkPlayerFlags.Grounded) != 0;
            var targetMove = Mathf.Clamp01(speed / 4.4f);
            moveBlend = Mathf.MoveTowards(moveBlend, targetMove, deltaTime * 8f);
            crouchBlend = Mathf.MoveTowards(crouchBlend, state.CrouchAmount, deltaTime * 10f);

            if (grounded && speed > 0.08f)
                stridePhase += deltaTime * Mathf.Lerp(7.5f, 12.5f, moveBlend);

            var yawRotation = Quaternion.Euler(0f, state.Yaw, 0f);
            var localVelocity = Quaternion.Inverse(yawRotation) * planarVelocity;
            var targetLean = Mathf.Clamp(localVelocity.x / 5f, -1f, 1f);
            leanBlend = Mathf.MoveTowards(leanBlend, targetLean, deltaTime * 7f);

            var stride = grounded ? Mathf.Sin(stridePhase) * moveBlend : 0f;
            var strideOpposite = grounded ? Mathf.Sin(stridePhase + Mathf.PI) * moveBlend : 0f;
            var crouchDrop = crouchBlend * 0.34f;
            var torsoPitch = Mathf.Clamp(-localVelocity.z * 2.0f, -8f, 8f) * moveBlend;
            var planting = (state.Flags & (NetworkPlayerFlags.Planting | NetworkPlayerFlags.Defusing)) != 0;

            pelvis.localPosition = new Vector3(0f, 0.86f - crouchDrop, 0f);
            pelvis.localRotation = Quaternion.Euler(0f, 0f, -leanBlend * 4f);

            torso.localPosition = new Vector3(0f, 1.23f - crouchDrop, 0f);
            torso.localRotation = Quaternion.Euler(torsoPitch + crouchBlend * 8f, 0f, -leanBlend * 7f);

            head.localPosition = new Vector3(0f, 1.70f - crouchDrop * 1.15f, 0f);
            head.localRotation = Quaternion.Euler(-state.Pitch * 0.82f, 0f, leanBlend * 2f);

            var legAmplitude = Mathf.Lerp(7f, 34f, moveBlend);
            if (!grounded)
            {
                leftLeg.localRotation = Quaternion.Euler(-14f, 0f, -5f);
                rightLeg.localRotation = Quaternion.Euler(12f, 0f, 5f);
            }
            else
            {
                leftLeg.localRotation = Quaternion.Euler(stride * legAmplitude - crouchBlend * 20f, 0f, 0f);
                rightLeg.localRotation = Quaternion.Euler(strideOpposite * legAmplitude - crouchBlend * 20f, 0f, 0f);
            }

            leftLeg.localPosition = new Vector3(-0.14f, 0.42f - crouchDrop * 0.58f, crouchBlend * 0.09f);
            rightLeg.localPosition = new Vector3(0.14f, 0.42f - crouchDrop * 0.58f, crouchBlend * 0.09f);

            if (planting)
            {
                leftArm.localRotation = Quaternion.Euler(58f, 8f, -16f);
                rightArm.localRotation = Quaternion.Euler(62f, -8f, 16f);
                weapon.gameObject.SetActive(false);
            }
            else
            {
                var armBob = grounded ? stride * 4f : 0f;
                leftArm.localRotation = Quaternion.Euler(68f + armBob, 8f, -18f);
                rightArm.localRotation = Quaternion.Euler(72f - armBob, -5f, 14f);
                weapon.gameObject.SetActive(state.ActiveWeapon is 1 or 2);
            }

            leftArm.localPosition = new Vector3(-0.34f, 1.28f - crouchDrop, 0.03f);
            rightArm.localPosition = new Vector3(0.34f, 1.28f - crouchDrop, 0.03f);

            weapon.localPosition = new Vector3(0.18f, 1.29f - crouchDrop, 0.34f);
            weapon.localRotation = Quaternion.Euler(-state.Pitch * 0.45f, 0f, 0f);

            if (lastTeam != state.Team)
            {
                lastTeam = state.Team;
                var color = state.Team == 0 ? TerroristCloth : CounterTerroristCloth;
                for (var i = 0; i < teamRenderers.Length; i++)
                    ApplyColor(teamRenderers[i], color);
            }

            if (lastWeapon != state.ActiveWeapon)
            {
                lastWeapon = state.ActiveWeapon;
                weapon.localScale = state.ActiveWeapon == 1
                    ? new Vector3(0.10f, 0.10f, 0.66f)
                    : new Vector3(0.09f, 0.09f, 0.30f);
                weapon.localPosition = state.ActiveWeapon == 1
                    ? new Vector3(0.18f, 1.29f - crouchDrop, 0.34f)
                    : new Vector3(0.18f, 1.27f - crouchDrop, 0.26f);
            }
        }

        public void Destroy()
        {
            if (Root != null)
                Object.Destroy(Root);
        }

        private static Transform CreatePart(
            Transform parent,
            PrimitiveType primitiveType,
            string name,
            Vector3 localPosition,
            Vector3 localScale,
            List<Renderer> rendererList)
        {
            var part = GameObject.CreatePrimitive(primitiveType);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;

            var collider = part.GetComponent<Collider>();
            if (collider != null)
                Object.Destroy(collider);

            var renderer = part.GetComponent<Renderer>();
            if (renderer != null && rendererList != null)
                rendererList.Add(renderer);

            return part.transform;
        }

        private static void ApplyColor(Renderer renderer, Color color)
        {
            if (renderer == null)
                return;

            var material = renderer.material;
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            else
                material.color = color;
        }
    }
}
