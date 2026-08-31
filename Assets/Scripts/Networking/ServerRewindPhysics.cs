using Unity.Mathematics;
using Unity.NetCode;
using Unity.Physics;

namespace PolyStrike.Networking
{
    public static class ServerRewindPhysics
    {
        public static bool CastWorld(
            in PhysicsWorldHistorySingleton history,
            in PhysicsWorldSingleton currentWorld,
            NetworkTick shotTick,
            uint interpolationDelay,
            float3 start,
            float3 end,
            out Unity.Physics.RaycastHit hit)
        {
            var physicsWorld = currentWorld.PhysicsWorld;
            history.GetCollisionWorldFromTick(
                shotTick,
                interpolationDelay,
                ref physicsWorld,
                out var collisionWorld);

            var input = new RaycastInput
            {
                Start = start,
                End = end,
                Filter = CollisionFilter.Default
            };

            return collisionWorld.CastRay(input, out hit);
        }

        public static uint ClampInterpolationDelay(uint reportedDelay)
        {
            // Never rewind farther than CS2's current 200 ms max-unlag window.
            const uint maxTicks = 13;
            return math.min(reportedDelay, maxTicks);
        }
    }
}
