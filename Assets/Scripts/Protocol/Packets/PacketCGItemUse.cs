using Metin2.Protocol.Constants;

namespace Metin2.Protocol.Packets
{
    /// <summary>
    /// Use-item request (TPacketCGItemUse, 4 bytes).
    /// Verified from Server packet.h:632-636 and Client Packet.h (same struct).
    /// Server handler: CInputMain::ItemUse (input_main.cpp:830-833, observer-
    /// mode guarded at dispatch input_main.cpp:3142-3145) → CHARACTER::UseItem.
    /// Result arrives as GC_ITEM_SET / GC_ITEM_UPDATE / point changes — the
    /// use itself has no direct reply packet.
    /// Wire layout (LE, pack(1)):
    /// [0]      BYTE header (11)
    /// [1..3]   TItemPos Cell (window u8 + cell u16)
    /// </summary>
    public struct PacketCGItemUse : IPacket
    {
        public const byte PacketHeader = PacketHeaders.HEADER_CG_ITEM_USE;
        public const int PacketSize = 4;

        public byte Header => PacketHeader;
        public int Length => PacketSize;

        public byte Window { get; set; }
        public ushort Cell { get; set; }

        public PacketCGItemUse(byte window, ushort cell)
        {
            Window = window;
            Cell = cell;
        }

        public override string ToString()
        {
            return $"PacketCGItemUse(Win={Window}, Cell={Cell})";
        }
    }
}
