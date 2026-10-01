using System;
using Metin2.Protocol.Constants;

namespace Metin2.Protocol.Packets
{
    /// <summary>
    /// Character-list packet for the select screen
    /// (TPacketGCLoginSuccess with NEWSLOT header, 329 bytes).
    /// Verified both sides:
    /// Server `game/src/packet.h:838-847`, sent `desc.cpp:892-919`
    /// (`SendLoginSuccessPacket`: header = HEADER_GC_LOGIN_SUCCESS_NEWSLOT,
    /// players/guilds copied from the account table, handle + mark random_key);
    /// Client `UserInterface/Packet.h:1125-1133` (`TPacketGCLoginSuccess4`),
    /// handled `PhaseLogin.cpp:162-188` (slots + guilds + mark handle/key).
    /// Wire layout (total 329 = 1 + 63*4 + 4*4 + 13*4 + 4 + 4):
    /// [0]        BYTE  bHeader (32 = 0x20)
    /// [1..252]   TSimplePlayer players[4] (63 bytes each)
    /// [253..268] DWORD guild_id[4]
    /// [269..320] char  guild_name[4][13] (GUILD_NAME_MAX_LEN+1, length.h:23)
    /// [321..324] DWORD handle (mark auth handle)
    /// [325..328] DWORD random_key (mark auth random key)
    /// </summary>
    public struct PacketGCLoginSuccess : IPacket
    {
        public const byte PacketHeader = PacketHeaders.HEADER_GC_LOGIN_SUCCESS_NEWSLOT;
        public const int SlotCount = 4; // PLAYER_PER_ACCOUNT (length.h:11)
        public const int GuildNameBufferLen = 13; // GUILD_NAME_MAX_LEN + 1 (length.h:23)
        public const int PacketSize = 1
            + (SimplePlayer.FieldSize * SlotCount)
            + (4 * SlotCount)
            + (GuildNameBufferLen * SlotCount)
            + 4 + 4; // 329 bytes

        public byte Header => PacketHeader;
        public int Length => PacketSize;

        public SimplePlayer[] Players { get; set; }
        public uint[] GuildIds { get; set; }
        public string[] GuildNames { get; set; }
        public uint Handle { get; set; }
        public uint RandomKey { get; set; }

        public PacketGCLoginSuccess(
            SimplePlayer[] players, uint[] guildIds, string[] guildNames, uint handle, uint randomKey)
        {
            Players = PadPlayers(players);
            GuildIds = PadUints(guildIds);
            GuildNames = PadNames(guildNames);
            Handle = handle;
            RandomKey = randomKey;
        }

        private static SimplePlayer[] PadPlayers(SimplePlayer[] source)
        {
            var result = new SimplePlayer[SlotCount];
            if (source != null)
            {
                Array.Copy(source, 0, result, 0, Math.Min(source.Length, SlotCount));
            }

            return result;
        }

        private static uint[] PadUints(uint[] source)
        {
            var result = new uint[SlotCount];
            if (source != null)
            {
                Array.Copy(source, 0, result, 0, Math.Min(source.Length, SlotCount));
            }

            return result;
        }

        private static string[] PadNames(string[] source)
        {
            var result = new string[SlotCount];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = string.Empty;
            }

            if (source != null)
            {
                for (int i = 0; i < Math.Min(source.Length, SlotCount); i++)
                {
                    result[i] = source[i] ?? string.Empty;
                }
            }

            return result;
        }

        public override string ToString()
        {
            return $"PacketGCLoginSuccess(Slots={SlotCount}, Handle={Handle})";
        }
    }
}
