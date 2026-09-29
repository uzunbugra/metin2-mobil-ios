using System;
using Metin2.Protocol.Constants;

namespace Metin2.Protocol.Packets
{
    /// <summary>
    /// Key agreement packet (TPacketKeyAgreement, 261 bytes).
    /// Verified from Server packet.h:2226-2233 and desc.cpp:704-722.
    /// Wire layout:
    /// [0]      BYTE bHeader       (0xfb = 251)
    /// [1..2]   WORD wAgreedLength (2 bytes LE)
    /// [3..4]   WORD wDataLength   (2 bytes LE)
    /// [5..260] BYTE data[256]     (256 bytes raw payload)
    /// </summary>
    public struct PacketKeyAgreement : IPacket
    {
        public const byte PacketHeader = PacketHeaders.HEADER_GC_KEY_AGREEMENT;
        public const int MaxDataLen = 256;
        public const int PacketSize = 1 + 2 + 2 + MaxDataLen; // 261 bytes

        public byte Header => PacketHeader;
        public int Length => PacketSize;

        public ushort AgreedLength { get; set; }
        public ushort DataLength { get; set; }
        public byte[] Data { get; set; }

        public PacketKeyAgreement(ushort agreedLength, ushort dataLength, byte[] data)
        {
            AgreedLength = agreedLength;
            DataLength = dataLength;

            if (data == null)
            {
                Data = new byte[MaxDataLen];
            }
            else if (data.Length == MaxDataLen)
            {
                Data = data;
            }
            else
            {
                Data = new byte[MaxDataLen];
                int copyLen = Math.Min(data.Length, MaxDataLen);
                Array.Copy(data, 0, Data, 0, copyLen);
            }
        }

        public override string ToString()
        {
            return $"PacketKeyAgreement(AgreedLen={AgreedLength}, DataLen={DataLength}, Total={PacketSize}B)";
        }
    }
}
