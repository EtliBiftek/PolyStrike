using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace PolyStrike.Networking
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(GhostAuthoringComponent))]
    public sealed class NetworkPlayerGhostAuthoring : MonoBehaviour
    {
        private void Reset()
        {
            ConfigureGhost();
        }

        private void OnValidate()
        {
            ConfigureGhost();
        }

        private void ConfigureGhost()
        {
            var ghost = GetComponent<GhostAuthoringComponent>();
            if (ghost == null)
                return;

            ghost.SupportedGhostModes = GhostModeMask.All;
            ghost.DefaultGhostMode = GhostMode.OwnerPredicted;
            ghost.OptimizationMode = GhostOptimizationMode.Dynamic;
            ghost.HasOwner = true;
            ghost.SupportAutoCommandTarget = true;
            ghost.TrackInterpolationDelay = true;
            ghost.Importance = 100;
        }

        private sealed class Baker : Baker<NetworkPlayerGhostAuthoring>
        {
            public override void Bake(NetworkPlayerGhostAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new NetworkPlayerState
                {
                    VelocityModifier = 1f,
                    Health = 100,
                    Armor = 0,
                    Money = NetworkMatchRules.StartMoney,
                    Flags = NetworkPlayerFlags.Alive | NetworkPlayerFlags.Grounded
                });
                AddComponent<NetworkPlayerInput>(entity);
                AddComponent<NetworkWeaponRuntime>(entity);
                AddComponent<NetworkTagState>(entity);
                AddComponent<NetworkInteractionState>(entity);
                AddComponent<NetworkMatchSnapshot>(entity);
                AddComponent<NetworkLoadoutState>(entity);
                AddComponent<NetworkUtilityRuntime>(entity);
                AddComponent<NetworkUtilityPresentationState>(entity);
                AddComponent<NetworkFlashState>(entity);
                AddBuffer<NetworkPlayerPoseHistory>(entity);
            }
        }
    }
}
