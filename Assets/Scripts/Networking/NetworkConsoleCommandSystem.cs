using Unity.Entities;
using Unity.NetCode;

namespace PolyStrike.Networking
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup), OrderFirst = true)]
    [UpdateBefore(typeof(NetworkServerMatchSystem))]
    public partial struct NetworkConsoleCommandSystem : ISystem
    {
        private float restartRemaining;
        private byte restartPending;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkMatchRuntime>();
        }

        public void OnUpdate(ref SystemState state)
        {
            if (NetworkConsoleBridge.TryConsumeRestart(out var delay))
            {
                restartRemaining = delay;
                restartPending = 1;
            }

            if (restartPending == 0)
                return;

            restartRemaining -= SystemAPI.Time.DeltaTime;
            if (restartRemaining > 0f)
                return;

            var runtime = SystemAPI.GetSingleton<NetworkMatchRuntime>();
            runtime.Started = 0;
            runtime.Phase = NetworkMatchPhase.Waiting;
            runtime.PhaseTimeRemaining = 0f;
            runtime.BuyTimeRemaining = 0f;
            runtime.BombTimeRemaining = 0f;
            runtime.BombPlanted = 0;
            SystemAPI.SetSingleton(runtime);

            restartPending = 0;
            restartRemaining = 0f;
        }
    }
}
