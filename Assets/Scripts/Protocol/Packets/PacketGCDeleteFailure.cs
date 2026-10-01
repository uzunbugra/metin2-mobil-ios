using Metin2.Protocol.Constants;

namespace Metin2.Protocol.Packets
{
    /// <summary>
    /// Character delete failure (1 byte: header only).
    /// Verified both sides:
    /// Server `input_db.cpp:291-300` (`PlayerDeleteFail`: single 1-byte
    /// `Packet()` of header 11; despite the name the trigger here is an
    /// empty slot — `input_login.cpp:520-525`);
    /// Client `PhaseSelect.cpp:278-286` reads a 1-byte `TPacketGCBlank`.
    /// Wire layout:
    /// [0] BYTE header (11 = 0x0b)
    /// </summary>
    public struct PacketGCDeleteFailure : IPacket
    {
        public const byte PacketHeader = PacketHeaders.HEADER_GC_CHARACTER_DELETE_WRONG_SOCIAL_ID;
        public const int PacketSize = 1;

        public byte Header => PacketHeader;
        public int Length => PacketSize;
    }
}
