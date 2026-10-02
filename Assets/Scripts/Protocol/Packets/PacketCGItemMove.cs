using Metin2.Protocol.Constants;

namespace Metin2.Protocol.Packets
{
    /// <summary>
    /// Move/stack/equip request (TPacketCGItemMove, 8 bytes).
    /// Verified from Server packet.h:660-666 and Client Packet.h (same struct).
    /// Server handler: CInputMain::ItemMove (input_main.cpp:871-877) →
    /// CHARACTER::MoveItem (char_item.cpp:5557+): position validity, exchanging/
    /// locked/irremovable guards, equip/unequip via destination window+cell,
    /// stack merge on same-vnum stackables (count 0 = whole stack).
    /// Outcome arrives as GC_ITEM_SET / GC_ITEM_DEL / GC_ITEM_UPDATE.
    /// Wire layout (LE, pack(1)):
    /// [0]      BYTE header (13)
    /// [1..3]   TItemPos Cell (source)
    /// [4..6]   TItemPos CellTo (destination)
    /// [7]      BYTE count (0 = whole stack)
    /// </summary>
    public struct PacketCGItemMove : IPacket
    {
        public const byte PacketHeader = PacketHeaders.HEADER_CG_ITEM_MOVE;
        public const int PacketSize = 8;

        public byte Header => PacketHeader;
        public int Length => PacketSize;

        public byte Window { get; set; }
        public ushort Cell { get; set; }
        public byte WindowTo { get; set; }
        public ushort CellTo { get; set; }
        public byte Count { get; set; }

        public PacketCGItemMove(byte window, ushort cell, byte windowTo, ushort cellTo, byte count = 0)
        {
            Window = window;
            Cell = cell;
            WindowTo = windowTo;
            CellTo = cellTo;
            Count = count;
        }

        public override string ToString()
        {
            return $"PacketCGItemMove(Win={Window}, Cell={Cell} -> Win={WindowTo}, Cell={CellTo}, Count={Count})";
        }
    }
}
