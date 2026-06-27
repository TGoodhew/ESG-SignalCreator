using System;
using System.Collections.Generic;
using EsgSignalCreator.Measure;
using EsgSignalCreator.Measure.Results;
using EsgSignalCreator.Model;
using EsgSignalCreator.Visa;

namespace EsgSignalCreator.Verify
{
    /// <summary>
    /// In-app closed-loop verification (#67): given a generated <see cref="WaveformModel"/> and the
    /// commanded carrier/power, measure the played signal on a <see cref="VsaInstrument"/> and compare
    /// measured-vs-expected within the profile's tolerances. Signal-agnostic: verifies channel power
    /// and PAPR for any waveform; an optional tone offset adds a spectrum-peak frequency check.
    /// </summary>
    public static class VerificationHarness
    {
        /// <summary>
        /// Measure the signal at <paramref name="carrierHz"/> and compare to expectations.
        /// </summary>
        /// <param name="vsa">Connected analyzer.</param>
        /// <param name="waveform">The generated baseband I/Q (used to compute the expected PAPR).</param>
        /// <param name="carrierHz">Commanded carrier frequency, in hertz.</param>
        /// <param name="commandedPowerDbm">Commanded ESG output power, in dBm.</param>
        /// <param name="profile">Tolerances + path loss + measurement span.</param>
        /// <param name="toneOffsetHz">
        /// If non-zero, also verify a spectrum peak at <c>carrier + offset</c> (e.g. a CW tone).
        /// </param>
        public static IReadOnlyList<VerificationResult> Verify(
            VsaInstrument vsa, WaveformModel waveform, double carrierHz, double commandedPowerDbm,
            VerificationProfile profile, double toneOffsetHz = 0)
        {
            if (vsa == null) throw new ArgumentNullException(nameof(vsa));
            if (waveform == null) throw new ArgumentNullException(nameof(waveform));
            profile = profile ?? new VerificationProfile();

            double span = profile.MeasurementSpanHz > 0 ? profile.MeasurementSpanHz : 5e6;
            var results = new List<VerificationResult>();

            // Channel power vs commanded level (less declared path loss).
            ChannelPowerResult cp = ChannelPower.Measure(vsa, carrierHz, span, span);
            results.Add(new VerificationResult("Channel power",
                commandedPowerDbm - profile.PathLossDb, cp.TotalPowerDbm, profile.PowerToleranceDb, "dBm"));

            // PAPR: measured crest factor vs the value computed from the generated I/Q.
            double expectedPapr = Dsp.Ccdf.PaprDb(ToDouble(waveform.I), ToDouble(waveform.Q));
            CcdfResult ccdf = Ccdf.Measure(vsa, carrierHz);
            results.Add(new VerificationResult("PAPR", expectedPapr, ccdf.PaprDb, profile.PaprToleranceDb, "dB"));

            // Optional tone-placement check (CW / single-tone).
            if (Math.Abs(toneOffsetHz) > 0)
            {
                SpectrumResult sp = SpectrumMarker.MeasurePeak(vsa, carrierHz, span);
                results.Add(new VerificationResult("Tone frequency",
                    carrierHz + toneOffsetHz, sp.MarkerFrequencyHz, profile.FrequencyToleranceHz, "Hz"));
            }

            return results;
        }

        /// <summary>True when every result passes.</summary>
        public static bool AllPass(IEnumerable<VerificationResult> results)
        {
            foreach (VerificationResult r in results) if (!r.Pass) return false;
            return true;
        }

        private static double[] ToDouble(float[] x)
        {
            var d = new double[x.Length];
            for (int n = 0; n < x.Length; n++) d[n] = x[n];
            return d;
        }
    }
}
