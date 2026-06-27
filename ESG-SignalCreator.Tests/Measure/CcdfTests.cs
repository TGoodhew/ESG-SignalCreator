using System.Collections.Generic;
using EsgSignalCreator.Instruments;
using EsgSignalCreator.Measure;
using EsgSignalCreator.Measure.Results;
using EsgSignalCreator.Visa;
using Xunit;

namespace EsgSignalCreator.Tests.Measure
{
    public class CcdfTests
    {
        /// <summary>
        /// Fake transport: records every write/query and returns a canned scalar response for the
        /// <c>:READ:PSTatistic?</c> query (other queries return empty/zero).
        /// </summary>
        private sealed class FakeVsa : IInstrument
        {
            public readonly List<string> Writes = new List<string>();

            // 10 values, in Programmer's Guide order; the 9th (8.4) is the peak/PAPR.
            public string PstatResponse = "-10.0,50.0,3.0,5.5,7.2,8.9,10.1,11.0,8.4,1000";

            public string ResourceName => "GPIB0::18::INSTR";
            public bool IsConnected => true;
            public int TimeoutMilliseconds { get; set; }
            public void Write(string command) => Writes.Add(command);
            public string ReadString() => "";

            public string Query(string command)
            {
                Writes.Add(command);
                if (command == ":READ:PSTatistic?") return PstatResponse;
                return command.EndsWith("?") ? "0" : "";
            }

            public void WriteBinaryBlock(byte[] message) { }
            public void Dispose() { }
        }

        [Fact]
        public void Measure_maps_scalars_to_average_papr_and_probability()
        {
            var io = new FakeVsa();
            CcdfResult result = Ccdf.Measure(new VsaInstrument(io), 1e9);

            Assert.Equal(-10.0, result.AveragePowerDbm, 3);
            Assert.Equal(50.0, result.ProbabilityAtAveragePercent, 3);
            Assert.Equal(8.4, result.PaprDb, 3);
            Assert.Equal(10, result.Raw.Length);
            Assert.Equal("PSTatistic", result.Measurement);
        }

        [Fact]
        public void Measure_issues_basic_setup_center_freq_and_read()
        {
            var io = new FakeVsa();
            Ccdf.Measure(new VsaInstrument(io), 1e9);

            Assert.Contains(":INSTrument:SELect BASIC", io.Writes);
            Assert.Contains(":INITiate:CONTinuous OFF", io.Writes);
            Assert.Contains(io.Writes, w => w.StartsWith(":SENSe:FREQuency:CENTer"));
            Assert.Contains(":READ:PSTatistic?", io.Writes);
        }

        [Fact]
        public void Measure_does_not_send_a_span_command()
        {
            var io = new FakeVsa();
            Ccdf.Measure(new VsaInstrument(io), 1e9);

            Assert.DoesNotContain(io.Writes, w => w.Contains(":FREQuency:SPAN"));
        }

        [Fact]
        public void Measure_with_short_response_guards_with_nan()
        {
            var io = new FakeVsa { PstatResponse = "-10.0,50.0" };
            CcdfResult result = Ccdf.Measure(new VsaInstrument(io), 1e9);

            Assert.Equal(-10.0, result.AveragePowerDbm, 3);
            Assert.Equal(50.0, result.ProbabilityAtAveragePercent, 3);
            Assert.True(double.IsNaN(result.PaprDb));
            Assert.Equal(2, result.Raw.Length);
        }

        [Fact]
        public void Measure_with_empty_response_does_not_throw()
        {
            var io = new FakeVsa { PstatResponse = "" };
            CcdfResult result = Ccdf.Measure(new VsaInstrument(io), 1e9);

            Assert.True(double.IsNaN(result.AveragePowerDbm));
            Assert.True(double.IsNaN(result.ProbabilityAtAveragePercent));
            Assert.True(double.IsNaN(result.PaprDb));
            Assert.Empty(result.Raw);
        }
    }
}
