using Unity.Mathematics;

namespace PolyStrike.Maps
{
    public enum SandlineSurface : byte
    {
        Concrete,
        Wood
    }

    public readonly struct SandlineBlock
    {
        public readonly float3 Center;
        public readonly float3 Size;
        public readonly SandlineSurface Surface;

        public SandlineBlock(float3 center, float3 size, SandlineSurface surface = SandlineSurface.Concrete)
        {
            Center = center;
            Size = size;
            Surface = surface;
        }
    }

    public static class SandlineLayout
    {
        public const float GroundY = 0.05f;
        public const float Boundary = 36.5f;

        public static readonly float3 ASiteCenter = new float3(21f, 0.08f, 20f);
        public static readonly float3 BSiteCenter = new float3(-21f, 0.08f, 20f);
        public static readonly float2 ASiteHalfExtents = new float2(4.5f, 4.1f);
        public static readonly float2 BSiteHalfExtents = new float2(4.4f, 4.1f);

        public static readonly float3 MidControl = new float3(0f, GroundY, 5.5f);
        public static readonly float3 LongControl = new float3(21.5f, GroundY, -1.5f);
        public static readonly float3 ShortControl = new float3(8.3f, GroundY, 11.8f);
        public static readonly float3 TunnelControl = new float3(-21.5f, GroundY, -1f);
        public static readonly float3 MidDoors = new float3(0f, GroundY, 9.5f);
        public static readonly float3 CtMid = new float3(0f, GroundY, 18.2f);
        public static readonly float3 ALongEntry = new float3(18.5f, GroundY, 9f);
        public static readonly float3 AShortEntry = new float3(14.1f, GroundY, 15.3f);
        public static readonly float3 BTunnelEntry = new float3(-16.2f, GroundY, 13.2f);
        public static readonly float3 BMidEntry = new float3(-10.5f, GroundY, 15.2f);

        public static readonly float3[] TSpawns =
        {
            new float3(-2.4f, GroundY, -31.5f),
            new float3(-1.2f, GroundY, -31.5f),
            new float3(0f, GroundY, -31.5f),
            new float3(1.2f, GroundY, -31.5f),
            new float3(2.4f, GroundY, -31.5f)
        };

        public static readonly float3[] CTSpawns =
        {
            new float3(-2.4f, GroundY, 31.5f),
            new float3(-1.2f, GroundY, 31.5f),
            new float3(0f, GroundY, 31.5f),
            new float3(1.2f, GroundY, 31.5f),
            new float3(2.4f, GroundY, 31.5f)
        };

        public static readonly SandlineBlock[] SolidBlocks =
        {
            new(new float3(0f, 1.6f, Boundary), new float3(74f, 3.2f, 1f)),
            new(new float3(0f, 1.6f, -Boundary), new float3(74f, 3.2f, 1f)),
            new(new float3(-Boundary, 1.6f, 0f), new float3(1f, 3.2f, 74f)),
            new(new float3(Boundary, 1.6f, 0f), new float3(1f, 3.2f, 74f)),

            // Long / A side. The split opening between the two divider pieces is the short connector.
            new(new float3(9.2f, 1.5f, -8f), new float3(1f, 3f, 26f)),
            new(new float3(9.2f, 1.5f, 22f), new float3(1f, 3f, 16f)),
            new(new float3(29f, 1.8f, 1f), new float3(6f, 3.6f, 42f)),
            new(new float3(31f, 1.6f, 28.5f), new float3(10f, 3.2f, 1f)),
            new(new float3(16f, 1.5f, 28.5f), new float3(10f, 3f, 1f)),

            // Tunnel / B side. The bend prevents a single spawn-to-site sightline.
            new(new float3(-9.4f, 1.5f, -8f), new float3(1f, 3f, 25f)),
            new(new float3(-9.4f, 1.5f, 22f), new float3(1f, 3f, 16f)),
            new(new float3(-29f, 1.8f, 1f), new float3(6f, 3.6f, 42f)),
            new(new float3(-21f, 1.5f, 7.5f), new float3(10f, 3f, 1f)),
            new(new float3(-31f, 1.6f, 28.5f), new float3(10f, 3.2f, 1f)),
            new(new float3(-16f, 1.5f, 28.5f), new float3(10f, 3f, 1f)),

            // Mid. Two narrow gates create a meaningful early duel but leave enough room for utility.
            new(new float3(-5.5f, 1.5f, 9.5f), new float3(8.6f, 3f, 1f)),
            new(new float3(5.5f, 1.5f, 9.5f), new float3(8.6f, 3f, 1f)),
            new(new float3(-5f, 1.5f, -14f), new float3(8f, 3f, 1f)),
            new(new float3(5f, 1.5f, -14f), new float3(8f, 3f, 1f)),
            new(new float3(-2.4f, 1.5f, 14.5f), new float3(1.4f, 3f, 1.2f)),
            new(new float3(2.4f, 1.5f, 14.5f), new float3(1.4f, 3f, 1.2f)),

            // Site and lane cover. Wood is intentionally penetrable while the larger anchors are concrete.
            new(new float3(22.8f, 0.75f, 20.5f), new float3(1.5f, 1.4f, 1.5f), SandlineSurface.Wood),
            new(new float3(19.0f, 0.65f, 22.4f), new float3(1.3f, 1.2f, 2.2f)),
            new(new float3(16.5f, 0.62f, 16.5f), new float3(1.2f, 1.15f, 1.8f), SandlineSurface.Wood),
            new(new float3(24.2f, 0.60f, 10f), new float3(1.6f, 1.1f, 2.6f)),
            new(new float3(15.0f, 0.70f, -6.5f), new float3(1.5f, 1.3f, 1.5f)),

            new(new float3(-23.0f, 0.75f, 20.5f), new float3(2.4f, 1.4f, 1.3f), SandlineSurface.Wood),
            new(new float3(-18.5f, 0.65f, 18.2f), new float3(1.3f, 1.2f, 2.2f)),
            new(new float3(-16.4f, 0.62f, 13.0f), new float3(1.2f, 1.15f, 1.8f), SandlineSurface.Wood),
            new(new float3(-24.0f, 0.60f, 10.5f), new float3(1.5f, 1.1f, 2.4f)),

            new(new float3(3.0f, 0.62f, 3.5f), new float3(1.25f, 1.15f, 1.25f), SandlineSurface.Wood),
            new(new float3(-3.2f, 0.58f, 18.5f), new float3(1.2f, 1.05f, 1.8f)),
            new(new float3(7.8f, 0.58f, 11.8f), new float3(1.0f, 1.05f, 1.4f), SandlineSurface.Wood),
            new(new float3(-7.8f, 0.58f, 12.2f), new float3(1.0f, 1.05f, 1.4f), SandlineSurface.Wood)
        };

        public static float3 GetSpawn(byte team, int slot)
        {
            var index = math.clamp(slot, 0, 4);
            return team == 0 ? TSpawns[index] : CTSpawns[index];
        }

        public static byte FindBombSite(float3 position)
        {
            if (math.abs(position.x - ASiteCenter.x) <= ASiteHalfExtents.x &&
                math.abs(position.z - ASiteCenter.z) <= ASiteHalfExtents.y)
                return 0;

            if (math.abs(position.x - BSiteCenter.x) <= BSiteHalfExtents.x &&
                math.abs(position.z - BSiteCenter.z) <= BSiteHalfExtents.y)
                return 1;

            return byte.MaxValue;
        }
    }
}
