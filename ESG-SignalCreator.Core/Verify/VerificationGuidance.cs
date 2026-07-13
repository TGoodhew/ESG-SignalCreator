using System;
using System.Collections.Generic;

namespace EsgSignalCreator.Verify
{
    /// <summary>Troubleshooting guidance for one failed verification check (issue #130).</summary>
    public sealed class VerificationGuidance
    {
        public VerificationGuidance(string check, string cause, string[] suggestions)
        {
            Check = check;
            Cause = cause;
            Suggestions = suggestions ?? new string[0];
        }

        /// <summary>The failed check's label (e.g. "AM · PAPR").</summary>
        public string Check { get; }

        /// <summary>One-line likely cause.</summary>
        public string Cause { get; }

        /// <summary>Ordered troubleshooting steps.</summary>
        public string[] Suggestions { get; }
    }

    /// <summary>
    /// Maps a failed <see cref="VerificationResult"/> to likely causes + troubleshooting suggestions
    /// (issue #130), keyed by signal, metric, and the direction of the deviation. Pure/testable; a UI
    /// dialog renders the output. Where relevant it names both possibilities — the ESG generating a poor
    /// signal vs the analyzer not reading correctly — so the operator can bisect the chain.
    /// </summary>
    public static class VerificationGuidanceBook
    {
        /// <summary>Guidance for each failing result (passes are skipped).</summary>
        public static IReadOnlyList<VerificationGuidance> ForFailures(IEnumerable<VerificationResult> results)
        {
            var list = new List<VerificationGuidance>();
            if (results == null) return list;
            foreach (VerificationResult r in results)
                if (r != null && !r.Pass) list.Add(For(r));
            return list;
        }

        /// <summary>Guidance for a single (typically failed) result.</summary>
        public static VerificationGuidance For(VerificationResult r)
        {
            if (r == null) throw new ArgumentNullException(nameof(r));

            SplitLabel(r.Metric, out string signal, out string metric);
            string m = (metric ?? string.Empty).ToLowerInvariant();
            string s = (signal ?? string.Empty).ToLowerInvariant();
            bool high = r.Measured > r.Expected;

            // No data at all — the step never really measured.
            if (double.IsNaN(r.Measured))
                return G(r.Metric,
                    "No measurement data — the step didn't run or returned nothing.",
                    "Confirm the ESG and VSA are connected and the RF path is armed.",
                    "Check the VSA model toggle matches the connected instrument.",
                    "Allow any in-progress auto-alignment to finish, then retry.",
                    "Read the analyzer's :SYSTem:ERRor? queue for a rejected command.");

            if (m.Contains("papr"))
            {
                if (s.Contains("am"))
                    return high
                        ? G(r.Metric,
                            "Excessive AM — measured crest factor is higher than expected.",
                            "The E4438C may be generating a distorted/over-driven signal, or the analyzer may be mis-reading it.",
                            "Lower the ESG output level and check the ALC; confirm the ARB scaling isn't clipping.",
                            "Confirm the AM depth is what you intended (over-modulation raises PAPR).",
                            "Check the analyzer input range/attenuation isn't compressing the peaks.",
                            "Re-run Path cal… and re-check cabling/attenuators.")
                        : G(r.Metric,
                            "Insufficient AM — measured crest factor is lower than expected.",
                            "The AM depth may not have reached the ARB, or the analyzer is averaging away the peaks.",
                            "Confirm the AM depth setting and that modulation is enabled.",
                            "Check the analyzer isn't in an averaging mode that flattens the crest.");
                if (s.Contains("fm"))
                    return G(r.Metric,
                        "FM crest factor is off — an FM signal should be ~0 dB PAPR (constant envelope).",
                        "A non-constant envelope suggests the deviation/rate are wrong, or the analyzer is compressing.",
                        "Confirm the FM deviation and rate; a clipped/scaled ARB breaks the constant envelope.",
                        "Check the analyzer input range/attenuation.");
                return G(r.Metric,
                    "PAPR / crest factor differs from the value computed from the generated I/Q.",
                    "Either the ESG isn't reproducing the waveform faithfully (scaling/clipping) or the analyzer is mis-reading it.",
                    "Check the ARB runtime scaling isn't clipping; lower it if the peak is over-range.",
                    "Confirm the analyzer input range/attenuation and averaging settings.");
            }

            if (m.Contains("channel power"))
                return G(r.Metric,
                    high ? "Measured channel power is higher than commanded (level too high)."
                         : "Measured channel power is lower than commanded (level too low / excess loss).",
                    "The discrepancy is usually in the path level, not the signal: cabling, pads/attenuators, ESG level accuracy, or analyzer amplitude accuracy.",
                    "Re-run Path cal… so the declared path loss matches the real cable/pad.",
                    "Check the cable and any inline attenuator; confirm the ESG amplitude and the analyzer input range.",
                    "Verify nothing else is driving or loading the RF path.");

            if (m.Contains("tone frequency") || m.Contains("frequency"))
                return G(r.Metric,
                    "Measured tone frequency is off from carrier + offset.",
                    "Most often a reference/timebase issue rather than a real error.",
                    "Lock the ESG and analyzer to a common 10 MHz (Reference menu).",
                    "Confirm the ESG carrier frequency and the intended tone offset.",
                    "Narrow the span / RBW so the peak search resolves the tone precisely.");

            // Generic fallback.
            return G(r.Metric,
                "Measured value is outside tolerance.",
                "Bisect the chain: confirm the ESG is generating the intended signal, then that the analyzer reads it correctly.",
                "Re-run Path cal… and re-check connections, then retry the self-test.");
        }

        private static VerificationGuidance G(string check, string cause, params string[] suggestions) =>
            new VerificationGuidance(check, cause, suggestions);

        private static void SplitLabel(string label, out string signal, out string metric)
        {
            signal = string.Empty;
            metric = label ?? string.Empty;
            if (string.IsNullOrEmpty(label)) return;
            int i = label.IndexOf(" · ", StringComparison.Ordinal);
            if (i >= 0)
            {
                signal = label.Substring(0, i);
                metric = label.Substring(i + 3);
            }
        }
    }
}
