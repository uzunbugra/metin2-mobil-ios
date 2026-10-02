using Metin2.Protocol.Constants;

namespace Metin2.Protocol.Packets
{
    /// <summary>
    /// Drop item or gold (TPacketCGItemDrop, 8 bytes).
    /// Verified from Server packet.h:645-650 and Client Packet.h (same struct;
    /// client send site PythonNetworkStreamPhaseGameItem.cpp:547-564).
    /// Server handler: CInputMain::ItemDrop (input_main.cpp:842-854):
    /// gold &gt; 0 → DropGold(gold), else DropItem(Cell).
    /// Wire layout (LE, pack(1)):
    /// [0]      BYTE header (12)
    /// [1..3]   TItemPos Cell
    /// [4..7]   DWORD gold (0 to drop the item, &gt; 0 to drop gold)
    /// </summary>
    public struct PacketCGItemDrop : IPacket
    {
        public const byte PacketHeader = PacketHeaders.HEADER_CG_ITEM_DROP;
        public const int PacketSize = 8;

        public byte Header => PacketHeader;
        public int Length => PacketSize;

        public byte Window { get; set; }
        public ushort Cell { get; set; }
        public uint Gold { get; set; }

        public PacketCGItemDrop(byte window, ushort cell, uint gold = 0)
        {
            Window = window;
            Cell = cell;
            Gold = gold;
        }

        public override string ToString()
        {
            return $"PacketCGItemDrop(Win={Window}, Cell={Cell}, Gold={Gold})";
        }
    }
}
