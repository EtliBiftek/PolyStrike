using Unity.Mathematics;

namespace PolyStrike.Networking
{
    /// <summary>
    /// Sandline is intentionally built from axis-aligned low-poly blocks. Keeping the online collision
    /// description as plain data lets prediction and the server resolve the exact same geometry.
    /// </summary>
    public static class NetworkSandlineCollision
    {
        public const float PlayerRadius = 0.35f;
        public const float StandingHeight = 1.829f;
        public const float CrouchingHeight = 1.372f;
        public const float GroundY = 0.05f;
        public const float StepHeight = 0.30f;

        public enum Material : byte
        {
            Concrete,
            Wood
        }

        public readonly struct Block
        {
            public readonly float3 Center;
            public readonly float3 Size;
            public readonly Material Surface;

            public Block(float3 center, float3 size, Material surface = Material.Concrete)
            {
                Center = center;
                Size = size;
                Surface = surface;
            }

            public float3 Min => Center - Size * 0.5f;
            public float3 Max => Center + Size * 0.5f;
        }

        public readonly struct RayHit
        {
            public readonly float EntryDistance;
            public readonly float ExitDistance;
            public readonly Material Surface;

            public RayHit(float entryDistance, float exitDistance, Material surface)
            {
                EntryDistance = entryDistance;
                ExitDistance = exitDistance;
                Surface = surface;
            }

            public float Thickness => math.max(0f, ExitDistance - EntryDistance);
        }

        private static readonly Block[] Blocks =
        {
            new(new float3(0f, 1.5f, 29.5f), new float3(60f, 3f, 1f)),
            new(new float3(0f, 1.5f, -29.5f), new float3(60f, 3f, 1f)),
            new(new float3(-29.5f, 1.5f, 0f), new float3(1f, 3f, 60f)),
            new(new float3(29.5f, 1.5f, 0f), new float3(1f, 3f, 60f)),

            new(new float3(8.2f, 1.5f, -8f), new float3(1f, 3f, 20f)),
            new(new float3(8.2f, 1.5f, 17f), new float3(1f, 3f, 12f)),
            new(new float3(24f, 1.7f, 2f), new float3(4f, 3.4f, 32f)),

            new(new float3(-8.5f, 1.5f, -7f), new float3(1f, 3f, 22f)),
            new(new float3(-8.5f, 1.5f, 18f), new float3(1f, 3f, 10f)),
            new(new float3(-24f, 1.7f, 2f), new float3(4f, 3.4f, 31f)),
            new(new float3(-16f, 1.5f, 7.5f), new float3(8f, 3f, 1f)),

            new(new float3(-4.8f, 1.5f, 8f), new float3(7.4f, 3f, 1f)),
            new(new float3(4.8f, 1.5f, 8f), new float3(7.4f, 3f, 1f)),
            new(new float3(-4.5f, 1.5f, -12f), new float3(8f, 3f, 1f)),
            new(new float3(4.5f, 1.5f, -12f), new float3(8f, 3f, 1f)),
            new(new float3(-1.8f, 1.5f, 13f), new float3(1.4f, 3f, 1.2f)),
            new(new float3(1.8f, 1.5f, 13f), new float3(1.4f, 3f, 1.2f)),

            new(new float3(18.7f, 0.73f, 14.9f), new float3(1.4f, 1.3f, 1.4f), Material.Wood),
            new(new float3(14.6f, 0.63f, 16.6f), new float3(1.2f, 1.1f, 2f)),
            new(new float3(-18f, 0.73f, 15.5f), new float3(2.2f, 1.3f, 1.2f), Material.Wood),
            new(new float3(-14.5f, 0.63f, 13.2f), new float3(1.2f, 1.1f, 2f)),
            new(new float3(2.4f, 0.55f, 2.5f), new float3(1.2f, 1.1f, 1.2f), Material.Wood),
            new(new float3(13f, 0.65f, -5f), new float3(1.4f, 1.3f, 1.4f))
        };

        public static void SimulateMove(
            ref float3 position,
            ref float3 velocity,
            float height,
            float deltaTime,
            ref bool grounded)
        {
            var previousY = position.y;
            var targetY = previousY + velocity.y * deltaTime;
            grounded = false;

            var support = FindLandingHeight(position.xz, previousY, targetY);
            if (velocity.y <= 0f && support >= GroundY && targetY <= support)
            {
                position.y = support;
                velocity.y = 0f;
                grounded = true;
            }
            else
            {
                position.y = math.max(targetY, GroundY);
                if (position.y <= GroundY + 0.0001f && velocity.y <= 0f)
                {
                    position.y = GroundY;
                    velocity.y = 0f;
                    grounded = true;
                }
            }

            var displacement = velocity.xz * deltaTime;
            ResolveAxis(ref position, ref velocity, displacement.x, true, height);
            ResolveAxis(ref position, ref velocity, displacement.y, false, height);

            if (!grounded)
            {
                var standingOn = FindSupportHeight(position.xz, position.y);
                if (math.abs(position.y - standingOn) <= 0.012f && velocity.y <= 0f)
                {
                    position.y = standingOn;
                    velocity.y = 0f;
                    grounded = true;
                }
            }
        }

        public static bool TryRaycast(float3 origin, float3 direction, float maxDistance, out RayHit hit)
        {
            hit = default;
            var bestEntry = maxDistance + 1f;
            var found = false;
            direction = math.normalizesafe(direction);

            for (var i = 0; i < Blocks.Length; i++)
            {
                var block = Blocks[i];
                if (!RayAabb(origin, direction, block.Min, block.Max, out var entry, out var exit))
                    continue;

                if (exit < 0f || entry > maxDistance)
                    continue;

                entry = math.max(0f, entry);
                exit = math.min(maxDistance, exit);
                if (entry >= bestEntry)
                    continue;

                bestEntry = entry;
                hit = new RayHit(entry, exit, block.Surface);
                found = true;
            }

            return found;
        }

        public static float PenetrationModifier(Material material) => material == Material.Wood ? 0.9f : 0.5f;
        public static float DamageLossModifier(Material material) => 0.16f;

        private static void ResolveAxis(ref float3 position, ref float3 velocity, float delta, bool xAxis, float height)
        {
            if (math.abs(delta) < 0.000001f)
                return;

            var start = xAxis ? position.x : position.z;
            var target = start + delta;
            var perpendicular = xAxis ? position.z : position.x;
            var direction = math.sign(delta);

            for (var i = 0; i < Blocks.Length; i++)
            {
                var block = Blocks[i];
                if (!VerticalOverlap(position.y, height, block))
                    continue;

                var min = block.Min;
                var max = block.Max;
                var perpendicularMin = (xAxis ? min.z : min.x) - PlayerRadius;
                var perpendicularMax = (xAxis ? max.z : max.x) + PlayerRadius;
                if (perpendicular < perpendicularMin || perpendicular > perpendicularMax)
                    continue;

                var axisMin = (xAxis ? min.x : min.z) - PlayerRadius;
                var axisMax = (xAxis ? max.x : max.z) + PlayerRadius;
                var top = max.y;

                if (top - position.y <= StepHeight && top >= position.y - 0.01f)
                {
                    var headAfterStep = top + height;
                    if (!HasCeiling(position.xz, top, headAfterStep, i))
                    {
                        position.y = top;
                        continue;
                    }
                }

                if (direction > 0f)
                {
                    if (start <= axisMin && target > axisMin)
                        target = math.min(target, axisMin);
                    else if (start > axisMin && start < axisMax)
                        target = math.min(target, axisMin);
                }
                else
                {
                    if (start >= axisMax && target < axisMax)
                        target = math.max(target, axisMax);
                    else if (start > axisMin && start < axisMax)
                        target = math.max(target, axisMax);
                }
            }

            if (xAxis)
            {
                position.x = target;
                if (math.abs(target - (start + delta)) > 0.00001f)
                    velocity.x = 0f;
            }
            else
            {
                position.z = target;
                if (math.abs(target - (start + delta)) > 0.00001f)
                    velocity.z = 0f;
            }
        }

        private static float FindLandingHeight(float2 horizontalPosition, float previousY, float targetY)
        {
            var support = GroundY;
            for (var i = 0; i < Blocks.Length; i++)
            {
                var block = Blocks[i];
                var top = block.Max.y;
                if (top > previousY + 0.01f || top < targetY - 0.01f)
                    continue;

                if (CircleOverlapsRect(horizontalPosition, block.Min.xz, block.Max.xz, PlayerRadius))
                    support = math.max(support, top);
            }

            return support;
        }

        private static float FindSupportHeight(float2 horizontalPosition, float feetY)
        {
            var support = GroundY;
            for (var i = 0; i < Blocks.Length; i++)
            {
                var block = Blocks[i];
                var top = block.Max.y;
                if (top > feetY + 0.02f)
                    continue;

                if (CircleOverlapsRect(horizontalPosition, block.Min.xz, block.Max.xz, PlayerRadius))
                    support = math.max(support, top);
            }

            return support;
        }

        private static bool HasCeiling(float2 horizontalPosition, float feetY, float headY, int ignoredBlock)
        {
            for (var i = 0; i < Blocks.Length; i++)
            {
                if (i == ignoredBlock)
                    continue;

                var block = Blocks[i];
                if (block.Max.y <= feetY || block.Min.y >= headY)
                    continue;

                if (CircleOverlapsRect(horizontalPosition, block.Min.xz, block.Max.xz, PlayerRadius))
                    return true;
            }

            return false;
        }

        private static bool VerticalOverlap(float feetY, float height, in Block block)
        {
            var headY = feetY + height;
            return headY > block.Min.y + 0.001f && feetY < block.Max.y - 0.001f;
        }

        private static bool CircleOverlapsRect(float2 center, float2 min, float2 max, float radius)
        {
            var closest = math.clamp(center, min, max);
            return math.lengthsq(center - closest) <= radius * radius;
        }

        private static bool RayAabb(float3 origin, float3 direction, float3 min, float3 max, out float entry, out float exit)
        {
            entry = 0f;
            exit = float.MaxValue;

            for (var axis = 0; axis < 3; axis++)
            {
                var d = direction[axis];
                if (math.abs(d) < 0.000001f)
                {
                    if (origin[axis] < min[axis] || origin[axis] > max[axis])
                        return false;
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
                    return false;
            }

            return true;
        }
    }
}
