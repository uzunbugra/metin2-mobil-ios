namespace Metin2.Protocol.Constants
{
    /// <summary>
    /// Source-verified EPointTypes subset (server char.h:97-135). These values
    /// travel in GC_POINT_CHANGE (17) `type` and index TPacketGCPoints (16).
    /// The full enum continues past POINT_BOW_DISTANCE (34); extend only with
    /// values re-verified from char.h.
    /// </summary>
    public static class PointTypes
    {
        public const byte None = 0;
        public const byte Level = 1;
        public const byte Voice = 2;
        public const byte Exp = 3;
        public const byte NextExp = 4;
        public const byte Hp = 5;
        public const byte MaxHp = 6;
        public const byte Sp = 7;
        public const byte MaxSp = 8;
        public const byte Stamina = 9;
        public const byte MaxStamina = 10;
        public const byte Gold = 11;
        public const byte St = 12;
        public const byte Ht = 13;
        public const byte Dx = 14;
        public const byte Iq = 15;
        public const byte DefGrade = 16;
        public const byte AttSpeed = 17;
        public const byte AttGrade = 18;
        public const byte MovSpeed = 19;
        public const byte ClientDefGrade = 20;
        public const byte CastingSpeed = 21;
        public const byte MagicAttGrade = 22;
        public const byte MagicDefGrade = 23;
        public const byte EmpirePoint = 24;
        public const byte LevelStep = 25;
        public const byte Stat = 26;
        public const byte SubSkill = 27;
        public const byte Skill = 28;
        public const byte WeaponMin = 29;
        public const byte WeaponMax = 30;
        public const byte Playtime = 31;
        public const byte HpRegen = 32;
        public const byte SpRegen = 33;
        public const byte BowDistance = 34;
    }
}
