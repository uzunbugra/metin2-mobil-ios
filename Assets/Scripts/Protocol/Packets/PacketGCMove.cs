using Metin2.Protocol.Constants;

namespace Metin2.Protocol.Packets
{
    /// <summary>
    /// Broadcast movement of another actor (TPacketGCMove, 16 bytes).
    /// Verified: server `game/src/packet.h:1288-1299`, sent
    /// `input_main.cpp:1651-1663` (`PacketAround`, i.e. everyone EXCEPT the
    /// mover — the mover's own position is authoritative via its request,
    /// corrected only by reshow/teleport on violation).
    /// Wire layout (total 24 = 1+3+4+4+4+4+4):
    /// [0]      BYTE  bHeader (3 = 0x03)
    /// [1]      BYTE  bFunc
    /// [2]      BYTE  bArg
    /// [3]      BYTE  bRot
    /// [4..7]   DWORD dwVID
    /// [8..11]  long  lX (cm, LE)
    /// [12..15] long  lY (cm, LE)
    /// [16..19] DWORD dwTime
    /// [20..23] DWORD dwDuration (FUNC_MOVE travel time, else 0)
    /// </summary>
    public struct PacketGCMove : IPacket
    {
        public const byte PacketHeader = PacketHeaders.HEADER_GC_MOVE;
        public const int PacketSize = 24;

        public byte Header => PacketHeader;
        public int Length => PacketSize;

        public byte Func { get; set; }
        public byte Arg { get; set; }
        public byte Rot { get; set; }
        public uint Vid { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public uint Time { get; set; }
        public uint Duration { get; set; }

        public override string ToString()
        {
            return $"PacketGCMove(Vid={Vid}, Func={Func}, Pos=({X},{Y}))";
        }
    }
}
