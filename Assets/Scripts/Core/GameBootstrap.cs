using PolyStrike.AI;
using PolyStrike.Gameplay;
using PolyStrike.Maps;
using PolyStrike.Match;
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
            GrenadeEffects.EnsureExists();
            SandlineMap.Build();
            CreatePlayer();
            CreateBots();
            CreateMatchManager();
        }

        public static MatchParticipant ConsoleAddBot(MatchTeam team)
        {
            var sameTeam = 0;
            var all = MatchParticipant.All;
            for (var i = 0; i < all.Count; i++)
            {
                if (all[i] != null && all[i].Team == team)
                    sameTeam++;
            }

            if (sameTeam >= 5)
                return null;

            var spawns = team == MatchTeam.Terrorists ? SandlineMap.TSpawns : SandlineMap.CTSpawns;
            var slot = 0;
            var bestDistance = float.NegativeInfinity;
            for (var candidate = 0; candidate < spawns.Length; candidate++)
            {
                var nearest = float.PositiveInfinity;
                for (var i = 0; i < all.Count; i++)
                {
                    var participant = all[i];
                    if (participant == null || participant.Team != team)
                        continue;
                    nearest = Mathf.Min(nearest, Vector3.Distance(participant.SpawnPosition, spawns[candidate]));
                }

                if (nearest > bestDistance)
                {
                    bestDistance = nearest;
                    slot = candidate;
                }
            }

            var bot = CreateBot(team, slot);
            MatchRoundManager.Instance?.RegisterParticipant(bot);
            bot.BeginHalf(team);
            return bot;
        }

        public static int ConsoleKickBots()
        {
            var count = 0;
            var snapshot = new MatchParticipant[MatchParticipant.All.Count];
            for (var i = 0; i < snapshot.Length; i++)
                snapshot[i] = MatchParticipant.All[i];

            for (var i = 0; i < snapshot.Length; i++)
            {
                var participant = snapshot[i];
                if (participant == null || participant.IsLocalPlayer)
                    continue;

                MatchRoundManager.Instance?.UnregisterParticipant(participant);
                Object.Destroy(participant.gameObject);
                count++;
            }

            return count;
        }

        public static bool ConsolePlaceBot()
        {
            var camera = Camera.main;
            if (camera == null)
                return false;

            MatchParticipant target = null;
            var all = MatchParticipant.All;
            for (var i = 0; i < all.Count; i++)
            {
                if (all[i] != null && !all[i].IsLocalPlayer)
                {
                    target = all[i];
                    break;
                }
            }

            if (target == null)
                return false;

            var ray = new Ray(camera.transform.position, camera.transform.forward);
            var position = camera.transform.position + camera.transform.forward * 4f;
            if (Physics.Raycast(ray, out var hit, 80f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                position = hit.point + hit.normal * 0.35f;

            var controller = target.GetComponent<CharacterController>();
            if (controller != null)
                controller.enabled = false;
            target.transform.position = position;
            if (controller != null)
                controller.enabled = true;
            target.GetComponent<PlayerMovement>()?.ResetRoundMotion();
            return true;
        }

        private static void CreateLighting()
        {
            if (Object.FindFirstObjectByType<Light>() != null)
                return;

            var lightObject = new GameObject("Güneş");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.color = new Color(1f, 0.92f, 0.78f);
            lightObject.transform.rotation = Quaternion.Euler(48f, -28f, 0f);

            RenderSettings.ambientLight = new Color(0.38f, 0.35f, 0.31f);
        }

        private static void CreatePlayer()
        {
            var tRotation = Quaternion.Euler(0f, 0f, 0f);
            var ctRotation = Quaternion.Euler(0f, 180f, 0f);
            var player = new GameObject("Oyuncu");
            player.transform.SetPositionAndRotation(SandlineMap.TSpawns[0], tRotation);

            ConfigureCharacterController(player);
            var health = player.AddComponent<Health>();
            health.SetDisableOnDeath(false);

            var participant = player.AddComponent<MatchParticipant>();
            participant.Configure(MatchTeam.Terrorists, true);
            participant.ConfigureTeamSpawns(SandlineMap.TSpawns[0], tRotation, SandlineMap.CTSpawns[0], ctRotation);

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

            var flashEffect = player.AddComponent<FlashEffect>();
            flashEffect.SetCamera(camera);

            var viewmodel = CreateViewmodel(cameraObject.transform, camera, out var muzzle);
            var feedback = cameraObject.AddComponent<CombatFeedback>();
            feedback.SetMuzzle(muzzle);

            var weapon = cameraObject.AddComponent<HitscanWeapon>();
            weapon.SetReferences(camera, look, movement, viewmodel);
            movement.SetHeldWeapon(weapon);
            deathResponse.SetWeapon(weapon);

            var utility = player.AddComponent<UtilityController>();
            utility.SetReferences(look, movement, weapon, viewmodel);
            deathResponse.SetUtility(utility);

            participant.SetLoadoutReferences(weapon, utility);
            player.AddComponent<C4Controller>();
            player.AddComponent<BuyMenu>();
            player.AddComponent<DebugHud>();
        }

        private static void CreateBots()
        {
            for (var slot = 1; slot < 5; slot++)
                CreateBot(MatchTeam.Terrorists, slot);

            for (var slot = 0; slot < 5; slot++)
                CreateBot(MatchTeam.CounterTerrorists, slot);
        }

        private static MatchParticipant CreateBot(MatchTeam initialTeam, int slot)
        {
            slot = Mathf.Clamp(slot, 0, 4);
            var tRotation = Quaternion.Euler(0f, 0f, 0f);
            var ctRotation = Quaternion.Euler(0f, 180f, 0f);
            var start = initialTeam == MatchTeam.Terrorists ? SandlineMap.TSpawns[slot] : SandlineMap.CTSpawns[slot];
            var rotation = initialTeam == MatchTeam.Terrorists ? tRotation : ctRotation;

            var root = new GameObject($"Bot {initialTeam} {slot + 1}");
            root.layer = LayerMask.NameToLayer("Ignore Raycast");
            root.transform.SetPositionAndRotation(start, rotation);
            ConfigureCharacterController(root);

            var health = root.AddComponent<Health>();
            health.SetDisableOnDeath(false);

            var participant = root.AddComponent<MatchParticipant>();
            participant.Configure(initialTeam, false);
            participant.ConfigureTeamSpawns(SandlineMap.TSpawns[slot], tRotation, SandlineMap.CTSpawns[slot], ctRotation);

            var movement = root.AddComponent<PlayerMovement>();
            root.AddComponent<PlayerFootstepAudio>();

            var weapon = root.AddComponent<HitscanWeapon>();
            weapon.SetReferences(null, null, movement, null);
            weapon.SetExternalInputBlocked(true);
            movement.SetHeldWeapon(weapon);
            participant.SetLoadoutReferences(weapon, null);

            CreateBotHitboxRig(root.transform, health, initialTeam);
            var bot = root.AddComponent<TacticalBotController>();
            bot.Configure(slot);
            return participant;
        }

        private static CharacterController ConfigureCharacterController(GameObject root)
        {
            var controller = root.AddComponent<CharacterController>();
            controller.radius = SourceUnit.ToMeters(16f);
            controller.height = SourceUnit.ToMeters(72f);
            controller.stepOffset = SourceUnit.ToMeters(18f);
            controller.slopeLimit = 45.6f;
            controller.skinWidth = 0.03f;
            return controller;
        }

        private static void CreateBotHitboxRig(Transform parent, Health health, MatchTeam team)
        {
            var color = team == MatchTeam.Terrorists
                ? new Color(0.45f, 0.29f, 0.16f)
                : new Color(0.18f, 0.30f, 0.43f);

            CreateHitboxPart(parent, health, HitGroup.Head, "Kafa", new Vector3(0f, 1.66f, 0f), new Vector3(0.30f, 0.30f, 0.30f), color * 1.12f);
            CreateHitboxPart(parent, health, HitGroup.Chest, "Göğüs", new Vector3(0f, 1.31f, 0f), new Vector3(0.54f, 0.40f, 0.30f), color);
            CreateHitboxPart(parent, health, HitGroup.Stomach, "Mide", new Vector3(0f, 1.02f, 0f), new Vector3(0.48f, 0.24f, 0.28f), color * 0.94f);
            CreateHitboxPart(parent, health, HitGroup.LeftArm, "Sol Kol", new Vector3(-0.37f, 1.25f, 0f), new Vector3(0.18f, 0.56f, 0.20f), color * 0.94f);
            CreateHitboxPart(parent, health, HitGroup.RightArm, "Sağ Kol", new Vector3(0.37f, 1.25f, 0f), new Vector3(0.18f, 0.56f, 0.20f), color * 0.94f);
            CreateHitboxPart(parent, health, HitGroup.LeftLeg, "Sol Bacak", new Vector3(-0.14f, 0.52f, 0f), new Vector3(0.20f, 0.78f, 0.22f), color * 0.82f);
            CreateHitboxPart(parent, health, HitGroup.RightLeg, "Sağ Bacak", new Vector3(0.14f, 0.52f, 0f), new Vector3(0.20f, 0.78f, 0.22f), color * 0.82f);
        }

        private static void CreateHitboxPart(
            Transform parent,
            Health health,
            HitGroup hitGroup,
            string name,
            Vector3 localPosition,
            Vector3 scale,
            Color color)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.layer = 0;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = scale;
            part.AddComponent<PlayerHitbox>().Configure(health, hitGroup);

            var renderer = part.GetComponent<Renderer>();
            if (renderer.material.HasProperty("_BaseColor"))
                renderer.material.SetColor("_BaseColor", color);
            else
                renderer.material.color = color;
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

        private static void CreateMatchManager()
        {
            var match = new GameObject("Competitive Match");
            match.AddComponent<MatchRoundManager>();
        }
    }
}
