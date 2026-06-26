using System;

namespace EsgSignalCreator.Model
{
    /// <summary>
    /// The neutral output of every signal personality: normalized baseband I/Q (roughly [-1, +1]),
    /// an optional per-sample marker stream, and the sample (clock) rate. Everything downstream —
    /// scaling, int16 conversion, byte order, IEEE-488.2 block framing, transport — is shared and
    /// operates only on this model (see <see cref="EsgSignalCreator.Arb.EsgArbEncoder"/>).
    /// </summary>
    public sealed class WaveformModel
    {
        public WaveformModel(float[] i, float[] q, double sampleRateHz, string name = null, byte[] markers = null)
        {
            if (i == null) throw new ArgumentNullException(nameof(i));
            if (q == null) throw new ArgumentNullException(nameof(q));
            if (i.Length != q.Length) throw new ArgumentException("I and Q must have equal length.");
            if (i.Length == 0) throw new ArgumentException("Waveform is empty.");
            if (markers != null && markers.Length != i.Length)
                throw new ArgumentException("Markers must be one byte per sample, or null.");
            if (sampleRateHz <= 0) throw new ArgumentException("Sample rate must be positive.", nameof(sampleRateHz));

            I = i;
            Q = q;
            SampleRateHz = sampleRateHz;
            Name = name;
            Markers = markers;
        }

        /// <summary>In-phase samples, normalized to roughly [-1, +1].</summary>
        public float[] I { get; }

        /// <summary>Quadrature samples, normalized to roughly [-1, +1].</summary>
        public float[] Q { get; }

        /// <summary>Optional marker bits, one byte per sample (length == <see cref="Length"/>), or null.</summary>
        public byte[] Markers { get; }

        /// <summary>I/Q sample (playback clock) rate, in hertz.</summary>
        public double SampleRateHz { get; }

        /// <summary>Segment name; becomes <c>WFM1:&lt;Name&gt;</c> on download.</summary>
        public string Name { get; set; }

        /// <summary>Number of complex samples.</summary>
        public int Length => I.Length;

        /// <summary>Largest instantaneous vector magnitude sqrt(I²+Q²) across the waveform.</summary>
        public double PeakMagnitude()
        {
            double peak = 0;
            for (int n = 0; n < I.Length; n++)
            {
                double m = Math.Sqrt((double)I[n] * I[n] + (double)Q[n] * Q[n]);
                if (m > peak) peak = m;
            }
            return peak;
        }
    }
}
