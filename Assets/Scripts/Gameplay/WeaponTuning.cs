using UnityEngine;

namespace PolyStrike.Gameplay
{
    public sealed class WeaponTuning
    {
        public string DisplayNameKey { get; private set; }
        public float Damage { get; private set; }
        public float RangeMeters { get; private set; }
        public float RangeModifier { get; private set; }
        public float RoundsPerMinute { get; private set; }
        public int MagazineSize { get; private set; }
        public int ReserveAmmo { get; private set; }
        public float ReloadTime { get; private set; }
        public float MaxMoveSpeedSourceUnits { get; private set; }
        public float StandingInaccuracy { get; private set; }
        public float CrouchingInaccuracy { get; private set; }
        public float MovingInaccuracy { get; private set; }
        public float FireInaccuracy { get; private set; }
        public float StandingRecoveryTime { get; private set; }
        public float CrouchingRecoveryTime { get; private set; }
        public float BaseSpread { get; private set; }
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
                RangeMeters = 120f,
                RangeModifier = 0.98f,
                RoundsPerMinute = 600f,
                MagazineSize = 30,
                ReserveAmmo = 90,
                ReloadTime = 2.5f,
                MaxMoveSpeedSourceUnits = 215f,
                StandingInaccuracy = 0.00641f,
                CrouchingInaccuracy = 0.0048f,
                MovingInaccuracy = 0.17506f,
                FireInaccuracy = 0.0078f,
                StandingRecoveryTime = 0.368f,
                CrouchingRecoveryTime = 0.305f,
                BaseSpread = 0.0006f,
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
                RangeMeters = 120f,
                RangeModifier = 0.97f,
                RoundsPerMinute = 667f,
                MagazineSize = 30,
                ReserveAmmo = 90,
                ReloadTime = 3.07f,
                MaxMoveSpeedSourceUnits = 225f,
                StandingInaccuracy = 0.0049f,
                CrouchingInaccuracy = 0.0041f,
                MovingInaccuracy = 0.13788f,
                FireInaccuracy = 0.006f,
                StandingRecoveryTime = 0.339f,
                CrouchingRecoveryTime = 0.28f,
                BaseSpread = 0.0006f,
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
    }
}
