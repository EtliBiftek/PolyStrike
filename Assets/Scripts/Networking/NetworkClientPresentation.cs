using System.Collections.Generic;
using PolyStrike.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using Unity.Transforms;
using UnityEngine;

namespace PolyStrike.Networking
{
    [DisallowMultipleComponent]
    public sealed class NetworkClientPresentation : MonoBehaviour
    {
        private sealed class PawnView
        {
            public GameObject Root;
            public Transform Head;
            public Renderer[] Renderers;
            public byte LastTeam = byte.MaxValue;
        }

        private readonly Dictionary<Entity, PawnView> remoteViews = new Dictionary<Entity, PawnView>();
        private readonly List<Entity> staleEntities = new List<Entity>();

        private Camera localCamera;
        private Entity localPlayer = Entity.Null;
        private NetworkPlayerState localState;
        private int playerCount;

        private static readonly Color TerroristColor = new Color(0.55f, 0.36f, 0.18f);
        private static readonly Color CounterTerroristColor = new Color(0.18f, 0.34f, 0.58f);

        private void Update()
        {
            var world = ClientServerBootstrap.ClientWorld;
            if (world == null || !world.IsCreated)
            {
                ClearViews();
                return;
            }

            var entityManager = world.EntityManager;
            var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<NetworkPlayerState>(),
                ComponentType.ReadOnly<LocalTransform>());
            var entities = query.ToEntityArray(Allocator.Temp);
            playerCount = entities.Length;
            staleEntities.Clear();
            foreach (var pair in remoteViews)
                staleEntities.Add(pair.Key);

            localPlayer = Entity.Null;
            for (var i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                var state = entityManager.GetComponentData<NetworkPlayerState>(entity);
                var transform = entityManager.GetComponentData<LocalTransform>(entity);
                var isLocal = entityManager.HasComponent<GhostOwnerIsLocal>(entity);

                if (isLocal)
                {
                    localPlayer = entity;
                    localState = state;
                    UpdateLocalCamera(in state, in transform);
                    RemoveRemoteView(entity);
                    continue;
                }

                staleEntities.Remove(entity);
                UpdateRemoteView(entity, in state, in transform);
            }

            entities.Dispose();
            query.Dispose();

            for (var i = 0; i < staleEntities.Count; i++)
                RemoveRemoteView(staleEntities[i]);

            if (localPlayer == Entity.Null && localCamera != null)
                localCamera.gameObject.SetActive(false);
        }

        private void OnGUI()
        {
            if (localPlayer == Entity.Null)
                return;

            DrawCrosshair();

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                alignment = TextAnchor.LowerLeft
            };
            style.normal.textColor = Color.white;

            var bottom = Screen.height - 24f;
            GUI.Label(new Rect(24f, bottom - 86f, 280f, 28f), Format("hud.health", localState.Health), style);
            GUI.Label(new Rect(24f, bottom - 58f, 280f, 28f), Format("hud.armor", localState.Armor), style);
            GUI.Label(new Rect(24f, bottom - 30f, 280f, 28f), Format("hud.money", localState.Money), style);

            var ammo = $"{localState.MagazineAmmo} / {localState.ReserveAmmo}";
            GUI.Label(new Rect(Screen.width - 230f, bottom - 46f, 210f, 32f), ammo, new GUIStyle(style)
            {
                alignment = TextAnchor.LowerRight,
                fontSize = 22
            });

            var team = localState.Team == 0 ? Localization.Get("team.t") : Localization.Get("team.ct");
            GUI.Label(new Rect(24f, 16f, 240f, 28f), Localization.Get("network.local_team").Replace("{0}", team), style);
            GUI.Label(new Rect(Screen.width - 200f, 16f, 176f, 28f), Localization.Get("network.player_count").Replace("{0}", playerCount.ToString()), new GUIStyle(style)
            {
                alignment = TextAnchor.UpperRight
            });
        }

        private void UpdateLocalCamera(in NetworkPlayerState state, in LocalTransform transform)
        {
            EnsureLocalCamera();
            if (!localCamera.gameObject.activeSelf)
                localCamera.gameObject.SetActive(true);

            var eyeHeight = Mathf.Lerp(1.62f, 1.03f, state.CrouchAmount);
            localCamera.transform.position = new Vector3(transform.Position.x, transform.Position.y + eyeHeight, transform.Position.z);
            localCamera.transform.rotation = Quaternion.Euler(-state.Pitch, state.Yaw, 0f);

            if ((state.Flags & NetworkPlayerFlags.Alive) != 0)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void EnsureLocalCamera()
        {
            if (localCamera != null)
                return;

            var cameraObject = new GameObject("Network Oyuncu Kamerası");
            cameraObject.transform.SetParent(transform, false);
            localCamera = cameraObject.AddComponent<Camera>();
            localCamera.fieldOfView = 74f;
            localCamera.nearClipPlane = 0.02f;
            cameraObject.AddComponent<AudioListener>();
        }

        private void UpdateRemoteView(Entity entity, in NetworkPlayerState state, in LocalTransform transform)
        {
            if (!remoteViews.TryGetValue(entity, out var view))
            {
                view = CreateRemoteView();
                remoteViews.Add(entity, view);
            }

            view.Root.SetActive((state.Flags & NetworkPlayerFlags.Alive) != 0);
            if (!view.Root.activeSelf)
                return;

            view.Root.transform.SetPositionAndRotation(
                new Vector3(transform.Position.x, transform.Position.y, transform.Position.z),
                Quaternion.Euler(0f, state.Yaw, 0f));

            var crouchScale = Mathf.Lerp(1f, 0.75f, state.CrouchAmount);
            view.Root.transform.localScale = new Vector3(1f, crouchScale, 1f);
            if (view.Head != null)
                view.Head.localRotation = Quaternion.Euler(-state.Pitch, 0f, 0f);

            if (view.LastTeam != state.Team)
            {
                view.LastTeam = state.Team;
                var color = state.Team == 0 ? TerroristColor : CounterTerroristColor;
                for (var i = 0; i < view.Renderers.Length; i++)
                    ApplyColor(view.Renderers[i], color);
            }
        }

        private static PawnView CreateRemoteView()
        {
            var root = new GameObject("Network Remote Player");
            var renderers = new List<Renderer>();

            var torso = CreateBodyPart(root.transform, PrimitiveType.Cube, "Gövde", new Vector3(0f, 1.12f, 0f), new Vector3(0.52f, 0.65f, 0.34f));
            renderers.Add(torso.GetComponent<Renderer>());

            var head = CreateBodyPart(root.transform, PrimitiveType.Sphere, "Kafa", new Vector3(0f, 1.65f, 0f), new Vector3(0.34f, 0.34f, 0.34f));
            renderers.Add(head.GetComponent<Renderer>());

            var leftArm = CreateBodyPart(root.transform, PrimitiveType.Cube, "Sol Kol", new Vector3(-0.37f, 1.13f, 0f), new Vector3(0.18f, 0.62f, 0.20f));
            var rightArm = CreateBodyPart(root.transform, PrimitiveType.Cube, "Sağ Kol", new Vector3(0.37f, 1.13f, 0f), new Vector3(0.18f, 0.62f, 0.20f));
            var leftLeg = CreateBodyPart(root.transform, PrimitiveType.Cube, "Sol Bacak", new Vector3(-0.15f, 0.48f, 0f), new Vector3(0.22f, 0.78f, 0.24f));
            var rightLeg = CreateBodyPart(root.transform, PrimitiveType.Cube, "Sağ Bacak", new Vector3(0.15f, 0.48f, 0f), new Vector3(0.22f, 0.78f, 0.24f));
            renderers.Add(leftArm.GetComponent<Renderer>());
            renderers.Add(rightArm.GetComponent<Renderer>());
            renderers.Add(leftLeg.GetComponent<Renderer>());
            renderers.Add(rightLeg.GetComponent<Renderer>());

            return new PawnView
            {
                Root = root,
                Head = head.transform,
                Renderers = renderers.ToArray()
            };
        }

        private static GameObject CreateBodyPart(Transform parent, PrimitiveType type, string name, Vector3 localPosition, Vector3 localScale)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;

            var collider = part.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            return part;
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

        private static string Format(string key, object value)
        {
            return Localization.Get(key).Replace("{0}", value.ToString());
        }

        private static void DrawCrosshair()
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                alignment = TextAnchor.MiddleCenter
            };
            style.normal.textColor = Color.white;
            GUI.Label(new Rect(Screen.width * 0.5f - 14f, Screen.height * 0.5f - 16f, 28f, 28f), "+", style);
        }

        private void RemoveRemoteView(Entity entity)
        {
            if (!remoteViews.TryGetValue(entity, out var view))
                return;

            if (view.Root != null)
                Destroy(view.Root);
            remoteViews.Remove(entity);
        }

        private void ClearViews()
        {
            foreach (var pair in remoteViews)
            {
                if (pair.Value.Root != null)
                    Destroy(pair.Value.Root);
            }
            remoteViews.Clear();
            localPlayer = Entity.Null;
        }
    }
}
