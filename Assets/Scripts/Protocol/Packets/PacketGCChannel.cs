using Metin2.Protocol.Constants;

namespace Metin2.Protocol.Packets
{
    /// <summary>
    /// Channel announcement (TPacketGCChannel, 2 bytes).
    /// Verified both sides:
    /// Server `game/src/packet.h:1995-1999`, sent `input_login.cpp:617-620`
    /// (`Entergame`, `channel = g_bChannel`, right after GC_TIME);
    /// Client `UserInterface/Packet.h:2377-2381`.
    /// Wire layout:
    /// [0] BYTE header (121 = 0x79)
    /// [1] BYTE channel
    /// </summary>
    public struct PacketGCChannel : IPacket
    {
        public const byte PacketHeader = PacketHeaders.HEADER_GC_CHANNEL;
        public const int PacketSize = 2;

        public byte Header => PacketHeader;
        public int Length => PacketSize;

        public byte Channel { get; set; }

        public PacketGCChannel(byte channel)
        {
            Channel = channel;
        }

        public override string ToString()
        {
            return $"PacketGCChannel(Channel={Channel})";
        }
    }
}
