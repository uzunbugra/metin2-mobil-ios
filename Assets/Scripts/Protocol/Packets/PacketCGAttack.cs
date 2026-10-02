using Metin2.Protocol.Constants;

namespace Metin2.Protocol.Packets
{
    /// <summary>
    /// Client attack intent (TPacketCGAttack, 8 bytes).
    /// Verified from Server packet.h:564-571 and Client Packet.h:535-542 —
    /// the two structs are field-for-field identical.
    /// Server handler: CInputMain::Attack (input_main.cpp:1690-1770): finds the
    /// victim by VID, rejects NPC/WARP/GOTO targets and self-targets, applies
    /// the skill hit-count guard for bType &gt; 0, then CHARACTER::Attack.
    /// bType == 0 is a normal attack; bType &gt; 0 selects a skill id.
    /// The two CRC bytes feed the server-side magic-cube anti-cheat
    /// accumulator (AssembleCRCMagicCube, input_main.cpp:1745) and carry no
    /// gameplay meaning.
    /// Wire layout (LE, pack(1)):
    /// [0]      BYTE header (2)
    /// [1]      BYTE bType (0 = normal attack, else skill id)
    /// [2..5]   DWORD dwVID (victim)
    /// [6]      BYTE bCRCMagicCubeProcPiece
    /// [7]      BYTE bCRCMagicCubeFilePiece
    /// </summary>
    public struct PacketCGAttack : IPacket
    {
        public const byte PacketHeader = PacketHeaders.HEADER_CG_ATTACK;
        public const int PacketSize = 8;

        public byte Header => PacketHeader;
        public int Length => PacketSize;

        public byte Type { get; set; }
        public uint VictimVid { get; set; }
        public byte CrcMagicCubeProcPiece { get; set; }
        public byte CrcMagicCubeFilePiece { get; set; }

        public PacketCGAttack(byte type, uint victimVid, byte crcProcPiece = 0, byte crcFilePiece = 0)
        {
            Type = type;
            VictimVid = victimVid;
            CrcMagicCubeProcPiece = crcProcPiece;
            CrcMagicCubeFilePiece = crcFilePiece;
        }

        public override string ToString()
        {
            return $"PacketCGAttack(Type={Type}, VictimVid={VictimVid})";
        }
    }
}
