using PolyStrike.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace PolyStrike.Networking
{
    [DisallowMultipleComponent]
    public sealed class NetworkCompetitiveHudPolish : MonoBehaviour
    {
        private ushort previousHealth;
        private bool hasPreviousHealth;
        private float damageFeedback;
        private int terroristPlayers;
        private int counterTerroristPlayers;
        private int terroristAlive;
        private int counterTerroristAlive;
        private bool hasLocalPlayer;

        private static readonly Color TerroristColor = new Color(0.84f, 0.62f, 0.31f);
        private static readonly Color CounterTerroristColor = new Color(0.39f, 0.64f, 0.92f);

        private void Update()
        {
            var world = ClientServerBootstrap.ClientWorld;
            if (world == null || !world.IsCreated)
            {
                ResetState();
                return;
            }

            var entityManager = world.EntityManager;
            var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkPlayerState>());
            var entities = query.ToEntityArray(Allocator.Temp);

            terroristPlayers = 0;
            counterTerroristPlayers = 0;
            terroristAlive = 0;
            counterTerroristAlive = 0;
            hasLocalPlayer = false;

            for (var i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                var player = entityManager.GetComponentData<NetworkPlayerState>(entity);
                var alive = (player.Flags & NetworkPlayerFlags.Alive) != 0;

                if (player.Team == 0)
                {
                    terroristPlayers++;
                    if (alive)
                        terroristAlive++;
                }
                else
                {
                    counterTerroristPlayers++;
                    if (alive)
                        counterTerroristAlive++;
                }

                if (!entityManager.HasComponent<GhostOwnerIsLocal>(entity))
                    continue;

                hasLocalPlayer = true;
                if (hasPreviousHealth && alive && player.Health < previousHealth)
                {
                    var lost = previousHealth - player.Health;
                    damageFeedback = Mathf.Clamp01(0.22f + lost / 85f);
                }

                previousHealth = player.Health;
                hasPreviousHealth = alive;
            }

            entities.Dispose();
            query.Dispose();
            damageFeedback = Mathf.MoveTowards(damageFeedback, 0f, Time.deltaTime * 2.8f);
        }

        private void OnGUI()
        {
            if (!hasLocalPlayer)
                return;

            DrawTeamStrip(0, terroristPlayers, terroristAlive, TerroristColor, Screen.width * 0.5f - 308f);
            DrawTeamStrip(1, counterTerroristPlayers, counterTerroristAlive, CounterTerroristColor, Screen.width * 0.5f + 118f);

            if (damageFeedback > 0.001f)
                DrawDamageFeedback();
        }

        private static void DrawTeamStrip(byte team, int totalPlayers, int alivePlayers, Color color, float x)
        {
            const float width = 190f;
            const float height = 34f;
            GUI.Box(new Rect(x, 14f, width, height), string.Empty);

            var labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            labelStyle.normal.textColor = color;
            GUI.Label(new Rect(x + 8f, 17f, 34f, 26f), team == 0 ? Localization.Get("team.t") : Localization.Get("team.ct"), labelStyle);

            var playerCount = Mathf.Clamp(totalPlayers, 0, 5);
            for (var i = 0; i < 5; i++)
            {
                var occupied = i < playerCount;
                var alive = i < alivePlayers;
                var previous = GUI.color;
                GUI.color = occupied
                    ? new Color(color.r, color.g, color.b, alive ? 0.95f : 0.22f)
                    : new Color(0.42f, 0.42f, 0.42f, 0.14f);
                GUI.DrawTexture(new Rect(x + 48f + i * 26f, 22f, 18f, 18f), Texture2D.whiteTexture);
                GUI.color = previous;
            }
        }

        private void DrawDamageFeedback()
        {
            var alpha = 0.16f * damageFeedback;
            var previous = GUI.color;
            GUI.color = new Color(0.72f, 0.06f, 0.04f, alpha);

            var thickness = Mathf.Lerp(18f, 52f, damageFeedback);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0f, Screen.height - thickness, Screen.width, thickness), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(0f, thickness, thickness, Screen.height - thickness * 2f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(Screen.width - thickness, thickness, thickness, Screen.height - thickness * 2f), Texture2D.whiteTexture);
            GUI.color = previous;
        }

        private void ResetState()
        {
            hasLocalPlayer = false;
            hasPreviousHealth = false;
            damageFeedback = 0f;
            terroristPlayers = 0;
            counterTerroristPlayers = 0;
            terroristAlive = 0;
            counterTerroristAlive = 0;
        }
    }
}
