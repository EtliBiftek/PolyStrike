using PolyStrike.Gameplay;
using PolyStrike.Maps;
using PolyStrike.Match;
using PolyStrike.Player;
using UnityEngine;
using UnityEngine.AI;

namespace PolyStrike.AI
{
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(MatchParticipant), typeof(PlayerMovement), typeof(Health))]
    public sealed class TacticalBotController : MonoBehaviour
    {
        private const float PathRefreshInterval = 0.18f;
        private const float EnemyScanInterval = 0.08f;
        private const float MaximumEngagementDistance = 55f;
        private const float CounterStrafeThreshold = 24f;
        private const float TurnSpeed = 720f;

        private MatchParticipant participant;
        private PlayerMovement movement;
        private HitscanWeapon weapon;
        private readonly NavMeshPath path = new NavMeshPath();

        private int slot;
        private bool attackA;
        private int observedRound = -1;
        private MatchTeam observedTeam;
        private MatchParticipant target;
        private float nextPathRefresh;
        private float nextEnemyScan;
        private float nextShotTime;
        private float nextAimJitter;
        private float plantStartedAt = -1f;
        private float defuseStartedAt = -1f;
        private Vector2 aimJitter;
        private Vector3 destination;
        private WeaponTuning combatProfile;
        private bool usingPrimaryProfile;
        private int magazineAmmo;
        private int reserveAmmo;
        private int sprayIndex;
        private float lastShotTime = -10f;

        public void Configure(int teamSlot)
        {
            slot = Mathf.Clamp(teamSlot, 0, 4);
        }

        private void Awake()
        {
            participant = GetComponent<MatchParticipant>();
            movement = GetComponent<PlayerMovement>();
            weapon = GetComponentInChildren<HitscanWeapon>();
        }

        private void Start()
        {
            movement.SetMovementCommand(Vector2.zero, false, false, false);
            weapon?.SetExternalInputBlocked(true);
            observedTeam = participant.Team;
            ChooseAttackSide();
            RefreshCombatProfile(true);
        }

        private void OnDisable()
        {
            movement?.ClearMovementCommand();
            movement?.ClearExternalMaxSpeed();
        }

        private void Update()
        {
            var match = MatchRoundManager.Instance;
            if (match == null || !participant.IsAlive)
            {
                StopMoving();
                return;
            }

            RefreshRoundState(match);
            RefreshCombatProfile(false);

            if (match.Phase == RoundPhase.FreezeTime)
            {
                StopMoving();
                BuyForRound();
                return;
            }

            if (match.Phase == RoundPhase.RoundEnd || match.Phase == RoundPhase.HalfTime || match.Phase == RoundPhase.MatchEnd)
            {
                StopMoving();
                return;
            }

            RefreshTarget();
            if (target != null && HasLineOfSight(target))
            {
                plantStartedAt = -1f;
                defuseStartedAt = -1f;
                FightTarget(target);
                return;
            }

            target = null;

            if (TryHandleObjective(match))
                return;

            Navigate(match);
        }

        private void RefreshRoundState(MatchRoundManager match)
        {
            if (participant.Team != observedTeam)
            {
                observedTeam = participant.Team;
                ChooseAttackSide();
                observedRound = -1;
            }

            if (match.RoundNumber == observedRound)
                return;

            observedRound = match.RoundNumber;
            target = null;
            sprayIndex = 0;
            plantStartedAt = -1f;
            defuseStartedAt = -1f;
            ChooseAttackSide();
            RefreshCombatProfile(true);
        }

        private void BuyForRound()
        {
            if (participant.Health.Armor < 100f)
            {
                if (participant.Money >= MatchRules.HelmetBundlePrice)
                    participant.BuyHelmetBundle();
                else if (participant.Money >= MatchRules.KevlarPrice)
                    participant.BuyKevlar();
            }

            if (weapon != null && !weapon.HasPrimary)
                participant.BuyPrimaryRifle();

            if (participant.Team == MatchTeam.CounterTerrorists && !participant.HasDefuseKit)
                participant.BuyDefuseKit();
        }

        private bool TryHandleObjective(MatchRoundManager match)
        {
            if (match.Phase == RoundPhase.Live && participant.Team == MatchTeam.Terrorists && participant.CarriesBomb)
            {
                var site = BombSite.FindAt(transform.position);
                if (site != null)
                {
                    StopMoving();
                    if (plantStartedAt < 0f)
                        plantStartedAt = Time.time;

                    if (Time.time - plantStartedAt >= MatchRules.PlantTime)
                    {
                        C4Controller.TryBotPlant(participant);
                        plantStartedAt = -1f;
                    }

                    return true;
                }
            }

            plantStartedAt = -1f;

            if (match.Phase != RoundPhase.PostPlant || participant.Team != MatchTeam.CounterTerrorists)
            {
                defuseStartedAt = -1f;
                return false;
            }

            var bomb = C4Controller.PlantedBombTransform;
            if (bomb == null)
                return false;

            if (Vector3.Distance(transform.position, bomb.position) > 1.7f)
                return false;

            StopMoving();
            if (defuseStartedAt < 0f)
                defuseStartedAt = Time.time;

            var duration = participant.HasDefuseKit ? MatchRules.DefuseKitTime : MatchRules.DefuseTime;
            if (Time.time - defuseStartedAt >= duration)
            {
                C4Controller.TryBotDefuse(participant);
                defuseStartedAt = -1f;
            }

            return true;
        }

        private void Navigate(MatchRoundManager match)
        {
            destination = ResolveDestination(match);

            if (Time.time >= nextPathRefresh)
            {
                nextPathRefresh = Time.time + PathRefreshInterval;
                NavMesh.CalculatePath(transform.position, destination, NavMesh.AllAreas, path);
            }

            var steeringTarget = destination;
            if (path.status != NavMeshPathStatus.PathInvalid && path.corners != null && path.corners.Length > 1)
            {
                for (var i = 1; i < path.corners.Length; i++)
                {
                    if (Vector3.Distance(transform.position, path.corners[i]) < 0.65f)
                        continue;

                    steeringTarget = path.corners[i];
                    break;
                }
            }

            var worldDirection = steeringTarget - transform.position;
            worldDirection.y = 0f;
            if (worldDirection.sqrMagnitude < 0.12f)
            {
                StopMoving();
                return;
            }

            worldDirection.Normalize();
            RotateTowards(worldDirection);
            var local = new Vector2(Vector3.Dot(transform.right, worldDirection), Vector3.Dot(transform.forward, worldDirection));
            var walk = ShouldWalk(match, steeringTarget);
            movement.SetMovementCommand(local, walk, false, false);
        }

        private Vector3 ResolveDestination(MatchRoundManager match)
        {
            if (match.Phase == RoundPhase.PostPlant && participant.Team == MatchTeam.CounterTerrorists)
            {
                var bomb = C4Controller.PlantedBombTransform;
                if (bomb != null)
                    return bomb.position;
            }

            if (participant.Team == MatchTeam.CounterTerrorists)
                return SandlineMap.GetDefendGoal(slot);

            var siteCenter = attackA ? SandlineMap.ASiteCenter : SandlineMap.BSiteCenter;
            var control = SandlineMap.GetAttackGoal(attackA, slot);
            return Vector3.Distance(transform.position, control) > 2.1f ? control : siteCenter;
        }

        private bool ShouldWalk(MatchRoundManager match, Vector3 steeringTarget)
        {
            if (match.Phase == RoundPhase.PostPlant)
                return false;

            if (participant.Team == MatchTeam.CounterTerrorists)
                return Vector3.Distance(transform.position, steeringTarget) < 5f;

            return Vector3.Distance(transform.position, steeringTarget) < 4f;
        }

        private void RefreshTarget()
        {
            if (Time.time < nextEnemyScan && target != null && target.IsAlive)
                return;

            nextEnemyScan = Time.time + EnemyScanInterval;
            target = FindBestVisibleEnemy();
        }

        private MatchParticipant FindBestVisibleEnemy()
        {
            MatchParticipant best = null;
            var bestScore = float.PositiveInfinity;
            var all = MatchParticipant.All;

            for (var i = 0; i < all.Count; i++)
            {
                var candidate = all[i];
                if (candidate == null || candidate == participant || !candidate.IsAlive || candidate.Team == participant.Team)
                    continue;

                var distance = Vector3.Distance(transform.position, candidate.transform.position);
                if (distance > MaximumEngagementDistance || !HasLineOfSight(candidate))
                    continue;

                var score = distance;
                if (candidate.CarriesBomb)
                    score -= 4f;

                if (score < bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            return best;
        }

        private bool HasLineOfSight(MatchParticipant candidate)
        {
            if (candidate == null)
                return false;

            var origin = EyePosition;
            var targetPoint = candidate.transform.position + Vector3.up * 1.25f;
            var delta = targetPoint - origin;
            var distance = delta.magnitude;
            if (distance <= 0.01f)
                return true;

            if (!Physics.Raycast(origin, delta / distance, out var hit, distance + 0.2f, ~0, QueryTriggerInteraction.Ignore))
                return true;

            var health = hit.collider.GetComponentInParent<Health>();
            return health != null && health == candidate.Health;
        }

        private void FightTarget(MatchParticipant enemy)
        {
            var targetPoint = enemy.transform.position + Vector3.up * 1.32f;
            var aimDirection = targetPoint - EyePosition;
            aimDirection.y = Mathf.Max(aimDirection.y, -1.5f);
            if (aimDirection.sqrMagnitude < 0.001f)
                return;

            RotateTowards(aimDirection.normalized);

            if (movement.SpeedSourceUnits > CounterStrafeThreshold)
            {
                CounterStrafe();
                return;
            }

            movement.SetMovementCommand(Vector2.zero, false, false, false);
            TryFire(aimDirection.normalized);
        }

        private void CounterStrafe()
        {
            var velocity = movement.PlanarVelocity;
            velocity.y = 0f;
            if (velocity.sqrMagnitude < 0.001f)
            {
                StopMoving();
                return;
            }

            var stopDirection = -velocity.normalized;
            var local = new Vector2(Vector3.Dot(transform.right, stopDirection), Vector3.Dot(transform.forward, stopDirection));
            movement.SetMovementCommand(local, false, false, false);
        }

        private void TryFire(Vector3 baseDirection)
        {
            if (combatProfile == null || Time.time < nextShotTime)
                return;

            if (magazineAmmo <= 0)
            {
                var loaded = Mathf.Min(combatProfile.MagazineSize, reserveAmmo);
                magazineAmmo = loaded;
                reserveAmmo -= loaded;
                nextShotTime = Time.time + combatProfile.ReloadFireReadyTime;
                sprayIndex = 0;
                return;
            }

            if (Time.time - lastShotTime > 0.38f)
                sprayIndex = 0;

            var secondsPerShot = 60f / combatProfile.RoundsPerMinute;
            nextShotTime = Time.time + secondsPerShot;
            lastShotTime = Time.time;
            magazineAmmo--;

            if (Time.time >= nextAimJitter)
            {
                nextAimJitter = Time.time + Random.Range(0.09f, 0.16f);
                aimJitter = Random.insideUnitCircle * (usingPrimaryProfile ? 0.22f : 0.34f);
            }

            var patternIndex = Mathf.Clamp(sprayIndex, 0, combatProfile.SprayPattern.Length - 1);
            var recoil = combatProfile.SprayPattern[patternIndex] * 0.22f;
            var inaccuracy = CalculateInaccuracy();
            var randomSpread = Random.insideUnitCircle * inaccuracy * Mathf.Rad2Deg;
            var error = aimJitter + recoil + randomSpread;

            var aimRotation = Quaternion.LookRotation(baseDirection, Vector3.up) * Quaternion.Euler(-error.y, error.x, 0f);
            FireRay(aimRotation * Vector3.forward);
            sprayIndex++;
        }

        private void FireRay(Vector3 direction)
        {
            var origin = EyePosition + direction * 0.08f;
            if (!Physics.Raycast(origin, direction, out var hit, combatProfile.RangeMeters, ~0, QueryTriggerInteraction.Ignore))
                return;

            var health = hit.collider.GetComponentInParent<Health>();
            if (health == null || health.IsDead)
                return;

            var victim = health.GetComponent<MatchParticipant>();
            if (victim == null || victim.Team == participant.Team)
                return;

            var distanceUnits = SourceUnit.ToSourceUnits(hit.distance);
            var damage = combatProfile.Damage * Mathf.Pow(combatProfile.RangeModifier, distanceUnits / 500f);
            var hitGroup = ResolveHitGroup(victim, hit.point);
            var result = health.TakeBulletDamage(new BulletDamage(
                damage,
                combatProfile.ArmorPenetration,
                combatProfile.TaggingBaseVsM4,
                hitGroup,
                direction));

            if (result.Killed)
                participant.AddMoney(combatProfile.KillReward);
        }

        private float CalculateInaccuracy()
        {
            var crouched = movement.DuckAmount > 0.5f;
            var result = crouched ? combatProfile.CrouchingInaccuracy : combatProfile.StandingInaccuracy;
            var speedFraction = movement.SpeedSourceUnits / Mathf.Max(combatProfile.MaxMoveSpeedSourceUnits, 1f);
            if (speedFraction > 0.34f)
                result += combatProfile.MovingInaccuracy * Mathf.Clamp01((speedFraction - 0.34f) / 0.61f);
            return combatProfile.BaseSpread + result;
        }

        private static HitGroup ResolveHitGroup(MatchParticipant victim, Vector3 hitPoint)
        {
            var height = hitPoint.y - victim.transform.position.y;
            if (height >= 1.48f)
                return HitGroup.Head;
            if (height >= 0.92f)
                return height < 1.12f ? HitGroup.Stomach : HitGroup.Chest;
            return HitGroup.LeftLeg;
        }

        private void RefreshCombatProfile(bool forceRefill)
        {
            var usePrimary = weapon != null && weapon.HasPrimary;
            if (!forceRefill && combatProfile != null && usePrimary == usingPrimaryProfile && participant.Team == observedTeam)
                return;

            usingPrimaryProfile = usePrimary;
            combatProfile = usePrimary
                ? participant.Team == MatchTeam.Terrorists ? WeaponTuning.CreateTRifle() : WeaponTuning.CreateCTRifle()
                : participant.Team == MatchTeam.Terrorists ? WeaponTuning.CreateTPistol() : WeaponTuning.CreateCTPistol();

            magazineAmmo = combatProfile.MagazineSize;
            reserveAmmo = combatProfile.ReserveAmmo;
            sprayIndex = 0;
            movement.SetExternalMaxSpeed(combatProfile.MaxMoveSpeedSourceUnits);
        }

        private void RotateTowards(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f)
                return;

            var targetRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, TurnSpeed * Time.deltaTime);
        }

        private void StopMoving()
        {
            movement.SetMovementCommand(Vector2.zero, false, false, false);
        }

        private void ChooseAttackSide()
        {
            attackA = ((slot + Random.Range(0, 3)) & 1) == 0;
        }

        private Vector3 EyePosition => transform.position + Vector3.up * 1.58f;
    }
}
