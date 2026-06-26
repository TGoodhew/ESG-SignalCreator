using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using EsgSignalCreator.Model;

namespace EsgSignalCreator.Io
{
    /// <summary>
    /// Loads baseband I/Q from external files into a <see cref="WaveformModel"/>. Format is
    /// auto-detected from the file extension, with an optional explicit override. Supported:
    /// <list type="bullet">
    ///   <item>CSV/TSV/TXT — two numeric columns (I,Q) per line; comma/tab/whitespace delimited;
    ///         an optional non-numeric header line is skipped; blank lines tolerated.</item>
    ///   <item>Raw interleaved little-endian int16 (.bin/.iq) — I,Q,I,Q…, scaled by 1/32768.</item>
    ///   <item>16-bit PCM WAV (.wav) — stereo maps ch0→I, ch1→Q; mono maps samples→I, Q=0.
    ///         Sample rate is taken from the <c>fmt </c> chunk.</item>
    /// </list>
    /// MAT files are recognized but not supported in P1.
    /// </summary>
    public static class IqFileReader
    {
        /// <summary>Recognized container formats.</summary>
        public enum IqFormat
        {
            /// <summary>Pick the format from the file extension.</summary>
            Auto = 0,
            /// <summary>Delimited text, two columns I,Q.</summary>
            DelimitedText,
            /// <summary>Raw interleaved little-endian signed 16-bit.</summary>
            RawInt16,
            /// <summary>RIFF/WAVE 16-bit PCM.</summary>
            Wav,
            /// <summary>MATLAB .mat (not supported in P1).</summary>
            Mat
        }

        /// <summary>
        /// Read a file at <paramref name="path"/> into a normalized <see cref="WaveformModel"/>.
        /// </summary>
        /// <param name="path">Source file.</param>
        /// <param name="sampleRateHzOverride">
        /// Sample rate to use for formats that carry no rate (CSV/TSV/raw int16). For WAV the header
        /// rate is used unless this is positive, in which case it overrides the header.
        /// </param>
        /// <param name="swapIq">When true, swap the I and Q channels after reading.</param>
        /// <param name="scale">Multiplier applied to every sample (default 1.0).</param>
        /// <param name="format">Explicit format, or <see cref="IqFormat.Auto"/> to detect by extension.</param>
        public static WaveformModel Read(
            string path,
            double sampleRateHzOverride,
            bool swapIq = false,
            double scale = 1.0,
            IqFormat format = IqFormat.Auto)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (!File.Exists(path)) throw new FileNotFoundException("I/Q file not found.", path);

            IqFormat fmt = format == IqFormat.Auto ? DetectFormat(path) : format;

            float[] i;
            float[] q;
            double sampleRateHz;

            switch (fmt)
            {
                case IqFormat.DelimitedText:
                    ReadDelimitedText(path, out i, out q);
                    sampleRateHz = RequirePositiveRate(sampleRateHzOverride, "CSV/TSV files carry no sample rate");
                    break;

                case IqFormat.RawInt16:
                    ReadRawInt16(path, out i, out q);
                    sampleRateHz = RequirePositiveRate(sampleRateHzOverride, "Raw int16 files carry no sample rate");
                    break;

                case IqFormat.Wav:
                    double headerRate;
                    ReadWav(path, out i, out q, out headerRate);
                    sampleRateHz = sampleRateHzOverride > 0 ? sampleRateHzOverride : headerRate;
                    break;

                case IqFormat.Mat:
                    throw new NotSupportedException(
                        "MATLAB .mat import is not supported in P1. Export the I/Q as CSV, WAV, or raw interleaved int16 instead.");

                default:
                    throw new NotSupportedException("Unrecognized I/Q file format for '" + path + "'.");
            }

            if (i.Length == 0)
                throw new InvalidDataException("No I/Q samples were found in '" + path + "'.");

            if (scale != 1.0)
            {
                for (int n = 0; n < i.Length; n++)
                {
                    i[n] = (float)(i[n] * scale);
                    q[n] = (float)(q[n] * scale);
                }
            }

            if (swapIq)
            {
                float[] tmp = i;
                i = q;
                q = tmp;
            }

            return new WaveformModel(i, q, sampleRateHz, Path.GetFileNameWithoutExtension(path));
        }

        /// <summary>Map a file extension to a container format.</summary>
        public static IqFormat DetectFormat(string path)
        {
            string ext = (Path.GetExtension(path) ?? string.Empty).ToLowerInvariant();
            switch (ext)
            {
                case ".csv":
                case ".tsv":
                case ".txt":
                    return IqFormat.DelimitedText;
                case ".bin":
                case ".iq":
                    return IqFormat.RawInt16;
                case ".wav":
                    return IqFormat.Wav;
                case ".mat":
                    return IqFormat.Mat;
                default:
                    throw new NotSupportedException(
                        "Cannot auto-detect I/Q format from extension '" + ext + "'. Pass an explicit format.");
            }
        }

        private static double RequirePositiveRate(double rate, string why)
        {
            if (rate > 0) return rate;
            throw new ArgumentException(why + "; a positive sampleRateHzOverride is required.", nameof(rate));
        }

        // --- CSV / TSV ----------------------------------------------------------------

        private static readonly char[] FieldSeparators = { ',', '\t', ' ', ';' };

        private static void ReadDelimitedText(string path, out float[] i, out float[] q)
        {
            var ri = new List<float>();
            var rq = new List<float>();

            foreach (string raw in File.ReadLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;                 // blank line
                if (line[0] == '#' || line[0] == '%') continue; // comment line

                string[] fields = line.Split(FieldSeparators, StringSplitOptions.RemoveEmptyEntries);
                if (fields.Length < 2) continue;

                float fi, fq;
                if (!float.TryParse(fields[0], NumberStyles.Float, CultureInfo.InvariantCulture, out fi) ||
                    !float.TryParse(fields[1], NumberStyles.Float, CultureInfo.InvariantCulture, out fq))
                {
                    // Non-numeric row (e.g. a header). Skip if nothing parsed yet; otherwise it's bad data.
                    if (ri.Count == 0) continue;
                    throw new InvalidDataException("Non-numeric I/Q row encountered: '" + line + "'.");
                }

                ri.Add(fi);
                rq.Add(fq);
            }

            i = ri.ToArray();
            q = rq.ToArray();
        }

        // --- Raw interleaved int16 ----------------------------------------------------

        private static void ReadRawInt16(string path, out float[] i, out float[] q)
        {
            byte[] bytes = File.ReadAllBytes(path);
            int complete = bytes.Length / 4; // 2 int16 (I,Q) = 4 bytes per complex sample
            i = new float[complete];
            q = new float[complete];

            for (int n = 0; n < complete; n++)
            {
                int b = n * 4;
                short si = (short)(bytes[b] | (bytes[b + 1] << 8));         // little-endian
                short sq = (short)(bytes[b + 2] | (bytes[b + 3] << 8));
                i[n] = si / 32768f;
                q[n] = sq / 32768f;
            }
        }

        // --- WAV (RIFF / PCM) ---------------------------------------------------------

        private static void ReadWav(string path, out float[] i, out float[] q, out double sampleRateHz)
        {
            byte[] b = File.ReadAllBytes(path);

            if (b.Length < 12 ||
                b[0] != 'R' || b[1] != 'I' || b[2] != 'F' || b[3] != 'F' ||
                b[8] != 'W' || b[9] != 'A' || b[10] != 'V' || b[11] != 'E')
            {
                throw new InvalidDataException("Not a RIFF/WAVE file: '" + path + "'.");
            }

            int channels = 0;
            int bitsPerSample = 0;
            sampleRateHz = 0;
            int dataOffset = -1;
            int dataLength = 0;
            bool haveFmt = false;

            int pos = 12;
            while (pos + 8 <= b.Length)
            {
                string id = Encoding.ASCII.GetString(b, pos, 4);
                uint size = ReadUInt32Le(b, pos + 4);
                int body = pos + 8;

                if (id == "fmt ")
                {
                    if (body + 16 > b.Length)
                        throw new InvalidDataException("Truncated 'fmt ' chunk.");
                    int audioFormat = ReadUInt16Le(b, body);
                    channels = ReadUInt16Le(b, body + 2);
                    sampleRateHz = ReadUInt32Le(b, body + 4);
                    bitsPerSample = ReadUInt16Le(b, body + 14);
                    haveFmt = true;

                    // 1 == PCM, 0xFFFE == WAVE_FORMAT_EXTENSIBLE (still 16-bit PCM payload here)
                    if (audioFormat != 1 && audioFormat != 0xFFFE)
                        throw new NotSupportedException("Only PCM WAV is supported (audioFormat=" + audioFormat + ").");
                }
                else if (id == "data")
                {
                    dataOffset = body;
                    dataLength = (int)Math.Min(size, (uint)(b.Length - body));
                }

                // Chunks are word-aligned: an odd size is padded by one byte.
                long advance = 8 + size + (size & 1);
                pos += (int)advance;
            }

            if (!haveFmt) throw new InvalidDataException("WAV file is missing its 'fmt ' chunk.");
            if (dataOffset < 0) throw new InvalidDataException("WAV file is missing its 'data' chunk.");
            if (bitsPerSample != 16) throw new NotSupportedException("Only 16-bit PCM WAV is supported (got " + bitsPerSample + " bits).");
            if (channels < 1 || channels > 2) throw new NotSupportedException("Only mono or stereo WAV is supported (got " + channels + " channels).");

            int frameBytes = channels * 2;
            int frames = dataLength / frameBytes;
            i = new float[frames];
            q = new float[frames];

            for (int n = 0; n < frames; n++)
            {
                int f = dataOffset + n * frameBytes;
                short ch0 = (short)(b[f] | (b[f + 1] << 8));
                i[n] = ch0 / 32768f;
                if (channels == 2)
                {
                    short ch1 = (short)(b[f + 2] | (b[f + 3] << 8));
                    q[n] = ch1 / 32768f;
                }
                else
                {
                    q[n] = 0f;
                }
            }
        }

        private static int ReadUInt16Le(byte[] b, int o) => b[o] | (b[o + 1] << 8);

        private static uint ReadUInt32Le(byte[] b, int o) =>
            (uint)(b[o] | (b[o + 1] << 8) | (b[o + 2] << 16) | (b[o + 3] << 24));
    }
}
