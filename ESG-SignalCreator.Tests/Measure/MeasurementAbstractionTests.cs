using System.Collections.Generic;
using EsgSignalCreator.Instruments;
using EsgSignalCreator.Measure;
using EsgSignalCreator.Visa;
using Xunit;

namespace EsgSignalCreator.Tests.Measure
{
    public class MeasurementAbstractionTests
    {
        private sealed class FakeVsa : IInstrument
        {
            public readonly List<string> Writes = new List<string>();
            public string ScalarResponse = "-10.2,-50.1";
            public string ResourceName => "GPIB0::17::INSTR";
            public bool IsConnected => true;
            public int TimeoutMilliseconds { get; set; }
            public void Write(string command) => Writes.Add(command);
            public string ReadString() => "";
            public string Query(string command)
            {
                Writes.Add(command);
                return command.EndsWith("?") ? ScalarResponse : "";
            }
            public void WriteBinaryBlock(byte[] message) { }
            public void Dispose() { }
        }

        [Theory]
        [InlineData("CHPower", 1, ":READ:CHPower?")]
        [InlineData("SPECtrum", 4, ":READ:SPECtrum4?")]
        public void Read_command_uses_root_and_index(string root, int n, string expected)
        {
            Assert.Equal(expected, VsaCommands.Read(root, n));
        }

        [Fact]
        public void All_four_verbs_build_correctly()
        {
            Assert.Equal(":MEASure:CHPower?", VsaCommands.Measure("CHPower"));
            Assert.Equal(":FETCh:SPECtrum2?", VsaCommands.Fetch("SPECtrum", 2));
            Assert.Equal(":CONFigure:CHPower", VsaCommands.Configure("CHPower"));
        }

        [Fact]
        public void ParseScalars_handles_whitespace_quotes_and_exponents()
        {
            double[] v = VsaScalarParser.ParseScalars("-10.2, -50.1 ,\"1e3\", ,bad");
            Assert.Equal(new[] { -10.2, -50.1, 1000.0 }, v);
        }

        [Fact]
        public void BasicMeasurement_setup_then_read_parses_scalars()
        {
            var io = new FakeVsa { ScalarResponse = "-10.2,-50.1" };
            var m = new BasicMeasurement(new VsaInstrument(io));
            m.Setup(1e9, 1e6);
            double[] result = m.Read("CHPower");

            Assert.Equal(new[] { -10.2, -50.1 }, result);
            Assert.Contains(":INSTrument:SELect BASIC", io.Writes);
            Assert.Contains(io.Writes, w => w.StartsWith(":SENSe:FREQuency:CENTer"));
            Assert.Contains(":READ:CHPower?", io.Writes);
        }
    }
}
