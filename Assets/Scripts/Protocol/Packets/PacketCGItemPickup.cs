using Metin2.Protocol.Constants;

namespace Metin2.Protocol.Packets
{
    /// <summary>
    /// Ground-item pickup request (TPacketCGItemPickup, 5 bytes).
    /// Verified from Server packet.h:668-672 and Client Packet.h (same struct).
    /// Server handler: CInputMain::ItemPickup (input_main.cpp:879-884) →
    /// CHARACTER::PickupItem(vid) — distance and ownership are validated
    /// server-side; the picked-up item arrives as GC_ITEM_SET.
    /// Wire layout (LE, pack(1)):
    /// [0]      BYTE header (15)
    /// [1..4]   DWORD vid (ground item)
    /// </summary>
    public struct PacketCGItemPickup : IPacket
    {
        public const byte PacketHeader = PacketHeaders.HEADER_CG_ITEM_PICKUP;
        public const int PacketSize = 5;

        public byte Header => PacketHeader;
        public int Length => PacketSize;

        public uint Vid { get; set; }

        public PacketCGItemPickup(uint vid)
        {
            Vid = vid;
        }

        public override string ToString()
        {
            return $"PacketCGItemPickup(Vid={Vid})";
        }
    }
}
