using System.Linq;
using EsgSignalCreator.Capability;
using Xunit;

namespace EsgSignalCreator.Tests.Capability
{
    public class InstrumentProfileTests
    {
        [Fact]
        public void E4438C_profile_loads_from_embedded_resource()
        {
            InstrumentProfile p = InstrumentProfiles.Load("E4438C");

            Assert.NotNull(p);
            Assert.Equal("E4438C", p.Model);
            Assert.Equal(60, p.MinSamples);
            Assert.Equal(100e6, p.MaxSampleClockHz);
        }

        [Fact]
        public void E4438C_profile_lists_the_three_baseband_options()
        {
            InstrumentProfile p = InstrumentProfiles.Load("E4438C");

            Assert.Equal(3, p.BasebandOptions.Length);
            // Capacities from the rebuild spec §5.4.
            Assert.Contains(p.BasebandOptions, o => o.MaxSamples == 8377088);
            Assert.Contains(p.BasebandOptions, o => o.MaxSamples == 33509120);
            Assert.Contains(p.BasebandOptions, o => o.MaxSamples == 67018496);
            Assert.Equal(67018496, p.MaxSamples);
        }

        [Fact]
        public void LoadAll_is_keyed_by_model_name_case_insensitively()
        {
            var all = InstrumentProfiles.LoadAll();
            Assert.True(all.ContainsKey("e4438c"));
        }
    }
}
