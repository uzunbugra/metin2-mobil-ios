using System;

namespace Metin2.Tools.PackExtractor
{
    /// <summary>
    /// LZO1X-1 safe decompressor — port of the Linux kernel's
    /// lib/lzo/lzo1x_decompress_safe.c (Markus F.X.J. Oberhumer's LZO,
    /// changed for kernel use by Nitin Gupta / Richard Purdie), which is the
    /// hardened variant of the classic minilzo decompressor used by the PC
    /// client (lzo1x_decompress, EterBase/lzo.cpp:280,291).
    ///
    /// Differences from the kernel version, deliberately:
    /// - bitstream_version is fixed to 0: Metin2 packs were produced by the
    ///   classic lzo1x_1_compress, which has no [17, version] header and no
    ///   zero-run extension. With version 0 the kernel code degenerates to
    ///   exactly the classic semantics.
    /// - The unaligned COPY4/COPY8 fast paths are replaced by the plain
    ///   byte loops from the same file (correctness over speed).
    /// </summary>
    public static class Lzo1x
    {
        private const int LzoEOk = 0;
        private const int LzoEError = 1;
        private const int LzoEInputOverrun = 4;
        private const int LzoEOutputOverrun = 5;
        private const int LzoELookbehindOverrun = 6;
        private const int M2MaxOffset = 0x0800;
        private const int Max255Count = (int.MaxValue / 255) - 2;

        /// <summary>
        /// Decompresses a classic LZO1X-1 stream and validates that exactly
        /// <paramref name="expectedSize"/> bytes are produced (the MCOZ
        /// wrapper's dwRealSize, lzo.cpp:298-302 parity).
        /// </summary>
        public static byte[] Decompress(ReadOnlySpan<byte> input, int expectedSize)
        {
            if (expectedSize < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(expectedSize));
            }

            var output = new byte[expectedSize];
            int result = DecompressSafe(input, output, out int outLen);

            if (result != LzoEOk)
            {
                throw new InvalidDataException($"LZO1X decompress failed: {ErrorName(result)} (out={outLen})");
            }

            if (outLen != expectedSize)
            {
                throw new InvalidDataException($"LZO1X size mismatch: produced {outLen}, expected {expectedSize}");
            }

            return output;
        }

        public static string ErrorName(int code) => code switch
        {
            LzoEError => "LZO_E_ERROR",
            LzoEInputOverrun => "LZO_E_INPUT_OVERRUN",
            LzoEOutputOverrun => "LZO_E_OUTPUT_OVERRUN",
            LzoELookbehindOverrun => "LZO_E_LOOKBEHIND_OVERRUN",
            _ => $"LZO_E_{code}",
        };

        private static int DecompressSafe(ReadOnlySpan<byte> input, byte[] output, out int outLen)
        {
            try
            {
                return DecompressSafeCore(input, output, out outLen);
            }
            catch (IndexOutOfRangeException)
            {
                // Malformed/truncated stream: the C original reads past the
                // buffer (UB); we treat it as an input overrun.
                outLen = 0;
                return LzoEInputOverrun;
            }
        }

        private static int DecompressSafeCore(ReadOnlySpan<byte> input, byte[] output, out int outLen)
        {
            int ip = 0;
            int op = 0;
            int ipEnd = input.Length;
            int opEnd = output.Length;
            int state = 0;
            int mPos = 0;
            int t = 0;
            int next = 0;

            outLen = op;

            if (ipEnd < 3)
            {
                return LzoEInputOverrun;
            }

            // Classic stream: no [17, version] header (bitstream_version = 0).

            // Initial literal run (kernel: the pre-loop "*ip > 17" check with
            // gotos into the loop body — inlined here because C# forbids
            // jumping into nested scopes; at most one occurrence).
            if (input[ip] > 17)
            {
                t = input[ip++] - 17;
                if (t < 4)
                {
                    // matchNext equivalent: state = next; copy `t` literals.
                    state = t;
                    if (ipEnd - ip < t + 3)
                    {
                        return LzoEInputOverrun;
                    }

                    if (opEnd - op < t)
                    {
                        return LzoEOutputOverrun;
                    }

                    while (t > 0)
                    {
                        output[op++] = input[ip++];
                        t--;
                    }
                }
                else
                {
                    // copyLiteralRun equivalent: copy `t` literals; state = 4.
                    if (opEnd - op < t)
                    {
                        return LzoEOutputOverrun;
                    }

                    if (ipEnd - ip < t + 3)
                    {
                        return LzoEInputOverrun;
                    }

                    do
                    {
                        output[op++] = input[ip++];
                    }
                    while (--t > 0);

                    state = 4;
                }
            }

            for (;;)
            {
                t = input[ip++];
                if (t < 16)
                {
                    if (state == 0)
                    {
                        if (t == 0)
                        {
                            int ipLast = ip;
                            while (ip < ipEnd && input[ip] == 0)
                            {
                                ip++;
                                if (ipEnd - ip < 1)
                                {
                                    return LzoEInputOverrun;
                                }
                            }

                            int offset = ip - ipLast;
                            if (offset > Max255Count)
                            {
                                return LzoEError;
                            }

                            offset = (offset << 8) - offset;
                            t += offset + 15 + input[ip++];
                        }

                        t += 3;

                        if (opEnd - op < t)
                        {
                            return LzoEOutputOverrun;
                        }

                        if (ipEnd - ip < t + 3)
                        {
                            return LzoEInputOverrun;
                        }

                        do
                        {
                            output[op++] = input[ip++];
                        }
                        while (--t > 0);

                        state = 4;
                        continue;
                    }
                    else if (state != 4)
                    {
                        next = t & 3;
                        mPos = op - 1;
                        mPos -= t >> 2;
                        mPos -= input[ip++] << 2;
                        if (mPos < 0)
                        {
                            return LzoELookbehindOverrun;
                        }

                        if (opEnd - op < 2)
                        {
                            return LzoEOutputOverrun;
                        }

                        output[op] = output[mPos];
                        output[op + 1] = output[mPos + 1];
                        op += 2;
                        goto matchNext;
                    }
                    else
                    {
                        next = t & 3;
                        mPos = op - (1 + M2MaxOffset);
                        mPos -= t >> 2;
                        mPos -= input[ip++] << 2;
                        t = 3;
                    }
                }
                else if (t >= 64)
                {
                    next = t & 3;
                    mPos = op - 1;
                    mPos -= (t >> 2) & 7;
                    mPos -= input[ip++] << 3;
                    t = (t >> 5) - 1 + (3 - 1);
                }
                else if (t >= 32)
                {
                    t = (t & 31) + (3 - 1);
                    if (t == 2)
                    {
                        int ipLast = ip;
                        while (ip < ipEnd && input[ip] == 0)
                        {
                            ip++;
                            if (ipEnd - ip < 1)
                            {
                                return LzoEInputOverrun;
                            }
                        }

                        int offset = ip - ipLast;
                        if (offset > Max255Count)
                        {
                            return LzoEError;
                        }

                        offset = (offset << 8) - offset;
                        t += offset + 31 + input[ip++];
                        if (ipEnd - ip < 2)
                        {
                            return LzoEInputOverrun;
                        }
                    }

                    mPos = op - 1;
                    next = input[ip] | (input[ip + 1] << 8);
                    ip += 2;
                    mPos -= next >> 2;
                    next &= 3;
                }
                else
                {
                    // t < 32, bitstream_version == 0: classic M1 match
                    // (no zero-run extension).
                    if (ipEnd - ip < 2)
                    {
                        return LzoEInputOverrun;
                    }

                    next = input[ip] | (input[ip + 1] << 8);
                    mPos = op;
                    mPos -= (t & 8) << 11;
                    t = (t & 7) + (3 - 1);
                    if (t == 2)
                    {
                        int ipLast = ip;
                        while (ip < ipEnd && input[ip] == 0)
                        {
                            ip++;
                            if (ipEnd - ip < 1)
                            {
                                return LzoEInputOverrun;
                            }
                        }

                        int offset = ip - ipLast;
                        if (offset > Max255Count)
                        {
                            return LzoEError;
                        }

                        offset = (offset << 8) - offset;
                        t += offset + 7 + input[ip++];
                        if (ipEnd - ip < 2)
                        {
                            return LzoEInputOverrun;
                        }

                        next = input[ip] | (input[ip + 1] << 8);
                    }

                    ip += 2;
                    mPos -= next >> 2;
                    next &= 3;
                    if (mPos == op)
                    {
                        goto eofFound;
                    }

                    mPos -= 0x4000;
                }

                if (mPos < 0)
                {
                    return LzoELookbehindOverrun;
                }

                {
                    int oe = op + t;
                    if (opEnd - op < t)
                    {
                        return LzoEOutputOverrun;
                    }

                    output[op] = output[mPos];
                    output[op + 1] = output[mPos + 1];
                    op += 2;
                    mPos += 2;
                    do
                    {
                        output[op++] = output[mPos++];
                    }
                    while (op < oe);
                }

matchNext:
                state = next;
                t = next;
                if (ipEnd - ip < t + 3)
                {
                    return LzoEInputOverrun;
                }

                if (opEnd - op < t)
                {
                    return LzoEOutputOverrun;
                }

                while (t > 0)
                {
                    output[op++] = input[ip++];
                    t--;
                }
            }

eofFound:
            outLen = op;
            return t != 3 ? LzoEError
                : ip == ipEnd ? LzoEOk
                : ip < ipEnd ? LzoEError /* INPUT_NOT_CONSUMED: classic parity = fail */
                : LzoEInputOverrun;
        }
    }
}
