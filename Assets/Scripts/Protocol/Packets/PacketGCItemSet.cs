using System;
using Metin2.Protocol.Constants;

namespace Metin2.Protocol.Packets
{
    /// <summary>
    /// Full item cell assignment (server TPacketGCItemSet, 51 bytes).
    /// Sent from `CHARACTER::SetItem` (`char_item.cpp:408-425`) whenever a cell
    /// gains an item (loading, pickup, move, equip, buy, ...).
    /// Verified both sides:
    /// Server `game/src/packet.h:1073-1084` (`ITEM_SOCKET_MAX_NUM=3`,
    /// `ITEM_ATTRIBUTE_MAX_NUM=7`, `common/item_length.h:11-13`);
    /// Client `UserInterface/Packet.h:1673-1684` (`TPacketGCItemSet2`,
    /// `GameType.h:299-300` → 3/7), handled `PhaseGameItem.cpp:238-264`
    /// (`RecvItemSetPacket2`, header 21).
    /// NAME TRAP: the server calls header 21 ITEM_SET while the client calls it
    /// ITEM_SET2 — the 51B wire layout is what matters, and it matches.
    /// Wire layout (total 51 = 1+3+4+1+4+4+1+12+21):
    /// [0]      BYTE header (21 = 0x15)
    /// [1..3]   TItemPos Cell (window u8 + cell u16 LE)
    /// [4..7]   DWORD vnum
    /// [8]      BYTE count
    /// [9..12]  DWORD flags
    /// [13..16] DWORD anti_flags
    /// [17]     bool highlight (1 byte)
    /// [18..29] long alSockets[3] (4 bytes LE each)
    /// [30..50] TPlayerItemAttribute aAttr[7] (3 bytes each)
    /// </summary>
    public struct PacketGCItemSet : IPacket
    {
        public const byte PacketHeader = PacketHeaders.HEADER_GC_ITEM_SET;
        public const int SocketCount = 3;
        public const int AttributeCount = 7;
        public const int PacketSize = 51;

        public byte Header => PacketHeader;
        public int Length => PacketSize;

        public byte Window { get; set; }
        public ushort Cell { get; set; }
        public uint Vnum { get; set; }
        public byte Count { get; set; }
        public uint Flags { get; set; }
        public uint AntiFlags { get; set; }
        public bool Highlight { get; set; }
        public int[] Sockets { get; set; }
        public ItemAttribute[] Attributes { get; set; }

        public PacketGCItemSet(
            byte window, ushort cell, uint vnum, byte count,
            uint flags, uint antiFlags, bool highlight,
            int[] sockets, ItemAttribute[] attributes)
        {
            Window = window;
            Cell = cell;
            Vnum = vnum;
            Count = count;
            Flags = flags;
            AntiFlags = antiFlags;
            Highlight = highlight;
            Sockets = PadSockets(sockets);
            Attributes = PadAttributes(attributes);
        }

        internal static int[] PadSockets(int[] source)
        {
            var result = new int[SocketCount];
            if (source != null)
            {
                Array.Copy(source, 0, result, 0, Math.Min(source.Length, SocketCount));
            }

            return result;
        }

        internal static ItemAttribute[] PadAttributes(ItemAttribute[] source)
        {
            var result = new ItemAttribute[AttributeCount];
            if (source != null)
            {
                Array.Copy(source, 0, result, 0, Math.Min(source.Length, AttributeCount));
            }

            return result;
        }

        public override string ToString()
        {
            return $"PacketGCItemSet(Win={Window}, Cell={Cell}, Vnum={Vnum}, Count={Count})";
        }
    }
}
