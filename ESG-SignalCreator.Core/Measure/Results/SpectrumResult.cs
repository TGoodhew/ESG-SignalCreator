namespace EsgSignalCreator.Measure.Results
{
    /// <summary>
    /// Typed result of a Basic-mode Spectrum measurement with a marker peak search:
    /// the marker frequency (Hz) and amplitude (dBm), plus the optional occupied bandwidth (Hz).
    /// </summary>
    public sealed class SpectrumResult : MeasurementResult
    {
        /// <summary>Marker X value: frequency of the located peak, in Hz.</summary>
        public double MarkerFrequencyHz { get; set; }

        /// <summary>Marker Y value: amplitude of the located peak, in dBm.</summary>
        public double MarkerPowerDbm { get; set; }

        /// <summary>Occupied bandwidth in Hz, when measured; otherwise <see cref="double.NaN"/>.</summary>
        public double OccupiedBandwidthHz { get; set; } = double.NaN;
    }
}
