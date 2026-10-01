using Metin2.Protocol.Constants;

namespace Metin2.Protocol.Packets
{
    /// <summary>
    /// Character create failure (TPacketGCCreateFailure, 2 bytes).
    /// Verified both sides:
    /// Server `game/src/packet.h:862-866`, sent `input_login.cpp:427-482`
    /// (bType=0 blocked/overlong/bad-job, bType=1 bad-name Canada/Europe);
    /// Client `UserInterface/Packet.h:1163-1167`, handled
    /// `PhaseSelect.cpp:252-262` (surfaced with bType).
    /// NOTE quirk: one path (`input_db.cpp:199`, DB-returned bad slot) sends
    /// only the 1-byte header without bType — the C++ client then stalls on
    /// `Recv(2)` exactly like our framer does; documented, not worked around.
    /// Wire layout:
    /// [0] BYTE header (9 = 0x09)
    /// [1] BYTE bType  (0/1)
    /// </summary>
    public struct PacketGCCreateFailure : IPacket
    {
        public const byte PacketHeader = PacketHeaders.HEADER_GC_CHARACTER_CREATE_FAILURE;
        public const int PacketSize = 2;

        public byte Header => PacketHeader;
        public int Length => PacketSize;

        public byte Type { get; set; }

        public PacketGCCreateFailure(byte type)
        {
            Type = type;
        }

        public override string ToString()
        {
            return $"PacketGCCreateFailure(Type={Type})";
        }
    }
}
