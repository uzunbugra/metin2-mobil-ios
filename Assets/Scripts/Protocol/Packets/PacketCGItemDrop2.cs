using Metin2.Protocol.Constants;

namespace Metin2.Protocol.Packets
{
    /// <summary>
    /// Drop a partial stack or gold (TPacketCGItemDrop2, 9 bytes).
    /// Verified from Server packet.h:652-658 and Client Packet.h (same struct;
    /// client send site PythonNetworkStreamPhaseGameItem.cpp:566-584).
    /// Server handler: CInputMain::ItemDrop2 (input_main.cpp:856-869):
    /// gold &gt; 0 → DropGold(gold), else DropItem(Cell, count).
    /// Wire layout (LE, pack(1)):
    /// [0]      BYTE header (20)
    /// [1..3]   TItemPos Cell
    /// [4..7]   DWORD gold (0 to drop the item, &gt; 0 to drop gold)
    /// [8]      BYTE count
    /// </summary>
    public struct PacketCGItemDrop2 : IPacket
    {
        public const byte PacketHeader = PacketHeaders.HEADER_CG_ITEM_DROP2;
        public const int PacketSize = 9;

        public byte Header => PacketHeader;
        public int Length => PacketSize;

        public byte Window { get; set; }
        public ushort Cell { get; set; }
        public uint Gold { get; set; }
        public byte Count { get; set; }

        public PacketCGItemDrop2(byte window, ushort cell, uint gold, byte count)
        {
            Window = window;
            Cell = cell;
            Gold = gold;
            Count = count;
        }

        public override string ToString()
        {
            return $"PacketCGItemDrop2(Win={Window}, Cell={Cell}, Gold={Gold}, Count={Count})";
        }
    }
}
