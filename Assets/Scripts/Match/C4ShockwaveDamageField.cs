using System.Collections.Generic;
using PolyStrike.Gameplay;
using UnityEngine;

namespace PolyStrike.Match
{
    public sealed class C4ShockwaveDamageField
    {
        private const float MaximumRange = 18f;
        private const float MaximumDamage = 500f;
        private const float FalloffExponent = 1.65f;
        private const float GridStep = 0.75f;
        private const float TraceHeight = 0.65f;

        private readonly List<DamageEntry> entries = new List<DamageEntry>();

        public static C4ShockwaveDamageField Build(Vector3 origin)
        {
            var field = new C4ShockwaveDamageField();
            var healthObjects = Object.FindObjectsByType<Health>(FindObjectsSortMode.None);

            for (var i = 0; i < healthObjects.Length; i++)
            {
                var health = healthObjects[i];
                if (health == null || health.IsDead)
                    continue;

                var target = health.transform.position;
                var pathDistance = EstimatePropagationDistance(origin, target);
                if (!float.IsFinite(pathDistance) || pathDistance >= MaximumRange)
                    continue;

                var normalized = Mathf.Clamp01(pathDistance / MaximumRange);
                var damage = MaximumDamage * Mathf.Pow(1f - normalized, FalloffExponent);
                var roundedDamage = Mathf.Max(1, Mathf.FloorToInt(damage));

                field.entries.Add(new DamageEntry(health, pathDistance, roundedDamage));
            }

            return field;
        }

        public void ApplyReachedRadius(float radius)
        {
            for (var i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                if (entry.Applied || entry.Health == null || entry.Health.IsDead || entry.PathDistance > radius)
                    continue;

                entry.Applied = true;
                entry.Health.TakeDamage(entry.Damage);
            }
        }

        private static float EstimatePropagationDistance(Vector3 origin, Vector3 target)
        {
            var flatOrigin = new Vector3(origin.x, origin.y + TraceHeight, origin.z);
            var flatTarget = new Vector3(target.x, flatOrigin.y, target.z);
            var directDistance = Vector3.Distance(flatOrigin, flatTarget);

            if (directDistance >= MaximumRange)
                return float.PositiveInfinity;

            if (!SegmentBlocked(flatOrigin, flatTarget))
                return Mathf.Sqrt(directDistance * directDistance + Mathf.Pow(target.y - origin.y, 2f));

            return FindGridPath(flatOrigin, flatTarget);
        }

        private static float FindGridPath(Vector3 start, Vector3 target)
        {
            var radiusCells = Mathf.CeilToInt(MaximumRange / GridStep);
            var diameter = radiusCells * 2 + 1;
            var total = diameter * diameter;
            var distances = new float[total];
            var visited = new bool[total];

            for (var i = 0; i < distances.Length; i++)
                distances[i] = float.PositiveInfinity;

            var centerIndex = radiusCells * diameter + radiusCells;
            distances[centerIndex] = 0f;

            for (var iteration = 0; iteration < total; iteration++)
            {
                var current = -1;
                var bestDistance = float.PositiveInfinity;

                for (var i = 0; i < total; i++)
                {
                    if (visited[i] || distances[i] >= bestDistance)
                        continue;

                    current = i;
                    bestDistance = distances[i];
                }

                if (current < 0 || bestDistance > MaximumRange)
                    break;

                visited[current] = true;
                var cx = current % diameter - radiusCells;
                var cz = current / diameter - radiusCells;
                var currentWorld = start + new Vector3(cx * GridStep, 0f, cz * GridStep);

                if (Vector3.Distance(currentWorld, target) <= GridStep * 0.8f && !SegmentBlocked(currentWorld, target))
                    return bestDistance + Vector3.Distance(currentWorld, target);

                for (var dz = -1; dz <= 1; dz++)
                {
                    for (var dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dz == 0)
                            continue;

                        var nx = cx + dx;
                        var nz = cz + dz;
                        if (Mathf.Abs(nx) > radiusCells || Mathf.Abs(nz) > radiusCells)
                            continue;

                        var next = (nz + radiusCells) * diameter + (nx + radiusCells);
                        if (visited[next])
                            continue;

                        var nextWorld = start + new Vector3(nx * GridStep, 0f, nz * GridStep);
                        if (Vector3.Distance(start, nextWorld) > MaximumRange || SegmentBlocked(currentWorld, nextWorld))
                            continue;

                        var stepCost = dx != 0 && dz != 0 ? GridStep * 1.41421356f : GridStep;
                        var candidate = bestDistance + stepCost;
                        if (candidate < distances[next])
                            distances[next] = candidate;
                    }
                }
            }

            return float.PositiveInfinity;
        }

        private static bool SegmentBlocked(Vector3 start, Vector3 end)
        {
            var delta = end - start;
            var distance = delta.magnitude;
            if (distance <= 0.001f)
                return false;

            var hits = Physics.RaycastAll(start, delta / distance, distance, ~0, QueryTriggerInteraction.Ignore);
            for (var i = 0; i < hits.Length; i++)
            {
                var collider = hits[i].collider;
                if (collider == null || !IsGeometryBlocker(collider))
                    continue;

                return true;
            }

            return false;
        }

        private static bool IsGeometryBlocker(Collider collider)
        {
            if (collider.GetComponentInParent<Health>() != null)
                return false;
            if (collider.GetComponentInParent<DroppedMatchItem>() != null)
                return false;
            if (collider.GetComponentInParent<GrenadeProjectile>() != null)
                return false;
            if (collider.GetComponentInParent<BombSite>() != null)
                return false;

            var planted = C4Controller.PlantedBombTransform;
            if (planted != null && (collider.transform == planted || collider.transform.IsChildOf(planted)))
                return false;

            return true;
        }

        private sealed class DamageEntry
        {
            public Health Health { get; }
            public float PathDistance { get; }
            public int Damage { get; }
            public bool Applied { get; set; }

            public DamageEntry(Health health, float pathDistance, int damage)
            {
                Health = health;
                PathDistance = pathDistance;
                Damage = damage;
            }
        }
    }
}
