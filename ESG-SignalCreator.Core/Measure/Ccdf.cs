using System;
using System.Globalization;
using EsgSignalCreator.Measure.Results;
using EsgSignalCreator.Visa;

namespace EsgSignalCreator.Measure
{
    /// <summary>
    /// E4406A Power Statistics CCDF measurement (GitHub issue #68). Sets up Basic single-measurement
    /// mode at the requested center frequency, then reads the <c>PSTatistic</c> scalar result set and
    /// maps it to a <see cref="CcdfResult"/>. The CCDF has no frequency span, so only the center is
    /// tuned (via <see cref="BasicMeasurement.Setup"/>); no span command is sent.
    /// </summary>
    public static class Ccdf
    {
        /// <summary>SCPI measurement root for the Power Statistics CCDF measurement.</summary>
        private const string Root = "PSTatistic";

        /// <summary>
        /// Perform a Power Statistics CCDF measurement.
        /// </summary>
        /// <param name="vsa">Open analyzer facade.</param>
        /// <param name="centerHz">Signal center frequency, in hertz.</param>
        /// <param name="counts">
        /// Sample count at which the measurement stops (<c>:SENSe:PSTatistic:COUNts</c>). The factory
        /// default is 10,000,000, which makes a single READ take far too long; this defaults to
        /// 1,000,000 (valid range 1,000 … 2,000,000,000) for a fast, accurate-enough CCDF.
        /// </param>
        /// <remarks>
        /// Per the E4406A Programmer's Guide (confirmed), the <c>PSTatistic</c> scalar result set is
        /// 10 comma-separated values, in order:
        /// <list type="number">
        /// <item><description>Average input power (dBm)</description></item>
        /// <item><description>Probability at the average input power (%)</description></item>
        /// <item><description>Power level with 10% of power (dB)</description></item>
        /// <item><description>...1% (dB)</description></item>
        /// <item><description>...0.1% (dB)</description></item>
        /// <item><description>...0.01% (dB)</description></item>
        /// <item><description>...0.001% (dB)</description></item>
        /// <item><description>...0.0001% (dB)</description></item>
        /// <item><description>Peak power (dB) — the PAPR / crest factor (peak above average)</description></item>
        /// <item><description>Count</description></item>
        /// </list>
        /// A short/empty response yields <see cref="double.NaN"/> for the missing field(s) rather than
        /// throwing.
        /// </remarks>
        public static CcdfResult Measure(VsaInstrument vsa, double centerHz, long counts = 1_000_000)
        {
            if (vsa == null) throw new ArgumentNullException(nameof(vsa));

            var basic = new BasicMeasurement(vsa);
            basic.Setup(VsaMeasurement.Ccdf, centerHz);

            // Bound the sample count so READ:PSTatistic? returns promptly (factory default is 10,000,000).
            if (counts > 0)
                vsa.Write(":SENSe:PSTatistic:COUNts " + counts.ToString(CultureInfo.InvariantCulture));

            // The scalar set lives at a model-specific result index (E4406A n=1; N9010A n=2 — n=0/1 are
            // trace data there). The within-set ordering is the same, so the field mapping below is shared.
            double[] data = basic.Read(Root, vsa.Dialect.CcdfScalarResultIndex);

            if (vsa.Dialect.CcdfResultIsTrace)
            {
                // The read returned the 5001-point CCDF trace (probability % vs dB-above-average, in
                // 0.01 dB steps), not the 10 scalars. PAPR is the highest dB-above-average level the
                // signal still reaches; the average power isn't in the trace (use Channel Power for that).
                return new CcdfResult
                {
                    Measurement = Root,
                    Raw = data,
                    AveragePowerDbm = double.NaN,
                    ProbabilityAtAveragePercent = double.NaN,
                    PaprDb = PaprFromCcdfTrace(data)
                };
            }

            return new CcdfResult
            {
                Measurement = Root,
                Raw = data,
                AveragePowerDbm = data.Length > 0 ? data[0] : double.NaN,
                ProbabilityAtAveragePercent = data.Length > 1 ? data[1] : double.NaN,
                PaprDb = data.Length > 8 ? data[8] : double.NaN
            };
        }

        /// <summary>
        /// PAPR from a CCDF probability trace: probability(%) that the instantaneous power is at least
        /// <c>i·step</c> dB above average, where the trace spans 0…50 dB. PAPR is the highest level still
        /// reached, i.e. the largest index whose probability stays above the analyzer's ~0.0001% floor.
        /// </summary>
        private static double PaprFromCcdfTrace(double[] trace)
        {
            if (trace == null || trace.Length < 2) return double.NaN;
            const double maxDb = 50.0;            // 5001 points span 0..50 dB above average
            const double floorPercent = 0.0001;   // the CCDF measurement floor
            double stepDb = maxDb / (trace.Length - 1);
            for (int i = trace.Length - 1; i >= 0; i--)
                if (trace[i] >= floorPercent) return i * stepDb;
            return 0.0;
        }
    }
}
