using Metin2.Protocol.Constants;

namespace Metin2.Protocol.Packets
{
    /// <summary>
    /// Auth-server login reply (TPacketGCAuthSuccess, 6 bytes).
    /// Verified both sides:
    /// Server `game/src/packet.h:849-854`, sent `input_db.cpp:1686-1710`
    /// (`bResult` nonzero = ok with `dwLoginKey`, zero = fail with key 0);
    /// Client `UserInterface/Packet.h:2370-2375`, handled
    /// `AccountConnector.cpp:312-337` (bResult==0 → "BESAMEKEY" failure,
    /// else PanamaKey = key ^ clientKeys[4] and channel connect).
    /// Wire layout:
    /// [0]     BYTE  bHeader    (150 = 0x96)
    /// [1..4]  DWORD dwLoginKey (4 bytes LE)
    /// [5]     BYTE  bResult    (nonzero = success)
    /// </summary>
    public struct PacketGCAuthSuccess : IPacket
    {
        public const byte PacketHeader = PacketHeaders.HEADER_GC_AUTH_SUCCESS;
        public const int PacketSize = 1 + 4 + 1; // 6 bytes

        public byte Header => PacketHeader;
        public int Length => PacketSize;

        public uint LoginKey { get; set; }
        public byte Result { get; set; }

        public bool Succeeded => Result != 0;

        public PacketGCAuthSuccess(uint loginKey, byte result)
        {
            LoginKey = loginKey;
            Result = result;
        }

        public override string ToString()
        {
            return $"PacketGCAuthSuccess(LoginKey={LoginKey}, Result={Result})";
        }
    }
}
