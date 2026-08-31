using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace PolyStrike.Networking
{
    public struct NetworkJoinRequest : IRpcCommand
    {
        public FixedString128Bytes PlayerName;
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

    public struct NetworkVoiceRoomState : IComponentData
    {
        public FixedString64Bytes RoomId;
    }

    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct ServerNetworkPlayerSpawnSystem : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkGameSetup>();

            var roomEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(roomEntity, new NetworkVoiceRoomState
            {
                RoomId = new FixedString64Bytes($"ps-{DateTime.UtcNow.Ticks:x}")
            });
        }

        public void OnUpdate(ref SystemState state)
        {
            var setup = SystemAPI.GetSingleton<NetworkGameSetup>();
            if (setup.PlayerGhostPrefab == Entity.Null)
                return;

            var voiceRoom = SystemAPI.GetSingleton<NetworkVoiceRoomState>().RoomId;
            var terroristCount = 0;
            var counterTerroristCount = 0;
            foreach (var playerConnection in SystemAPI.Query<RefRO<NetworkPlayerConnection>>())
            {
                if (playerConnection.ValueRO.Team == 0)
                    terroristCount++;
                else
                    counterTerroristCount++;
            }

            var joinAlive = true;
            if (SystemAPI.TryGetSingleton<NetworkMatchRuntime>(out var matchRuntime) && matchRuntime.Started != 0)
            {
                joinAlive = matchRuntime.Phase == NetworkMatchPhase.FreezeTime ||
                            matchRuntime.Phase == NetworkMatchPhase.Waiting;
            }

            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (request, join, rpcEntity) in SystemAPI.Query<RefRO<ReceiveRpcCommandRequest>, RefRO<NetworkJoinRequest>>().WithEntityAccess())
            {
                var connection = request.ValueRO.SourceConnection;
                if (!SystemAPI.HasComponent<PolyStrikeConnectionAccepted>(connection) ||
                    SystemAPI.HasComponent<NetworkPlayerConnection>(connection))
                {
                    commandBuffer.DestroyEntity(rpcEntity);
                    continue;
                }

                if (join.ValueRO.PlayerName.IsEmpty)
                {
                    commandBuffer.AddComponent<NetworkStreamRequestDisconnect>(connection);
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

                var position = NetworkSandlineCollision.GetSpawn(team, slot);
                var yaw = team == 0 ? 0f : 180f;
                commandBuffer.SetComponent(player, LocalTransform.FromPositionRotation(
                    position,
                    quaternion.RotateY(math.radians(yaw))));

                var pistolMagazine = team == 0 ? (byte)20 : (byte)12;
                var pistolReserve = team == 0 ? (byte)120 : (byte)24;

                commandBuffer.SetComponent(player, new NetworkPlayerState
                {
                    PlayerName = join.ValueRO.PlayerName,
                    VoiceRoom = voiceRoom,
                    Position = position,
                    Velocity = float3.zero,
                    Yaw = yaw,
                    Pitch = 0f,
                    CrouchAmount = 0f,
                    VelocityModifier = 1f,
                    Health = joinAlive ? (ushort)100 : (ushort)0,
                    Armor = 0,
                    Money = NetworkMatchRules.StartMoney,
                    Kills = 0,
                    Deaths = 0,
                    PingMs = 0,
                    Team = team,
                    ActiveWeapon = 2,
                    MagazineAmmo = pistolMagazine,
                    ReserveAmmo = pistolReserve,
                    Flags = joinAlive
                        ? NetworkPlayerFlags.Alive | NetworkPlayerFlags.Grounded
                        : NetworkPlayerFlags.Grounded
                });
                commandBuffer.SetComponent(player, new NetworkLoadoutState
                {
                    PistolMagazine = pistolMagazine,
                    PistolReserve = pistolReserve
                });
                commandBuffer.SetComponent(player, new NetworkBombDropRuntime
                {
                    WasAlive = joinAlive ? (byte)1 : (byte)0
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
    }
}
