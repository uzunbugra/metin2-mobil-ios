using System;

namespace Metin2.Protocol.Packets
{
    /// <summary>
    /// One skill slot (TPlayerSkill, 6 bytes).
    /// Server `common/tables.h:337-342`, client `UserInterface/Packet.h:1970-1975`
    /// — identical. `tNextRead` is a 4-byte `time_t` (see GC_TIME ABI note).
    /// </summary>
    public struct PlayerSkill : IEquatable<PlayerSkill>
    {
        public const int FieldSize = 1 + 1 + 4; // 6 bytes

        public byte MasterType { get; set; }
        public byte Level { get; set; }
        public uint NextRead { get; set; }

        public bool Equals(PlayerSkill other)
        {
            return MasterType == other.MasterType
                && Level == other.Level
                && NextRead == other.NextRead;
        }

        public override bool Equals(object obj)
        {
            return obj is PlayerSkill other && Equals(other);
        }

        public override int GetHashCode()
        {
            return System.HashCode.Combine(MasterType, Level, NextRead);
        }

        public override string ToString()
        {
            return $"PlayerSkill(Master={MasterType}, Level={Level})";
        }
    }
}
