using PolyStrike.Gameplay;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace PolyStrike.Networking
{
    [GhostComponent]
    public struct NetworkUtilityPresentationState : IComponentData
    {
        [GhostField] public uint ThrowSequence;
        [GhostField] public byte ThrowType;
        [GhostField(Quantization = 1000)] public float3 ThrowPosition;
        [GhostField(Quantization = 1000)] public float3 ThrowVelocity;

        [GhostField] public uint DetonateSequence;
        [GhostField] public byte DetonateType;
        [GhostField(Quantization = 1000)] public float3 DetonatePosition;
    }

    [GhostComponent]
    public struct NetworkFlashState : IComponentData
    {
        [GhostField(Quantization = 1000)] public float Intensity;
        [GhostField(Quantization = 1000)] public float Remaining;
    }

    public struct NetworkUtilityRuntime : IComponentData
    {
        public byte Primed;
        public byte PrimedType;
        public byte PendingThrow;
        public byte PendingType;
        public float Strength;
        public float PendingStrength;
        public float ConstructionDelay;
    }

    public struct NetworkGrenadeProjectile : IComponentData
    {
        public Entity Owner;
        public float3 Position;
        public float3 Velocity;
        public float Age;
        public uint Sequence;
        public byte Team;
        public byte Type;
    }

    public struct NetworkSmokeArea : IComponentData
    {
        public float3 Position;
        public float Remaining;
    }

    public struct NetworkInfernoArea : IComponentData
    {
        public Entity Owner;
        public float3 Position;
        public float Remaining;
        public float DamageTick;
        public byte Team;
    }

    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(NetworkLoadoutSwitchSystem))]
    [UpdateBefore(typeof(NetworkServerCombatSystem))]
    public partial struct NetworkUtilityThrowSystem : ISystem
    {
        private const float SourceUnitsPerMeter = 39.37f;

        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);

            foreach (var (player, loadout, input, match, runtime, presentation, entity) in
                     SystemAPI.Query<
                             RefRW<NetworkPlayerState>,
                             RefRW<NetworkLoadoutState>,
                             RefRO<NetworkPlayerInput>,
                             RefRO<NetworkMatchSnapshot>,
                             RefRW<NetworkUtilityRuntime>,
                             RefRW<NetworkUtilityPresentationState>>()
                         .WithEntityAccess())
            {
                ref var playerState = ref player.ValueRW;
                ref var utility = ref runtime.ValueRW;
                var command = input.ValueRO;

                var canUseUtility = (playerState.Flags & NetworkPlayerFlags.Alive) != 0 &&
                                    (match.ValueRO.Phase == NetworkMatchPhase.Live || match.ValueRO.Phase == NetworkMatchPhase.PostPlant) &&
                                    IsUtilitySlot(playerState.ActiveWeapon) &&
                                    HasGrenade(playerState.ActiveWeapon, in loadout.ValueRO);

                if (!canUseUtility)
                {
                    utility.Primed = 0;
                    if ((playerState.Flags & NetworkPlayerFlags.Alive) == 0)
                        utility.PendingThrow = 0;
                    continue;
                }

                if (utility.PendingThrow != 0)
                {
                    utility.ConstructionDelay = math.max(0f, utility.ConstructionDelay - deltaTime);
                    if (utility.ConstructionDelay <= 0f)
                    {
                        SpawnGrenade(
                            ref commandBuffer,
                            entity,
                            ref playerState,
                            ref loadout.ValueRW,
                            ref utility,
                            ref presentation.ValueRW);
                    }
                    continue;
                }

                if (utility.Primed == 0 && (command.FirePressed.IsSet || command.SecondaryFirePressed.IsSet))
                {
                    utility.Primed = 1;
                    utility.PrimedType = SlotToType(playerState.ActiveWeapon);
                    utility.Strength = ResolveThrowStrength(command.FireHeld != 0, command.SecondaryFireHeld != 0);
                }

                if (utility.Primed == 0)
                    continue;

                if (command.FireHeld != 0 || command.SecondaryFireHeld != 0)
                    utility.Strength = ResolveThrowStrength(command.FireHeld != 0, command.SecondaryFireHeld != 0);

                if (!command.FireReleased.IsSet && !command.SecondaryFireReleased.IsSet)
                    continue;

                utility.Primed = 0;
                utility.PendingThrow = 1;
                utility.PendingType = utility.PrimedType;
                utility.PendingStrength = utility.Strength;
                utility.ConstructionDelay = GrenadeRules.ThrowConstructionDelay;
            }

            commandBuffer.Playback(state.EntityManager);
            commandBuffer.Dispose();
        }

        private static void SpawnGrenade(
            ref EntityCommandBuffer commandBuffer,
            Entity owner,
            ref NetworkPlayerState player,
            ref NetworkLoadoutState loadout,
            ref NetworkUtilityRuntime runtime,
            ref NetworkUtilityPresentationState presentation)
        {
            var type = runtime.PendingType;
            var slot = TypeToSlot(type);
            if (!HasGrenade(slot, in loadout))
            {
                runtime.PendingThrow = 0;
                return;
            }

            var eyeHeight = math.lerp(1.62f, 1.03f, player.CrouchAmount);
            var eye = player.Position + new float3(0f, eyeHeight, 0f);
            var direction = BuildThrowDirection(player.Yaw, player.Pitch);
            var speed = GrenadeRules.GetThrowSpeed(runtime.PendingStrength) / SourceUnitsPerMeter;
            var velocity = direction * speed + player.Velocity * GrenadeRules.PlayerVelocityInheritance;
            var position = eye + direction * 0.32f;

            presentation.ThrowSequence++;
            presentation.ThrowType = type;
            presentation.ThrowPosition = position;
            presentation.ThrowVelocity = velocity;

            var projectile = commandBuffer.CreateEntity();
            commandBuffer.AddComponent(projectile, new NetworkGrenadeProjectile
            {
                Owner = owner,
                Position = position,
                Velocity = velocity,
                Sequence = presentation.ThrowSequence,
                Team = player.Team,
                Type = type
            });

            ConsumeGrenade(type, ref loadout);
            runtime.PendingThrow = 0;
            runtime.ConstructionDelay = 0f;

            if (!HasGrenade(slot, in loadout))
            {
                player.ActiveWeapon = loadout.PrimaryOwned != 0 ? (byte)1 : (byte)2;
                NetworkLoadoutSwitchSystem.LoadActiveAmmo(ref player, in loadout);
            }
        }

        private static float ResolveThrowStrength(bool primary, bool secondary)
        {
            if (primary && secondary)
                return 0.5f;
            return secondary ? 0f : 1f;
        }

        private static float3 BuildThrowDirection(float yaw, float pitch)
        {
            var adjustedPitch = pitch - (90f - math.abs(pitch)) * (10f / 90f);
            var yawRadians = math.radians(yaw);
            var pitchRadians = math.radians(adjustedPitch);
            var cosPitch = math.cos(pitchRadians);
            return math.normalizesafe(new float3(
                math.sin(yawRadians) * cosPitch,
                -math.sin(pitchRadians),
                math.cos(yawRadians) * cosPitch));
        }

        private static bool IsUtilitySlot(byte slot) => slot == 6 || slot == 7 || slot == 8 || slot == 10;

        private static byte SlotToType(byte slot)
        {
            return slot switch
            {
                6 => (byte)GrenadeType.HighExplosive,
                7 => (byte)GrenadeType.Flashbang,
                8 => (byte)GrenadeType.Smoke,
                10 => (byte)GrenadeType.Molotov,
                _ => byte.MaxValue
            };
        }

        private static byte TypeToSlot(byte type)
        {
            return (GrenadeType)type switch
            {
                GrenadeType.HighExplosive => 6,
                GrenadeType.Flashbang => 7,
                GrenadeType.Smoke => 8,
                GrenadeType.Molotov => 10,
                _ => 0
            };
        }

        private static bool HasGrenade(byte slot, in NetworkLoadoutState loadout)
        {
            return slot switch
            {
                6 => loadout.HeGrenades > 0,
                7 => loadout.Flashbangs > 0,
                8 => loadout.SmokeGrenades > 0,
                10 => loadout.FireGrenades > 0,
                _ => false
            };
        }

        private static void ConsumeGrenade(byte type, ref NetworkLoadoutState loadout)
        {
            switch ((GrenadeType)type)
            {
                case GrenadeType.HighExplosive:
                    if (loadout.HeGrenades > 0) loadout.HeGrenades--;
                    break;
                case GrenadeType.Flashbang:
                    if (loadout.Flashbangs > 0) loadout.Flashbangs--;
                    break;
                case GrenadeType.Smoke:
                    if (loadout.SmokeGrenades > 0) loadout.SmokeGrenades--;
                    break;
                case GrenadeType.Molotov:
                    if (loadout.FireGrenades > 0) loadout.FireGrenades--;
                    break;
            }
        }
    }

    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(NetworkServerCombatSystem))]
    [UpdateBefore(typeof(NetworkServerMatchSystem))]
    public partial struct NetworkUtilitySimulationSystem : ISystem
    {
        private const float SourceUnitsPerMeter = 39.37f;
        private const float HeRadius = 350f / SourceUnitsPerMeter;
        private const float HeBaseDamage = 99f;
        private const float FlashRadius = 1500f / SourceUnitsPerMeter;
        private const float FlashMaxDuration = 5.07f;
        private const float FriendlyGrenadeScale = 0.85f;
        private const float InfernoRadius = 2.55f;
        private const float SmokeRadius = 2.2f;

        private EntityQuery playerQuery;
        private EntityQuery projectileQuery;
        private EntityQuery smokeQuery;
        private EntityQuery infernoQuery;

        public void OnCreate(ref SystemState state)
        {
            playerQuery = SystemAPI.QueryBuilder()
                .WithAll<NetworkPlayerState, NetworkFlashState>()
                .Build();
            projectileQuery = SystemAPI.QueryBuilder()
                .WithAll<NetworkGrenadeProjectile>()
                .Build();
            smokeQuery = SystemAPI.QueryBuilder()
                .WithAll<NetworkSmokeArea>()
                .Build();
            infernoQuery = SystemAPI.QueryBuilder()
                .WithAll<NetworkInfernoArea>()
                .Build();
        }

        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            var players = playerQuery.ToEntityArray(Allocator.Temp);
            var projectiles = projectileQuery.ToEntityArray(Allocator.Temp);
            var smokes = smokeQuery.ToEntityArray(Allocator.Temp);
            var infernos = infernoQuery.ToEntityArray(Allocator.Temp);
            var commandBuffer = new EntityCommandBuffer(Allocator.Temp);

            TickFlash(ref state, players, deltaTime);
            TickSmoke(ref state, ref commandBuffer, smokes, deltaTime);
            TickInfernos(ref state, ref commandBuffer, players, smokes, infernos, deltaTime);
            TickProjectiles(ref state, ref commandBuffer, players, smokes, projectiles, deltaTime);

            commandBuffer.Playback(state.EntityManager);
            commandBuffer.Dispose();
            infernos.Dispose();
            smokes.Dispose();
            projectiles.Dispose();
            players.Dispose();
        }

        private static void TickProjectiles(
            ref SystemState state,
            ref EntityCommandBuffer commandBuffer,
            NativeArray<Entity> players,
            NativeArray<Entity> smokes,
            NativeArray<Entity> projectiles,
            float deltaTime)
        {
            for (var i = 0; i < projectiles.Length; i++)
            {
                var entity = projectiles[i];
                if (!state.EntityManager.Exists(entity))
                    continue;

                var grenade = state.EntityManager.GetComponentData<NetworkGrenadeProjectile>(entity);
                grenade.Age += deltaTime;
                grenade.Velocity.y -= GrenadeRules.Gravity / SourceUnitsPerMeter * deltaTime;

                var start = grenade.Position;
                var next = start + grenade.Velocity * deltaTime;
                var delta = next - start;
                var distance = math.length(delta);
                var hitSurface = false;

                if (distance > 0.0001f &&
                    NetworkSandlineCollision.TryRaycast(start, delta / distance, distance, out var wall) &&
                    wall.EntryDistance <= distance)
                {
                    var direction = delta / distance;
                    grenade.Position = start + direction * math.max(0f, wall.EntryDistance - 0.025f);
                    grenade.Velocity *= -GrenadeRules.BounceScale;
                    hitSurface = true;
                }
                else
                {
                    grenade.Position = next;
                }

                var hitGround = grenade.Position.y <= NetworkSandlineCollision.GroundY + 0.06f;
                if (hitGround)
                {
                    grenade.Position.y = NetworkSandlineCollision.GroundY + 0.06f;
                    if (grenade.Velocity.y < 0f)
                        grenade.Velocity.y = -grenade.Velocity.y * GrenadeRules.BounceScale;
                    grenade.Velocity.x *= 0.82f;
                    grenade.Velocity.z *= 0.82f;
                }

                var shouldDetonate = (GrenadeType)grenade.Type switch
                {
                    GrenadeType.HighExplosive => grenade.Age >= GrenadeRules.HeFlashFuse,
                    GrenadeType.Flashbang => grenade.Age >= GrenadeRules.HeFlashFuse,
                    GrenadeType.Smoke => grenade.Age >= GrenadeRules.SmokeArmTime &&
                                         (hitGround || hitSurface || math.lengthsq(grenade.Velocity) < 0.40f),
                    GrenadeType.Molotov => (hitGround && grenade.Age >= 0.10f) || grenade.Age >= GrenadeRules.MolotovAirFuse,
                    _ => true
                };

                if (shouldDetonate)
                {
                    Detonate(ref state, ref commandBuffer, players, smokes, in grenade);
                    commandBuffer.DestroyEntity(entity);
                    continue;
                }

                state.EntityManager.SetComponentData(entity, grenade);
            }
        }

        private static void Detonate(
            ref SystemState state,
            ref EntityCommandBuffer commandBuffer,
            NativeArray<Entity> players,
            NativeArray<Entity> smokes,
            in NetworkGrenadeProjectile grenade)
        {
            PublishDetonation(ref state, grenade.Owner, grenade.Type, grenade.Position);

            switch ((GrenadeType)grenade.Type)
            {
                case GrenadeType.HighExplosive:
                    DetonateHe(ref state, players, in grenade);
                    break;
                case GrenadeType.Flashbang:
                    DetonateFlash(ref state, players, in grenade);
                    break;
                case GrenadeType.Smoke:
                    commandBuffer.AddComponent(commandBuffer.CreateEntity(), new NetworkSmokeArea
                    {
                        Position = grenade.Position,
                        Remaining = GrenadeRules.SmokeDuration
                    });
                    break;
                case GrenadeType.Molotov:
                    if (!IsInsideSmoke(ref state, smokes, grenade.Position))
                    {
                        commandBuffer.AddComponent(commandBuffer.CreateEntity(), new NetworkInfernoArea
                        {
                            Owner = grenade.Owner,
                            Team = grenade.Team,
                            Position = grenade.Position,
                            Remaining = grenade.Team == 0 ? GrenadeRules.MolotovLifetime : GrenadeRules.IncendiaryLifetime,
                            DamageTick = 0f
                        });
                    }
                    break;
            }
        }

        private static void DetonateHe(ref SystemState state, NativeArray<Entity> players, in NetworkGrenadeProjectile grenade)
        {
            var reward = 0;
            var enemyKills = 0;
            for (var i = 0; i < players.Length; i++)
            {
                var targetEntity = players[i];
                var target = state.EntityManager.GetComponentData<NetworkPlayerState>(targetEntity);
                if ((target.Flags & NetworkPlayerFlags.Alive) == 0)
                    continue;

                var damagePoint = target.Position + new float3(0f, math.lerp(0.9f, 0.72f, target.CrouchAmount), 0f);
                var delta = damagePoint - grenade.Position;
                var distance = math.length(delta);
                if (distance >= HeRadius || !HasLineOfSight(grenade.Position, damagePoint))
                    continue;

                var rawDamage = HeBaseDamage * (1f - distance / HeRadius);
                if (target.Team == grenade.Team && targetEntity != grenade.Owner)
                    rawDamage *= FriendlyGrenadeScale;

                var wasAlive = (target.Flags & NetworkPlayerFlags.Alive) != 0;
                ApplyArmoredDamage(ref target, rawDamage, 0.60f);
                var killed = wasAlive && (target.Flags & NetworkPlayerFlags.Alive) == 0;
                if (killed)
                {
                    target.Deaths = (ushort)math.min(ushort.MaxValue, target.Deaths + 1);
                    if (target.Team != grenade.Team && targetEntity != grenade.Owner)
                    {
                        reward += 300;
                        enemyKills++;
                    }
                }

                state.EntityManager.SetComponentData(targetEntity, target);
            }

            if ((reward > 0 || enemyKills > 0) && state.EntityManager.Exists(grenade.Owner) &&
                state.EntityManager.HasComponent<NetworkPlayerState>(grenade.Owner))
            {
                var owner = state.EntityManager.GetComponentData<NetworkPlayerState>(grenade.Owner);
                owner.Money = (ushort)math.min(NetworkMatchRules.MaxMoney, owner.Money + reward);
                owner.Kills = (ushort)math.min(ushort.MaxValue, owner.Kills + enemyKills);
                state.EntityManager.SetComponentData(grenade.Owner, owner);
            }
        }

        private static void DetonateFlash(ref SystemState state, NativeArray<Entity> players, in NetworkGrenadeProjectile grenade)
        {
            for (var i = 0; i < players.Length; i++)
            {
                var targetEntity = players[i];
                var target = state.EntityManager.GetComponentData<NetworkPlayerState>(targetEntity);
                if ((target.Flags & NetworkPlayerFlags.Alive) == 0)
                    continue;

                var eye = target.Position + new float3(0f, math.lerp(1.62f, 1.03f, target.CrouchAmount), 0f);
                var delta = grenade.Position - eye;
                var distance = math.length(delta);
                if (distance >= FlashRadius || distance <= 0.001f || !HasLineOfSight(grenade.Position, eye))
                    continue;

                var toFlash = delta / distance;
                var viewForward = BuildViewForward(target.Yaw, target.Pitch);
                var facing = math.dot(viewForward, toFlash);
                var angleFactor = math.lerp(0.12f, 1f, math.saturate((facing + 0.35f) / (0.92f + 0.35f)));
                var distanceFactor = math.sqrt(1f - math.saturate(distance / FlashRadius));
                var intensity = math.saturate(angleFactor * math.lerp(0.42f, 1f, distanceFactor));
                var duration = FlashMaxDuration * distanceFactor * math.lerp(0.22f, 1f, angleFactor);
                if (duration < 0.08f)
                    continue;

                var flash = state.EntityManager.GetComponentData<NetworkFlashState>(targetEntity);
                if (duration >= flash.Remaining)
                {
                    flash.Remaining = duration;
                    flash.Intensity = math.max(flash.Intensity, intensity);
                    state.EntityManager.SetComponentData(targetEntity, flash);
                }
            }
        }

        private static void TickFlash(ref SystemState state, NativeArray<Entity> players, float deltaTime)
        {
            for (var i = 0; i < players.Length; i++)
            {
                var entity = players[i];
                var flash = state.EntityManager.GetComponentData<NetworkFlashState>(entity);
                if (flash.Remaining <= 0f)
                    continue;

                flash.Remaining = math.max(0f, flash.Remaining - deltaTime);
                if (flash.Remaining <= 0f)
                    flash.Intensity = 0f;
                else
                    flash.Intensity = math.max(0f, flash.Intensity - deltaTime * 0.12f);
                state.EntityManager.SetComponentData(entity, flash);
            }
        }

        private static void TickSmoke(
            ref SystemState state,
            ref EntityCommandBuffer commandBuffer,
            NativeArray<Entity> smokes,
            float deltaTime)
        {
            for (var i = 0; i < smokes.Length; i++)
            {
                var entity = smokes[i];
                var smoke = state.EntityManager.GetComponentData<NetworkSmokeArea>(entity);
                smoke.Remaining -= deltaTime;
                if (smoke.Remaining <= 0f)
                    commandBuffer.DestroyEntity(entity);
                else
                    state.EntityManager.SetComponentData(entity, smoke);
            }
        }

        private static void TickInfernos(
            ref SystemState state,
            ref EntityCommandBuffer commandBuffer,
            NativeArray<Entity> players,
            NativeArray<Entity> smokes,
            NativeArray<Entity> infernos,
            float deltaTime)
        {
            for (var i = 0; i < infernos.Length; i++)
            {
                var entity = infernos[i];
                var fire = state.EntityManager.GetComponentData<NetworkInfernoArea>(entity);
                fire.Remaining -= deltaTime;
                if (fire.Remaining <= 0f || IsInsideSmoke(ref state, smokes, fire.Position))
                {
                    commandBuffer.DestroyEntity(entity);
                    continue;
                }

                fire.DamageTick -= deltaTime;
                if (fire.DamageTick <= 0f)
                {
                    fire.DamageTick += GrenadeRules.InfernoDamageTick;
                    var damage = GrenadeRules.InfernoDamagePerSecond * GrenadeRules.InfernoDamageTick;

                    for (var playerIndex = 0; playerIndex < players.Length; playerIndex++)
                    {
                        var targetEntity = players[playerIndex];
                        var target = state.EntityManager.GetComponentData<NetworkPlayerState>(targetEntity);
                        if ((target.Flags & NetworkPlayerFlags.Alive) == 0)
                            continue;

                        var horizontal = target.Position.xz - fire.Position.xz;
                        if (math.lengthsq(horizontal) > InfernoRadius * InfernoRadius)
                            continue;

                        var dealt = target.Team == fire.Team && targetEntity != fire.Owner ? damage * 0.5f : damage;
                        target.Health = (ushort)math.max(0, (int)target.Health - (int)math.floor(dealt));
                        if (target.Health == 0)
                        {
                            target.Flags &= unchecked((byte)~NetworkPlayerFlags.Alive);
                            target.Velocity = float3.zero;
                            target.Deaths = (ushort)math.min(ushort.MaxValue, target.Deaths + 1);

                            if (target.Team != fire.Team && targetEntity != fire.Owner &&
                                state.EntityManager.Exists(fire.Owner) &&
                                state.EntityManager.HasComponent<NetworkPlayerState>(fire.Owner))
                            {
                                var owner = state.EntityManager.GetComponentData<NetworkPlayerState>(fire.Owner);
                                owner.Kills = (ushort)math.min(ushort.MaxValue, owner.Kills + 1);
                                owner.Money = (ushort)math.min(NetworkMatchRules.MaxMoney, owner.Money + 300);
                                state.EntityManager.SetComponentData(fire.Owner, owner);
                            }
                        }
                        state.EntityManager.SetComponentData(targetEntity, target);
                    }
                }

                state.EntityManager.SetComponentData(entity, fire);
            }
        }

        private static bool IsInsideSmoke(ref SystemState state, NativeArray<Entity> smokes, float3 point)
        {
            for (var i = 0; i < smokes.Length; i++)
            {
                var smoke = state.EntityManager.GetComponentData<NetworkSmokeArea>(smokes[i]);
                if (math.distancesq(smoke.Position, point) <= SmokeRadius * SmokeRadius)
                    return true;
            }
            return false;
        }

        private static bool HasLineOfSight(float3 start, float3 end)
        {
            var delta = end - start;
            var distance = math.length(delta);
            if (distance <= 0.001f)
                return true;

            return !NetworkSandlineCollision.TryRaycast(start, delta / distance, distance, out var wall) ||
                   wall.EntryDistance >= distance;
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

        private static void ApplyArmoredDamage(ref NetworkPlayerState target, float rawDamage, float armorRatio)
        {
            var healthDamage = rawDamage;
            var armorDamage = 0f;
            if (target.Armor > 0)
            {
                healthDamage = rawDamage * armorRatio;
                armorDamage = (rawDamage - healthDamage) * 0.5f;
                if (armorDamage > target.Armor)
                {
                    armorDamage = target.Armor;
                    healthDamage = rawDamage - armorDamage / 0.5f;
                }
            }

            target.Health = (ushort)math.max(0, (int)target.Health - (int)math.floor(healthDamage));
            target.Armor = (ushort)math.max(0, (int)target.Armor - (int)math.floor(armorDamage));
            if (target.Armor == 0)
                target.Flags &= unchecked((byte)~NetworkPlayerFlags.Helmet);
            if (target.Health == 0)
            {
                target.Flags &= unchecked((byte)~NetworkPlayerFlags.Alive);
                target.Velocity = float3.zero;
            }
        }

        private static void PublishDetonation(ref SystemState state, Entity owner, byte type, float3 position)
        {
            if (!state.EntityManager.Exists(owner) ||
                !state.EntityManager.HasComponent<NetworkUtilityPresentationState>(owner))
                return;

            var presentation = state.EntityManager.GetComponentData<NetworkUtilityPresentationState>(owner);
            presentation.DetonateSequence++;
            presentation.DetonateType = type;
            presentation.DetonatePosition = position;
            state.EntityManager.SetComponentData(owner, presentation);
        }
    }
}
