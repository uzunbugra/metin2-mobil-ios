using Metin2.Protocol.Constants;

namespace Metin2.Protocol.Packets
{
    /// <summary>
    /// Server clock sync (TPacketGCTime, 5 bytes).
    /// Verified both sides:
    /// Server `game/src/packet.h:1891-1895` (`time_t time`), sent
    /// `input_login.cpp:612-615` (`Entergame`, right after PHASE_GAME);
    /// Client `UserInterface/Packet.h:2240-2244`, handled
    /// `PhaseGame.cpp:3907-3914` (`SetServerTime`).
    /// ABI note: `time_t` is 4 bytes on BOTH sides here — server is a 32-bit
    /// FreeBSD binary, and the client forces `_USE_32BIT_TIME_T`
    /// (`source/.../UserInterface/StdAfx.h:14`) despite the modern toolchain.
    /// A 64-bit `time_t` on either side would silently desync the stream.
    /// Wire layout:
    /// [0]     BYTE  bHeader (106 = 0x6a)
    /// [1..4]  time_t time (4 bytes LE, unix seconds)
    /// </summary>
    public struct PacketGCTime : IPacket
    {
        public const byte PacketHeader = PacketHeaders.HEADER_GC_TIME;
        public const int PacketSize = 1 + 4; // 5 bytes

        public byte Header => PacketHeader;
        public int Length => PacketSize;

        public uint Time { get; set; }

        public PacketGCTime(uint time)
        {
            Time = time;
        }

        public override string ToString()
        {
            return $"PacketGCTime(Time={Time})";
        }
    }
}
