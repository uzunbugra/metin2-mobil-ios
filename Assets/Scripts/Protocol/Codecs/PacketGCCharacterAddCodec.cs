using System;
using Metin2.Protocol.Buffer;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Protocol.Codecs
{
    /// <summary>
    /// Codec for TPacketGCCharacterAdd (35 bytes).
    /// Server: game/src/packet.h:886-903. Client: UserInterface/Packet.h:1226-1252.
    /// </summary>
    public static class PacketGCCharacterAddCodec
    {
        public static int Serialize(in PacketGCCharacterAdd packet, Span<byte> destination)
        {
            if (destination.Length < PacketGCCharacterAdd.PacketSize)
            {
                throw new ArgumentException(
                    $"Destination buffer too small: required {PacketGCCharacterAdd.PacketSize} bytes, available {destination.Length}.",
                    nameof(destination));
            }

            var writer = new PacketWriter(destination);
            writer.WriteByte(packet.Header);
            writer.WriteUInt32(packet.Vid);
            writer.WriteFloat(packet.Angle);
            writer.WriteInt32(packet.X);
            writer.WriteInt32(packet.Y);
            writer.WriteInt32(packet.Z);
            writer.WriteByte(packet.Type);
            writer.WriteUInt16(packet.Race);
            writer.WriteByte(packet.MovingSpeed);
            writer.WriteByte(packet.AttackSpeed);
            writer.WriteByte(packet.StateFlag);
            writer.WriteUInt32(packet.AffectFlag0);
            writer.WriteUInt32(packet.AffectFlag1);

            return writer.BytesWritten;
        }

        public static byte[] Serialize(in PacketGCCharacterAdd packet)
        {
            byte[] buffer = new byte[PacketGCCharacterAdd.PacketSize];
            Serialize(packet, buffer);
            return buffer;
        }

        public static bool TryDeserialize(ReadOnlySpan<byte> source, out PacketGCCharacterAdd packet, out string errorMessage)
        {
            packet = default;
            errorMessage = null;

            if (source.Length < PacketGCCharacterAdd.PacketSize)
            {
                errorMessage = $"Buffer truncated: expected at least {PacketGCCharacterAdd.PacketSize} bytes, got {source.Length}.";
                return false;
            }

            var reader = new PacketReader(source);
            byte header = reader.ReadByte();
            if (header != PacketGCCharacterAdd.PacketHeader)
            {
                errorMessage = $"Invalid header: expected 0x{PacketGCCharacterAdd.PacketHeader:X2}, got 0x{header:X2}.";
                return false;
            }

            packet = new PacketGCCharacterAdd
            {
                Vid = reader.ReadUInt32(),
                Angle = reader.ReadFloat(),
                X = reader.ReadInt32(),
                Y = reader.ReadInt32(),
                Z = reader.ReadInt32(),
                Type = reader.ReadByte(),
                Race = reader.ReadUInt16(),
                MovingSpeed = reader.ReadByte(),
                AttackSpeed = reader.ReadByte(),
                StateFlag = reader.ReadByte(),
                AffectFlag0 = reader.ReadUInt32(),
                AffectFlag1 = reader.ReadUInt32()
            };
            return true;
        }

        public static PacketGCCharacterAdd Deserialize(ReadOnlySpan<byte> source)
        {
            if (source.Length < PacketGCCharacterAdd.PacketSize)
            {
                throw new PacketUnderflowException(PacketGCCharacterAdd.PacketSize, source.Length, nameof(PacketGCCharacterAdd));
            }

            byte header = source[0];
            if (header != PacketGCCharacterAdd.PacketHeader)
            {
                throw new InvalidPacketHeaderException(PacketGCCharacterAdd.PacketHeader, header, nameof(PacketGCCharacterAdd));
            }

            if (!TryDeserialize(source, out PacketGCCharacterAdd packet, out string errorMessage))
            {
                throw new PacketParseException(errorMessage, nameof(PacketGCCharacterAdd));
            }

            return packet;
        }
    }
}
