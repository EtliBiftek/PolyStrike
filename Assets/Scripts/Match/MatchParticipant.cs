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
        private PlayerMovement movement;
        private HitscanWeapon weapon;
        private UtilityController utility;
        private C4Controller c4;
        private Vector3 tSpawnPosition;
        private Vector3 ctSpawnPosition;
        private Quaternion tSpawnRotation;
        private Quaternion ctSpawnRotation;

        public static IReadOnlyList<MatchParticipant> All => Participants;

        public MatchTeam Team { get; private set; }
        public bool IsLocalPlayer { get; private set; }
        public int Money { get; private set; } = MatchRules.StartMoney;
        public bool HasDefuseKit { get; private set; }
        public bool CarriesBomb { get; private set; }
        public Health Health => health;
        public bool IsAlive => health != null && !health.IsDead;
        public Vector3 SpawnPosition => Team == MatchTeam.Terrorists ? tSpawnPosition : ctSpawnPosition;

        public event Action<MatchParticipant> Died;
        public event Action<int> MoneyChanged;
        public event Action EquipmentChanged;

        private void Awake()
        {
            health = GetComponent<Health>();
            movement = GetComponent<PlayerMovement>();
            tSpawnPosition = transform.position;
            ctSpawnPosition = transform.position;
            tSpawnRotation = transform.rotation;
            ctSpawnRotation = transform.rotation;
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
            if (weapon != null)
                weapon.EnemyKilled -= OnEnemyKilled;
        }

        public void Configure(MatchTeam team, bool localPlayer)
        {
            Team = team;
            IsLocalPlayer = localPlayer;

            movement ??= GetComponent<PlayerMovement>();

            if (GetComponent<BombDeathDropGuard>() == null)
                gameObject.AddComponent<BombDeathDropGuard>();

            if (localPlayer && GetComponent<PlayerDropController>() == null)
                gameObject.AddComponent<PlayerDropController>();
        }

        public void ConfigureTeamSpawns(Vector3 terroristSpawn, Quaternion terroristRotation, Vector3 counterTerroristSpawn, Quaternion counterTerroristRotation)
        {
            tSpawnPosition = terroristSpawn;
            tSpawnRotation = terroristRotation;
            ctSpawnPosition = counterTerroristSpawn;
            ctSpawnRotation = counterTerroristRotation;
            RestoreSpawn();
        }

        public void SetLoadoutReferences(HitscanWeapon hitscanWeapon, UtilityController utilityController)
        {
            if (weapon != null)
                weapon.EnemyKilled -= OnEnemyKilled;

            weapon = hitscanWeapon;
            utility = utilityController;
            movement ??= GetComponent<PlayerMovement>();

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
            movement?.ResetRoundMotion();
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
            movement?.ResetRoundMotion();

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

        public bool IsInBuyZone(float radius = 4.5f)
        {
            var delta = transform.position - SpawnPosition;
            delta.y = 0f;
            return delta.sqrMagnitude <= radius * radius;
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
                GrenadeType.Molotov => Team == MatchTeam.Terrorists ? MatchRules.MolotovPrice : MatchRules.IncendiaryPrice,
                _ => int.MaxValue
            };

            if (!utility.CanBuy(type) || !SpendMoney(price))
                return false;

            if (utility.AddGrenade(type, true))
            {
                EquipmentChanged?.Invoke();
                return true;
            }

            AddMoney(price);
            return false;
        }

        public bool TryPickupPrimary(int profileId, int magazine, int reserve)
        {
            if (weapon == null || !weapon.TryPickupPrimary(profileId, magazine, reserve))
                return false;

            EquipmentChanged?.Invoke();
            return true;
        }

        public bool TryPickupGrenade(GrenadeType type)
        {
            if (utility == null || !utility.AddGrenade(type))
                return false;

            EquipmentChanged?.Invoke();
            return true;
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
            if (Team == MatchTeam.Terrorists)
                transform.SetPositionAndRotation(tSpawnPosition, tSpawnRotation);
            else
                transform.SetPositionAndRotation(ctSpawnPosition, ctSpawnRotation);
        }

        private void OnEnemyKilled(int reward)
        {
            AddMoney(reward);
        }

        private void OnDeath()
        {
            DropDeathEquipment();
            Died?.Invoke(this);
        }

        private void DropDeathEquipment()
        {
            c4 ??= GetComponent<C4Controller>();
            if (CarriesBomb)
                c4?.DropCarriedBomb(false);

            var origin = transform.position + Vector3.up * 0.55f;
            var baseVelocity = new Vector3(0f, SourceUnit.ToMeters(70f), 0f);

            if (HasDefuseKit)
            {
                DroppedMatchItem.SpawnDefuseKit(origin + transform.right * 0.18f, baseVelocity + transform.right * 0.4f);
                HasDefuseKit = false;
            }

            if (weapon != null && weapon.TryDropPrimary(out var profileId, out var magazine, out var reserve))
            {
                DroppedMatchItem.SpawnPrimaryRifle(
                    origin - transform.right * 0.18f,
                    baseVelocity + transform.forward * 0.6f,
                    profileId,
                    magazine,
                    reserve);
            }

            if (utility != null && utility.TryTakeDeathDrop(Team, out var grenadeType))
            {
                DroppedMatchItem.SpawnGrenade(
                    origin + transform.forward * 0.15f,
                    baseVelocity - transform.right * 0.35f,
                    grenadeType);
            }

            EquipmentChanged?.Invoke();
        }
    }
}
