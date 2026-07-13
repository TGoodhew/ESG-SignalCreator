using System;
using EsgSignalCreator.Measure.Results;
using EsgSignalCreator.Visa;

namespace EsgSignalCreator.Measure
{
    /// <summary>
    /// E4406A Waveform (time-domain) measurement (GitHub issue #70). Puts the analyzer into Basic
    /// single-measurement mode, tunes the center frequency, then reads the <c>WAVeform</c> scalar
    /// result set and maps it to a <see cref="WaveformResult"/>.
    /// </summary>
    /// <remarks>
    /// The <c>:READ:WAVeform?</c> scalar ordering differs by model, so the peak / mean / peak-to-mean
    /// positions come from the dialect (<see cref="IVsaDialect.WaveformScalars"/>):
    /// <list type="bullet">
    /// <item><description>E4406A: [peak, mean, mean-avg, aux, peak-to-mean] (bench-validated).</description></item>
    /// <item><description>N9010A: [sample-time, mean, mean-avg, num-samples, peak-to-mean, max] — peak is
    /// the Maximum at index 5 (IQ Analyzer Mode Reference).</description></item>
    /// </list>
    /// Waveform is time-domain, so the center frequency is the only tuning parameter (no frequency span).
    /// A short/empty response yields <see cref="double.NaN"/> for the missing field(s) rather than throwing.
    /// </remarks>
    public static class WaveformMeasurement
    {
        /// <summary>SCPI measurement root for the Waveform (time-domain) measurement.</summary>
        private const string Root = "WAVeform";

        /// <summary>
        /// Perform a Waveform (time-domain) measurement.
        /// </summary>
        /// <param name="vsa">Open analyzer facade.</param>
        /// <param name="centerHz">Measurement center frequency, in hertz.</param>
        /// <returns>
        /// A <see cref="WaveformResult"/> with peak power (result 1), mean power (result 2) and
        /// peak-to-mean ratio (result 5) mapped from the scalar set; <see cref="MeasurementResult.Raw"/>
        /// keeps the full parsed payload.
        /// </returns>
        public static WaveformResult Measure(VsaInstrument vsa, double centerHz)
        {
            if (vsa == null) throw new ArgumentNullException(nameof(vsa));

            var basic = new BasicMeasurement(vsa);
            basic.Setup(VsaMeasurement.Waveform, centerHz);

            double[] scalars = basic.Read(Root);
            WaveformScalarLayout layout = vsa.Dialect.WaveformScalars;

            return new WaveformResult
            {
                Measurement = Root,
                Raw = scalars,
                PeakPowerDbm = At(scalars, layout.PeakIndex),
                MeanPowerDbm = At(scalars, layout.MeanIndex),
                PeakToMeanDb = At(scalars, layout.PeakToMeanIndex)
            };
        }

        /// <summary>Scalar at <paramref name="index"/>, or NaN when the response is too short.</summary>
        private static double At(double[] scalars, int index) =>
            scalars != null && index >= 0 && index < scalars.Length ? scalars[index] : double.NaN;
    }
}
