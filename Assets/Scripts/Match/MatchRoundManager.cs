using System;
using System.Collections.Generic;
using PolyStrike.Gameplay;
using PolyStrike.Player;
using UnityEngine;

namespace PolyStrike.Match
{
    public sealed class MatchRoundManager : MonoBehaviour
    {
        public static MatchRoundManager Instance { get; private set; }

        private readonly List<MatchParticipant> participants = new List<MatchParticipant>();
        private int tLossLevel = MatchRules.StartingLossLevel;
        private int ctLossLevel = MatchRules.StartingLossLevel;
        private int roundsPlayed;
        private float phaseEndsAt;
        private float buyEndsAt;
        private float restartAt = -1f;
        private bool bombWasPlantedThisRound;

        public RoundPhase Phase { get; private set; } = RoundPhase.FreezeTime;
        public int TerroristScore { get; private set; }
        public int CounterTerroristScore { get; private set; }
        public int RoundNumber => Mathf.Min(roundsPlayed + 1, MatchRules.RegulationRounds);
        public float TimeRemaining => Mathf.Max(0f, phaseEndsAt - Time.time);
        public bool BombPlanted => Phase == RoundPhase.PostPlant;
        public bool BuyAllowed => (Phase == RoundPhase.FreezeTime || Phase == RoundPhase.Live) && Time.time <= buyEndsAt;
        public bool MatchDrawn { get; private set; }
        public MatchTeam? MatchWinner { get; private set; }
        public RoundEndReason? LastRoundEndReason { get; private set; }
        public MatchTeam? LastRoundWinner { get; private set; }

        public event Action StateChanged;

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            var existing = MatchParticipant.All;
            for (var i = 0; i < existing.Count; i++)
                RegisterParticipant(existing[i]);

            BeginFirstHalf();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;

            for (var i = 0; i < participants.Count; i++)
            {
                if (participants[i] != null)
                    participants[i].Died -= OnParticipantDied;
            }
        }

        private void Update()
        {
            if (restartAt >= 0f && Time.time >= restartAt)
            {
                restartAt = -1f;
                RestartMatchNow();
                return;
            }

            switch (Phase)
            {
                case RoundPhase.FreezeTime:
                    if (Time.time >= phaseEndsAt)
                        StartLiveRound();
                    break;

                case RoundPhase.Live:
                    if (Time.time >= phaseEndsAt)
                        EndRound(MatchTeam.CounterTerrorists, RoundEndReason.TimeExpired);
                    break;

                case RoundPhase.PostPlant:
                    if (Time.time >= phaseEndsAt)
                        RegisterBombExploded();
                    break;

                case RoundPhase.RoundEnd:
                    if (Time.time >= phaseEndsAt)
                        AdvanceAfterRound();
                    break;

                case RoundPhase.HalfTime:
                    if (Time.time >= phaseEndsAt)
                        StartNextHalfRound();
                    break;
            }
        }

        public void RegisterParticipant(MatchParticipant participant)
        {
            if (participant == null || participants.Contains(participant))
                return;

            participants.Add(participant);
            participant.Died += OnParticipantDied;
        }

        public void UnregisterParticipant(MatchParticipant participant)
        {
            if (participant == null || !participants.Remove(participant))
                return;

            participant.Died -= OnParticipantDied;
        }

        public void RequestRestart(float delaySeconds)
        {
            restartAt = Time.time + Mathf.Max(0f, delaySeconds);
        }

        public void RegisterBombPlanted(MatchParticipant plantedBy)
        {
            if (Phase != RoundPhase.Live || plantedBy == null || plantedBy.Team != MatchTeam.Terrorists)
                return;

            bombWasPlantedThisRound = true;
            plantedBy.GiveBomb(false);
            plantedBy.AddMoney(MatchRules.BombPlantPlayerReward);

            Phase = RoundPhase.PostPlant;
            phaseEndsAt = Time.time + MatchRules.BombTimer;
            StateChanged?.Invoke();
        }

        public void RegisterBombDefused(MatchParticipant defusedBy)
        {
            if (Phase != RoundPhase.PostPlant || defusedBy == null || defusedBy.Team != MatchTeam.CounterTerrorists)
                return;

            defusedBy.AddMoney(MatchRules.BombDefusePlayerReward);
            EndRound(MatchTeam.CounterTerrorists, RoundEndReason.BombDefused);
        }

        public void RegisterBombExploded()
        {
            if (Phase != RoundPhase.PostPlant)
                return;

            C4Controller.ExplodePlantedBomb();
            EndRound(MatchTeam.Terrorists, RoundEndReason.BombExploded);
        }

        public MatchParticipant GetLocalPlayer()
        {
            for (var i = 0; i < participants.Count; i++)
            {
                if (participants[i] != null && participants[i].IsLocalPlayer)
                    return participants[i];
            }

            return null;
        }

        public int AliveCount(MatchTeam team)
        {
            var count = 0;
            for (var i = 0; i < participants.Count; i++)
            {
                var participant = participants[i];
                if (participant != null && participant.Team == team && participant.IsAlive)
                    count++;
            }

            return count;
        }

        private void RestartMatchNow()
        {
            TerroristScore = 0;
            CounterTerroristScore = 0;
            roundsPlayed = 0;
            MatchWinner = null;
            MatchDrawn = false;
            LastRoundWinner = null;
            LastRoundEndReason = null;
            tLossLevel = MatchRules.StartingLossLevel;
            ctLossLevel = MatchRules.StartingLossLevel;

            for (var i = participants.Count - 1; i >= 0; i--)
            {
                var participant = participants[i];
                if (participant == null)
                {
                    participants.RemoveAt(i);
                    continue;
                }

                participant.BeginHalf(participant.Team);
            }

            StartFreezeTime();
        }

        private void BeginFirstHalf()
        {
            tLossLevel = MatchRules.StartingLossLevel;
            ctLossLevel = MatchRules.StartingLossLevel;

            for (var i = 0; i < participants.Count; i++)
                participants[i].BeginHalf(participants[i].Team);

            StartFreezeTime();
        }

        private void StartFreezeTime()
        {
            bombWasPlantedThisRound = false;
            LastRoundEndReason = null;
            LastRoundWinner = null;

            for (var i = 0; i < participants.Count; i++)
            {
                if (participants[i] != null)
                    participants[i].PrepareRound();
            }

            AssignBombCarrier();
            ClearRoundWorldEffects();
            SetRoundInputLocked(true);

            Phase = RoundPhase.FreezeTime;
            phaseEndsAt = Time.time + MatchRules.FreezeTime;
            buyEndsAt = Time.time + MatchRules.BuyTime;
            StateChanged?.Invoke();
        }

        private void StartLiveRound()
        {
            SetRoundInputLocked(false);
            Phase = RoundPhase.Live;
            phaseEndsAt = Time.time + MatchRules.RoundTime;
            StateChanged?.Invoke();
        }

        private void EndRound(MatchTeam winner, RoundEndReason reason)
        {
            if (Phase == RoundPhase.RoundEnd || Phase == RoundPhase.MatchEnd || Phase == RoundPhase.HalfTime)
                return;

            LastRoundWinner = winner;
            LastRoundEndReason = reason;
            SetRoundInputLocked(true);

            if (winner == MatchTeam.Terrorists)
                TerroristScore++;
            else
                CounterTerroristScore++;

            AwardRoundEconomy(winner, reason);
            roundsPlayed++;

            Phase = RoundPhase.RoundEnd;
            phaseEndsAt = Time.time + MatchRules.RoundRestartDelay;
            StateChanged?.Invoke();
        }

        private void AwardRoundEconomy(MatchTeam winner, RoundEndReason reason)
        {
            var loser = winner == MatchTeam.Terrorists ? MatchTeam.CounterTerrorists : MatchTeam.Terrorists;
            var winnerReward = reason == RoundEndReason.BombExploded || reason == RoundEndReason.BombDefused
                ? MatchRules.ObjectiveWinReward
                : MatchRules.StandardWinReward;
            var loserLevel = loser == MatchTeam.Terrorists ? tLossLevel : ctLossLevel;
            var loserReward = MatchRules.LossReward(loserLevel);

            for (var i = 0; i < participants.Count; i++)
            {
                var participant = participants[i];
                if (participant == null)
                    continue;

                if (participant.Team == winner)
                    participant.AddMoney(winnerReward);
                else
                    participant.AddMoney(loserReward);
            }

            if (reason == RoundEndReason.BombDefused && bombWasPlantedThisRound)
            {
                for (var i = 0; i < participants.Count; i++)
                {
                    if (participants[i] != null && participants[i].Team == MatchTeam.Terrorists)
                        participants[i].AddMoney(MatchRules.PlantedButDefusedTeamReward);
                }
            }

            if (winner == MatchTeam.Terrorists)
            {
                tLossLevel = Mathf.Max(0, tLossLevel - 1);
                ctLossLevel = Mathf.Min(MatchRules.MaximumLossLevel, ctLossLevel + 1);
            }
            else
            {
                ctLossLevel = Mathf.Max(0, ctLossLevel - 1);
                tLossLevel = Mathf.Min(MatchRules.MaximumLossLevel, tLossLevel + 1);
            }
        }

        private void AdvanceAfterRound()
        {
            if (TerroristScore >= MatchRules.RoundsToWin || CounterTerroristScore >= MatchRules.RoundsToWin)
            {
                FinishMatch(TerroristScore > CounterTerroristScore ? MatchTeam.Terrorists : MatchTeam.CounterTerrorists);
                return;
            }

            if (roundsPlayed >= MatchRules.RegulationRounds)
            {
                if (TerroristScore == CounterTerroristScore)
                    FinishDraw();
                else
                    FinishMatch(TerroristScore > CounterTerroristScore ? MatchTeam.Terrorists : MatchTeam.CounterTerrorists);
                return;
            }

            if (roundsPlayed == MatchRules.HalfRounds)
            {
                BeginHalfTime();
                return;
            }

            StartFreezeTime();
        }

        private void BeginHalfTime()
        {
            Phase = RoundPhase.HalfTime;
            phaseEndsAt = Time.time + MatchRules.HalfTimeDuration;
            SetRoundInputLocked(true);

            for (var i = 0; i < participants.Count; i++)
            {
                var participant = participants[i];
                if (participant == null)
                    continue;

                var swapped = participant.Team == MatchTeam.Terrorists
                    ? MatchTeam.CounterTerrorists
                    : MatchTeam.Terrorists;
                participant.BeginHalf(swapped);
            }

            tLossLevel = MatchRules.StartingLossLevel;
            ctLossLevel = MatchRules.StartingLossLevel;
            StateChanged?.Invoke();
        }

        private void StartNextHalfRound()
        {
            StartFreezeTime();
        }

        private void FinishMatch(MatchTeam winner)
        {
            MatchWinner = winner;
            MatchDrawn = false;
            Phase = RoundPhase.MatchEnd;
            SetRoundInputLocked(true);
            StateChanged?.Invoke();
        }

        private void FinishDraw()
        {
            MatchWinner = null;
            MatchDrawn = true;
            Phase = RoundPhase.MatchEnd;
            SetRoundInputLocked(true);
            StateChanged?.Invoke();
        }

        private void AssignBombCarrier()
        {
            MatchParticipant carrier = null;
            for (var i = 0; i < participants.Count; i++)
            {
                var participant = participants[i];
                if (participant == null)
                    continue;

                participant.GiveBomb(false);
                if (carrier == null && participant.Team == MatchTeam.Terrorists && participant.IsAlive)
                    carrier = participant;
            }

            carrier?.GiveBomb(true);
        }

        private void SetRoundInputLocked(bool locked)
        {
            for (var i = 0; i < participants.Count; i++)
            {
                var participant = participants[i];
                if (participant == null || !participant.IsLocalPlayer)
                    continue;

                participant.GetComponent<PlayerMovement>()?.SetRoundMovementLocked(locked);
                participant.GetComponentInChildren<HitscanWeapon>()?.SetExternalInputBlocked(locked);
                participant.GetComponent<UtilityController>()?.SetExternalInputBlocked(locked);
            }
        }

        private void OnParticipantDied(MatchParticipant participant)
        {
            if (Phase != RoundPhase.Live && Phase != RoundPhase.PostPlant)
                return;

            var terroristsAlive = AliveCount(MatchTeam.Terrorists);
            var counterTerroristsAlive = AliveCount(MatchTeam.CounterTerrorists);

            if (counterTerroristsAlive == 0)
            {
                EndRound(MatchTeam.Terrorists, RoundEndReason.Elimination);
                return;
            }

            if (terroristsAlive == 0 && Phase != RoundPhase.PostPlant)
                EndRound(MatchTeam.CounterTerrorists, RoundEndReason.Elimination);
        }

        private static void ClearRoundWorldEffects()
        {
            var grenades = Object.FindObjectsByType<GrenadeProjectile>(FindObjectsSortMode.None);
            for (var i = 0; i < grenades.Length; i++)
                Object.Destroy(grenades[i].gameObject);

            var smokes = Object.FindObjectsByType<SmokeCloud>(FindObjectsSortMode.None);
            for (var i = 0; i < smokes.Length; i++)
                Object.Destroy(smokes[i].gameObject);

            var infernos = Object.FindObjectsByType<InfernoArea>(FindObjectsSortMode.None);
            for (var i = 0; i < infernos.Length; i++)
                Object.Destroy(infernos[i].gameObject);

            C4Controller.ClearPlantedBomb();
        }
    }
}
