using PolyStrike.Core;
using PolyStrike.Player;
using UnityEngine;

namespace PolyStrike.AI
{
    public sealed class BotStopRuntime : MonoBehaviour
    {
        private bool lastState;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (FindFirstObjectByType<BotStopRuntime>() != null)
                return;
            new GameObject("Bot Stop Runtime").AddComponent<BotStopRuntime>();
        }

        private void Update()
        {
            if (lastState == CompetitiveCvars.BotStop)
                return;

            lastState = CompetitiveCvars.BotStop;
            var bots = FindObjectsByType<TacticalBotController>(FindObjectsSortMode.None);
            for (var i = 0; i < bots.Length; i++)
            {
                var bot = bots[i];
                if (bot == null)
                    continue;

                if (lastState)
                    bot.GetComponent<PlayerMovement>()?.SetMovementCommand(Vector2.zero, false, false, false);
                bot.enabled = !lastState;
            }
        }
    }
}
