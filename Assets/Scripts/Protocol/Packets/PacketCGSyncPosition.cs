using System;
using Metin2.Protocol.Constants;

namespace Metin2.Protocol.Packets
{
    /// <summary>
    /// Outgoing sync-position batch for visible actors (dynamic, 3 + 12*N bytes).
    /// Server `game/src/packet.h:597-609`, handled `input_main.cpp:1782-1900+`
    /// (wSize validation: short → CLOSE; misaligned → error; count clamped to
    /// 16; per-victim sync-owner + 3500cm distance anti-hack);
    /// client `Packet.h:706-717`, sent per-actor
    /// (`PhaseGame.cpp:2697-2714`, from `PlayerEventHandler.cpp:214`).
    /// Our client mirrors the server clamp as fail-closed: at most
    /// <see cref="MaxElements"/> entries per packet.
    /// Wire layout:
    /// [0]     BYTE bHeader (8 = 0x08)
    /// [1..2]  WORD wSize (total bytes incl. header, LE)
    /// [3..]   N x {DWORD vid, long x, long y} (12 bytes each, LE)
    /// </summary>
    public struct PacketCGSyncPosition : IPacket
    {
        public const byte PacketHeader = PacketHeaders.HEADER_CG_SYNC_POSITION;
        public const int HeaderSize = 1 + 2; // 3 bytes
        public const int MaxElements = 16; // server clamp (input_main.cpp:1809-1818)
        public const int MaxPacketSize = HeaderSize + (SyncPositionElement.FieldSize * MaxElements);

        public byte Header => PacketHeader;
        public int Length => HeaderSize + (SyncPositionElement.FieldSize * ElementCount);

        public SyncPositionElement[] Elements { get; set; }

        // Raw count on purpose (no clamping): the codec and MovementClient
        // reject > MaxElements fail-closed instead of silently dropping actors.
        public int ElementCount => Elements != null ? Elements.Length : 0;

        public PacketCGSyncPosition(SyncPositionElement[] elements)
        {
            Elements = elements ?? new SyncPositionElement[0];
        }

        public override string ToString()
        {
            return $"PacketCGSyncPosition(Count={ElementCount})";
        }
    }
}
