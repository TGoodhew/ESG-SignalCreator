namespace EsgSignalCreator.Measure.Results
{
    /// <summary>
    /// Typed result of an E4406A Channel Power (<c>CHPower</c>) measurement: the total integrated
    /// power in the channel and the equivalent power spectral density. <see cref="MeasurementResult.Raw"/>
    /// still carries the parsed scalar payload for audit.
    /// </summary>
    public sealed class ChannelPowerResult : MeasurementResult
    {
        /// <summary>Total power integrated over the channel bandwidth, in dBm.</summary>
        public double TotalPowerDbm { get; set; }

        /// <summary>Power spectral density of the channel, in dBm/Hz.</summary>
        public double PowerSpectralDensityDbmHz { get; set; }
    }
}
