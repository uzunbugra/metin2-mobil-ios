using Metin2.Protocol.Constants;

namespace Metin2.Protocol.Packets
{
    /// <summary>
    /// Character delete success (2 bytes: header + slot index).
    /// Verified both sides:
    /// Server `input_db.cpp:278-289` (`PlayerDeleteSuccess`: two 1-byte
    /// `Packet()` calls = header 10 + account_index, clears the slot);
    /// Client `UserInterface/Packet.h:1176-1180`
    /// (`TPacketGCDestroyCharacterSuccess`), handled `PhaseSelect.cpp:264-276`
    /// (slot zeroed in select-screen data).
    /// Wire layout:
    /// [0] BYTE header (10 = 0x0a)
    /// [1] BYTE index  (0..3)
    /// </summary>
    public struct PacketGCDeleteSuccess : IPacket
    {
        public const byte PacketHeader = PacketHeaders.HEADER_GC_CHARACTER_DELETE_SUCCESS;
        public const int PacketSize = 2;

        public byte Header => PacketHeader;
        public int Length => PacketSize;

        public byte Index { get; set; }

        public PacketGCDeleteSuccess(byte index)
        {
            Index = index;
        }

        public override string ToString()
        {
            return $"PacketGCDeleteSuccess(Index={Index})";
        }
    }
}
