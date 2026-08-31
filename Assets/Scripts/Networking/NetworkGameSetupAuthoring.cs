using Unity.Entities;
using UnityEngine;

namespace PolyStrike.Networking
{
    public struct NetworkGameSetup : IComponentData
    {
        public Entity PlayerGhostPrefab;
    }

    public sealed class NetworkGameSetupAuthoring : MonoBehaviour
    {
        public GameObject PlayerGhostPrefab;

        private sealed class Baker : Baker<NetworkGameSetupAuthoring>
        {
            public override void Bake(NetworkGameSetupAuthoring authoring)
            {
                if (authoring.PlayerGhostPrefab == null)
                    return;

                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new NetworkGameSetup
                {
                    PlayerGhostPrefab = GetEntity(authoring.PlayerGhostPrefab, TransformUsageFlags.Dynamic)
                });
            }
        }
    }
}
