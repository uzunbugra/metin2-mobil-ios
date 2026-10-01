using System;
using System.Text;
using Metin2.Protocol.Buffer;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Protocol.Codecs
{
    /// <summary>
    /// Codec for serializing and deserializing TPacketGCLoginSuccess (329 bytes,
    /// NEWSLOT header 32).
    /// References:
    /// Server: game/src/packet.h:838-847, desc.cpp:892-919.
    /// Client: UserInterface/Packet.h:1125-1133, PhaseLogin.cpp:162-188.
    /// </summary>
    public static class PacketGCLoginSuccessCodec
    {
        public static int Serialize(in PacketGCLoginSuccess packet, Span<byte> destination)
        {
            if (destination.Length < PacketGCLoginSuccess.PacketSize)
            {
                throw new ArgumentException(
                    $"Destination buffer too small: required {PacketGCLoginSuccess.PacketSize} bytes, available {destination.Length}.",
                    nameof(destination));
            }

            var writer = new PacketWriter(destination);
            writer.WriteByte(packet.Header);

            SimplePlayer[] players = packet.Players ?? new SimplePlayer[PacketGCLoginSuccess.SlotCount];
            byte[] slotBuffer = new byte[SimplePlayer.FieldSize];
            for (int i = 0; i < PacketGCLoginSuccess.SlotCount; i++)
            {
                SimplePlayer slot = i < players.Length ? players[i] : default;
                SimplePlayerCodec.Serialize(slot, slotBuffer);
                writer.WriteBytes(slotBuffer);
            }

            uint[] guildIds = packet.GuildIds ?? new uint[PacketGCLoginSuccess.SlotCount];
            for (int i = 0; i < PacketGCLoginSuccess.SlotCount; i++)
            {
                writer.WriteUInt32(i < guildIds.Length ? guildIds[i] : 0);
            }

            string[] guildNames = packet.GuildNames ?? new string[PacketGCLoginSuccess.SlotCount];
            for (int i = 0; i < PacketGCLoginSuccess.SlotCount; i++)
            {
                writer.WriteFixedString(
                    i < guildNames.Length ? guildNames[i] : string.Empty,
                    PacketGCLoginSuccess.GuildNameBufferLen,
                    Encoding.ASCII);
            }

            writer.WriteUInt32(packet.Handle);
            writer.WriteUInt32(packet.RandomKey);

            return writer.BytesWritten;
        }

        public static byte[] Serialize(in PacketGCLoginSuccess packet)
        {
            byte[] buffer = new byte[PacketGCLoginSuccess.PacketSize];
            Serialize(packet, buffer);
            return buffer;
        }

        public static bool TryDeserialize(ReadOnlySpan<byte> source, out PacketGCLoginSuccess packet, out string errorMessage)
        {
            packet = default;
            errorMessage = null;

            if (source.Length < PacketGCLoginSuccess.PacketSize)
            {
                errorMessage = $"Buffer truncated: expected at least {PacketGCLoginSuccess.PacketSize} bytes for TPacketGCLoginSuccess, got {source.Length}.";
                return false;
            }

            var reader = new PacketReader(source);
            byte header = reader.ReadByte();
            if (header != PacketGCLoginSuccess.PacketHeader)
            {
                errorMessage = $"Invalid header: expected 0x{PacketGCLoginSuccess.PacketHeader:X2} (HEADER_GC_LOGIN_SUCCESS_NEWSLOT), got 0x{header:X2}.";
                return false;
            }

            var players = new SimplePlayer[PacketGCLoginSuccess.SlotCount];
            for (int i = 0; i < players.Length; i++)
            {
                if (!SimplePlayerCodec.TryDeserialize(reader.ReadSpan(SimplePlayer.FieldSize), out SimplePlayer slot, out string slotError))
                {
                    errorMessage = $"Slot {i}: {slotError}";
                    return false;
                }

                players[i] = slot;
            }

            var guildIds = new uint[PacketGCLoginSuccess.SlotCount];
            for (int i = 0; i < guildIds.Length; i++)
            {
                guildIds[i] = reader.ReadUInt32();
            }

            var guildNames = new string[PacketGCLoginSuccess.SlotCount];
            for (int i = 0; i < guildNames.Length; i++)
            {
                guildNames[i] = reader.ReadFixedString(PacketGCLoginSuccess.GuildNameBufferLen, Encoding.ASCII);
            }

            packet = new PacketGCLoginSuccess(players, guildIds, guildNames, reader.ReadUInt32(), reader.ReadUInt32());
            return true;
        }

        public static PacketGCLoginSuccess Deserialize(ReadOnlySpan<byte> source)
        {
            if (source.Length < PacketGCLoginSuccess.PacketSize)
            {
                throw new PacketUnderflowException(PacketGCLoginSuccess.PacketSize, source.Length, nameof(PacketGCLoginSuccess));
            }

            byte header = source[0];
            if (header != PacketGCLoginSuccess.PacketHeader)
            {
                throw new InvalidPacketHeaderException(PacketGCLoginSuccess.PacketHeader, header, nameof(PacketGCLoginSuccess));
            }

            if (!TryDeserialize(source, out PacketGCLoginSuccess packet, out string errorMessage))
            {
                throw new PacketParseException(errorMessage, nameof(PacketGCLoginSuccess));
            }

            return packet;
        }
    }
}
