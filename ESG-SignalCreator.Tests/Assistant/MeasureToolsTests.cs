using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EsgSignalCreator.Assistant.Host;
using EsgSignalCreator.Assistant.Tools;
using Newtonsoft.Json.Linq;
using Xunit;

namespace EsgSignalCreator.Tests.Assistant
{
    /// <summary>#90: E4406A measure_* / verify_signal tools delegate correctly and stay read-effect.</summary>
    public class MeasureToolsTests
    {
        private sealed class FakeMeasureHost : IAssistantMeasureHost
        {
            public readonly System.Collections.Generic.List<string> Calls = new System.Collections.Generic.List<string>();
            public double LastCenter, LastSpan, LastAcpBw;
            public double? VC, VP, VO;

            public JObject GetVsaState() { Calls.Add("state"); return new JObject { ["connected"] = true }; }
            public JObject MeasureChannelPower(double centerHz, double spanHz) { Calls.Add("chp"); LastCenter = centerHz; LastSpan = spanHz; return new JObject { ["total_power_dbm"] = -10.0 }; }
            public JObject MeasureAcp(double centerHz, double carrierBandwidthHz) { Calls.Add("acp"); LastCenter = centerHz; LastAcpBw = carrierBandwidthHz; return new JObject(); }
            public JObject MeasureCcdf(double centerHz) { Calls.Add("ccdf"); LastCenter = centerHz; return new JObject { ["papr_db"] = 3.8 }; }
            public JObject MeasureSpectrumPeak(double centerHz, double spanHz) { Calls.Add("spec"); LastCenter = centerHz; LastSpan = spanHz; return new JObject(); }
            public JObject MeasureWaveform(double centerHz) { Calls.Add("wave"); LastCenter = centerHz; return new JObject(); }
            public JObject VerifySignal(double? carrierHz, double? commandedPowerDbm, double? toneOffsetHz)
            { Calls.Add("verify"); VC = carrierHz; VP = commandedPowerDbm; VO = toneOffsetHz; return new JObject { ["all_pass"] = true }; }
        }

        private static IAppTool Tool(string name) => MeasureTools.All().Single(t => t.Name == name);

        private static async Task<ToolResult> Run(string name, JObject args, FakeMeasureHost host)
        {
            var ctx = new ToolContext();
            ctx.Register<IAssistantMeasureHost>(host);
            return await Tool(name).ExecuteAsync(args, ctx, CancellationToken.None);
        }

        [Fact]
        public void All_measure_tools_are_read_effect()
        {
            Assert.All(MeasureTools.All(), t => Assert.Equal(ToolEffect.Read, t.Effect));
            Assert.Equal(7, MeasureTools.All().Count());
        }

        // #112: tool descriptions must be model-neutral so they read correctly whether an E4406A or an
        // N9010A is connected. get_vsa_state may name both models; the rest name neither.
        [Fact]
        public void Tool_descriptions_are_model_neutral()
        {
            foreach (IAppTool t in MeasureTools.All())
            {
                if (t.Name == "get_vsa_state")
                    Assert.Contains("N9010A", t.Description);
                else
                    Assert.DoesNotContain("E4406A", t.Description);
            }
        }

        [Fact]
        public void Measurement_tools_require_a_center_frequency()
        {
            foreach (string n in new[] { "measure_channel_power", "measure_acp", "measure_ccdf", "measure_spectrum_peak", "measure_waveform" })
                Assert.Contains("center_hz", Tool(n).InputSchema["required"].Select(t => (string)t));
        }

        [Fact]
        public async Task Channel_power_passes_center_and_default_span()
        {
            var host = new FakeMeasureHost();
            await Run("measure_channel_power", new JObject { ["center_hz"] = 1e9 }, host);
            Assert.Equal(1e9, host.LastCenter, 0);
            Assert.Equal(5e6, host.LastSpan, 0); // default span applied
        }

        [Fact]
        public async Task Each_measure_tool_delegates_to_its_host_method()
        {
            var host = new FakeMeasureHost();
            await Run("get_vsa_state", new JObject(), host);
            await Run("measure_acp", new JObject { ["center_hz"] = 1e9, ["carrier_bandwidth_hz"] = 4e6 }, host);
            await Run("measure_ccdf", new JObject { ["center_hz"] = 1e9 }, host);
            await Run("measure_spectrum_peak", new JObject { ["center_hz"] = 1e9 }, host);
            await Run("measure_waveform", new JObject { ["center_hz"] = 1e9 }, host);

            Assert.Equal(new[] { "state", "acp", "ccdf", "spec", "wave" }, host.Calls.ToArray());
            Assert.Equal(4e6, host.LastAcpBw, 0);
        }

        [Fact]
        public async Task Verify_signal_forwards_optional_params_as_nullable()
        {
            var host = new FakeMeasureHost();
            await Run("verify_signal", new JObject { ["carrier_hz"] = 1e9, ["tone_offset_hz"] = 1e6 }, host);
            Assert.Equal("verify", host.Calls.Single());
            Assert.Equal(1e9, host.VC.Value, 0);
            Assert.Null(host.VP);              // omitted -> null (host falls back to ESG setting)
            Assert.Equal(1e6, host.VO.Value, 0);
        }

        [Fact]
        public async Task Verify_signal_summary_comes_from_the_host()
        {
            var host = new FakeMeasureHost();
            ToolResult r = await Run("measure_ccdf", new JObject { ["center_hz"] = 1e9 }, host);
            Assert.False(r.IsError);
            Assert.Equal(3.8, (double)r.Data["papr_db"], 6);
        }
    }
}
