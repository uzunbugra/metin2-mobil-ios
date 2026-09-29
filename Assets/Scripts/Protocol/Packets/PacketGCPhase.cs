using Metin2.Protocol.Constants;

namespace Metin2.Protocol.Packets
{
    /// <summary>
    /// Server phase transition packet (TPacketGCPhase, 2 bytes).
    /// Verified from Server packet.h:814-818 and desc.cpp:522-525.
    /// Wire layout:
    /// [0] BYTE header (0xfd = 253)
    /// [1] BYTE phase  (EPhase enum value, 0..11)
    /// </summary>
    public struct PacketGCPhase : IPacket
    {
        public const byte PacketHeader = PacketHeaders.HEADER_GC_PHASE;
        public const int PacketSize = 2;

        public byte Header => PacketHeader;
        public int Length => PacketSize;

        public PhaseType Phase { get; set; }

        public PacketGCPhase(PhaseType phase)
        {
            Phase = phase;
        }

        public override string ToString()
        {
            return $"PacketGCPhase(Phase={Phase} [{(byte)Phase}])";
        }
    }
}
