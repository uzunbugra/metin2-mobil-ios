using System;
using Metin2.Protocol.Constants;

namespace Metin2.Protocol.Packets
{
    /// <summary>
    /// Client authentication command packet (TPacketCGLogin3, 65 bytes).
    /// Verified from Server packet.h:516-522, common/length.h:9-10, input_auth.cpp:102-187.
    /// Wire layout:
    /// [0]      BYTE  header          (111 = 0x6f)
    /// [1..31]  char  login[31]       (31 bytes null-terminated/padded)
    /// [32..48] char  passwd[17]      (17 bytes null-terminated/padded)
    /// [49..64] DWORD adwClientKey[4] (4 x 4 = 16 bytes LE)
    /// Total = 1 + 31 + 17 + 16 = 65 bytes
    /// </summary>
    public struct PacketCGLogin3 : IPacket
    {
        public const byte PacketHeader = PacketHeaders.HEADER_CG_LOGIN3;
        public const int LoginBufferLen = 31;   // LOGIN_MAX_LEN + 1 (length.h:9)
        public const int LoginMaxChars = 30;
        public const int PasswdBufferLen = 17;  // PASSWD_MAX_LEN + 1 (length.h:10)
        public const int PasswdMaxChars = 16;
        public const int KeyCount = 4;
        public const int PacketSize = 1 + LoginBufferLen + PasswdBufferLen + (KeyCount * 4); // 65 bytes

        public byte Header => PacketHeader;
        public int Length => PacketSize;

        public string Login { get; set; }
        public string Password { get; set; }
        public uint[] ClientKeys { get; set; }

        public PacketCGLogin3(string login, string password, uint[] clientKeys)
        {
            Login = login ?? string.Empty;
            Password = password ?? string.Empty;

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
            // Do NOT print actual password per AGENT_DEVELOPMENT_GUIDE § 0.1 rule 6
            return $"PacketCGLogin3(Login='{Login}', PwdLen={Password?.Length ?? 0}, Keys=[{ClientKeys[0]:X8},{ClientKeys[1]:X8},{ClientKeys[2]:X8},{ClientKeys[3]:X8}])";
        }
    }
}
