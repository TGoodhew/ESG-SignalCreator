using System;
using System.Collections.Generic;
using EsgSignalCreator.Arb;
using EsgSignalCreator.Capability;
using EsgSignalCreator.Model;

namespace EsgSignalCreator.Validation
{
    /// <summary>
    /// The live dependency / validation checker (issue #52). Run at "Calculate" time, before any
    /// hardware contact, it predicts the failures that would otherwise only surface on the
    /// instrument: too few samples, odd / non-granular counts, memory-cap overflow against the
    /// connected baseband option, sample-clock and carrier-frequency limits, and DAC over-range
    /// given the digital backoff. Findings are returned as a flat, inline-friendly list of
    /// <see cref="ValidationResult"/>s; <see cref="HasErrors"/> tells the caller whether download
    /// must be blocked.
    /// </summary>
    public static class WaveformValidator
    {
        /// <summary>Floor on segment length when a profile is missing or omits a minimum.</summary>
        public const int AbsoluteMinSamples = EsgArbEncoder.MinSamples; // 60

        /// <summary>
        /// Validate a generated <see cref="WaveformModel"/> against an instrument profile and the
        /// requested play settings. <paramref name="profile"/> may be null (profile-dependent
        /// checks are then skipped).
        /// </summary>
        public static IReadOnlyList<ValidationResult> Validate(
            WaveformModel waveform,
            InstrumentProfile profile,
            double sampleClockHz,
            double carrierFrequencyHz,
            double backoff = EsgArbEncoder.DefaultBackoff)
        {
            if (waveform == null) throw new ArgumentNullException(nameof(waveform));
            return Validate(
                waveform.Length,
                waveform.PeakMagnitude(),
                profile,
                sampleClockHz,
                carrierFrequencyHz,
                backoff);
        }

        /// <summary>
        /// Validate from an explicit sample count and peak magnitude rather than a materialized
        /// waveform. This is the workhorse overload: it lets callers (and tests) check enormous
        /// sample counts against the memory cap without ever allocating the samples.
        /// </summary>
        public static IReadOnlyList<ValidationResult> Validate(
            int sampleCount,
            double peakMagnitude,
            InstrumentProfile profile,
            double sampleClockHz,
            double carrierFrequencyHz,
            double backoff = EsgArbEncoder.DefaultBackoff)
        {
            var results = new List<ValidationResult>();

            ValidateSampleCount(sampleCount, profile, results);
            ValidateSampleClock(sampleClockHz, profile, results);
            ValidateCarrierFrequency(carrierFrequencyHz, profile, results);
            ValidateLevels(peakMagnitude, backoff, results);

            return results;
        }

        /// <summary>True when any finding in <paramref name="results"/> is an Error.</summary>
        public static bool HasErrors(IEnumerable<ValidationResult> results)
        {
            if (results == null) return false;
            foreach (var r in results)
                if (r != null && r.Severity == ValidationSeverity.Error)
                    return true;
            return false;
        }

        private static void ValidateSampleCount(
            int sampleCount, InstrumentProfile profile, List<ValidationResult> results)
        {
            int min = profile != null && profile.MinSamples > 0
                ? profile.MinSamples
                : AbsoluteMinSamples;

            if (sampleCount < min)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Error,
                    "Waveform has " + sampleCount + " samples; the instrument requires at least " +
                    min + ".",
                    "min-sample"));
            }

            // Even-count preference: the ARB plays I,Q pairs and several modes assume an even length.
            if ((sampleCount & 1) != 0)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Warning,
                    "Sample count " + sampleCount + " is odd; an even sample count is preferred.",
                    "even-count preference"));
            }
            else if ((sampleCount & 3) != 0)
            {
                // Light, informational granularity hint: multiples of 4 sit best with the
                // interpolators. Only emitted when already even (so we don't double up on odd).
                results.Add(new ValidationResult(
                    ValidationSeverity.Info,
                    "Sample count " + sampleCount + " is not a multiple of 4; a multiple of 4 is " +
                    "marginally preferred for granularity.",
                    "granularity"));
            }

            if (profile != null)
            {
                long cap = profile.MaxSamples;
                if (cap > 0 && sampleCount > cap)
                {
                    results.Add(new ValidationResult(
                        ValidationSeverity.Error,
                        "Waveform has " + sampleCount + " samples; exceeds the installed baseband " +
                        "memory capacity of " + cap + " samples.",
                        "memory cap"));
                }
            }
        }

        private static void ValidateSampleClock(
            double sampleClockHz, InstrumentProfile profile, List<ValidationResult> results)
        {
            if (sampleClockHz <= 0)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Error,
                    "Sample clock must be positive; got " + sampleClockHz + " Hz.",
                    "sample clock"));
                return;
            }

            if (profile != null && profile.MaxSampleClockHz > 0 &&
                sampleClockHz > profile.MaxSampleClockHz)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Error,
                    "Sample clock " + sampleClockHz + " Hz exceeds the instrument maximum of " +
                    profile.MaxSampleClockHz + " Hz.",
                    "sample clock over max"));
            }
        }

        private static void ValidateCarrierFrequency(
            double carrierFrequencyHz, InstrumentProfile profile, List<ValidationResult> results)
        {
            if (profile == null) return;

            if (profile.MinFrequencyHz > 0 && carrierFrequencyHz < profile.MinFrequencyHz)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Error,
                    "Carrier frequency " + carrierFrequencyHz + " Hz is below the instrument " +
                    "minimum of " + profile.MinFrequencyHz + " Hz.",
                    "carrier frequency"));
            }
            else if (profile.MaxFrequencyHz > 0 && carrierFrequencyHz > profile.MaxFrequencyHz)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Error,
                    "Carrier frequency " + carrierFrequencyHz + " Hz is above the instrument " +
                    "maximum of " + profile.MaxFrequencyHz + " Hz.",
                    "carrier frequency"));
            }
        }

        private static void ValidateLevels(
            double peakMagnitude, double backoff, List<ValidationResult> results)
        {
            if (peakMagnitude <= 0)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Info,
                    "Waveform peak magnitude is zero (empty / silent signal).",
                    "level"));
                return;
            }

            // Interpolation-aware DAC over-range heuristic: the encoder scales so that
            // peak * backoff maps to full scale. A backoff above the default 0.95 leaves little
            // headroom, so interpolation overshoot can clip the DAC even though every input
            // sample is in range. Suggest lowering the scaling or using instrument RSCaling.
            if (backoff > EsgArbEncoder.DefaultBackoff)
            {
                results.Add(new ValidationResult(
                    ValidationSeverity.Warning,
                    "Digital backoff " + backoff + " leaves little DAC headroom (> " +
                    EsgArbEncoder.DefaultBackoff + "); interpolation may over-range. Consider " +
                    "lowering the scaling or using :RADio:ARB:RSCaling.",
                    "backoff"));
            }
        }
    }
}
