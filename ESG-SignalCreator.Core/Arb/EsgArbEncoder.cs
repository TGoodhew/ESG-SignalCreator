using System;
using EsgSignalCreator.Model;

namespace EsgSignalCreator.Arb
{
    /// <summary>
    /// Converts normalized baseband I/Q into the byte-exact payload the E4438C ARB expects:
    /// 16-bit signed two's-complement samples, interleaved I,Q,I,Q…, big-endian (MSB first).
    /// A digital backoff leaves DAC headroom so interpolation in the signal path does not
    /// over-range even though every individual sample is in range. The instrument-side
    /// <c>:RADio:ARB:RSCaling</c> is the second half of that strategy and is applied separately.
    /// </summary>
    public static class EsgArbEncoder
    {
        /// <summary>Minimum ARB segment length the E4438C accepts.</summary>
        public const int MinSamples = 60;

        /// <summary>Positive full-scale DAC code. Negative full scale is clamped symmetrically to -FullScale.</summary>
        public const int FullScale = 32767;

        /// <summary>Default digital backoff: peak vector magnitude maps to 95% of full scale.</summary>
        public const double DefaultBackoff = 0.95;

        /// <summary>
        /// Encode to the interleaved big-endian int16 payload (no block header). The waveform is
        /// scaled so its peak vector magnitude maps to <paramref name="backoff"/> × full scale,
        /// guaranteeing no sample exceeds the DAC range.
        /// </summary>
        public static byte[] EncodePayload(float[] i, float[] q, double backoff = DefaultBackoff)
        {
            if (i == null) throw new ArgumentNullException(nameof(i));
            if (q == null) throw new ArgumentNullException(nameof(q));
            if (i.Length != q.Length) throw new ArgumentException("I and Q must have equal length.");
            if (i.Length < MinSamples)
                throw new ArgumentException(
                    $"Waveform has {i.Length} samples; the E4438C requires at least {MinSamples}.");
            if (backoff <= 0 || backoff > 1)
                throw new ArgumentOutOfRangeException(nameof(backoff), "Backoff must be in (0, 1].");

            double peak = 0;
            for (int n = 0; n < i.Length; n++)
            {
                double m = Math.Sqrt((double)i[n] * i[n] + (double)q[n] * q[n]);
                if (m > peak) peak = m;
            }
            double scale = peak > 0 ? backoff * FullScale / peak : 0;

            var bytes = new byte[i.Length * 4]; // 2 bytes I + 2 bytes Q per sample
            int b = 0;
            for (int n = 0; n < i.Length; n++)
            {
                WriteBigEndian(bytes, ref b, Saturate(i[n] * scale));
                WriteBigEndian(bytes, ref b, Saturate(q[n] * scale));
            }
            return bytes;
        }

        /// <summary>Encode straight to a complete IEEE-488.2 definite-length block (header + payload).</summary>
        public static byte[] EncodeBlock(float[] i, float[] q, double backoff = DefaultBackoff)
        {
            return Ieee4882Block.Frame(EncodePayload(i, q, backoff));
        }

        /// <summary>
        /// Encode I and Q into <b>separate</b> big-endian int16 byte streams (not interleaved), for the
        /// legacy E443xB-compatible <c>ARBI:</c>/<c>ARBQ:</c> download path. Both channels share one
        /// scale factor (the joint peak vector magnitude) so the constellation is preserved.
        /// </summary>
        public static void EncodeChannelsSeparate(float[] i, float[] q, double backoff,
            out byte[] iBytes, out byte[] qBytes)
        {
            if (i == null) throw new ArgumentNullException(nameof(i));
            if (q == null) throw new ArgumentNullException(nameof(q));
            if (i.Length != q.Length) throw new ArgumentException("I and Q must have equal length.");
            if (i.Length < MinSamples)
                throw new ArgumentException($"Waveform has {i.Length} samples; the E4438C requires at least {MinSamples}.");
            if (backoff <= 0 || backoff > 1)
                throw new ArgumentOutOfRangeException(nameof(backoff), "Backoff must be in (0, 1].");

            double peak = 0;
            for (int n = 0; n < i.Length; n++)
            {
                double m = Math.Sqrt((double)i[n] * i[n] + (double)q[n] * q[n]);
                if (m > peak) peak = m;
            }
            double scale = peak > 0 ? backoff * FullScale / peak : 0;

            iBytes = new byte[i.Length * 2];
            qBytes = new byte[q.Length * 2];
            int bi = 0, bq = 0;
            for (int n = 0; n < i.Length; n++)
            {
                WriteBigEndian(iBytes, ref bi, Saturate(i[n] * scale));
                WriteBigEndian(qBytes, ref bq, Saturate(q[n] * scale));
            }
        }

        /// <summary>Encode a <see cref="WaveformModel"/> to the interleaved big-endian int16 payload.</summary>
        public static byte[] EncodePayload(WaveformModel waveform, double backoff = DefaultBackoff)
        {
            if (waveform == null) throw new ArgumentNullException(nameof(waveform));
            return EncodePayload(waveform.I, waveform.Q, backoff);
        }

        /// <summary>
        /// Double-precision overload, for callers holding <c>double[]</c> I/Q (e.g.
        /// <see cref="EsgSignalCreator.Waveform.IqWaveform"/>). Same scaling and byte layout.
        /// </summary>
        public static byte[] EncodePayload(double[] i, double[] q, double backoff = DefaultBackoff)
        {
            if (i == null) throw new ArgumentNullException(nameof(i));
            if (q == null) throw new ArgumentNullException(nameof(q));
            if (i.Length != q.Length) throw new ArgumentException("I and Q must have equal length.");
            if (i.Length < MinSamples)
                throw new ArgumentException(
                    $"Waveform has {i.Length} samples; the E4438C requires at least {MinSamples}.");
            if (backoff <= 0 || backoff > 1)
                throw new ArgumentOutOfRangeException(nameof(backoff), "Backoff must be in (0, 1].");

            double peak = 0;
            for (int n = 0; n < i.Length; n++)
            {
                double m = Math.Sqrt(i[n] * i[n] + q[n] * q[n]);
                if (m > peak) peak = m;
            }
            double scale = peak > 0 ? backoff * FullScale / peak : 0;

            var bytes = new byte[i.Length * 4];
            int b = 0;
            for (int n = 0; n < i.Length; n++)
            {
                WriteBigEndian(bytes, ref b, Saturate(i[n] * scale));
                WriteBigEndian(bytes, ref b, Saturate(q[n] * scale));
            }
            return bytes;
        }

        /// <summary>Double-precision overload producing a complete definite-length block.</summary>
        public static byte[] EncodeBlock(double[] i, double[] q, double backoff = DefaultBackoff)
        {
            return Ieee4882Block.Frame(EncodePayload(i, q, backoff));
        }

        private static void WriteBigEndian(byte[] buffer, ref int offset, short value)
        {
            ushort u = unchecked((ushort)value);
            buffer[offset++] = (byte)(u >> 8);     // most-significant byte first
            buffer[offset++] = (byte)(u & 0xFF);
        }

        private static short Saturate(double v)
        {
            long r = (long)Math.Round(v, MidpointRounding.AwayFromZero);
            if (r > FullScale) r = FullScale;
            if (r < -FullScale) r = -FullScale; // symmetric clamp: avoid the lone -32768 code
            return (short)r;
        }
    }
}
