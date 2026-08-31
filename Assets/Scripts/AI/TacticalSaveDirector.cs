using PolyStrike.Maps;
using PolyStrike.Match;
using PolyStrike.Player;
using UnityEngine;
using UnityEngine.AI;

namespace PolyStrike.AI
{
    [DefaultExecutionOrder(900)]
    public sealed class TacticalSaveDirector : MonoBehaviour
    {
        private const float PathRefreshInterval = 0.22f;
        private readonly NavMeshPath path = new NavMeshPath();
        private float nextPathRefresh;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (FindFirstObjectByType<TacticalSaveDirector>() != null)
                return;

            new GameObject("Tactical Save Director").AddComponent<TacticalSaveDirector>();
        }

        private void LateUpdate()
        {
            var match = MatchRoundManager.Instance;
            if (match == null || match.Phase != RoundPhase.PostPlant)
                return;

            var terroristsAlive = match.AliveCount(MatchTeam.Terrorists);
            var counterTerroristsAlive = match.AliveCount(MatchTeam.CounterTerrorists);
            if (!ShouldSave(counterTerroristsAlive, terroristsAlive, match.TimeRemaining))
                return;

            var bomb = C4Controller.PlantedBombTransform;
            if (bomb == null)
                return;

            var bots = FindObjectsByType<TacticalBotController>(FindObjectsSortMode.None);
            for (var i = 0; i < bots.Length; i++)
            {
                var bot = bots[i];
                if (bot == null)
                    continue;

                var participant = bot.GetComponent<MatchParticipant>();
                var movement = bot.GetComponent<PlayerMovement>();
                if (participant == null || movement == null || !participant.IsAlive || participant.Team != MatchTeam.CounterTerrorists)
                    continue;

                // If the bot already fought its way onto the bomb, finishing the retake is better than turning away.
                if (Vector3.Distance(bot.transform.position, bomb.position) < 6.5f)
                    continue;

                var savePocket = ResolveSavePocket(bomb.position, i);
                SteerToSavePocket(bot.transform, movement, savePocket);
            }
        }

        private void SteerToSavePocket(Transform bot, PlayerMovement movement, Vector3 destination)
        {
            if (Time.time >= nextPathRefresh)
            {
                nextPathRefresh = Time.time + PathRefreshInterval;
                NavMesh.CalculatePath(bot.position, destination, NavMesh.AllAreas, path);
            }

            var steering = destination;
            if (path.status != NavMeshPathStatus.PathInvalid && path.corners != null && path.corners.Length > 1)
            {
                for (var i = 1; i < path.corners.Length; i++)
                {
                    if (Vector3.Distance(bot.position, path.corners[i]) <= 0.65f)
                        continue;
                    steering = path.corners[i];
                    break;
                }
            }

            var direction = steering - bot.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.18f)
            {
                movement.SetMovementCommand(Vector2.zero, true, false, false);
                FaceLikelyExit(bot, destination);
                return;
            }

            direction.Normalize();
            var local = new Vector2(Vector3.Dot(bot.right, direction), Vector3.Dot(bot.forward, direction));
            movement.SetMovementCommand(local, false, false, false);
        }

        private static void FaceLikelyExit(Transform bot, Vector3 pocket)
        {
            var lookAt = pocket.x > 0f ? SandlineMap.ALongEntry : SandlineMap.BTunnelEntry;
            var direction = lookAt - bot.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f)
                return;

            var rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            bot.rotation = Quaternion.RotateTowards(bot.rotation, rotation, 480f * Time.deltaTime);
        }

        private static Vector3 ResolveSavePocket(Vector3 bombPosition, int index)
        {
            var plantedA = Vector3.Distance(bombPosition, SandlineMap.ASiteCenter) <=
                           Vector3.Distance(bombPosition, SandlineMap.BSiteCenter);

            if (plantedA)
            {
                return index % 2 == 0
                    ? SandlineMap.BSiteCenter + new Vector3(-2.8f, 0f, 4.5f)
                    : SandlineMap.TunnelControl + new Vector3(-2.0f, 0f, 3.0f);
            }

            return index % 2 == 0
                ? SandlineMap.ASiteCenter + new Vector3(3.0f, 0f, 4.2f)
                : SandlineMap.LongControl + new Vector3(2.0f, 0f, 3.0f);
        }

        private static bool ShouldSave(int ctAlive, int tAlive, float bombTimeRemaining)
        {
            if (ctAlive <= 0 || tAlive <= 0)
                return false;

            if (ctAlive == 1 && tAlive >= 3 && bombTimeRemaining <= 18f)
                return true;

            return ctAlive <= 2 && tAlive >= 4 && bombTimeRemaining <= 12f;
        }
    }
}
