using EsgSignalCreator.Verify;
using EsgSignalCreator.Visa;
using Xunit;

namespace EsgSignalCreator.Tests.Verify
{
    public class PowerSafetyGateTests
    {
        // #109: the input-damage default is per model (the N9010A default is a lower, conservative backstop).
        [Theory]
        [InlineData(VsaModel.E4406A, 30.0, false)]
        [InlineData(VsaModel.N9010A, 25.0, true)]
        public void Input_limit_default_is_per_model(VsaModel model, double expected, bool conservative)
        {
            Assert.Equal(expected, AnalyzerInputLimits.DefaultMaxSafeInputDbm(model), 9);
            Assert.Equal(conservative, AnalyzerInputLimits.IsConservativeDefault(model));
        }

        [Fact]
        public void Gate_enforces_the_n9010a_limit()
        {
            // N9010A conservative limit 25 dBm, direct cable: an armed ESG at +30 dBm overdrives it.
            var cfg = new RfPathSafety
            {
                Armed = true,
                AnalyzerMaxSafeInputDbm = AnalyzerInputLimits.DefaultMaxSafeInputDbm(VsaModel.N9010A),
                PathLossDb = 0.0
            };

            Assert.False(PowerSafetyGate.IsSafe(30.0, cfg, out _));
            Assert.Throws<RfSafetyException>(() => PowerSafetyGate.Guard(30.0, cfg));
            // 10 dB pad brings +30 dBm to +20 dBm at the input -> under the 25 dBm limit -> safe.
            cfg.PathLossDb = 10.0;
            Assert.True(PowerSafetyGate.IsSafe(30.0, cfg, out _));
        }

        [Fact]
        public void Not_armed_any_power_is_safe()
        {
            var cfg = new RfPathSafety { Armed = false, AnalyzerMaxSafeInputDbm = 0.0, PathLossDb = 0.0 };

            string reason;
            bool safe = PowerSafetyGate.IsSafe(30.0, cfg, out reason);

            Assert.True(safe);
            Assert.Equal(string.Empty, reason);
            // Guard must not throw.
            PowerSafetyGate.Guard(30.0, cfg);
        }

        [Fact]
        public void Armed_above_limit_no_path_loss_is_unsafe_and_guard_throws()
        {
            var cfg = new RfPathSafety { Armed = true, AnalyzerMaxSafeInputDbm = 0.0, PathLossDb = 0.0 };

            string reason;
            bool safe = PowerSafetyGate.IsSafe(10.0, cfg, out reason);

            Assert.False(safe);
            Assert.False(string.IsNullOrEmpty(reason));
            // Reason should mention the safe limit.
            Assert.Contains("limit", reason);
            Assert.Contains("0", reason);

            var ex = Assert.Throws<RfSafetyException>(() => PowerSafetyGate.Guard(10.0, cfg));
            Assert.Equal(reason, ex.Message);
        }

        [Fact]
        public void Armed_path_loss_brings_input_under_limit_is_safe()
        {
            // ESG +20 dBm, path loss 30 dB, limit 0 dBm -> analyzer -10 dBm -> safe.
            var cfg = new RfPathSafety { Armed = true, AnalyzerMaxSafeInputDbm = 0.0, PathLossDb = 30.0 };

            string reason;
            bool safe = PowerSafetyGate.IsSafe(20.0, cfg, out reason);

            Assert.True(safe);
            Assert.Equal(string.Empty, reason);
            PowerSafetyGate.Guard(20.0, cfg); // must not throw
        }

        [Fact]
        public void Armed_exactly_at_limit_is_safe()
        {
            // Predicted analyzer input must strictly exceed the limit to be unsafe.
            var cfg = new RfPathSafety { Armed = true, AnalyzerMaxSafeInputDbm = 0.0, PathLossDb = 0.0 };

            string reason;
            bool safe = PowerSafetyGate.IsSafe(0.0, cfg, out reason);

            Assert.True(safe);
            Assert.Equal(string.Empty, reason);
        }

        [Fact]
        public void AnalyzerInputDbm_subtracts_path_loss()
        {
            var cfg = new RfPathSafety { PathLossDb = 30.0 };

            Assert.Equal(-10.0, PowerSafetyGate.AnalyzerInputDbm(20.0, cfg), 9);
            Assert.Equal(5.0, PowerSafetyGate.AnalyzerInputDbm(5.0, new RfPathSafety { PathLossDb = 0.0 }), 9);
        }

        [Fact]
        public void Defaults_are_conservative()
        {
            var cfg = new RfPathSafety();

            Assert.False(cfg.Armed);
            // +30 dBm is a 5 dB backstop below the E4406A's +35 dBm rated max input.
            Assert.Equal(30.0, cfg.AnalyzerMaxSafeInputDbm, 9);
            Assert.Equal(0.0, cfg.PathLossDb, 9);
        }
    }
}
