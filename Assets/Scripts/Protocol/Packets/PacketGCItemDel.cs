using System;
using Metin2.Protocol.Constants;

namespace Metin2.Protocol.Packets
{
    /// <summary>
    /// Item cell clear (server TPacketGCItemDelDeprecated, 42 bytes).
    /// Sent from `CHARACTER::SetItem` (`char_item.cpp:426-437`) whenever a cell
    /// loses its item — vnum/count/sockets/attrs zeroed.
    /// Verified both sides:
    /// Server `game/src/packet.h:1063-1071` (header = HEADER_GC_ITEM_DEL = 20);
    /// Client `UserInterface/Packet.h:1663-1671` (non-GAIDEN `TPacketGCItemSet`,
    /// 42B), handled `PhaseGameItem.cpp:214-236` (`RecvItemSetPacket`, header 20).
    /// NAME TRAP: despite the server type name, this is NOT the 2-byte
    /// `packet_item_del` (which nothing sends here) — header 20 always carries
    /// this 42B layout.
    /// Wire layout (total 42 = 1+3+4+1+12+21):
    /// [0]      BYTE header (20 = 0x14)
    /// [1..3]   TItemPos Cell (window u8 + cell u16 LE)
    /// [4..7]   DWORD vnum (zero on clear)
    /// [8]      BYTE count (zero on clear)
    /// [9..20]  long alSockets[3]
    /// [21..41] TPlayerItemAttribute aAttr[7]
    /// </summary>
    public struct PacketGCItemDel : IPacket
    {
        public const byte PacketHeader = PacketHeaders.HEADER_GC_ITEM_DEL;
        public const int PacketSize = 42;

        public byte Header => PacketHeader;
        public int Length => PacketSize;

        public byte Window { get; set; }
        public ushort Cell { get; set; }
        public uint Vnum { get; set; }
        public byte Count { get; set; }
        public int[] Sockets { get; set; }
        public ItemAttribute[] Attributes { get; set; }

        public PacketGCItemDel(
            byte window, ushort cell, uint vnum, byte count,
            int[] sockets, ItemAttribute[] attributes)
        {
            Window = window;
            Cell = cell;
            Vnum = vnum;
            Count = count;
            Sockets = PacketGCItemSet.PadSockets(sockets);
            Attributes = PacketGCItemSet.PadAttributes(attributes);
        }

        public override string ToString()
        {
            return $"PacketGCItemDel(Win={Window}, Cell={Cell})";
        }
    }
}
