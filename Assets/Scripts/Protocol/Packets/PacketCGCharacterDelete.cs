using Metin2.Protocol.Constants;

namespace Metin2.Protocol.Packets
{
    /// <summary>
    /// Character delete request (command_player_delete, 10 bytes).
    /// Verified both sides:
    /// Server `game/src/packet.h:536-541` (`private_code[8]`), handled
    /// `input_login.cpp:501-535` (no account / index overflow → silent;
    /// empty slot → 1-byte 11; else GD_PLAYER_DELETE to DB);
    /// Client `UserInterface/Packet.h:1169-1174`
    /// (`szPrivateCode[PRIVATE_CODE_LENGTH]`, `Packet.h:378` → 8),
    /// sent `PhaseSelect.cpp:177-192`.
    /// Wire layout (total 10 = 1+1+8):
    /// [0]     BYTE header (5 = 0x05)
    /// [1]     BYTE index  (0..3)
    /// [2..9]  char private_code[8]
    /// </summary>
    public struct PacketCGCharacterDelete : IPacket
    {
        public const byte PacketHeader = PacketHeaders.HEADER_CG_CHARACTER_DELETE;
        public const int PrivateCodeBufferLen = 8;
        public const int PacketSize = 1 + 1 + PrivateCodeBufferLen; // 10 bytes

        public byte Header => PacketHeader;
        public int Length => PacketSize;

        public byte Index { get; set; }
        public string PrivateCode { get; set; }

        public PacketCGCharacterDelete(byte index, string privateCode)
        {
            Index = index;
            PrivateCode = privateCode ?? string.Empty;
        }

        public override string ToString()
        {
            return $"PacketCGCharacterDelete(Index={Index})";
        }
    }
}
