using System;
using Metin2.Protocol.Buffer;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Protocol.Codecs
{
    /// <summary>
    /// Codec for serializing and deserializing TPacketKeyAgreement (261 bytes).
    /// </summary>
    public static class PacketKeyAgreementCodec
    {
        public static int Serialize(in PacketKeyAgreement packet, Span<byte> destination)
        {
            if (destination.Length < PacketKeyAgreement.PacketSize)
            {
                throw new ArgumentException(
                    $"Destination buffer too small: required {PacketKeyAgreement.PacketSize} bytes, available {destination.Length}.",
                    nameof(destination));
            }

            var writer = new PacketWriter(destination);
            writer.WriteByte(packet.Header);
            writer.WriteUInt16(packet.AgreedLength);
            writer.WriteUInt16(packet.DataLength);

            int dataLen = packet.Data != null ? Math.Min(packet.Data.Length, PacketKeyAgreement.MaxDataLen) : 0;
            if (dataLen > 0)
            {
                writer.WriteBytes(packet.Data.AsSpan(0, dataLen));
            }

            int remainingZeros = PacketKeyAgreement.MaxDataLen - dataLen;
            if (remainingZeros > 0)
            {
                writer.WriteZeros(remainingZeros);
            }

            return writer.BytesWritten;
        }

        public static byte[] Serialize(in PacketKeyAgreement packet)
        {
            byte[] buffer = new byte[PacketKeyAgreement.PacketSize];
            Serialize(packet, buffer);
            return buffer;
        }

        public static bool TryDeserialize(ReadOnlySpan<byte> source, out PacketKeyAgreement packet, out string errorMessage)
        {
            packet = default;
            errorMessage = null;

            if (source.Length < PacketKeyAgreement.PacketSize)
            {
                errorMessage = $"Buffer truncated: expected at least {PacketKeyAgreement.PacketSize} bytes for TPacketKeyAgreement, got {source.Length}.";
                return false;
            }

            var reader = new PacketReader(source);
            byte header = reader.ReadByte();
            if (header != PacketKeyAgreement.PacketHeader)
            {
                errorMessage = $"Invalid header: expected 0x{PacketKeyAgreement.PacketHeader:X2} (HEADER_GC_KEY_AGREEMENT / HEADER_CG_KEY_AGREEMENT), got 0x{header:X2}.";
                return false;
            }

            ushort agreedLength = reader.ReadUInt16();
            ushort dataLength = reader.ReadUInt16();
            byte[] data = new byte[PacketKeyAgreement.MaxDataLen];
            reader.ReadBytes(data);

            packet = new PacketKeyAgreement(agreedLength, dataLength, data);
            return true;
        }

        public static PacketKeyAgreement Deserialize(ReadOnlySpan<byte> source)
        {
            if (source.Length < PacketKeyAgreement.PacketSize)
            {
                throw new PacketUnderflowException(PacketKeyAgreement.PacketSize, source.Length, nameof(PacketKeyAgreement));
            }

            byte header = source[0];
            if (header != PacketKeyAgreement.PacketHeader)
            {
                throw new InvalidPacketHeaderException(PacketKeyAgreement.PacketHeader, header, nameof(PacketKeyAgreement));
            }

            if (!TryDeserialize(source, out PacketKeyAgreement packet, out string errorMessage))
            {
                throw new PacketParseException(errorMessage, nameof(PacketKeyAgreement));
            }

            return packet;
        }
    }
}
