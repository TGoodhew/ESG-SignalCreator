using System;
using System.Collections.Generic;
using EsgSignalCreator.Model;
using EsgSignalCreator.Personalities;
using EsgSignalCreator.Presets;
using Xunit;

namespace EsgSignalCreator.Tests.Presets
{
    public class TestModelsTests
    {
        [Fact]
        public void All_has_at_least_four_presets()
        {
            Assert.NotNull(TestModels.All);
            Assert.True(TestModels.All.Count >= 4, $"Expected at least 4 presets, got {TestModels.All.Count}.");
        }

        [Fact]
        public void Preset_names_are_unique()
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (TestModel m in TestModels.All)
            {
                Assert.False(string.IsNullOrEmpty(m.Name), "Preset name must be non-empty.");
                Assert.True(seen.Add(m.Name), $"Duplicate preset name: {m.Name}");
            }
        }

        [Fact]
        public void Every_preset_creates_a_configured_personality()
        {
            foreach (TestModel m in TestModels.All)
            {
                Assert.NotNull(m.Create);
                IWaveformPersonality p = m.Create();
                Assert.NotNull(p);
            }
        }

        [Fact]
        public void Every_preset_calculates_a_non_empty_waveform()
        {
            foreach (TestModel m in TestModels.All)
            {
                IWaveformPersonality p = m.Create();
                WaveformModel wf = p.Calculate(new Progress<int>());
                Assert.NotNull(wf);
                Assert.True(wf.Length > 0, $"Preset '{m.Name}' produced an empty waveform.");
            }
        }

        [Fact]
        public void Find_returns_preset_by_exact_name()
        {
            foreach (TestModel m in TestModels.All)
            {
                TestModel found = TestModels.Find(m.Name);
                Assert.NotNull(found);
                Assert.Same(m, found);
            }
        }

        [Fact]
        public void Find_returns_null_for_unknown_or_null_name()
        {
            Assert.Null(TestModels.Find("no such preset"));
            Assert.Null(TestModels.Find(null));
        }

        [Fact]
        public void SingleCarrier16Qam_preset_uses_custom_mod_personality()
        {
            TestModel m = TestModels.Find("Single-carrier 16-QAM");
            Assert.NotNull(m);
            Assert.Equal("custom-mod", m.Create().Id);
        }

        [Fact]
        public void Awgn_preset_uses_awgn_personality()
        {
            TestModel m = TestModels.Find("AWGN only");
            Assert.NotNull(m);
            Assert.Equal("awgn", m.Create().Id);
        }
    }
}
