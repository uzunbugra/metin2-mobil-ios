using Metin2.Protocol.Constants;

namespace Metin2.Protocol.Framing
{
    /// <summary>
    /// Source-verified fixed frame lengths for the server-to-client handshake path.
    /// Each entry quotes the C++ struct it was measured from; anything not listed
    /// here is treated as an unknown header by <see cref="PacketFramer"/>.
    ///
    /// Deliberately excluded (UNVERIFIED, see docs/protocol/packet-catalog.json):
    /// - GC ping: PacketHeaders.cs says 0xfe (packet.h:115) while packet-catalog.json
    ///   says header 44 (packet.h:172). Not registered until the conflict is resolved
    ///   from source with a golden sample.
    /// - Server-to-client dynamic packets (chat etc.): their size lives inside each
    ///   struct at UNVERIFIED offsets (docs/protocol/protocol-inventory.md §1).
    /// </summary>
    public static class PacketLengthTable
    {
        public static bool TryGetFixedLength(byte header, out int length)
        {
            // 0xff is shared by HEADER_CG_HANDSHAKE / HEADER_GC_HANDSHAKE (both 255);
            // this table covers the S2C handshake path, lengths are identical (13).
            if (header == PacketHeaders.HEADER_GC_HANDSHAKE)
            {
                // sizeof(TPacketGCHandshake) = 1+4+4+4 under #pragma pack(1), packet.h:789-795.
                length = 13;
                return true;
            }

            if (header == PacketHeaders.HEADER_GC_KEY_AGREEMENT)
            {
                // sizeof(TPacketKeyAgreement) = 1+2+2+256, packet.h:2226-2233.
                length = 261;
                return true;
            }

            if (header == PacketHeaders.HEADER_GC_KEY_AGREEMENT_COMPLETED)
            {
                // sizeof(TPacketKeyAgreementCompleted) = 1+3 dummy, packet.h:2235-2239.
                // PARTIALLY VERIFIED: no codec/golden sample yet, length from source only.
                length = 4;
                return true;
            }

            if (header == PacketHeaders.HEADER_GC_PHASE)
            {
                // sizeof(TPacketGCPhase) = BYTE header + BYTE phase, packet.h:814-818.
                length = 2;
                return true;
            }

            if (header == PacketHeaders.HEADER_GC_AUTH_SUCCESS)
            {
                // sizeof(TPacketGCAuthSuccess) = 1+4+1, packet.h:849-854
                // (client mirror UserInterface/Packet.h:2370-2375).
                length = 6;
                return true;
            }

            if (header == PacketHeaders.HEADER_GC_LOGIN_FAILURE)
            {
                // sizeof(TPacketGCLoginFailure) = 1+9, packet.h:856-860
                // (szStatus[ACCOUNT_STATUS_MAX_LEN+1], length.h:12;
                // client mirror UserInterface/Packet.h:1136-1141).
                length = 10;
                return true;
            }

            length = 0;
            return false;
        }
    }
}
