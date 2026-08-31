using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace PolyStrike.Networking
{
    [GhostComponent]
    public struct NetworkBombPresentationState : IComponentData
    {
        [GhostField] public uint DropSequence;
        [GhostField(Quantization = 1000)] public float3 DropPosition;
        [GhostField] public uint PickupSequence;
        [GhostField(Quantization = 1000)] public float3 PickupPosition;
    }

    public struct NetworkBombDropRuntime : IComponentData
    {
        public byte WasAlive;
    }

    public struct NetworkDroppedBomb : IComponentData
    {
        public float3 Position;
        public float PickupDelay;
    }

    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(NetworkUtilitySimulationSystem))]
    [UpdateBefore(typeof(NetworkServerMatchSystem))]
    public partial struct NetworkBombDropSystem : ISystem
    {
        private const float PickupRadius = 1.10f;
        private const float DropPickupDelay = 0.25f;

        private EntityQuery playerQuery;
        private EntityQuery droppedBombQuery;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkMatchRuntime>();
            playerQuery = SystemAPI.QueryBuilder()
                .WithAll<NetworkPlayerState, NetworkPlayerInput, NetworkLoadoutState, NetworkBombDropRuntime, NetworkBombPresentationState>()
                .Build();
            droppedBombQuery = SystemAPI.QueryBuilder().WithAll<NetworkDroppedBomb>().Build();
        }

        public void OnUpdate(ref SystemState state)
        {
            var match = SystemAPI.GetSingleton<NetworkMatchRuntime>();
            if (match.Phase != NetworkMatchPhase.Live && match.Phase != NetworkMatchPhase.PostPlant)
            {
                ClearDroppedBombs(ref state);
                SyncAliveState(ref state);
                return;
            }

            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);
            foreach (var (player, input, loadout, runtime, presentation) in
                     SystemAPI.Query<
                         RefRW<NetworkPlayerState>,
                         RefRO<NetworkPlayerInput>,
                         RefRO<NetworkLoadoutState>,
                         RefRW<NetworkBombDropRuntime>,
                         RefRW<NetworkBombPresentationState>>())
            {
                ref var playerState = ref player.ValueRW;
                ref var dropRuntime = ref runtime.ValueRW;
                var alive = (playerState.Flags & NetworkPlayerFlags.Alive) != 0;
                var hasBomb = (playerState.Flags & NetworkPlayerFlags.HasBomb) != 0;

                var diedThisTick = dropRuntime.WasAlive != 0 && !alive;
                var manuallyDropped = alive && hasBomb && playerState.ActiveWeapon == 5 && input.ValueRO.Drop.IsSet;
                if (hasBomb && (diedThisTick || manuallyDropped))
                {
                    var dropPosition = playerState.Position + new float3(0f, 0.12f, 0f);
                    var dropped = commandBuffer.CreateEntity();
                    commandBuffer.AddComponent(dropped, new NetworkDroppedBomb
                    {
                        Position = dropPosition,
                        PickupDelay = DropPickupDelay
                    });

                    presentation.ValueRW.DropSequence++;
                    presentation.ValueRW.DropPosition = dropPosition;
                    playerState.Flags &= unchecked((byte)~NetworkPlayerFlags.HasBomb);

                    if (playerState.ActiveWeapon == 5)
                    {
                        playerState.ActiveWeapon = loadout.ValueRO.PrimaryOwned != 0 ? (byte)1 : (byte)2;
                        NetworkLoadoutSwitchSystem.LoadActiveAmmo(ref playerState, in loadout.ValueRO);
                    }
                }

                dropRuntime.WasAlive = alive ? (byte)1 : (byte)0;
            }

            commandBuffer.Playback(state.EntityManager);
            commandBuffer.Dispose();
            ResolvePickups(ref state, SystemAPI.Time.DeltaTime);
        }

        private void ResolvePickups(ref SystemState state, float deltaTime)
        {
            var bombs = droppedBombQuery.ToEntityArray(Allocator.Temp);
            var players = playerQuery.ToEntityArray(Allocator.Temp);

            for (var bombIndex = 0; bombIndex < bombs.Length; bombIndex++)
            {
                var bombEntity = bombs[bombIndex];
                if (!state.EntityManager.Exists(bombEntity))
                    continue;

                var bomb = state.EntityManager.GetComponentData<NetworkDroppedBomb>(bombEntity);
                if (bomb.PickupDelay > 0f)
                {
                    bomb.PickupDelay = math.max(0f, bomb.PickupDelay - deltaTime);
                    state.EntityManager.SetComponentData(bombEntity, bomb);
                    continue;
                }

                var bestPlayer = Entity.Null;
                var bestDistanceSq = PickupRadius * PickupRadius;

                for (var playerIndex = 0; playerIndex < players.Length; playerIndex++)
                {
                    var playerEntity = players[playerIndex];
                    var player = state.EntityManager.GetComponentData<NetworkPlayerState>(playerEntity);
                    if (player.Team != 0 ||
                        (player.Flags & NetworkPlayerFlags.Alive) == 0 ||
                        (player.Flags & NetworkPlayerFlags.HasBomb) != 0)
                        continue;

                    var distanceSq = math.distancesq(player.Position, bomb.Position);
                    if (distanceSq > bestDistanceSq)
                        continue;

                    bestDistanceSq = distanceSq;
                    bestPlayer = playerEntity;
                }

                if (bestPlayer == Entity.Null)
                    continue;

                var picker = state.EntityManager.GetComponentData<NetworkPlayerState>(bestPlayer);
                picker.Flags |= NetworkPlayerFlags.HasBomb;
                state.EntityManager.SetComponentData(bestPlayer, picker);

                var presentation = state.EntityManager.GetComponentData<NetworkBombPresentationState>(bestPlayer);
                presentation.PickupSequence++;
                presentation.PickupPosition = bomb.Position;
                state.EntityManager.SetComponentData(bestPlayer, presentation);

                state.EntityManager.DestroyEntity(bombEntity);
            }

            players.Dispose();
            bombs.Dispose();
        }

        private void ClearDroppedBombs(ref SystemState state)
        {
            var bombs = droppedBombQuery.ToEntityArray(Allocator.Temp);
            if (bombs.Length > 0)
                state.EntityManager.DestroyEntity(bombs);
            bombs.Dispose();
        }

        private void SyncAliveState(ref SystemState state)
        {
            var players = playerQuery.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < players.Length; i++)
            {
                var player = state.EntityManager.GetComponentData<NetworkPlayerState>(players[i]);
                var runtime = state.EntityManager.GetComponentData<NetworkBombDropRuntime>(players[i]);
                runtime.WasAlive = (player.Flags & NetworkPlayerFlags.Alive) != 0 ? (byte)1 : (byte)0;
                state.EntityManager.SetComponentData(players[i], runtime);
            }
            players.Dispose();
        }
    }
}
