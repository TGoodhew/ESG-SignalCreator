using System;

namespace EsgSignalCreator.Waveform
{
    /// <summary>
    /// A baseband I/Q waveform: paired in-phase and quadrature sample arrays plus the sample
    /// (clock) rate they are played back at. Values are unitless; the instrument's RF power
    /// setting determines the absolute output level.
    /// </summary>
    public sealed class IqWaveform
    {
        public IqWaveform(double[] i, double[] q, double sampleRateHz)
        {
            if (i == null) throw new ArgumentNullException(nameof(i));
            if (q == null) throw new ArgumentNullException(nameof(q));
            if (i.Length != q.Length) throw new ArgumentException("I and Q must have equal length.");
            if (i.Length == 0) throw new ArgumentException("Waveform is empty.");

            I = i;
            Q = q;
            SampleRateHz = sampleRateHz;
        }

        public double[] I { get; }
        public double[] Q { get; }
        public double SampleRateHz { get; }
        public int Length => I.Length;

        /// <summary>Largest instantaneous vector magnitude sqrt(I²+Q²) across the waveform.</summary>
        public double PeakMagnitude()
        {
            double peak = 0;
            for (int n = 0; n < I.Length; n++)
            {
                double m = Math.Sqrt(I[n] * I[n] + Q[n] * Q[n]);
                if (m > peak) peak = m;
            }
            return peak;
        }

        /// <summary>
        /// Quantize to interleaved 16-bit signed samples (I0,Q0,I1,Q1,…) as the E4438C ARB expects.
        /// The waveform is scaled so its peak vector magnitude maps to <paramref name="fullScale"/>,
        /// which guarantees no sample exceeds the DAC range (avoids DAC over-range / clipping).
        /// </summary>
        public short[] ToInterleavedShorts(int fullScale = 32767)
        {
            double peak = PeakMagnitude();
            double scale = peak > 0 ? fullScale / peak : 0;

            var samples = new short[I.Length * 2];
            for (int n = 0; n < I.Length; n++)
            {
                samples[2 * n] = Saturate(I[n] * scale);
                samples[2 * n + 1] = Saturate(Q[n] * scale);
            }
            return samples;
        }

        /// <summary>
        /// Pack the waveform into the big-endian (MSB-first) interleaved byte payload that follows
        /// the IEEE-488.2 block header in a <c>:MEMory:DATA</c> download.
        /// </summary>
        public byte[] ToArbPayload(int fullScale = 32767)
        {
            short[] s = ToInterleavedShorts(fullScale);
            var bytes = new byte[s.Length * 2];
            for (int k = 0; k < s.Length; k++)
            {
                ushort u = unchecked((ushort)s[k]);
                bytes[2 * k] = (byte)(u >> 8);     // most-significant byte first
                bytes[2 * k + 1] = (byte)(u & 0xFF);
            }
            return bytes;
        }

        private static short Saturate(double v)
        {
            long r = (long)Math.Round(v, MidpointRounding.AwayFromZero);
            if (r > 32767) r = 32767;
            if (r < -32768) r = -32768;
            return (short)r;
        }
    }
}
