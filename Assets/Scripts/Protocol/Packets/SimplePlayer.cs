using System;

namespace Metin2.Protocol.Packets
{
    /// <summary>
    /// One character slot on the select screen (TSimplePlayer, 63 bytes,
    /// #pragma pack(1)).
    /// Verified field-for-field on both sides:
    /// Server `common/tables.h:275-291` (`CHARACTER_NAME_MAX_LEN=24`,
    /// `common/length.h:13`); Client `UserInterface/Packet.h:1096-1113`
    /// (`TSimplePlayerInformation`, `StdAfx.h:43` name len 24; only the last
    /// field is renamed `bySkillGroup` vs `skill_group`).
    /// Wire layout (all little-endian, total 63):
    /// [0..3]   DWORD dwID
    /// [4..28]  char  szName[25]
    /// [29]     BYTE  byJob
    /// [30]     BYTE  byLevel
    /// [31..34] DWORD dwPlayMinutes
    /// [35..38] BYTE  byST, byHT, byDX, byIQ
    /// [39..40] WORD  wMainPart
    /// [41]     BYTE  bChangeName
    /// [42..43] WORD  wHairPart
    /// [44..47] BYTE  bDummy[4]
    /// [48..51] long  x (4 bytes LE)
    /// [52..55] long  y (4 bytes LE)
    /// [56..59] long  lAddr (4 bytes, NETWORK byte order IPv4 — see below)
    /// [60..61] WORD  wPort (host order game-core port)
    /// [62]     BYTE  skillGroup
    ///
    /// Address note: server fills lAddr with `inet_addr()` output
    /// (`map_location.cpp:47`, network order) and the client passes it
    /// straight to `Connect()` (`PythonNetworkStream.cpp:471-472`).
    /// </summary>
    public struct SimplePlayer : IEquatable<SimplePlayer>
    {
        public const int NameBufferLen = 25; // CHARACTER_NAME_MAX_LEN + 1
        public const int FieldSize = 4 + NameBufferLen + 1 + 1 + 4 + 4 + 2 + 1 + 2 + 4 + 4 + 4 + 4 + 2 + 1; // 63 bytes

        public uint Id { get; set; }
        public string Name { get; set; }
        public byte Job { get; set; }
        public byte Level { get; set; }
        public uint PlayMinutes { get; set; }
        public byte St { get; set; }
        public byte Ht { get; set; }
        public byte Dx { get; set; }
        public byte Iq { get; set; }
        public ushort MainPart { get; set; }
        public byte ChangeName { get; set; }
        public ushort HairPart { get; set; }
        public int X { get; set; }
        public int Y { get; set; }

        /// <summary>IPv4 address in NETWORK byte order (inet_addr output).</summary>
        public uint AddrNetworkOrder { get; set; }

        public ushort Port { get; set; }
        public byte SkillGroup { get; set; }

        public bool Equals(SimplePlayer other)
        {
            // Null and empty names are equivalent: an empty slot serializes to
            // all-zero bytes and reads back as string.Empty.
            return Id == other.Id
                && string.Equals(Name ?? string.Empty, other.Name ?? string.Empty, StringComparison.Ordinal)
                && Job == other.Job
                && Level == other.Level
                && PlayMinutes == other.PlayMinutes
                && St == other.St
                && Ht == other.Ht
                && Dx == other.Dx
                && Iq == other.Iq
                && MainPart == other.MainPart
                && ChangeName == other.ChangeName
                && HairPart == other.HairPart
                && X == other.X
                && Y == other.Y
                && AddrNetworkOrder == other.AddrNetworkOrder
                && Port == other.Port
                && SkillGroup == other.SkillGroup;
        }

        public override bool Equals(object obj)
        {
            return obj is SimplePlayer other && Equals(other);
        }

        public override int GetHashCode()
        {
            return System.HashCode.Combine(Id, Name, Job, Level, PlayMinutes, St, Ht, Dx)
                ^ System.HashCode.Combine(Iq, MainPart, ChangeName, HairPart, X, Y, AddrNetworkOrder, Port)
                ^ SkillGroup.GetHashCode();
        }

        public override string ToString()
        {
            return $"SimplePlayer(Id={Id}, Name='{Name}', Job={Job}, Level={Level})";
        }
    }
}
