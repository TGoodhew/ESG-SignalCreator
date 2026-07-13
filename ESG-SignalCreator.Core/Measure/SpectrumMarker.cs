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
    /// Marker SCPI confirmed against a real E4406A (FW A.08.10) from the Programmer's Guide
    /// "Using Markers" example: markers are numbered and must be assigned to a trace first —
    /// <c>:CALCulate:SPECtrum:MARKer1:TRACe ASP</c> (average spectrum) → <c>:MARKer1:MAXimum</c>
    /// (peak search) → <c>:MARKer1:X?</c> (absolute frequency, Hz) / <c>:MARKer1:Y?</c> (dBm).
    /// Mirrors the established 8563E MKPK → MKF?/MKA? workflow.
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
            basic.Setup(VsaMeasurement.Spectrum, centerHz);

            // Span is per-measurement on the E4406A (no global :SENSe:FREQuency:SPAN).
            if (spanHz > 0)
                vsa.Write(":SENSe:SPECtrum:FREQuency:SPAN " +
                          spanHz.ToString("G17", System.Globalization.CultureInfo.InvariantCulture) + " Hz");

            // Acquire the spectrum (initiate + return) so the marker has live trace data to search.
            double[] raw = basic.Read("SPECtrum");

            // Assign marker 1 to the average spectrum trace, peak-search, then read X (Hz) / Y (dBm).
            vsa.Write(":CALCulate:SPECtrum:MARKer1:TRACe ASP");
            vsa.Write(":CALCulate:SPECtrum:MARKer1:MAXimum");

            double freq = FirstOrNaN(VsaScalarParser.ParseScalars(vsa.Query(":CALCulate:SPECtrum:MARKer1:X?")));
            double amp = FirstOrNaN(VsaScalarParser.ParseScalars(vsa.Query(":CALCulate:SPECtrum:MARKer1:Y?")));

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
