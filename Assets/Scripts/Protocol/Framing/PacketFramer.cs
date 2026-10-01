using System;
using System.Collections.Generic;
using Metin2.Protocol.Exceptions;

namespace Metin2.Protocol.Framing
{
    /// <summary>
    /// Reassembles the TCP byte stream into complete protocol frames.
    /// Mirrors the verified framing rules (docs/protocol/protocol-inventory.md §1):
    /// - TCP is a byte stream: one read may hold a fragment, one frame, or many frames.
    /// - 0x00 bytes are padding and are skipped (server input.cpp:78-79, client CheckPacket).
    /// - Fixed frame lengths come from <see cref="PacketLengthTable"/>; the one
    ///   dynamic frame (GC_SYNC_POSITION, header 0x05) carries its wSize inline
    ///   (server packet.h:1317-1322, validated like input_main.cpp:1786-1802).
    /// - Unknown headers and malformed dynamic sizes are dropped one byte at a
    ///   time and counted in <see cref="DroppedBytes"/> so the session layer
    ///   can close the connection on sustained garbage (server maps unknown
    ///   headers to PHASE_CLOSE, input.cpp:80-87).
    /// - The buffer never grows past <see cref="MaxFrameLength"/> (MAX_INPUT_LEN 65536,
    ///   desc.h:10-12); excess Append calls throw instead of allocating unbounded memory.
    ///
    /// Single-threaded by design: call Append/TryDequeue from one network thread and
    /// hand completed frames to the Unity main thread via a dispatcher queue.
    /// No UnityEngine dependency.
    /// </summary>
    public class PacketFramer
    {
        /// <summary>
        /// Server MAX_INPUT_LEN, desc.h:10-12. Also the output size
        /// DEFAULT_PACKET_BUFFER_SIZE*2 (protocol.h:42).
        /// </summary>
        public const int MaxFrameLength = 65536;

        private const byte PaddingByte = 0x00;
        private const int CompactionThreshold = 4096;

        private readonly List<byte> _buffer = new List<byte>();
        private int _start;

        /// <summary>Bytes currently buffered awaiting a complete frame.</summary>
        public int BufferedBytes => _buffer.Count - _start;

        /// <summary>Unknown-header bytes dropped so far. Never reset except by <see cref="Reset"/>.</summary>
        public int DroppedBytes { get; private set; }

        /// <summary>
        /// Appends newly received stream bytes. Throws <see cref="PacketException"/>
        /// instead of growing the buffer past <see cref="MaxFrameLength"/>.
        /// </summary>
        public void Append(ReadOnlySpan<byte> data)
        {
            if (data.Length < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(data));
            }

            if (BufferedBytes + data.Length > MaxFrameLength)
            {
                throw new PacketException(
                    $"Frame buffer overflow: {BufferedBytes} buffered + {data.Length} incoming exceeds MaxFrameLength {MaxFrameLength}.",
                    nameof(PacketFramer));
            }

            if (data.Length == 0)
            {
                return;
            }

            // List<byte>.AddRange(IEnumerable<byte>) would box the span; copy manually.
            _buffer.Capacity = Math.Max(_buffer.Capacity, _buffer.Count + data.Length);
            for (int i = 0; i < data.Length; i++)
            {
                _buffer.Add(data[i]);
            }
        }

        /// <summary>
        /// Dequeues one complete frame if available. Returns false when more bytes
        /// are needed (fragmented packet). Padding and unknown-header bytes are
        /// skipped/dropped transparently; observe <see cref="DroppedBytes"/> to
        /// detect garbage or desync.
        /// </summary>
        public bool TryDequeue(out byte[] frame)
        {
            frame = null;

            while (true)
            {
                if (BufferedBytes <= 0)
                {
                    return false;
                }

                byte header = _buffer[_start];

                if (header == PaddingByte)
                {
                    _start++;
                    CompactIfNeeded();
                    continue;
                }

                if (!PacketLengthTable.TryGetFixedLength(header, out int length))
                {
                    if (PacketLengthTable.IsSyncHeader(header))
                    {
                        // Dynamic sync frame: dequeue when complete, wait when
                        // fragmented, drop on malformed wSize (see below).
                        if (TryDequeueSyncFrame(out frame, out bool needMoreBytes))
                        {
                            return true;
                        }

                        if (needMoreBytes)
                        {
                            return false;
                        }

                        _start++;
                        DroppedBytes++;
                        CompactIfNeeded();
                        continue;
                    }

                    _start++;
                    DroppedBytes++;
                    CompactIfNeeded();
                    continue;
                }

                if (BufferedBytes < length)
                {
                    // Fragmented frame: wait for more stream bytes.
                    return false;
                }

                frame = new byte[length];
                _buffer.CopyTo(_start, frame, 0, length);
                _start += length;
                CompactIfNeeded();
                return true;
            }
        }

        public void Reset()
        {
            _buffer.Clear();
            _start = 0;
            DroppedBytes = 0;
        }

        /// <summary>
        /// Attempts to dequeue one GC_SYNC_POSITION frame starting at
        /// <see cref="_start"/>. Returns true with the frame when complete;
        /// false with needMoreBytes=true when fragmented; false with
        /// needMoreBytes=false when wSize is malformed (caller drops a byte).
        /// Mirrors the server guards (input_main.cpp:1786-1802): short wSize
        /// and misaligned payloads are rejected, never trusted for allocation.
        /// </summary>
        private bool TryDequeueSyncFrame(out byte[] frame, out bool needMoreBytes)
        {
            frame = null;
            needMoreBytes = false;

            if (BufferedBytes < PacketLengthTable.SyncHeaderSize)
            {
                needMoreBytes = true;
                return false;
            }

            int declared = _buffer[_start + 1] | (_buffer[_start + 2] << 8);
            if (declared < PacketLengthTable.SyncHeaderSize
                || declared > PacketLengthTable.MaxSyncPacketSize
                || ((declared - PacketLengthTable.SyncHeaderSize) % PacketLengthTable.SyncElementSize) != 0)
            {
                return false;
            }

            if (BufferedBytes < declared)
            {
                needMoreBytes = true;
                return false;
            }

            frame = new byte[declared];
            _buffer.CopyTo(_start, frame, 0, declared);
            _start += declared;
            CompactIfNeeded();
            return true;
        }

        private void CompactIfNeeded()
        {
            if (_start <= 0)
            {
                return;
            }

            if (_start >= _buffer.Count || _start >= CompactionThreshold)
            {
                _buffer.RemoveRange(0, _start);
                _start = 0;
            }
        }
    }
}
