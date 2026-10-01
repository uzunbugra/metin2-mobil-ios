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
        public const byte HEADER_CG_EMPIRE = 90;               // 0x5a (packet.h:73, input_login.cpp:792)
        public const byte HEADER_CG_CHARACTER_SELECT = 6;      // 0x06 (packet.h:16, input_login.cpp:222)
        public const byte HEADER_CG_CHARACTER_CREATE = 4;      // 0x04 (packet.h:14, input_login.cpp:416)
        public const byte HEADER_CG_CHARACTER_DELETE = 5;      // 0x05 (packet.h:15, input_login.cpp:501)
        public const byte HEADER_CG_ENTERGAME = 10;            // 0x0a (packet.h:19, input_login.cpp:546)
        public const byte HEADER_GC_CHARACTER_CREATE_SUCCESS = 8; // 0x08 (packet.h:128, input_db.cpp:221)
        public const byte HEADER_GC_CHARACTER_CREATE_FAILURE = 9; // 0x09 (packet.h:129, input_login.cpp:427)
        public const byte HEADER_GC_CHARACTER_DELETE_SUCCESS = 10; // 0x0a (packet.h:130, input_db.cpp:278)
        public const byte HEADER_GC_CHARACTER_DELETE_WRONG_SOCIAL_ID = 11; // 0x0b (packet.h:131, input_db.cpp:291)

        // World Entry
        public const byte HEADER_GC_MAIN_CHARACTER2_EMPIRE = 113; // 0x71 (server packet.h:225; client Packet.h:261, non-GAIDEN branch)
        public const byte HEADER_GC_TIME = 106;            // 0x6a (server packet.h:218; client Packet.h:248)
        public const byte HEADER_GC_CHANNEL = 121;         // 0x79 (server packet.h:234; client Packet.h:272)

        // Loading Stats & Spawn
        public const byte HEADER_GC_CHARACTER_POINTS = 16; // 0x10 (server packet.h:138; client Packet.h:159)
        public const byte HEADER_GC_SKILL_LEVEL = 76;      // 0x4c (server packet.h:186; client Packet.h:212 SKILL_LEVEL_NEW)
        public const byte HEADER_GC_CHARACTER_ADD = 1;     // 0x01 (server packet.h:118; client Packet.h:143)
        public const byte HEADER_GC_CHARACTER_DEL = 2;     // 0x02 (server packet.h:119; client Packet.h:144)
    }
}
