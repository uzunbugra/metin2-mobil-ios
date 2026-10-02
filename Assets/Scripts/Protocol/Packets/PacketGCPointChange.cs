using Metin2.Protocol.Constants;

namespace Metin2.Protocol.Packets
{
    /// <summary>
    /// Point delta update (TPacketGCPointChange, 17 bytes).
    /// Verified from Server packet.h:1042-1049 (sent by
    /// CHARACTER::PointChange, char.cpp:3595-3613) and Client Packet.h:1606-1615
    /// — the two structs are field-for-field identical under #pragma pack(1).
    ///
    /// WIRE QUIRK: the first field is a 4-byte int header, not a BYTE like every
    /// other packet. On the wire (LE) the frame therefore starts with the header
    /// value followed by three 0x00 bytes; 1-byte header framing still works
    /// because the low byte is looked up first and the full 17 bytes are
    /// consumed atomically (client mirror: PythonNetworkStream.cpp:70 registers
    /// it as STATIC_SIZE_PACKET).
    ///
    /// Name mismatch across sides (same value 17): server
    /// HEADER_GC_CHARACTER_POINT_CHANGE vs client HEADER_GC_PLAYER_POINT_CHANGE.
    ///
    /// Delivery: own descriptor only by default, PacketAround when the caller
    /// sets bBroadcast (char.cpp:3609-3612). type selects the point
    /// (EPointTypes, char.h:97-135: HP=5, SP=7, LEVEL=1, EXP=3, GOLD=11 ...);
    /// value is the new absolute value, amount the applied delta (sent only
    /// when bAmount is set, else 0).
    /// Client dispatch: PhaseSelect.cpp:129, PhaseLoading.cpp:129,
    /// PhaseGame.cpp:311 — valid in Select, Loading AND Game phases.
    /// Wire layout (LE, pack(1)):
    /// [0..3]   int32 header (17)
    /// [4..7]   DWORD dwVID
    /// [8]      BYTE type (EPointTypes)
    /// [9..12]  int32 amount (delta; 0 when not sent)
    /// [13..16] int32 value (new absolute value)
    /// </summary>
    public struct PacketGCPointChange : IPacket
    {
        public const byte PacketHeader = PacketHeaders.HEADER_GC_POINT_CHANGE;
        public const int PacketSize = 17;

        public byte Header => PacketHeader;
        public int Length => PacketSize;

        public uint Vid { get; set; }
        public byte Type { get; set; }
        public int Amount { get; set; }
        public int Value { get; set; }

        public PacketGCPointChange(uint vid, byte type, int amount, int value)
        {
            Vid = vid;
            Type = type;
            Amount = amount;
            Value = value;
        }

        public override string ToString()
        {
            return $"PacketGCPointChange(Vid={Vid}, Type={Type}, Amount={Amount}, Value={Value})";
        }
    }
}
