using System.Collections.Generic;
using PolyStrike.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace PolyStrike.Networking
{
    [DisallowMultipleComponent]
    public sealed class NetworkSpectatorController : MonoBehaviour
    {
        private readonly List<Entity> candidates = new List<Entity>(5);

        private Camera spectatorCamera;
        private Entity target = Entity.Null;
        private NetworkPlayerState targetState;
        private bool spectating;
        private bool chaseMode;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (FindFirstObjectByType<NetworkSpectatorController>() != null)
                return;

            var root = new GameObject("Network Spectator");
            DontDestroyOnLoad(root);
            root.AddComponent<NetworkSpectatorController>();
        }

        private void LateUpdate()
        {
            var world = ClientServerBootstrap.ClientWorld;
            if (world == null || !world.IsCreated)
            {
                ResetSpectator();
                return;
            }

            var entityManager = world.EntityManager;
            var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<NetworkPlayerState>(),
                ComponentType.ReadOnly<LocalTransform>());
            var entities = query.ToEntityArray(Allocator.Temp);

            var localEntity = Entity.Null;
            var localState = default(NetworkPlayerState);
            for (var i = 0; i < entities.Length; i++)
            {
                if (!entityManager.HasComponent<GhostOwnerIsLocal>(entities[i]))
                    continue;

                localEntity = entities[i];
                localState = entityManager.GetComponentData<NetworkPlayerState>(localEntity);
                break;
            }

            if (localEntity == Entity.Null || (localState.Flags & NetworkPlayerFlags.Alive) != 0)
            {
                entities.Dispose();
                query.Dispose();
                ResetSpectator();
                return;
            }

            spectating = true;
            RebuildCandidates(entityManager, entities, localEntity, localState.Team);
            EnsureCamera();

            if (candidates.Count == 0)
            {
                target = Entity.Null;
                entities.Dispose();
                query.Dispose();
                return;
            }

            if (!IsValidTarget(entityManager, target, localState.Team))
                target = candidates[0];

            if (GameInput.FirePressed)
                CycleTarget(1);
            else if (GameInput.SecondaryFirePressed)
                CycleTarget(-1);

            if (GameInput.JumpPressed)
                chaseMode = !chaseMode;

            if (!IsValidTarget(entityManager, target, localState.Team))
                target = candidates[0];

            targetState = entityManager.GetComponentData<NetworkPlayerState>(target);
            var targetTransform = entityManager.GetComponentData<LocalTransform>(target);
            UpdateCamera(in targetState, in targetTransform);

            entities.Dispose();
            query.Dispose();
        }

        private void RebuildCandidates(EntityManager entityManager, NativeArray<Entity> entities, Entity localEntity, byte team)
        {
            candidates.Clear();
            for (var i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                if (entity == localEntity)
                    continue;

                var state = entityManager.GetComponentData<NetworkPlayerState>(entity);
                if (state.Team != team || (state.Flags & NetworkPlayerFlags.Alive) == 0)
                    continue;

                candidates.Add(entity);
            }

            candidates.Sort((left, right) => left.Index.CompareTo(right.Index));
        }

        private bool IsValidTarget(EntityManager entityManager, Entity entity, byte team)
        {
            if (entity == Entity.Null || !entityManager.Exists(entity) || !entityManager.HasComponent<NetworkPlayerState>(entity))
                return false;

            var state = entityManager.GetComponentData<NetworkPlayerState>(entity);
            return state.Team == team && (state.Flags & NetworkPlayerFlags.Alive) != 0;
        }

        private void CycleTarget(int direction)
        {
            if (candidates.Count == 0)
                return;

            var index = candidates.IndexOf(target);
            if (index < 0)
                index = 0;
            else
                index = (index + direction + candidates.Count) % candidates.Count;
            target = candidates[index];
        }

        private void UpdateCamera(in NetworkPlayerState state, in LocalTransform targetTransform)
        {
            if (spectatorCamera == null)
                return;

            if (!spectatorCamera.gameObject.activeSelf)
                spectatorCamera.gameObject.SetActive(true);

            var eyeHeight = Mathf.Lerp(1.62f, 1.03f, state.CrouchAmount);
            var eye = new Vector3(targetTransform.Position.x, targetTransform.Position.y + eyeHeight, targetTransform.Position.z);
            var rotation = Quaternion.Euler(-state.Pitch, state.Yaw, 0f);

            if (!chaseMode)
            {
                spectatorCamera.transform.SetPositionAndRotation(eye + rotation * Vector3.forward * 0.06f, rotation);
                return;
            }

            var forward = Quaternion.Euler(0f, state.Yaw, 0f) * Vector3.forward;
            var desired = eye - forward * 3.1f + Vector3.up * 0.55f;
            var delta = desired - eye;
            var distance = delta.magnitude;
            if (distance > 0.001f && NetworkSandlineCollision.TryRaycast(
                    new float3(eye.x, eye.y, eye.z),
                    new float3(delta.x, delta.y, delta.z) / distance,
                    distance,
                    out var wall) && wall.EntryDistance < distance)
            {
                var safeDistance = Mathf.Max(0.25f, wall.EntryDistance - 0.12f);
                desired = eye + delta.normalized * safeDistance;
            }

            var lookAt = eye + forward * 1.2f;
            spectatorCamera.transform.SetPositionAndRotation(desired, Quaternion.LookRotation(lookAt - desired, Vector3.up));
        }

        private void EnsureCamera()
        {
            if (spectatorCamera != null)
                return;

            var cameraObject = GameObject.Find("Network Oyuncu Kamerası");
            if (cameraObject != null)
                spectatorCamera = cameraObject.GetComponent<Camera>();
        }

        private void OnGUI()
        {
            if (!spectating)
                return;

            GUI.depth = -50;
            var centered = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 18,
                fontStyle = FontStyle.Bold
            };
            centered.normal.textColor = Color.white;

            if (target == Entity.Null)
            {
                GUI.Label(
                    new Rect(Screen.width * 0.5f - 220f, Screen.height * 0.55f, 440f, 32f),
                    Localization.Get("spectator.no_teammates"),
                    centered);
                return;
            }

            var watched = Localization.Get("spectator.watching").Replace("{0}", targetState.PlayerName.ToString());
            GUI.Box(new Rect(Screen.width * 0.5f - 190f, Screen.height - 146f, 380f, 34f), string.Empty);
            GUI.Label(new Rect(Screen.width * 0.5f - 180f, Screen.height - 142f, 360f, 28f), watched, centered);

            var infoStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                alignment = TextAnchor.MiddleLeft
            };
            infoStyle.normal.textColor = Color.white;

            GUI.Box(new Rect(14f, Screen.height - 126f, 300f, 106f), string.Empty);
            GUI.Label(new Rect(28f, Screen.height - 116f, 270f, 28f),
                Localization.Get("hud.health").Replace("{0}", targetState.Health.ToString()), infoStyle);
            GUI.Label(new Rect(28f, Screen.height - 86f, 270f, 28f),
                Localization.Get("hud.armor").Replace("{0}", targetState.Armor.ToString()), infoStyle);

            var ammo = Localization.Get("hud.ammo")
                .Replace("{0}", targetState.MagazineAmmo.ToString())
                .Replace("{1}", targetState.ReserveAmmo.ToString());
            var rightStyle = new GUIStyle(infoStyle) { alignment = TextAnchor.MiddleRight, fontSize = 21 };
            GUI.Box(new Rect(Screen.width - 314f, Screen.height - 94f, 300f, 74f), string.Empty);
            GUI.Label(new Rect(Screen.width - 300f, Screen.height - 82f, 272f, 42f), ammo, rightStyle);

            var modeKey = chaseMode ? "spectator.mode.chase" : "spectator.mode.firstperson";
            var hint = Localization.Get("spectator.controls") + "   |   " + Localization.Get(modeKey);
            GUI.Label(new Rect(Screen.width * 0.5f - 360f, Screen.height - 104f, 720f, 30f), hint, centered);
        }

        private void ResetSpectator()
        {
            spectating = false;
            chaseMode = false;
            target = Entity.Null;
            candidates.Clear();
        }
    }
}
