using System;
using Metin2.Protocol.Buffer;
using Metin2.Protocol.Exceptions;
using Metin2.Protocol.Packets;

namespace Metin2.Protocol.Codecs
{
    /// <summary>
    /// Codec for TPacketCGEnterGame (1 byte).
    /// Server: game/src/packet.h:627-630. Client: UserInterface/Packet.h:564-567.
    /// </summary>
    public static class PacketCGEnterGameCodec
    {
        public static int Serialize(in PacketCGEnterGame packet, Span<byte> destination)
        {
            if (destination.Length < PacketCGEnterGame.PacketSize)
            {
                throw new ArgumentException(
                    $"Destination buffer too small: required {PacketCGEnterGame.PacketSize} bytes, available {destination.Length}.",
                    nameof(destination));
            }

            destination[0] = packet.Header;
            return PacketCGEnterGame.PacketSize;
        }

        public static byte[] Serialize(in PacketCGEnterGame packet)
        {
            return new byte[] { packet.Header };
        }

        public static bool TryDeserialize(ReadOnlySpan<byte> source, out PacketCGEnterGame packet, out string errorMessage)
        {
            packet = default;
            errorMessage = null;

            if (source.Length < PacketCGEnterGame.PacketSize)
            {
                errorMessage = $"Buffer truncated: expected at least {PacketCGEnterGame.PacketSize} bytes, got {source.Length}.";
                return false;
            }

            if (source[0] != PacketCGEnterGame.PacketHeader)
            {
                errorMessage = $"Invalid header: expected 0x{PacketCGEnterGame.PacketHeader:X2}, got 0x{source[0]:X2}.";
                return false;
            }

            packet = new PacketCGEnterGame();
            return true;
        }

        public static PacketCGEnterGame Deserialize(ReadOnlySpan<byte> source)
        {
            if (source.Length < PacketCGEnterGame.PacketSize)
            {
                throw new PacketUnderflowException(PacketCGEnterGame.PacketSize, source.Length, nameof(PacketCGEnterGame));
            }

            if (source[0] != PacketCGEnterGame.PacketHeader)
            {
                throw new InvalidPacketHeaderException(PacketCGEnterGame.PacketHeader, source[0], nameof(PacketCGEnterGame));
            }

            return new PacketCGEnterGame();
        }
    }
}
