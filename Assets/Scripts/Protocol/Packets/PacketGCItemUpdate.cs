using System;
using Metin2.Protocol.Constants;

namespace Metin2.Protocol.Packets
{
    /// <summary>
    /// In-place item mutation (TPacketGCItemUpdate, 38 bytes): count, sockets
    /// and attributes of an existing cell (stack split, refine, socketing...).
    /// Verified both sides:
    /// Server `game/src/packet.h:1108-1115` (header = HEADER_GC_ITEM_UPDATE = 25);
    /// Client `UserInterface/Packet.h:1703-1710`, handled
    /// `PhaseGameItem.cpp:278-294` (`RecvItemUpdatePacket`: count + sockets + attrs).
    /// Wire layout (total 38 = 1+3+1+12+21):
    /// [0]      BYTE header (25 = 0x19)
    /// [1..3]   TItemPos Cell (window u8 + cell u16 LE)
    /// [4]      BYTE count
    /// [5..16]  long alSockets[3]
    /// [17..37] TPlayerItemAttribute aAttr[7]
    /// </summary>
    public struct PacketGCItemUpdate : IPacket
    {
        public const byte PacketHeader = PacketHeaders.HEADER_GC_ITEM_UPDATE;
        public const int PacketSize = 38;

        public byte Header => PacketHeader;
        public int Length => PacketSize;

        public byte Window { get; set; }
        public ushort Cell { get; set; }
        public byte Count { get; set; }
        public int[] Sockets { get; set; }
        public ItemAttribute[] Attributes { get; set; }

        public PacketGCItemUpdate(
            byte window, ushort cell, byte count,
            int[] sockets, ItemAttribute[] attributes)
        {
            Window = window;
            Cell = cell;
            Count = count;
            Sockets = PacketGCItemSet.PadSockets(sockets);
            Attributes = PacketGCItemSet.PadAttributes(attributes);
        }

        public override string ToString()
        {
            return $"PacketGCItemUpdate(Win={Window}, Cell={Cell}, Count={Count})";
        }
    }
}
