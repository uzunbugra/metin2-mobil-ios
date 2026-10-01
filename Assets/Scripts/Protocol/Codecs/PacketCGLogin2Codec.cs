using System;
using System.Text;
using Metin2.Protocol.Buffer;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Protocol.Codecs
{
    /// <summary>
    /// Codec for serializing and deserializing TPacketCGLogin2 (52 bytes).
    /// References:
    /// Server: game/src/packet.h:508-514, input_login.cpp:138-190.
    /// Client: UserInterface/Packet.h:503-509, PhaseLogin.cpp:254-280.
    /// </summary>
    public static class PacketCGLogin2Codec
    {
        public static int Serialize(in PacketCGLogin2 packet, Span<byte> destination)
        {
            if (destination.Length < PacketCGLogin2.PacketSize)
            {
                throw new ArgumentException(
                    $"Destination buffer too small: required {PacketCGLogin2.PacketSize} bytes, available {destination.Length}.",
                    nameof(destination));
            }

            var writer = new PacketWriter(destination);
            writer.WriteByte(packet.Header);
            writer.WriteFixedString(packet.Login, PacketCGLogin2.LoginBufferLen, Encoding.ASCII);
            writer.WriteUInt32(packet.LoginKey);

            for (int i = 0; i < PacketCGLogin2.KeyCount; i++)
            {
                uint key = (packet.ClientKeys != null && i < packet.ClientKeys.Length) ? packet.ClientKeys[i] : 0;
                writer.WriteUInt32(key);
            }

            return writer.BytesWritten;
        }

        public static byte[] Serialize(in PacketCGLogin2 packet)
        {
            byte[] buffer = new byte[PacketCGLogin2.PacketSize];
            Serialize(packet, buffer);
            return buffer;
        }

        public static bool TryDeserialize(ReadOnlySpan<byte> source, out PacketCGLogin2 packet, out string errorMessage)
        {
            packet = default;
            errorMessage = null;

            if (source.Length < PacketCGLogin2.PacketSize)
            {
                errorMessage = $"Buffer truncated: expected at least {PacketCGLogin2.PacketSize} bytes for TPacketCGLogin2, got {source.Length}.";
                return false;
            }

            var reader = new PacketReader(source);
            byte header = reader.ReadByte();
            if (header != PacketCGLogin2.PacketHeader)
            {
                errorMessage = $"Invalid header: expected {PacketCGLogin2.PacketHeader} (HEADER_CG_LOGIN2), got {header}.";
                return false;
            }

            string login = reader.ReadFixedString(PacketCGLogin2.LoginBufferLen, Encoding.ASCII);
            uint loginKey = reader.ReadUInt32();

            uint[] clientKeys = new uint[PacketCGLogin2.KeyCount];
            for (int i = 0; i < PacketCGLogin2.KeyCount; i++)
            {
                clientKeys[i] = reader.ReadUInt32();
            }

            packet = new PacketCGLogin2(login, loginKey, clientKeys);
            return true;
        }

        public static PacketCGLogin2 Deserialize(ReadOnlySpan<byte> source)
        {
            if (source.Length < PacketCGLogin2.PacketSize)
            {
                throw new PacketUnderflowException(PacketCGLogin2.PacketSize, source.Length, nameof(PacketCGLogin2));
            }

            byte header = source[0];
            if (header != PacketCGLogin2.PacketHeader)
            {
                throw new InvalidPacketHeaderException(PacketCGLogin2.PacketHeader, header, nameof(PacketCGLogin2));
            }

            if (!TryDeserialize(source, out PacketCGLogin2 packet, out string errorMessage))
            {
                throw new PacketParseException(errorMessage, nameof(PacketCGLogin2));
            }

            return packet;
        }
    }
}
