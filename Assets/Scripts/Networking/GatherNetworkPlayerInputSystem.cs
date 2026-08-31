using PolyStrike.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace PolyStrike.Networking
{
    [UpdateInGroup(typeof(GhostInputSystemGroup))]
    public partial class GatherNetworkPlayerInputSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<NetworkPlayerInput>();
        }

        protected override void OnUpdate()
        {
            var move = GameInput.Movement;
            var look = GameInput.MouseDelta;
            var jump = GameInput.JumpPressed;
            var reload = GameInput.ReloadPressed;
            var crouch = GameInput.CrouchHeld ? (byte)1 : (byte)0;
            var fire = GameInput.FireHeld ? (byte)1 : (byte)0;
            var secondaryFire = GameInput.SecondaryFireHeld ? (byte)1 : (byte)0;
            var slot = ResolveWeaponSlot();

            Entities
                .WithAll<GhostOwnerIsLocal>()
                .ForEach((ref NetworkPlayerInput input) =>
                {
                    input.Move = new float2(move.x, move.y);
                    input.Look = new float2(look.x, look.y);
                    input.CrouchHeld = crouch;
                    input.FireHeld = fire;
                    input.SecondaryFireHeld = secondaryFire;
                    input.WeaponSlot = slot;

                    if (jump)
                        input.Jump.Set();
                    if (reload)
                        input.Reload.Set();
                })
                .Run();
        }

        private static byte ResolveWeaponSlot()
        {
            if (GameInput.Weapon1Pressed) return 1;
            if (GameInput.Weapon2Pressed) return 2;
            if (GameInput.UtilityPressed) return 4;
            if (GameInput.BombPressed) return 5;
            return 0;
        }
    }
}
