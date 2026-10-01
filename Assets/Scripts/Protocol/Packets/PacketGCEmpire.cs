using Metin2.Protocol.Constants;

namespace Metin2.Protocol.Packets
{
    /// <summary>
    /// Empire assignment packet (TPacketGCEmpire, 2 bytes).
    /// Verified both sides:
    /// Server `game/src/packet.h:1638-1642`, sent `input_db.cpp:157-169`
    /// (`LoginSuccess`: random 1..3 when the account has no empire yet,
    /// else the account empire; always BEFORE SetPhase(SELECT));
    /// Client `UserInterface/Packet.h:2083-2087`, handled
    /// `PhaseLogin.cpp:124-132` (stored as m_dwEmpireID).
    /// Wire layout:
    /// [0] BYTE bHeader (90 = 0x5a)
    /// [1] BYTE bEmpire (1..3)
    /// </summary>
    public struct PacketGCEmpire : IPacket
    {
        public const byte PacketHeader = PacketHeaders.HEADER_GC_EMPIRE;
        public const int PacketSize = 2;

        public byte Header => PacketHeader;
        public int Length => PacketSize;

        public byte Empire { get; set; }

        public PacketGCEmpire(byte empire)
        {
            Empire = empire;
        }

        public override string ToString()
        {
            return $"PacketGCEmpire(Empire={Empire})";
        }
    }
}
