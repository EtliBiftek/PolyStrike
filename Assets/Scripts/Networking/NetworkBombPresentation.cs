using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;
using UnityEngine;

namespace PolyStrike.Networking
{
    [DisallowMultipleComponent]
    public sealed class NetworkBombPresentation : MonoBehaviour
    {
        private readonly Dictionary<Entity, uint> lastDropSequence = new Dictionary<Entity, uint>();
        private readonly Dictionary<Entity, uint> lastPickupSequence = new Dictionary<Entity, uint>();
        private readonly List<GameObject> droppedBombs = new List<GameObject>();
        private NetworkMatchPhase lastPhase = NetworkMatchPhase.Waiting;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureExists()
        {
            if (FindFirstObjectByType<NetworkBombPresentation>() != null)
                return;

            var root = new GameObject("PolyStrike Bomb Presentation");
            DontDestroyOnLoad(root);
            root.AddComponent<NetworkBombPresentation>();
        }

        private void Update()
        {
            var world = ClientServerBootstrap.ClientWorld;
            if (world == null || !world.IsCreated)
            {
                ClearBombs();
                return;
            }

            var entityManager = world.EntityManager;
            var query = entityManager.CreateEntityQuery(ComponentType.ReadOnly<NetworkBombPresentationState>());
            var entities = query.ToEntityArray(Allocator.Temp);

            var phaseKnown = false;
            var phase = NetworkMatchPhase.Waiting;
            for (var i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                var state = entityManager.GetComponentData<NetworkBombPresentationState>(entity);

                if (entityManager.HasComponent<NetworkMatchSnapshot>(entity) && !phaseKnown)
                {
                    phase = entityManager.GetComponentData<NetworkMatchSnapshot>(entity).Phase;
                    phaseKnown = true;
                }

                if (!lastDropSequence.TryGetValue(entity, out var dropSequence))
                {
                    lastDropSequence[entity] = state.DropSequence;
                    lastPickupSequence[entity] = state.PickupSequence;
                    continue;
                }

                if (state.DropSequence != dropSequence)
                {
                    lastDropSequence[entity] = state.DropSequence;
                    SpawnBomb(state.DropPosition);
                }

                var pickupSequence = lastPickupSequence.TryGetValue(entity, out var knownPickup) ? knownPickup : 0u;
                if (state.PickupSequence != pickupSequence)
                {
                    lastPickupSequence[entity] = state.PickupSequence;
                    RemoveNearestBomb(state.PickupPosition);
                }
            }

            if (phaseKnown && lastPhase != phase)
            {
                lastPhase = phase;
                if (phase != NetworkMatchPhase.Live && phase != NetworkMatchPhase.PostPlant)
                    ClearBombs();
            }

            entities.Dispose();
            query.Dispose();
        }

        private void SpawnBomb(float3 position)
        {
            var bomb = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bomb.name = "Network Dropped C4";
            bomb.transform.position = new Vector3(position.x, position.y, position.z);
            bomb.transform.localScale = new Vector3(0.34f, 0.12f, 0.24f);

            var collider = bomb.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            var renderer = bomb.GetComponent<Renderer>();
            if (renderer != null)
            {
                var material = renderer.material;
                var color = new Color(0.20f, 0.16f, 0.07f);
                if (material.HasProperty("_BaseColor"))
                    material.SetColor("_BaseColor", color);
                else
                    material.color = color;
            }

            droppedBombs.Add(bomb);
        }

        private void RemoveNearestBomb(float3 position)
        {
            var target = new Vector3(position.x, position.y, position.z);
            var bestIndex = -1;
            var bestDistance = float.PositiveInfinity;
            for (var i = 0; i < droppedBombs.Count; i++)
            {
                var bomb = droppedBombs[i];
                if (bomb == null)
                    continue;

                var distance = (bomb.transform.position - target).sqrMagnitude;
                if (distance >= bestDistance)
                    continue;

                bestDistance = distance;
                bestIndex = i;
            }

            if (bestIndex < 0)
                return;

            var selected = droppedBombs[bestIndex];
            droppedBombs.RemoveAt(bestIndex);
            if (selected != null)
                Destroy(selected);
        }

        private void ClearBombs()
        {
            for (var i = 0; i < droppedBombs.Count; i++)
            {
                if (droppedBombs[i] != null)
                    Destroy(droppedBombs[i]);
            }
            droppedBombs.Clear();
        }
    }
}
