using System;
using Metin2.Protocol.Buffer;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Protocol.Codecs
{
    /// <summary>
    /// Codec for command_move (16 bytes).
    /// Server: game/src/packet.h:586-595. Client: UserInterface/Packet.h:695-704.
    /// </summary>
    public static class PacketCGMoveCodec
    {
        public static int Serialize(in PacketCGMove packet, Span<byte> destination)
        {
            if (destination.Length < PacketCGMove.PacketSize)
            {
                throw new ArgumentException(
                    $"Destination buffer too small: required {PacketCGMove.PacketSize} bytes, available {destination.Length}.",
                    nameof(destination));
            }

            var writer = new PacketWriter(destination);
            writer.WriteByte(packet.Header);
            writer.WriteByte(packet.Func);
            writer.WriteByte(packet.Arg);
            writer.WriteByte(packet.Rot);
            writer.WriteInt32(packet.X);
            writer.WriteInt32(packet.Y);
            writer.WriteUInt32(packet.Time);

            return writer.BytesWritten;
        }

        public static byte[] Serialize(in PacketCGMove packet)
        {
            byte[] buffer = new byte[PacketCGMove.PacketSize];
            Serialize(packet, buffer);
            return buffer;
        }

        public static bool TryDeserialize(ReadOnlySpan<byte> source, out PacketCGMove packet, out string errorMessage)
        {
            packet = default;
            errorMessage = null;

            if (source.Length < PacketCGMove.PacketSize)
            {
                errorMessage = $"Buffer truncated: expected at least {PacketCGMove.PacketSize} bytes, got {source.Length}.";
                return false;
            }

            var reader = new PacketReader(source);
            byte header = reader.ReadByte();
            if (header != PacketCGMove.PacketHeader)
            {
                errorMessage = $"Invalid header: expected 0x{PacketCGMove.PacketHeader:X2}, got 0x{header:X2}.";
                return false;
            }

            packet = new PacketCGMove(
                reader.ReadByte(), reader.ReadByte(), reader.ReadByte(),
                reader.ReadInt32(), reader.ReadInt32(), reader.ReadUInt32());
            return true;
        }

        public static PacketCGMove Deserialize(ReadOnlySpan<byte> source)
        {
            if (source.Length < PacketCGMove.PacketSize)
            {
                throw new PacketUnderflowException(PacketCGMove.PacketSize, source.Length, nameof(PacketCGMove));
            }

            byte header = source[0];
            if (header != PacketCGMove.PacketHeader)
            {
                throw new InvalidPacketHeaderException(PacketCGMove.PacketHeader, header, nameof(PacketCGMove));
            }

            if (!TryDeserialize(source, out PacketCGMove packet, out string errorMessage))
            {
                throw new PacketParseException(errorMessage, nameof(PacketCGMove));
            }

            return packet;
        }
    }
}
