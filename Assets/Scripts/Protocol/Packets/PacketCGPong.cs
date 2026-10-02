using Metin2.Protocol.Constants;

namespace Metin2.Protocol.Packets
{
    /// <summary>
    /// Client keepalive pong (TPacketCGPong, 1 byte: header only).
    /// Verified from Client Packet.h:1844-1847; sent in response to every
    /// TPacketGCPing by RecvPingPacket (PythonNetworkStream.cpp:647-651).
    /// Server accepts it in every phase — CInputProcessor::Pong sets the
    /// per-connection pong flag (input.cpp:132-135); dispatched before any
    /// phase-specific handler (input.cpp:551-552). The server closes the
    /// session when the next ping cycle finds the flag still clear
    /// (desc.cpp:174-180 "PING_EVENT: no pong").
    /// Wire layout:
    /// [0] BYTE header (0xfe = 254)
    /// </summary>
    public struct PacketCGPong : IPacket
    {
        public const byte PacketHeader = PacketHeaders.HEADER_CG_PONG;
        public const int PacketSize = 1;

        public byte Header => PacketHeader;
        public int Length => PacketSize;

        public override string ToString()
        {
            return "PacketCGPong()";
        }
    }
}
