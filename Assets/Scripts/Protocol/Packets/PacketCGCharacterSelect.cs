using Metin2.Protocol.Constants;

namespace Metin2.Protocol.Packets
{
    /// <summary>
    /// Character select request (command_player_select, 2 bytes).
    /// Verified both sides:
    /// Server `game/src/packet.h:530-534`, handled `input_login.cpp:222-255`
    /// (account/index guards, then GD_PLAYER_LOAD to DB — NO direct reply,
    /// the world entry continues via ENTERGAME);
    /// Client `UserInterface/Packet.h:529-533`, sent
    /// `PhaseSelect.cpp:161-175` (`SendSelectCharacterPacket`).
    /// Wire layout:
    /// [0] BYTE header (6 = 0x06)
    /// [1] BYTE index  (0..3)
    /// </summary>
    public struct PacketCGCharacterSelect : IPacket
    {
        public const byte PacketHeader = PacketHeaders.HEADER_CG_CHARACTER_SELECT;
        public const int PacketSize = 2;

        public byte Header => PacketHeader;
        public int Length => PacketSize;

        public byte Index { get; set; }

        public PacketCGCharacterSelect(byte index)
        {
            Index = index;
        }

        public override string ToString()
        {
            return $"PacketCGCharacterSelect(Index={Index})";
        }
    }
}
