using Metin2.Protocol.Constants;

namespace Metin2.Protocol.Packets
{
    /// <summary>
    /// Death event (TPacketGCDead, 5 bytes).
    /// Verified from Server packet.h:1057-1061 (sent by CHARACTER::Dead,
    /// char_battle.cpp:1468-1471, broadcast via PacketAround; also
    /// char_horse.cpp:225 for mount death) and Client Packet.h:1369-1373.
    /// Client registration: PythonNetworkStream.cpp:59; dispatch:
    /// PhaseGame.cpp:307.
    /// Wire layout (LE, pack(1)):
    /// [0]      BYTE header (14)
    /// [1..4]   DWORD vid
    /// </summary>
    public struct PacketGCDead : IPacket
    {
        public const byte PacketHeader = PacketHeaders.HEADER_GC_DEAD;
        public const int PacketSize = 5;

        public byte Header => PacketHeader;
        public int Length => PacketSize;

        public uint Vid { get; set; }

        public PacketGCDead(uint vid)
        {
            Vid = vid;
        }

        public override string ToString()
        {
            return $"PacketGCDead(Vid={Vid})";
        }
    }
}
