using EsgSignalCreator.Capability;
using EsgSignalCreator.Validation;
using Xunit;

namespace EsgSignalCreator.Tests.Capability
{
    /// <summary>#120: the capability profile is reconciled with the live *IDN?/*OPT?/range.</summary>
    public class EffectiveProfileTests
    {
        private static InstrumentProfile Base() => new InstrumentProfile
        {
            Model = "E4438C",
            MinFrequencyHz = 250e3,
            MaxFrequencyHz = 6e9,
            MaxSampleClockHz = 100e6,
            MinSamples = 60,
            BasebandOptions = new[]
            {
                new BasebandOption { Name = "001/601", MaxSamples = 8_377_088 },
                new BasebandOption { Name = "002",     MaxSamples = 33_509_120 },
                new BasebandOption { Name = "602",     MaxSamples = 67_018_496 },
            }
        };

        [Fact]
        public void Memory_cap_reflects_only_the_installed_option()
        {
            // A unit with just 601: cap should be 8.3M, not the model-max 67M.
            InstrumentProfile eff = EffectiveProfile.Reconcile(Base(), "E4438C", new[] { "601", "UNJ" }, 0, 0);
            Assert.Equal(8_377_088, eff.MaxSamples);          // slash-alternative "001/601" matched via 601
            Assert.Single(eff.BasebandOptions);
        }

        [Fact]
        public void Empty_options_keep_the_base_set()
        {
            // A missing/failed *OPT? read must not strip capability.
            InstrumentProfile eff = EffectiveProfile.Reconcile(Base(), "E4438C", new string[0], 0, 0);
            Assert.Equal(67_018_496, eff.MaxSamples);
            Assert.Equal(3, eff.BasebandOptions.Length);
        }

        [Fact]
        public void No_installed_baseband_option_yields_empty_set()
        {
            InstrumentProfile eff = EffectiveProfile.Reconcile(Base(), "E4438C", new[] { "UNJ", "1EA" }, 0, 0);
            Assert.Empty(eff.BasebandOptions);
            Assert.Equal(0, eff.MaxSamples);
        }

        [Fact]
        public void Frequency_range_is_tightened_to_the_live_limits()
        {
            InstrumentProfile eff = EffectiveProfile.Reconcile(Base(), "E4438C", new[] { "602" }, 3e9, 100e3);
            Assert.Equal(3e9, eff.MaxFrequencyHz);            // live max 3 GHz < base 6 GHz
            Assert.Equal(250e3, eff.MinFrequencyHz);          // base min 250 kHz > live 100 kHz -> keep tighter
        }

        [Fact]
        public void Live_freq_of_zero_is_ignored()
        {
            InstrumentProfile eff = EffectiveProfile.Reconcile(Base(), "N/A", new[] { "602" }, 0, 0);
            Assert.Equal(6e9, eff.MaxFrequencyHz);
            Assert.Equal(250e3, eff.MinFrequencyHz);
        }

        [Fact]
        public void Model_comes_from_the_live_idn_when_present()
        {
            Assert.Equal("E4438C-live", EffectiveProfile.Reconcile(Base(), "E4438C-live", new[] { "602" }, 0, 0).Model);
            Assert.Equal("E4438C", EffectiveProfile.Reconcile(Base(), "", new[] { "602" }, 0, 0).Model);
        }

        [Fact]
        public void Null_base_profile_returns_null()
        {
            Assert.Null(EffectiveProfile.Reconcile(null, "E4438C", new[] { "602" }, 0, 0));
        }

        [Fact]
        public void Reconciled_cap_flags_a_waveform_the_base_profile_would_pass()
        {
            // 20M samples fits the 67M model-max but NOT a 601 unit's 8.3M.
            const int samples = 20_000_000;
            InstrumentProfile baseProfile = Base();
            InstrumentProfile eff = EffectiveProfile.Reconcile(baseProfile, "E4438C", new[] { "601" }, 0, 0);

            Assert.False(WaveformValidator.HasErrors(
                WaveformValidator.Validate(samples, 0.5, baseProfile, 10e6, 1e9)));
            Assert.True(WaveformValidator.HasErrors(
                WaveformValidator.Validate(samples, 0.5, eff, 10e6, 1e9)));
        }
    }
}
