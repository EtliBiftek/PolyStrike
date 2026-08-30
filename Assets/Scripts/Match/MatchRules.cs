namespace PolyStrike.Match
{
    public enum MatchTeam
    {
        Terrorists,
        CounterTerrorists
    }

    public enum RoundPhase
    {
        FreezeTime,
        Live,
        PostPlant,
        RoundEnd,
        HalfTime,
        MatchEnd
    }

    public enum RoundEndReason
    {
        Elimination,
        TimeExpired,
        BombExploded,
        BombDefused
    }

    public static class MatchRules
    {
        public const int RegulationRounds = 24;
        public const int HalfRounds = 12;
        public const int RoundsToWin = 13;

        public const float FreezeTime = 15f;
        public const float BuyTime = 20f;
        public const float RoundTime = 115.2f;
        public const float RoundRestartDelay = 7f;
        public const float HalfTimeDuration = 15f;

        public const float BombTimer = 40f;
        public const float PlantTime = 3.2f;
        public const float DefuseTime = 10f;
        public const float DefuseKitTime = 5f;

        public const int StartMoney = 800;
        public const int MaxMoney = 16000;
        public const int BaseLossBonus = 1400;
        public const int LossBonusStep = 500;
        public const int MaximumLossLevel = 4;
        public const int StartingLossLevel = 1;

        public const int StandardWinReward = 3250;
        public const int ObjectiveWinReward = 3500;
        public const int BombPlantPlayerReward = 300;
        public const int BombDefusePlayerReward = 300;
        public const int PlantedButDefusedTeamReward = 600;
        public const int RifleKillReward = 300;

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
            var level = UnityEngine.Mathf.Clamp(lossLevel, 0, MaximumLossLevel);
            return BaseLossBonus + LossBonusStep * level;
        }
    }
}
