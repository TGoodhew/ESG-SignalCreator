using System;
using EsgSignalCreator.Model;

namespace EsgSignalCreator.Impairments
{
    /// <summary>
    /// Applies modulator I/Q impairments (gain imbalance, quadrature skew, DC offset, I/Q swap)
    /// to a <see cref="WaveformModel"/>. Used to emulate non-ideal modulator behaviour for test
    /// and characterization purposes.
    /// </summary>
    public static class IqImpairments
    {
        /// <summary>
        /// Returns a new <see cref="WaveformModel"/> (same length, sample rate, name, and markers)
        /// with the impairments in <paramref name="cfg"/> applied. The input is not modified.
        /// Order of operations: gain imbalance → quadrature skew → DC offset → I/Q swap.
        /// </summary>
        public static WaveformModel Apply(WaveformModel input, IqImpairmentConfig cfg)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));

            int n = input.Length;
            float[] outI = new float[n];
            float[] outQ = new float[n];

            // Gain imbalance: +half dB on I, -half dB on Q, so 0 dB is a no-op.
            double gI = Math.Pow(10.0, +cfg.GainImbalanceDb / 40.0);
            double gQ = Math.Pow(10.0, -cfg.GainImbalanceDb / 40.0);

            // Quadrature skew: rotate Q relative to I by the skew angle.
            double theta = cfg.QuadratureSkewDeg * Math.PI / 180.0;
            double sin = Math.Sin(theta);
            double cos = Math.Cos(theta);

            for (int k = 0; k < n; k++)
            {
                double i = input.I[k] * gI;
                double q = input.Q[k] * gQ;

                // I' = I, Q' = I·sin(θ) + Q·cos(θ)
                double qSkewed = i * sin + q * cos;

                // DC offset.
                i += cfg.DcOffsetI;
                qSkewed += cfg.DcOffsetQ;

                outI[k] = (float)i;
                outQ[k] = (float)qSkewed;
            }

            // I/Q swap (after the above).
            if (cfg.SwapIq)
            {
                float[] tmp = outI;
                outI = outQ;
                outQ = tmp;
            }

            byte[] markers = input.Markers == null ? null : (byte[])input.Markers.Clone();
            return new WaveformModel(outI, outQ, input.SampleRateHz, input.Name, markers);
        }
    }
}
