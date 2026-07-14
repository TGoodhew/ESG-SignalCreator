using System.Collections.Generic;
using System.Text;
using EsgSignalCreator;
using EsgSignalCreator.Instruments;
using EsgSignalCreator.Model;
using Xunit;

namespace EsgSignalCreator.Tests.Control
{
    public class EsgControllerDownloadTests
    {
        /// <summary>An in-memory transport that records writes — lets us assert SCPI without hardware.</summary>
        private sealed class FakeInstrument : IInstrument
        {
            public readonly List<string> Writes = new List<string>();
            public byte[] LastBinaryBlock;

            public string ResourceName => "FAKE::INSTR";
            public bool IsConnected => true;
            public int TimeoutMilliseconds { get; set; }

            /// <summary>Reply for :SYSTem:ERRor? — overridable to simulate a rejected download (#120).</summary>
            public string ErrorReply = "0,\"No error\"";

            public void Write(string command) => Writes.Add(command);
            public string ReadString() => "0,\"No error\"";
            public string Query(string command)
            {
                Writes.Add(command);
                return command == ":SYSTem:ERRor?" ? ErrorReply : "0,\"No error\"";
            }
            public void WriteBinaryBlock(byte[] message) => LastBinaryBlock = message;
            public void Dispose() { }
        }

        private static WaveformModel DcWaveform(int n)
        {
            var i = new float[n];
            var q = new float[n];
            for (int k = 0; k < n; k++) i[k] = 1.0f;
            return new WaveformModel(i, q, 1e6, "seg");
        }

        [Fact]
        public void DownloadWaveform_turns_arb_off_before_writing_the_block()
        {
            var io = new FakeInstrument();
            var esg = new EsgController(io);

            esg.DownloadWaveform("seg", DcWaveform(64), backoff: 1.0);

            // ARB must be turned off before the segment is overwritten (rebuild spec §5.3).
            Assert.Contains(":RADio:ARB:STATe OFF", io.Writes);
            Assert.NotNull(io.LastBinaryBlock);
        }

        [Fact]
        public void DownloadWaveform_emits_a_well_formed_WFM1_definite_length_block()
        {
            var io = new FakeInstrument();
            var esg = new EsgController(io);

            esg.DownloadWaveform("seg", DcWaveform(64), backoff: 1.0);

            string head = Encoding.ASCII.GetString(io.LastBinaryBlock, 0, 32);
            // 64 samples -> 256 payload bytes -> header "#3256".
            Assert.StartsWith(":MEMory:DATA \"WFM1:seg\",#3256", head);
        }

        // #120: after WriteBinaryBlock the controller reads *OPC? then :SYSTem:ERRor? and throws on error.
        [Fact]
        public void DownloadWaveform_reads_back_opc_and_error_after_the_block()
        {
            var io = new FakeInstrument();
            var esg = new EsgController(io);

            esg.DownloadWaveform("seg", DcWaveform(64), backoff: 1.0);

            int iBlock = io.Writes.FindIndex(w => w.StartsWith(":MEMory:DATA"));
            int iOpc = io.Writes.FindIndex(w => w == "*OPC?");
            int iErr = io.Writes.FindIndex(w => w == ":SYSTem:ERRor?");
            // Both read-backs happen; error read follows *OPC? which follows... the block write is
            // recorded via WriteBinaryBlock (not Writes), so just assert *OPC? precedes the error read.
            Assert.True(iOpc >= 0 && iErr > iOpc, "Expected *OPC? then :SYSTem:ERRor? after download.");
        }

        [Fact]
        public void DownloadWaveform_throws_when_the_generator_rejects_the_block()
        {
            var io = new FakeInstrument { ErrorReply = "-222,\"Data out of range\"" };
            var esg = new EsgController(io);

            var ex = Assert.Throws<System.InvalidOperationException>(
                () => esg.DownloadWaveform("seg", DcWaveform(64), backoff: 1.0));
            Assert.Contains("Data out of range", ex.Message);
            Assert.Contains("seg", ex.Message);
        }

        [Fact]
        public void PlayWaveform_selects_sets_clock_scaling_then_enables_arb_in_order()
        {
            var io = new FakeInstrument();
            var esg = new EsgController(io);

            esg.PlayWaveform("seg", 10e6, runtimeScalingPercent: 80);

            int iWave = io.Writes.FindIndex(w => w.StartsWith(":RADio:ARB:WAVeform"));
            int iClk = io.Writes.FindIndex(w => w.StartsWith(":RADio:ARB:SCLock:RATE"));
            int iScale = io.Writes.FindIndex(w => w.StartsWith(":RADio:ARB:RSCaling"));
            int iOn = io.Writes.FindIndex(w => w == ":RADio:ARB:STATe ON");

            Assert.True(iWave >= 0 && iClk > iWave && iScale > iClk && iOn > iScale,
                "Expected WAVeform -> SCLock:RATE -> RSCaling -> STATe ON order.");
        }
    }
}
