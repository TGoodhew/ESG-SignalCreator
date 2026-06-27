using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EsgSignalCreator.Assistant.Api;
using EsgSignalCreator.Assistant.Host;
using EsgSignalCreator.Assistant.Tools;
using EsgSignalCreator.Validation;
using Newtonsoft.Json.Linq;
using Xunit;

namespace EsgSignalCreator.Tests.Assistant
{
    public class ReadToolsTests
    {
        private sealed class FakeReadHost : IAssistantReadHost
        {
            public ReadoutSnapshot Readout = new ReadoutSnapshot
            {
                SampleCount = 4096,
                SampleRateHz = 10e6,
                DurationSeconds = 4096 / 10e6,
                PeakDbfs = -1.2,
                RmsDbfs = -12.0,
                PaprDb = 3.8,
                OccupiedBwHz = 5e6,
                DacHeadroomDb = 2.1
            };

            public AppStateSnapshot GetAppState() => new AppStateSnapshot
            {
                PersonalityName = "Multitone",
                Connected = true,
                InstrumentModel = "E4438C",
                InstrumentOptions = new[] { "005", "602" },
                PipelineStage = "calculated",
                MemoryUsedSamples = 4096,
                MemoryAvailableSamples = 8_000_000,
                LastError = null
            };

            public IReadOnlyList<PersonalityInfo> ListPersonalities() => new[]
            {
                new PersonalityInfo { Name = "CW", Description = "single tone", Parameters = new JObject { ["offset_hz"] = "number" } },
                new PersonalityInfo { Name = "Multitone", Description = "n tones", Parameters = new JObject() }
            };

            public JObject GetCurrentConfig() => new JObject { ["personality"] = "Multitone", ["tones"] = 4 };

            public IReadOnlyList<ValidationResult> GetValidation() => new[]
            {
                new ValidationResult(ValidationSeverity.Warning, "near DAC limit", "scaling"),
                new ValidationResult(ValidationSeverity.Error, "memory cap exceeded", "length")
            };

            public ReadoutSnapshot GetReadout() => Readout;
        }

        private static async Task<JObject> RunVia(IAppTool tool, IAssistantReadHost host)
        {
            var ctx = new ToolContext();
            ctx.Register<IAssistantReadHost>(host);
            ToolResult r = await tool.ExecuteAsync(new JObject(), ctx, CancellationToken.None);
            Assert.False(r.IsError);
            return r.Data;
        }

        private static IAppTool Tool(string name) => ReadTools.All().Single(t => t.Name == name);

        [Fact]
        public void All_read_tools_are_read_effect()
        {
            Assert.All(ReadTools.All(), t => Assert.Equal(ToolEffect.Read, t.Effect));
            Assert.Equal(5, ReadTools.All().Count());
        }

        [Fact]
        public async Task Get_app_state_reports_connection_and_personality()
        {
            JObject d = await RunVia(Tool("get_app_state"), new FakeReadHost());
            Assert.Equal("Multitone", (string)d["personality"]);
            Assert.True((bool)d["connected"]);
            Assert.Equal("E4438C", (string)d["instrument_model"]);
            Assert.Equal(new[] { "005", "602" }, d["instrument_options"].Select(t => (string)t).ToArray());
            Assert.Equal(8_000_000, (long)d["memory_available_samples"]);
        }

        [Fact]
        public async Task List_personalities_returns_names_and_parameters()
        {
            JObject d = await RunVia(Tool("list_personalities"), new FakeReadHost());
            var names = ((JArray)d["personalities"]).Select(p => (string)p["name"]).ToArray();
            Assert.Equal(new[] { "CW", "Multitone" }, names);
        }

        [Fact]
        public async Task Get_current_config_returns_the_project_json()
        {
            JObject d = await RunVia(Tool("get_current_config"), new FakeReadHost());
            Assert.Equal(4, (int)d["config"]["tones"]);
        }

        [Fact]
        public async Task Get_validation_results_counts_severities()
        {
            JObject d = await RunVia(Tool("get_validation_results"), new FakeReadHost());
            Assert.Equal(1, (int)d["error_count"]);
            Assert.Equal(1, (int)d["warning_count"]);
            Assert.Equal("Error", (string)((JArray)d["results"])[1]["severity"]);
        }

        [Fact]
        public async Task Get_results_readout_reports_metrics()
        {
            JObject d = await RunVia(Tool("get_results_readout"), new FakeReadHost());
            Assert.True((bool)d["calculated"]);
            Assert.Equal(4096, (long)d["sample_count"]);
            Assert.Equal(3.8, (double)d["papr_db"], 6);
        }

        [Fact]
        public async Task Get_results_readout_handles_no_waveform()
        {
            var host = new FakeReadHost();
            host.Readout = null;
            JObject d = await RunVia(Tool("get_results_readout"), host);
            Assert.False((bool)d["calculated"]);
        }
    }
}
