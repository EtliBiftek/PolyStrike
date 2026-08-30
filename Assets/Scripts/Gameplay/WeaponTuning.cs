using UnityEngine;

namespace PolyStrike.Gameplay
{
    public sealed class WeaponTuning
    {
        public string DisplayNameKey { get; private set; }
        public float Damage { get; private set; }
        public float RangeMeters { get; private set; }
        public float RangeModifier { get; private set; }
        public float ArmorPenetration { get; private set; }
        public float PenetrationPower { get; private set; }
        public float TaggingBaseVsM4 { get; private set; }
        public float RoundsPerMinute { get; private set; }
        public int MagazineSize { get; private set; }
        public int ReserveAmmo { get; private set; }
        public float ReloadClipReadyTime { get; private set; }
        public float ReloadFireReadyTime { get; private set; }
        public float DeployTime { get; private set; }
        public float MaxMoveSpeedSourceUnits { get; private set; }
        public float StandingInaccuracy { get; private set; }
        public float CrouchingInaccuracy { get; private set; }
        public float MovingInaccuracy { get; private set; }
        public float FireInaccuracy { get; private set; }
        public float StandingRecoveryTime { get; private set; }
        public float CrouchingRecoveryTime { get; private set; }
        public float BaseSpread { get; private set; }
        public bool Automatic { get; private set; }
        public int KillReward { get; private set; }
        public Vector2[] SprayPattern { get; private set; }

        private WeaponTuning()
        {
        }

        public static WeaponTuning CreateTRifle()
        {
            return new WeaponTuning
            {
                DisplayNameKey = "weapon.t_rifle",
                Damage = 36f,
                RangeMeters = SourceUnit.ToMeters(8192f),
                RangeModifier = 0.98f,
                ArmorPenetration = 0.775f,
                PenetrationPower = 2f,
                TaggingBaseVsM4 = 0.319f,
                RoundsPerMinute = 600f,
                MagazineSize = 30,
                ReserveAmmo = 90,
                ReloadClipReadyTime = 1.17f,
                ReloadFireReadyTime = 2.43f,
                DeployTime = 1.00f,
                MaxMoveSpeedSourceUnits = 215f,
                StandingInaccuracy = 0.00641f,
                CrouchingInaccuracy = 0.0048f,
                MovingInaccuracy = 0.17506f,
                FireInaccuracy = 0.0078f,
                StandingRecoveryTime = 0.368f,
                CrouchingRecoveryTime = 0.305f,
                BaseSpread = 0.0006f,
                Automatic = true,
                KillReward = 300,
                SprayPattern = new[]
                {
                    new Vector2(0.00f, 0.00f), new Vector2(0.04f, 0.45f), new Vector2(-0.03f, 1.05f),
                    new Vector2(0.06f, 1.75f), new Vector2(-0.08f, 2.60f), new Vector2(0.10f, 3.55f),
                    new Vector2(-0.05f, 4.50f), new Vector2(0.12f, 5.35f), new Vector2(-0.18f, 6.15f),
                    new Vector2(-0.55f, 6.85f), new Vector2(-1.05f, 7.55f), new Vector2(-1.70f, 8.10f),
                    new Vector2(-2.40f, 8.55f), new Vector2(-2.90f, 8.90f), new Vector2(-2.40f, 9.20f),
                    new Vector2(-1.50f, 9.55f), new Vector2(-0.40f, 9.85f), new Vector2(0.80f, 10.15f),
                    new Vector2(1.90f, 10.50f), new Vector2(2.80f, 10.70f), new Vector2(3.50f, 10.85f),
                    new Vector2(4.24f, 10.95f), new Vector2(3.60f, 11.00f), new Vector2(2.40f, 11.04f),
                    new Vector2(1.00f, 11.06f), new Vector2(-0.50f, 10.95f), new Vector2(-1.80f, 10.90f),
                    new Vector2(-2.60f, 10.82f), new Vector2(-1.40f, 10.90f), new Vector2(0.20f, 10.85f)
                }
            };
        }

        public static WeaponTuning CreateCTRifle()
        {
            return new WeaponTuning
            {
                DisplayNameKey = "weapon.ct_rifle",
                Damage = 33f,
                RangeMeters = SourceUnit.ToMeters(8192f),
                RangeModifier = 0.97f,
                ArmorPenetration = 0.70f,
                PenetrationPower = 2f,
                TaggingBaseVsM4 = 0.319f,
                RoundsPerMinute = 667f,
                MagazineSize = 30,
                ReserveAmmo = 90,
                ReloadClipReadyTime = 1.37f,
                ReloadFireReadyTime = 3.07f,
                DeployTime = 1.13f,
                MaxMoveSpeedSourceUnits = 225f,
                StandingInaccuracy = 0.0049f,
                CrouchingInaccuracy = 0.0041f,
                MovingInaccuracy = 0.13788f,
                FireInaccuracy = 0.006f,
                StandingRecoveryTime = 0.339f,
                CrouchingRecoveryTime = 0.28f,
                BaseSpread = 0.0006f,
                Automatic = true,
                KillReward = 300,
                SprayPattern = new[]
                {
                    new Vector2(0.00f, 0.00f), new Vector2(0.03f, 0.38f), new Vector2(-0.02f, 0.88f),
                    new Vector2(0.05f, 1.48f), new Vector2(-0.06f, 2.15f), new Vector2(0.08f, 2.88f),
                    new Vector2(-0.04f, 3.62f), new Vector2(0.10f, 4.32f), new Vector2(-0.12f, 5.00f),
                    new Vector2(-0.40f, 5.65f), new Vector2(-0.85f, 6.25f), new Vector2(-1.35f, 6.78f),
                    new Vector2(-1.85f, 7.20f), new Vector2(-2.20f, 7.55f), new Vector2(-1.80f, 7.85f),
                    new Vector2(-1.05f, 8.10f), new Vector2(-0.10f, 8.34f), new Vector2(0.90f, 8.55f),
                    new Vector2(1.80f, 8.72f), new Vector2(2.65f, 8.84f), new Vector2(3.10f, 8.93f),
                    new Vector2(3.45f, 9.00f), new Vector2(2.80f, 9.05f), new Vector2(1.85f, 9.06f),
                    new Vector2(0.80f, 9.03f), new Vector2(-0.35f, 8.96f), new Vector2(-1.40f, 8.90f),
                    new Vector2(-2.10f, 8.83f), new Vector2(-1.20f, 8.88f), new Vector2(0.10f, 8.84f)
                }
            };
        }

        public static WeaponTuning CreateTPistol()
        {
            return new WeaponTuning
            {
                DisplayNameKey = "weapon.t_pistol",
                Damage = 30f,
                RangeMeters = SourceUnit.ToMeters(4096f),
                RangeModifier = 0.85f,
                ArmorPenetration = 0.47f,
                PenetrationPower = 1f,
                TaggingBaseVsM4 = 0.36f,
                RoundsPerMinute = 400f,
                MagazineSize = 20,
                ReserveAmmo = 120,
                ReloadClipReadyTime = 1.45f,
                ReloadFireReadyTime = 2.17f,
                DeployTime = 1.10f,
                MaxMoveSpeedSourceUnits = 240f,
                StandingInaccuracy = 0.0056f,
                CrouchingInaccuracy = 0.0042f,
                MovingInaccuracy = 0.010f,
                FireInaccuracy = 0.010f,
                StandingRecoveryTime = 0.20f,
                CrouchingRecoveryTime = 0.18f,
                BaseSpread = 0.001f,
                Automatic = false,
                KillReward = 300,
                SprayPattern = BuildPistolPattern(0.34f, 0.09f, 20)
            };
        }

        public static WeaponTuning CreateCTPistol()
        {
            return new WeaponTuning
            {
                DisplayNameKey = "weapon.ct_pistol",
                Damage = 35f,
                RangeMeters = SourceUnit.ToMeters(4096f),
                RangeModifier = 0.91f,
                ArmorPenetration = 0.505f,
                PenetrationPower = 1f,
                TaggingBaseVsM4 = 0.36f,
                RoundsPerMinute = 353f,
                MagazineSize = 12,
                ReserveAmmo = 24,
                ReloadClipReadyTime = 1.45f,
                ReloadFireReadyTime = 2.17f,
                DeployTime = 1.00f,
                MaxMoveSpeedSourceUnits = 240f,
                StandingInaccuracy = 0.0049f,
                CrouchingInaccuracy = 0.0037f,
                MovingInaccuracy = 0.014f,
                FireInaccuracy = 0.0085f,
                StandingRecoveryTime = 0.35f,
                CrouchingRecoveryTime = 0.30f,
                BaseSpread = 0.0008f,
                Automatic = false,
                KillReward = 300,
                SprayPattern = BuildPistolPattern(0.48f, 0.035f, 12)
            };
        }

        private static Vector2[] BuildPistolPattern(float verticalStep, float horizontalStep, int count)
        {
            var pattern = new Vector2[count];
            for (var i = 0; i < count; i++)
            {
                var horizontal = Mathf.Sin(i * 2.31f) * horizontalStep * i;
                pattern[i] = new Vector2(horizontal, verticalStep * i);
            }

            return pattern;
        }
    }
}
