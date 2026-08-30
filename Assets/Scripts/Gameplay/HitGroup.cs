namespace PolyStrike.Gameplay
{
    public enum HitGroup
    {
        Generic,
        Head,
        Chest,
        Stomach,
        LeftArm,
        RightArm,
        LeftLeg,
        RightLeg
    }

    public static class HitGroupRules
    {
        public static float GetDamageMultiplier(HitGroup hitGroup)
        {
            switch (hitGroup)
            {
                case HitGroup.Head:
                    return 4f;
                case HitGroup.Stomach:
                    return 1.25f;
                case HitGroup.LeftLeg:
                case HitGroup.RightLeg:
                    return 0.75f;
                default:
                    return 1f;
            }
        }

        public static bool IsProtectedByArmor(HitGroup hitGroup, bool hasHelmet)
        {
            switch (hitGroup)
            {
                case HitGroup.Head:
                    return hasHelmet;
                case HitGroup.Chest:
                case HitGroup.Stomach:
                case HitGroup.LeftArm:
                case HitGroup.RightArm:
                    return true;
                default:
                    return false;
            }
        }
    }
}
