using Metin2.Protocol.Constants;

namespace Metin2.Protocol.Packets
{
    /// <summary>
    /// World entry request (TPacketCGEnterGame, 1 byte).
    /// Verified both sides:
    /// Server `game/src/packet.h:627-630`, handled `input_login.cpp:546-579`
    /// (`Entergame`: needs a loaded character else PHASE_CLOSE, then
    /// Show() + SetPhase(PHASE_GAME) + time/channel greet);
    /// Client `UserInterface/Packet.h:564-567` (`TPacketCGEnterFrontGame`),
    /// sent `PhaseLoading.cpp` (`SendEnterGame`).
    /// Wire layout:
    /// [0] BYTE header (10 = 0x0a)
    /// </summary>
    public struct PacketCGEnterGame : IPacket
    {
        public const byte PacketHeader = PacketHeaders.HEADER_CG_ENTERGAME;
        public const int PacketSize = 1;

        public byte Header => PacketHeader;
        public int Length => PacketSize;
    }
}
