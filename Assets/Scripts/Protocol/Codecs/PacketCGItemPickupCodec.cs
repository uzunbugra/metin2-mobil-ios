using System;
using Metin2.Protocol.Buffer;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Protocol.Codecs
{
    /// <summary>
    /// Codec for serializing and deserializing TPacketCGItemPickup (5 bytes).
    /// Verified from Server packet.h:668-672, Client Packet.h (same struct).
    /// </summary>
    public static class PacketCGItemPickupCodec
    {
        public static int Serialize(in PacketCGItemPickup packet, Span<byte> destination)
        {
            if (destination.Length < PacketCGItemPickup.PacketSize)
            {
                throw new ArgumentException(
                    $"Destination buffer too small: required {PacketCGItemPickup.PacketSize} bytes, available {destination.Length}.",
                    nameof(destination));
            }

            var writer = new PacketWriter(destination);
            writer.WriteByte(packet.Header);
            writer.WriteUInt32(packet.Vid);

            return writer.BytesWritten;
        }

        public static byte[] Serialize(in PacketCGItemPickup packet)
        {
            byte[] buffer = new byte[PacketCGItemPickup.PacketSize];
            Serialize(packet, buffer);
            return buffer;
        }

        public static bool TryDeserialize(ReadOnlySpan<byte> source, out PacketCGItemPickup packet, out string errorMessage)
        {
            packet = default;
            errorMessage = null;

            if (source.Length < PacketCGItemPickup.PacketSize)
            {
                errorMessage = $"Buffer truncated: expected at least {PacketCGItemPickup.PacketSize} bytes for TPacketCGItemPickup, got {source.Length}.";
                return false;
            }

            var reader = new PacketReader(source);
            byte header = reader.ReadByte();
            if (header != PacketCGItemPickup.PacketHeader)
            {
                errorMessage = $"Invalid header: expected 0x{PacketCGItemPickup.PacketHeader:X2} (HEADER_CG_ITEM_PICKUP), got 0x{header:X2}.";
                return false;
            }

            uint vid = reader.ReadUInt32();
            packet = new PacketCGItemPickup(vid);
            return true;
        }

        public static PacketCGItemPickup Deserialize(ReadOnlySpan<byte> source)
        {
            if (source.Length < PacketCGItemPickup.PacketSize)
            {
                throw new PacketUnderflowException(PacketCGItemPickup.PacketSize, source.Length, nameof(PacketCGItemPickup));
            }

            if (source[0] != PacketCGItemPickup.PacketHeader)
            {
                throw new InvalidPacketHeaderException(PacketCGItemPickup.PacketHeader, source[0], nameof(PacketCGItemPickup));
            }

            if (!TryDeserialize(source, out PacketCGItemPickup packet, out string errorMessage))
            {
                throw new PacketParseException(errorMessage, nameof(PacketCGItemPickup));
            }

            return packet;
        }
    }
}
