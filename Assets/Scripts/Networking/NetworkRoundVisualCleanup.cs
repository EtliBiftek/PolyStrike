using Unity.Collections;
using Unity.Entities;
using Unity.NetCode;
using UnityEngine;

namespace PolyStrike.Networking
{
    [DisallowMultipleComponent]
    public sealed class NetworkRoundVisualCleanup : MonoBehaviour
    {
        private NetworkMatchPhase lastPhase = NetworkMatchPhase.Waiting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (FindFirstObjectByType<NetworkRoundVisualCleanup>() != null)
                return;

            var root = new GameObject("PolyStrike Round Visual Cleanup");
            DontDestroyOnLoad(root);
            root.AddComponent<NetworkRoundVisualCleanup>();
        }

        private void Update()
        {
            var world = ClientServerBootstrap.ClientWorld;
            if (world == null || !world.IsCreated)
                return;

            var entityManager = world.EntityManager;
            var query = entityManager.CreateEntityQuery(
                ComponentType.ReadOnly<NetworkMatchSnapshot>(),
                ComponentType.ReadOnly<GhostOwnerIsLocal>());
            if (query.CalculateEntityCount() == 0)
            {
                query.Dispose();
                return;
            }

            var snapshots = query.ToComponentDataArray<NetworkMatchSnapshot>(Allocator.Temp);
            var phase = snapshots[0].Phase;
            snapshots.Dispose();
            query.Dispose();

            if (phase == lastPhase)
                return;

            lastPhase = phase;
            if (phase == NetworkMatchPhase.Live || phase == NetworkMatchPhase.PostPlant)
                return;

            var projectiles = FindObjectsByType<NetworkGrenadeVisual>(FindObjectsSortMode.None);
            for (var i = 0; i < projectiles.Length; i++)
            {
                if (projectiles[i] != null)
                    Destroy(projectiles[i].gameObject);
            }

            var effects = FindObjectsByType<NetworkTimedUtilityVisual>(FindObjectsSortMode.None);
            for (var i = 0; i < effects.Length; i++)
            {
                if (effects[i] != null)
                    Destroy(effects[i].gameObject);
            }
        }
    }
}
