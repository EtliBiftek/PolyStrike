using System;
using System.Collections.Generic;
using PolyStrike.Gameplay;
using PolyStrike.Player;
using UnityEngine;

namespace PolyStrike.Match
{
    [RequireComponent(typeof(Health))]
    public sealed class MatchParticipant : MonoBehaviour
    {
        private static readonly List<MatchParticipant> Participants = new List<MatchParticipant>();

        private Health health;
        private HitscanWeapon weapon;
        private UtilityController utility;
        private Vector3 spawnPosition;
        private Quaternion spawnRotation;

        public static IReadOnlyList<MatchParticipant> All => Participants;

        public MatchTeam Team { get; private set; }
        public bool IsLocalPlayer { get; private set; }
        public int Money { get; private set; } = MatchRules.StartMoney;
        public bool HasDefuseKit { get; private set; }
        public bool CarriesBomb { get; private set; }
        public Health Health => health;
        public bool IsAlive => health != null && !health.IsDead;

        public event Action<MatchParticipant> Died;
        public event Action<int> MoneyChanged;
        public event Action EquipmentChanged;

        private void Awake()
        {
            health = GetComponent<Health>();
            spawnPosition = transform.position;
            spawnRotation = transform.rotation;
            health.Died += OnDeath;
        }

        private void OnEnable()
        {
            if (!Participants.Contains(this))
                Participants.Add(this);
        }

        private void OnDisable()
        {
            Participants.Remove(this);
        }

        private void OnDestroy()
        {
            if (health != null)
                health.Died -= OnDeath;
        }

        public void Configure(MatchTeam team, bool localPlayer)
        {
            Team = team;
            IsLocalPlayer = localPlayer;
        }

        public void SetLoadoutReferences(HitscanWeapon hitscanWeapon, UtilityController utilityController)
        {
            weapon = hitscanWeapon;
            utility = utilityController;

            if (weapon != null)
            {
                weapon.SetMatchTeam(Team);
                weapon.EnemyKilled += OnEnemyKilled;
            }
        }

        public void BeginHalf(MatchTeam team)
        {
            Team = team;
            Money = MatchRules.StartMoney;
            HasDefuseKit = false;
            CarriesBomb = false;

            health.ResetForRound(true);
            weapon?.ResetForHalf(team);
            utility?.ResetForHalf();
            RestoreSpawn();

            MoneyChanged?.Invoke(Money);
            EquipmentChanged?.Invoke();
        }

        public void PrepareRound()
        {
            var diedLastRound = health.IsDead;
            health.ResetForRound(diedLastRound);

            if (diedLastRound)
            {
                HasDefuseKit = false;
                CarriesBomb = false;
            }

            weapon?.ResetForRound(diedLastRound);
            utility?.ResetForRound(diedLastRound);
            RestoreSpawn();
            EquipmentChanged?.Invoke();
        }

        public void AddMoney(int amount)
        {
            if (amount <= 0)
                return;

            Money = Mathf.Min(MatchRules.MaxMoney, Money + amount);
            MoneyChanged?.Invoke(Money);
        }

        public bool SpendMoney(int amount)
        {
            if (amount < 0 || Money < amount)
                return false;

            Money -= amount;
            MoneyChanged?.Invoke(Money);
            return true;
        }

        public bool BuyKevlar()
        {
            if (health.Armor >= 100f)
                return false;

            if (!SpendMoney(MatchRules.KevlarPrice))
                return false;

            health.SetEquipment(100f, health.HasHelmet);
            EquipmentChanged?.Invoke();
            return true;
        }

        public bool BuyHelmetBundle()
        {
            if (health.Armor >= 100f && health.HasHelmet)
                return false;

            var price = health.Armor >= 100f && !health.HasHelmet ? 350 : MatchRules.HelmetBundlePrice;
            if (!SpendMoney(price))
                return false;

            health.SetEquipment(100f, true);
            EquipmentChanged?.Invoke();
            return true;
        }

        public bool BuyDefuseKit()
        {
            if (Team != MatchTeam.CounterTerrorists || HasDefuseKit)
                return false;

            if (!SpendMoney(MatchRules.DefuseKitPrice))
                return false;

            HasDefuseKit = true;
            EquipmentChanged?.Invoke();
            return true;
        }

        public bool BuyPrimaryRifle()
        {
            if (weapon == null || weapon.HasPrimary)
                return false;

            var price = Team == MatchTeam.Terrorists ? MatchRules.TRiflePrice : MatchRules.CTRiflePrice;
            if (!SpendMoney(price))
                return false;

            weapon.BuyPrimary();
            EquipmentChanged?.Invoke();
            return true;
        }

        public bool BuyGrenade(GrenadeType type)
        {
            if (utility == null)
                return false;

            var price = type switch
            {
                GrenadeType.HighExplosive => MatchRules.HePrice,
                GrenadeType.Flashbang => MatchRules.FlashPrice,
                GrenadeType.Smoke => MatchRules.SmokePrice,
                GrenadeType.Molotov => MatchRules.MolotovPrice,
                _ => int.MaxValue
            };

            if (!utility.CanBuy(type) || !SpendMoney(price))
                return false;

            if (utility.AddGrenade(type))
            {
                EquipmentChanged?.Invoke();
                return true;
            }

            AddMoney(price);
            return false;
        }

        public void GiveBomb(bool carriesBomb)
        {
            CarriesBomb = carriesBomb;
            EquipmentChanged?.Invoke();
        }

        public void SetDefuseKit(bool hasKit)
        {
            HasDefuseKit = Team == MatchTeam.CounterTerrorists && hasKit;
            EquipmentChanged?.Invoke();
        }

        private void RestoreSpawn()
        {
            transform.SetPositionAndRotation(spawnPosition, spawnRotation);
        }

        private void OnEnemyKilled(int reward)
        {
            AddMoney(reward);
        }

        private void OnDeath()
        {
            Died?.Invoke(this);
        }
    }
}
