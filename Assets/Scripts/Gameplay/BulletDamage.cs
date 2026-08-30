namespace PolyStrike.Gameplay
{
    public readonly struct BulletDamage
    {
        public float Damage { get; }
        public float ArmorPenetration { get; }
        public float TaggingBaseVsM4 { get; }
        public HitGroup HitGroup { get; }

        public BulletDamage(float damage, float armorPenetration, float taggingBaseVsM4, HitGroup hitGroup)
        {
            Damage = damage;
            ArmorPenetration = armorPenetration;
            TaggingBaseVsM4 = taggingBaseVsM4;
            HitGroup = hitGroup;
        }
    }

    public readonly struct BulletDamageResult
    {
        public int HealthDamage { get; }
        public int ArmorDamage { get; }
        public bool Killed { get; }

        public BulletDamageResult(int healthDamage, int armorDamage, bool killed)
        {
            HealthDamage = healthDamage;
            ArmorDamage = armorDamage;
            Killed = killed;
        }
    }
}
