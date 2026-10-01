using Metin2.Protocol.Constants;

namespace Metin2.Protocol.Packets
{
    /// <summary>
    /// Entity despawn (TPacketGCCharacterDelete, 5 bytes).
    /// Verified both sides:
    /// Server `game/src/packet.h:959-963`;
    /// Client handled `PhaseGame.cpp:273-274` (`RecvCharacterDeletePacket`).
    /// Wire layout:
    /// [0]     BYTE  header (2 = 0x02)
    /// [1..4]  DWORD id (VID)
    /// </summary>
    public struct PacketGCCharacterDelete : IPacket
    {
        public const byte PacketHeader = PacketHeaders.HEADER_GC_CHARACTER_DEL;
        public const int PacketSize = 1 + 4; // 5 bytes

        public byte Header => PacketHeader;
        public int Length => PacketSize;

        public uint Vid { get; set; }

        public PacketGCCharacterDelete(uint vid)
        {
            Vid = vid;
        }

        public override string ToString()
        {
            return $"PacketGCCharacterDelete(Vid={Vid})";
        }
    }
}
