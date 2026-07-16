using System;
using System.IO;
using System.IO.Compression;

namespace EsgSignalCreator.Io
{
    /// <summary>
    /// Minimal reader for MATLAB Level-5 MAT-files (MAT File 5), extracting a baseband I/Q waveform
    /// from the first suitable numeric array. Supports:
    /// <list type="bullet">
    ///   <item>Uncompressed and zlib-compressed (<c>miCOMPRESSED</c>) top-level matrix elements —
    ///         MATLAB's default <c>save</c> (v7) compresses; <c>save -v6</c> does not.</item>
    ///   <item>Numeric classes double / single / (u)int8-32 for the data arrays.</item>
    ///   <item>Little- and big-endian files (per the header's endian indicator).</item>
    /// </list>
    /// Waveform mapping: a <b>complex</b> array → I = real part, Q = imaginary part; a real
    /// <b>2×N or N×2</b> array → the two rows/columns become I and Q; a real <b>vector</b> → I only
    /// (Q = 0). MATLAB stores column-major, which is honoured when de-interleaving.
    /// </summary>
    public static class MatFileReader
    {
        // miTYPE element type codes.
        private const int MiInt8 = 1, MiUint8 = 2, MiInt16 = 3, MiUint16 = 4, MiInt32 = 5, MiUint32 = 6,
                          MiSingle = 7, MiDouble = 9, MiInt64 = 12, MiUint64 = 13,
                          MiMatrix = 14, MiCompressed = 15;

        // mxCLASS array-class codes we treat as numeric.
        private const int MxDouble = 6, MxSingle = 7, MxInt8 = 8, MxUint8 = 9, MxInt16 = 10, MxUint16 = 11,
                          MxInt32 = 12, MxUint32 = 13, MxInt64 = 14, MxUint64 = 15;

        /// <summary>Read the first numeric array in the MAT-file at <paramref name="path"/> as I/Q.</summary>
        public static void ReadIq(string path, out float[] i, out float[] q)
        {
            byte[] bytes = File.ReadAllBytes(path);
            if (bytes.Length < 128)
                throw new InvalidDataException("File is too short to be a MAT-file: '" + path + "'.");

            // Header endian indicator lives at bytes 126-127 ('IM' => little-endian, 'MI' => big-endian).
            bool little = bytes[126] == (byte)'I';

            int pos = 128;
            while (pos + 8 <= bytes.Length)
            {
                ReadTag(bytes, pos, little, out int type, out int nbytes, out int dataOffset, out int headerLen);
                int next = pos + headerLen + Pad8(nbytes);

                byte[] element = null;
                int elemType = type;
                if (type == MiCompressed)
                {
                    element = Inflate(bytes, dataOffset, nbytes);
                    // The decompressed payload is itself a tagged element (a matrix).
                    ReadTag(element, 0, little, out elemType, out int innerBytes, out int innerOffset, out int innerHeader);
                    if (elemType == MiMatrix &&
                        TryReadMatrix(element, innerOffset, innerBytes, little, out i, out q))
                        return;
                }
                else if (type == MiMatrix)
                {
                    if (TryReadMatrix(bytes, dataOffset, nbytes, little, out i, out q))
                        return;
                }

                pos = next;
            }

            throw new InvalidDataException("No numeric I/Q array was found in the MAT-file '" + path + "'.");
        }

        /// <summary>Parse one <c>miMATRIX</c> element body into I/Q. Returns false for non-numeric arrays.</summary>
        private static bool TryReadMatrix(byte[] b, int offset, int length, bool little, out float[] i, out float[] q)
        {
            i = null; q = null;
            int end = offset + length;
            int p = offset;

            // 1) Array flags (miUINT32, 2 words): class in the low byte, complex flag at bit 11.
            ReadTag(b, p, little, out _, out int flagBytes, out int flagData, out int flagHeader);
            if (flagBytes < 8) return false;
            uint flags = ReadUInt32(b, flagData, little);
            int mxClass = (int)(flags & 0xFF);
            bool complex = (flags & 0x0800) != 0;
            p += flagHeader + Pad8(flagBytes);

            // 2) Dimensions (miINT32).
            ReadTag(b, p, little, out _, out int dimBytes, out int dimData, out int dimHeader);
            int nDims = dimBytes / 4;
            int rows = nDims > 0 ? ReadInt32(b, dimData, little) : 0;
            int cols = nDims > 1 ? ReadInt32(b, dimData + 4, little) : 1;
            for (int d = 2; d < nDims; d++) cols *= ReadInt32(b, dimData + d * 4, little); // fold extra dims into cols
            p += dimHeader + Pad8(dimBytes);

            // 3) Array name (miINT8) — skip.
            ReadTag(b, p, little, out _, out int nameBytes, out _, out int nameHeader);
            p += nameHeader + Pad8(nameBytes);

            if (mxClass < MxDouble || mxClass > MxUint64) return false; // not a plain numeric array

            // 4) Real part (pr).
            if (p >= end) return false;
            double[] re = ReadNumericElement(b, p, little, out int reAdvance);
            p += reAdvance;

            double[] im = null;
            if (complex && p < end)
                im = ReadNumericElement(b, p, little, out _);

            BuildIq(rows, cols, re, im, out i, out q);
            return i != null;
        }

        /// <summary>Map a real/complex MATLAB array (column-major) to I/Q channels.</summary>
        private static void BuildIq(int rows, int cols, double[] re, double[] im, out float[] i, out float[] q)
        {
            i = null; q = null;
            if (re == null || re.Length == 0) return;

            if (im != null)
            {
                // Complex array => one complex sample per element (column-major order preserved).
                int n = Math.Min(re.Length, im.Length);
                i = new float[n]; q = new float[n];
                for (int k = 0; k < n; k++) { i[k] = (float)re[k]; q[k] = (float)im[k]; }
                return;
            }

            if (rows == 2 && cols >= 1 && re.Length >= 2 * cols)
            {
                // 2×N, column-major: [r0c0, r1c0, r0c1, r1c1, ...] => I = row 0, Q = row 1.
                i = new float[cols]; q = new float[cols];
                for (int c = 0; c < cols; c++) { i[c] = (float)re[2 * c]; q[c] = (float)re[2 * c + 1]; }
                return;
            }

            if (cols == 2 && rows >= 1 && re.Length >= 2 * rows)
            {
                // N×2, column-major: first N = column 0 (I), next N = column 1 (Q).
                i = new float[rows]; q = new float[rows];
                for (int r = 0; r < rows; r++) { i[r] = (float)re[r]; q[r] = (float)re[rows + r]; }
                return;
            }

            // Real vector (or anything else) => I only, Q = 0.
            i = new float[re.Length]; q = new float[re.Length];
            for (int k = 0; k < re.Length; k++) i[k] = (float)re[k];
        }

        /// <summary>Read a numeric data sub-element (real or imaginary part) into doubles.</summary>
        private static double[] ReadNumericElement(byte[] b, int p, bool little, out int advance)
        {
            ReadTag(b, p, little, out int type, out int nbytes, out int data, out int header);
            advance = header + Pad8(nbytes);

            int size = ElementSize(type);
            if (size == 0) return Array.Empty<double>();
            int count = nbytes / size;
            var v = new double[count];
            for (int k = 0; k < count; k++)
            {
                int o = data + k * size;
                switch (type)
                {
                    case MiDouble: v[k] = BitConverter.Int64BitsToDouble(ReadInt64Bits(b, o, little)); break;
                    case MiSingle: v[k] = ReadSingle(b, o, little); break;
                    case MiInt8: v[k] = (sbyte)b[o]; break;
                    case MiUint8: v[k] = b[o]; break;
                    case MiInt16: v[k] = (short)ReadUInt16(b, o, little); break;
                    case MiUint16: v[k] = ReadUInt16(b, o, little); break;
                    case MiInt32: v[k] = ReadInt32(b, o, little); break;
                    case MiUint32: v[k] = ReadUInt32(b, o, little); break;
                    case MiInt64: v[k] = ReadInt64Bits(b, o, little); break;
                    case MiUint64: v[k] = (ulong)ReadInt64Bits(b, o, little); break;
                }
            }
            return v;
        }

        private static int ElementSize(int type)
        {
            switch (type)
            {
                case MiInt8: case MiUint8: return 1;
                case MiInt16: case MiUint16: return 2;
                case MiInt32: case MiUint32: case MiSingle: return 4;
                case MiDouble: case MiInt64: case MiUint64: return 8;
                default: return 0;
            }
        }

        /// <summary>
        /// Read an element tag at <paramref name="p"/>, handling both the normal 8-byte tag and the
        /// compact "small element" tag (type/size packed into the first 4 bytes, data in the next 4).
        /// </summary>
        private static void ReadTag(byte[] b, int p, bool little, out int type, out int nbytes, out int dataOffset, out int headerLen)
        {
            uint first = ReadUInt32(b, p, little);
            if ((first >> 16) != 0)
            {
                // Small element format: upper 16 bits = byte count, lower 16 bits = type.
                type = (int)(first & 0xFFFF);
                nbytes = (int)(first >> 16);
                dataOffset = p + 4;
                headerLen = 4;
            }
            else
            {
                type = (int)first;
                nbytes = (int)ReadUInt32(b, p + 4, little);
                dataOffset = p + 8;
                headerLen = 8;
            }
        }

        /// <summary>Inflate a zlib stream (2-byte header + deflate + Adler-32) into its raw bytes.</summary>
        private static byte[] Inflate(byte[] b, int offset, int length)
        {
            // Skip the 2-byte zlib header; DeflateStream reads the raw deflate payload.
            using (var ms = new MemoryStream(b, offset + 2, length - 2))
            using (var ds = new DeflateStream(ms, CompressionMode.Decompress))
            using (var outMs = new MemoryStream())
            {
                ds.CopyTo(outMs);
                return outMs.ToArray();
            }
        }

        private static int Pad8(int n) => (n + 7) & ~7;

        private static ushort ReadUInt16(byte[] b, int o, bool little) =>
            little ? (ushort)(b[o] | (b[o + 1] << 8)) : (ushort)((b[o] << 8) | b[o + 1]);

        private static int ReadInt32(byte[] b, int o, bool little) => (int)ReadUInt32(b, o, little);

        private static uint ReadUInt32(byte[] b, int o, bool little) => little
            ? (uint)(b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24))
            : (uint)((b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3]);

        private static long ReadInt64Bits(byte[] b, int o, bool little)
        {
            long lo = ReadUInt32(b, o, little);
            long hi = ReadUInt32(b, o + 4, little);
            return little ? (lo | (hi << 32)) : ((lo << 32) | hi);
        }

        private static float ReadSingle(byte[] b, int o, bool little)
        {
            byte[] tmp = { b[o], b[o + 1], b[o + 2], b[o + 3] };
            if (little != BitConverter.IsLittleEndian) Array.Reverse(tmp);
            return BitConverter.ToSingle(tmp, 0);
        }
    }
}
