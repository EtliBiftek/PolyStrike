using PolyStrike.Core;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace PolyStrike.Networking
{
    [UpdateInGroup(typeof(GhostInputSystemGroup))]
    public partial class GatherNetworkPlayerInputSystem : SystemBase
    {
        private const float LookSensitivity = 0.085f;

        private EntityQuery localPlayerQuery;
        private float yaw;
        private float pitch;
        private bool viewInitialized;

        protected override void OnCreate()
        {
            RequireForUpdate<NetworkPlayerInput>();
            RequireForUpdate<NetworkTime>();
            localPlayerQuery = GetEntityQuery(
                ComponentType.ReadOnly<NetworkPlayerState>(),
                ComponentType.ReadOnly<GhostOwnerIsLocal>());
        }

        protected override void OnUpdate()
        {
            if (!viewInitialized)
            {
                var states = localPlayerQuery.ToComponentDataArray<NetworkPlayerState>(Allocator.Temp);
                if (states.Length == 0)
                {
                    states.Dispose();
                    return;
                }

                yaw = states[0].Yaw;
                pitch = states[0].Pitch;
                viewInitialized = true;
                states.Dispose();
            }

            var move = GameInput.Movement;
            var lookDelta = GameInput.MouseDelta * LookSensitivity;
            yaw = WrapAngle(yaw + lookDelta.x);
            pitch = math.clamp(pitch - lookDelta.y, -89f, 89f);

            var jump = GameInput.JumpPressed;
            var firePressed = GameInput.FirePressed;
            var fireReleased = GameInput.FireReleased;
            var secondaryPressed = GameInput.SecondaryFirePressed;
            var secondaryReleased = GameInput.SecondaryFireReleased;
            var reload = GameInput.ReloadPressed;
            var drop = GameInput.DropPressed;
            var crouch = GameInput.CrouchHeld ? (byte)1 : (byte)0;
            var walk = GameInput.WalkHeld ? (byte)1 : (byte)0;
            var fire = GameInput.FireHeld ? (byte)1 : (byte)0;
            var secondaryFire = GameInput.SecondaryFireHeld ? (byte)1 : (byte)0;
            var use = GameInput.UseHeld ? (byte)1 : (byte)0;
            var slot = ResolveWeaponSlot();

            var networkTime = SystemAPI.GetSingleton<NetworkTime>();
            var subtick = QuantizeSubtick(networkTime.ServerTickFraction);

            Entities
                .WithAll<GhostOwnerIsLocal>()
                .ForEach((ref NetworkPlayerInput input) =>
                {
                    input.Move = new float2(move.x, move.y);
                    input.Look = new float2(yaw, pitch);
                    input.CrouchHeld = crouch;
                    input.WalkHeld = walk;
                    input.FireHeld = fire;
                    input.SecondaryFireHeld = secondaryFire;
                    input.UseHeld = use;
                    input.WeaponSlot = slot;

                    if (jump)
                    {
                        input.Jump.Set();
                        input.JumpSubtick = subtick;
                    }

                    if (firePressed)
                    {
                        input.FirePressed.Set();
                        input.FireSubtick = subtick;
                    }

                    if (fireReleased)
                        input.FireReleased.Set();

                    if (secondaryPressed)
                    {
                        input.SecondaryFirePressed.Set();
                        input.SecondaryFireSubtick = subtick;
                    }

                    if (secondaryReleased)
                        input.SecondaryFireReleased.Set();

                    if (reload)
                        input.Reload.Set();

                    if (drop)
                        input.Drop.Set();
                })
                .Run();
        }

        private static byte QuantizeSubtick(float fraction)
        {
            return (byte)math.clamp((int)math.round(math.saturate(fraction) * 255f), 0, 255);
        }

        private static float WrapAngle(float angle)
        {
            angle %= 360f;
            return angle < 0f ? angle + 360f : angle;
        }

        private static byte ResolveWeaponSlot()
        {
            if (GameInput.Weapon1Pressed) return 1;
            if (GameInput.Weapon2Pressed) return 2;
            if (GameInput.UtilityPressed) return 4;
            if (GameInput.BombPressed) return 5;
            if (GameInput.HeGrenadePressed) return 6;
            if (GameInput.FlashbangPressed) return 7;
            if (GameInput.SmokePressed) return 8;
            if (GameInput.MolotovPressed) return 10;
            return 0;
        }
    }
}
