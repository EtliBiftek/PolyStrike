using Unity.Mathematics;
using Unity.NetCode;

namespace PolyStrike.Networking
{
    public struct NetworkPlayerInput : IInputComponentData
    {
        [GhostField(Quantization = 1000)] public float2 Move;
        [GhostField(Quantization = 100)] public float2 Look;

        [GhostField] public InputEvent Jump;
        [GhostField] public InputEvent FirePressed;
        [GhostField] public InputEvent FireReleased;
        [GhostField] public InputEvent SecondaryFirePressed;
        [GhostField] public InputEvent SecondaryFireReleased;
        [GhostField] public InputEvent Reload;
        [GhostField] public InputEvent Drop;

        [GhostField] public byte JumpSubtick;
        [GhostField] public byte FireSubtick;
        [GhostField] public byte SecondaryFireSubtick;
        [GhostField] public byte CrouchHeld;
        [GhostField] public byte WalkHeld;
        [GhostField] public byte FireHeld;
        [GhostField] public byte SecondaryFireHeld;
        [GhostField] public byte UseHeld;
        [GhostField] public byte WeaponSlot;
    }
}
