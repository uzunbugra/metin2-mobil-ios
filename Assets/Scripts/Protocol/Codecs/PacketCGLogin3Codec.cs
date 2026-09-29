using System;
using System.Text;
using Metin2.Protocol.Buffer;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Protocol.Codecs
{
    /// <summary>
    /// Codec for serializing and deserializing TPacketCGLogin3 (65 bytes).
    /// References:
    /// Server: packet.h:516-522, common/length.h:9-10, input_auth.cpp:102-187
    /// Registration: packet_info.cpp:118 (sizeof=65)
    /// </summary>
    public static class PacketCGLogin3Codec
    {
        public static int Serialize(in PacketCGLogin3 packet, Span<byte> destination)
        {
            if (destination.Length < PacketCGLogin3.PacketSize)
            {
                throw new ArgumentException(
                    $"Destination buffer too small: required {PacketCGLogin3.PacketSize} bytes, available {destination.Length}.",
                    nameof(destination));
            }

            var writer = new PacketWriter(destination);
            writer.WriteByte(packet.Header);
            writer.WriteFixedString(packet.Login, PacketCGLogin3.LoginBufferLen, Encoding.ASCII);
            writer.WriteFixedString(packet.Password, PacketCGLogin3.PasswdBufferLen, Encoding.ASCII);

            for (int i = 0; i < PacketCGLogin3.KeyCount; i++)
            {
                uint key = (packet.ClientKeys != null && i < packet.ClientKeys.Length) ? packet.ClientKeys[i] : 0;
                writer.WriteUInt32(key);
            }

            return writer.BytesWritten;
        }

        public static byte[] Serialize(in PacketCGLogin3 packet)
        {
            byte[] buffer = new byte[PacketCGLogin3.PacketSize];
            Serialize(packet, buffer);
            return buffer;
        }

        public static bool TryDeserialize(ReadOnlySpan<byte> source, out PacketCGLogin3 packet, out string errorMessage)
        {
            packet = default;
            errorMessage = null;

            if (source.Length < PacketCGLogin3.PacketSize)
            {
                errorMessage = $"Buffer truncated: expected at least {PacketCGLogin3.PacketSize} bytes for TPacketCGLogin3, got {source.Length}.";
                return false;
            }

            var reader = new PacketReader(source);
            byte header = reader.ReadByte();
            if (header != PacketCGLogin3.PacketHeader)
            {
                errorMessage = $"Invalid header: expected {PacketCGLogin3.PacketHeader} (HEADER_CG_LOGIN3), got {header}.";
                return false;
            }

            string login = reader.ReadFixedString(PacketCGLogin3.LoginBufferLen, Encoding.ASCII);
            string password = reader.ReadFixedString(PacketCGLogin3.PasswdBufferLen, Encoding.ASCII);

            uint[] clientKeys = new uint[PacketCGLogin3.KeyCount];
            for (int i = 0; i < PacketCGLogin3.KeyCount; i++)
            {
                clientKeys[i] = reader.ReadUInt32();
            }

            packet = new PacketCGLogin3(login, password, clientKeys);
            return true;
        }

        public static PacketCGLogin3 Deserialize(ReadOnlySpan<byte> source)
        {
            if (source.Length < PacketCGLogin3.PacketSize)
            {
                throw new PacketUnderflowException(PacketCGLogin3.PacketSize, source.Length, nameof(PacketCGLogin3));
            }

            byte header = source[0];
            if (header != PacketCGLogin3.PacketHeader)
            {
                throw new InvalidPacketHeaderException(PacketCGLogin3.PacketHeader, header, nameof(PacketCGLogin3));
            }

            if (!TryDeserialize(source, out PacketCGLogin3 packet, out string errorMessage))
            {
                throw new PacketParseException(errorMessage, nameof(PacketCGLogin3));
            }

            return packet;
        }
    }
}
