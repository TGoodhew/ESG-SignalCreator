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
    /// </remarks>
    public static class Acp
    {
        /// <summary>Carrier reference integration bandwidth command (valid on Basic-mode ACP).</summary>
        private const string IntegrationBwCommand = ":SENSe:ACP:BANDwidth:INTegration";

        /// <summary>SCPI measurement root for Adjacent Channel Power.</summary>
        private const string Root = "ACP";

        /// <summary>Number of offset channels per side the Basic-mode scalar set carries.</summary>
        private const int MaxOffsets = 5;

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

            if (carrierBandwidthHz > 0)
                vsa.Write(IntegrationBwCommand + " " + carrierBandwidthHz.ToString("G17", CultureInfo.InvariantCulture) + " Hz");

            double[] s = basic.Read(Root);

            var lowerDbc = new List<double>();
            var lowerDbm = new List<double>();
            var upperDbc = new List<double>();
            var upperDbm = new List<double>();

            // Offsets 1..5 begin at index 4, four values each (neg rel, neg abs, pos rel, pos abs).
            for (int k = 0; k < MaxOffsets; k++)
            {
                int negRel = 4 + 4 * k, negAbs = 5 + 4 * k, posRel = 6 + 4 * k, posAbs = 7 + 4 * k;
                if (negRel < s.Length) lowerDbc.Add(s[negRel]);
                if (negAbs < s.Length) lowerDbm.Add(s[negAbs]);
                if (posRel < s.Length) upperDbc.Add(s[posRel]);
                if (posAbs < s.Length) upperDbm.Add(s[posAbs]);
            }

            return new AcpResult
            {
                Measurement = Root,
                Raw = s,
                UpperAdjacentDbc = s.Length > 0 ? s[0] : double.NaN,
                LowerAdjacentDbc = s.Length > 2 ? s[2] : double.NaN,
                LowerOffsetsDbc = lowerDbc.ToArray(),
                LowerOffsetsDbm = lowerDbm.ToArray(),
                UpperOffsetsDbc = upperDbc.ToArray(),
                UpperOffsetsDbm = upperDbm.ToArray()
            };
        }
    }
}
