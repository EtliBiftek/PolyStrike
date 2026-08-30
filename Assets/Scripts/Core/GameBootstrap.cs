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

            CreateBlock(new Vector3(0f, 1.5f, 15f), new Vector3(30f, 3f, 1f));
            CreateBlock(new Vector3(-15f, 1.5f, 0f), new Vector3(1f, 3f, 30f));
            CreateBlock(new Vector3(15f, 1.5f, 0f), new Vector3(1f, 3f, 30f));
        }

        private static void CreateBlock(Vector3 position, Vector3 scale)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = "Duvar";
            block.transform.position = position;
            block.transform.localScale = scale;
        }

        private static void CreatePlayer()
        {
            var player = new GameObject("Oyuncu");
            player.transform.position = new Vector3(0f, 0.05f, -7f);

            var controller = player.AddComponent<CharacterController>();
            controller.radius = 0.35f;
            controller.stepOffset = 0.3f;
            controller.slopeLimit = 50f;

            player.AddComponent<Health>();
            player.AddComponent<PlayerMovement>();
            var look = player.AddComponent<PlayerLook>();

            var cameraObject = new GameObject("Oyuncu Kamerası");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(player.transform, false);
            cameraObject.transform.localPosition = new Vector3(0f, 1.62f, 0f);

            var camera = cameraObject.AddComponent<Camera>();
            camera.fieldOfView = 75f;
            camera.nearClipPlane = 0.02f;
            cameraObject.AddComponent<AudioListener>();

            look.SetCamera(cameraObject.transform);

            var weapon = cameraObject.AddComponent<HitscanWeapon>();
            weapon.SetCamera(camera);

            player.AddComponent<DebugHud>();
        }

        private static void CreateTargets()
        {
            var positions = new[]
            {
                new Vector3(-6f, 0.9f, 7f),
                new Vector3(-3f, 0.9f, 10f),
                new Vector3(0f, 0.9f, 8f),
                new Vector3(3f, 0.9f, 11f),
                new Vector3(6f, 0.9f, 7f)
            };

            for (var i = 0; i < positions.Length; i++)
            {
                var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
                target.name = $"Hedef {i + 1}";
                target.transform.position = positions[i];
                target.transform.localScale = new Vector3(0.75f, 1.8f, 0.5f);
                target.AddComponent<Health>();
            }
        }
    }
}
