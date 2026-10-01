using Metin2.Protocol.Constants;

namespace Metin2.Protocol.Packets
{
    /// <summary>
    /// Own-character world spawn (TPacketGCMainCharacter2_EMPIRE, 46 bytes).
    /// Sent from `PlayerLoad` (`input_db.cpp:427-428`, PHASE_LOADING) when no
    /// map BGM is configured — the common case. (BGM variants 137/138 exist
    /// but are out of scope until a BGM map is traced.)
    /// Verified both sides:
    /// Server `game/src/packet.h:982-991` (`char.cpp:1506-1519` sends the
    /// 4_BGM_VOL twin, `1524-1535` the 3_BGM twin, `1543-1553` this one);
    /// Client `UserInterface/Packet.h:1386-1395`, handled
    /// `PhaseLoading.cpp:218-242` (`RecvMainCharacter2_EMPIRE`: stores
    /// VID/race/empire/skill, names the player, loads map coords).
    /// NOTE: the client's legacy `TPacketGCMainCharacter` (45B, no empire,
    /// header 15) is NOT what this server sends — header 113 always carries
    /// the 46B empire layout here (non-GAIDEN branch, `Packet.h:259-263`).
    /// Wire layout (total 46 = 1+4+2+25+12+1+1):
    /// [0]      BYTE  header (113 = 0x71)
    /// [1..4]   DWORD dwVID
    /// [5..6]   WORD  wRaceNum
    /// [7..31]  char  szName[25]
    /// [32..43] long  lx, ly, lz (4 bytes LE each)
    /// [44]     BYTE  empire
    /// [45]     BYTE  skill_group
    /// </summary>
    public struct PacketGCMainCharacter : IPacket
    {
        public const byte PacketHeader = PacketHeaders.HEADER_GC_MAIN_CHARACTER2_EMPIRE;
        public const int NameBufferLen = 25; // CHARACTER_NAME_MAX_LEN + 1
        public const int PacketSize = 1 + 4 + 2 + NameBufferLen + 12 + 1 + 1; // 46 bytes

        public byte Header => PacketHeader;
        public int Length => PacketSize;

        public uint Vid { get; set; }
        public ushort Race { get; set; }
        public string Name { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Z { get; set; }
        public byte Empire { get; set; }
        public byte SkillGroup { get; set; }

        public PacketGCMainCharacter(
            uint vid, ushort race, string name, int x, int y, int z, byte empire, byte skillGroup)
        {
            Vid = vid;
            Race = race;
            Name = name ?? string.Empty;
            X = x;
            Y = y;
            Z = z;
            Empire = empire;
            SkillGroup = skillGroup;
        }

        public override string ToString()
        {
            return $"PacketGCMainCharacter(Vid={Vid}, Race={Race}, Name='{Name}', Pos=({X},{Y},{Z}))";
        }
    }
}
