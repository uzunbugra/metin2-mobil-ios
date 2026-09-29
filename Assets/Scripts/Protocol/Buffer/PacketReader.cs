using System;
using System.Buffers.Binary;
using System.Text;

namespace Metin2.Protocol.Buffer
{
    /// <summary>
    /// High-performance, zero-allocation reader for parsing binary packets from a ReadOnlySpan.
    /// Strictly enforces bounds checks and Little-Endian byte order matching the C++ server/client.
    /// </summary>
    public ref struct PacketReader
    {
        private readonly ReadOnlySpan<byte> _data;
        private int _position;

        public PacketReader(ReadOnlySpan<byte> data)
        {
            _data = data;
            _position = 0;
        }

        public int Position => _position;
        public int Length => _data.Length;
        public int Remaining => _data.Length - _position;

        private void EnsureAvailable(int count, string typeName)
        {
            if (count < 0 || count > Remaining)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(count),
                    $"Truncated buffer: Attempted to read {count} bytes for {typeName} at position {_position}, but only {Remaining} bytes remain.");
            }
        }

        public byte PeekByte()
        {
            EnsureAvailable(1, nameof(Byte));
            return _data[_position];
        }

        public byte ReadByte()
        {
            EnsureAvailable(1, nameof(Byte));
            return _data[_position++];
        }

        public ushort ReadUInt16()
        {
            EnsureAvailable(2, nameof(UInt16));
            ushort value = BinaryPrimitives.ReadUInt16LittleEndian(_data.Slice(_position, 2));
            _position += 2;
            return value;
        }

        public short ReadInt16()
        {
            EnsureAvailable(2, nameof(Int16));
            short value = BinaryPrimitives.ReadInt16LittleEndian(_data.Slice(_position, 2));
            _position += 2;
            return value;
        }

        public uint ReadUInt32()
        {
            EnsureAvailable(4, nameof(UInt32));
            uint value = BinaryPrimitives.ReadUInt32LittleEndian(_data.Slice(_position, 4));
            _position += 4;
            return value;
        }

        public int ReadInt32()
        {
            EnsureAvailable(4, nameof(Int32));
            int value = BinaryPrimitives.ReadInt32LittleEndian(_data.Slice(_position, 4));
            _position += 4;
            return value;
        }

        public void ReadBytes(Span<byte> destination)
        {
            EnsureAvailable(destination.Length, $"Span<byte>[{destination.Length}]");
            _data.Slice(_position, destination.Length).CopyTo(destination);
            _position += destination.Length;
        }

        public ReadOnlySpan<byte> ReadSpan(int count)
        {
            EnsureAvailable(count, $"Span<byte>[{count}]");
            ReadOnlySpan<byte> slice = _data.Slice(_position, count);
            _position += count;
            return slice;
        }

        public string ReadFixedString(int fixedLength, Encoding encoding = null)
        {
            if (fixedLength < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(fixedLength), "Fixed length must be non-negative.");
            }

            EnsureAvailable(fixedLength, $"FixedString[{fixedLength}]");
            encoding ??= Encoding.ASCII;

            ReadOnlySpan<byte> slice = _data.Slice(_position, fixedLength);
            _position += fixedLength;

            // Find null terminator (0x00) if present
            int nullIndex = slice.IndexOf((byte)0);
            int validLength = nullIndex >= 0 ? nullIndex : fixedLength;

            if (validLength == 0)
            {
                return string.Empty;
            }

#if NETSTANDARD2_1_OR_GREATER || NETCOREAPP || NET5_0_OR_GREATER
            return encoding.GetString(slice.Slice(0, validLength));
#else
            return encoding.GetString(slice.Slice(0, validLength).ToArray());
#endif
        }
    }
}
