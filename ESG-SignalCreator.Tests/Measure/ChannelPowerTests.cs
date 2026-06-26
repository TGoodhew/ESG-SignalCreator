using System.Collections.Generic;
using System.Linq;
using EsgSignalCreator.Instruments;
using EsgSignalCreator.Measure;
using EsgSignalCreator.Measure.Results;
using EsgSignalCreator.Visa;
using Xunit;

namespace EsgSignalCreator.Tests.Measure
{
    public class ChannelPowerTests
    {
        /// <summary>
        /// Fake transport: records every write/query and returns a canned scalar response for the
        /// <c>:READ:CHPower?</c> query (other queries return empty).
        /// </summary>
        private sealed class FakeVsa : IInstrument
        {
            public readonly List<string> Writes = new List<string>();
            public string ChPowerResponse = "-10.23,-71.5";

            public string ResourceName => "GPIB0::17::INSTR";
            public bool IsConnected => true;
            public int TimeoutMilliseconds { get; set; }
            public void Write(string command) => Writes.Add(command);
            public string ReadString() => "";

            public string Query(string command)
            {
                Writes.Add(command);
                if (command == ":READ:CHPower?") return ChPowerResponse;
                return command.EndsWith("?") ? "0" : "";
            }

            public void WriteBinaryBlock(byte[] message) { }
            public void Dispose() { }
        }

        [Fact]
        public void Measure_maps_scalars_to_total_power_and_psd()
        {
            var io = new FakeVsa();
            ChannelPowerResult result = ChannelPower.Measure(new VsaInstrument(io), 1e9, 1e6);

            Assert.Equal(-10.23, result.TotalPowerDbm, 3);
            Assert.Equal(-71.5, result.PowerSpectralDensityDbmHz, 3);
            Assert.Equal(2, result.Raw.Length);
            Assert.Equal("CHPower", result.Measurement);
        }

        [Fact]
        public void Measure_issues_basic_setup_center_freq_and_read()
        {
            var io = new FakeVsa();
            ChannelPower.Measure(new VsaInstrument(io), 1e9, 1e6);

            Assert.Contains(":INSTrument:SELect BASIC", io.Writes);
            Assert.Contains(":INITiate:CONTinuous OFF", io.Writes);
            Assert.Contains(io.Writes, w => w.StartsWith(":SENSe:FREQuency:CENTer"));
            Assert.Contains(":READ:CHPower?", io.Writes);
        }

        [Fact]
        public void Measure_with_channel_bandwidth_writes_integration_bw()
        {
            var io = new FakeVsa();
            ChannelPower.Measure(new VsaInstrument(io), 1e9, 1e6, 1.23e6);

            Assert.Contains(io.Writes,
                w => w.StartsWith(":SENSe:CHPower:BANDwidth:INTegration") && w.Contains("1230000"));
        }

        [Fact]
        public void Measure_without_channel_bandwidth_omits_integration_bw()
        {
            var io = new FakeVsa();
            ChannelPower.Measure(new VsaInstrument(io), 1e9, 1e6);

            Assert.DoesNotContain(io.Writes, w => w.StartsWith(":SENSe:CHPower:BANDwidth:INTegration"));
        }

        [Fact]
        public void Measure_with_short_response_guards_with_nan()
        {
            var io = new FakeVsa { ChPowerResponse = "-10.23" };
            ChannelPowerResult result = ChannelPower.Measure(new VsaInstrument(io), 1e9, 1e6);

            Assert.Equal(-10.23, result.TotalPowerDbm, 3);
            Assert.True(double.IsNaN(result.PowerSpectralDensityDbmHz));
            Assert.Single(result.Raw);
        }

        [Fact]
        public void Measure_with_empty_response_does_not_throw()
        {
            var io = new FakeVsa { ChPowerResponse = "" };
            ChannelPowerResult result = ChannelPower.Measure(new VsaInstrument(io), 1e9, 1e6);

            Assert.True(double.IsNaN(result.TotalPowerDbm));
            Assert.True(double.IsNaN(result.PowerSpectralDensityDbmHz));
            Assert.Empty(result.Raw);
        }
    }
}
