using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace PolyStrike.Networking
{
    [GhostComponent]
    public struct NetworkCombatPresentationState : IComponentData
    {
        [GhostField] public uint ShotSequence;
        [GhostField] public byte Weapon;
        [GhostField(Quantization = 1000)] public float3 Position;
    }

    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation)]
    [UpdateInGroup(typeof(PredictedFixedStepSimulationSystemGroup), OrderLast = true)]
    [UpdateAfter(typeof(NetworkServerCombatSystem))]
    public partial struct NetworkCombatPresentationSyncSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var (player, weaponRuntime, presentation) in
                     SystemAPI.Query<RefRO<NetworkPlayerState>, RefRO<NetworkWeaponRuntime>, RefRW<NetworkCombatPresentationState>>())
            {
                if (presentation.ValueRO.ShotSequence == weaponRuntime.ValueRO.TotalShots)
                    continue;

                presentation.ValueRW.ShotSequence = weaponRuntime.ValueRO.TotalShots;
                presentation.ValueRW.Weapon = player.ValueRO.ActiveWeapon;
                presentation.ValueRW.Position = player.ValueRO.Position;
            }
        }
    }
}
