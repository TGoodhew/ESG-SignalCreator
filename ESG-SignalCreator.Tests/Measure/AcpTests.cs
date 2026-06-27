using System.Collections.Generic;
using EsgSignalCreator.Instruments;
using EsgSignalCreator.Measure;
using EsgSignalCreator.Measure.Results;
using EsgSignalCreator.Visa;
using Xunit;

namespace EsgSignalCreator.Tests.Measure
{
    public class AcpTests
    {
        /// <summary>
        /// Fake transport: records every write/query and returns a canned 24-value Basic-mode ACP
        /// scalar response for <c>:READ:ACP?</c> (other queries return "0"/empty).
        /// </summary>
        private sealed class FakeVsa : IInstrument
        {
            // Basic-mode (Total-power reference) 24-value layout, with predictable values:
            //  [0] upper-adj rel, [1] upper-adj abs, [2] lower-adj rel, [3] lower-adj abs,
            //  then offsets 1..5: neg rel, neg abs, pos rel, pos abs.
            //   offset k (1..5): neg rel = -40-k, neg abs = -50-k, pos rel = -60-k, pos abs = -70-k
            public string AcpResponse =
                "-30,-40,-31,-41," +   // upper-adj rel/abs, lower-adj rel/abs
                "-41,-51,-61,-71," +   // offset 1
                "-42,-52,-62,-72," +   // offset 2
                "-43,-53,-63,-73," +   // offset 3
                "-44,-54,-64,-74," +   // offset 4
                "-45,-55,-65,-75";     // offset 5

            public readonly List<string> Writes = new List<string>();
            public string ResourceName => "GPIB0::17::INSTR";
            public bool IsConnected => true;
            public int TimeoutMilliseconds { get; set; }
            public void Write(string command) => Writes.Add(command);
            public string ReadString() => "";
            public string Query(string command)
            {
                Writes.Add(command);
                if (command == ":READ:ACP?") return AcpResponse;
                return command.EndsWith("?") ? "0" : "";
            }
            public void WriteBinaryBlock(byte[] message) { }
            public void Dispose() { }
        }

        [Fact]
        public void Measure_parses_adjacent_channel_relative_powers()
        {
            var io = new FakeVsa();
            AcpResult result = Acp.Measure(new VsaInstrument(io), 1e9, 1.35e6);

            Assert.Equal(-30.0, result.UpperAdjacentDbc, 6);
            Assert.Equal(-31.0, result.LowerAdjacentDbc, 6);
            Assert.Equal("ACP", result.Measurement);
            Assert.Equal(24, result.Raw.Length);
        }

        [Fact]
        public void Measure_picks_lower_and_upper_offset_relative_powers()
        {
            var io = new FakeVsa();
            AcpResult result = Acp.Measure(new VsaInstrument(io), 1e9, 1.35e6);

            Assert.Equal(new[] { -41.0, -42.0, -43.0, -44.0, -45.0 }, result.LowerOffsetsDbc);
            Assert.Equal(new[] { -61.0, -62.0, -63.0, -64.0, -65.0 }, result.UpperOffsetsDbc);
        }

        [Fact]
        public void Measure_picks_lower_and_upper_offset_absolute_powers()
        {
            var io = new FakeVsa();
            AcpResult result = Acp.Measure(new VsaInstrument(io), 1e9, 1.35e6);

            Assert.Equal(new[] { -51.0, -52.0, -53.0, -54.0, -55.0 }, result.LowerOffsetsDbm);
            Assert.Equal(new[] { -71.0, -72.0, -73.0, -74.0, -75.0 }, result.UpperOffsetsDbm);
        }

        [Fact]
        public void Measure_issues_basic_setup_integration_bw_and_read_but_no_span()
        {
            var io = new FakeVsa();
            Acp.Measure(new VsaInstrument(io), 1e9, 1.35e6);

            Assert.Contains(":INSTrument:SELect BASIC", io.Writes);
            Assert.Contains(io.Writes, w => w.StartsWith(":SENSe:FREQuency:CENTer"));
            Assert.Contains(io.Writes, w => w.StartsWith(":SENSe:ACP:BANDwidth:INTegration"));
            // The invalid global/ACP span must never be sent (it returns -113 on the unit).
            Assert.DoesNotContain(io.Writes, w => w.StartsWith(":SENSe:ACP:FREQuency:SPAN"));
            Assert.Contains(":READ:ACP?", io.Writes);
        }

        [Fact]
        public void Measure_with_zero_bandwidth_omits_the_integration_bw_command()
        {
            var io = new FakeVsa();
            Acp.Measure(new VsaInstrument(io), 1e9, 0);

            Assert.DoesNotContain(io.Writes, w => w.StartsWith(":SENSe:ACP:BANDwidth:INTegration"));
            Assert.Contains(":READ:ACP?", io.Writes);
        }

        [Fact]
        public void Measure_with_empty_response_does_not_throw()
        {
            var io = new FakeVsa { AcpResponse = "" };
            AcpResult result = Acp.Measure(new VsaInstrument(io), 1e9, 1.35e6);

            Assert.True(double.IsNaN(result.UpperAdjacentDbc));
            Assert.Empty(result.LowerOffsetsDbc);
            Assert.Empty(result.UpperOffsetsDbc);
            Assert.Empty(result.Raw);
        }
    }
}
