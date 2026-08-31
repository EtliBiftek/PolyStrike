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
            // A competitive server must keep ticking while its window is unfocused or headless.
            Application.runInBackground = true;

#if UNITY_EDITOR
            // Multiplayer Play Mode can immediately run one server and one client for iteration.
            AutoConnectPort = DefaultGamePort;
#endif

            CreateDefaultClientServerWorlds();
            return true;
        }
    }
}
