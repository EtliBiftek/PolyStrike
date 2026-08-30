namespace PolyStrike.Gameplay
{
    public static class SourceUnit
    {
        public const float PerMeter = 39.37f;

        public static float ToMeters(float sourceUnits)
        {
            return sourceUnits / PerMeter;
        }

        public static float ToSourceUnits(float meters)
        {
            return meters * PerMeter;
        }
    }
}
