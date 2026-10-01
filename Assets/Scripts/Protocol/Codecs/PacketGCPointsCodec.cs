using System;
using Metin2.Protocol.Buffer;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Protocol.Codecs
{
    /// <summary>
    /// Codec for TPacketGCPoints (1021 bytes).
    /// Server: game/src/packet.h:1030-1034, char.cpp:1557-1581.
    /// Client: UserInterface/Packet.h:1600-1604.
    /// </summary>
    public static class PacketGCPointsCodec
    {
        public static int Serialize(in PacketGCPoints packet, Span<byte> destination)
        {
            if (destination.Length < PacketGCPoints.PacketSize)
            {
                throw new ArgumentException(
                    $"Destination buffer too small: required {PacketGCPoints.PacketSize} bytes, available {destination.Length}.",
                    nameof(destination));
            }

            var writer = new PacketWriter(destination);
            writer.WriteByte(packet.Header);

            int[] points = packet.Points ?? new int[PacketGCPoints.PointCount];
            for (int i = 0; i < PacketGCPoints.PointCount; i++)
            {
                writer.WriteInt32(i < points.Length ? points[i] : 0);
            }

            return writer.BytesWritten;
        }

        public static byte[] Serialize(in PacketGCPoints packet)
        {
            byte[] buffer = new byte[PacketGCPoints.PacketSize];
            Serialize(packet, buffer);
            return buffer;
        }

        public static bool TryDeserialize(ReadOnlySpan<byte> source, out PacketGCPoints packet, out string errorMessage)
        {
            packet = default;
            errorMessage = null;

            if (source.Length < PacketGCPoints.PacketSize)
            {
                errorMessage = $"Buffer truncated: expected at least {PacketGCPoints.PacketSize} bytes for TPacketGCPoints, got {source.Length}.";
                return false;
            }

            var reader = new PacketReader(source);
            byte header = reader.ReadByte();
            if (header != PacketGCPoints.PacketHeader)
            {
                errorMessage = $"Invalid header: expected 0x{PacketGCPoints.PacketHeader:X2}, got 0x{header:X2}.";
                return false;
            }

            var points = new int[PacketGCPoints.PointCount];
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = reader.ReadInt32();
            }

            packet = new PacketGCPoints(points);
            return true;
        }

        public static PacketGCPoints Deserialize(ReadOnlySpan<byte> source)
        {
            if (source.Length < PacketGCPoints.PacketSize)
            {
                throw new PacketUnderflowException(PacketGCPoints.PacketSize, source.Length, nameof(PacketGCPoints));
            }

            byte header = source[0];
            if (header != PacketGCPoints.PacketHeader)
            {
                throw new InvalidPacketHeaderException(PacketGCPoints.PacketHeader, header, nameof(PacketGCPoints));
            }

            if (!TryDeserialize(source, out PacketGCPoints packet, out string errorMessage))
            {
                throw new PacketParseException(errorMessage, nameof(PacketGCPoints));
            }

            return packet;
        }
    }
}
