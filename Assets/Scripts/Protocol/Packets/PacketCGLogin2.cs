using System;
using Metin2.Protocol.Constants;

namespace Metin2.Protocol.Packets
{
    /// <summary>
    /// Channel-core key login packet (TPacketCGLogin2, 52 bytes).
    /// Verified both sides:
    /// Server `game/src/packet.h:508-514`, handled `input_login.cpp:138-190`
    /// (`LoginByKey`: trim+lower, SHUTDOWN/FULL guards, GD_LOGIN_BY_KEY to DB);
    /// Client `UserInterface/Packet.h:503-509`, sent `PhaseLogin.cpp:254-280`
    /// (`SendLoginPacketNew`: login_key = m_dwLoginKey from auth, adwClientKey
    /// = g_adwEncryptKey[4]).
    /// Wire layout:
    /// [0]      BYTE  header          (109 = 0x6d)
    /// [1..31]  char  login[31]       (31 bytes null-terminated/padded)
    /// [32..35] DWORD dwLoginKey      (4 bytes LE, auth-issued)
    /// [36..51] DWORD adwClientKey[4] (4 x 4 = 16 bytes LE)
    /// Total = 1 + 31 + 4 + 16 = 52 bytes
    /// </summary>
    public struct PacketCGLogin2 : IPacket
    {
        public const byte PacketHeader = PacketHeaders.HEADER_CG_LOGIN2;
        public const int LoginBufferLen = 31; // LOGIN_MAX_LEN + 1 (length.h:9)
        public const int KeyCount = 4;
        public const int PacketSize = 1 + LoginBufferLen + 4 + (KeyCount * 4); // 52 bytes

        public byte Header => PacketHeader;
        public int Length => PacketSize;

        public string Login { get; set; }
        public uint LoginKey { get; set; }
        public uint[] ClientKeys { get; set; }

        public PacketCGLogin2(string login, uint loginKey, uint[] clientKeys)
        {
            Login = login ?? string.Empty;
            LoginKey = loginKey;

            if (clientKeys != null && clientKeys.Length == KeyCount)
            {
                ClientKeys = (uint[])clientKeys.Clone();
            }
            else
            {
                ClientKeys = new uint[KeyCount];
                if (clientKeys != null)
                {
                    int copyLen = Math.Min(clientKeys.Length, KeyCount);
                    Array.Copy(clientKeys, 0, ClientKeys, 0, copyLen);
                }
            }
        }

        public override string ToString()
        {
            return $"PacketCGLogin2(Login='{Login}', LoginKey={LoginKey})";
        }
    }
}
