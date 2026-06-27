namespace EsgSignalCreator.Measure.Results
{
    /// <summary>
    /// Typed result of an E4406A Basic-mode Adjacent Channel Power (<c>ACP</c> / ACPR) measurement:
    /// the upper/lower adjacent-channel powers and the lower (negative) / upper (positive) offset
    /// channel powers, both relative (dBc) and absolute (dBm). <see cref="MeasurementResult.Raw"/>
    /// still carries the parsed scalar payload for audit.
    /// </summary>
    /// <remarks>
    /// Basic-mode <c>:READ:ACP?</c> returns 24 scalars (Total-power reference): [0] upper-adjacent
    /// relative (dB), [1] upper-adjacent absolute (dBm), [2] lower-adjacent relative, [3] lower-adjacent
    /// absolute, then for offsets 1..5 the quartet (neg rel, neg abs, pos rel, pos abs) at [4..23].
    /// Unmeasured/inactive channels report a large-negative sentinel (~ -999).
    /// </remarks>
    public sealed class AcpResult : MeasurementResult
    {
        /// <summary>Upper adjacent channel relative power, in dBc (scalar value 1).</summary>
        public double UpperAdjacentDbc { get; set; }

        /// <summary>Lower adjacent channel relative power, in dBc (scalar value 3).</summary>
        public double LowerAdjacentDbc { get; set; }

        /// <summary>Relative power of each lower (negative) offset channel, in dBc, offsets 1..5.</summary>
        public double[] LowerOffsetsDbc { get; set; }

        /// <summary>Relative power of each upper (positive) offset channel, in dBc, offsets 1..5.</summary>
        public double[] UpperOffsetsDbc { get; set; }

        /// <summary>Absolute power of each lower (negative) offset channel, in dBm, offsets 1..5.</summary>
        public double[] LowerOffsetsDbm { get; set; }

        /// <summary>Absolute power of each upper (positive) offset channel, in dBm, offsets 1..5.</summary>
        public double[] UpperOffsetsDbm { get; set; }
    }
}
