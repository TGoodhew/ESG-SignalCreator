using System.Collections.Generic;
using System.Text;
using EsgSignalCreator;
using EsgSignalCreator.Instruments;
using EsgSignalCreator.Waveform;
using Xunit;

namespace EsgSignalCreator.Tests.Control
{
    public class EsgControllerLegacyTests
    {
        private sealed class CapturingInstrument : IInstrument
        {
            public readonly List<byte[]> Blocks = new List<byte[]>();
            public string ResourceName => "FAKE";
            public bool IsConnected => true;
            public int TimeoutMilliseconds { get; set; }
            public void Write(string command) { }
            public string ReadString() => "";
            public string Query(string command) => "0,\"No error\"";
            public void WriteBinaryBlock(byte[] message) => Blocks.Add(message);
            public void Dispose() { }
        }

        private static IqWaveform Dc(int n)
        {
            var i = new double[n];
            var q = new double[n];
            for (int k = 0; k < n; k++) i[k] = 1.0;
            return new IqWaveform(i, q, 1e6);
        }

        private static string Head(byte[] b, int len) => Encoding.ASCII.GetString(b, 0, len);

        [Fact]
        public void DownloadWaveformLegacy_writes_separate_ARBI_then_ARBQ_blocks()
        {
            var io = new CapturingInstrument();
            new EsgController(io).DownloadWaveformLegacy("seg", Dc(64));

            Assert.Equal(2, io.Blocks.Count);
            // 64 samples -> 128 bytes per channel -> header "#3128".
            Assert.StartsWith(":MEMory:DATA \"ARBI:seg\",#3128", Head(io.Blocks[0], 30));
            Assert.StartsWith(":MEMory:DATA \"ARBQ:seg\",#3128", Head(io.Blocks[1], 30));
        }

        [Fact]
        public void DownloadMarkers_writes_an_MKR1_block_one_byte_per_sample()
        {
            var io = new CapturingInstrument();
            var markers = new byte[64];
            markers[0] = 1;
            new EsgController(io).DownloadMarkers("seg", markers);

            Assert.Single(io.Blocks);
            // 64 marker bytes -> header "#264".
            Assert.StartsWith(":MEMory:DATA \"MKR1:seg\",#264", Head(io.Blocks[0], 29));
        }
    }
}
