using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace PolyStrike.Networking
{
    public struct NetworkPlayerPoseHistory : IBufferElementData
    {
        public NetworkTick Tick;
        public float3 Position;
        public float Yaw;
        public float Pitch;
        public float CrouchAmount;
    }

    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(ServerSimulationSystemGroup), OrderLast = true)]
    public partial struct ServerPlayerPoseHistorySystem : ISystem
    {
        // CS2 currently caps server-side unlag at 0.200 seconds. At 64 Hz that is 12.8 ticks;
        // sixteen samples leave a little room for the edge ticks used for interpolation.
        public const int HistoryCapacity = 16;

        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<NetworkTime>();
        }

        public void OnUpdate(ref SystemState state)
        {
            var tick = SystemAPI.GetSingleton<NetworkTime>().ServerTick;
            if (!tick.IsValid)
                return;

            var histories = SystemAPI.GetBufferLookup<NetworkPlayerPoseHistory>(false);
            foreach (var (player, entity) in SystemAPI.Query<RefRO<NetworkPlayerState>>().WithEntityAccess())
            {
                if (!histories.HasBuffer(entity))
                    continue;

                var history = histories[entity];
                if (history.Length > 0 && history[history.Length - 1].Tick == tick)
                    continue;

                if (history.Length >= HistoryCapacity)
                    history.RemoveAt(0);

                history.Add(new NetworkPlayerPoseHistory
                {
                    Tick = tick,
                    Position = player.ValueRO.Position,
                    Yaw = player.ValueRO.Yaw,
                    Pitch = player.ValueRO.Pitch,
                    CrouchAmount = player.ValueRO.CrouchAmount
                });
            }
        }
    }

    public static class SubtickPoseRewind
    {
        public static bool TrySample(
            in DynamicBuffer<NetworkPlayerPoseHistory> history,
            NetworkTick shotTick,
            byte subtick,
            out NetworkPlayerPoseHistory pose)
        {
            pose = default;
            if (!shotTick.IsValid || history.Length == 0)
                return false;

            var olderIndex = -1;
            var newerIndex = -1;

            for (var i = history.Length - 1; i >= 0; i--)
            {
                var sampleTick = history[i].Tick;
                if (!sampleTick.IsValid)
                    continue;

                if (sampleTick == shotTick || shotTick.IsNewerThan(sampleTick))
                {
                    olderIndex = i;
                    newerIndex = i + 1 < history.Length ? i + 1 : i;
                    break;
                }
            }

            if (olderIndex < 0)
                return false;

            var older = history[olderIndex];
            var newer = history[newerIndex];
            if (newerIndex == olderIndex || newer.Tick.TicksSince(older.Tick) != 1)
            {
                pose = older;
                return true;
            }

            var t = subtick / 255f;
            pose = older;
            pose.Position = math.lerp(older.Position, newer.Position, t);
            pose.Yaw = LerpAngle(older.Yaw, newer.Yaw, t);
            pose.Pitch = math.lerp(older.Pitch, newer.Pitch, t);
            pose.CrouchAmount = math.lerp(older.CrouchAmount, newer.CrouchAmount, t);
            return true;
        }

        private static float LerpAngle(float a, float b, float t)
        {
            var delta = math.fmod(b - a + 540f, 360f) - 180f;
            return a + delta * t;
        }
    }
}
