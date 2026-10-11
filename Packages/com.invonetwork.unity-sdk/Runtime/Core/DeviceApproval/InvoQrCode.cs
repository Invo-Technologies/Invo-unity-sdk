using System;
using System.Collections.Generic;
using System.Text;

namespace InvoSDK
{
    /// <summary>QR error-correction level. Higher levels survive more damage and glare at the cost of size.</summary>
    public enum InvoQrEcc
    {
        Low = 0,      // ~7% recoverable
        Medium = 1,   // ~15% recoverable
        Quartile = 2, // ~25% recoverable
        High = 3      // ~30% recoverable
    }

    /// <summary>
    /// Minimal QR Code Model 2 encoder (ISO/IEC 18004), byte mode, versions 1-40.
    ///
    /// Exists so the device-approval QR can be drawn on any platform with no native plugin and
    /// no third-party package. Pure C#, no UnityEngine dependency: <see cref="InvoQrTexture"/>
    /// turns the module grid into a texture. The algorithm follows Project Nayuki's reference
    /// encoder (MIT), restricted to the one segment mode a URL needs.
    /// </summary>
    public sealed class InvoQrCode
    {
        public const int MinVersion = 1;
        public const int MaxVersion = 40;

        /// <summary>Version of the symbol, 1-40.</summary>
        public int Version { get; }

        /// <summary>Side length in modules (version * 4 + 17). Excludes the quiet zone.</summary>
        public int Size { get; }

        /// <summary>Error-correction level actually used.</summary>
        public InvoQrEcc Ecc { get; }

        /// <summary>Mask pattern applied, 0-7.</summary>
        public int Mask { get; }

        private readonly bool[,] modules;    // [y, x]; true = dark
        private readonly bool[,] isFunction;

        /// <summary>True when the module at column <paramref name="x"/>, row <paramref name="y"/> is dark.
        /// Coordinates outside the symbol read as light, which is what the quiet zone is.</summary>
        public bool IsDark(int x, int y)
        {
            return x >= 0 && x < Size && y >= 0 && y < Size && modules[y, x];
        }

        /// <summary>Encodes <paramref name="text"/> as UTF-8 in byte mode, in the smallest version that fits.</summary>
        public static InvoQrCode EncodeText(string text, InvoQrEcc ecc = InvoQrEcc.Medium)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            return EncodeBytes(Encoding.UTF8.GetBytes(text), ecc, -1);
        }

        /// <summary>
        /// Encodes raw bytes. <paramref name="forcedMask"/> -1 picks the lowest-penalty mask, which is
        /// what a scanner expects; 0-7 forces one (tests only).
        /// </summary>
        public static InvoQrCode EncodeBytes(byte[] data, InvoQrEcc ecc, int forcedMask)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (forcedMask < -1 || forcedMask > 7) throw new ArgumentOutOfRangeException(nameof(forcedMask));

            int version = MinVersion;
            int dataUsedBits;
            for (; ; version++)
            {
                if (version > MaxVersion)
                    throw new ArgumentException("Data too long for a QR code.", nameof(data));
                int ccBits = version <= 9 ? 8 : 16;
                if (data.Length < (1 << ccBits))
                {
                    dataUsedBits = 4 + ccBits + data.Length * 8;
                    if (dataUsedBits <= NumDataCodewords(version, ecc) * 8)
                        break;
                }
            }

            var bits = new BitBuffer();
            bits.Append(0x4, 4); // byte mode indicator
            bits.Append(data.Length, version <= 9 ? 8 : 16);
            foreach (byte b in data)
                bits.Append(b, 8);

            int capacityBits = NumDataCodewords(version, ecc) * 8;
            bits.Append(0, Math.Min(4, capacityBits - bits.Count));
            bits.Append(0, (8 - bits.Count % 8) % 8);
            for (int pad = 0xEC; bits.Count < capacityBits; pad ^= 0xEC ^ 0x11)
                bits.Append(pad, 8);

            var codewords = new byte[bits.Count / 8];
            for (int i = 0; i < bits.Count; i++)
                if (bits[i])
                    codewords[i >> 3] |= (byte)(1 << (7 - (i & 7)));

            return new InvoQrCode(version, ecc, codewords, forcedMask);
        }

        private InvoQrCode(int version, InvoQrEcc ecc, byte[] dataCodewords, int forcedMask)
        {
            Version = version;
            Ecc = ecc;
            Size = version * 4 + 17;
            modules = new bool[Size, Size];
            isFunction = new bool[Size, Size];

            DrawFunctionPatterns();
            DrawCodewords(AddEccAndInterleave(dataCodewords));

            int mask = forcedMask;
            if (mask == -1)
            {
                int minPenalty = int.MaxValue;
                for (int i = 0; i < 8; i++)
                {
                    ApplyMask(i);
                    DrawFormatBits(i);
                    int penalty = PenaltyScore();
                    if (penalty < minPenalty)
                    {
                        mask = i;
                        minPenalty = penalty;
                    }
                    ApplyMask(i); // XOR undoes it
                }
            }
            Mask = mask;
            ApplyMask(mask);
            DrawFormatBits(mask);
        }

        // ------------------------------------------------------------------
        // Function patterns

        private void DrawFunctionPatterns()
        {
            for (int i = 0; i < Size; i++)
            {
                SetFunction(6, i, i % 2 == 0);
                SetFunction(i, 6, i % 2 == 0);
            }

            DrawFinder(3, 3);
            DrawFinder(Size - 4, 3);
            DrawFinder(3, Size - 4);

            int[] align = AlignmentPositions();
            int n = align.Length;
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++)
                {
                    if ((i == 0 && j == 0) || (i == 0 && j == n - 1) || (i == n - 1 && j == 0))
                        continue; // overlaps a finder
                    DrawAlignment(align[i], align[j]);
                }
            }

            DrawFormatBits(0); // placeholder so the area is marked as function modules
            DrawVersion();
        }

        private void DrawFormatBits(int mask)
        {
            int data = FormatBits(Ecc) << 3 | mask;
            int rem = data;
            for (int i = 0; i < 10; i++)
                rem = (rem << 1) ^ ((rem >> 9) * 0x537);
            int bits = (data << 10 | rem) ^ 0x5412;

            for (int i = 0; i <= 5; i++)
                SetFunction(8, i, Bit(bits, i));
            SetFunction(8, 7, Bit(bits, 6));
            SetFunction(8, 8, Bit(bits, 7));
            SetFunction(7, 8, Bit(bits, 8));
            for (int i = 9; i < 15; i++)
                SetFunction(14 - i, 8, Bit(bits, i));

            for (int i = 0; i < 8; i++)
                SetFunction(Size - 1 - i, 8, Bit(bits, i));
            for (int i = 8; i < 15; i++)
                SetFunction(8, Size - 15 + i, Bit(bits, i));
            SetFunction(8, Size - 8, true); // the dark module
        }

        private void DrawVersion()
        {
            if (Version < 7)
                return;
            int rem = Version;
            for (int i = 0; i < 12; i++)
                rem = (rem << 1) ^ ((rem >> 11) * 0x1F25);
            int bits = Version << 12 | rem;

            for (int i = 0; i < 18; i++)
            {
                bool bit = Bit(bits, i);
                int a = Size - 11 + i % 3;
                int b = i / 3;
                SetFunction(a, b, bit);
                SetFunction(b, a, bit);
            }
        }

        private void DrawFinder(int x, int y)
        {
            for (int dy = -4; dy <= 4; dy++)
            {
                for (int dx = -4; dx <= 4; dx++)
                {
                    int dist = Math.Max(Math.Abs(dx), Math.Abs(dy));
                    int xx = x + dx, yy = y + dy;
                    if (xx >= 0 && xx < Size && yy >= 0 && yy < Size)
                        SetFunction(xx, yy, dist != 2 && dist != 4);
                }
            }
        }

        private void DrawAlignment(int x, int y)
        {
            for (int dy = -2; dy <= 2; dy++)
                for (int dx = -2; dx <= 2; dx++)
                    SetFunction(x + dx, y + dy, Math.Max(Math.Abs(dx), Math.Abs(dy)) != 1);
        }

        private void SetFunction(int x, int y, bool dark)
        {
            modules[y, x] = dark;
            isFunction[y, x] = true;
        }

        private int[] AlignmentPositions()
        {
            if (Version == 1)
                return new int[0];
            int numAlign = Version / 7 + 2;
            int step = (Version * 8 + numAlign * 3 + 5) / (numAlign * 4 - 4) * 2;
            var result = new int[numAlign];
            result[0] = 6;
            for (int i = numAlign - 1, pos = Size - 7; i >= 1; i--, pos -= step)
                result[i] = pos;
            return result;
        }

        // ------------------------------------------------------------------
        // Codewords

        private byte[] AddEccAndInterleave(byte[] data)
        {
            int numBlocks = NumErrorCorrectionBlocks[(int)Ecc, Version];
            int blockEccLen = EccCodewordsPerBlock[(int)Ecc, Version];
            int rawCodewords = NumRawDataModules(Version) / 8;
            int numShortBlocks = numBlocks - rawCodewords % numBlocks;
            int shortBlockLen = rawCodewords / numBlocks;

            byte[] divisor = ReedSolomonDivisor(blockEccLen);
            var blocks = new List<byte[]>(numBlocks);
            for (int i = 0, k = 0; i < numBlocks; i++)
            {
                int datLen = shortBlockLen - blockEccLen + (i < numShortBlocks ? 0 : 1);
                var dat = new byte[datLen];
                Array.Copy(data, k, dat, 0, datLen);
                k += datLen;
                byte[] ecc = ReedSolomonRemainder(dat, divisor);

                // Every block is padded to shortBlockLen + 1 so the interleave is one loop;
                // the padding slot of a short block is skipped below.
                var block = new byte[shortBlockLen + 1];
                Array.Copy(dat, 0, block, 0, datLen);
                Array.Copy(ecc, 0, block, block.Length - blockEccLen, blockEccLen);
                blocks.Add(block);
            }

            var result = new byte[rawCodewords];
            int r = 0;
            for (int i = 0; i < shortBlockLen + 1; i++)
            {
                for (int j = 0; j < numBlocks; j++)
                {
                    if (i != shortBlockLen - blockEccLen || j >= numShortBlocks)
                        result[r++] = blocks[j][i];
                }
            }
            return result;
        }

        private void DrawCodewords(byte[] data)
        {
            int i = 0;
            for (int right = Size - 1; right >= 1; right -= 2)
            {
                if (right == 6)
                    right = 5; // skip the vertical timing pattern
                for (int vert = 0; vert < Size; vert++)
                {
                    for (int j = 0; j < 2; j++)
                    {
                        int x = right - j;
                        bool upward = ((right + 1) & 2) == 0;
                        int y = upward ? Size - 1 - vert : vert;
                        if (!isFunction[y, x] && i < data.Length * 8)
                        {
                            modules[y, x] = Bit(data[i >> 3], 7 - (i & 7));
                            i++;
                        }
                        // Remainder bits stay light, as initialised.
                    }
                }
            }
        }

        private void ApplyMask(int mask)
        {
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    bool invert;
                    switch (mask)
                    {
                        case 0: invert = (x + y) % 2 == 0; break;
                        case 1: invert = y % 2 == 0; break;
                        case 2: invert = x % 3 == 0; break;
                        case 3: invert = (x + y) % 3 == 0; break;
                        case 4: invert = (x / 3 + y / 2) % 2 == 0; break;
                        case 5: invert = x * y % 2 + x * y % 3 == 0; break;
                        case 6: invert = (x * y % 2 + x * y % 3) % 2 == 0; break;
                        case 7: invert = ((x + y) % 2 + x * y % 3) % 2 == 0; break;
                        default: throw new ArgumentOutOfRangeException(nameof(mask));
                    }
                    if (invert && !isFunction[y, x])
                        modules[y, x] = !modules[y, x];
                }
            }
        }

        // ------------------------------------------------------------------
        // Mask penalty (ISO/IEC 18004 section 7.8.3)

        private const int PenaltyN1 = 3;
        private const int PenaltyN2 = 3;
        private const int PenaltyN3 = 40;
        private const int PenaltyN4 = 10;

        private int PenaltyScore()
        {
            int result = 0;
            var history = new int[7];

            for (int y = 0; y < Size; y++)
            {
                bool runColor = false;
                int runLen = 0;
                Array.Clear(history, 0, history.Length);
                for (int x = 0; x < Size; x++)
                {
                    if (modules[y, x] == runColor)
                    {
                        runLen++;
                        if (runLen == 5) result += PenaltyN1;
                        else if (runLen > 5) result++;
                    }
                    else
                    {
                        AddHistory(runLen, history);
                        if (!runColor)
                            result += CountFinderLike(history) * PenaltyN3;
                        runColor = modules[y, x];
                        runLen = 1;
                    }
                }
                result += TerminateAndCount(runColor, runLen, history) * PenaltyN3;
            }

            for (int x = 0; x < Size; x++)
            {
                bool runColor = false;
                int runLen = 0;
                Array.Clear(history, 0, history.Length);
                for (int y = 0; y < Size; y++)
                {
                    if (modules[y, x] == runColor)
                    {
                        runLen++;
                        if (runLen == 5) result += PenaltyN1;
                        else if (runLen > 5) result++;
                    }
                    else
                    {
                        AddHistory(runLen, history);
                        if (!runColor)
                            result += CountFinderLike(history) * PenaltyN3;
                        runColor = modules[y, x];
                        runLen = 1;
                    }
                }
                result += TerminateAndCount(runColor, runLen, history) * PenaltyN3;
            }

            for (int y = 0; y < Size - 1; y++)
            {
                for (int x = 0; x < Size - 1; x++)
                {
                    bool c = modules[y, x];
                    if (c == modules[y, x + 1] && c == modules[y + 1, x] && c == modules[y + 1, x + 1])
                        result += PenaltyN2;
                }
            }

            int dark = 0;
            for (int y = 0; y < Size; y++)
                for (int x = 0; x < Size; x++)
                    if (modules[y, x]) dark++;
            int total = Size * Size;
            int k = (Math.Abs(dark * 20 - total * 10) + total - 1) / total - 1;
            result += k * PenaltyN4;
            return result;
        }

        private int CountFinderLike(int[] h)
        {
            int n = h[1];
            bool core = n > 0 && h[2] == n && h[3] == n * 3 && h[4] == n && h[5] == n;
            return (core && h[0] >= n * 4 && h[6] >= n ? 1 : 0)
                 + (core && h[6] >= n * 4 && h[0] >= n ? 1 : 0);
        }

        private int TerminateAndCount(bool runColor, int runLen, int[] h)
        {
            if (runColor)
            {
                AddHistory(runLen, h);
                runLen = 0;
            }
            runLen += Size; // the light quiet zone after the last module
            AddHistory(runLen, h);
            return CountFinderLike(h);
        }

        private void AddHistory(int runLen, int[] h)
        {
            if (h[0] == 0)
                runLen += Size; // the light quiet zone before the first module
            Array.Copy(h, 0, h, 1, h.Length - 1);
            h[0] = runLen;
        }

        // ------------------------------------------------------------------
        // Reed-Solomon over GF(2^8 / 0x11D)

        private static byte[] ReedSolomonDivisor(int degree)
        {
            var result = new byte[degree];
            result[degree - 1] = 1;
            int root = 1;
            for (int i = 0; i < degree; i++)
            {
                for (int j = 0; j < degree; j++)
                {
                    result[j] = GfMultiply(result[j], root);
                    if (j + 1 < degree)
                        result[j] ^= result[j + 1];
                }
                root = GfMultiply(root, 0x02);
            }
            return result;
        }

        private static byte[] ReedSolomonRemainder(byte[] data, byte[] divisor)
        {
            var result = new byte[divisor.Length];
            foreach (byte b in data)
            {
                int factor = b ^ result[0];
                Array.Copy(result, 1, result, 0, result.Length - 1);
                result[result.Length - 1] = 0;
                for (int i = 0; i < result.Length; i++)
                    result[i] ^= GfMultiply(divisor[i], factor);
            }
            return result;
        }

        private static byte GfMultiply(int x, int y)
        {
            int z = 0;
            for (int i = 7; i >= 0; i--)
            {
                z = (z << 1) ^ ((z >> 7) * 0x11D);
                z ^= ((y >> i) & 1) * x;
            }
            return (byte)z;
        }

        // ------------------------------------------------------------------
        // Capacity tables

        private static int NumRawDataModules(int ver)
        {
            int result = (16 * ver + 128) * ver + 64;
            if (ver >= 2)
            {
                int numAlign = ver / 7 + 2;
                result -= (25 * numAlign - 10) * numAlign - 55;
                if (ver >= 7)
                    result -= 36;
            }
            return result;
        }

        private static int NumDataCodewords(int ver, InvoQrEcc ecc)
        {
            return NumRawDataModules(ver) / 8
                 - EccCodewordsPerBlock[(int)ecc, ver] * NumErrorCorrectionBlocks[(int)ecc, ver];
        }

        private static int FormatBits(InvoQrEcc ecc)
        {
            switch (ecc)
            {
                case InvoQrEcc.Low: return 1;
                case InvoQrEcc.Medium: return 0;
                case InvoQrEcc.Quartile: return 3;
                default: return 2;
            }
        }

        private static bool Bit(int x, int i)
        {
            return ((x >> i) & 1) != 0;
        }

        private static readonly int[,] EccCodewordsPerBlock =
        {
            // 0, 1..40
            { -1,  7, 10, 15, 20, 26, 18, 20, 24, 30, 18, 20, 24, 26, 30, 22, 24, 28, 30, 28, 28, 28, 28, 30, 30, 26, 28, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30 },
            { -1, 10, 16, 26, 18, 24, 16, 18, 22, 22, 26, 30, 22, 22, 24, 24, 28, 28, 26, 26, 26, 26, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28, 28 },
            { -1, 13, 22, 18, 26, 18, 24, 18, 22, 20, 24, 28, 26, 24, 20, 30, 24, 28, 28, 26, 30, 28, 30, 30, 30, 30, 28, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30 },
            { -1, 17, 28, 22, 16, 22, 28, 26, 26, 24, 28, 24, 28, 22, 24, 24, 30, 28, 28, 26, 28, 30, 24, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30, 30 },
        };

        private static readonly int[,] NumErrorCorrectionBlocks =
        {
            { -1, 1, 1, 1, 1, 1, 2, 2, 2, 2,  4,  4,  4,  4,  4,  6,  6,  6,  6,  7,  8,  8,  9,  9, 10, 12, 12, 12, 13, 14, 15, 16, 17, 18, 19, 19, 20, 21, 22, 24, 25 },
            { -1, 1, 1, 1, 2, 2, 4, 4, 4, 5,  5,  5,  8,  9,  9, 10, 10, 11, 13, 14, 16, 17, 17, 18, 20, 21, 23, 25, 26, 28, 29, 31, 33, 35, 37, 38, 40, 43, 45, 47, 49 },
            { -1, 1, 1, 2, 2, 4, 4, 6, 6, 8,  8,  8, 10, 12, 16, 12, 17, 16, 18, 21, 20, 23, 23, 25, 27, 29, 34, 34, 35, 38, 40, 43, 45, 48, 51, 53, 56, 59, 62, 65, 68 },
            { -1, 1, 1, 2, 4, 4, 4, 5, 6, 8,  8, 11, 11, 16, 16, 18, 16, 19, 21, 25, 25, 25, 34, 30, 32, 35, 37, 40, 42, 45, 48, 51, 54, 57, 60, 63, 66, 70, 74, 77, 81 },
        };

        private sealed class BitBuffer
        {
            private readonly List<bool> bits = new List<bool>();

            public int Count { get { return bits.Count; } }

            public bool this[int i] { get { return bits[i]; } }

            public void Append(int value, int length)
            {
                for (int i = length - 1; i >= 0; i--)
                    bits.Add(((value >> i) & 1) != 0);
            }
        }
    }
}
