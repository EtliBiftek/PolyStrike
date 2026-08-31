using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace PolyStrike.Networking
{
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(ClientSimulationSystemGroup))]
    public partial struct ClientEnterGameSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (_, entity) in SystemAPI.Query<RefRO<NetworkId>>()
                         .WithAll<NetworkStreamConnection>()
                         .WithNone<ClientJoinRequestSent>()
                         .WithEntityAccess())
            {
                if (!SystemAPI.HasComponent<NetworkStreamInGame>(entity))
                    commandBuffer.AddComponent<NetworkStreamInGame>(entity);

                commandBuffer.AddComponent<ClientJoinRequestSent>(entity);

                var rpc = commandBuffer.CreateEntity();
                commandBuffer.AddComponent<NetworkJoinRequest>(rpc);
                commandBuffer.AddComponent(rpc, new SendRpcCommandRequest { TargetConnection = entity });
            }

            commandBuffer.Playback(state.EntityManager);
            commandBuffer.Dispose();
        }
    }
}
