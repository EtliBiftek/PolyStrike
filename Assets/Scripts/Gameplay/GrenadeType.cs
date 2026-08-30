using UnityEngine;

namespace PolyStrike.Gameplay
{
    public enum GrenadeType
    {
        HighExplosive,
        Flashbang,
        Smoke,
        Molotov
    }

    public static class GrenadeRules
    {
        public const float EquippedMoveSpeed = 245f;
        public const float FullThrowSpeed = 675f;
        public const float Gravity = 320f;
        public const float BounceScale = 0.45f;
        public const float ProjectileHalfExtent = 2f;
        public const float RestSpeed = 20f;
        public const float ThrowConstructionDelay = 0.10f;
        public const float PlayerVelocityInheritance = 1.25f;
        public const float HeFlashFuse = 1.50f;
        public const float MolotovAirFuse = 2.0f;
        public const float SmokeArmTime = 1.50f;
        public const float SmokeDuration = 18f;

        public const float MolotovLifetime = 7f;
        public const float IncendiaryLifetime = 5.5f;
        public const float MolotovMaxRange = 150f;
        public const float IncendiaryMaxRange = 110f;
        public const float InfernoDamagePerSecond = 40f;
        public const float InfernoDamageTick = 0.20f;
        public const float MolotovMaxSlope = 30f;

        public static float GetThrowSpeed(float strength)
        {
            return FullThrowSpeed * (0.7f * Mathf.Clamp01(strength) + 0.3f);
        }

        public static float AdjustThrowPitch(float pitch)
        {
            var normalized = Mathf.DeltaAngle(0f, pitch);
            return normalized - (90f - Mathf.Abs(normalized)) * (10f / 90f);
        }
    }
}
