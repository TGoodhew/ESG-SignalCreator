using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EsgSignalCreator;
using EsgSignalCreator.Assistant.Api;
using EsgSignalCreator.Assistant.Host;
using EsgSignalCreator.Assistant.Tools;
using EsgSignalCreator.Instruments;
using EsgSignalCreator.Verify;
using Newtonsoft.Json.Linq;
using Xunit;

namespace EsgSignalCreator.Tests.Assistant
{
    /// <summary>
    /// Acceptance: manual-vs-assistant SCPI parity and outbound data/secret hygiene (#87, §11). The
    /// assistant must drive the SAME Core path as the UI (so the SCPI is identical), and must never leak
    /// bulk data or the API key.
    /// </summary>
    public class AcceptanceParityHygieneTests
    {
        /// <summary>Records every SCPI write/query so two paths can be compared byte-for-byte.</summary>
        private sealed class RecordingIo : IInstrument
        {
            public readonly List<string> Writes = new List<string>();
            public string ResourceName => "rec";
            public bool IsConnected => true;
            public int TimeoutMilliseconds { get; set; }
            public void Write(string command) => Writes.Add(command);
            public string ReadString() => "";
            public string Query(string command) { Writes.Add(command); return command.EndsWith("?") ? "0" : ""; }
            public void WriteBinaryBlock(byte[] message) { }
            public void Dispose() { }
        }

        /// <summary>
        /// Minimal hardware host over a real EsgController — mirrors StudioAssistantHost.SetInstrumentSettings
        /// (same EsgController calls, same order), so the test exercises the real SCPI authority.
        /// </summary>
        private sealed class ControllerHardwareHost : IAssistantHardwareHost
        {
            private readonly EsgController _esg;
            private readonly RfPathSafety _safety = new RfPathSafety();
            public ControllerHardwareHost(EsgController esg) => _esg = esg;

            public JObject SetInstrumentSettings(JObject args)
            {
                if (args["frequency_hz"] != null) _esg.SetFrequencyHz((double)args["frequency_hz"]);
                if (args["power_dbm"] != null) { double d = (double)args["power_dbm"]; PowerSafetyGate.Guard(d, _safety); _esg.SetAmplitudeDbm(d); }
                if (args["rf_on"] != null) _esg.SetRfOutput((bool)args["rf_on"]);
                if (args["modulation_on"] != null) _esg.SetModulation((bool)args["modulation_on"]);
                return new JObject { ["applied"] = true };
            }

            public JObject ConnectInstrument(string resource) => new JObject();
            public JObject DisconnectInstrument() => new JObject();
            public JObject DownloadWaveform() => new JObject();
            public JObject PlayRf() => new JObject();
            public JObject StopRf() => new JObject();
        }

        [Fact]
        public async Task Assistant_set_instrument_settings_emits_the_same_scpi_as_manual()
        {
            // Manual path: drive EsgController directly, as the UI does.
            var manualIo = new RecordingIo();
            var manual = new EsgController(manualIo);
            manual.SetFrequencyHz(1e9);
            manual.SetAmplitudeDbm(-10.0);
            manual.SetRfOutput(true);
            manual.SetModulation(true);

            // Assistant path: same operations via the set_instrument_settings tool -> host -> EsgController.
            var asstIo = new RecordingIo();
            var ctx = new ToolContext();
            ctx.Register<IAssistantHardwareHost>(new ControllerHardwareHost(new EsgController(asstIo)));
            IAppTool tool = HardwareTools.All().Single(t => t.Name == "set_instrument_settings");
            await tool.ExecuteAsync(new JObject
            {
                ["frequency_hz"] = 1e9,
                ["power_dbm"] = -10.0,
                ["rf_on"] = true,
                ["modulation_on"] = true
            }, ctx, default);

            Assert.Equal(manualIo.Writes, asstIo.Writes); // identical SCPI, same order
        }

        // ---- outbound hygiene ----

        private sealed class BigArrayTool : IAppTool
        {
            public string Name => "dump_iq";
            public string Description => "returns a large array to test privacy minimization";
            public ToolEffect Effect => ToolEffect.Read;
            public JObject InputSchema => Schema.Object();
            public Task<ToolResult> ExecuteAsync(JObject args, ToolContext ctx, CancellationToken ct)
            {
                var arr = new JArray();
                for (int i = 0; i < 5000; i++) arr.Add(i);
                return Task.FromResult(ToolResult.Ok("big", new JObject { ["iq"] = arr }));
            }
        }

        [Fact]
        public async Task Bulk_arrays_are_collapsed_before_leaving_the_dispatcher()
        {
            var reg = new ToolRegistry().Register(new BigArrayTool());
            var dispatcher = new ToolDispatcher(reg, new ToolContext());

            ContentBlock block = await dispatcher.InvokeAsync(ContentBlock.OfToolUse("t", "dump_iq", new JObject()), default);
            JObject body = JObject.Parse(block.Content);

            Assert.True((bool)body["iq"]["_omitted_array"]); // raw 5000 values never serialized
            Assert.Equal(5000, (int)body["iq"]["length"]);
            Assert.DoesNotContain("4999", block.Content);
        }

        [Fact]
        public void The_api_key_is_never_in_the_request_body()
        {
            var req = new ClaudeRequest { Model = "claude-opus-4-8", Messages = { ClaudeMessage.User("hello") } };
            string json = ClaudeClient.Serialize(req);
            // The key is sent as the x-api-key header, never serialized into the JSON body.
            Assert.DoesNotContain("api_key", json);
            Assert.DoesNotContain("x-api-key", json);
            Assert.DoesNotContain("sk-", json);
        }
    }
}
