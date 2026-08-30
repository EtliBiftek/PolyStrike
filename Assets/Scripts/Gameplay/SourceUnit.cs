using UnityEngine;

namespace PolyStrike.Gameplay
{
    public static class SourceUnit
    {
        public const float PerMeter = 39.37f;

        public static float ToMeters(float sourceUnits)
        {
            return sourceUnits / PerMeter;
        }

        public static Vector3 ToMeters(Vector3 sourceUnits)
        {
            return sourceUnits / PerMeter;
        }

        public static float ToSourceUnits(float meters)
        {
            return meters * PerMeter;
        }

        public static Vector3 ToSourceUnits(Vector3 meters)
        {
            return meters * PerMeter;
        }
    }
}
