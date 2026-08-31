using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace PolyStrike.Networking
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(NetworkServerCombatSystem))]
    public partial struct NetworkServerMatchSystem : ISystem
    {
        private EntityQuery playerQuery;

        public void OnCreate(ref SystemState state)
        {
            playerQuery = SystemAPI.QueryBuilder()
                .WithAll<NetworkPlayerState, NetworkPlayerInput, NetworkInteractionState, NetworkMatchSnapshot, NetworkLoadoutState>()
                .Build();

            var runtimeEntity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponentData(runtimeEntity, new NetworkMatchRuntime
            {
                Phase = NetworkMatchPhase.Waiting,
                TerroristLossLevel = NetworkMatchRules.StartingLossLevel,
                CounterTerroristLossLevel = NetworkMatchRules.StartingLossLevel,
                LastWinner = byte.MaxValue
            });
        }

        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            var players = playerQuery.ToEntityArray(Allocator.Temp);
            var runtime = SystemAPI.GetSingleton<NetworkMatchRuntime>();

            if (players.Length < 2)
            {
                runtime.Started = 0;
                runtime.Phase = NetworkMatchPhase.Waiting;
                runtime.PhaseTimeRemaining = 0f;
                runtime.BuyTimeRemaining = 0f;
                runtime.BombPlanted = 0;
                SyncSnapshots(ref state, players, in runtime);
                SystemAPI.SetSingleton(runtime);
                players.Dispose();
                return;
            }

            if (runtime.Started == 0)
            {
                runtime.Started = 1;
                BeginFreshMatch(ref state, players, ref runtime);
            }

            if (runtime.BuyTimeRemaining > 0f &&
                (runtime.Phase == NetworkMatchPhase.FreezeTime || runtime.Phase == NetworkMatchPhase.Live))
            {
                runtime.BuyTimeRemaining = math.max(0f, runtime.BuyTimeRemaining - deltaTime);
            }

            switch (runtime.Phase)
            {
                case NetworkMatchPhase.FreezeTime:
                    runtime.PhaseTimeRemaining = math.max(0f, runtime.PhaseTimeRemaining - deltaTime);
                    if (runtime.PhaseTimeRemaining <= 0f)
                    {
                        runtime.Phase = NetworkMatchPhase.Live;
                        runtime.PhaseTimeRemaining = NetworkMatchRules.RoundTime;
                    }
                    break;

                case NetworkMatchPhase.Live:
                    runtime.PhaseTimeRemaining = math.max(0f, runtime.PhaseTimeRemaining - deltaTime);
                    UpdatePlanting(ref state, players, ref runtime, deltaTime);
                    if (runtime.Phase == NetworkMatchPhase.Live)
                        ResolveLiveRound(ref state, players, ref runtime);
                    break;

                case NetworkMatchPhase.PostPlant:
                    runtime.BombTimeRemaining = math.max(0f, runtime.BombTimeRemaining - deltaTime);
                    UpdateDefusing(ref state, players, ref runtime, deltaTime);
                    if (runtime.Phase != NetworkMatchPhase.PostPlant)
                        break;

                    if (AliveCount(ref state, players, 1) == 0)
                    {
                        EndRound(ref state, players, ref runtime, 0, NetworkRoundEndReason.Elimination);
                    }
                    else if (runtime.BombTimeRemaining <= 0f)
                    {
                        ApplyBombDamage(ref state, players, in runtime);
                        EndRound(ref state, players, ref runtime, 0, NetworkRoundEndReason.BombExploded);
                    }
                    break;

                case NetworkMatchPhase.RoundEnd:
                    runtime.PhaseTimeRemaining = math.max(0f, runtime.PhaseTimeRemaining - deltaTime);
                    if (runtime.PhaseTimeRemaining <= 0f)
                        AdvanceAfterRound(ref state, players, ref runtime);
                    break;

                case NetworkMatchPhase.HalfTime:
                    runtime.PhaseTimeRemaining = math.max(0f, runtime.PhaseTimeRemaining - deltaTime);
                    if (runtime.PhaseTimeRemaining <= 0f)
                        StartFreezeTime(ref state, players, ref runtime);
                    break;
            }

            SyncSnapshots(ref state, players, in runtime);
            SystemAPI.SetSingleton(runtime);
            players.Dispose();
        }

        private static void BeginFreshMatch(
            ref SystemState state,
            NativeArray<Entity> players,
            ref NetworkMatchRuntime runtime)
        {
            runtime.TerroristScore = 0;
            runtime.CounterTerroristScore = 0;
            runtime.RoundsPlayed = 0;
            runtime.TerroristLossLevel = NetworkMatchRules.StartingLossLevel;
            runtime.CounterTerroristLossLevel = NetworkMatchRules.StartingLossLevel;
            runtime.LastWinner = byte.MaxValue;
            runtime.LastReason = NetworkRoundEndReason.None;

            for (var i = 0; i < players.Length; i++)
            {
                var entity = players[i];
                var player = state.EntityManager.GetComponentData<NetworkPlayerState>(entity);
                player.Money = (ushort)math.clamp(NetworkMatchRules.StartMoney, 0, ushort.MaxValue);
                ResetLoadoutForNewHalf(ref state, entity, ref player);
                state.EntityManager.SetComponentData(entity, player);
            }

            StartFreezeTime(ref state, players, ref runtime);
        }

        private static void StartFreezeTime(
            ref SystemState state,
            NativeArray<Entity> players,
            ref NetworkMatchRuntime runtime)
        {
            runtime.Phase = NetworkMatchPhase.FreezeTime;
            runtime.PhaseTimeRemaining = NetworkMatchRules.FreezeTime;
            runtime.BuyTimeRemaining = NetworkMatchRules.BuyTime;
            runtime.BombTimeRemaining = 0f;
            runtime.BombPlanted = 0;
            runtime.BombWasPlanted = 0;
            runtime.BombSite = byte.MaxValue;
            runtime.BombPosition = float3.zero;
            runtime.LastWinner = byte.MaxValue;
            runtime.LastReason = NetworkRoundEndReason.None;

            for (var i = 0; i < players.Length; i++)
            {
                var entity = players[i];
                var player = state.EntityManager.GetComponentData<NetworkPlayerState>(entity);
                var wasAlive = (player.Flags & NetworkPlayerFlags.Alive) != 0;
                var loadout = state.EntityManager.GetComponentData<NetworkLoadoutState>(entity);

                if (!wasAlive)
                {
                    player.Armor = 0;
                    player.Flags &= unchecked((byte)~(NetworkPlayerFlags.Helmet | NetworkPlayerFlags.DefuseKit));
                    loadout.PrimaryOwned = 0;
                    loadout.PrimaryMagazine = 0;
                    loadout.PrimaryReserve = 0;
                    loadout.HeGrenades = 0;
                    loadout.Flashbangs = 0;
                    loadout.SmokeGrenades = 0;
                    loadout.FireGrenades = 0;
                    RefillPistol(player.Team, ref loadout);
                }

                player.Health = 100;
                player.Velocity = float3.zero;
                player.VelocityModifier = 1f;
                player.CrouchAmount = 0f;
                player.Flags |= NetworkPlayerFlags.Alive | NetworkPlayerFlags.Grounded;
                player.Flags &= unchecked((byte)~(NetworkPlayerFlags.Planting | NetworkPlayerFlags.Defusing | NetworkPlayerFlags.HasBomb));
                player.ActiveWeapon = loadout.PrimaryOwned != 0 ? (byte)1 : (byte)2;
                SyncActiveAmmo(ref player, in loadout);

                ResetInteraction(ref state, entity);
                ResetWeaponRuntime(ref state, entity);
                MoveToTeamSpawn(ref state, entity, ref player);

                state.EntityManager.SetComponentData(entity, player);
                state.EntityManager.SetComponentData(entity, loadout);
            }

            AssignBombCarrier(ref state, players, runtime.RoundsPlayed);
        }

        private static void ResolveLiveRound(
            ref SystemState state,
            NativeArray<Entity> players,
            ref NetworkMatchRuntime runtime)
        {
            if (AliveCount(ref state, players, 1) == 0)
            {
                EndRound(ref state, players, ref runtime, 0, NetworkRoundEndReason.Elimination);
                return;
            }

            if (AliveCount(ref state, players, 0) == 0)
            {
                EndRound(ref state, players, ref runtime, 1, NetworkRoundEndReason.Elimination);
                return;
            }

            if (runtime.PhaseTimeRemaining <= 0f)
                EndRound(ref state, players, ref runtime, 1, NetworkRoundEndReason.TimeExpired);
        }

        private static void UpdatePlanting(
            ref SystemState state,
            NativeArray<Entity> players,
            ref NetworkMatchRuntime runtime,
            float deltaTime)
        {
            for (var i = 0; i < players.Length; i++)
            {
                var entity = players[i];
                var player = state.EntityManager.GetComponentData<NetworkPlayerState>(entity);
                if (player.Team != 0 ||
                    (player.Flags & (NetworkPlayerFlags.Alive | NetworkPlayerFlags.HasBomb)) !=
                    (NetworkPlayerFlags.Alive | NetworkPlayerFlags.HasBomb))
                    continue;

                var input = state.EntityManager.GetComponentData<NetworkPlayerInput>(entity);
                var interaction = state.EntityManager.GetComponentData<NetworkInteractionState>(entity);
                var site = NetworkSandlineCollision.FindBombSite(player.Position);
                var grounded = (player.Flags & NetworkPlayerFlags.Grounded) != 0;
                var holdingPlant = player.ActiveWeapon == 5 && (input.FireHeld != 0 || input.UseHeld != 0);

                if (site == byte.MaxValue || !grounded || !holdingPlant)
                {
                    interaction.PlantProgress = 0f;
                    player.Flags &= unchecked((byte)~NetworkPlayerFlags.Planting);
                    state.EntityManager.SetComponentData(entity, interaction);
                    state.EntityManager.SetComponentData(entity, player);
                    continue;
                }

                player.Flags |= NetworkPlayerFlags.Planting;
                player.Velocity = float3.zero;
                interaction.PlantProgress += deltaTime;

                if (interaction.PlantProgress >= NetworkMatchRules.PlantTime)
                {
                    player.Flags &= unchecked((byte)~(NetworkPlayerFlags.Planting | NetworkPlayerFlags.HasBomb));
                    player.Money = (ushort)math.min(NetworkMatchRules.MaxMoney, player.Money + NetworkMatchRules.BombPlantPlayerReward);
                    player.ActiveWeapon = 2;
                    var loadout = state.EntityManager.GetComponentData<NetworkLoadoutState>(entity);
                    SyncActiveAmmo(ref player, in loadout);
                    interaction.PlantProgress = 0f;

                    runtime.Phase = NetworkMatchPhase.PostPlant;
                    runtime.PhaseTimeRemaining = 0f;
                    runtime.BombTimeRemaining = NetworkMatchRules.BombTimer;
                    runtime.BombPosition = player.Position;
                    runtime.BombSite = site;
                    runtime.BombPlanted = 1;
                    runtime.BombWasPlanted = 1;
                }

                state.EntityManager.SetComponentData(entity, interaction);
                state.EntityManager.SetComponentData(entity, player);
            }
        }

        private static void UpdateDefusing(
            ref SystemState state,
            NativeArray<Entity> players,
            ref NetworkMatchRuntime runtime,
            float deltaTime)
        {
            for (var i = 0; i < players.Length; i++)
            {
                var entity = players[i];
                var player = state.EntityManager.GetComponentData<NetworkPlayerState>(entity);
                if (player.Team != 1 || (player.Flags & NetworkPlayerFlags.Alive) == 0)
                    continue;

                var input = state.EntityManager.GetComponentData<NetworkPlayerInput>(entity);
                var interaction = state.EntityManager.GetComponentData<NetworkInteractionState>(entity);
                var closeEnough = math.distance(player.Position, runtime.BombPosition) <= 1.25f;

                if (!closeEnough || input.UseHeld == 0)
                {
                    interaction.DefuseProgress = 0f;
                    player.Flags &= unchecked((byte)~NetworkPlayerFlags.Defusing);
                    state.EntityManager.SetComponentData(entity, interaction);
                    state.EntityManager.SetComponentData(entity, player);
                    continue;
                }

                player.Flags |= NetworkPlayerFlags.Defusing;
                player.Velocity = float3.zero;
                interaction.DefuseProgress += deltaTime;
                var required = (player.Flags & NetworkPlayerFlags.DefuseKit) != 0
                    ? NetworkMatchRules.DefuseKitTime
                    : NetworkMatchRules.DefuseTime;

                if (interaction.DefuseProgress >= required)
                {
                    player.Money = (ushort)math.min(NetworkMatchRules.MaxMoney, player.Money + NetworkMatchRules.BombDefusePlayerReward);
                    interaction.DefuseProgress = 0f;
                    state.EntityManager.SetComponentData(entity, interaction);
                    state.EntityManager.SetComponentData(entity, player);
                    EndRound(ref state, players, ref runtime, 1, NetworkRoundEndReason.BombDefused);
                    return;
                }

                state.EntityManager.SetComponentData(entity, interaction);
                state.EntityManager.SetComponentData(entity, player);
            }
        }

        private static void EndRound(
            ref SystemState state,
            NativeArray<Entity> players,
            ref NetworkMatchRuntime runtime,
            byte winner,
            NetworkRoundEndReason reason)
        {
            if (runtime.Phase == NetworkMatchPhase.RoundEnd || runtime.Phase == NetworkMatchPhase.MatchEnd)
                return;

            runtime.LastWinner = winner;
            runtime.LastReason = reason;
            runtime.BombPlanted = 0;
            runtime.BombTimeRemaining = 0f;

            if (winner == 0)
                runtime.TerroristScore++;
            else
                runtime.CounterTerroristScore++;

            var loser = (byte)(winner == 0 ? 1 : 0);
            var winnerReward = reason == NetworkRoundEndReason.BombExploded || reason == NetworkRoundEndReason.BombDefused
                ? NetworkMatchRules.ObjectiveWinReward
                : NetworkMatchRules.StandardWinReward;
            var loserLevel = loser == 0 ? runtime.TerroristLossLevel : runtime.CounterTerroristLossLevel;
            var loserReward = NetworkMatchRules.LossReward(loserLevel);

            for (var i = 0; i < players.Length; i++)
            {
                var entity = players[i];
                var player = state.EntityManager.GetComponentData<NetworkPlayerState>(entity);
                var reward = player.Team == winner ? winnerReward : loserReward;
                if (reason == NetworkRoundEndReason.BombDefused && runtime.BombWasPlanted != 0 && player.Team == 0)
                    reward += NetworkMatchRules.PlantedButDefusedTeamReward;

                player.Money = (ushort)math.min(NetworkMatchRules.MaxMoney, player.Money + reward);
                player.Flags &= unchecked((byte)~(NetworkPlayerFlags.Planting | NetworkPlayerFlags.Defusing | NetworkPlayerFlags.HasBomb));
                state.EntityManager.SetComponentData(entity, player);
                ResetInteraction(ref state, entity);
            }

            if (winner == 0)
            {
                runtime.TerroristLossLevel = (byte)math.max(0, runtime.TerroristLossLevel - 1);
                runtime.CounterTerroristLossLevel = (byte)math.min(NetworkMatchRules.MaximumLossLevel, runtime.CounterTerroristLossLevel + 1);
            }
            else
            {
                runtime.CounterTerroristLossLevel = (byte)math.max(0, runtime.CounterTerroristLossLevel - 1);
                runtime.TerroristLossLevel = (byte)math.min(NetworkMatchRules.MaximumLossLevel, runtime.TerroristLossLevel + 1);
            }

            runtime.RoundsPlayed++;
            runtime.Phase = NetworkMatchPhase.RoundEnd;
            runtime.PhaseTimeRemaining = NetworkMatchRules.RoundRestartDelay;
        }

        private static void AdvanceAfterRound(
            ref SystemState state,
            NativeArray<Entity> players,
            ref NetworkMatchRuntime runtime)
        {
            if (runtime.TerroristScore >= NetworkMatchRules.RoundsToWin ||
                runtime.CounterTerroristScore >= NetworkMatchRules.RoundsToWin ||
                runtime.RoundsPlayed >= NetworkMatchRules.RegulationRounds)
            {
                runtime.Phase = NetworkMatchPhase.MatchEnd;
                runtime.PhaseTimeRemaining = 0f;
                return;
            }

            if (runtime.RoundsPlayed == NetworkMatchRules.HalfRounds)
            {
                SwapSidesForHalfTime(ref state, players, ref runtime);
                runtime.Phase = NetworkMatchPhase.HalfTime;
                runtime.PhaseTimeRemaining = NetworkMatchRules.HalfTimeDuration;
                return;
            }

            StartFreezeTime(ref state, players, ref runtime);
        }

        private static void SwapSidesForHalfTime(
            ref SystemState state,
            NativeArray<Entity> players,
            ref NetworkMatchRuntime runtime)
        {
            runtime.TerroristLossLevel = NetworkMatchRules.StartingLossLevel;
            runtime.CounterTerroristLossLevel = NetworkMatchRules.StartingLossLevel;

            for (var i = 0; i < players.Length; i++)
            {
                var entity = players[i];
                var player = state.EntityManager.GetComponentData<NetworkPlayerState>(entity);
                player.Team = (byte)(player.Team == 0 ? 1 : 0);
                player.Money = (ushort)math.clamp(NetworkMatchRules.StartMoney, 0, ushort.MaxValue);
                ResetLoadoutForNewHalf(ref state, entity, ref player);
                state.EntityManager.SetComponentData(entity, player);
            }

            using var connectionQuery = state.EntityManager.CreateEntityQuery(ComponentType.ReadWrite<NetworkPlayerConnection>());
            using var connectionEntities = connectionQuery.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < connectionEntities.Length; i++)
            {
                var entity = connectionEntities[i];
                var connection = state.EntityManager.GetComponentData<NetworkPlayerConnection>(entity);
                connection.Team = (byte)(connection.Team == 0 ? 1 : 0);
                state.EntityManager.SetComponentData(entity, connection);
            }
        }

        private static void ResetLoadoutForNewHalf(ref SystemState state, Entity entity, ref NetworkPlayerState player)
        {
            player.Health = 100;
            player.Armor = 0;
            player.Velocity = float3.zero;
            player.VelocityModifier = 1f;
            player.CrouchAmount = 0f;
            player.ActiveWeapon = 2;
            player.Flags = NetworkPlayerFlags.Alive | NetworkPlayerFlags.Grounded;

            var loadout = state.EntityManager.GetComponentData<NetworkLoadoutState>(entity);
            loadout.PrimaryOwned = 0;
            loadout.PrimaryMagazine = 0;
            loadout.PrimaryReserve = 0;
            loadout.HeGrenades = 0;
            loadout.Flashbangs = 0;
            loadout.SmokeGrenades = 0;
            loadout.FireGrenades = 0;
            RefillPistol(player.Team, ref loadout);
            SyncActiveAmmo(ref player, in loadout);
            state.EntityManager.SetComponentData(entity, loadout);
            ResetInteraction(ref state, entity);
            ResetWeaponRuntime(ref state, entity);
        }

        private static void AssignBombCarrier(ref SystemState state, NativeArray<Entity> players, int roundIndex)
        {
            var terroristCount = 0;
            for (var i = 0; i < players.Length; i++)
            {
                var player = state.EntityManager.GetComponentData<NetworkPlayerState>(players[i]);
                if (player.Team == 0)
                    terroristCount++;
            }

            if (terroristCount == 0)
                return;

            var wanted = roundIndex % terroristCount;
            var seen = 0;
            for (var i = 0; i < players.Length; i++)
            {
                var entity = players[i];
                var player = state.EntityManager.GetComponentData<NetworkPlayerState>(entity);
                player.Flags &= unchecked((byte)~NetworkPlayerFlags.HasBomb);
                if (player.Team == 0 && seen++ == wanted)
                    player.Flags |= NetworkPlayerFlags.HasBomb;
                state.EntityManager.SetComponentData(entity, player);
            }
        }

        private static void MoveToTeamSpawn(ref SystemState state, Entity entity, ref NetworkPlayerState player)
        {
            var slot = FindSlotForPlayer(ref state, entity);
            player.Position = NetworkSandlineCollision.GetSpawn(player.Team, slot);
            player.Yaw = player.Team == 0 ? 0f : 180f;
            player.Pitch = 0f;

            if (state.EntityManager.HasComponent<LocalTransform>(entity))
            {
                var transform = state.EntityManager.GetComponentData<LocalTransform>(entity);
                transform.Position = player.Position;
                transform.Rotation = quaternion.RotateY(math.radians(player.Yaw));
                state.EntityManager.SetComponentData(entity, transform);
            }
        }

        private static int FindSlotForPlayer(ref SystemState state, Entity playerEntity)
        {
            using var connectionQuery = state.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkPlayerConnection>());
            using var connectionEntities = connectionQuery.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < connectionEntities.Length; i++)
            {
                var connection = state.EntityManager.GetComponentData<NetworkPlayerConnection>(connectionEntities[i]);
                if (connection.Player == playerEntity)
                    return connection.Slot;
            }
            return 0;
        }

        private static int AliveCount(ref SystemState state, NativeArray<Entity> players, byte team)
        {
            var count = 0;
            for (var i = 0; i < players.Length; i++)
            {
                var player = state.EntityManager.GetComponentData<NetworkPlayerState>(players[i]);
                if (player.Team == team && (player.Flags & NetworkPlayerFlags.Alive) != 0)
                    count++;
            }
            return count;
        }

        private static void ApplyBombDamage(ref SystemState state, NativeArray<Entity> players, in NetworkMatchRuntime runtime)
        {
            for (var i = 0; i < players.Length; i++)
            {
                var entity = players[i];
                var player = state.EntityManager.GetComponentData<NetworkPlayerState>(entity);
                if ((player.Flags & NetworkPlayerFlags.Alive) == 0)
                    continue;

                var toPlayer = player.Position - runtime.BombPosition;
                var distance = math.length(toPlayer);
                if (distance > 22f)
                    continue;

                var damage = 500f * math.exp(-(distance * distance) / 72f);
                if (distance > 0.05f && NetworkSandlineCollision.TryRaycast(
                        runtime.BombPosition + new float3(0f, 0.2f, 0f),
                        toPlayer / distance,
                        distance,
                        out var wall) && wall.EntryDistance < distance)
                {
                    damage *= wall.Surface == NetworkSandlineCollision.Material.Wood ? 0.55f : 0.30f;
                }

                var dealt = math.max(0, (int)math.floor(damage));
                player.Health = (ushort)math.max(0, (int)player.Health - dealt);
                if (player.Health == 0)
                {
                    player.Flags &= unchecked((byte)~NetworkPlayerFlags.Alive);
                    player.Velocity = float3.zero;
                }
                state.EntityManager.SetComponentData(entity, player);
            }
        }

        private static void SyncSnapshots(
            ref SystemState state,
            NativeArray<Entity> players,
            in NetworkMatchRuntime runtime)
        {
            for (var i = 0; i < players.Length; i++)
            {
                var entity = players[i];
                var interaction = state.EntityManager.GetComponentData<NetworkInteractionState>(entity);
                var player = state.EntityManager.GetComponentData<NetworkPlayerState>(entity);
                var progress = 0f;
                if ((player.Flags & NetworkPlayerFlags.Planting) != 0)
                    progress = math.saturate(interaction.PlantProgress / NetworkMatchRules.PlantTime);
                else if ((player.Flags & NetworkPlayerFlags.Defusing) != 0)
                {
                    var duration = (player.Flags & NetworkPlayerFlags.DefuseKit) != 0
                        ? NetworkMatchRules.DefuseKitTime
                        : NetworkMatchRules.DefuseTime;
                    progress = math.saturate(interaction.DefuseProgress / duration);
                }

                state.EntityManager.SetComponentData(entity, new NetworkMatchSnapshot
                {
                    Phase = runtime.Phase,
                    RoundNumber = (byte)math.min(NetworkMatchRules.RegulationRounds, runtime.RoundsPlayed + 1),
                    TerroristScore = runtime.TerroristScore,
                    CounterTerroristScore = runtime.CounterTerroristScore,
                    LastWinner = runtime.LastWinner,
                    LastReason = runtime.LastReason,
                    BombSite = runtime.BombSite,
                    BombPlanted = runtime.BombPlanted,
                    PhaseTimeRemaining = runtime.PhaseTimeRemaining,
                    BuyTimeRemaining = runtime.BuyTimeRemaining,
                    BombTimeRemaining = runtime.BombTimeRemaining,
                    BombPosition = runtime.BombPosition,
                    InteractionProgress = progress
                });
            }
        }

        private static void ResetInteraction(ref SystemState state, Entity entity)
        {
            state.EntityManager.SetComponentData(entity, new NetworkInteractionState());
        }

        private static void ResetWeaponRuntime(ref SystemState state, Entity entity)
        {
            state.EntityManager.SetComponentData(entity, new NetworkWeaponRuntime());
            state.EntityManager.SetComponentData(entity, new NetworkTagState());
        }

        private static void RefillPistol(byte team, ref NetworkLoadoutState loadout)
        {
            loadout.PistolMagazine = team == 0 ? (byte)20 : (byte)12;
            loadout.PistolReserve = team == 0 ? (byte)120 : (byte)24;
        }

        private static void SyncActiveAmmo(ref NetworkPlayerState player, in NetworkLoadoutState loadout)
        {
            if (player.ActiveWeapon == 1 && loadout.PrimaryOwned != 0)
            {
                player.MagazineAmmo = loadout.PrimaryMagazine;
                player.ReserveAmmo = loadout.PrimaryReserve;
            }
            else
            {
                player.MagazineAmmo = loadout.PistolMagazine;
                player.ReserveAmmo = loadout.PistolReserve;
            }
        }
    }
}
