using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;

namespace PolyStrike.Networking
{
    public struct PolyStrikeConnectionAccepted : IComponentData
    {
    }

    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(ServerSimulationSystemGroup))]
    public partial struct ServerAdmissionSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            var acceptedCount = 0;
            foreach (var (_, entity) in SystemAPI.Query<RefRO<NetworkStreamConnection>>().WithEntityAccess())
            {
                if (SystemAPI.HasComponent<PolyStrikeConnectionAccepted>(entity))
                    acceptedCount++;
            }

            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (_, entity) in SystemAPI.Query<RefRO<NetworkStreamConnection>>().WithEntityAccess())
            {
                if (SystemAPI.HasComponent<PolyStrikeConnectionAccepted>(entity))
                {
                    if (!SystemAPI.HasComponent<NetworkStreamInGame>(entity))
                        commandBuffer.AddComponent<NetworkStreamInGame>(entity);
                    continue;
                }

                if (SystemAPI.HasComponent<NetworkStreamRequestDisconnect>(entity))
                    continue;

                if (acceptedCount >= PolyStrikeNetcodeBootstrap.MaximumPlayers)
                {
                    commandBuffer.AddComponent<NetworkStreamRequestDisconnect>(entity);
                    continue;
                }

                commandBuffer.AddComponent<PolyStrikeConnectionAccepted>(entity);
                commandBuffer.AddComponent<NetworkStreamInGame>(entity);
                acceptedCount++;
            }

            commandBuffer.Playback(state.EntityManager);
            commandBuffer.Dispose();
        }
    }
}
