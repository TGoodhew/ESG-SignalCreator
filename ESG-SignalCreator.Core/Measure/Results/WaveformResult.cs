namespace EsgSignalCreator.Measure.Results
{
    /// <summary>
    /// Typed result of an E4406A Waveform (<c>WAVeform</c>) time-domain measurement: the peak and mean
    /// power in the captured trace and their ratio. <see cref="MeasurementResult.Raw"/> still carries the
    /// parsed scalar payload for audit.
    /// </summary>
    /// <remarks>
    /// Not to be confused with the signal-generator <c>EsgSignalCreator.Model.WaveformModel</c>; this is a
    /// VSA measurement result in the <c>EsgSignalCreator.Measure.Results</c> namespace.
    /// </remarks>
    public sealed class WaveformResult : MeasurementResult
    {
        /// <summary>Peak power of the captured time-domain trace, in dBm (scalar result 1).</summary>
        public double PeakPowerDbm { get; set; }

        /// <summary>Mean power of the captured time-domain trace, in dBm (scalar result 2).</summary>
        public double MeanPowerDbm { get; set; }

        /// <summary>Peak-to-mean ratio of the trace, in dB (scalar result 5).</summary>
        public double PeakToMeanDb { get; set; }
    }
}
