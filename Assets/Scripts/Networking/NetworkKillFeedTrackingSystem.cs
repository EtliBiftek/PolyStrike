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
            public uint TotalShots;
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
            public byte RecentShot;
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
                .WithAll<NetworkPlayerState, NetworkUtilityPresentationState, NetworkWeaponRuntime>()
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
                var weaponRuntime = state.EntityManager.GetComponentData<NetworkWeaponRuntime>(entity);
                var alive = (byte)(((player.Flags & NetworkPlayerFlags.Alive) != 0) ? 1 : 0);

                if (!previous.TryGetValue(entity, out var old))
                {
                    previous.TryAdd(entity, new PreviousPlayerState
                    {
                        Kills = player.Kills,
                        Deaths = player.Deaths,
                        TotalShots = weaponRuntime.TotalShots,
                        DetonateSequence = utility.DetonateSequence,
                        Alive = alive
                    });
                    continue;
                }

                var killDelta = math.max(0, (int)player.Kills - old.Kills);
                var deathDelta = math.max(0, (int)player.Deaths - old.Deaths);
                var diedThisTick = old.Alive != 0 && alive == 0;
                var recentShot = weaponRuntime.TotalShots != old.TotalShots;
                var recentDetonate = utility.DetonateSequence != old.DetonateSequence;

                if (diedThisTick && deathDelta == 0)
                {
                    player.Deaths = (ushort)math.min(ushort.MaxValue, player.Deaths + 1);
                    state.EntityManager.SetComponentData(entity, player);
                    deathDelta = 1;
                }

                if (killDelta > 0 || recentShot || recentDetonate)
                {
                    killers.Add(new KillerChange
                    {
                        Entity = entity,
                        State = player,
                        Utility = utility,
                        RemainingKills = (ushort)math.min(ushort.MaxValue, killDelta),
                        RecentDetonateType = recentDetonate ? utility.DetonateType : byte.MaxValue,
                        RecentShot = recentShot ? (byte)1 : (byte)0
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

            AddInfernoOwners(ref state, players, infernos, ref killers);

            for (var victimIndex = 0; victimIndex < victims.Length; victimIndex++)
            {
                var victim = victims[victimIndex];
                var killerIndex = FindBestKiller(ref state, in victim, killers, infernos);
                if (killerIndex >= 0)
                {
                    var killer = killers[killerIndex];
                    var weapon = ResolveWeapon(ref state, in killer, in victim, infernos);
                    var headshot = weapon is 1 or 2 && killer.RecentShot != 0 &&
                                   IsLikelyHeadshot(in killer.State, in victim.State);
                    NetworkKillFeedServer.Broadcast(
                        ref state,
                        in killer.State,
                        in victim.State,
                        weapon,
                        headshot);

                    if (killer.RemainingKills > 0)
                        killer.RemainingKills--;
                    if (weapon is 1 or 2)
                        killer.RecentShot = 0;
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
                var weaponRuntime = state.EntityManager.GetComponentData<NetworkWeaponRuntime>(entity);
                previous[entity] = new PreviousPlayerState
                {
                    Kills = player.Kills,
                    Deaths = player.Deaths,
                    TotalShots = weaponRuntime.TotalShots,
                    DetonateSequence = utility.DetonateSequence,
                    Alive = (byte)(((player.Flags & NetworkPlayerFlags.Alive) != 0) ? 1 : 0)
                };
            }

            victims.Dispose();
            killers.Dispose();
            infernos.Dispose();
            players.Dispose();
        }

        private static void AddInfernoOwners(
            ref SystemState state,
            NativeArray<Entity> players,
            NativeArray<Entity> infernos,
            ref NativeList<KillerChange> killers)
        {
            for (var infernoIndex = 0; infernoIndex < infernos.Length; infernoIndex++)
            {
                var fire = state.EntityManager.GetComponentData<NetworkInfernoArea>(infernos[infernoIndex]);
                if (fire.Owner == Entity.Null || !state.EntityManager.Exists(fire.Owner) ||
                    !state.EntityManager.HasComponent<NetworkPlayerState>(fire.Owner))
                    continue;

                var alreadyPresent = false;
                for (var killerIndex = 0; killerIndex < killers.Length; killerIndex++)
                {
                    if (killers[killerIndex].Entity == fire.Owner)
                    {
                        alreadyPresent = true;
                        break;
                    }
                }

                if (alreadyPresent)
                    continue;

                var player = state.EntityManager.GetComponentData<NetworkPlayerState>(fire.Owner);
                var utility = state.EntityManager.GetComponentData<NetworkUtilityPresentationState>(fire.Owner);
                killers.Add(new KillerChange
                {
                    Entity = fire.Owner,
                    State = player,
                    Utility = utility,
                    RemainingKills = 0,
                    RecentDetonateType = byte.MaxValue,
                    RecentShot = 0
                });
            }
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
                if (killer.Entity == victim.Entity)
                    continue;

                var infernoNear = HasOwnedInfernoNear(ref state, killer.Entity, victim.State.Position, infernos);
                var hasRecentAction = killer.RemainingKills > 0 || killer.RecentShot != 0 ||
                                      killer.RecentDetonateType != byte.MaxValue || infernoNear;
                if (!hasRecentAction)
                    continue;

                if (killer.State.Team == victim.State.Team && killer.RemainingKills > 0 &&
                    killer.RecentShot == 0 && killer.RecentDetonateType == byte.MaxValue && !infernoNear)
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
            var score = killer.RemainingKills > 0 ? 10000f : 0f;

            if (HasOwnedInfernoNear(ref state, killer.Entity, victim.State.Position, infernos))
                return score + 2200f;

            if (killer.RecentDetonateType == (byte)GrenadeType.HighExplosive)
                return score + 1800f - math.distance(killer.Utility.DetonatePosition, victim.State.Position);

            if (killer.RecentShot == 0)
                return score - 5000f;

            var toVictim = victim.State.Position - killer.State.Position;
            var distance = math.length(toVictim);
            if (distance <= 0.001f)
                return score;

            var forward = BuildViewForward(killer.State.Yaw, killer.State.Pitch);
            var aimScore = math.dot(forward, toVictim / distance);
            return score + aimScore * 500f - distance * 0.02f;
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

        private static bool IsLikelyHeadshot(in NetworkPlayerState killer, in NetworkPlayerState victim)
        {
            var eyeHeight = math.lerp(1.62f, 1.03f, killer.CrouchAmount);
            var origin = killer.Position + new float3(0f, eyeHeight, 0f);
            var victimScale = math.lerp(1f, 0.75f, victim.CrouchAmount);
            var head = victim.Position + new float3(0f, 1.62f * victimScale, 0f);
            var direction = BuildViewForward(killer.Yaw, killer.Pitch);
            var toHead = head - origin;
            var projected = math.dot(toHead, direction);
            if (projected <= 0f)
                return false;

            var perpendicularSq = math.lengthsq(toHead) - projected * projected;
            const float headRadius = 0.19f;
            return perpendicularSq <= headRadius * headRadius;
        }

        private static float3 BuildViewForward(float yaw, float pitch)
        {
            var yawRadians = math.radians(yaw);
            var pitchRadians = math.radians(pitch);
            var cosPitch = math.cos(pitchRadians);
            return math.normalizesafe(new float3(
                math.sin(yawRadians) * cosPitch,
                -math.sin(pitchRadians),
                math.cos(yawRadians) * cosPitch));
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
