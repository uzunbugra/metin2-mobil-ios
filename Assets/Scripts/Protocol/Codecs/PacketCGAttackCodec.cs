using System;
using Metin2.Protocol.Buffer;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Protocol.Codecs
{
    /// <summary>
    /// Codec for serializing and deserializing TPacketCGAttack (8 bytes).
    /// Verified from Server packet.h:564-571, Client Packet.h:535-542.
    /// </summary>
    public static class PacketCGAttackCodec
    {
        public static int Serialize(in PacketCGAttack packet, Span<byte> destination)
        {
            if (destination.Length < PacketCGAttack.PacketSize)
            {
                throw new ArgumentException(
                    $"Destination buffer too small: required {PacketCGAttack.PacketSize} bytes, available {destination.Length}.",
                    nameof(destination));
            }

            var writer = new PacketWriter(destination);
            writer.WriteByte(packet.Header);
            writer.WriteByte(packet.Type);
            writer.WriteUInt32(packet.VictimVid);
            writer.WriteByte(packet.CrcMagicCubeProcPiece);
            writer.WriteByte(packet.CrcMagicCubeFilePiece);

            return writer.BytesWritten;
        }

        public static byte[] Serialize(in PacketCGAttack packet)
        {
            byte[] buffer = new byte[PacketCGAttack.PacketSize];
            Serialize(packet, buffer);
            return buffer;
        }

        public static bool TryDeserialize(ReadOnlySpan<byte> source, out PacketCGAttack packet, out string errorMessage)
        {
            packet = default;
            errorMessage = null;

            if (source.Length < PacketCGAttack.PacketSize)
            {
                errorMessage = $"Buffer truncated: expected at least {PacketCGAttack.PacketSize} bytes for TPacketCGAttack, got {source.Length}.";
                return false;
            }

            var reader = new PacketReader(source);
            byte header = reader.ReadByte();
            if (header != PacketCGAttack.PacketHeader)
            {
                errorMessage = $"Invalid header: expected 0x{PacketCGAttack.PacketHeader:X2} (HEADER_CG_ATTACK), got 0x{header:X2}.";
                return false;
            }

            byte type = reader.ReadByte();
            uint victimVid = reader.ReadUInt32();
            byte crcProc = reader.ReadByte();
            byte crcFile = reader.ReadByte();
            packet = new PacketCGAttack(type, victimVid, crcProc, crcFile);
            return true;
        }

        public static PacketCGAttack Deserialize(ReadOnlySpan<byte> source)
        {
            if (source.Length < PacketCGAttack.PacketSize)
            {
                throw new PacketUnderflowException(PacketCGAttack.PacketSize, source.Length, nameof(PacketCGAttack));
            }

            if (source[0] != PacketCGAttack.PacketHeader)
            {
                throw new InvalidPacketHeaderException(PacketCGAttack.PacketHeader, source[0], nameof(PacketCGAttack));
            }

            if (!TryDeserialize(source, out PacketCGAttack packet, out string errorMessage))
            {
                throw new PacketParseException(errorMessage, nameof(PacketCGAttack));
            }

            return packet;
        }
    }
}
