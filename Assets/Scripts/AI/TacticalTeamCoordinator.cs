using System.Collections.Generic;
using PolyStrike.Maps;
using PolyStrike.Match;
using PolyStrike.Player;
using UnityEngine;

namespace PolyStrike.AI
{
    public enum TacticalBotRole : byte
    {
        Entry,
        Trader,
        Support,
        Lurk,
        Anchor
    }

    public enum TacticalAttackPlan : byte
    {
        ALongSplit,
        AShortSplit,
        BPressure,
        MidToB,
        Default
    }

    public sealed class TacticalTeamCoordinator : MonoBehaviour
    {
        private const float HearingScanInterval = 0.14f;
        private const float RunningFootstepThreshold = 135f;
        private const float HearingRange = 14f;

        private sealed class TeamState
        {
            public int RoundNumber = -1;
            public TacticalAttackPlan Plan;
            public Vector3 LastEnemyPosition;
            public float LastEnemySeenAt = -20f;
            public Vector3 LastTeammateDeathPosition;
            public float LastTeammateDeathAt = -20f;
        }

        public static TacticalTeamCoordinator Instance { get; private set; }

        private readonly TeamState terrorists = new TeamState();
        private readonly TeamState counterTerrorists = new TeamState();
        private readonly HashSet<MatchParticipant> observedParticipants = new HashSet<MatchParticipant>();
        private float nextHearingScan;

        public static TacticalTeamCoordinator EnsureExists()
        {
            if (Instance != null)
                return Instance;

            var existing = FindFirstObjectByType<TacticalTeamCoordinator>();
            if (existing != null)
                return existing;

            return new GameObject("Tactical Team Coordinator").AddComponent<TacticalTeamCoordinator>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

        private void Update()
        {
            var participants = MatchParticipant.All;
            for (var i = 0; i < participants.Count; i++)
            {
                var participant = participants[i];
                if (participant == null || !observedParticipants.Add(participant))
                    continue;
                participant.Died += OnParticipantDied;
            }

            if (Time.time >= nextHearingScan)
            {
                nextHearingScan = Time.time + HearingScanInterval;
                ScanRunningFootsteps(participants);
            }
        }

        private void OnDestroy()
        {
            foreach (var participant in observedParticipants)
            {
                if (participant != null)
                    participant.Died -= OnParticipantDied;
            }
            observedParticipants.Clear();

            if (Instance == this)
                Instance = null;
        }

        public TacticalBotRole GetRole(MatchTeam team, int slot)
        {
            if (team == MatchTeam.Terrorists)
            {
                return slot switch
                {
                    0 => TacticalBotRole.Entry,
                    1 => TacticalBotRole.Trader,
                    2 => TacticalBotRole.Support,
                    3 => TacticalBotRole.Lurk,
                    _ => TacticalBotRole.Anchor
                };
            }

            return slot switch
            {
                0 => TacticalBotRole.Anchor,
                1 => TacticalBotRole.Support,
                2 => TacticalBotRole.Trader,
                3 => TacticalBotRole.Support,
                _ => TacticalBotRole.Anchor
            };
        }

        public TacticalAttackPlan GetPlan(MatchTeam team, int roundNumber)
        {
            var state = GetState(team);
            EnsureRound(state, roundNumber);
            return state.Plan;
        }

        public bool IsAttackingA(int roundNumber)
        {
            var plan = GetPlan(MatchTeam.Terrorists, roundNumber);
            return plan == TacticalAttackPlan.ALongSplit ||
                   plan == TacticalAttackPlan.AShortSplit ||
                   (plan == TacticalAttackPlan.Default && roundNumber % 2 == 0);
        }

        public Vector3 GetAttackStagingPoint(TacticalBotRole role, int slot, int roundNumber)
        {
            var plan = GetPlan(MatchTeam.Terrorists, roundNumber);
            return plan switch
            {
                TacticalAttackPlan.ALongSplit => role switch
                {
                    TacticalBotRole.Entry => SandlineMap.LongControl,
                    TacticalBotRole.Trader => SandlineMap.ALongEntry + new Vector3(-1.4f, 0f, -2.0f),
                    TacticalBotRole.Support => SandlineMap.LongControl + new Vector3(-2.2f, 0f, -2.5f),
                    TacticalBotRole.Lurk => SandlineMap.MidControl + new Vector3(-2f, 0f, -1f),
                    _ => SandlineMap.AShortEntry + new Vector3(-3f, 0f, -2f)
                },
                TacticalAttackPlan.AShortSplit => role switch
                {
                    TacticalBotRole.Entry => SandlineMap.ShortControl,
                    TacticalBotRole.Trader => SandlineMap.AShortEntry + new Vector3(-2.4f, 0f, -2.2f),
                    TacticalBotRole.Support => SandlineMap.MidControl + new Vector3(3.0f, 0f, -1.0f),
                    TacticalBotRole.Lurk => SandlineMap.LongControl + new Vector3(0f, 0f, -4f),
                    _ => SandlineMap.ShortControl + new Vector3(-2f, 0f, -2f)
                },
                TacticalAttackPlan.BPressure => role switch
                {
                    TacticalBotRole.Entry => SandlineMap.TunnelControl,
                    TacticalBotRole.Trader => SandlineMap.BTunnelEntry + new Vector3(-2.5f, 0f, -2.5f),
                    TacticalBotRole.Support => SandlineMap.TunnelControl + new Vector3(2.4f, 0f, -2f),
                    TacticalBotRole.Lurk => SandlineMap.MidControl + new Vector3(2f, 0f, -1f),
                    _ => SandlineMap.BMidEntry + new Vector3(2.2f, 0f, -2.2f)
                },
                TacticalAttackPlan.MidToB => role switch
                {
                    TacticalBotRole.Entry => SandlineMap.MidControl,
                    TacticalBotRole.Trader => SandlineMap.MidDoors + new Vector3(0f, 0f, -3f),
                    TacticalBotRole.Support => SandlineMap.MidControl + new Vector3(-2.8f, 0f, -2f),
                    TacticalBotRole.Lurk => SandlineMap.TunnelControl,
                    _ => SandlineMap.BMidEntry + new Vector3(3f, 0f, -2f)
                },
                _ => SandlineMap.GetAttackGoal(IsAttackingA(roundNumber), slot)
            };
        }

        public Vector3 GetDefendAnchor(int slot)
        {
            return SandlineMap.GetDefendGoal(slot);
        }

        public void ReportEnemy(MatchTeam reportingTeam, Vector3 position)
        {
            var state = GetState(reportingTeam);
            state.LastEnemyPosition = position;
            state.LastEnemySeenAt = Time.time;
        }

        public void ReportTeammateDeath(MatchTeam team, Vector3 position)
        {
            var state = GetState(team);
            state.LastTeammateDeathPosition = position;
            state.LastTeammateDeathAt = Time.time;
        }

        public bool TryGetSharedEnemy(MatchTeam team, out Vector3 position, float maxAge = 3.5f)
        {
            var state = GetState(team);
            position = state.LastEnemyPosition;
            return Time.time - state.LastEnemySeenAt <= maxAge;
        }

        public bool TryGetTradePosition(MatchTeam team, out Vector3 position, float maxAge = 2.25f)
        {
            var state = GetState(team);
            position = state.LastTeammateDeathPosition;
            return Time.time - state.LastTeammateDeathAt <= maxAge;
        }

        public bool ShouldRotateCounterTerrorist(int slot, Vector3 currentPosition)
        {
            if (!TryGetSharedEnemy(MatchTeam.CounterTerrorists, out var enemyPosition, 3.0f))
                return false;

            if (slot == 2)
                return true;

            var ownAnchor = GetDefendAnchor(slot);
            var enemyCloserToOtherSite = Vector3.Distance(enemyPosition, ownAnchor) > 13f;
            var travelWouldMatter = Vector3.Distance(currentPosition, enemyPosition) > 5f;
            return enemyCloserToOtherSite && travelWouldMatter && Time.time - GetState(MatchTeam.CounterTerrorists).LastEnemySeenAt > 0.65f;
        }

        private void ScanRunningFootsteps(IReadOnlyList<MatchParticipant> participants)
        {
            for (var i = 0; i < participants.Count; i++)
            {
                var source = participants[i];
                if (source == null || !source.IsAlive)
                    continue;

                var sourceMovement = source.GetComponent<PlayerMovement>();
                if (sourceMovement == null || !sourceMovement.IsGrounded || sourceMovement.SpeedSourceUnits < RunningFootstepThreshold)
                    continue;

                var listenerTeam = source.Team == MatchTeam.Terrorists
                    ? MatchTeam.CounterTerrorists
                    : MatchTeam.Terrorists;
                if (!AnyBotCanHear(participants, listenerTeam, source.transform.position))
                    continue;

                var error = Random.insideUnitSphere * 1.15f;
                error.y = 0f;
                ReportEnemy(listenerTeam, source.transform.position + error);
            }
        }

        private static bool AnyBotCanHear(IReadOnlyList<MatchParticipant> participants, MatchTeam team, Vector3 sourcePosition)
        {
            var rangeSquared = HearingRange * HearingRange;
            for (var i = 0; i < participants.Count; i++)
            {
                var listener = participants[i];
                if (listener == null || !listener.IsAlive || listener.Team != team || listener.GetComponent<TacticalBotController>() == null)
                    continue;

                if ((listener.transform.position - sourcePosition).sqrMagnitude <= rangeSquared)
                    return true;
            }
            return false;
        }

        private void OnParticipantDied(MatchParticipant dead)
        {
            if (dead != null)
                ReportTeammateDeath(dead.Team, dead.transform.position);
        }

        private TeamState GetState(MatchTeam team)
        {
            return team == MatchTeam.Terrorists ? terrorists : counterTerrorists;
        }

        private static void EnsureRound(TeamState state, int roundNumber)
        {
            if (state.RoundNumber == roundNumber)
                return;

            state.RoundNumber = roundNumber;
            state.LastEnemySeenAt = -20f;
            state.LastTeammateDeathAt = -20f;

            var cycle = Mathf.Abs(roundNumber - 1) % 5;
            state.Plan = cycle switch
            {
                0 => TacticalAttackPlan.ALongSplit,
                1 => TacticalAttackPlan.BPressure,
                2 => TacticalAttackPlan.AShortSplit,
                3 => TacticalAttackPlan.MidToB,
                _ => TacticalAttackPlan.Default
            };
        }
    }
}
