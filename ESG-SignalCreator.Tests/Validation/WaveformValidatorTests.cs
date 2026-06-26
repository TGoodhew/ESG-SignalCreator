using System;
using EsgSignalCreator.Capability;
using EsgSignalCreator.Model;
using EsgSignalCreator.Validation;
using Xunit;

namespace EsgSignalCreator.Tests.Validation
{
    public class WaveformValidatorTests
    {
        private static readonly InstrumentProfile E4438C = InstrumentProfiles.Load("E4438C");

        /// <summary>Build a small constant-magnitude I/Q waveform of the requested length.</summary>
        private static WaveformModel MakeWaveform(int length, double sampleRateHz = 10e6, float level = 0.5f)
        {
            var i = new float[length];
            var q = new float[length];
            for (int n = 0; n < length; n++)
            {
                i[n] = level;
                q[n] = 0f;
            }
            return new WaveformModel(i, q, sampleRateHz);
        }

        [Fact]
        public void Clean_4096_sample_waveform_has_no_errors()
        {
            var wf = MakeWaveform(4096);

            var results = WaveformValidator.Validate(
                wf, E4438C, sampleClockHz: 10e6, carrierFrequencyHz: 1e9);

            Assert.False(WaveformValidator.HasErrors(results));
        }

        [Fact]
        public void Too_few_samples_reports_minimum_error()
        {
            var wf = MakeWaveform(50);

            var results = WaveformValidator.Validate(
                wf, E4438C, sampleClockHz: 10e6, carrierFrequencyHz: 1e9);

            Assert.True(WaveformValidator.HasErrors(results));
            var err = Assert.Single(results, r =>
                r.Severity == ValidationSeverity.Error && r.Field == "min-sample");
            Assert.Contains("at least", err.Message);
            Assert.Contains("60", err.Message);
        }

        [Fact]
        public void Odd_sample_count_reports_warning()
        {
            var wf = MakeWaveform(4097);

            var results = WaveformValidator.Validate(
                wf, E4438C, sampleClockHz: 10e6, carrierFrequencyHz: 1e9);

            Assert.Contains(results, r =>
                r.Severity == ValidationSeverity.Warning && r.Field == "even-count preference");
        }

        [Fact]
        public void Sample_count_over_memory_cap_reports_error_without_allocating()
        {
            // 70 million samples exceeds the E4438C's largest option (67,018,496).
            // Use the count-based overload so nothing is allocated.
            var results = WaveformValidator.Validate(
                sampleCount: 70_000_000,
                peakMagnitude: 0.5,
                profile: E4438C,
                sampleClockHz: 10e6,
                carrierFrequencyHz: 1e9);

            Assert.True(WaveformValidator.HasErrors(results));
            var err = Assert.Single(results, r =>
                r.Severity == ValidationSeverity.Error && r.Field == "memory cap");
            Assert.Contains(E4438C.MaxSamples.ToString(), err.Message);
        }

        [Fact]
        public void Sample_clock_over_maximum_reports_error()
        {
            var wf = MakeWaveform(4096);

            var results = WaveformValidator.Validate(
                wf, E4438C, sampleClockHz: 200e6, carrierFrequencyHz: 1e9);

            Assert.Contains(results, r =>
                r.Severity == ValidationSeverity.Error && r.Field == "sample clock over max");
        }

        [Fact]
        public void Non_positive_sample_clock_reports_error()
        {
            var wf = MakeWaveform(4096);

            var results = WaveformValidator.Validate(
                wf, E4438C, sampleClockHz: 0, carrierFrequencyHz: 1e9);

            Assert.Contains(results, r =>
                r.Severity == ValidationSeverity.Error && r.Field == "sample clock");
        }

        [Fact]
        public void Carrier_above_maximum_reports_error()
        {
            var wf = MakeWaveform(4096);

            var results = WaveformValidator.Validate(
                wf, E4438C, sampleClockHz: 10e6, carrierFrequencyHz: 50e9);

            Assert.Contains(results, r =>
                r.Severity == ValidationSeverity.Error && r.Field == "carrier frequency");
        }

        [Fact]
        public void Carrier_below_minimum_reports_error()
        {
            var wf = MakeWaveform(4096);

            var results = WaveformValidator.Validate(
                wf, E4438C, sampleClockHz: 10e6, carrierFrequencyHz: 1e3);

            Assert.Contains(results, r =>
                r.Severity == ValidationSeverity.Error && r.Field == "carrier frequency");
        }

        [Fact]
        public void Excessive_backoff_reports_overrange_warning()
        {
            var wf = MakeWaveform(4096);

            var results = WaveformValidator.Validate(
                wf, E4438C, sampleClockHz: 10e6, carrierFrequencyHz: 1e9, backoff: 0.99);

            Assert.Contains(results, r =>
                r.Severity == ValidationSeverity.Warning && r.Field == "backoff");
        }

        [Fact]
        public void Empty_signal_reports_info()
        {
            var wf = MakeWaveform(4096, level: 0f); // all-zero I/Q

            var results = WaveformValidator.Validate(
                wf, E4438C, sampleClockHz: 10e6, carrierFrequencyHz: 1e9);

            Assert.Contains(results, r =>
                r.Severity == ValidationSeverity.Info && r.Field == "level");
        }

        [Fact]
        public void Null_profile_skips_profile_dependent_checks_but_keeps_min_check()
        {
            // No profile: carrier / clock-max / memory checks skipped, but the absolute
            // minimum (60) still applies, and basic clock positivity still applies.
            var resultsTooFew = WaveformValidator.Validate(
                sampleCount: 10, peakMagnitude: 0.5, profile: null,
                sampleClockHz: 10e6, carrierFrequencyHz: 50e9);

            Assert.Contains(resultsTooFew, r =>
                r.Severity == ValidationSeverity.Error && r.Field == "min-sample");
            // 50 GHz carrier produces no error when profile is null.
            Assert.DoesNotContain(resultsTooFew, r => r.Field == "carrier frequency");
        }

        [Fact]
        public void Validate_with_null_waveform_throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                WaveformValidator.Validate(
                    (WaveformModel)null, E4438C, 10e6, 1e9));
        }

        [Fact]
        public void HasErrors_is_false_for_warning_only_and_null_lists()
        {
            var warnOnly = new[]
            {
                new ValidationResult(ValidationSeverity.Warning, "w"),
                new ValidationResult(ValidationSeverity.Info, "i"),
            };

            Assert.False(WaveformValidator.HasErrors(warnOnly));
            Assert.False(WaveformValidator.HasErrors(null));
        }
    }
}
