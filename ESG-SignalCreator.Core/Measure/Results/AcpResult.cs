namespace EsgSignalCreator.Measure.Results
{
    /// <summary>
    /// Typed result of an E4406A Adjacent Channel Power (<c>ACP</c> / ACPR) measurement: the absolute
    /// carrier power plus the relative (dBc) and absolute (dBm) powers of the lower (negative) and upper
    /// (positive) offset channels. <see cref="MeasurementResult.Raw"/> still carries the parsed scalar
    /// payload for audit.
    /// </summary>
    public sealed class AcpResult : MeasurementResult
    {
        /// <summary>Center (carrier) channel absolute power, in dBm (Basic-mode scalar value 1).</summary>
        public double CenterPowerDbm { get; set; }

        /// <summary>
        /// Relative power of each lower (negative) offset channel, in dBc, ordered offset 1..5.
        /// These are the negative-offset relative-power scalars (values 3, 7, 11, 15, 19).
        /// </summary>
        public double[] LowerOffsetsDbc { get; set; }

        /// <summary>
        /// Relative power of each upper (positive) offset channel, in dBc, ordered offset 1..5.
        /// These are the positive-offset relative-power scalars (values 5, 9, 13, 17, 21).
        /// </summary>
        public double[] UpperOffsetsDbc { get; set; }

        /// <summary>
        /// Absolute power of each lower (negative) offset channel, in dBm, ordered offset 1..5
        /// (values 4, 8, 12, 16, 20).
        /// </summary>
        public double[] LowerOffsetsDbm { get; set; }

        /// <summary>
        /// Absolute power of each upper (positive) offset channel, in dBm, ordered offset 1..5
        /// (values 6, 10, 14, 18, 22).
        /// </summary>
        public double[] UpperOffsetsDbm { get; set; }
    }
}
