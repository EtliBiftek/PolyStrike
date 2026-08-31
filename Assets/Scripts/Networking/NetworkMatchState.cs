using PolyStrike.Core;
using Unity.Entities;
using Unity.Mathematics;
using Unity.NetCode;

namespace PolyStrike.Networking
{
    public enum NetworkMatchPhase : byte
    {
        Waiting,
        FreezeTime,
        Live,
        PostPlant,
        RoundEnd,
        HalfTime,
        MatchEnd
    }

    public enum NetworkRoundEndReason : byte
    {
        None,
        Elimination,
        TimeExpired,
        BombExploded,
        BombDefused
    }

    [GhostComponent]
    public struct NetworkMatchSnapshot : IComponentData
    {
        [GhostField] public NetworkMatchPhase Phase;
        [GhostField] public byte RoundNumber;
        [GhostField] public byte TerroristScore;
        [GhostField] public byte CounterTerroristScore;
        [GhostField] public byte LastWinner;
        [GhostField] public NetworkRoundEndReason LastReason;
        [GhostField] public byte BombSite;
        [GhostField] public byte BombPlanted;
        [GhostField(Quantization = 100)] public float PhaseTimeRemaining;
        [GhostField(Quantization = 100)] public float BuyTimeRemaining;
        [GhostField(Quantization = 100)] public float BombTimeRemaining;
        [GhostField(Quantization = 1000)] public float3 BombPosition;
        [GhostField(Quantization = 1000)] public float InteractionProgress;
    }

    [GhostComponent]
    public struct NetworkLoadoutState : IComponentData
    {
        [GhostField] public byte PrimaryOwned;
        [GhostField] public byte PrimaryMagazine;
        [GhostField] public byte PrimaryReserve;
        [GhostField] public byte PistolMagazine;
        [GhostField] public byte PistolReserve;
        [GhostField] public byte HeGrenades;
        [GhostField] public byte Flashbangs;
        [GhostField] public byte SmokeGrenades;
        [GhostField] public byte FireGrenades;
    }

    public struct NetworkInteractionState : IComponentData
    {
        public float PlantProgress;
        public float DefuseProgress;
    }

    public struct NetworkMatchRuntime : IComponentData
    {
        public NetworkMatchPhase Phase;
        public float PhaseTimeRemaining;
        public float BuyTimeRemaining;
        public float BombTimeRemaining;
        public float3 BombPosition;
        public byte BombSite;
        public byte BombPlanted;
        public byte BombWasPlanted;
        public byte TerroristScore;
        public byte CounterTerroristScore;
        public byte RoundsPlayed;
        public byte TerroristLossLevel;
        public byte CounterTerroristLossLevel;
        public byte LastWinner;
        public NetworkRoundEndReason LastReason;
        public byte Started;
    }

    public enum NetworkPurchaseItem : byte
    {
        Rifle,
        Kevlar,
        HelmetBundle,
        DefuseKit,
        HeGrenade,
        Flashbang,
        SmokeGrenade,
        FireGrenade
    }

    public struct NetworkPurchaseRequest : IRpcCommand
    {
        public NetworkPurchaseItem Item;
    }

    public static class NetworkMatchRules
    {
        public const int RegulationRounds = 24;
        public const int HalfRounds = 12;
        public const int RoundsToWin = 13;

        public static float FreezeTime => CompetitiveCvars.FreezeTime;
        public static float BuyTime => CompetitiveCvars.BuyTime;
        public static float RoundTime => CompetitiveCvars.RoundTime;
        public const float RoundRestartDelay = 7f;
        public const float HalfTimeDuration = 15f;
        public const float BombTimer = 40f;
        public const float PlantTime = 3.2f;
        public const float DefuseTime = 10f;
        public const float DefuseKitTime = 5f;

        public static int StartMoney => CompetitiveCvars.StartMoney;
        public static int MaxMoney => CompetitiveCvars.MaxMoney;
        public const int BaseLossBonus = 1400;
        public const int LossBonusStep = 500;
        public const int MaximumLossLevel = 4;
        public const int StartingLossLevel = 1;

        public const int StandardWinReward = 3250;
        public const int ObjectiveWinReward = 3500;
        public const int BombPlantPlayerReward = 300;
        public const int BombDefusePlayerReward = 300;
        public const int PlantedButDefusedTeamReward = 600;

        public const int KevlarPrice = 650;
        public const int HelmetBundlePrice = 1000;
        public const int DefuseKitPrice = 400;
        public const int TRiflePrice = 2700;
        public const int CTRiflePrice = 2900;
        public const int HePrice = 300;
        public const int FlashPrice = 200;
        public const int SmokePrice = 300;
        public const int MolotovPrice = 400;
        public const int IncendiaryPrice = 500;

        public static int LossReward(int lossLevel)
        {
            return BaseLossBonus + LossBonusStep * math.clamp(lossLevel, 0, MaximumLossLevel);
        }
    }
}
