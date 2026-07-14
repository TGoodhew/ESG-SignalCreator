using System.Collections.Generic;
using System.Text;
using EsgSignalCreator.Arb;
using EsgSignalCreator.Instruments;
using EsgSignalCreator.Visa;
using Xunit;

namespace EsgSignalCreator.Tests.Visa
{
    public class ScreenCaptureTests
    {
        /// <summary>Fake transport that also supports the binary read used for screen capture.</summary>
        private sealed class FakeCaptureIo : IInstrument, ISupportsBinaryRead
        {
            public readonly List<string> Writes = new List<string>();
            public string IdnResponse = "Keysight Technologies,N9010A,MY51234567,A.20.14";
            public byte[] RawResponse = new byte[0];

            public string ResourceName => "TCPIP0::10.0.0.5::hislip0::INSTR";
            public bool IsConnected => true;
            public int TimeoutMilliseconds { get; set; }
            public void Write(string command) => Writes.Add(command);
            public string ReadString() => "";
            public string Query(string command)
            {
                Writes.Add(command);
                if (command == "*IDN?") return IdnResponse;
                if (command == "*OPC?") return "1";
                return "";
            }
            public void WriteBinaryBlock(byte[] message) { }
            public byte[] ReadRaw(int maxBytes = 8 * 1024 * 1024) => RawResponse;
            public void Dispose() { }
        }

        /// <summary>Fake transport WITHOUT binary-read support (to prove the clear NotSupported failure).</summary>
        private sealed class PlainIo : IInstrument
        {
            public string ResourceName => "GPIB0::17::INSTR";
            public bool IsConnected => true;
            public int TimeoutMilliseconds { get; set; }
            public void Write(string command) { }
            public string ReadString() => "";
            public string Query(string command) => command == "*IDN?" ? "Agilent Technologies, E4406A, US1, A.05" : "";
            public void WriteBinaryBlock(byte[] message) { }
            public void Dispose() { }
        }

        // ---- IEEE-488.2 block payload parsing ----

        [Fact]
        public void ParsePayload_round_trips_a_framed_block()
        {
            byte[] payload = { 0x89, 0x50, 0x4E, 0x47, 0x00, 0x0A, 0xFF }; // PNG-ish, incl. a 0x0A byte
            byte[] framed = Ieee4882Block.Frame(payload);   // "#37" + 7 bytes
            Assert.Equal(payload, Ieee4882Block.ParsePayload(framed));
        }

        [Fact]
        public void ParsePayload_skips_leading_whitespace_and_ignores_trailing_terminator()
        {
            byte[] payload = { 1, 2, 3, 4 };
            byte[] framed = Ieee4882Block.Frame(payload);
            var buf = new List<byte> { (byte)'\n', (byte)' ' };  // stray leading bytes
            buf.AddRange(framed);
            buf.Add((byte)'\n');                                  // trailing terminator after payload
            Assert.Equal(payload, Ieee4882Block.ParsePayload(buf.ToArray()));
        }

        [Fact]
        public void ParsePayload_returns_input_when_there_is_no_block_header()
        {
            byte[] notABlock = Encoding.ASCII.GetBytes("no header here");
            Assert.Equal(notABlock, Ieee4882Block.ParsePayload(notABlock));
        }

        [Fact]
        public void ParsePayload_handles_indefinite_length_hash_zero()
        {
            byte[] payload = { 10, 20, 30 };
            var buf = new List<byte> { (byte)'#', (byte)'0' };
            buf.AddRange(payload);
            buf.Add((byte)'\n'); // single trailing newline stripped
            Assert.Equal(payload, Ieee4882Block.ParsePayload(buf.ToArray()));
        }

        // ---- per-model recipes ----

        [Fact]
        public void Dialects_expose_a_screen_capture_recipe()
        {
            ScreenCaptureRecipe n = new N9010ADialect().ScreenCapture;
            Assert.NotNull(n);
            Assert.Contains("STORe:SCReen", n.SaveCommandFormat);
            Assert.Contains("DATA?", n.DataQueryFormat);
            Assert.False(string.IsNullOrEmpty(n.TempPath));

            ScreenCaptureRecipe e = new E4406ADialect().ScreenCapture;
            Assert.NotNull(e);
            Assert.Contains("DATA?", e.DataQueryFormat);
        }

        [Fact]
        public void Recipe_With_applies_only_non_null_overrides()
        {
            var baseRecipe = new N9010ADialect().ScreenCapture;
            var overridden = baseRecipe.With(dataQueryFormat: ":HCOPy:SDUMp:DATA?", tempPath: "D:\\shot.png");
            Assert.Equal(":HCOPy:SDUMp:DATA?", overridden.DataQueryFormat);
            Assert.Equal("D:\\shot.png", overridden.TempPath);
            Assert.Equal(baseRecipe.SaveCommandFormat, overridden.SaveCommandFormat); // untouched
        }

        // ---- capture sequence ----

        [Fact]
        public void CaptureScreen_saves_reads_back_and_deletes_in_order_and_returns_payload()
        {
            byte[] png = { 0x89, (byte)'P', (byte)'N', (byte)'G', 1, 2, 3 };
            var io = new FakeCaptureIo { RawResponse = Ieee4882Block.Frame(png) };
            var vsa = new VsaInstrument(io);

            byte[] image = vsa.CaptureScreen();

            Assert.Equal(png, image);
            int iSave = io.Writes.FindIndex(w => w.StartsWith(":MMEMory:STORe:SCReen"));
            int iOpc = io.Writes.FindIndex(w => w == "*OPC?");
            int iData = io.Writes.FindIndex(w => w.StartsWith(":MMEMory:DATA?"));
            int iDel = io.Writes.FindIndex(w => w.StartsWith(":MMEMory:DELete"));
            Assert.True(iSave >= 0 && iOpc > iSave && iData > iOpc && iDel > iData,
                "Expected save -> *OPC? -> data query -> delete order.");
            // The instrument-side temp path is substituted into each command.
            Assert.Contains(io.Writes, w => w.Contains("ESGCAP.png"));
        }

        [Fact]
        public void CaptureScreen_honours_recipe_overrides()
        {
            var io = new FakeCaptureIo { RawResponse = Ieee4882Block.Frame(new byte[] { 7, 7 }) };
            var vsa = new VsaInstrument(io);

            // A direct-dump recipe: only a data query, no save/cleanup. (With(null) keeps existing
            // fields — the CLI-override semantics — so a direct dump is built explicitly.)
            var recipe = new ScreenCaptureRecipe(":HCOPy:SDUMp:DATA?");
            byte[] image = vsa.CaptureScreen(recipe);

            Assert.Equal(new byte[] { 7, 7 }, image);
            Assert.Contains(":HCOPy:SDUMp:DATA?", io.Writes);
            Assert.DoesNotContain(io.Writes, w => w.StartsWith(":MMEMory:STORe:SCReen"));
        }

        [Fact]
        public void CaptureScreen_throws_a_clear_error_when_transport_cannot_binary_read()
        {
            var vsa = new VsaInstrument(new PlainIo());
            var ex = Assert.Throws<System.NotSupportedException>(() => vsa.CaptureScreen());
            Assert.Contains("binary read", ex.Message);
        }
    }
}
