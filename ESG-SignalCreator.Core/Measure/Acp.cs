using System;
using System.Collections.Generic;
using System.Globalization;
using EsgSignalCreator.Measure.Results;
using EsgSignalCreator.Visa;

namespace EsgSignalCreator.Measure
{
    /// <summary>
    /// E4406A Basic-mode Adjacent Channel Power (<c>ACP</c> / ACPR) measurement (GitHub issue #69).
    /// Sets up Basic single-measurement mode at the requested center, optionally constrains the carrier
    /// integration bandwidth, then reads the <c>ACP</c> scalar set and maps it to an <see cref="AcpResult"/>.
    /// </summary>
    /// <remarks>
    /// Hardware-truthed on a real E4406A (FW A.08.10): ACP has <b>no</b> settable <c>:SENSe:ACP:FREQuency:SPAN</c>
    /// (that emits -113); the span is coupled from the carrier/offset setup. The carrier reference
    /// bandwidth is set with <c>:SENSe:ACP:BANDwidth:INTegration</c>. The Basic-mode (Total-power
    /// reference) scalar set is 24 values: [0] upper-adjacent rel (dB), [1] upper-adjacent abs (dBm),
    /// [2] lower-adjacent rel, [3] lower-adjacent abs, then offsets 1..5 as (neg rel, neg abs, pos rel,
    /// pos abs) at [4..23]. Inactive channels report ~ -999. A short response yields fewer entries.
    /// <para>
    /// The N9010A uses the SCPI short-form root <c>ACP</c> and, in its default SA config (Radio Std = None,
    /// one carrier, offset A on), <c>:READ:ACP?</c> returns 3 scalars — [0] reference carrier power (dBm),
    /// [1] lower-adjacent (dBc), [2] upper-adjacent (dBc) — per the SA Mode Reference (9018-06099, p.1586);
    /// bench-confirmed on firmware A.07.05. The root, offset count and adjacent-channel positions come from
    /// the dialect (<see cref="IVsaDialect.AcpScalars"/>), so the same code drives both models. (Enabling
    /// more offsets switches the instrument to the 28-value Total-power-reference table — header
    /// [0, total, 0, ref] then 6 offsets x (Lrel, Labs, Urel, Uabs) from index 4.) Note A.07.05 rejects the
    /// long-form result verbs <c>:READ/FETCh/MEASure:ACPower?</c> with -113; the short form is portable.
    /// </para>
    /// </remarks>
    public static class Acp
    {
        /// <summary>Carrier reference integration bandwidth command (valid on Basic-mode ACP).</summary>
        private const string IntegrationBwCommand = ":SENSe:ACP:BANDwidth:INTegration";

        /// <summary>
        /// Perform an Adjacent Channel Power measurement using the analyzer's offset definitions.
        /// </summary>
        /// <param name="vsa">Open analyzer facade.</param>
        /// <param name="centerHz">Carrier center frequency, in hertz.</param>
        /// <param name="carrierBandwidthHz">
        /// Carrier reference (integration) bandwidth, in hertz. When &gt; 0 it is written via
        /// <c>:SENSe:ACP:BANDwidth:INTegration</c> before the read; 0 leaves the analyzer's setting.
        /// </param>
        public static AcpResult Measure(VsaInstrument vsa, double centerHz, double carrierBandwidthHz = 0)
        {
            if (vsa == null) throw new ArgumentNullException(nameof(vsa));

            var basic = new BasicMeasurement(vsa);
            basic.Setup(VsaMeasurement.Acp, centerHz);

            // Force a deterministic offset configuration where the model needs it (the N9010A result
            // format is offset-count-dependent), so the scalar layout in the dialect always applies.
            string setup = vsa.Dialect.AcpSetupCommand;
            if (!string.IsNullOrEmpty(setup)) vsa.Write(setup);

            if (carrierBandwidthHz > 0)
                vsa.Write(IntegrationBwCommand + " " + carrierBandwidthHz.ToString("G17", CultureInfo.InvariantCulture) + " Hz");

            string root = vsa.Dialect.RootFor(VsaMeasurement.Acp); // E4406A "ACP" / N9010A "ACPower"
            AcpScalarLayout layout = vsa.Dialect.AcpScalars;
            double[] s = basic.Read(root);

            var lowerDbc = new List<double>();
            var lowerDbm = new List<double>();
            var upperDbc = new List<double>();
            var upperDbm = new List<double>();

            // Each offset is four values from the base index: (lowerRel, lowerAbs, upperRel, upperAbs).
            for (int k = 0; k < layout.OffsetCount; k++)
            {
                int b = layout.OffsetBaseIndex + 4 * k;
                if (b < s.Length) lowerDbc.Add(s[b]);
                if (b + 1 < s.Length) lowerDbm.Add(s[b + 1]);
                if (b + 2 < s.Length) upperDbc.Add(s[b + 2]);
                if (b + 3 < s.Length) upperDbm.Add(s[b + 3]);
            }

            return new AcpResult
            {
                Measurement = root,
                Raw = s,
                UpperAdjacentDbc = At(s, layout.UpperAdjacentDbcIndex),
                LowerAdjacentDbc = At(s, layout.LowerAdjacentDbcIndex),
                LowerOffsetsDbc = lowerDbc.ToArray(),
                LowerOffsetsDbm = lowerDbm.ToArray(),
                UpperOffsetsDbc = upperDbc.ToArray(),
                UpperOffsetsDbm = upperDbm.ToArray()
            };
        }

        /// <summary>Scalar at <paramref name="index"/>, or NaN when the response is too short.</summary>
        private static double At(double[] s, int index) =>
            s != null && index >= 0 && index < s.Length ? s[index] : double.NaN;
    }
}
