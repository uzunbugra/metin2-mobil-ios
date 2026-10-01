using Metin2.Protocol.Constants;

namespace Metin2.Protocol.Packets
{
    /// <summary>
    /// Auth/login failure packet (TPacketGCLoginFailure, 10 bytes).
    /// Verified both sides:
    /// Server `game/src/packet.h:856-860` (`szStatus[ACCOUNT_STATUS_MAX_LEN+1]`,
    /// `common/length.h:12` → 8+1 = 9), sent `input.cpp:177-188`
    /// (statuses observed: "NOID", "ALREADY", "WRONGPWD", "SHUTDOWN",
    /// plus DB status passthrough `input_db.cpp:142`);
    /// Client `UserInterface/Packet.h:1136-1141` (`LOGIN_STATUS_MAX_LEN = 8`),
    /// handled `PhaseLogin.cpp:204-212` and `AccountConnector.cpp:340-352`.
    /// Wire layout:
    /// [0]     BYTE header    (7 = 0x07)
    /// [1..9]  char szStatus[9] (ASCII, null-padded)
    /// </summary>
    public struct PacketGCLoginFailure : IPacket
    {
        public const byte PacketHeader = PacketHeaders.HEADER_GC_LOGIN_FAILURE;
        public const int StatusBufferLen = 9; // ACCOUNT_STATUS_MAX_LEN + 1
        public const int StatusMaxChars = 8;
        public const int PacketSize = 1 + StatusBufferLen; // 10 bytes

        public byte Header => PacketHeader;
        public int Length => PacketSize;

        public string Status { get; set; }

        public PacketGCLoginFailure(string status)
        {
            Status = status ?? string.Empty;
        }

        public override string ToString()
        {
            return $"PacketGCLoginFailure(Status='{Status}')";
        }
    }
}
