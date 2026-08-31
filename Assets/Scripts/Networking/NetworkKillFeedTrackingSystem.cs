using PolyStrike.Gameplay;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace PolyStrike.Networking
{
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(NetworkServerMatchSystem))]
    public partial struct NetworkKillFeedTrackingSystem : ISystem
    {
        private struct PreviousPlayerState
        {
            public ushort Kills;
            public ushort Deaths;
            public uint DetonateSequence;
            public byte Alive;
        }

        private struct KillerChange
        {
            public Entity Entity;
            public NetworkPlayerState State;
            public NetworkUtilityPresentationState Utility;
            public ushort RemainingKills;
            public byte RecentDetonateType;
        }

        private struct VictimChange
        {
            public Entity Entity;
            public NetworkPlayerState State;
        }

        private NativeParallelHashMap<Entity, PreviousPlayerState> previous;
        private EntityQuery playerQuery;
        private EntityQuery infernoQuery;

        public void OnCreate(ref SystemState state)
        {
            previous = new NativeParallelHashMap<Entity, PreviousPlayerState>(32, Allocator.Persistent);
            playerQuery = SystemAPI.QueryBuilder()
                .WithAll<NetworkPlayerState, NetworkUtilityPresentationState>()
                .Build();
            infernoQuery = SystemAPI.QueryBuilder()
                .WithAll<NetworkInfernoArea>()
                .Build();
        }

        public void OnDestroy(ref SystemState state)
        {
            if (previous.IsCreated)
                previous.Dispose();
        }

        public void OnUpdate(ref SystemState state)
        {
            var players = playerQuery.ToEntityArray(Allocator.Temp);
            if (players.Length == 0)
            {
                players.Dispose();
                return;
            }

            var bombExplosion = SystemAPI.TryGetSingleton<NetworkMatchRuntime>(out var matchRuntime) &&
                                matchRuntime.LastReason == NetworkRoundEndReason.BombExploded;
            var infernos = infernoQuery.ToEntityArray(Allocator.Temp);
            var killers = new NativeList<KillerChange>(Allocator.Temp);
            var victims = new NativeList<VictimChange>(Allocator.Temp);

            for (var i = 0; i < players.Length; i++)
            {
                var entity = players[i];
                var player = state.EntityManager.GetComponentData<NetworkPlayerState>(entity);
                var utility = state.EntityManager.GetComponentData<NetworkUtilityPresentationState>(entity);
                var alive = (byte)(((player.Flags & NetworkPlayerFlags.Alive) != 0) ? 1 : 0);

                if (!previous.TryGetValue(entity, out var old))
                {
                    previous.TryAdd(entity, new PreviousPlayerState
                    {
                        Kills = player.Kills,
                        Deaths = player.Deaths,
                        DetonateSequence = utility.DetonateSequence,
                        Alive = alive
                    });
                    continue;
                }

                var killDelta = math.max(0, (int)player.Kills - old.Kills);
                var deathDelta = math.max(0, (int)player.Deaths - old.Deaths);
                var diedThisTick = old.Alive != 0 && alive == 0;

                if (diedThisTick && deathDelta == 0)
                {
                    player.Deaths = (ushort)math.min(ushort.MaxValue, player.Deaths + 1);
                    state.EntityManager.SetComponentData(entity, player);
                    deathDelta = 1;
                }

                if (killDelta > 0)
                {
                    killers.Add(new KillerChange
                    {
                        Entity = entity,
                        State = player,
                        Utility = utility,
                        RemainingKills = (ushort)math.min(ushort.MaxValue, killDelta),
                        RecentDetonateType = utility.DetonateSequence != old.DetonateSequence
                            ? utility.DetonateType
                            : byte.MaxValue
                    });
                }

                for (var death = 0; death < deathDelta; death++)
                {
                    victims.Add(new VictimChange
                    {
                        Entity = entity,
                        State = player
                    });
                }
            }

            for (var victimIndex = 0; victimIndex < victims.Length; victimIndex++)
            {
                var victim = victims[victimIndex];
                var killerIndex = FindBestKiller(ref state, in victim, killers, infernos);
                if (killerIndex >= 0)
                {
                    var killer = killers[killerIndex];
                    var weapon = ResolveWeapon(ref state, in killer, in victim, infernos);
                    NetworkKillFeedServer.Broadcast(
                        ref state,
                        in killer.State,
                        in victim.State,
                        weapon,
                        false);
                    killer.RemainingKills--;
                    killers[killerIndex] = killer;
                }
                else
                {
                    NetworkKillFeedServer.BroadcastEnvironment(
                        ref state,
                        in victim.State,
                        bombExplosion ? (byte)11 : (byte)0);
                }
            }

            for (var i = 0; i < players.Length; i++)
            {
                var entity = players[i];
                var player = state.EntityManager.GetComponentData<NetworkPlayerState>(entity);
                var utility = state.EntityManager.GetComponentData<NetworkUtilityPresentationState>(entity);
                previous[entity] = new PreviousPlayerState
                {
                    Kills = player.Kills,
                    Deaths = player.Deaths,
                    DetonateSequence = utility.DetonateSequence,
                    Alive = (byte)(((player.Flags & NetworkPlayerFlags.Alive) != 0) ? 1 : 0)
                };
            }

            victims.Dispose();
            killers.Dispose();
            infernos.Dispose();
            players.Dispose();
        }

        private static int FindBestKiller(
            ref SystemState state,
            in VictimChange victim,
            NativeList<KillerChange> killers,
            NativeArray<Entity> infernos)
        {
            var bestIndex = -1;
            var bestScore = float.MinValue;

            for (var i = 0; i < killers.Length; i++)
            {
                var killer = killers[i];
                if (killer.RemainingKills == 0 || killer.Entity == victim.Entity || killer.State.Team == victim.State.Team)
                    continue;

                var score = ScoreKiller(ref state, in killer, in victim, infernos);
                if (score <= bestScore)
                    continue;

                bestScore = score;
                bestIndex = i;
            }

            return bestIndex;
        }

        private static float ScoreKiller(
            ref SystemState state,
            in KillerChange killer,
            in VictimChange victim,
            NativeArray<Entity> infernos)
        {
            if (HasOwnedInfernoNear(ref state, killer.Entity, victim.State.Position, infernos))
                return 2000f;

            if (killer.RecentDetonateType == (byte)GrenadeType.HighExplosive)
                return 1500f - math.distance(killer.Utility.DetonatePosition, victim.State.Position);

            var toVictim = victim.State.Position - killer.State.Position;
            var distance = math.length(toVictim);
            if (distance <= 0.001f)
                return 0f;

            var yaw = math.radians(killer.State.Yaw);
            var pitch = math.radians(killer.State.Pitch);
            var cosPitch = math.cos(pitch);
            var forward = math.normalizesafe(new float3(
                math.sin(yaw) * cosPitch,
                -math.sin(pitch),
                math.cos(yaw) * cosPitch));
            var aimScore = math.dot(forward, toVictim / distance);
            return aimScore * 100f - distance * 0.02f;
        }

        private static byte ResolveWeapon(
            ref SystemState state,
            in KillerChange killer,
            in VictimChange victim,
            NativeArray<Entity> infernos)
        {
            if (HasOwnedInfernoNear(ref state, killer.Entity, victim.State.Position, infernos))
                return 10;

            if (killer.RecentDetonateType == (byte)GrenadeType.HighExplosive)
                return 6;

            return killer.State.ActiveWeapon is 1 or 2 ? killer.State.ActiveWeapon : (byte)0;
        }

        private static bool HasOwnedInfernoNear(
            ref SystemState state,
            Entity owner,
            float3 victimPosition,
            NativeArray<Entity> infernos)
        {
            const float radius = 2.85f;
            for (var i = 0; i < infernos.Length; i++)
            {
                var fire = state.EntityManager.GetComponentData<NetworkInfernoArea>(infernos[i]);
                if (fire.Owner != owner)
                    continue;

                if (math.distancesq(fire.Position.xz, victimPosition.xz) <= radius * radius)
                    return true;
            }

            return false;
        }
    }
}
