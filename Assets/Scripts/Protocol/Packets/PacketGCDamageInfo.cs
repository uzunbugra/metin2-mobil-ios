using Metin2.Protocol.Constants;

namespace Metin2.Protocol.Packets
{
    /// <summary>
    /// Damage number for combat feedback (TPacketGCDamageInfo, 10 bytes).
    /// Verified from Server packet.h:2093-2099 (sent by
    /// CHARACTER::SendDamagePacket, char_battle.cpp:1584-1605) and Client
    /// Packet.h (same struct; client registration PythonNetworkStream.cpp:172,
    /// dispatch PhaseGame.cpp:396).
    /// Delivery: to the VICTIM's descriptor and the ATTACKER's descriptor only
    /// (never broadcast); HP itself travels via GC_POINT_CHANGE. flag selects
    /// the damage rendering variant (normal/crit/poison etc. — client-side
    /// cosmetic, values not validated here).
    /// Wire layout (LE, pack(1)):
    /// [0]      BYTE header (135)
    /// [1..4]   DWORD dwVID (the damaged character)
    /// [5]      BYTE flag
    /// [6..9]   int32 damage
    /// </summary>
    public struct PacketGCDamageInfo : IPacket
    {
        public const byte PacketHeader = PacketHeaders.HEADER_GC_DAMAGE_INFO;
        public const int PacketSize = 10;

        public byte Header => PacketHeader;
        public int Length => PacketSize;

        public uint Vid { get; set; }
        public byte Flag { get; set; }
        public int Damage { get; set; }

        public PacketGCDamageInfo(uint vid, byte flag, int damage)
        {
            Vid = vid;
            Flag = flag;
            Damage = damage;
        }

        public override string ToString()
        {
            return $"PacketGCDamageInfo(Vid={Vid}, Flag={Flag}, Damage={Damage})";
        }
    }
}
