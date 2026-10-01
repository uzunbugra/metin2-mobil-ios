using System;
using Metin2.Protocol.Buffer;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Protocol.Codecs
{
    /// <summary>
    /// Codec for TPacketGCMove (24 bytes).
    /// Server: game/src/packet.h:1288-1299, input_main.cpp:1651-1663.
    /// Client: UserInterface/Packet.h:1888-1899.
    /// </summary>
    public static class PacketGCMoveCodec
    {
        public static int Serialize(in PacketGCMove packet, Span<byte> destination)
        {
            if (destination.Length < PacketGCMove.PacketSize)
            {
                throw new ArgumentException(
                    $"Destination buffer too small: required {PacketGCMove.PacketSize} bytes, available {destination.Length}.",
                    nameof(destination));
            }

            var writer = new PacketWriter(destination);
            writer.WriteByte(packet.Header);
            writer.WriteByte(packet.Func);
            writer.WriteByte(packet.Arg);
            writer.WriteByte(packet.Rot);
            writer.WriteUInt32(packet.Vid);
            writer.WriteInt32(packet.X);
            writer.WriteInt32(packet.Y);
            writer.WriteUInt32(packet.Time);
            writer.WriteUInt32(packet.Duration);

            return writer.BytesWritten;
        }

        public static byte[] Serialize(in PacketGCMove packet)
        {
            byte[] buffer = new byte[PacketGCMove.PacketSize];
            Serialize(packet, buffer);
            return buffer;
        }

        public static bool TryDeserialize(ReadOnlySpan<byte> source, out PacketGCMove packet, out string errorMessage)
        {
            packet = default;
            errorMessage = null;

            if (source.Length < PacketGCMove.PacketSize)
            {
                errorMessage = $"Buffer truncated: expected at least {PacketGCMove.PacketSize} bytes, got {source.Length}.";
                return false;
            }

            var reader = new PacketReader(source);
            byte header = reader.ReadByte();
            if (header != PacketGCMove.PacketHeader)
            {
                errorMessage = $"Invalid header: expected 0x{PacketGCMove.PacketHeader:X2}, got 0x{header:X2}.";
                return false;
            }

            packet = new PacketGCMove
            {
                Func = reader.ReadByte(),
                Arg = reader.ReadByte(),
                Rot = reader.ReadByte(),
                Vid = reader.ReadUInt32(),
                X = reader.ReadInt32(),
                Y = reader.ReadInt32(),
                Time = reader.ReadUInt32(),
                Duration = reader.ReadUInt32()
            };
            return true;
        }

        public static PacketGCMove Deserialize(ReadOnlySpan<byte> source)
        {
            if (source.Length < PacketGCMove.PacketSize)
            {
                throw new PacketUnderflowException(PacketGCMove.PacketSize, source.Length, nameof(PacketGCMove));
            }

            byte header = source[0];
            if (header != PacketGCMove.PacketHeader)
            {
                throw new InvalidPacketHeaderException(PacketGCMove.PacketHeader, header, nameof(PacketGCMove));
            }

            if (!TryDeserialize(source, out PacketGCMove packet, out string errorMessage))
            {
                throw new PacketParseException(errorMessage, nameof(PacketGCMove));
            }

            return packet;
        }
    }
}
