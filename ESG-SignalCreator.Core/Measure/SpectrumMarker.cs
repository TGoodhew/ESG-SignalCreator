using System;
using EsgSignalCreator.Measure.Results;
using EsgSignalCreator.Visa;

namespace EsgSignalCreator.Measure
{
    /// <summary>
    /// Basic-mode Spectrum measurement with a marker peak search (GitHub issue #63).
    /// Acquires a spectrum, runs a marker MAXimum (peak) search, and reads the marker X/Y values.
    /// </summary>
    /// <remarks>
    /// The exact marker SCPI nodes/roots (<c>:CALCulate:SPECtrum:MARKer:*</c>) are to be confirmed
    /// against the unit (see §5.4/§10 of the requirements doc). This mirrors the established
    /// 8563E marker workflow (MKPK HI &#8594; MKF? / MKA?): peak-search, then query frequency and amplitude.
    /// </remarks>
    public static class SpectrumMarker
    {
        /// <summary>
        /// Set up Basic mode at <paramref name="centerHz"/>/<paramref name="spanHz"/>, acquire a spectrum,
        /// perform a marker peak search, and return the marker frequency (Hz) and amplitude (dBm).
        /// Empty marker responses yield <see cref="double.NaN"/> rather than throwing.
        /// </summary>
        public static SpectrumResult MeasurePeak(VsaInstrument vsa, double centerHz, double spanHz)
        {
            if (vsa == null) throw new ArgumentNullException(nameof(vsa));

            var basic = new BasicMeasurement(vsa);
            basic.Setup(centerHz, spanHz);

            // Acquire the spectrum (initiate + return) so the marker has live trace data to search.
            double[] raw = basic.Read("SPECtrum");

            // Marker peak (maximum) search, then read marker X (frequency) and Y (amplitude).
            vsa.Write(":CALCulate:SPECtrum:MARKer:MAXimum");

            double freq = FirstOrNaN(VsaScalarParser.ParseScalars(vsa.Query(":CALCulate:SPECtrum:MARKer:X?")));
            double amp = FirstOrNaN(VsaScalarParser.ParseScalars(vsa.Query(":CALCulate:SPECtrum:MARKer:Y?")));

            return new SpectrumResult
            {
                Measurement = "SPECtrum",
                Raw = raw,
                MarkerFrequencyHz = freq,
                MarkerPowerDbm = amp
            };
        }

        private static double FirstOrNaN(double[] values) =>
            values != null && values.Length > 0 ? values[0] : double.NaN;
    }
}
