using System;
using System.IO;
using System.Runtime.Serialization;
using EsgSignalCreator.Project;
using Xunit;

namespace EsgSignalCreator.Tests.Project
{
    public class ProjectStoreTests
    {
        /// <summary>A small stand-in personality config used to exercise the opaque round-trip helpers.</summary>
        [DataContract]
        public sealed class SampleConfig
        {
            [DataMember]
            public double SampleRateHz { get; set; }

            [DataMember]
            public int Length { get; set; }

            [DataMember]
            public string Label { get; set; }
        }

        private static SampleConfig MakeSampleConfig()
        {
            return new SampleConfig { SampleRateHz = 12.5e6, Length = 4096, Label = "test-tone" };
        }

        [Fact]
        public void SerializeConfig_then_DeserializeConfig_by_type_preserves_values()
        {
            var original = MakeSampleConfig();

            string json = ProjectStore.SerializeConfig(original);
            var restored = (SampleConfig)ProjectStore.DeserializeConfig(json, typeof(SampleConfig));

            Assert.Equal(original.SampleRateHz, restored.SampleRateHz);
            Assert.Equal(original.Length, restored.Length);
            Assert.Equal(original.Label, restored.Label);
        }

        [Fact]
        public void DeserializeConfig_by_type_name_resolves_sample_type()
        {
            var original = MakeSampleConfig();
            string json = ProjectStore.SerializeConfig(original);

            // Assembly-qualified name exercises the Type.GetType path and assembly-scan fallback.
            string typeName = typeof(SampleConfig).AssemblyQualifiedName;
            var restored = ProjectStore.DeserializeConfig(json, typeName);

            var typed = Assert.IsType<SampleConfig>(restored);
            Assert.Equal(original.Label, typed.Label);
            Assert.Equal(original.Length, typed.Length);
            Assert.Equal(original.SampleRateHz, typed.SampleRateHz);
        }

        [Fact]
        public void Save_then_Load_round_trips_project()
        {
            var config = MakeSampleConfig();
            var project = new SsProject
            {
                PersonalityId = "Cw",
                ConfigTypeName = typeof(SampleConfig).AssemblyQualifiedName,
                ConfigJson = ProjectStore.SerializeConfig(config),
                Version = SsProject.CurrentVersion,
                Instrument = new SsProject.InstrumentSettings
                {
                    FrequencyHz = 1.0e9,
                    AmplitudeDbm = -10.0,
                    SampleClockHz = 12.5e6,
                    RuntimeScalingPercent = 70.0,
                    RfOn = true,
                    ModulationOn = true
                }
            };

            string path = Path.GetTempFileName();
            try
            {
                ProjectStore.Save(path, project);
                var loaded = ProjectStore.Load(path);

                Assert.Equal(project.PersonalityId, loaded.PersonalityId);
                Assert.Equal(project.ConfigTypeName, loaded.ConfigTypeName);
                Assert.Equal(project.ConfigJson, loaded.ConfigJson);
                Assert.Equal(project.Version, loaded.Version);

                Assert.Equal(project.Instrument.FrequencyHz, loaded.Instrument.FrequencyHz);
                Assert.Equal(project.Instrument.AmplitudeDbm, loaded.Instrument.AmplitudeDbm);
                Assert.Equal(project.Instrument.SampleClockHz, loaded.Instrument.SampleClockHz);
                Assert.Equal(project.Instrument.RuntimeScalingPercent, loaded.Instrument.RuntimeScalingPercent);
                Assert.Equal(project.Instrument.RfOn, loaded.Instrument.RfOn);
                Assert.Equal(project.Instrument.ModulationOn, loaded.Instrument.ModulationOn);

                // The embedded config JSON should still rehydrate via the stored type name.
                var restoredConfig = (SampleConfig)ProjectStore.DeserializeConfig(loaded.ConfigJson, loaded.ConfigTypeName);
                Assert.Equal(config.SampleRateHz, restoredConfig.SampleRateHz);
                Assert.Equal(config.Length, restoredConfig.Length);
                Assert.Equal(config.Label, restoredConfig.Label);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }
}
