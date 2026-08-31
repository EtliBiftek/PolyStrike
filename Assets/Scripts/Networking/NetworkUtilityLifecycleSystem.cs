using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace PolyStrike.Networking
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup), OrderLast = true)]
    public partial struct NetworkUtilityLifecycleSystem : ISystem
    {
        private EntityQuery playerQuery;
        private EntityQuery projectileQuery;
        private EntityQuery smokeQuery;
        private EntityQuery infernoQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkMatchRuntime>();
            playerQuery = SystemAPI.QueryBuilder()
                .WithAll<NetworkPlayerState, NetworkUtilityRuntime, NetworkFlashState>()
                .Build();
            projectileQuery = SystemAPI.QueryBuilder().WithAll<NetworkGrenadeProjectile>().Build();
            smokeQuery = SystemAPI.QueryBuilder().WithAll<NetworkSmokeArea>().Build();
            infernoQuery = SystemAPI.QueryBuilder().WithAll<NetworkInfernoArea>().Build();
        }

        public void OnUpdate(ref SystemState state)
        {
            var match = SystemAPI.GetSingleton<NetworkMatchRuntime>();
            if (match.Phase == NetworkMatchPhase.Live || match.Phase == NetworkMatchPhase.PostPlant)
                return;

            var players = playerQuery.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < players.Length; i++)
            {
                state.EntityManager.SetComponentData(players[i], new NetworkUtilityRuntime());
                state.EntityManager.SetComponentData(players[i], new NetworkFlashState());
            }
            players.Dispose();

            DestroyAll(ref state, projectileQuery);
            DestroyAll(ref state, smokeQuery);
            DestroyAll(ref state, infernoQuery);
        }

        private static void DestroyAll(ref SystemState state, EntityQuery query)
        {
            var entities = query.ToEntityArray(Allocator.Temp);
            if (entities.Length > 0)
                state.EntityManager.DestroyEntity(entities);
            entities.Dispose();
        }
    }

    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(NetworkUtilityThrowSystem))]
    public partial struct NetworkPendingUtilityConstructionSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (player, loadout, runtime) in
                     SystemAPI.Query<RefRW<NetworkPlayerState>, RefRO<NetworkLoadoutState>, RefRW<NetworkUtilityRuntime>>())
            {
                if (runtime.ValueRO.PendingThrow == 0)
                    continue;

                if ((player.ValueRO.Flags & NetworkPlayerFlags.Alive) == 0)
                {
                    runtime.ValueRW = new NetworkUtilityRuntime();
                    continue;
                }

                var slot = runtime.ValueRO.PendingType switch
                {
                    0 => (byte)6,
                    1 => (byte)7,
                    2 => (byte)8,
                    3 => (byte)10,
                    _ => (byte)0
                };

                if (slot == 0 || !HasGrenade(slot, in loadout.ValueRO))
                {
                    runtime.ValueRW = new NetworkUtilityRuntime();
                    continue;
                }

                if (player.ValueRO.ActiveWeapon == slot)
                    continue;

                player.ValueRW.ActiveWeapon = slot;
                player.ValueRW.MagazineAmmo = 0;
                player.ValueRW.ReserveAmmo = 0;
            }
        }

        private static bool HasGrenade(byte slot, in NetworkLoadoutState loadout)
        {
            return slot switch
            {
                6 => loadout.HeGrenades > 0,
                7 => loadout.Flashbangs > 0,
                8 => loadout.SmokeGrenades > 0,
                10 => loadout.FireGrenades > 0,
                _ => false
            };
        }
    }
}
