using System.Collections.Generic;
using EsgSignalCreator.Instruments;
using EsgSignalCreator.Measure;
using EsgSignalCreator.Measure.Results;
using EsgSignalCreator.Visa;
using Xunit;

namespace EsgSignalCreator.Tests.Measure
{
    public class SpectrumMarkerTests
    {
        /// <summary>
        /// Fake transport that records every write/query and answers marker X/Y queries with
        /// canned values. Other queries return a generic scalar so the acquisition READ parses.
        /// </summary>
        private sealed class FakeVsa : IInstrument
        {
            public readonly List<string> Writes = new List<string>();
            public string MarkerX = "1.001e9";
            public string MarkerY = "-10.4";
            public string ResourceName => "GPIB0::17::INSTR";
            public bool IsConnected => true;
            public int TimeoutMilliseconds { get; set; }
            public void Write(string command) => Writes.Add(command);
            public string ReadString() => "";

            public string Query(string command)
            {
                Writes.Add(command);
                if (command == ":CALCulate:SPECtrum:MARKer1:X?") return MarkerX;
                if (command == ":CALCulate:SPECtrum:MARKer1:Y?") return MarkerY;
                return command.EndsWith("?") ? "-30.0" : "";
            }

            public void WriteBinaryBlock(byte[] message) { }
            public void Dispose() { }
        }

        [Fact]
        public void MeasurePeak_returns_marker_frequency_and_power()
        {
            var io = new FakeVsa();
            SpectrumResult result = SpectrumMarker.MeasurePeak(new VsaInstrument(io), 1e9, 1e6);

            Assert.Equal(1.001e9, result.MarkerFrequencyHz, 3);
            Assert.Equal(-10.4, result.MarkerPowerDbm, 6);
            Assert.Equal("SPECtrum", result.Measurement);
        }

        [Fact]
        public void MeasurePeak_issues_setup_peak_search_and_marker_queries()
        {
            var io = new FakeVsa();
            SpectrumMarker.MeasurePeak(new VsaInstrument(io), 1e9, 1e6);

            // Basic-mode setup + center frequency.
            Assert.Contains(":INSTrument:SELect BASIC", io.Writes);
            Assert.Contains(io.Writes, w => w.StartsWith(":SENSe:FREQuency:CENTer"));

            // Spectrum acquisition.
            Assert.Contains(":READ:SPECtrum?", io.Writes);

            // Marker assigned to a trace, peak search, then X/Y queries (numbered marker).
            Assert.Contains(":CALCulate:SPECtrum:MARKer1:TRACe ASP", io.Writes);
            Assert.Contains(":CALCulate:SPECtrum:MARKer1:MAXimum", io.Writes);
            Assert.Contains(":CALCulate:SPECtrum:MARKer1:X?", io.Writes);
            Assert.Contains(":CALCulate:SPECtrum:MARKer1:Y?", io.Writes);
        }

        [Fact]
        public void MeasurePeak_with_empty_marker_responses_does_not_throw()
        {
            var io = new FakeVsa { MarkerX = "", MarkerY = "" };
            SpectrumResult result = SpectrumMarker.MeasurePeak(new VsaInstrument(io), 1e9, 1e6);

            Assert.True(double.IsNaN(result.MarkerFrequencyHz));
            Assert.True(double.IsNaN(result.MarkerPowerDbm));
        }
    }
}
