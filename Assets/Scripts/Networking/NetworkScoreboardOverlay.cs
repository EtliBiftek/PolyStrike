using System.Collections.Generic;
using PolyStrike.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace PolyStrike.Networking
{
    [DisallowMultipleComponent]
    public sealed class NetworkScoreboardOverlay : MonoBehaviour
    {
        private readonly List<Entry> entries = new List<Entry>(PolyStrikeNetcodeBootstrap.MaximumPlayers);

        private readonly struct Entry
        {
            public readonly int NetworkId;
            public readonly string PlayerName;
            public readonly byte Team;
            public readonly ushort Kills;
            public readonly ushort Deaths;
            public readonly ushort PingMs;
            public readonly bool Alive;
            public readonly bool Local;

            public Entry(int networkId, string playerName, byte team, ushort kills, ushort deaths, ushort pingMs, bool alive, bool local)
            {
                NetworkId = networkId;
                PlayerName = playerName;
                Team = team;
                Kills = kills;
                Deaths = deaths;
                PingMs = pingMs;
                Alive = alive;
                Local = local;
            }
        }

        private void OnGUI()
        {
            if (!GameInput.ScoreboardHeld)
                return;

            var world = ClientServerBootstrap.ClientWorld;
            if (world == null || !world.IsCreated)
                return;

            var entityManager = world.EntityManager;
            var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<NetworkPlayerState>(),
                ComponentType.ReadOnly<GhostOwner>());
            var entities = query.ToEntityArray(Allocator.Temp);
            if (entities.Length == 0)
            {
                entities.Dispose();
                query.Dispose();
                return;
            }

            entries.Clear();
            var hasMatch = false;
            var match = default(NetworkMatchSnapshot);

            for (var i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                var player = entityManager.GetComponentData<NetworkPlayerState>(entity);
                var owner = entityManager.GetComponentData<GhostOwner>(entity);
                var isLocal = entityManager.HasComponent<GhostOwnerIsLocal>(entity);
                entries.Add(new Entry(
                    owner.NetworkId,
                    player.PlayerName.ToString(),
                    player.Team,
                    player.Kills,
                    player.Deaths,
                    player.PingMs,
                    (player.Flags & NetworkPlayerFlags.Alive) != 0,
                    isLocal));

                if (isLocal && entityManager.HasComponent<NetworkMatchSnapshot>(entity))
                {
                    match = entityManager.GetComponentData<NetworkMatchSnapshot>(entity);
                    hasMatch = true;
                }
            }

            entities.Dispose();
            query.Dispose();

            entries.Sort((left, right) =>
            {
                var teamCompare = left.Team.CompareTo(right.Team);
                if (teamCompare != 0)
                    return teamCompare;

                var killCompare = right.Kills.CompareTo(left.Kills);
                if (killCompare != 0)
                    return killCompare;

                var deathCompare = left.Deaths.CompareTo(right.Deaths);
                return deathCompare != 0 ? deathCompare : left.NetworkId.CompareTo(right.NetworkId);
            });

            const float width = 760f;
            const float height = 450f;
            var rect = new Rect(
                (Screen.width - width) * 0.5f,
                Mathf.Max(30f, (Screen.height - height) * 0.32f),
                width,
                height);

            GUI.Box(rect, string.Empty);
            GUILayout.BeginArea(new Rect(rect.x + 24f, rect.y + 18f, rect.width - 48f, rect.height - 36f));

            var titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
            titleStyle.normal.textColor = Color.white;
            GUILayout.Label(Localization.Get("scoreboard.title"), titleStyle, GUILayout.Height(30f));

            if (hasMatch)
            {
                var score = Localization.Get("hud.score")
                    .Replace("{0}", match.TerroristScore.ToString())
                    .Replace("{1}", match.CounterTerroristScore.ToString());
                GUILayout.Label(score, titleStyle, GUILayout.Height(28f));
            }

            GUILayout.Space(8f);
            DrawTeam(0, new Color(0.82f, 0.60f, 0.30f));
            GUILayout.Space(12f);
            DrawTeam(1, new Color(0.38f, 0.62f, 0.90f));
            GUILayout.EndArea();
        }

        private void DrawTeam(byte team, Color color)
        {
            var headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold
            };
            headerStyle.normal.textColor = color;

            var centeredHeader = new GUIStyle(headerStyle)
            {
                alignment = TextAnchor.MiddleCenter
            };

            GUILayout.BeginHorizontal();
            GUILayout.Label(team == 0 ? Localization.Get("team.t") : Localization.Get("team.ct"), headerStyle);
            GUILayout.FlexibleSpace();
            GUILayout.Label(Localization.Get("scoreboard.kills"), centeredHeader, GUILayout.Width(64f));
            GUILayout.Label(Localization.Get("scoreboard.deaths"), centeredHeader, GUILayout.Width(64f));
            GUILayout.Label(Localization.Get("scoreboard.ping"), centeredHeader, GUILayout.Width(70f));
            GUILayout.Label(Localization.Get("scoreboard.status"), headerStyle, GUILayout.Width(105f));
            GUILayout.EndHorizontal();

            var rowStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16
            };
            rowStyle.normal.textColor = Color.white;

            var numberStyle = new GUIStyle(rowStyle)
            {
                alignment = TextAnchor.MiddleCenter
            };

            var statusStyle = new GUIStyle(rowStyle)
            {
                alignment = TextAnchor.MiddleRight
            };

            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.Team != team)
                    continue;

                var playerName = string.IsNullOrWhiteSpace(entry.PlayerName)
                    ? Localization.Get("scoreboard.player").Replace("{0}", entry.NetworkId.ToString())
                    : entry.PlayerName;
                if (entry.Local)
                    playerName = Localization.Get("scoreboard.you").Replace("{0}", playerName);

                var status = Localization.Get(entry.Alive ? "scoreboard.alive" : "scoreboard.dead");
                var previousColor = GUI.color;
                if (!entry.Alive)
                    GUI.color = new Color(previousColor.r, previousColor.g, previousColor.b, 0.55f);

                GUILayout.BeginHorizontal(GUI.skin.box, GUILayout.Height(30f));
                GUILayout.Label(playerName, rowStyle);
                GUILayout.FlexibleSpace();
                GUILayout.Label(entry.Kills.ToString(), numberStyle, GUILayout.Width(64f));
                GUILayout.Label(entry.Deaths.ToString(), numberStyle, GUILayout.Width(64f));
                GUILayout.Label(entry.PingMs.ToString(), numberStyle, GUILayout.Width(70f));
                GUILayout.Label(status, statusStyle, GUILayout.Width(105f));
                GUILayout.EndHorizontal();
                GUI.color = previousColor;
            }
        }
    }
}
