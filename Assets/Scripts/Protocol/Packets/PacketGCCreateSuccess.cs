using Metin2.Protocol.Constants;

namespace Metin2.Protocol.Packets
{
    /// <summary>
    /// Character create success (command_player_create_success, 65 bytes).
    /// Verified both sides:
    /// Server `game/src/packet.h:556-561`, sent `input_db.cpp:221-227`
    /// (`PlayerCreateSuccess`: header 8 + slot + fresh TSimplePlayer);
    /// Client `UserInterface/Packet.h:1156-1161`, handled
    /// `PhaseSelect.cpp:233-249` (slot bounds-checked against
    /// PLAYER_PER_ACCOUNT4, stored into select-screen data).
    /// Wire layout (total 65 = 1+1+63):
    /// [0]     BYTE header (8 = 0x08)
    /// [1]     BYTE slot   (0..3)
    /// [2..64] TSimplePlayer player (63 bytes)
    /// </summary>
    public struct PacketGCCreateSuccess : IPacket
    {
        public const byte PacketHeader = PacketHeaders.HEADER_GC_CHARACTER_CREATE_SUCCESS;
        public const int PacketSize = 1 + 1 + SimplePlayer.FieldSize; // 65 bytes

        public byte Header => PacketHeader;
        public int Length => PacketSize;

        public byte Slot { get; set; }
        public SimplePlayer Player { get; set; }

        public PacketGCCreateSuccess(byte slot, SimplePlayer player)
        {
            Slot = slot;
            Player = player;
        }

        public override string ToString()
        {
            return $"PacketGCCreateSuccess(Slot={Slot}, Player={Player})";
        }
    }
}
