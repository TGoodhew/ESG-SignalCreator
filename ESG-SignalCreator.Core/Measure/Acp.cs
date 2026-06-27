using System;
using System.Collections.Generic;
using System.Globalization;
using EsgSignalCreator.Measure.Results;
using EsgSignalCreator.Visa;

namespace EsgSignalCreator.Measure
{
    /// <summary>
    /// E4406A Adjacent Channel Power (<c>ACP</c> / ACPR) measurement (GitHub issue #69). Sets up Basic
    /// single-measurement mode at the requested center, optionally constrains the measurement span, then
    /// reads the <c>ACP</c> scalar result set and maps it to an <see cref="AcpResult"/>.
    /// </summary>
    public static class Acp
    {
        /// <summary>Per-measurement frequency span for ACP (the E4406A has no global span).</summary>
        private const string SpanCommand = ":SENSe:ACP:FREQuency:SPAN";

        /// <summary>SCPI measurement root for Adjacent Channel Power.</summary>
        private const string Root = "ACP";

        /// <summary>Maximum number of offset channels the Basic-mode scalar set carries (per side).</summary>
        private const int MaxOffsets = 5;

        /// <summary>
        /// Perform an Adjacent Channel Power measurement using the analyzer's default offset definitions.
        /// </summary>
        /// <param name="vsa">Open analyzer facade.</param>
        /// <param name="centerHz">Carrier center frequency, in hertz.</param>
        /// <param name="spanHz">
        /// Measurement span, in hertz. When &gt; 0 it is written via
        /// <c>:SENSe:ACP:FREQuency:SPAN</c> before the read; 0 leaves the analyzer's setting.
        /// </param>
        /// <remarks>
        /// Per the E4406A Programmer's Guide, the Basic-mode <c>ACP</c> scalar result set (n omitted) is
        /// 22 comma-separated values in this order:
        /// <list type="number">
        /// <item>Center frequency — absolute power (dBm)</item>
        /// <item>Center frequency — absolute power (W)</item>
        /// <item>Negative offset 1 — relative power (dB)</item>
        /// <item>Negative offset 1 — absolute power (dBm)</item>
        /// <item>Positive offset 1 — relative power (dB)</item>
        /// <item>Positive offset 1 — absolute power (dBm)</item>
        /// </list>
        /// then the (neg rel, neg abs, pos rel, pos abs) quartet repeats for offsets 2..5, ending at
        /// value 22. <see cref="AcpResult.CenterPowerDbm"/> is value 1; for offset k (0-based) the lower
        /// relative power is value <c>3 + 4k</c> and the upper relative power is value <c>5 + 4k</c>.
        /// A short response simply yields fewer offset entries (the missing offsets are skipped) rather
        /// than throwing. Custom offset configuration is a future extension; v1 uses defaults.
        /// </remarks>
        public static AcpResult Measure(VsaInstrument vsa, double centerHz, double spanHz)
        {
            if (vsa == null) throw new ArgumentNullException(nameof(vsa));

            var basic = new BasicMeasurement(vsa);
            basic.Setup(centerHz);

            if (spanHz > 0)
            {
                vsa.Write(SpanCommand + " " + spanHz.ToString("G17", CultureInfo.InvariantCulture) + " Hz");
            }

            double[] scalars = basic.Read(Root);

            var lowerDbc = new List<double>();
            var lowerDbm = new List<double>();
            var upperDbc = new List<double>();
            var upperDbm = new List<double>();

            for (int k = 0; k < MaxOffsets; k++)
            {
                int lowerRel = 2 + 4 * k; // values 3, 7, 11, 15, 19 (0-based indices)
                int lowerAbs = 3 + 4 * k; // values 4, 8, 12, 16, 20
                int upperRel = 4 + 4 * k; // values 5, 9, 13, 17, 21
                int upperAbs = 5 + 4 * k; // values 6, 10, 14, 18, 22

                if (lowerRel < scalars.Length) lowerDbc.Add(scalars[lowerRel]);
                if (lowerAbs < scalars.Length) lowerDbm.Add(scalars[lowerAbs]);
                if (upperRel < scalars.Length) upperDbc.Add(scalars[upperRel]);
                if (upperAbs < scalars.Length) upperDbm.Add(scalars[upperAbs]);
            }

            return new AcpResult
            {
                Measurement = Root,
                Raw = scalars,
                CenterPowerDbm = scalars.Length > 0 ? scalars[0] : double.NaN,
                LowerOffsetsDbc = lowerDbc.ToArray(),
                LowerOffsetsDbm = lowerDbm.ToArray(),
                UpperOffsetsDbc = upperDbc.ToArray(),
                UpperOffsetsDbm = upperDbm.ToArray()
            };
        }
    }
}
