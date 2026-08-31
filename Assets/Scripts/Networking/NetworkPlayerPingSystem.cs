using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace PolyStrike.Networking
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(ServerSimulationSystemGroup))]
    public partial struct NetworkPlayerPingSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (connection, snapshotAck) in
                     SystemAPI.Query<RefRO<NetworkPlayerConnection>, RefRO<NetworkSnapshotAck>>())
            {
                var playerEntity = connection.ValueRO.Player;
                if (playerEntity == Entity.Null || !state.EntityManager.Exists(playerEntity) ||
                    !state.EntityManager.HasComponent<NetworkPlayerState>(playerEntity))
                    continue;

                var player = state.EntityManager.GetComponentData<NetworkPlayerState>(playerEntity);
                player.PingMs = (ushort)math.clamp((int)math.round(snapshotAck.ValueRO.EstimatedRTT), 0, 999);
                state.EntityManager.SetComponentData(playerEntity, player);
            }
        }
    }
}
