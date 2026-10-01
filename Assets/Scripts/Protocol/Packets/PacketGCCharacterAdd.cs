using Metin2.Protocol.Constants;

namespace Metin2.Protocol.Packets
{
    /// <summary>
    /// Entity spawn (TPacketGCCharacterAdd, 35 bytes).
    /// Verified both sides:
    /// Server `game/src/packet.h:886-903`, sent `char.cpp:812`
    /// (from `CHARACTER::Show`/view packets);
    /// Client `UserInterface/Packet.h:1226-1252`, handled
    /// `PhaseGameActor.cpp:83-130` (`RecvCharacterAppendPacket`).
    /// (ADD2/120 with equipment is a separate variant, out of scope.)
    /// Wire layout (total 35 = 1+4+4+12+1+2+1+1+1+8):
    /// [0]      BYTE  header (1 = 0x01)
    /// [1..4]   DWORD dwVID
    /// [5..8]   float angle (LE IEEE-754)
    /// [9..20]  long  x, y, z (4 bytes LE each)
    /// [21]     BYTE  bType
    /// [22..23] WORD  wRaceNum
    /// [24]     BYTE  bMovingSpeed
    /// [25]     BYTE  bAttackSpeed
    /// [26]     BYTE  bStateFlag
    /// [27..34] DWORD dwAffectFlag[2]
    /// </summary>
    public struct PacketGCCharacterAdd : IPacket
    {
        public const byte PacketHeader = PacketHeaders.HEADER_GC_CHARACTER_ADD;
        public const int PacketSize = 35;

        public byte Header => PacketHeader;
        public int Length => PacketSize;

        public uint Vid { get; set; }
        public float Angle { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public byte Type { get; set; }
        public ushort Race { get; set; }
        public byte MovingSpeed { get; set; }
        public byte AttackSpeed { get; set; }
        public byte StateFlag { get; set; }
        public uint AffectFlag0 { get; set; }
        public uint AffectFlag1 { get; set; }

        public override string ToString()
        {
            return $"PacketGCCharacterAdd(Vid={Vid}, Race={Race}, Pos=({X},{Y},{Z}))";
        }
    }
}
