using PolyStrike.Match;
using UnityEngine;

namespace PolyStrike.Gameplay
{
    public sealed class GrenadeProjectile : MonoBehaviour
    {
        private const float Substep = 1f / 128f;
        private const float MaxSimulationCatchup = 0.05f;

        private GrenadeType grenadeType;
        private Vector3 velocitySourceUnits;
        private Vector3 spinDegreesPerSecond;
        private Transform owner;
        private MatchParticipant ownerParticipant;
        private bool incendiary;
        private float accumulator;
        private float elapsed;
        private float nextSmokeThink;
        private bool atRest;
        private bool detonated;

        public GrenadeType Type => grenadeType;
        public Vector3 VelocitySourceUnits => velocitySourceUnits;

        public void Initialize(
            GrenadeType type,
            Vector3 spawnPosition,
            Vector3 launchVelocitySourceUnits,
            Transform thrower,
            bool isIncendiary = false)
        {
            grenadeType = type;
            transform.position = spawnPosition;
            velocitySourceUnits = launchVelocitySourceUnits;
            spinDegreesPerSecond = new Vector3(600f, Random.Range(-1200f, 1200f), 0f);
            owner = thrower;
            ownerParticipant = thrower != null ? thrower.GetComponent<MatchParticipant>() : null;
            incendiary = isIncendiary;
            nextSmokeThink = GrenadeRules.SmokeArmTime;
        }

        private void Update()
        {
            if (detonated)
                return;

            transform.Rotate(spinDegreesPerSecond * Time.deltaTime, Space.World);

            accumulator = Mathf.Min(accumulator + Time.deltaTime, MaxSimulationCatchup);
            while (accumulator >= Substep && !detonated)
            {
                SimulateSubstep(Substep);
                accumulator -= Substep;
            }
        }

        private void SimulateSubstep(float dt)
        {
            elapsed += dt;

            if (!atRest)
            {
                var nextVelocity = velocitySourceUnits;
                nextVelocity.y -= GrenadeRules.Gravity * dt;
                var averageVelocity = (velocitySourceUnits + nextVelocity) * 0.5f;
                var displacement = SourceUnit.ToMeters(averageVelocity * dt);

                if (displacement.sqrMagnitude > 0f && TrySweep(displacement, out var hit))
                {
                    var travel = Mathf.Max(hit.distance - SourceUnit.ToMeters(1f / 32f), 0f);
                    transform.position += displacement.normalized * travel;
                    velocitySourceUnits = nextVelocity;
                    ResolveCollision(hit);
                }
                else
                {
                    transform.position += displacement;
                    velocitySourceUnits = nextVelocity;
                }
            }

            UpdateFuse();
        }

        private bool TrySweep(Vector3 displacement, out RaycastHit bestHit)
        {
            bestHit = default;
            var distance = displacement.magnitude;
            if (distance <= 0.00001f)
                return false;

            var halfExtent = SourceUnit.ToMeters(GrenadeRules.ProjectileHalfExtent);
            var hits = Physics.BoxCastAll(
                transform.position,
                Vector3.one * halfExtent,
                displacement / distance,
                Quaternion.identity,
                distance,
                ~0,
                QueryTriggerInteraction.Ignore);

            var bestDistance = float.PositiveInfinity;
            var found = false;

            for (var i = 0; i < hits.Length; i++)
            {
                var collider = hits[i].collider;
                if (collider == null || IsOwnerCollider(collider))
                    continue;

                if (hits[i].distance >= bestDistance)
                    continue;

                bestDistance = hits[i].distance;
                bestHit = hits[i];
                found = true;
            }

            return found;
        }

        private bool IsOwnerCollider(Collider collider)
        {
            if (owner == null)
                return false;

            var target = collider.transform;
            return target == owner || target.IsChildOf(owner);
        }

        private void ResolveCollision(RaycastHit hit)
        {
            var hitHealth = hit.collider.GetComponentInParent<Health>();
            var hitPlayer = hitHealth != null && hitHealth.transform != owner;

            if (hitPlayer)
            {
                var damage = hitHealth.Armor > 0f ? 1f : 2f;
                var victim = hitHealth.GetComponent<MatchParticipant>();
                if (ownerParticipant != null && victim != null && victim.Team == ownerParticipant.Team)
                    damage *= 0.4f;

                // Grenade impact has tiny integer damage; keep a non-zero friendly bump when it connects.
                if (damage > 0f && damage < 1f)
                    damage = 1f;

                hitHealth.TakeDamage(damage);
            }
            else if (grenadeType == GrenadeType.Molotov)
            {
                var minimumFloorNormal = Mathf.Cos(GrenadeRules.MolotovMaxSlope * Mathf.Deg2Rad);
                if (hit.normal.y >= minimumFloorNormal)
                {
                    Detonate();
                    return;
                }
            }

            velocitySourceUnits = Vector3.Reflect(velocitySourceUnits, hit.normal) * GrenadeRules.BounceScale;
            spinDegreesPerSecond *= GrenadeRules.BounceScale;
            GrenadeEffects.PlayBounce(transform.position, velocitySourceUnits.magnitude, ResolveSurface(hit.collider));

            if (!hitPlayer && hit.normal.y > 0.1f && velocitySourceUnits.magnitude < GrenadeRules.RestSpeed)
            {
                velocitySourceUnits = Vector3.zero;
                spinDegreesPerSecond = Vector3.zero;
                atRest = true;
            }
        }

        private void UpdateFuse()
        {
            switch (grenadeType)
            {
                case GrenadeType.HighExplosive:
                case GrenadeType.Flashbang:
                    if (elapsed >= GrenadeRules.HeFlashFuse)
                        Detonate();
                    break;

                case GrenadeType.Molotov:
                    if (elapsed >= GrenadeRules.MolotovAirFuse)
                        Detonate();
                    break;

                case GrenadeType.Smoke:
                    if (elapsed < nextSmokeThink)
                        break;

                    if (atRest)
                    {
                        Detonate();
                        break;
                    }

                    nextSmokeThink += 0.2f;
                    break;
            }
        }

        private void Detonate()
        {
            if (detonated)
                return;

            detonated = true;
            GrenadeEffects.Detonate(grenadeType, transform.position, ownerParticipant, incendiary);
            Destroy(gameObject);
        }

        private static SurfaceMaterial ResolveSurface(Collider collider)
        {
            var surface = collider.GetComponent<PenetrableSurface>();
            if (surface == null)
                surface = collider.GetComponentInParent<PenetrableSurface>();

            return surface != null ? surface.Material : SurfaceMaterial.Concrete;
        }
    }
}
