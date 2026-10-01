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

            if (header == PacketHeaders.HEADER_GC_EMPIRE)
            {
                // sizeof(TPacketGCEmpire) = 1+1, packet.h:1638-1642
                // (client mirror UserInterface/Packet.h:2083-2087).
                length = 2;
                return true;
            }

            if (header == PacketHeaders.HEADER_GC_LOGIN_SUCCESS_NEWSLOT)
            {
                // sizeof(TPacketGCLoginSuccess) = 1+63*4+4*4+13*4+4+4 = 329,
                // packet.h:838-847 (client mirror UserInterface/Packet.h:1125-1133).
                length = 329;
                return true;
            }

            if (header == PacketHeaders.HEADER_GC_CHARACTER_CREATE_SUCCESS)
            {
                // sizeof(TPacketGCPlayerCreateSuccess) = 1+1+63 = 65,
                // packet.h:556-561 (client mirror UserInterface/Packet.h:1156-1161).
                length = 65;
                return true;
            }

            if (header == PacketHeaders.HEADER_GC_CHARACTER_CREATE_FAILURE)
            {
                // sizeof(TPacketGCCreateFailure) = 1+1 = 2, packet.h:862-866
                // (client mirror UserInterface/Packet.h:1163-1167).
                // NOTE quirk: input_db.cpp:199 sends a bare 1-byte 9 header on
                // one path; both our framer and the C++ client stall on it
                // identically (documented, not worked around).
                length = 2;
                return true;
            }

            if (header == PacketHeaders.HEADER_GC_CHARACTER_DELETE_SUCCESS)
            {
                // 2 one-byte Packet() calls: header 10 + slot index
                // (input_db.cpp:285-286; client UserInterface/Packet.h:1176-1180).
                length = 2;
                return true;
            }

            if (header == PacketHeaders.HEADER_GC_CHARACTER_DELETE_WRONG_SOCIAL_ID)
            {
                // Single 1-byte Packet() of header 11 (input_db.cpp:296;
                // client reads 1-byte TPacketGCBlank, PhaseSelect.cpp:278-286).
                length = 1;
                return true;
            }

            if (header == PacketHeaders.HEADER_GC_MAIN_CHARACTER2_EMPIRE)
            {
                // sizeof(TPacketGCMainCharacter2_EMPIRE) = 1+4+2+25+12+1+1 = 46,
                // server packet.h:982-991 (client mirror UserInterface/Packet.h:1386-1395).
                length = 46;
                return true;
            }

            if (header == PacketHeaders.HEADER_GC_TIME)
            {
                // sizeof(TPacketGCTime) = 1+4 = 5, server packet.h:1891-1895
                // (time_t is 4B both sides: 32-bit server, _USE_32BIT_TIME_T client).
                length = 5;
                return true;
            }

            if (header == PacketHeaders.HEADER_GC_CHANNEL)
            {
                // sizeof(TPacketGCChannel) = 1+1 = 2, server packet.h:1995-1999
                // (client mirror UserInterface/Packet.h:2377-2381).
                length = 2;
                return true;
            }

            if (header == PacketHeaders.HEADER_GC_CHARACTER_POINTS)
            {
                // sizeof(TPacketGCPoints) = 1+4*255 = 1021,
                // server packet.h:1030-1034 (client mirror UserInterface/Packet.h:1600-1604).
                length = 1021;
                return true;
            }

            if (header == PacketHeaders.HEADER_GC_SKILL_LEVEL)
            {
                // sizeof(TPacketGCSkillLevel) = 1+6*255 = 1531,
                // server packet.h:1036-1040 (client mirror UserInterface/Packet.h:1977-1981).
                length = 1531;
                return true;
            }

            if (header == PacketHeaders.HEADER_GC_CHARACTER_ADD)
            {
                // sizeof(TPacketGCCharacterAdd) = 35, server packet.h:886-903
                // (client mirror UserInterface/Packet.h:1226-1252).
                length = 35;
                return true;
            }

            if (header == PacketHeaders.HEADER_GC_CHARACTER_DEL)
            {
                // sizeof(TPacketGCCharacterDelete) = 1+4 = 5, server packet.h:959-963.
                length = 5;
                return true;
            }

            length = 0;
            return false;
        }
    }
}
