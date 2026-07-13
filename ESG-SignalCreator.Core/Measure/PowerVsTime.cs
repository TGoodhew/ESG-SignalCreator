using System;
using EsgSignalCreator.Measure.Results;
using EsgSignalCreator.Visa;

namespace EsgSignalCreator.Measure
{
    /// <summary>
    /// E4406A Power-vs-Time measurement (#74). Acquires the Basic-mode time-domain trace, derives the
    /// power envelope, and (optionally) evaluates it against a <see cref="PowerMask"/> for burst/frame
    /// pass-fail.
    /// </summary>
    /// <remarks>
    /// The Basic-mode <c>:READ:WAVeform0?</c> trace is corrected time-domain I/Q. Rather than rely on an
    /// absolute volts→dBm conversion (which depends on the analyzer's reference-level scaling), the
    /// envelope shape is <b>anchored</b> to the trusted scalar peak power: the raw |I/Q|² peak is mapped
    /// to the measured peak-power scalar and every other sample is offset from there. This gives a
    /// shape-correct, peak-accurate power-vs-time without absolute-calibration guesswork. The scalar set
    /// is taken with <c>:FETCh:WAVeform?</c> so it comes from the same acquisition as the trace.
    /// The trace-index 0 convention should be confirmed against the specific E4406A firmware.
    /// </remarks>
    public static class PowerVsTime
    {
        private const string Root = "WAVeform";
        private const string TraceQuery = ":READ:WAVeform0?";

        /// <summary>
        /// Acquire the power envelope at <paramref name="centerHz"/>. <paramref name="sampleIntervalSeconds"/>
        /// sets the time axis spacing; pass <see cref="double.NaN"/> to index samples 0,1,2,…. When
        /// <paramref name="mask"/> is supplied the result carries its verdict.
        /// </summary>
        public static PowerVsTimeResult Measure(
            VsaInstrument vsa, double centerHz, double sampleIntervalSeconds = double.NaN, PowerMask mask = null)
        {
            if (vsa == null) throw new ArgumentNullException(nameof(vsa));

            var basic = new BasicMeasurement(vsa);
            basic.Setup(VsaMeasurement.Waveform, centerHz);

            double[] trace = VsaScalarParser.ParseScalars(vsa.Query(TraceQuery)); // I,Q interleaved
            double[] scalars = basic.Fetch(Root);                                 // same acquisition

            double peakDbm = scalars.Length > 0 ? scalars[0] : double.NaN;
            double meanDbm = scalars.Length > 1 ? scalars[1] : double.NaN;

            int n = trace.Length / 2;
            var times = new double[n];
            var power = new double[n];
            var mag2 = new double[n];
            double peakMag2 = 0;

            for (int k = 0; k < n; k++)
            {
                double i = trace[2 * k];
                double q = trace[2 * k + 1];
                double m = i * i + q * q;
                mag2[k] = m;
                if (m > peakMag2) peakMag2 = m;
            }

            bool anchor = peakMag2 > 0 && !double.IsNaN(peakDbm);
            for (int k = 0; k < n; k++)
            {
                times[k] = double.IsNaN(sampleIntervalSeconds) ? k : k * sampleIntervalSeconds;
                power[k] = (anchor && mag2[k] > 0)
                    ? peakDbm + 10.0 * Math.Log10(mag2[k] / peakMag2)
                    : double.NaN;
            }

            var result = new PowerVsTimeResult
            {
                Measurement = Root,
                Raw = scalars,
                TimeSeconds = times,
                PowerDbm = power,
                PeakPowerDbm = peakDbm,
                MeanPowerDbm = meanDbm
            };
            if (mask != null) result.Mask = mask.Evaluate(times, power);
            return result;
        }
    }
}
