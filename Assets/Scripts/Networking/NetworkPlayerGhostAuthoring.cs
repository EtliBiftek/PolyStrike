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
                    Health = 100,
                    Armor = 0,
                    Flags = NetworkPlayerFlags.Alive | NetworkPlayerFlags.Grounded
                });
                AddComponent<NetworkPlayerInput>(entity);
                AddBuffer<NetworkPlayerPoseHistory>(entity);
            }
        }
    }
}
