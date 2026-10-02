using Metin2.Protocol.Constants;

namespace Metin2.Protocol.Packets
{
    /// <summary>
    /// Use-item-on-item request (TPacketCGItemUseToItem, 7 bytes).
    /// Verified from Server packet.h:638-643 and Client Packet.h (same struct;
    /// client send site PythonNetworkStreamPhaseGameItem.cpp:~520-545).
    /// Server handler: CInputMain::ItemToItem (input_main.cpp:835-840) →
    /// CHARACTER::UseItem(Cell, TargetCell) — e.g. upgrade stones applied to
    /// equipment; outcome arrives via the item/point update packets.
    /// Wire layout (LE, pack(1)):
    /// [0]      BYTE header (60)
    /// [1..3]   TItemPos Cell (the applying item)
    /// [4..6]   TItemPos TargetCell (the target item)
    /// </summary>
    public struct PacketCGItemUseToItem : IPacket
    {
        public const byte PacketHeader = PacketHeaders.HEADER_CG_ITEM_USE_TO_ITEM;
        public const int PacketSize = 7;

        public byte Header => PacketHeader;
        public int Length => PacketSize;

        public byte Window { get; set; }
        public ushort Cell { get; set; }
        public byte TargetWindow { get; set; }
        public ushort TargetCell { get; set; }

        public PacketCGItemUseToItem(byte window, ushort cell, byte targetWindow, ushort targetCell)
        {
            Window = window;
            Cell = cell;
            TargetWindow = targetWindow;
            TargetCell = targetCell;
        }

        public override string ToString()
        {
            return $"PacketCGItemUseToItem(Win={Window}, Cell={Cell} -> Win={TargetWindow}, Cell={TargetCell})";
        }
    }
}
