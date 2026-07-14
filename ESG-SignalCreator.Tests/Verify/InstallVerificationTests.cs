using System.Collections.Generic;
using System.Linq;
using EsgSignalCreator;
using EsgSignalCreator.Instruments;
using EsgSignalCreator.Verify;
using EsgSignalCreator.Visa;
using Xunit;

namespace EsgSignalCreator.Tests.Verify
{
    /// <summary>#125: the install self-test drives the ESG per signal and measures each on the analyzer.</summary>
    public class InstallVerificationTests
    {
        // Accepts all writes; EsgController only writes (no queries) on the download/play/set path.
        private sealed class FakeEsgIo : IInstrument
        {
            public readonly List<string> Writes = new List<string>();
            public string ResourceName => "TCPIP0::esg::INSTR";
            public bool IsConnected => true;
            public int TimeoutMilliseconds { get; set; }
            public void Write(string command) => Writes.Add(command);
            public string ReadString() => "";
            public string Query(string command) { Writes.Add(command); return ""; }
            public void WriteBinaryBlock(byte[] message) { }
            public void Dispose() { }
        }

        // Returns canned measurement scalars so VerificationHarness.Verify yields results.
        private sealed class FakeVsaIo : IInstrument
        {
            public string ResourceName => "GPIB0::17::INSTR";
            public bool IsConnected => true;
            public int TimeoutMilliseconds { get; set; }
            public void Write(string command) { }
            public string ReadString() => "";
            public string Query(string command)
            {
                if (command == "*IDN?") return "Agilent Technologies, E4406A, US1, A.05.00";
                if (command.StartsWith(":READ:CHPower")) return "-10.0,-70.0";
                if (command.StartsWith(":READ:PSTatistic")) return "0,0,0,0,0,0,0,0,3.0,0"; // PAPR at [8]
                if (command.EndsWith(":MARKer1:X?")) return "1001000000";
                if (command.EndsWith(":MARKer1:Y?")) return "-12.0";
                if (command.StartsWith(":READ:SPECtrum")) return "0";
                return "";
            }
            public void WriteBinaryBlock(byte[] message) { }
            public void Dispose() { }
        }

        private static InstallVerificationReport Run(RfPathSafety safety = null) =>
            InstallVerification.Run(
                new EsgController(new FakeEsgIo()),
                new VsaInstrument(new FakeVsaIo()),
                safety,
                new InstallVerificationOptions { SettleMs = 0, CarrierHz = 1e9, PowerDbm = -10.0 });

        [Fact]
        public void Run_produces_cw_am_fm_iq_steps()
        {
            InstallVerificationReport report = Run();

            Assert.Equal(new[] { "CW", "AM", "FM", "IQ (multitone)" }, report.Steps.Select(s => s.Name).ToArray());

            foreach (InstallVerificationStep step in report.Steps)
            {
                Assert.Contains(step.Results, r => r.Metric == "Channel power");
                Assert.Contains(step.Results, r => r.Metric == "PAPR");
            }
            // Only the CW step adds the spectrum-peak tone-frequency check.
            Assert.Contains(report.Steps.Single(s => s.Name == "CW").Results, r => r.Metric == "Tone frequency");
            Assert.DoesNotContain(report.Steps.Single(s => s.Name == "FM").Results, r => r.Metric == "Tone frequency");
        }

        [Fact]
        public void Flatten_prefixes_each_metric_with_its_step()
        {
            IReadOnlyList<VerificationResult> flat = Run().Flatten();
            Assert.Contains(flat, r => r.Metric == "CW · Channel power");
            Assert.Contains(flat, r => r.Metric == "AM · PAPR");
            Assert.Contains(flat, r => r.Metric == "IQ (multitone) · Channel power");
        }

        // #143: the capture hook fires once per measured signal, after the step is recorded, while that
        // signal is still armed on the analyzer.
        [Fact]
        public void Run_invokes_the_capture_hook_once_per_signal()
        {
            var seen = new List<string>();
            InstallVerification.Run(
                new EsgController(new FakeEsgIo()),
                new VsaInstrument(new FakeVsaIo()),
                null,
                new InstallVerificationOptions { SettleMs = 0, CarrierHz = 1e9, PowerDbm = -10.0 },
                onStepMeasured: step => seen.Add(step.Name));

            Assert.Equal(new[] { "CW", "AM", "FM", "IQ (multitone)" }, seen.ToArray());
        }

        [Fact]
        public void Capture_hook_exception_does_not_fail_the_run()
        {
            // A capture failure must be swallowed so the verification verdict still stands.
            InstallVerificationReport report = InstallVerification.Run(
                new EsgController(new FakeEsgIo()),
                new VsaInstrument(new FakeVsaIo()),
                null,
                new InstallVerificationOptions { SettleMs = 0, CarrierHz = 1e9, PowerDbm = -10.0 },
                onStepMeasured: step => throw new System.IO.IOException("capture boom"));

            Assert.Equal(4, report.Steps.Count);
        }

        [Fact]
        public void Armed_gate_over_limit_blocks_the_run_before_emitting()
        {
            // Armed with a limit below the -10 dBm test level (no path loss) -> the gate must throw.
            var safety = new RfPathSafety { Armed = true, AnalyzerMaxSafeInputDbm = -20.0, PathLossDb = 0.0 };
            Assert.Throws<RfSafetyException>(() => Run(safety));
        }
    }
}
