using System;
using Metin2.Protocol.Constants;

namespace Metin2.Protocol.Packets
{
    /// <summary>
    /// Incoming sync-position batch (dynamic, 3 + 12*N bytes).
    /// Server `game/src/packet.h:1310-1322` (rebroadcast of validated CG syncs);
    /// client `Packet.h:1947-1953`; wSize rule documented inline
    /// (`packet.h:1321`: count = (wSize - 3) / 12).
    /// Wire layout: same as <see cref="PacketCGSyncPosition"/> with
    /// header 5 = 0x05.
    /// </summary>
    public struct PacketGCSyncPosition : IPacket
    {
        public const byte PacketHeader = PacketHeaders.HEADER_GC_SYNC_POSITION;
        public const int HeaderSize = 1 + 2; // 3 bytes
        public const int MaxElements = 16;
        public const int MaxPacketSize = HeaderSize + (SyncPositionElement.FieldSize * MaxElements);

        public byte Header => PacketHeader;
        public int Length => HeaderSize + (SyncPositionElement.FieldSize * ElementCount);

        public SyncPositionElement[] Elements { get; set; }

        public int ElementCount => Elements != null ? Elements.Length : 0;

        public PacketGCSyncPosition(SyncPositionElement[] elements)
        {
            Elements = elements ?? new SyncPositionElement[0];
        }

        public override string ToString()
        {
            return $"PacketGCSyncPosition(Count={ElementCount})";
        }
    }
}
