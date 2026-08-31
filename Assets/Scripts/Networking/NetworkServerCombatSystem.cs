using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace PolyStrike.Networking
{
    public struct NetworkWeaponRuntime : IComponentData
    {
        public float FireCooldown;
        public float AccuracyPenalty;
        public float TimeSinceLastShot;
        public float ReloadRemaining;
        public float ReloadCommitRemaining;
        public byte SprayIndex;
        public byte ReloadCommitted;
        public uint TotalShots;
    }

    public struct NetworkTagState : IComponentData
    {
        public float PendingFactor;
        public byte DelayTicks;
    }

    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup), OrderLast = true)]
    public partial struct NetworkServerCombatSystem : ISystem
    {
        private const float FriendlyFireScale = 0.33f;
        private const float SourceUnitsPerMeter = 39.37f;
        private const int MaxUnlagTicks = 13;

        private EntityQuery playerQuery;

        public void OnCreate(ref SystemState state)
        {
            playerQuery = SystemAPI.QueryBuilder()
                .WithAll<NetworkPlayerState, NetworkPlayerInput, NetworkWeaponRuntime, NetworkPlayerPoseHistory>()
                .Build();
            state.RequireForUpdate(playerQuery);
            state.RequireForUpdate<NetworkTime>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            var serverTick = networkTime.ServerTick;
            if (!serverTick.IsValid)
                return;

            var entities = playerQuery.ToEntityArray(Allocator.Temp);
            for (var i = 0; i < entities.Length; i++)
            {
                var shooterEntity = entities[i];
                var shooter = state.EntityManager.GetComponentData<NetworkPlayerState>(shooterEntity);
                var input = state.EntityManager.GetComponentData<NetworkPlayerInput>(shooterEntity);
                var runtime = state.EntityManager.GetComponentData<NetworkWeaponRuntime>(shooterEntity);
                var interpolationDelay = state.EntityManager.HasComponent<CommandDataInterpolationDelay>(shooterEntity)
                    ? state.EntityManager.GetComponentData<CommandDataInterpolationDelay>(shooterEntity).Delay
                    : 0u;

                TickWeaponRuntime(ref shooter, in input, ref runtime, deltaTime);

                if ((shooter.Flags & NetworkPlayerFlags.Alive) != 0 && WantsToFire(in shooter, in input, in runtime))
                    TryFire(ref state, shooterEntity, ref shooter, in input, ref runtime, serverTick, interpolationDelay, entities);

                state.EntityManager.SetComponentData(shooterEntity, shooter);
                state.EntityManager.SetComponentData(shooterEntity, runtime);
            }

            TickTagging(ref state, entities, deltaTime);
            entities.Dispose();
        }

        private static void TickWeaponRuntime(
            ref NetworkPlayerState player,
            in NetworkPlayerInput input,
            ref NetworkWeaponRuntime runtime,
            float deltaTime)
        {
            runtime.FireCooldown = math.max(0f, runtime.FireCooldown - deltaTime);
            runtime.TimeSinceLastShot += deltaTime;

            var profile = WeaponProfile.Get(player.Team, player.ActiveWeapon);
            if (runtime.AccuracyPenalty > 0f)
            {
                var recoveryTime = player.CrouchAmount > 0.5f ? profile.CrouchRecovery : profile.StandRecovery;
                var recoveryRate = profile.FireInaccuracy / math.max(recoveryTime, 0.01f);
                runtime.AccuracyPenalty = math.max(0f, runtime.AccuracyPenalty - recoveryRate * deltaTime);
            }

            if (runtime.TimeSinceLastShot > math.max(0.35f, profile.StandRecovery))
                runtime.SprayIndex = 0;

            if (input.WeaponSlot == 1 || input.WeaponSlot == 2)
            {
                if (input.WeaponSlot != player.ActiveWeapon)
                {
                    player.ActiveWeapon = input.WeaponSlot;
                    var next = WeaponProfile.Get(player.Team, player.ActiveWeapon);
                    runtime.FireCooldown = math.max(runtime.FireCooldown, next.DeployTime);
                    runtime.SprayIndex = 0;
                    runtime.AccuracyPenalty = 0f;
                    runtime.ReloadRemaining = 0f;
                    runtime.ReloadCommitRemaining = 0f;
                    runtime.ReloadCommitted = 0;
                }
            }

            if (runtime.ReloadRemaining > 0f)
            {
                runtime.ReloadRemaining = math.max(0f, runtime.ReloadRemaining - deltaTime);
                runtime.ReloadCommitRemaining = math.max(0f, runtime.ReloadCommitRemaining - deltaTime);

                if (runtime.ReloadCommitted == 0 && runtime.ReloadCommitRemaining <= 0f)
                {
                    CommitReload(ref player, in profile);
                    runtime.ReloadCommitted = 1;
                }
            }
            else if (input.Reload.IsSet && player.MagazineAmmo < profile.MagazineSize && player.ReserveAmmo > 0)
            {
                runtime.ReloadRemaining = profile.ReloadReady;
                runtime.ReloadCommitRemaining = profile.ReloadClipReady;
                runtime.ReloadCommitted = 0;
                runtime.FireCooldown = math.max(runtime.FireCooldown, profile.ReloadReady);
            }
        }

        private static void CommitReload(ref NetworkPlayerState player, in WeaponProfile profile)
        {
            var needed = profile.MagazineSize - player.MagazineAmmo;
            var loaded = math.min(needed, player.ReserveAmmo);
            player.MagazineAmmo = (byte)(player.MagazineAmmo + loaded);
            player.ReserveAmmo = (byte)(player.ReserveAmmo - loaded);
        }

        private static bool WantsToFire(in NetworkPlayerState player, in NetworkPlayerInput input, in NetworkWeaponRuntime runtime)
        {
            if (runtime.FireCooldown > 0f || runtime.ReloadRemaining > 0f || player.MagazineAmmo == 0)
                return false;

            var profile = WeaponProfile.Get(player.Team, player.ActiveWeapon);
            return profile.Automatic ? input.FireHeld != 0 : input.FirePressed.IsSet;
        }

        private static void TryFire(
            ref SystemState state,
            Entity shooterEntity,
            ref NetworkPlayerState shooter,
            in NetworkPlayerInput input,
            ref NetworkWeaponRuntime runtime,
            NetworkTick serverTick,
            uint interpolationDelay,
            NativeArray<Entity> players)
        {
            var profile = WeaponProfile.Get(shooter.Team, shooter.ActiveWeapon);
            shooter.MagazineAmmo--;
            runtime.FireCooldown = 60f / profile.Rpm;
            runtime.TimeSinceLastShot = 0f;

            var recoil = profile.GetRecoil(runtime.SprayIndex);
            var inaccuracy = CalculateInaccuracy(in shooter, in profile, runtime.AccuracyPenalty);
            var direction = BuildShotDirection(shooter.Yaw, shooter.Pitch, recoil, inaccuracy, shooterEntity.Index, runtime.TotalShots);
            runtime.AccuracyPenalty += profile.FireInaccuracy;
            runtime.SprayIndex = (byte)math.min(runtime.SprayIndex + 1, 29);
            runtime.TotalShots++;

            var eyeHeight = math.lerp(1.62f, 1.03f, shooter.CrouchAmount);
            var origin = shooter.Position + new float3(0f, eyeHeight, 0f);
            var maxDistance = profile.RangeMeters;
            var bestTarget = Entity.Null;
            var bestDistance = maxDistance + 1f;
            var bestHitGroup = NetworkHitGroup.Chest;

            for (var i = 0; i < players.Length; i++)
            {
                var targetEntity = players[i];
                if (targetEntity == shooterEntity)
                    continue;

                var target = state.EntityManager.GetComponentData<NetworkPlayerState>(targetEntity);
                if ((target.Flags & NetworkPlayerFlags.Alive) == 0)
                    continue;

                var history = state.EntityManager.GetBuffer<NetworkPlayerPoseHistory>(targetEntity);
                if (!TryGetRewoundPose(in history, serverTick, interpolationDelay, input.FireSubtick, out var pose))
                    pose = new NetworkPlayerPoseHistory
                    {
                        Tick = serverTick,
                        Position = target.Position,
                        Yaw = target.Yaw,
                        Pitch = target.Pitch,
                        CrouchAmount = target.CrouchAmount
                    };

                if (!HistoricalHitboxes.Raycast(origin, direction, in pose, maxDistance, out var distance, out var hitGroup))
                    continue;

                if (distance >= bestDistance)
                    continue;

                bestTarget = targetEntity;
                bestDistance = distance;
                bestHitGroup = hitGroup;
            }

            if (bestTarget == Entity.Null)
                return;

            var damage = profile.Damage;
            damage *= math.pow(profile.RangeModifier, bestDistance * SourceUnitsPerMeter / 500f);

            if (NetworkSandlineCollision.TryRaycast(origin, direction, bestDistance, out var wall) && wall.EntryDistance < bestDistance)
            {
                var thicknessUnits = wall.Thickness * SourceUnitsPerMeter;
                var inverseModifier = 1f / math.max(NetworkSandlineCollision.PenetrationModifier(wall.Surface), 0.01f);
                var lostDamage = damage * NetworkSandlineCollision.DamageLossModifier(wall.Surface);
                lostDamage += (3.75f / math.max(profile.PenetrationPower, 0.01f)) * (inverseModifier * 3f);
                lostDamage += inverseModifier * thicknessUnits * thicknessUnits / 24f;
                damage -= math.max(lostDamage, 0f);
                if (damage < 1f || bestDistance <= wall.ExitDistance)
                    return;

                damage *= math.pow(profile.RangeModifier, thicknessUnits / 500f);
            }

            var targetState = state.EntityManager.GetComponentData<NetworkPlayerState>(bestTarget);
            if (targetState.Team == shooter.Team)
                damage *= FriendlyFireScale;

            var wasAlive = (targetState.Flags & NetworkPlayerFlags.Alive) != 0;
            ApplyDamage(ref targetState, bestHitGroup, damage, profile.ArmorPenetration);
            ApplyTag(ref state, bestTarget, in profile);

            var killed = wasAlive && (targetState.Flags & NetworkPlayerFlags.Alive) == 0;
            if (killed)
            {
                targetState.Deaths = (ushort)math.min(ushort.MaxValue, targetState.Deaths + 1);
                if (targetState.Team != shooter.Team)
                {
                    shooter.Kills = (ushort)math.min(ushort.MaxValue, shooter.Kills + 1);
                    shooter.Money = (ushort)math.min(16000, shooter.Money + profile.KillReward);
                }
            }

            state.EntityManager.SetComponentData(bestTarget, targetState);
        }

        private static bool TryGetRewoundPose(
            in DynamicBuffer<NetworkPlayerPoseHistory> history,
            NetworkTick currentTick,
            uint interpolationDelay,
            byte subtick,
            out NetworkPlayerPoseHistory pose)
        {
            pose = default;
            if (!currentTick.IsValid || history.Length == 0)
                return false;

            var shotTick = currentTick;
            shotTick.Subtract(math.min(interpolationDelay, (uint)MaxUnlagTicks));

            var oldest = history[0].Tick;
            if (oldest.IsValid && oldest.IsNewerThan(shotTick))
            {
                pose = history[0];
                return true;
            }

            return SubtickPoseRewind.TrySample(in history, shotTick, subtick, out pose);
        }

        private static float CalculateInaccuracy(in NetworkPlayerState player, in WeaponProfile profile, float penalty)
        {
            var result = math.lerp(profile.StandingInaccuracy, profile.CrouchingInaccuracy, player.CrouchAmount) + penalty;
            var grounded = (player.Flags & NetworkPlayerFlags.Grounded) != 0;
            if (!grounded)
                result += 0.35f;

            var planarSpeed = math.length(player.Velocity.xz) * SourceUnitsPerMeter;
            var speedFraction = planarSpeed / math.max(profile.MaxMoveSpeed, 1f);
            if (speedFraction > 0.34f)
            {
                var movementFactor = math.saturate((speedFraction - 0.34f) / (0.95f - 0.34f));
                movementFactor = math.pow(movementFactor, 0.25f);
                result += profile.MovingInaccuracy * movementFactor;
            }

            return result;
        }

        private static float3 BuildShotDirection(
            float yaw,
            float pitch,
            float2 recoil,
            float inaccuracy,
            int entityIndex,
            uint shotNumber)
        {
            var random = new Unity.Mathematics.Random((uint)math.max(1, entityIndex * 73856093 + (int)shotNumber * 19349663 + 1));
            var radius = math.sqrt(random.NextFloat()) * inaccuracy;
            var angle = random.NextFloat(0f, math.PI * 2f);
            var spreadX = math.cos(angle) * radius * (180f / math.PI);
            var spreadY = math.sin(angle) * radius * (180f / math.PI);

            var shotYaw = math.radians(yaw + recoil.x + spreadX);
            var shotPitch = math.radians(pitch - recoil.y - spreadY);
            var cosPitch = math.cos(shotPitch);
            return math.normalizesafe(new float3(
                math.sin(shotYaw) * cosPitch,
                -math.sin(shotPitch),
                math.cos(shotYaw) * cosPitch));
        }

        private static void ApplyDamage(ref NetworkPlayerState target, NetworkHitGroup hitGroup, float rawDamage, float armorRatio)
        {
            var multiplier = hitGroup switch
            {
                NetworkHitGroup.Head => 4f,
                NetworkHitGroup.Stomach => 1.25f,
                NetworkHitGroup.Legs => 0.75f,
                _ => 1f
            };

            var damage = rawDamage * multiplier;
            var helmet = (target.Flags & NetworkPlayerFlags.Helmet) != 0;
            var armorProtected = target.Armor > 0 && hitGroup != NetworkHitGroup.Legs && (hitGroup != NetworkHitGroup.Head || helmet);
            var healthDamage = damage;
            var armorDamage = 0f;

            if (armorProtected)
            {
                healthDamage = damage * math.saturate(armorRatio);
                armorDamage = (damage - healthDamage) * 0.5f;
                if (armorDamage > target.Armor)
                {
                    armorDamage = target.Armor;
                    healthDamage = damage - armorDamage / 0.5f;
                }
            }

            var dealt = math.max(0, (int)math.floor(healthDamage));
            var armorSpent = math.max(0, (int)math.floor(armorDamage));
            target.Health = (ushort)math.max(0, target.Health - dealt);
            target.Armor = (ushort)math.max(0, target.Armor - armorSpent);

            if (target.Armor == 0)
                target.Flags &= unchecked((byte)~NetworkPlayerFlags.Helmet);

            if (target.Health == 0)
            {
                target.Flags &= unchecked((byte)~NetworkPlayerFlags.Alive);
                target.Velocity = float3.zero;
            }
        }

        private static void ApplyTag(ref SystemState state, Entity targetEntity, in WeaponProfile profile)
        {
            var tag = state.EntityManager.GetComponentData<NetworkTagState>(targetEntity);
            tag.PendingFactor = tag.PendingFactor <= 0f ? profile.TaggingFactor : math.min(tag.PendingFactor, profile.TaggingFactor);
            tag.DelayTicks = 2;
            state.EntityManager.SetComponentData(targetEntity, tag);
        }

        private static void TickTagging(ref SystemState state, NativeArray<Entity> players, float deltaTime)
        {
            for (var i = 0; i < players.Length; i++)
            {
                var entity = players[i];
                var player = state.EntityManager.GetComponentData<NetworkPlayerState>(entity);
                var tag = state.EntityManager.GetComponentData<NetworkTagState>(entity);

                if (tag.DelayTicks > 0)
                {
                    tag.DelayTicks--;
                    if (tag.DelayTicks == 0 && tag.PendingFactor > 0f)
                    {
                        player.VelocityModifier = math.min(player.VelocityModifier <= 0f ? 1f : player.VelocityModifier, tag.PendingFactor);
                        tag.PendingFactor = 0f;
                    }
                }
                else if ((player.Flags & NetworkPlayerFlags.Grounded) != 0)
                {
                    player.VelocityModifier = math.min(1f, (player.VelocityModifier <= 0f ? 1f : player.VelocityModifier) + 0.4f * deltaTime);
                }

                state.EntityManager.SetComponentData(entity, player);
                state.EntityManager.SetComponentData(entity, tag);
            }
        }

        private enum NetworkHitGroup : byte
        {
            Head,
            Chest,
            Stomach,
            Legs
        }

        private static class HistoricalHitboxes
        {
            public static bool Raycast(
                float3 origin,
                float3 direction,
                in NetworkPlayerPoseHistory pose,
                float maxDistance,
                out float distance,
                out NetworkHitGroup hitGroup)
            {
                distance = maxDistance + 1f;
                hitGroup = NetworkHitGroup.Chest;
                var scale = math.lerp(1f, 0.75f, pose.CrouchAmount);

                TestSphere(origin, direction, pose.Position + new float3(0f, 1.62f * scale, 0f), 0.17f, NetworkHitGroup.Head, ref distance, ref hitGroup);
                TestBox(origin, direction, pose.Position + new float3(0f, 1.28f * scale, 0f), new float3(0.28f, 0.25f * scale, 0.19f), NetworkHitGroup.Chest, ref distance, ref hitGroup);
                TestBox(origin, direction, pose.Position + new float3(0f, 0.91f * scale, 0f), new float3(0.25f, 0.18f * scale, 0.18f), NetworkHitGroup.Stomach, ref distance, ref hitGroup);
                TestBox(origin, direction, pose.Position + new float3(0f, 0.43f * scale, 0f), new float3(0.27f, 0.35f * scale, 0.17f), NetworkHitGroup.Legs, ref distance, ref hitGroup);
                return distance <= maxDistance;
            }

            private static void TestSphere(
                float3 origin,
                float3 direction,
                float3 center,
                float radius,
                NetworkHitGroup group,
                ref float bestDistance,
                ref NetworkHitGroup bestGroup)
            {
                var toCenter = center - origin;
                var projected = math.dot(toCenter, direction);
                if (projected < 0f || projected >= bestDistance)
                    return;

                var distanceSq = math.lengthsq(toCenter) - projected * projected;
                var radiusSq = radius * radius;
                if (distanceSq > radiusSq)
                    return;

                var entry = projected - math.sqrt(math.max(0f, radiusSq - distanceSq));
                if (entry >= 0f && entry < bestDistance)
                {
                    bestDistance = entry;
                    bestGroup = group;
                }
            }

            private static void TestBox(
                float3 origin,
                float3 direction,
                float3 center,
                float3 halfExtents,
                NetworkHitGroup group,
                ref float bestDistance,
                ref NetworkHitGroup bestGroup)
            {
                var min = center - halfExtents;
                var max = center + halfExtents;
                var entry = 0f;
                var exit = bestDistance;

                for (var axis = 0; axis < 3; axis++)
                {
                    var d = direction[axis];
                    if (math.abs(d) < 0.000001f)
                    {
                        if (origin[axis] < min[axis] || origin[axis] > max[axis])
                            return;
                        continue;
                    }

                    var inverse = 1f / d;
                    var t1 = (min[axis] - origin[axis]) * inverse;
                    var t2 = (max[axis] - origin[axis]) * inverse;
                    if (t1 > t2)
                        (t1, t2) = (t2, t1);

                    entry = math.max(entry, t1);
                    exit = math.min(exit, t2);
                    if (entry > exit)
                        return;
                }

                if (entry >= 0f && entry < bestDistance)
                {
                    bestDistance = entry;
                    bestGroup = group;
                }
            }
        }

        private readonly struct WeaponProfile
        {
            public readonly float Damage;
            public readonly float RangeMeters;
            public readonly float RangeModifier;
            public readonly float ArmorPenetration;
            public readonly float PenetrationPower;
            public readonly float TaggingFactor;
            public readonly float Rpm;
            public readonly byte MagazineSize;
            public readonly float ReloadClipReady;
            public readonly float ReloadReady;
            public readonly float DeployTime;
            public readonly float MaxMoveSpeed;
            public readonly float StandingInaccuracy;
            public readonly float CrouchingInaccuracy;
            public readonly float MovingInaccuracy;
            public readonly float FireInaccuracy;
            public readonly float StandRecovery;
            public readonly float CrouchRecovery;
            public readonly bool Automatic;
            public readonly ushort KillReward;

            private WeaponProfile(
                float damage, float rangeMeters, float rangeModifier, float armorPenetration, float penetrationPower,
                float taggingFactor, float rpm, byte magazineSize, float reloadClipReady, float reloadReady,
                float deployTime, float maxMoveSpeed, float standingInaccuracy, float crouchingInaccuracy,
                float movingInaccuracy, float fireInaccuracy, float standRecovery, float crouchRecovery,
                bool automatic, ushort killReward)
            {
                Damage = damage;
                RangeMeters = rangeMeters;
                RangeModifier = rangeModifier;
                ArmorPenetration = armorPenetration;
                PenetrationPower = penetrationPower;
                TaggingFactor = taggingFactor;
                Rpm = rpm;
                MagazineSize = magazineSize;
                ReloadClipReady = reloadClipReady;
                ReloadReady = reloadReady;
                DeployTime = deployTime;
                MaxMoveSpeed = maxMoveSpeed;
                StandingInaccuracy = standingInaccuracy;
                CrouchingInaccuracy = crouchingInaccuracy;
                MovingInaccuracy = movingInaccuracy;
                FireInaccuracy = fireInaccuracy;
                StandRecovery = standRecovery;
                CrouchRecovery = crouchRecovery;
                Automatic = automatic;
                KillReward = killReward;
            }

            public static WeaponProfile Get(byte team, byte weapon)
            {
                if (weapon == 1)
                {
                    return team == 0
                        ? new WeaponProfile(36f, 8192f / SourceUnitsPerMeter, 0.98f, 0.775f, 2f, 0.319f, 600f, 30, 1.17f, 2.43f, 1f, 215f, 0.00641f, 0.0048f, 0.17506f, 0.0078f, 0.368f, 0.305f, true, 300)
                        : new WeaponProfile(33f, 8192f / SourceUnitsPerMeter, 0.97f, 0.70f, 2f, 0.319f, 667f, 30, 1.37f, 3.07f, 1.13f, 225f, 0.0049f, 0.0041f, 0.13788f, 0.006f, 0.339f, 0.28f, true, 300);
                }

                return team == 0
                    ? new WeaponProfile(30f, 4096f / SourceUnitsPerMeter, 0.85f, 0.47f, 1f, 0.36f, 400f, 20, 1.45f, 2.17f, 1.10f, 240f, 0.0056f, 0.0042f, 0.010f, 0.010f, 0.20f, 0.18f, false, 300)
                    : new WeaponProfile(35f, 4096f / SourceUnitsPerMeter, 0.91f, 0.505f, 1f, 0.36f, 353f, 12, 1.45f, 2.17f, 1.00f, 240f, 0.0049f, 0.0037f, 0.014f, 0.0085f, 0.35f, 0.30f, false, 300);
            }

            public float2 GetRecoil(byte index)
            {
                if (index == 0)
                    return float2.zero;

                if (Automatic)
                {
                    var i = math.min((int)index, 29);
                    var vertical = math.min(i * (TeamPatternScale() ? 0.43f : 0.36f), TeamPatternScale() ? 10.9f : 8.9f);
                    var phase = i < 9 ? 0f : (i - 9) * 0.55f;
                    var horizontal = i < 9 ? math.sin(i * 1.8f) * 0.1f : math.sin(phase) * (TeamPatternScale() ? 3.8f : 3.1f);
                    return new float2(horizontal, vertical);
                }

                return new float2(math.sin(index * 2.31f) * 0.04f * index, 0.4f * index);
            }

            private bool TeamPatternScale() => Damage >= 36f;
        }
    }
}
