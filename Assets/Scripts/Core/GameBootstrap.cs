using PolyStrike.Gameplay;
using PolyStrike.Player;
using UnityEngine;

namespace PolyStrike.Core
{
    public static class GameBootstrap
    {
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

            player.AddComponent<Health>();
            var movement = player.AddComponent<PlayerMovement>();
            var look = player.AddComponent<PlayerLook>();

            var cameraObject = new GameObject("Oyuncu Kamerası");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(player.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, SourceUnit.ToMeters(64f), 0f);

            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 75f;
            camera.nearClipPlane = 0.02f;
            cameraObject.AddComponent<AudioListener>();

            look.SetCamera(cameraObject.transform);

            var weapon = cameraObject.AddComponent<HitscanWeapon>();
            weapon.SetReferences(camera, look, movement);
            movement.SetHeldWeapon(weapon);

            player.AddComponent<DebugHud>();
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

            CreateHitboxPart(root.transform, health, HitGroup.Head, "Kafa", new Vector3(0f, 1.66f, 0f), new Vector3(0.30f, 0.30f, 0.30f));
            CreateHitboxPart(root.transform, health, HitGroup.Chest, "Göğüs", new Vector3(0f, 1.32f, 0f), new Vector3(0.56f, 0.42f, 0.28f));
            CreateHitboxPart(root.transform, health, HitGroup.Stomach, "Mide", new Vector3(0f, 1.02f, 0f), new Vector3(0.50f, 0.25f, 0.26f));
            CreateHitboxPart(root.transform, health, HitGroup.LeftArm, "Sol Kol", new Vector3(-0.38f, 1.25f, 0f), new Vector3(0.18f, 0.58f, 0.20f));
            CreateHitboxPart(root.transform, health, HitGroup.RightArm, "Sağ Kol", new Vector3(0.38f, 1.25f, 0f), new Vector3(0.18f, 0.58f, 0.20f));
            CreateHitboxPart(root.transform, health, HitGroup.LeftLeg, "Sol Bacak", new Vector3(-0.15f, 0.52f, 0f), new Vector3(0.21f, 0.78f, 0.23f));
            CreateHitboxPart(root.transform, health, HitGroup.RightLeg, "Sağ Bacak", new Vector3(0.15f, 0.52f, 0f), new Vector3(0.21f, 0.78f, 0.23f));
        }

        private static void CreateHitboxPart(
            Transform parent,
            Health health,
            HitGroup hitGroup,
            string partName,
            Vector3 localPosition,
            Vector3 localScale)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = partName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            part.AddComponent<PlayerHitbox>().Configure(health, hitGroup);
        }
    }
}
