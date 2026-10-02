using Metin2.Protocol.Constants;

namespace Metin2.Protocol.Packets
{
    /// <summary>
    /// Server keepalive ping (TPacketGCPing, 1 byte: header only).
    /// Verified from Server packet.h:1259-1262, sent by the DESC ping event
    /// (desc.cpp:159-193 ping_event, created in the DESC constructor
    /// desc.cpp:227-233 — it runs in EVERY phase, including plaintext
    /// handshake). Client mirror: Packet.h:1839-1842, handled by
    /// RecvPingPacket (PythonNetworkStream.cpp:636-656) in all five client
    /// phases (PhaseHandShake.cpp:63, PhaseLogin.cpp:50, PhaseSelect.cpp:137,
    /// PhaseLoading.cpp:140, PhaseGame.cpp:381).
    /// Wire layout:
    /// [0] BYTE header (44 = 0x2c)
    /// </summary>
    public struct PacketGCPing : IPacket
    {
        public const byte PacketHeader = PacketHeaders.HEADER_GC_PING;
        public const int PacketSize = 1;

        public byte Header => PacketHeader;
        public int Length => PacketSize;

        public override string ToString()
        {
            return "PacketGCPing()";
        }
    }
}
