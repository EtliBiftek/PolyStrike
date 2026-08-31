using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace PolyStrike.Networking
{
    public struct NetworkModernReloadTracker : IComponentData
    {
        public byte InReload;
        public byte Corrected;
        public byte MagazineAtStart;
    }

    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(NetworkServerCombatSystem))]
    [UpdateBefore(typeof(NetworkLoadoutAmmoCommitSystem))]
    public partial struct NetworkModernReloadSystem : ISystem
    {
        private EntityQuery playerQuery;

        public void OnCreate(ref SystemState state)
        {
            playerQuery = SystemAPI.QueryBuilder()
                .WithAll<NetworkPlayerState, NetworkWeaponRuntime>()
                .Build();
            state.RequireForUpdate(playerQuery);
        }

        public void OnUpdate(ref SystemState state)
        {
            var entities = playerQuery.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                if (!state.EntityManager.HasComponent<NetworkModernReloadTracker>(entity))
                    state.EntityManager.AddComponentData(entity, new NetworkModernReloadTracker());
            }

            for (var i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                var player = state.EntityManager.GetComponentData<NetworkPlayerState>(entity);
                var runtime = state.EntityManager.GetComponentData<NetworkWeaponRuntime>(entity);
                var tracker = state.EntityManager.GetComponentData<NetworkModernReloadTracker>(entity);

                if (runtime.ReloadRemaining <= 0f)
                {
                    tracker.InReload = 0;
                    tracker.Corrected = 0;
                    tracker.MagazineAtStart = 0;
                    state.EntityManager.SetComponentData(entity, tracker);
                    continue;
                }

                var magazineSize = GetMagazineSize(in player);
                if (tracker.InReload == 0)
                {
                    if (player.ReserveAmmo < magazineSize)
                    {
                        runtime.ReloadRemaining = 0f;
                        runtime.ReloadCommitRemaining = 0f;
                        runtime.ReloadCommitted = 0;
                        state.EntityManager.SetComponentData(entity, runtime);
                        continue;
                    }

                    tracker.InReload = 1;
                    tracker.Corrected = 0;
                    tracker.MagazineAtStart = player.MagazineAmmo;
                }

                if (runtime.ReloadCommitted != 0 && tracker.Corrected == 0)
                {
                    var discardedRounds = math.min((int)tracker.MagazineAtStart, player.ReserveAmmo);
                    player.ReserveAmmo = (byte)math.max(0, player.ReserveAmmo - discardedRounds);
                    tracker.Corrected = 1;
                    state.EntityManager.SetComponentData(entity, player);
                }

                state.EntityManager.SetComponentData(entity, tracker);
            }

            entities.Dispose();
        }

        private static byte GetMagazineSize(in NetworkPlayerState player)
        {
            if (player.ActiveWeapon == 1)
                return 30;
            if (player.ActiveWeapon == 2)
                return player.Team == 0 ? (byte)20 : (byte)12;
            return byte.MaxValue;
        }
    }
}
