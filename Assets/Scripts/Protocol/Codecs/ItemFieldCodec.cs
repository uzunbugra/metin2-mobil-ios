using System;
using Metin2.Protocol.Buffer;
using Metin2.Protocol.Packets;

namespace Metin2.Protocol.Codecs
{
    /// <summary>
    /// Shared field helpers for the item packets (SET 21 / DEL 20 / UPDATE 25).
    /// Cell, socket and attribute layouts are identical in all three
    /// (server packet.h:1063-1115; client Packet.h:1663-1710, GameType.h:312-316).
    /// </summary>
    internal static class ItemFieldCodec
    {
        public static void WriteCell(ref PacketWriter writer, byte window, ushort cell)
        {
            writer.WriteByte(window);
            writer.WriteUInt16(cell);
        }

        public static void ReadCell(ref PacketReader reader, out byte window, out ushort cell)
        {
            window = reader.ReadByte();
            cell = reader.ReadUInt16();
        }

        public static void WriteSockets(ref PacketWriter writer, int[] sockets)
        {
            int[] values = sockets ?? new int[PacketGCItemSet.SocketCount];
            for (int i = 0; i < PacketGCItemSet.SocketCount; i++)
            {
                writer.WriteInt32(i < values.Length ? values[i] : 0);
            }
        }

        public static int[] ReadSockets(ref PacketReader reader)
        {
            var result = new int[PacketGCItemSet.SocketCount];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = reader.ReadInt32();
            }

            return result;
        }

        public static void WriteAttributes(ref PacketWriter writer, ItemAttribute[] attributes)
        {
            ItemAttribute[] values = attributes ?? new ItemAttribute[PacketGCItemSet.AttributeCount];
            for (int i = 0; i < PacketGCItemSet.AttributeCount; i++)
            {
                ItemAttribute attr = i < values.Length ? values[i] : default;
                writer.WriteByte(attr.Type);
                writer.WriteInt16(attr.Value);
            }
        }

        public static ItemAttribute[] ReadAttributes(ref PacketReader reader)
        {
            var result = new ItemAttribute[PacketGCItemSet.AttributeCount];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = new ItemAttribute
                {
                    Type = reader.ReadByte(),
                    Value = reader.ReadInt16()
                };
            }

            return result;
        }
    }
}

