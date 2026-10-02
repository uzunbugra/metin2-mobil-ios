using Metin2.Protocol.Constants;

namespace Metin2.Protocol.Packets
{
    /// <summary>
    /// Stun event (TPacketGCStun, 5 bytes).
    /// Verified from Server packet.h:1051-1055 (sent by CHARACTER::Stun,
    /// char_battle.cpp:429-432, broadcast via PacketAround) and Client
    /// Packet.h:1363-1367. Client registration: PythonNetworkStream.cpp:58
    /// (STATIC_SIZE_PACKET); dispatch: PhaseGame.cpp:303.
    /// Wire layout (LE, pack(1)):
    /// [0]      BYTE header (13)
    /// [1..4]   DWORD vid
    /// </summary>
    public struct PacketGCStun : IPacket
    {
        public const byte PacketHeader = PacketHeaders.HEADER_GC_STUN;
        public const int PacketSize = 5;

        public byte Header => PacketHeader;
        public int Length => PacketSize;

        public uint Vid { get; set; }

        public PacketGCStun(uint vid)
        {
            Vid = vid;
        }

        public override string ToString()
        {
            return $"PacketGCStun(Vid={Vid})";
        }
    }
}
