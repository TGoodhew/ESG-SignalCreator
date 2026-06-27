using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EsgSignalCreator.Assistant.Host;
using EsgSignalCreator.Assistant.Tools;
using Newtonsoft.Json.Linq;
using Xunit;

namespace EsgSignalCreator.Tests.Assistant
{
    public class ConfigureToolsTests
    {
        private sealed class FakeConfigureHost : IAssistantConfigureHost
        {
            public string LastPersonalitySet;
            public string LastConfigureArea;
            public JObject LastConfigureArgs;
            public string LastPane, LastView, LastProjectAction, LastProjectPath;
            public bool Calculated;

            public JObject SetSourcePersonality(string name)
            {
                LastPersonalitySet = name;
                return new JObject { ["personality"] = name };
            }

            public JObject Configure(string personality, JObject args)
            {
                LastConfigureArea = personality;
                LastConfigureArgs = args;
                return new JObject { ["personality"] = personality, ["applied"] = true };
            }

            public JObject SelectPlotView(string pane, string view)
            {
                LastPane = pane; LastView = view;
                return new JObject { ["pane"] = pane, ["view"] = view };
            }

            public JObject SetProject(string action, string path)
            {
                LastProjectAction = action; LastProjectPath = path;
                return new JObject { ["action"] = action };
            }

            public JObject CalculateWaveform()
            {
                Calculated = true;
                return new JObject { ["sample_count"] = 4096, ["summary"] = "4096 samples calculated." };
            }
        }

        private static IAppTool Tool(string name) => ConfigureTools.All().Single(t => t.Name == name);

        private static async Task<ToolResult> Run(string name, JObject args, IAssistantConfigureHost host)
        {
            var ctx = new ToolContext();
            ctx.Register<IAssistantConfigureHost>(host);
            return await Tool(name).ExecuteAsync(args, ctx, CancellationToken.None);
        }

        [Fact]
        public void All_configure_tools_are_configure_effect()
        {
            Assert.All(ConfigureTools.All(), t => Assert.Equal(ToolEffect.Configure, t.Effect));
            Assert.Equal(9, ConfigureTools.All().Count());
        }

        [Fact]
        public async Task Set_source_personality_passes_name_to_host()
        {
            var host = new FakeConfigureHost();
            ToolResult r = await Run("set_source_personality", new JObject { ["personality"] = "Multitone" }, host);
            Assert.False(r.IsError);
            Assert.Equal("Multitone", host.LastPersonalitySet);
        }

        [Fact]
        public async Task Configure_multitone_routes_args_to_host_with_area()
        {
            var host = new FakeConfigureHost();
            var args = new JObject { ["tone_count"] = 4, ["phase_strategy"] = "Newman" };
            await Run("configure_multitone", args, host);
            Assert.Equal("multitone", host.LastConfigureArea);
            Assert.Equal(4, (int)host.LastConfigureArgs["tone_count"]);
        }

        [Fact]
        public async Task Configure_cw_and_awgn_and_custommod_and_importiq_use_distinct_areas()
        {
            var host = new FakeConfigureHost();
            await Run("configure_cw", new JObject { ["offset_hz"] = 1e6 }, host);
            Assert.Equal("cw", host.LastConfigureArea);
            await Run("configure_awgn", new JObject { ["bandwidth_hz"] = 5e6 }, host);
            Assert.Equal("awgn", host.LastConfigureArea);
            await Run("configure_custom_modulation", new JObject { ["modulation"] = "QAM16", ["symbol_rate_hz"] = 1e6 }, host);
            Assert.Equal("custom_modulation", host.LastConfigureArea);
            await Run("configure_import_iq", new JObject { ["file_path"] = "c:\\x.csv" }, host);
            Assert.Equal("import_iq", host.LastConfigureArea);
        }

        [Fact]
        public async Task Select_plot_view_passes_pane_and_view()
        {
            var host = new FakeConfigureHost();
            await Run("select_plot_view", new JObject { ["pane"] = "top", ["view"] = "Spectrum" }, host);
            Assert.Equal("top", host.LastPane);
            Assert.Equal("Spectrum", host.LastView);
        }

        [Fact]
        public async Task Set_project_passes_action_and_path()
        {
            var host = new FakeConfigureHost();
            await Run("set_project", new JObject { ["action"] = "save", ["path"] = "c:\\p.ssproj" }, host);
            Assert.Equal("save", host.LastProjectAction);
            Assert.Equal("c:\\p.ssproj", host.LastProjectPath);
        }

        [Fact]
        public async Task Calculate_waveform_runs_and_uses_host_summary()
        {
            var host = new FakeConfigureHost();
            ToolResult r = await Run("calculate_waveform", new JObject(), host);
            Assert.True(host.Calculated);
            Assert.Equal("4096 samples calculated.", r.Summary);
            Assert.Equal(4096, (int)r.Data["sample_count"]);
        }

        [Fact]
        public void Import_iq_requires_file_path_in_schema()
        {
            JObject schema = Tool("configure_import_iq").InputSchema;
            Assert.Contains("file_path", schema["required"].Select(t => (string)t));
        }
    }
}
