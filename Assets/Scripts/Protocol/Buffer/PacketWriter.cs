using System;
using System.Buffers.Binary;
using System.Text;

namespace Metin2.Protocol.Buffer
{
    /// <summary>
    /// High-performance, zero-allocation writer for serializing binary packets into a Span.
    /// Strictly enforces bounds checks and Little-Endian byte order matching the C++ server/client.
    /// </summary>
    public ref struct PacketWriter
    {
        private readonly Span<byte> _destination;
        private int _position;

        public PacketWriter(Span<byte> destination)
        {
            _destination = destination;
            _position = 0;
        }

        public int Position => _position;
        public int Capacity => _destination.Length;
        public int BytesWritten => _position;
        public int Remaining => _destination.Length - _position;

        private void EnsureAvailable(int count, string typeName)
        {
            if (count < 0 || count > Remaining)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(count),
                    $"Buffer overflow: Attempted to write {count} bytes for {typeName} at position {_position}, but only {Remaining} bytes remain in destination buffer.");
            }
        }

        public void WriteZeros(int count)
        {
            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count), "Count must be non-negative.");
            }
            EnsureAvailable(count, $"Zeros[{count}]");
            _destination.Slice(_position, count).Clear();
            _position += count;
        }

        public void WriteByte(byte value)
        {
            EnsureAvailable(1, nameof(Byte));
            _destination[_position++] = value;
        }

        public void WriteUInt16(ushort value)
        {
            EnsureAvailable(2, nameof(UInt16));
            BinaryPrimitives.WriteUInt16LittleEndian(_destination.Slice(_position, 2), value);
            _position += 2;
        }

        public void WriteInt16(short value)
        {
            EnsureAvailable(2, nameof(Int16));
            BinaryPrimitives.WriteInt16LittleEndian(_destination.Slice(_position, 2), value);
            _position += 2;
        }

        public void WriteUInt32(uint value)
        {
            EnsureAvailable(4, nameof(UInt32));
            BinaryPrimitives.WriteUInt32LittleEndian(_destination.Slice(_position, 4), value);
            _position += 4;
        }

        public void WriteInt32(int value)
        {
            EnsureAvailable(4, nameof(Int32));
            BinaryPrimitives.WriteInt32LittleEndian(_destination.Slice(_position, 4), value);
            _position += 4;
        }

        public void WriteBytes(ReadOnlySpan<byte> source)
        {
            EnsureAvailable(source.Length, $"Span<byte>[{source.Length}]");
            source.CopyTo(_destination.Slice(_position, source.Length));
            _position += source.Length;
        }

        public void WriteFixedString(string value, int fixedLength, Encoding encoding = null, bool nullTerminate = true)
        {
            if (fixedLength < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(fixedLength), "Fixed length must be non-negative.");
            }

            EnsureAvailable(fixedLength, $"FixedString[{fixedLength}]");
            encoding ??= Encoding.ASCII;

            Span<byte> target = _destination.Slice(_position, fixedLength);
            target.Clear(); // Zero-pad entire buffer

            if (!string.IsNullOrEmpty(value))
            {
                // Max characters that can fit leaving at least 1 null terminator byte if required
                // In C++ char buf[31] with strlcpy(buf, src, 31) allows up to 30 chars + null
                int maxBytes = nullTerminate ? Math.Max(0, fixedLength - 1) : fixedLength;
                if (maxBytes > 0)
                {
                    ReadOnlySpan<char> chars = value.AsSpan();
                    int charCount = Math.Min(chars.Length, maxBytes);

                    if (!encoding.IsSingleByte)
                    {
                        // For variable-width encodings, ensure byte count fits in maxBytes
                        while (charCount > 0 && encoding.GetByteCount(chars.Slice(0, charCount)) > maxBytes)
                        {
                            charCount--;
                        }
                    }

                    if (charCount > 0)
                    {
                        encoding.GetBytes(chars.Slice(0, charCount), target.Slice(0, maxBytes));
                    }
                }
            }

            _position += fixedLength;
        }
    }
}
