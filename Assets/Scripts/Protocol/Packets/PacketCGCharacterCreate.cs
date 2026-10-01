using Metin2.Protocol.Constants;

namespace Metin2.Protocol.Packets
{
    /// <summary>
    /// Character create request (command_player_create, 34 bytes).
    /// Verified both sides:
    /// Server `game/src/packet.h:543-554`, handled `input_login.cpp:416-499`
    /// (name strlen &gt; 12 → 9/bType=0; bad name/shape → 9/bType=1 Canada else
    /// 9/bType=0; bad job → 9/bType=0; else GD_PLAYER_CREATE to DB);
    /// Client `UserInterface/Packet.h:1143-1154`, sent `PhaseSelect.cpp:194-215`.
    /// Wire layout (total 34 = 1+1+25+2+1+4):
    /// [0]      BYTE header (4 = 0x04)
    /// [1]      BYTE index  (0..3)
    /// [2..26]  char name[25] (CHARACTER_NAME_MAX_LEN+1)
    /// [27..28] WORD job
    /// [29]     BYTE shape
    /// [30..33] BYTE Con, Int, Str, Dex
    /// </summary>
    public struct PacketCGCharacterCreate : IPacket
    {
        public const byte PacketHeader = PacketHeaders.HEADER_CG_CHARACTER_CREATE;
        public const int NameBufferLen = 25; // CHARACTER_NAME_MAX_LEN + 1
        public const int NameMaxChars = 12; // server-enforced (input_login.cpp:437)
        public const int PacketSize = 1 + 1 + NameBufferLen + 2 + 1 + 4; // 34 bytes

        public byte Header => PacketHeader;
        public int Length => PacketSize;

        public byte Index { get; set; }
        public string Name { get; set; }
        public ushort Job { get; set; }
        public byte Shape { get; set; }
        public byte Con { get; set; }
        public byte Int { get; set; }
        public byte Str { get; set; }
        public byte Dex { get; set; }

        public PacketCGCharacterCreate(
            byte index, string name, ushort job, byte shape, byte con, byte @int, byte str, byte dex)
        {
            Index = index;
            Name = name ?? string.Empty;
            Job = job;
            Shape = shape;
            Con = con;
            Int = @int;
            Str = str;
            Dex = dex;
        }

        public override string ToString()
        {
            return $"PacketCGCharacterCreate(Index={Index}, Name='{Name}', Job={Job})";
        }
    }
}
