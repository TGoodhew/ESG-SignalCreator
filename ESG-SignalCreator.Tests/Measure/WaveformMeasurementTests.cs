using System.Collections.Generic;
using EsgSignalCreator.Instruments;
using EsgSignalCreator.Measure;
using EsgSignalCreator.Measure.Results;
using EsgSignalCreator.Visa;
using Xunit;

namespace EsgSignalCreator.Tests.Measure
{
    public class WaveformMeasurementTests
    {
        /// <summary>
        /// Fake transport: records every write/query and returns a canned scalar response for the
        /// <c>:READ:WAVeform?</c> query (other queries return empty / "0").
        /// </summary>
        private sealed class FakeVsa : IInstrument
        {
            public readonly List<string> Writes = new List<string>();
            // Order: peak, mean, mean-avg, peak-avg/aux, peak-to-mean.
            public string WaveformResponse = "-2.0,-10.0,-10.0,-2.0,8.0";

            public string ResourceName => "GPIB0::17::INSTR";
            public bool IsConnected => true;
            public int TimeoutMilliseconds { get; set; }
            public void Write(string command) => Writes.Add(command);
            public string ReadString() => "";

            public string Query(string command)
            {
                Writes.Add(command);
                if (command == ":READ:WAVeform?") return WaveformResponse;
                return command.EndsWith("?") ? "0" : "";
            }

            public void WriteBinaryBlock(byte[] message) { }
            public void Dispose() { }
        }

        [Fact]
        public void Measure_maps_scalars_to_peak_mean_and_peak_to_mean()
        {
            var io = new FakeVsa();
            WaveformResult result = WaveformMeasurement.Measure(new VsaInstrument(io), 1e9);

            Assert.Equal(-2.0, result.PeakPowerDbm, 3);
            Assert.Equal(-10.0, result.MeanPowerDbm, 3);
            Assert.Equal(8.0, result.PeakToMeanDb, 3);
            Assert.Equal(5, result.Raw.Length);
            Assert.Equal("WAVeform", result.Measurement);
        }

        [Fact]
        public void Measure_issues_basic_setup_center_freq_and_read()
        {
            var io = new FakeVsa();
            WaveformMeasurement.Measure(new VsaInstrument(io), 1e9);

            Assert.Contains(":INSTrument:SELect BASIC", io.Writes);
            Assert.Contains(":INITiate:CONTinuous OFF", io.Writes);
            Assert.Contains(io.Writes, w => w.StartsWith(":SENSe:FREQuency:CENTer"));
            Assert.Contains(":READ:WAVeform?", io.Writes);
        }

        [Fact]
        public void Measure_with_short_response_guards_missing_fields_with_nan()
        {
            var io = new FakeVsa { WaveformResponse = "-2.0,-10.0" };
            WaveformResult result = WaveformMeasurement.Measure(new VsaInstrument(io), 1e9);

            Assert.Equal(-2.0, result.PeakPowerDbm, 3);
            Assert.Equal(-10.0, result.MeanPowerDbm, 3);
            Assert.True(double.IsNaN(result.PeakToMeanDb));
            Assert.Equal(2, result.Raw.Length);
        }

        [Fact]
        public void Measure_with_empty_response_does_not_throw()
        {
            var io = new FakeVsa { WaveformResponse = "" };
            WaveformResult result = WaveformMeasurement.Measure(new VsaInstrument(io), 1e9);

            Assert.True(double.IsNaN(result.PeakPowerDbm));
            Assert.True(double.IsNaN(result.MeanPowerDbm));
            Assert.True(double.IsNaN(result.PeakToMeanDb));
            Assert.Empty(result.Raw);
        }
    }
}
