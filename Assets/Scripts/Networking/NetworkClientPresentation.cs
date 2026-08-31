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
        private readonly Dictionary<Entity, NetworkLowPolyPawnView> remoteViews = new Dictionary<Entity, NetworkLowPolyPawnView>();
        private readonly List<Entity> staleEntities = new List<Entity>();

        private Camera localCamera;
        private Entity localPlayer = Entity.Null;
        private NetworkPlayerState localState;
        private NetworkMatchSnapshot matchSnapshot;
        private bool hasMatchSnapshot;
        private int playerCount;

        private void Update()
        {
            var world = ClientServerBootstrap.ClientWorld;
            if (world == null || !world.IsCreated)
            {
                ClearViews();
                hasMatchSnapshot = false;
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
            hasMatchSnapshot = false;
            for (var i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                var state = entityManager.GetComponentData<NetworkPlayerState>(entity);
                var localTransform = entityManager.GetComponentData<LocalTransform>(entity);
                var isLocal = entityManager.HasComponent<GhostOwnerIsLocal>(entity);

                if (isLocal)
                {
                    localPlayer = entity;
                    localState = state;
                    if (entityManager.HasComponent<NetworkMatchSnapshot>(entity))
                    {
                        matchSnapshot = entityManager.GetComponentData<NetworkMatchSnapshot>(entity);
                        hasMatchSnapshot = true;
                    }

                    UpdateLocalCamera(in state, in localTransform);
                    RemoveRemoteView(entity);
                    continue;
                }

                staleEntities.Remove(entity);
                UpdateRemoteView(entity, in state, in localTransform);
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

            var alive = (localState.Flags & NetworkPlayerFlags.Alive) != 0;
            if (alive)
                DrawCrosshair();

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                alignment = TextAnchor.LowerLeft
            };
            style.normal.textColor = Color.white;

            DrawMatchHud(style);
            if (!alive)
                return;

            var bottom = Screen.height - 24f;
            GUI.Label(new Rect(24f, bottom - 86f, 280f, 28f), Format("hud.health", localState.Health), style);
            GUI.Label(new Rect(24f, bottom - 58f, 280f, 28f), Format("hud.armor", localState.Armor), style);
            GUI.Label(new Rect(24f, bottom - 30f, 280f, 28f), Format("hud.money", localState.Money), style);

            var ammo = Localization.Get("hud.ammo")
                .Replace("{0}", localState.MagazineAmmo.ToString())
                .Replace("{1}", localState.ReserveAmmo.ToString());
            GUI.Label(new Rect(Screen.width - 300f, bottom - 46f, 280f, 32f), ammo, new GUIStyle(style)
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

        private void DrawMatchHud(GUIStyle baseStyle)
        {
            if (!hasMatchSnapshot)
                return;

            var centered = new GUIStyle(baseStyle)
            {
                alignment = TextAnchor.UpperCenter,
                fontSize = 20
            };

            var score = Localization.Get("hud.score")
                .Replace("{0}", matchSnapshot.TerroristScore.ToString())
                .Replace("{1}", matchSnapshot.CounterTerroristScore.ToString());
            GUI.Label(new Rect(Screen.width * 0.5f - 180f, 16f, 360f, 30f), score, centered);

            var round = Localization.Get("hud.round")
                .Replace("{0}", matchSnapshot.RoundNumber.ToString())
                .Replace("{1}", NetworkMatchRules.RegulationRounds.ToString());
            GUI.Label(new Rect(Screen.width * 0.5f - 180f, 42f, 360f, 28f), round, centered);

            if (matchSnapshot.Phase == NetworkMatchPhase.Waiting)
            {
                GUI.Label(new Rect(Screen.width * 0.5f - 220f, 68f, 440f, 28f), Localization.Get("network.waiting"), centered);
                return;
            }

            var phaseText = Localization.Get(GetPhaseKey(matchSnapshot.Phase));
            var timer = matchSnapshot.Phase == NetworkMatchPhase.PostPlant
                ? matchSnapshot.BombTimeRemaining
                : matchSnapshot.PhaseTimeRemaining;
            var phaseLine = Localization.Get("network.phase_time")
                .Replace("{0}", phaseText)
                .Replace("{1}", Mathf.Max(0f, timer).ToString("0.0"));
            GUI.Label(new Rect(Screen.width * 0.5f - 220f, 68f, 440f, 28f), phaseLine, centered);

            if (matchSnapshot.BombPlanted != 0)
            {
                var bombTimer = Localization.Get("hud.bomb_timer")
                    .Replace("{0}", Mathf.Max(0f, matchSnapshot.BombTimeRemaining).ToString("0.0"));
                GUI.Label(new Rect(Screen.width * 0.5f - 180f, 94f, 360f, 28f), bombTimer, centered);
            }

            if ((localState.Flags & NetworkPlayerFlags.Planting) != 0)
            {
                var planting = Localization.Get("hud.planting")
                    .Replace("{0}", Mathf.RoundToInt(matchSnapshot.InteractionProgress * 100f).ToString());
                GUI.Label(new Rect(Screen.width * 0.5f - 220f, Screen.height * 0.62f, 440f, 30f), planting, centered);
            }
            else if ((localState.Flags & NetworkPlayerFlags.Defusing) != 0)
            {
                var defusing = Localization.Get("hud.defusing")
                    .Replace("{0}", Mathf.RoundToInt(matchSnapshot.InteractionProgress * 100f).ToString());
                GUI.Label(new Rect(Screen.width * 0.5f - 220f, Screen.height * 0.62f, 440f, 30f), defusing, centered);
            }

            if (matchSnapshot.Phase == NetworkMatchPhase.RoundEnd && matchSnapshot.LastWinner <= 1)
            {
                var winner = matchSnapshot.LastWinner == 0 ? Localization.Get("team.t") : Localization.Get("team.ct");
                var reason = Localization.Get(GetReasonKey(matchSnapshot.LastReason));
                var result = Localization.Get("hud.round_winner")
                    .Replace("{0}", winner)
                    .Replace("{1}", reason);
                GUI.Label(new Rect(Screen.width * 0.5f - 300f, Screen.height * 0.28f, 600f, 32f), result, centered);
            }

            if (matchSnapshot.BuyTimeRemaining > 0f &&
                (matchSnapshot.Phase == NetworkMatchPhase.FreezeTime || matchSnapshot.Phase == NetworkMatchPhase.Live))
            {
                GUI.Label(new Rect(Screen.width * 0.5f - 180f, Screen.height - 64f, 360f, 28f), Localization.Get("network.buy_hint"), centered);
            }
        }

        private static string GetPhaseKey(NetworkMatchPhase phase)
        {
            return phase switch
            {
                NetworkMatchPhase.FreezeTime => "match.phase.freeze",
                NetworkMatchPhase.Live => "match.phase.live",
                NetworkMatchPhase.PostPlant => "match.phase.postplant",
                NetworkMatchPhase.RoundEnd => "match.phase.round_end",
                NetworkMatchPhase.HalfTime => "match.phase.halftime",
                NetworkMatchPhase.MatchEnd => "match.phase.match_end",
                _ => "network.waiting"
            };
        }

        private static string GetReasonKey(NetworkRoundEndReason reason)
        {
            return reason switch
            {
                NetworkRoundEndReason.Elimination => "match.reason.elimination",
                NetworkRoundEndReason.TimeExpired => "match.reason.time",
                NetworkRoundEndReason.BombExploded => "match.reason.explosion",
                NetworkRoundEndReason.BombDefused => "match.reason.defuse",
                _ => "match.reason.elimination"
            };
        }

        private void UpdateLocalCamera(in NetworkPlayerState state, in LocalTransform localTransform)
        {
            EnsureLocalCamera();
            if (!localCamera.gameObject.activeSelf)
                localCamera.gameObject.SetActive(true);

            var eyeHeight = Mathf.Lerp(1.62f, 1.03f, state.CrouchAmount);
            localCamera.transform.position = new Vector3(localTransform.Position.x, localTransform.Position.y + eyeHeight, localTransform.Position.z);
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

        private void UpdateRemoteView(Entity entity, in NetworkPlayerState state, in LocalTransform localTransform)
        {
            if (!remoteViews.TryGetValue(entity, out var view))
            {
                view = new NetworkLowPolyPawnView();
                remoteViews.Add(entity, view);
            }

            view.Update(in state, in localTransform, Time.deltaTime);
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

            view.Destroy();
            remoteViews.Remove(entity);
        }

        private void ClearViews()
        {
            foreach (var pair in remoteViews)
                pair.Value.Destroy();
            remoteViews.Clear();
            localPlayer = Entity.Null;
        }
    }
}
