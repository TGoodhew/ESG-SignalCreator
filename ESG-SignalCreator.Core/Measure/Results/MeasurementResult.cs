namespace EsgSignalCreator.Measure.Results
{
    /// <summary>Base for a typed E4406A measurement result; keeps the raw scalar payload for audit.</summary>
    public abstract class MeasurementResult
    {
        /// <summary>SCPI measurement root this result came from (e.g. "CHPower").</summary>
        public string Measurement { get; set; }

        /// <summary>The raw scalar values returned by the instrument, as parsed.</summary>
        public double[] Raw { get; set; }
    }
}
