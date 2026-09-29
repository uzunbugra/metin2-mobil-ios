using Metin2.Protocol.Constants;

namespace Metin2.Protocol.Packets
{
    /// <summary>
    /// Server handshake packet (TPacketGCHandshake, 13 bytes).
    /// Verified from Server packet.h:789-795 and desc.cpp:625-636.
    /// Wire layout:
    /// [0]     BYTE  bHeader     (0xff = 255)
    /// [1..4]  DWORD dwHandshake (4 bytes LE)
    /// [5..8]  DWORD dwTime      (4 bytes LE)
    /// [9..12] LONG  lDelta      (4 bytes LE)
    /// </summary>
    public struct PacketGCHandshake : IPacket
    {
        public const byte PacketHeader = PacketHeaders.HEADER_GC_HANDSHAKE;
        public const int PacketSize = 13;

        public byte Header => PacketHeader;
        public int Length => PacketSize;

        public uint Handshake { get; set; }
        public uint Time { get; set; }
        public int Delta { get; set; }

        public PacketGCHandshake(uint handshake, uint time, int delta)
        {
            Handshake = handshake;
            Time = time;
            Delta = delta;
        }

        public override string ToString()
        {
            return $"PacketGCHandshake(Handshake=0x{Handshake:X8}, Time={Time}ms, Delta={Delta}ms)";
        }
    }
}
