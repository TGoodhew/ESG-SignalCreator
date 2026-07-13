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
    /// Per the E4406A Programmer's Guide the <c>:READ:WAVeform?</c> scalar result set (n omitted / n = 1)
    /// is comma-separated in this order:
    /// <list type="number">
    /// <item><description>Peak power (dBm)</description></item>
    /// <item><description>Mean power (dBm)</description></item>
    /// <item><description>Mean power, averaged (dBm)</description></item>
    /// <item><description>Peak power, averaged / aux (dBm)</description></item>
    /// <item><description>Peak-to-mean ratio (dB)</description></item>
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

            return new WaveformResult
            {
                Measurement = Root,
                Raw = scalars,
                PeakPowerDbm = scalars.Length > 0 ? scalars[0] : double.NaN,
                MeanPowerDbm = scalars.Length > 1 ? scalars[1] : double.NaN,
                PeakToMeanDb = scalars.Length > 4 ? scalars[4] : double.NaN
            };
        }
    }
}
