namespace EsgSignalCreator.Measure.Results
{
    /// <summary>
    /// E4406A Power-vs-Time result (#74): the power envelope over time plus the burst-level scalars,
    /// and — when a mask was supplied — its pass/fail verdict.
    /// </summary>
    public sealed class PowerVsTimeResult : MeasurementResult
    {
        /// <summary>
        /// Sample time axis, in seconds (or sample index when the sample interval is unknown).
        /// Same length as <see cref="PowerDbm"/>.
        /// </summary>
        public double[] TimeSeconds { get; set; }

        /// <summary>
        /// Power envelope, in dBm, anchored so its peak equals the analyzer's measured peak power.
        /// Samples with no energy are <see cref="double.NaN"/>.
        /// </summary>
        public double[] PowerDbm { get; set; }

        /// <summary>Peak power over the acquisition, in dBm (from the Waveform scalar set).</summary>
        public double PeakPowerDbm { get; set; }

        /// <summary>Mean power over the acquisition, in dBm (from the Waveform scalar set).</summary>
        public double MeanPowerDbm { get; set; }

        /// <summary>Mask verdict, or null when no mask was evaluated.</summary>
        public Measure.PowerMaskResult Mask { get; set; }
    }
}
