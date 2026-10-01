using System;
using Metin2.Protocol.Buffer;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Protocol.Codecs
{
    /// <summary>
    /// Codec for command_player_create_success (65 bytes).
    /// Server: game/src/packet.h:556-561, input_db.cpp:221-227.
    /// Client: UserInterface/Packet.h:1156-1161, PhaseSelect.cpp:233-249.
    /// </summary>
    public static class PacketGCCreateSuccessCodec
    {
        public static int Serialize(in PacketGCCreateSuccess packet, Span<byte> destination)
        {
            if (destination.Length < PacketGCCreateSuccess.PacketSize)
            {
                throw new ArgumentException(
                    $"Destination buffer too small: required {PacketGCCreateSuccess.PacketSize} bytes, available {destination.Length}.",
                    nameof(destination));
            }

            var writer = new PacketWriter(destination);
            writer.WriteByte(packet.Header);
            writer.WriteByte(packet.Slot);

            byte[] slotBuffer = new byte[SimplePlayer.FieldSize];
            SimplePlayerCodec.Serialize(packet.Player, slotBuffer);
            writer.WriteBytes(slotBuffer);

            return writer.BytesWritten;
        }

        public static byte[] Serialize(in PacketGCCreateSuccess packet)
        {
            byte[] buffer = new byte[PacketGCCreateSuccess.PacketSize];
            Serialize(packet, buffer);
            return buffer;
        }

        public static bool TryDeserialize(ReadOnlySpan<byte> source, out PacketGCCreateSuccess packet, out string errorMessage)
        {
            packet = default;
            errorMessage = null;

            if (source.Length < PacketGCCreateSuccess.PacketSize)
            {
                errorMessage = $"Buffer truncated: expected at least {PacketGCCreateSuccess.PacketSize} bytes, got {source.Length}.";
                return false;
            }

            var reader = new PacketReader(source);
            byte header = reader.ReadByte();
            if (header != PacketGCCreateSuccess.PacketHeader)
            {
                errorMessage = $"Invalid header: expected 0x{PacketGCCreateSuccess.PacketHeader:X2}, got 0x{header:X2}.";
                return false;
            }

            byte slot = reader.ReadByte();
            if (!SimplePlayerCodec.TryDeserialize(reader.ReadSpan(SimplePlayer.FieldSize), out SimplePlayer player, out string slotError))
            {
                errorMessage = $"Player: {slotError}";
                return false;
            }

            packet = new PacketGCCreateSuccess(slot, player);
            return true;
        }

        public static PacketGCCreateSuccess Deserialize(ReadOnlySpan<byte> source)
        {
            if (source.Length < PacketGCCreateSuccess.PacketSize)
            {
                throw new PacketUnderflowException(PacketGCCreateSuccess.PacketSize, source.Length, nameof(PacketGCCreateSuccess));
            }

            byte header = source[0];
            if (header != PacketGCCreateSuccess.PacketHeader)
            {
                throw new InvalidPacketHeaderException(PacketGCCreateSuccess.PacketHeader, header, nameof(PacketGCCreateSuccess));
            }

            if (!TryDeserialize(source, out PacketGCCreateSuccess packet, out string errorMessage))
            {
                throw new PacketParseException(errorMessage, nameof(PacketGCCreateSuccess));
            }

            return packet;
        }
    }
}
