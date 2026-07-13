using System.Linq;
using EsgSignalCreator.Verify;
using Xunit;

namespace EsgSignalCreator.Tests.Verify
{
    /// <summary>#130: failed checks map to actionable likely-cause + troubleshooting guidance.</summary>
    public class VerificationGuidanceTests
    {
        private static VerificationResult R(string metric, double expected, double measured, double tol) =>
            new VerificationResult(metric, expected, measured, tol, "");

        [Fact]
        public void Am_papr_too_high_reads_as_excessive_modulation()
        {
            VerificationGuidance g = VerificationGuidanceBook.For(R("AM · PAPR", 5.2, 9.1, 2.5));
            Assert.Contains("Excessive AM", g.Cause);
            Assert.NotEmpty(g.Suggestions);
            // Names both sides of the chain so the operator can bisect.
            Assert.Contains(g.Suggestions, x => x.Contains("E4438C") || x.Contains("analyzer"));
        }

        [Fact]
        public void Am_papr_too_low_reads_as_insufficient_modulation()
        {
            VerificationGuidance g = VerificationGuidanceBook.For(R("AM · PAPR", 5.2, 1.0, 2.5));
            Assert.Contains("Insufficient AM", g.Cause);
        }

        [Fact]
        public void Fm_papr_failure_notes_constant_envelope()
        {
            VerificationGuidance g = VerificationGuidanceBook.For(R("FM · PAPR", 0.2, 4.0, 2.5));
            Assert.Contains("constant envelope", g.Cause);
        }

        [Fact]
        public void Channel_power_failure_points_at_the_path_level()
        {
            VerificationGuidance g = VerificationGuidanceBook.For(R("CW · Channel power", -10.0, -16.0, 1.0));
            Assert.Contains("channel power", g.Cause.ToLowerInvariant());
            Assert.Contains(g.Suggestions, x => x.Contains("Path cal"));
        }

        [Fact]
        public void Tone_frequency_failure_points_at_the_reference()
        {
            VerificationGuidance g = VerificationGuidanceBook.For(R("CW · Tone frequency", 1.001e9, 1.0009e9, 5e4));
            Assert.Contains(g.Suggestions, x => x.Contains("10 MHz"));
        }

        [Fact]
        public void No_data_failure_suggests_connection_and_alignment_checks()
        {
            VerificationGuidance g = VerificationGuidanceBook.For(R("IQ (multitone) · PAPR", 8.0, double.NaN, 2.5));
            Assert.Contains("No measurement data", g.Cause);
            Assert.Contains(g.Suggestions, x => x.Contains("model toggle"));
        }

        [Fact]
        public void ForFailures_skips_passing_checks()
        {
            var results = new[]
            {
                R("CW · Channel power", -10.0, -10.1, 1.0), // pass
                R("AM · PAPR", 5.0, 9.0, 2.5),              // fail
            };
            var guidance = VerificationGuidanceBook.ForFailures(results);
            Assert.Single(guidance);
            Assert.Equal("AM · PAPR", guidance[0].Check);
        }
    }
}
