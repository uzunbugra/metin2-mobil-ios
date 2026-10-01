namespace Metin2.Protocol.Constants
{
    /// <summary>
    /// Source-verified packet header constants from C++ Server and Client source code.
    /// References:
    /// Server: source/Razuning-V5/Server/game/src/packet.h
    /// Client: source/Client Source/source/UserInterface/Packet.h
    /// </summary>
    public static class PacketHeaders
    {
        // Handshake & Time Sync
        public const byte HEADER_CG_HANDSHAKE = 0xff;          // 255 (packet.h:493)
        public const byte HEADER_GC_HANDSHAKE = 0xff;          // 255 (packet.h:789)
        public const byte HEADER_CG_TIME_SYNC = 0xfc;          // 252 (PythonNetworkStreamPhaseHandShake.cpp:133)
        public const byte HEADER_GC_PING = 0xfe;               // 254 (packet.h:115)
        public const byte HEADER_CG_PONG = 0xfe;               // 254 (packet.h:10)

        // Key Agreement & Crypto
        public const byte HEADER_CG_KEY_AGREEMENT = 0xfb;      // 251 (packet.h:9)
        public const byte HEADER_GC_KEY_AGREEMENT = 0xfb;      // 251 (packet.h:112)
        public const byte HEADER_GC_KEY_AGREEMENT_COMPLETED = 0xfa; // 250 (packet.h:111)

        // Phase Management
        public const byte HEADER_GC_PHASE = 0xfd;              // 253 (packet.h:114)

        // Authentication & Login
        public const byte HEADER_CG_LOGIN3 = 111;              // 0x6f (packet.h:86, input_auth.cpp:102)
        public const byte HEADER_CG_LOGIN2 = 109;              // 0x6d (packet.h:84, input_login.cpp:138)
        public const byte HEADER_GC_AUTH_SUCCESS = 150;        // 0x96 (packet.h:266, input_db.cpp:1685)
        public const byte HEADER_GC_LOGIN_FAILURE = 7;         // 0x07 (packet.h:126, input.cpp:177)

        // Character Select
        public const byte HEADER_GC_EMPIRE = 90;               // 0x5a (packet.h:206, input_db.cpp:157)
        public const byte HEADER_GC_LOGIN_SUCCESS_NEWSLOT = 32; // 0x20 (packet.h:125, desc.cpp:892)
    }
}
