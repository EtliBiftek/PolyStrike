using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace PolyStrike.Networking
{
    public struct NetworkJoinRequest : IRpcCommand
    {
    }

    public struct ClientJoinRequestSent : IComponentData
    {
    }

    public struct NetworkPlayerConnection : IComponentData
    {
        public Entity Player;
        public byte Team;
        public byte Slot;
    }

    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(ServerSimulationSystemGroup))]
    public partial struct ServerNetworkPlayerSpawnSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkGameSetup>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var setup = SystemAPI.GetSingleton<NetworkGameSetup>();
            if (setup.PlayerGhostPrefab == Entity.Null)
                return;

            var terroristCount = 0;
            var counterTerroristCount = 0;
            foreach (var playerConnection in SystemAPI.Query<RefRO<NetworkPlayerConnection>>())
            {
                if (playerConnection.ValueRO.Team == 0)
                    terroristCount++;
                else
                    counterTerroristCount++;
            }

            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (request, _, rpcEntity) in SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<NetworkJoinRequest>>().WithEntityAccess())
            {
                var connection = request.ValueRO.SourceConnection;
                if (!SystemAPI.HasComponent<PolyStrikeConnectionAccepted>(connection) ||
                    SystemAPI.HasComponent<NetworkPlayerConnection>(connection))
                {
                    commandBuffer.DestroyEntity(rpcEntity);
                    continue;
                }

                var useTerrorists = terroristCount < 5 && (terroristCount <= counterTerroristCount || counterTerroristCount >= 5);
                if (!useTerrorists && counterTerroristCount >= 5)
                {
                    commandBuffer.AddComponent<NetworkStreamRequestDisconnect>(connection);
                    commandBuffer.DestroyEntity(rpcEntity);
                    continue;
                }

                var team = useTerrorists ? (byte)0 : (byte)1;
                var slot = useTerrorists ? terroristCount : counterTerroristCount;
                if (useTerrorists)
                    terroristCount++;
                else
                    counterTerroristCount++;

                var player = commandBuffer.Instantiate(setup.PlayerGhostPrefab);
                var networkId = SystemAPI.GetComponent<NetworkId>(connection).Value;
                commandBuffer.SetComponent(player, new GhostOwner { NetworkId = networkId });

                var position = GetSpawn(team, slot);
                var yaw = team == 0 ? 0f : 180f;
                commandBuffer.SetComponent(player, LocalTransform.FromPositionRotation(
                    position,
                    quaternion.RotateY(math.radians(yaw))));
                commandBuffer.SetComponent(player, new NetworkPlayerState
                {
                    Position = position,
                    Velocity = float3.zero,
                    Yaw = yaw,
                    Pitch = 0f,
                    CrouchAmount = 0f,
                    VelocityModifier = 1f,
                    Health = 100,
                    Armor = 0,
                    Money = 800,
                    Team = team,
                    ActiveWeapon = 2,
                    MagazineAmmo = team == 0 ? (byte)20 : (byte)12,
                    ReserveAmmo = team == 0 ? (byte)120 : (byte)24,
                    Flags = NetworkPlayerFlags.Alive | NetworkPlayerFlags.Grounded
                });

                commandBuffer.AppendToBuffer(connection, new LinkedEntityGroup { Value = player });
                commandBuffer.AddComponent(connection, new NetworkPlayerConnection
                {
                    Player = player,
                    Team = team,
                    Slot = (byte)slot
                });

                if (!SystemAPI.HasComponent<NetworkStreamInGame>(connection))
                    commandBuffer.AddComponent<NetworkStreamInGame>(connection);

                commandBuffer.DestroyEntity(rpcEntity);
            }

            commandBuffer.Playback(state.EntityManager);
            commandBuffer.Dispose();
        }

        private static float3 GetSpawn(byte team, int slot)
        {
            var x = -1.6f + math.clamp(slot, 0, 4) * 0.8f;
            return new float3(x, NetworkSandlineCollision.GroundY, team == 0 ? -24f : 24f);
        }
    }
}
