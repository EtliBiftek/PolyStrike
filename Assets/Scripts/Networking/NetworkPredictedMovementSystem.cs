using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using Unity.Transforms;

namespace PolyStrike.Networking
{
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation | WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ThinClientSimulation)]
    public partial struct NetworkPredictedMovementSystem : ISystem
    {
        private const float SourceUnitsPerMeter = 39.37f;
        private const float GroundAcceleration = 5.5f;
        private const float AirAcceleration = 12f;
        private const float GroundFriction = 5.2f;
        private const float StopSpeed = 80f;
        private const float AirWishSpeedCap = 30f;
        private const float Gravity = 800f;
        private const float JumpImpulse = 301.99338f;
        private const float WalkMultiplier = 0.52f;
        private const float DuckMultiplier = 0.34f;
        private const float DuckRate = 6.4f;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
            state.RequireForUpdate(SystemAPI.QueryBuilder()
                .WithAll<NetworkPlayerState, NetworkPlayerInput, NetworkMatchSnapshot, LocalTransform>()
                .Build());
        }

        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;
            if (deltaTime <= 0f)
                return;

            foreach (var (player, input, match, transform) in
                     SystemAPI.Query<RefRW<NetworkPlayerState>, RefRO<NetworkPlayerInput>, RefRO<NetworkMatchSnapshot>, RefRW<LocalTransform>>()
                         .WithAll<Simulate>())
            {
                ref var playerState = ref player.ValueRW;
                var command = input.ValueRO;

                playerState.Yaw = WrapAngle(command.Look.x);
                playerState.Pitch = math.clamp(command.Look.y, -89f, 89f);

                var crouchTarget = command.CrouchHeld != 0 ? 1f : 0f;
                playerState.CrouchAmount = MoveTowards(playerState.CrouchAmount, crouchTarget, DuckRate * deltaTime);
                if (playerState.CrouchAmount > 0.5f)
                    playerState.Flags |= NetworkPlayerFlags.Crouching;
                else
                    playerState.Flags &= unchecked((byte)~NetworkPlayerFlags.Crouching);

                var canMove = (playerState.Flags & NetworkPlayerFlags.Alive) != 0 &&
                              (match.ValueRO.Phase == NetworkMatchPhase.Live || match.ValueRO.Phase == NetworkMatchPhase.PostPlant) &&
                              (playerState.Flags & (NetworkPlayerFlags.Planting | NetworkPlayerFlags.Defusing)) == 0;

                if (!canMove)
                {
                    playerState.Velocity = float3.zero;
                    transform.ValueRW.Position = playerState.Position;
                    transform.ValueRW.Rotation = quaternion.RotateY(math.radians(playerState.Yaw));
                    continue;
                }

                var height = math.lerp(NetworkSandlineCollision.StandingHeight, NetworkSandlineCollision.CrouchingHeight, playerState.CrouchAmount);
                var planarVelocity = playerState.Velocity.xz;
                var verticalVelocity = playerState.Velocity.y;
                var moveInput = command.Move;
                var rawInputLength = math.length(moveInput);
                var inputLength = math.min(rawInputLength, 1f);
                if (rawInputLength > 1f)
                    moveInput /= rawInputLength;

                var yawRadians = math.radians(playerState.Yaw);
                var forward = new float2(math.sin(yawRadians), math.cos(yawRadians));
                var right = new float2(forward.y, -forward.x);
                var wishDirection = forward * moveInput.y + right * moveInput.x;
                if (math.lengthsq(wishDirection) > 0.000001f)
                    wishDirection = math.normalize(wishDirection);

                var grounded = (playerState.Flags & NetworkPlayerFlags.Grounded) != 0;
                var speedModifier = playerState.VelocityModifier > 0.001f ? playerState.VelocityModifier : 1f;
                var maxSpeedSourceUnits = GetMaxSpeedSourceUnits(playerState.ActiveWeapon, playerState.Team);
                var position = playerState.Position;

                if (grounded && command.Jump.IsSet && playerState.CrouchAmount < 0.95f)
                {
                    var jumpFraction = command.JumpSubtick / 255f;
                    var beforeJump = deltaTime * jumpFraction;
                    var afterJump = deltaTime - beforeJump;

                    if (beforeJump > 0.000001f)
                    {
                        ApplyGroundMovement(
                            ref planarVelocity,
                            wishDirection,
                            inputLength,
                            maxSpeedSourceUnits,
                            speedModifier,
                            command.WalkHeld != 0,
                            playerState.CrouchAmount,
                            beforeJump);

                        var preJumpVelocity = new float3(planarVelocity.x, 0f, planarVelocity.y);
                        NetworkSandlineCollision.SimulateMove(ref position, ref preJumpVelocity, height, beforeJump, ref grounded);
                        planarVelocity = preJumpVelocity.xz;
                    }

                    if (grounded)
                    {
                        verticalVelocity = ToMeters(JumpImpulse);
                        grounded = false;
                    }

                    if (afterJump > 0.000001f)
                    {
                        ApplyAirAcceleration(
                            ref planarVelocity,
                            wishDirection,
                            inputLength,
                            maxSpeedSourceUnits,
                            speedModifier,
                            afterJump);

                        verticalVelocity -= ToMeters(Gravity) * afterJump;
                        var postJumpVelocity = new float3(planarVelocity.x, verticalVelocity, planarVelocity.y);
                        NetworkSandlineCollision.SimulateMove(ref position, ref postJumpVelocity, height, afterJump, ref grounded);
                        planarVelocity = postJumpVelocity.xz;
                        verticalVelocity = postJumpVelocity.y;
                    }
                }
                else
                {
                    if (grounded)
                    {
                        ApplyGroundMovement(
                            ref planarVelocity,
                            wishDirection,
                            inputLength,
                            maxSpeedSourceUnits,
                            speedModifier,
                            command.WalkHeld != 0,
                            playerState.CrouchAmount,
                            deltaTime);
                    }
                    else
                    {
                        ApplyAirAcceleration(
                            ref planarVelocity,
                            wishDirection,
                            inputLength,
                            maxSpeedSourceUnits,
                            speedModifier,
                            deltaTime);
                    }

                    verticalVelocity -= ToMeters(Gravity) * deltaTime;
                    var velocity = new float3(planarVelocity.x, verticalVelocity, planarVelocity.y);
                    NetworkSandlineCollision.SimulateMove(ref position, ref velocity, height, deltaTime, ref grounded);
                    planarVelocity = velocity.xz;
                    verticalVelocity = velocity.y;
                }

                playerState.Position = position;
                playerState.Velocity = new float3(planarVelocity.x, verticalVelocity, planarVelocity.y);
                if (grounded)
                    playerState.Flags |= NetworkPlayerFlags.Grounded;
                else
                    playerState.Flags &= unchecked((byte)~NetworkPlayerFlags.Grounded);

                transform.ValueRW.Position = position;
                transform.ValueRW.Rotation = quaternion.RotateY(math.radians(playerState.Yaw));
            }
        }

        private static void ApplyGroundMovement(
            ref float2 velocity,
            float2 wishDirection,
            float inputLength,
            float maxSpeedSourceUnits,
            float speedModifier,
            bool walking,
            float crouchAmount,
            float deltaTime)
        {
            ApplyGroundFriction(ref velocity, deltaTime);

            var maxSpeed = ToMeters(maxSpeedSourceUnits) * speedModifier;
            if (walking)
                maxSpeed *= WalkMultiplier;
            maxSpeed *= math.lerp(1f, DuckMultiplier, crouchAmount);

            Accelerate(ref velocity, wishDirection, maxSpeed * inputLength, GroundAcceleration, deltaTime);
        }

        private static void ApplyGroundFriction(ref float2 velocity, float deltaTime)
        {
            var speed = math.length(velocity);
            if (speed < 0.0001f)
            {
                velocity = float2.zero;
                return;
            }

            var control = math.max(speed, ToMeters(StopSpeed));
            var drop = control * GroundFriction * deltaTime;
            var nextSpeed = math.max(speed - drop, 0f);
            velocity *= nextSpeed / speed;
        }

        private static void Accelerate(
            ref float2 velocity,
            float2 direction,
            float wishSpeed,
            float acceleration,
            float deltaTime)
        {
            if (wishSpeed <= 0f || math.lengthsq(direction) < 0.000001f)
                return;

            var currentSpeed = math.dot(velocity, direction);
            var speedToAdd = wishSpeed - currentSpeed;
            if (speedToAdd <= 0f)
                return;

            var accelerationStep = acceleration * wishSpeed * deltaTime;
            velocity += direction * math.min(accelerationStep, speedToAdd);
        }

        private static void ApplyAirAcceleration(
            ref float2 velocity,
            float2 direction,
            float inputLength,
            float maxSpeedSourceUnits,
            float velocityModifier,
            float deltaTime)
        {
            if (inputLength <= 0f || math.lengthsq(direction) < 0.000001f)
                return;

            var uncappedWishSpeed = ToMeters(maxSpeedSourceUnits) * velocityModifier * inputLength;
            var cappedWishSpeed = math.min(uncappedWishSpeed, ToMeters(AirWishSpeedCap));
            var currentSpeed = math.dot(velocity, direction);
            var speedToAdd = cappedWishSpeed - currentSpeed;
            if (speedToAdd <= 0f)
                return;

            var accelerationStep = AirAcceleration * uncappedWishSpeed * deltaTime;
            velocity += direction * math.min(accelerationStep, speedToAdd);
        }

        private static float GetMaxSpeedSourceUnits(byte weapon, byte team)
        {
            return weapon switch
            {
                1 => team == 0 ? 215f : 225f,
                2 => 240f,
                4 => 245f,
                5 => 250f,
                6 or 7 or 8 or 10 => 245f,
                _ => 250f
            };
        }

        private static float ToMeters(float sourceUnits) => sourceUnits / SourceUnitsPerMeter;

        private static float MoveTowards(float current, float target, float maxDelta)
        {
            if (math.abs(target - current) <= maxDelta)
                return target;
            return current + math.sign(target - current) * maxDelta;
        }

        private static float WrapAngle(float angle)
        {
            angle %= 360f;
            return angle < 0f ? angle + 360f : angle;
        }
    }
}
