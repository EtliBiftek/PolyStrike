using Unity.Mathematics;
using Unity.NetCode;

namespace PolyStrike.Networking
{
    public struct NetworkPlayerInput : IInputComponentData
    {
        [GhostField(Quantization = 1000)] public float2 Move;
        [GhostField(Quantization = 100)] public float2 Look;
        [GhostField] public InputEvent Jump;
        [GhostField] public InputEvent Crouch;
        [GhostField] public InputEvent Fire;
        [GhostField] public InputEvent SecondaryFire;
        [GhostField] public InputEvent Reload;
        [GhostField] public byte WeaponSlot;
    }
}
