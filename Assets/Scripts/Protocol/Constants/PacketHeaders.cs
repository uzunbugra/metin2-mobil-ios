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
        public const byte HEADER_CG_HANDSHAKE = 0xff;          // 255 (packet.h:6)
        public const byte HEADER_GC_HANDSHAKE = 0xff;          // 255 (packet.h:116)
        public const byte HEADER_CG_TIME_SYNC = 0xfc;          // 252 (client Packet.h:135; send site PythonNetworkStreamPhaseHandShake.cpp:136)
        public const byte HEADER_GC_PING = 44;                 // 0x2c (packet.h:172; client Packet.h:195; TPacketGCPing = header only)
        public const byte HEADER_CG_PONG = 0xfe;               // 254 (packet.h:7; client Packet.h:138)

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

        // Items
        public const byte HEADER_GC_ITEM_DEL = 20;         // 0x14 (server packet.h:144; client Packet.h non-GAIDEN branch)
        public const byte HEADER_GC_ITEM_SET = 21;         // 0x15 (server packet.h:145; client Packet.h SET2)
        public const byte HEADER_GC_ITEM_UPDATE = 25;      // 0x19 (server packet.h:148; client Packet.h)

        // Items (C2S actions; read side is GC 21/20/25)
        public const byte HEADER_CG_ITEM_USE = 11;        // 0x0b (server packet.h:21; client Packet.h:22)
        public const byte HEADER_CG_ITEM_DROP = 12;       // 0x0c (server packet.h:22; client Packet.h:23)
        public const byte HEADER_CG_ITEM_MOVE = 13;       // 0x0d (server packet.h:23; client Packet.h:24)
        public const byte HEADER_CG_ITEM_PICKUP = 15;     // 0x0f (server packet.h:24; client Packet.h:25)
        public const byte HEADER_CG_ITEM_DROP2 = 20;      // 0x14 (server packet.h:30; client Packet.h:30)
        public const byte HEADER_CG_ITEM_USE_TO_ITEM = 60; // 0x3c (server packet.h:46; client Packet.h:70)

        // Movement
        public const byte HEADER_CG_MOVE = 7;              // 0x07 (server packet.h:17; client Packet.h:18 CHARACTER_MOVE)
        public const byte HEADER_CG_SYNC_POSITION = 8;    // 0x08 (server packet.h:18; client Packet.h:19)
        public const byte HEADER_GC_MOVE = 3;              // 0x03 (server packet.h:120; client Packet.h:145 CHARACTER_MOVE)
        public const byte HEADER_GC_SYNC_POSITION = 5;    // 0x05 (server packet.h:122; client Packet.h:147)

        // Combat
        public const byte HEADER_CG_ATTACK = 2;            // 0x02 (server packet.h:12; client Packet.h:13)
        public const byte HEADER_GC_POINT_CHANGE = 17;     // 0x11 (server packet.h:139 HEADER_GC_CHARACTER_POINT_CHANGE; client Packet.h:160 HEADER_GC_PLAYER_POINT_CHANGE — same value 17, names differ across sides)
        public const byte HEADER_GC_STUN = 13;             // 0x0d (server packet.h:134; client Packet.h:155)
        public const byte HEADER_GC_DEAD = 14;             // 0x0e (server packet.h:135; client Packet.h:156)
        public const byte HEADER_GC_MOTION = 36;           // 0x24 (server packet.h:161)
        public const byte HEADER_GC_DAMAGE_INFO = 135;     // 0x87 (server packet.h:258)
    }
}
