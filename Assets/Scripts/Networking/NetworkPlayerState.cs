using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace PolyStrike.Networking
{
    public struct NetworkPlayerState : IComponentData
    {
        [GhostField(Quantization = 1000)] public float3 Position;
        [GhostField(Quantization = 1000)] public float3 Velocity;
        [GhostField(Quantization = 100)] public float Yaw;
        [GhostField(Quantization = 100)] public float Pitch;
        [GhostField] public ushort Health;
        [GhostField] public ushort Armor;
        [GhostField] public byte Team;
        [GhostField] public byte ActiveWeapon;
        [GhostField] public byte MagazineAmmo;
        [GhostField] public byte Flags;
    }

    public static class NetworkPlayerFlags
    {
        public const byte Grounded = 1 << 0;
        public const byte Crouching = 1 << 1;
        public const byte Alive = 1 << 2;
        public const byte Planting = 1 << 3;
        public const byte Defusing = 1 << 4;
    }
}
