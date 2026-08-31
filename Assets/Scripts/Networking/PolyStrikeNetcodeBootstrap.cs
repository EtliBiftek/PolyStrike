using Unity.NetCode;
using UnityEngine;
using UnityEngine.Scripting;

namespace PolyStrike.Networking
{
    [Preserve]
    public sealed class PolyStrikeNetcodeBootstrap : ClientServerBootstrap
    {
        public const ushort DefaultGamePort = 7979;
        public const int SimulationTickRate = 64;
        public const int MaximumPlayers = 10;

        public override bool Initialize(string defaultWorldName)
        {
            // Network simulation must continue when the game loses focus or runs headless.
            Application.runInBackground = true;

#if UNITY_EDITOR
            AutoConnectPort = DefaultGamePort;
#endif

            CreateDefaultClientServerWorlds();
            ConfigureServerTickRate();
            ConfigureLagCompensationHistory();
            return true;
        }

        private static void ConfigureServerTickRate()
        {
            var serverWorld = ServerWorld;
            if (serverWorld == null || !serverWorld.IsCreated)
                return;

            var tickRate = new ClientServerTickRate
            {
                SimulationTickRate = PolyStrikeNetcodeBootstrap.SimulationTickRate,
                NetworkTickRate = PolyStrikeNetcodeBootstrap.SimulationTickRate,
                MaxSimulationStepsPerFrame = 4
            };
            tickRate.ResolveDefaults();
            serverWorld.EntityManager.CreateSingleton(tickRate);
        }

        private static void ConfigureLagCompensationHistory()
        {
            var serverWorld = ServerWorld;
            if (serverWorld != null && serverWorld.IsCreated)
            {
                serverWorld.EntityManager.CreateSingleton(new LagCompensationConfig
                {
                    // Zero uses Netcode's full supported server history capacity.
                    ServerHistorySize = 0,
                    ClientHistorySize = 1
                });
            }

            var clientWorld = ClientWorld;
            if (clientWorld != null && clientWorld.IsCreated)
            {
                clientWorld.EntityManager.CreateSingleton(new LagCompensationConfig
                {
                    ServerHistorySize = 0,
                    ClientHistorySize = 1
                });
            }
        }
    }
}
