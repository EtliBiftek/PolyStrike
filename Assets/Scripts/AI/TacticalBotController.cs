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
        private const float PathRefreshInterval = 0.16f;
        private const float EnemyScanInterval = 0.07f;
        private const float MaximumEngagementDistance = 55f;
        private const float CounterStrafeThreshold = 24f;
        private const float TurnSpeed = 760f;

        private MatchParticipant participant;
        private PlayerMovement movement;
        private HitscanWeapon weapon;
        private UtilityController utility;
        private TacticalTeamCoordinator coordinator;
        private readonly NavMeshPath path = new NavMeshPath();

        private int slot;
        private int observedRound = -1;
        private MatchTeam observedTeam;
        private TacticalBotRole role;
        private bool attackA;
        private MatchParticipant target;
        private MatchParticipant previousTarget;
        private float targetAcquiredAt;
        private float nextPathRefresh;
        private float nextEnemyScan;
        private float nextShotTime;
        private float nextAimJitter;
        private float burstPauseUntil;
        private float plantStartedAt = -1f;
        private float defuseStartedAt = -1f;
        private Vector2 aimJitter;
        private Vector3 destination;
        private WeaponTuning combatProfile;
        private bool usingPrimaryProfile;
        private int magazineAmmo;
        private int reserveAmmo;
        private int sprayIndex;
        private int burstShots;
        private float lastShotTime = -10f;

        private bool usedSmoke;
        private bool usedFlash;
        private bool usedHe;
        private bool usedFire;

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
            coordinator = TacticalTeamCoordinator.EnsureExists();
            utility = GetComponent<UtilityController>();
            if (utility == null)
                utility = gameObject.AddComponent<UtilityController>();

            utility.SetReferences(null, movement, weapon, null);
            utility.SetExternalInputBlocked(true);
            participant.SetLoadoutReferences(weapon, utility);
            participant.Died += OnDied;

            movement.SetMovementCommand(Vector2.zero, false, false, false);
            weapon?.SetExternalInputBlocked(true);
            observedTeam = participant.Team;
            role = coordinator.GetRole(observedTeam, slot);
            RefreshCombatProfile(true);
        }

        private void OnDestroy()
        {
            if (participant != null)
                participant.Died -= OnDied;
        }

        private void OnDisable()
        {
            movement?.ClearMovementCommand();
            movement?.ClearExternalMaxSpeed();
        }

        private void Update()
        {
            var match = MatchRoundManager.Instance;
            weapon?.SetExternalInputBlocked(true);

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
                coordinator.ReportEnemy(participant.Team, target.transform.position);
                FightTarget(target);
                return;
            }

            target = null;

            if (TryHandleObjective(match))
                return;

            if (TryUseUtility(match))
            {
                StopMoving();
                return;
            }

            Navigate(match);
        }

        private void RefreshRoundState(MatchRoundManager match)
        {
            if (participant.Team != observedTeam)
            {
                observedTeam = participant.Team;
                observedRound = -1;
            }

            if (match.RoundNumber == observedRound)
                return;

            observedRound = match.RoundNumber;
            role = coordinator.GetRole(participant.Team, slot);
            attackA = coordinator.IsAttackingA(observedRound);
            target = null;
            previousTarget = null;
            burstShots = 0;
            sprayIndex = 0;
            plantStartedAt = -1f;
            defuseStartedAt = -1f;
            usedSmoke = false;
            usedFlash = false;
            usedHe = false;
            usedFire = false;
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

            switch (role)
            {
                case TacticalBotRole.Entry:
                    BuyIfPossible(GrenadeType.Flashbang);
                    BuyIfPossible(GrenadeType.HighExplosive);
                    break;
                case TacticalBotRole.Trader:
                    BuyIfPossible(GrenadeType.Flashbang);
                    BuyIfPossible(GrenadeType.HighExplosive);
                    break;
                case TacticalBotRole.Support:
                    BuyIfPossible(GrenadeType.Smoke);
                    BuyIfPossible(GrenadeType.Flashbang);
                    BuyIfPossible(GrenadeType.Flashbang);
                    BuyIfPossible(GrenadeType.HighExplosive);
                    break;
                case TacticalBotRole.Lurk:
                    BuyIfPossible(GrenadeType.Smoke);
                    BuyIfPossible(GrenadeType.HighExplosive);
                    break;
                case TacticalBotRole.Anchor:
                    BuyIfPossible(GrenadeType.Smoke);
                    BuyIfPossible(GrenadeType.Molotov);
                    BuyIfPossible(GrenadeType.Flashbang);
                    break;
            }
        }

        private void BuyIfPossible(GrenadeType type)
        {
            if (utility != null && utility.CanBuy(type))
                participant.BuyGrenade(type);
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
            if (bomb == null || Vector3.Distance(transform.position, bomb.position) > 1.7f)
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

        private bool TryUseUtility(MatchRoundManager match)
        {
            if (utility == null)
                return false;

            if (coordinator.TryGetSharedEnemy(participant.Team, out var sharedEnemy, 2.4f) &&
                !usedHe && utility.GetCount(GrenadeType.HighExplosive) > 0 &&
                Vector3.Distance(transform.position, sharedEnemy) is > 6f and < 22f)
            {
                if (utility.TryBotThrowAt(GrenadeType.HighExplosive, sharedEnemy + Vector3.up * 0.25f))
                {
                    usedHe = true;
                    return true;
                }
            }

            if (participant.Team == MatchTeam.Terrorists && match.Phase == RoundPhase.Live)
                return TryUseAttackUtility();

            if (participant.Team == MatchTeam.CounterTerrorists && match.Phase == RoundPhase.PostPlant)
                return TryUseRetakeUtility();

            if (participant.Team == MatchTeam.CounterTerrorists && match.Phase == RoundPhase.Live &&
                coordinator.TryGetSharedEnemy(participant.Team, out var pressure, 1.8f) &&
                Vector3.Distance(transform.position, pressure) < 15f &&
                !usedFire && utility.GetCount(GrenadeType.Molotov) > 0)
            {
                if (utility.TryBotThrowAt(GrenadeType.Molotov, pressure))
                {
                    usedFire = true;
                    return true;
                }
            }

            return false;
        }

        private bool TryUseAttackUtility()
        {
            var stage = coordinator.GetAttackStagingPoint(role, slot, observedRound);
            if (Vector3.Distance(transform.position, stage) > 4.5f)
                return false;

            var site = attackA ? SandlineMap.ASiteCenter : SandlineMap.BSiteCenter;
            var smokeTarget = attackA
                ? SandlineMap.AShortEntry + new Vector3(2.2f, 0.2f, 2.4f)
                : SandlineMap.BMidEntry + new Vector3(-1.8f, 0.2f, 2.5f);

            if (!usedSmoke &&
                (role == TacticalBotRole.Support || role == TacticalBotRole.Lurk || role == TacticalBotRole.Anchor) &&
                utility.GetCount(GrenadeType.Smoke) > 0 &&
                utility.TryBotThrowAt(GrenadeType.Smoke, smokeTarget))
            {
                usedSmoke = true;
                return true;
            }

            if (!usedFlash &&
                (role == TacticalBotRole.Entry || role == TacticalBotRole.Trader || role == TacticalBotRole.Support) &&
                utility.GetCount(GrenadeType.Flashbang) > 0 &&
                utility.TryBotThrowAt(GrenadeType.Flashbang, site + Vector3.up * 1.7f))
            {
                usedFlash = true;
                return true;
            }

            return false;
        }

        private bool TryUseRetakeUtility()
        {
            var bomb = C4Controller.PlantedBombTransform;
            if (bomb == null)
                return false;

            var distance = Vector3.Distance(transform.position, bomb.position);
            if (distance is < 7f or > 20f)
                return false;

            if (!usedFlash && utility.GetCount(GrenadeType.Flashbang) > 0 &&
                utility.TryBotThrowAt(GrenadeType.Flashbang, bomb.position + Vector3.up * 1.7f))
            {
                usedFlash = true;
                return true;
            }

            if (!usedSmoke && utility.GetCount(GrenadeType.Smoke) > 0 &&
                utility.TryBotThrowAt(GrenadeType.Smoke, bomb.position + Vector3.up * 0.15f))
            {
                usedSmoke = true;
                return true;
            }

            return false;
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
                HoldAngle(match);
                return;
            }

            worldDirection.Normalize();
            RotateTowards(worldDirection);
            var local = new Vector2(Vector3.Dot(transform.right, worldDirection), Vector3.Dot(transform.forward, worldDirection));
            movement.SetMovementCommand(local, ShouldWalk(match, steeringTarget), false, false);
        }

        private Vector3 ResolveDestination(MatchRoundManager match)
        {
            if (match.Phase == RoundPhase.PostPlant)
            {
                var bomb = C4Controller.PlantedBombTransform;
                if (bomb != null)
                {
                    if (participant.Team == MatchTeam.CounterTerrorists)
                        return ResolveRetakeDestination(bomb.position, match);

                    var siteA = Vector3.Distance(bomb.position, SandlineMap.ASiteCenter) <
                                Vector3.Distance(bomb.position, SandlineMap.BSiteCenter);
                    return SandlineMap.GetPostPlantGoal(siteA, slot);
                }
            }

            if (participant.Team == MatchTeam.CounterTerrorists)
            {
                if (coordinator.TryGetTradePosition(participant.Team, out var trade, 1.8f) &&
                    Vector3.Distance(transform.position, trade) < 15f)
                    return trade;

                if (coordinator.ShouldRotateCounterTerrorist(slot, transform.position) &&
                    coordinator.TryGetSharedEnemy(participant.Team, out var enemyPosition, 3.0f))
                    return enemyPosition;

                return coordinator.GetDefendAnchor(slot);
            }

            if (coordinator.TryGetTradePosition(participant.Team, out var teammateDeath, 1.8f) &&
                role != TacticalBotRole.Lurk && Vector3.Distance(transform.position, teammateDeath) < 14f)
                return teammateDeath;

            var siteCenter = attackA ? SandlineMap.ASiteCenter : SandlineMap.BSiteCenter;
            var staging = coordinator.GetAttackStagingPoint(role, slot, observedRound);

            if (role == TacticalBotRole.Lurk && match.TimeRemaining > 65f &&
                !coordinator.TryGetSharedEnemy(participant.Team, out _, 2.0f))
                return staging;

            return Vector3.Distance(transform.position, staging) > 2.0f ? staging : siteCenter;
        }

        private Vector3 ResolveRetakeDestination(Vector3 bombPosition, MatchRoundManager match)
        {
            var distance = Vector3.Distance(transform.position, bombPosition);
            if (distance < 6.5f || match.TimeRemaining < 12f)
                return bombPosition;

            var fromBomb = transform.position - bombPosition;
            fromBomb.y = 0f;
            if (fromBomb.sqrMagnitude < 0.01f)
                fromBomb = Vector3.back;
            fromBomb.Normalize();

            var side = slot % 2 == 0 ? transform.right : -transform.right;
            return bombPosition + fromBomb * 5.2f + side * 1.4f;
        }

        private bool ShouldWalk(MatchRoundManager match, Vector3 steeringTarget)
        {
            if (match.Phase == RoundPhase.PostPlant)
                return participant.Team == MatchTeam.Terrorists && Vector3.Distance(transform.position, steeringTarget) < 5f;

            if (coordinator.TryGetSharedEnemy(participant.Team, out var enemyPosition, 2.4f) &&
                Vector3.Distance(transform.position, enemyPosition) < 13f)
                return true;

            return Vector3.Distance(transform.position, steeringTarget) < 4.5f;
        }

        private void HoldAngle(MatchRoundManager match)
        {
            Vector3 lookAt;
            if (participant.Team == MatchTeam.CounterTerrorists && coordinator.TryGetSharedEnemy(participant.Team, out var threat, 3f))
                lookAt = threat;
            else if (participant.Team == MatchTeam.Terrorists)
                lookAt = attackA ? SandlineMap.ASiteCenter : SandlineMap.BSiteCenter;
            else
                lookAt = SandlineMap.MidControl;

            var direction = lookAt - transform.position;
            if (direction.sqrMagnitude > 0.1f)
                RotateTowards(direction.normalized);
        }

        private void RefreshTarget()
        {
            if (Time.time < nextEnemyScan && target != null && target.IsAlive)
                return;

            nextEnemyScan = Time.time + EnemyScanInterval;
            target = FindBestVisibleEnemy();
            if (target == previousTarget)
                return;

            previousTarget = target;
            targetAcquiredAt = Time.time;
            burstShots = 0;
            sprayIndex = 0;
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
                if (coordinator.TryGetSharedEnemy(participant.Team, out var shared, 1.2f))
                    score -= Mathf.Max(0f, 2.5f - Vector3.Distance(shared, candidate.transform.position));

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
            coordinator.ReportEnemy(participant.Team, enemy.transform.position);

            if (movement.SpeedSourceUnits > CounterStrafeThreshold)
            {
                CounterStrafe();
                return;
            }

            movement.SetMovementCommand(Vector2.zero, false, false, false);
            if (Time.time - targetAcquiredAt < ReactionTime)
                return;

            TryFire(aimDirection.normalized, Vector3.Distance(transform.position, enemy.transform.position));
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

        private void TryFire(Vector3 baseDirection, float distance)
        {
            if (combatProfile == null || Time.time < nextShotTime || Time.time < burstPauseUntil)
                return;

            if (magazineAmmo <= 0)
            {
                var loaded = Mathf.Min(combatProfile.MagazineSize, reserveAmmo);
                magazineAmmo = loaded;
                reserveAmmo -= loaded;
                nextShotTime = Time.time + combatProfile.ReloadFireReadyTime;
                sprayIndex = 0;
                burstShots = 0;
                return;
            }

            if (Time.time - lastShotTime > 0.38f)
            {
                sprayIndex = 0;
                burstShots = 0;
            }

            var secondsPerShot = 60f / combatProfile.RoundsPerMinute;
            nextShotTime = Time.time + secondsPerShot;
            lastShotTime = Time.time;
            magazineAmmo--;

            if (Time.time >= nextAimJitter)
            {
                nextAimJitter = Time.time + Random.Range(0.09f, 0.16f);
                var roleScale = role == TacticalBotRole.Entry ? 1.08f : role == TacticalBotRole.Anchor ? 0.90f : 1f;
                aimJitter = Random.insideUnitCircle * (usingPrimaryProfile ? 0.22f : 0.34f) * roleScale;
            }

            var patternIndex = Mathf.Clamp(sprayIndex, 0, combatProfile.SprayPattern.Length - 1);
            var recoil = combatProfile.SprayPattern[patternIndex] * 0.22f;
            var inaccuracy = CalculateInaccuracy();
            var randomSpread = Random.insideUnitCircle * inaccuracy * Mathf.Rad2Deg;
            var error = aimJitter + recoil + randomSpread;

            var aimRotation = Quaternion.LookRotation(baseDirection, Vector3.up) * Quaternion.Euler(-error.y, error.x, 0f);
            FireRay(aimRotation * Vector3.forward);
            sprayIndex++;
            burstShots++;

            var burstLimit = !usingPrimaryProfile ? 1 : distance > 18f ? 4 : distance > 9f ? 7 : 10;
            if (burstShots >= burstLimit)
            {
                burstShots = 0;
                sprayIndex = 0;
                burstPauseUntil = Time.time + Random.Range(0.08f, distance > 18f ? 0.18f : 0.13f);
            }
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
            burstShots = 0;
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

        private void OnDied(MatchParticipant dead)
        {
            coordinator?.ReportTeammateDeath(dead.Team, dead.transform.position);
        }

        private float ReactionTime => role switch
        {
            TacticalBotRole.Entry => 0.19f,
            TacticalBotRole.Trader => 0.17f,
            TacticalBotRole.Support => 0.21f,
            TacticalBotRole.Lurk => 0.18f,
            _ => 0.20f
        };

        private Vector3 EyePosition => transform.position + Vector3.up * 1.58f;
    }
}
