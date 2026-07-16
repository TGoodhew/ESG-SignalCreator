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
    /// <summary>
    /// Coverage guard for the v2 feature parameters at the assistant tool boundary (#225).
    /// The v1 <see cref="ConfigureToolsTests"/> proves each tool routes; the personality unit tests
    /// prove the DSP. This file closes the gap in between: every v2 parameter the tools advertise to
    /// Claude must actually be (a) declared in the tool's input schema, (b) — for enum params —
    /// accepted by <see cref="SchemaValidator"/>, and (c) forwarded verbatim into the host args.
    /// It also guards the assistant personality surface against silent drift.
    /// </summary>
    public class ConfigureToolsV2ParamsTests
    {
        private sealed class FakeConfigureHost : IAssistantConfigureHost
        {
            public string LastConfigureArea;
            public JObject LastConfigureArgs;

            public JObject SetSourcePersonality(string name) => new JObject { ["personality"] = name };
            public JObject Configure(string personality, JObject args)
            {
                LastConfigureArea = personality;
                LastConfigureArgs = args;
                return new JObject { ["personality"] = personality, ["applied"] = true };
            }
            public JObject SelectPlotView(string pane, string view) => new JObject();
            public JObject SetProject(string action, string path) => new JObject();
            public JObject CalculateWaveform() => new JObject();
        }

        private static IAppTool Tool(string name) => ConfigureTools.All().Single(t => t.Name == name);

        private static async Task<ToolResult> Run(string name, JObject args, IAssistantConfigureHost host)
        {
            var ctx = new ToolContext();
            ctx.Register<IAssistantConfigureHost>(host);
            return await Tool(name).ExecuteAsync(args, ctx, CancellationToken.None);
        }

        private static JObject Properties(string toolName) => (JObject)Tool(toolName).InputSchema["properties"];

        // Every v2 parameter that a configure tool must expose to Claude, by tool.
        // If a v2 feature ships without its tool param wired, one of these rows fails loud.
        public static IEnumerable<object[]> V2Params => new[]
        {
            // Multitone-Distortion: per-tone tables + pre-distortion (#... v2)
            new object[] { "configure_multitone_distortion", "per_tone_magnitude_db" },
            new object[] { "configure_multitone_distortion", "per_tone_phase_deg" },
            new object[] { "configure_multitone_distortion", "predistortion_enabled" },
            new object[] { "configure_multitone_distortion", "measured_tone_magnitude_error_db" },
            new object[] { "configure_multitone_distortion", "measured_tone_phase_error_deg" },
            // Pulse: intra-pulse coding + PRI patterning
            new object[] { "configure_pulse", "intra_pulse_step_count" },
            new object[] { "configure_pulse", "barker_length" },
            new object[] { "configure_pulse", "frank_order_n" },
            new object[] { "configure_pulse", "pri_mode" },
            new object[] { "configure_pulse", "pri_jitter_sec" },
            new object[] { "configure_pulse", "pri_jitter_seed" },
            // Jitter: SJ sweep + masks
            new object[] { "configure_jitter", "sweep_enabled" },
            new object[] { "configure_jitter", "sweep_start_hz" },
            new object[] { "configure_jitter", "sweep_stop_hz" },
            new object[] { "configure_jitter", "sweep_mode" },
            new object[] { "configure_jitter", "mask_standard" },
            new object[] { "configure_jitter", "sweep_follow_mask" },
            new object[] { "configure_jitter", "max_jitter_ui_pp" },
            // Broadcast Radio: RDS
            new object[] { "configure_broadcast_radio", "rds" },
            new object[] { "configure_broadcast_radio", "rds_deviation_hz" },
            // OFDM-family: frame-structured mode + parameters
            new object[] { "configure_digital_video", "frame_structured" },
            new object[] { "configure_digital_video", "guard_interval" },
            new object[] { "configure_tdmb", "frame_structured" },
            new object[] { "configure_wimax_mobile", "frame_structured" },
            new object[] { "configure_wimax_mobile", "cyclic_prefix_ratio" },
            new object[] { "configure_wimax_mobile", "include_preamble" },
            new object[] { "configure_wimax_fixed", "frame_structured" },
            new object[] { "configure_wimax_fixed", "cyclic_prefix_ratio" },
            new object[] { "configure_wimax_fixed", "include_preamble" },
            new object[] { "configure_wlan", "frame_structured" },
            new object[] { "configure_wlan", "guard_interval" },
            new object[] { "configure_wlan", "include_ltf_preamble" },
            new object[] { "configure_lte_fdd", "frame_structured" },
            new object[] { "configure_lte_fdd", "cyclic_prefix" },
            new object[] { "configure_lte_fdd", "physical_cell_id" },
            new object[] { "configure_lte_fdd", "subframe_count" },
            new object[] { "configure_lte_tdd", "frame_structured" },
            new object[] { "configure_lte_tdd", "cyclic_prefix" },
            new object[] { "configure_lte_tdd", "physical_cell_id" },
            new object[] { "configure_lte_tdd", "subframe_count" },
            new object[] { "configure_lte_tdd", "tdd_ul_dl_config" },
            new object[] { "configure_lte_tdd", "tdd_special_subframe_config" },
            // Cellular DSSS: multi-code composite
            new object[] { "configure_wcdma", "code_channel_count" },
            new object[] { "configure_wcdma_hspa", "code_channel_count" },
            new object[] { "configure_cdma2000", "code_channel_count" },
            new object[] { "configure_td_scdma", "code_channel_count" },
            // Import-IQ toolkit: .mat + marker authoring
            new object[] { "configure_import_iq", "marker_mode" },
        };

        [Theory]
        [MemberData(nameof(V2Params))]
        public void V2_parameter_is_declared_in_tool_schema(string toolName, string param)
        {
            JObject props = Properties(toolName);
            Assert.True(props[param] != null,
                $"{toolName} schema must declare v2 parameter '{param}' so the assistant can set it.");
        }

        // Enum-valued v2 params: the SchemaValidator enforces enums, so an omitted value would make
        // Claude's tool call fail validation. Assert the value is admitted.
        public static IEnumerable<object[]> V2EnumValues => new[]
        {
            new object[] { "configure_gsm_edge", "modulation", "Edge8Psk" },
            new object[] { "configure_bluetooth", "modulation", "Edr2Mbps" },
            new object[] { "configure_bluetooth", "modulation", "Edr3Mbps" },
            new object[] { "configure_import_iq", "format", "Mat" },
            new object[] { "configure_import_iq", "format", "AgilentInt14Be" },
        };

        [Theory]
        [MemberData(nameof(V2EnumValues))]
        public void V2_enum_value_is_admitted_by_schema_and_validator(string toolName, string param, string value)
        {
            IAppTool tool = Tool(toolName);
            var en = tool.InputSchema["properties"][param]?["enum"] as JArray;
            Assert.NotNull(en);
            Assert.Contains(value, en.Select(t => (string)t));

            // The dispatcher's validator must accept a call carrying this enum value. Seed any of the
            // tool's own required properties so the check isolates the enum, not a missing required arg.
            var args = new JObject { [param] = value };
            var required = tool.InputSchema["required"] as JArray;
            if (required != null)
                foreach (JToken r in required)
                    if (args[(string)r] == null) args[(string)r] = "x";

            string reason = SchemaValidator.Validate(tool.InputSchema, args);
            Assert.Null(reason);
        }

        [Fact]
        public async Task Lte_tdd_v2_frame_params_are_accepted_and_forwarded()
        {
            var host = new FakeConfigureHost();
            var args = new JObject
            {
                ["frame_structured"] = true,
                ["cyclic_prefix"] = "Normal",
                ["physical_cell_id"] = 42,
                ["subframe_count"] = 10,
                ["tdd_ul_dl_config"] = 1,
                ["tdd_special_subframe_config"] = 7,
            };
            Assert.Null(SchemaValidator.Validate(Tool("configure_lte_tdd").InputSchema, args));

            await Run("configure_lte_tdd", args, host);
            Assert.Equal("lte_tdd", host.LastConfigureArea);
            Assert.True((bool)host.LastConfigureArgs["frame_structured"]);
            Assert.Equal(42, (int)host.LastConfigureArgs["physical_cell_id"]);
            Assert.Equal(1, (int)host.LastConfigureArgs["tdd_ul_dl_config"]);
        }

        [Fact]
        public async Task Cellular_multicode_and_rds_v2_params_are_forwarded()
        {
            var host = new FakeConfigureHost();

            var wcdma = new JObject { ["spreading_factor"] = 16, ["code_channel_count"] = 4 };
            Assert.Null(SchemaValidator.Validate(Tool("configure_wcdma").InputSchema, wcdma));
            await Run("configure_wcdma", wcdma, host);
            Assert.Equal("wcdma_fdd", host.LastConfigureArea);
            Assert.Equal(4, (int)host.LastConfigureArgs["code_channel_count"]);

            var radio = new JObject { ["stereo"] = true, ["rds"] = true, ["rds_deviation_hz"] = 2000.0 };
            Assert.Null(SchemaValidator.Validate(Tool("configure_broadcast_radio").InputSchema, radio));
            await Run("configure_broadcast_radio", radio, host);
            Assert.Equal("broadcast_radio", host.LastConfigureArea);
            Assert.True((bool)host.LastConfigureArgs["rds"]);
        }

        [Fact]
        public async Task Edge_and_edr_modulation_values_route_through_tools()
        {
            var host = new FakeConfigureHost();

            await Run("configure_gsm_edge", new JObject { ["modulation"] = "Edge8Psk", ["symbol_count"] = 512 }, host);
            Assert.Equal("gsm_edge", host.LastConfigureArea);
            Assert.Equal("Edge8Psk", (string)host.LastConfigureArgs["modulation"]);

            await Run("configure_bluetooth", new JObject { ["modulation"] = "Edr3Mbps", ["symbol_count"] = 512 }, host);
            Assert.Equal("bluetooth", host.LastConfigureArea);
            Assert.Equal("Edr3Mbps", (string)host.LastConfigureArgs["modulation"]);
        }

        // ---- Assistant personality-surface drift guard ------------------------------------------

        // The registry itself lives in the App project (not referenced here); the assistant's
        // set_source_personality enum is its mirror. Freezing the expected set turns a silent add/
        // remove of a personality into a loud failure that forces a coverage decision (tool + tests).
        private static readonly string[] ExpectedPersonalities =
        {
            "CW", "Multitone", "Multitone-Distortion", "Multi-Carrier", "CustomMod", "Pulse", "Jitter",
            "GSM-EDGE", "Bluetooth", "W-CDMA", "W-CDMA-HSPA", "cdma2000", "TD-SCDMA", "LTE-FDD", "LTE-TDD",
            "WLAN", "WiMAX-Fixed", "WiMAX-Mobile", "T-DMB", "Digital-Video", "Broadcast-Radio", "AWGN", "Import-IQ",
        };

        [Fact]
        public void Set_source_personality_enum_matches_expected_surface()
        {
            var en = Tool("set_source_personality").InputSchema["properties"]["personality"]["enum"] as JArray;
            Assert.NotNull(en);
            string[] actual = en.Select(t => (string)t).ToArray();
            Assert.Equal(ExpectedPersonalities.OrderBy(s => s), actual.OrderBy(s => s));
        }
    }
}
