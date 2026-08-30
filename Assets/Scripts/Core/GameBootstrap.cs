using PolyStrike.Gameplay;
using PolyStrike.Player;
using UnityEngine;

namespace PolyStrike.Core
{
    public static class GameBootstrap
    {
        private const int ViewmodelLayer = 30;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreatePrototypeScene()
        {
            if (Object.FindFirstObjectByType<PlayerMovement>() != null)
                return;

            CreateLighting();
            CreateArena();
            CreatePlayer();
            CreateTargets();
        }

        private static void CreateLighting()
        {
            if (Object.FindFirstObjectByType<Light>() != null)
                return;

            var lightObject = new GameObject("Güneş");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static void CreateArena()
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Zemin";
            floor.transform.localScale = new Vector3(5f, 1f, 5f);
            floor.AddComponent<PenetrableSurface>().Configure(SurfaceMaterial.Concrete);

            CreateBlock(new Vector3(0f, 1.5f, 15f), new Vector3(30f, 3f, 1f), SurfaceMaterial.Concrete);
            CreateBlock(new Vector3(-15f, 1.5f, 0f), new Vector3(1f, 3f, 30f), SurfaceMaterial.Concrete);
            CreateBlock(new Vector3(15f, 1.5f, 0f), new Vector3(1f, 3f, 30f), SurfaceMaterial.Concrete);

            CreateBlock(new Vector3(-3f, 1.05f, 6f), new Vector3(2.4f, 2.1f, 0.14f), SurfaceMaterial.Wood, "Ahşap Test Paneli");
            CreateBlock(new Vector3(3f, 1.05f, 7f), new Vector3(2.4f, 2.1f, 0.08f), SurfaceMaterial.Metal, "Metal Test Paneli");
            CreateBlock(new Vector3(0f, 1.05f, 5f), new Vector3(2.4f, 2.1f, 0.05f), SurfaceMaterial.Glass, "Cam Test Paneli");

            CreateBlock(new Vector3(-6f, 0.035f, -2f), new Vector3(3f, 0.07f, 3f), SurfaceMaterial.Metal, "Metal Yürüyüş Alanı");
            CreateBlock(new Vector3(-2.5f, 0.035f, -2f), new Vector3(3f, 0.07f, 3f), SurfaceMaterial.Wood, "Ahşap Yürüyüş Alanı");
            CreateBlock(new Vector3(2.5f, 0.035f, -2f), new Vector3(3f, 0.07f, 3f), SurfaceMaterial.Plastic, "Plastik Yürüyüş Alanı");
        }

        private static void CreateBlock(Vector3 position, Vector3 scale, SurfaceMaterial material, string objectName = "Duvar")
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = objectName;
            block.transform.position = position;
            block.transform.localScale = scale;
            block.AddComponent<PenetrableSurface>().Configure(material);
        }

        private static void CreatePlayer()
        {
            var player = new GameObject("Oyuncu");
            player.transform.position = new Vector3(0f, 0.05f, -7f);

            var controller = player.AddComponent<CharacterController>();
            controller.radius = SourceUnit.ToMeters(16f);
            controller.height = SourceUnit.ToMeters(72f);
            controller.stepOffset = SourceUnit.ToMeters(18f);
            controller.slopeLimit = 45.6f;
            controller.skinWidth = 0.03f;

            var health = player.AddComponent<Health>();
            health.SetDisableOnDeath(false);

            var movement = player.AddComponent<PlayerMovement>();
            var look = player.AddComponent<PlayerLook>();
            var deathResponse = player.AddComponent<PlayerDeathResponse>();
            player.AddComponent<PlayerFootstepAudio>();

            var cameraObject = new GameObject("Oyuncu Kamerası");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(player.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, SourceUnit.ToMeters(64f), 0f);

            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 74f;
            camera.nearClipPlane = 0.02f;
            camera.cullingMask &= ~(1 << ViewmodelLayer);
            cameraObject.AddComponent<AudioListener>();

            look.SetCamera(cameraObject.transform);
            deathResponse.SetCamera(camera);

            var viewmodel = CreateViewmodel(cameraObject.transform, camera, out var muzzle);
            var feedback = cameraObject.AddComponent<CombatFeedback>();
            feedback.SetMuzzle(muzzle);

            var weapon = cameraObject.AddComponent<HitscanWeapon>();
            weapon.SetReferences(camera, look, movement, viewmodel);
            movement.SetHeldWeapon(weapon);
            deathResponse.SetWeapon(weapon);

            player.AddComponent<DebugHud>();
        }

        private static ViewmodelMotion CreateViewmodel(Transform cameraParent, Camera worldCamera, out Transform muzzle)
        {
            var viewmodelCameraObject = new GameObject("Viewmodel Kamerası");
            viewmodelCameraObject.transform.SetParent(cameraParent, false);
            viewmodelCameraObject.layer = ViewmodelLayer;

            var viewmodelCamera = viewmodelCameraObject.AddComponent<Camera>();
            viewmodelCamera.clearFlags = CameraClearFlags.Depth;
            viewmodelCamera.depth = worldCamera.depth + 1f;
            viewmodelCamera.fieldOfView = 60f;
            viewmodelCamera.nearClipPlane = 0.01f;
            viewmodelCamera.farClipPlane = 5f;
            viewmodelCamera.cullingMask = 1 << ViewmodelLayer;

            var root = new GameObject("Silah Viewmodel");
            root.layer = ViewmodelLayer;
            root.transform.SetParent(viewmodelCameraObject.transform, false);

            CreateViewmodelPart(root.transform, "Gövde", new Vector3(0f, 0f, 0.12f), new Vector3(0.10f, 0.08f, 0.42f));
            CreateViewmodelPart(root.transform, "Namlu", new Vector3(0f, 0.012f, 0.40f), new Vector3(0.035f, 0.035f, 0.34f));
            CreateViewmodelPart(root.transform, "Şarjör", new Vector3(0f, -0.075f, 0.08f), new Vector3(0.07f, 0.17f, 0.09f), new Vector3(15f, 0f, 0f));
            CreateViewmodelPart(root.transform, "Dipçik", new Vector3(0f, 0.01f, -0.16f), new Vector3(0.09f, 0.075f, 0.20f), new Vector3(0f, 0f, 5f));

            var muzzleObject = new GameObject("Namlu Ucu");
            muzzleObject.layer = ViewmodelLayer;
            muzzleObject.transform.SetParent(root.transform, false);
            muzzleObject.transform.localPosition = new Vector3(0f, 0.012f, 0.59f);
            muzzle = muzzleObject.transform;

            return root.AddComponent<ViewmodelMotion>();
        }

        private static void CreateViewmodelPart(
            Transform parent,
            string partName,
            Vector3 localPosition,
            Vector3 localScale,
            Vector3 localEuler = default)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = partName;
            part.layer = ViewmodelLayer;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            part.transform.localRotation = Quaternion.Euler(localEuler);

            var collider = part.GetComponent<Collider>();
            if (collider != null)
                Object.Destroy(collider);
        }

        private static void CreateTargets()
        {
            var positions = new[]
            {
                new Vector3(-6f, 0f, 7f),
                new Vector3(-3f, 0f, 10f),
                new Vector3(0f, 0f, 8f),
                new Vector3(3f, 0f, 11f),
                new Vector3(6f, 0f, 7f)
            };

            for (var i = 0; i < positions.Length; i++)
                CreateTarget($"Hedef {i + 1}", positions[i]);
        }

        private static void CreateTarget(string targetName, Vector3 position)
        {
            var root = new GameObject(targetName);
            root.transform.position = position;

            var health = root.AddComponent<Health>();
            health.SetEquipment(100f, true);
            health.SetDisableOnDeath(false);

            var head = CreateHitboxPart(root.transform, health, HitGroup.Head, "Kafa", new Vector3(0f, 1.66f, 0f), new Vector3(0.30f, 0.30f, 0.30f), 3.5f);
            var chest = CreateHitboxPart(root.transform, health, HitGroup.Chest, "Göğüs", new Vector3(0f, 1.32f, 0f), new Vector3(0.56f, 0.42f, 0.28f), 18f);
            var stomach = CreateHitboxPart(root.transform, health, HitGroup.Stomach, "Mide", new Vector3(0f, 1.02f, 0f), new Vector3(0.50f, 0.25f, 0.26f), 12f);
            var leftArm = CreateHitboxPart(root.transform, health, HitGroup.LeftArm, "Sol Kol", new Vector3(-0.38f, 1.25f, 0f), new Vector3(0.18f, 0.58f, 0.20f), 4f);
            var rightArm = CreateHitboxPart(root.transform, health, HitGroup.RightArm, "Sağ Kol", new Vector3(0.38f, 1.25f, 0f), new Vector3(0.18f, 0.58f, 0.20f), 4f);
            var leftLeg = CreateHitboxPart(root.transform, health, HitGroup.LeftLeg, "Sol Bacak", new Vector3(-0.15f, 0.52f, 0f), new Vector3(0.21f, 0.78f, 0.23f), 8f);
            var rightLeg = CreateHitboxPart(root.transform, health, HitGroup.RightLeg, "Sağ Bacak", new Vector3(0.15f, 0.52f, 0f), new Vector3(0.21f, 0.78f, 0.23f), 8f);

            ConnectRagdollPart(head, chest, 20f, 28f);
            ConnectRagdollPart(stomach, chest, 18f, 24f);
            ConnectRagdollPart(leftArm, chest, 38f, 55f);
            ConnectRagdollPart(rightArm, chest, 38f, 55f);
            ConnectRagdollPart(leftLeg, stomach, 24f, 38f);
            ConnectRagdollPart(rightLeg, stomach, 24f, 38f);

            root.AddComponent<RagdollDeath>();
        }

        private static Rigidbody CreateHitboxPart(
            Transform parent,
            Health health,
            HitGroup hitGroup,
            string partName,
            Vector3 localPosition,
            Vector3 localScale,
            float mass)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = partName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            part.AddComponent<PlayerHitbox>().Configure(health, hitGroup);

            var body = part.AddComponent<Rigidbody>();
            body.mass = mass;
            body.isKinematic = true;
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            part.AddComponent<HitReaction>();
            return body;
        }

        private static void ConnectRagdollPart(Rigidbody body, Rigidbody connectedBody, float twist, float swing)
        {
            var joint = body.gameObject.AddComponent<CharacterJoint>();
            joint.connectedBody = connectedBody;
            joint.enableCollision = false;
            joint.enablePreprocessing = true;

            joint.lowTwistLimit = new SoftJointLimit { limit = -twist };
            joint.highTwistLimit = new SoftJointLimit { limit = twist };
            joint.swing1Limit = new SoftJointLimit { limit = swing };
            joint.swing2Limit = new SoftJointLimit { limit = swing };
        }
    }
}
