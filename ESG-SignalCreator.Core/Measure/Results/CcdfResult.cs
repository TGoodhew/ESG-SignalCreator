namespace EsgSignalCreator.Measure.Results
{
    /// <summary>
    /// Typed result of an E4406A Power Statistics CCDF (<c>PSTatistic</c>) measurement. The CCDF
    /// (Complementary Cumulative Distribution Function) characterizes a signal's peak-to-average
    /// power behavior; the headline figure is <see cref="PaprDb"/>, the crest factor (peak power
    /// above the average). <see cref="MeasurementResult.Raw"/> still carries the full parsed scalar
    /// payload for audit.
    /// </summary>
    public sealed class CcdfResult : MeasurementResult
    {
        /// <summary>Average input power of the signal, in dBm.</summary>
        public double AveragePowerDbm { get; set; }

        /// <summary>
        /// Peak-to-average power ratio (crest factor), in dB: the peak power measured above the
        /// average input power.
        /// </summary>
        public double PaprDb { get; set; }

        /// <summary>Probability, in percent, that the signal power is at or above the average input power.</summary>
        public double ProbabilityAtAveragePercent { get; set; }
    }
}
