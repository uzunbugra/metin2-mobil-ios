using Metin2.Protocol.Constants;

namespace Metin2.Protocol.Packets
{
    /// <summary>
    /// Motion/animation broadcast (TPacketGCMotion, 11 bytes).
    /// Verified from Server packet.h:1158-1164 (filled by
    /// CHARACTER::MotionPacketEncode and broadcast via CHARACTER::Motion →
    /// PacketAround, char.cpp:3761-3778) and Client Packet.h:1617-1623.
    /// This is how observers see other characters' attack animations —
    /// HEADER_GC_ATTACK (12) is a dead constant with no sender in this build.
    /// Client registration: PythonNetworkStream.cpp:89; dispatch:
    /// PhaseGame.cpp:356.
    /// Wire layout (LE, pack(1)):
    /// [0]      BYTE header (36)
    /// [1..4]   DWORD vid (the acting character)
    /// [5..8]   DWORD victimVid (0 when victimless)
    /// [9..10]  WORD motion (client animation key)
    /// </summary>
    public struct PacketGCMotion : IPacket
    {
        public const byte PacketHeader = PacketHeaders.HEADER_GC_MOTION;
        public const int PacketSize = 11;

        public byte Header => PacketHeader;
        public int Length => PacketSize;

        public uint Vid { get; set; }
        public uint VictimVid { get; set; }
        public ushort Motion { get; set; }

        public PacketGCMotion(uint vid, uint victimVid, ushort motion)
        {
            Vid = vid;
            VictimVid = victimVid;
            Motion = motion;
        }

        public override string ToString()
        {
            return $"PacketGCMotion(Vid={Vid}, VictimVid={VictimVid}, Motion={Motion})";
        }
    }
}
