using System.Collections.Generic;
using EsgSignalCreator.Instruments;
using EsgSignalCreator.Visa;
using Xunit;

namespace EsgSignalCreator.Tests.Visa
{
    public class EsgInstrumentTests
    {
        /// <summary>An in-memory transport that returns canned responses keyed by the queried command.</summary>
        private sealed class FakeInstrument : IInstrument
        {
            public readonly Dictionary<string, string> Responses = new Dictionary<string, string>();
            public readonly List<string> Writes = new List<string>();
            public bool Disposed;

            public string ResourceName => "FAKE::INSTR";
            public bool IsConnected => true;
            public int TimeoutMilliseconds { get; set; }

            public void Write(string command) => Writes.Add(command);
            public string ReadString() => string.Empty;
            public string Query(string command) => Responses.TryGetValue(command, out var r) ? r : string.Empty;
            public void WriteBinaryBlock(byte[] message) { }
            public void Dispose() => Disposed = true;
        }

        [Fact]
        public void Identify_parses_manufacturer_model_serial_firmware_from_IDN()
        {
            var io = new FakeInstrument();
            io.Responses["*IDN?"] = "Agilent Technologies, E4438C, US44440123, C.05.84";
            var esg = new EsgInstrument(io);

            InstrumentIdentity id = esg.Identify();

            Assert.Equal("Agilent Technologies", id.Manufacturer);
            Assert.Equal("E4438C", id.Model);
            Assert.Equal("US44440123", id.Serial);
            Assert.Equal("C.05.84", id.FirmwareRevision);
        }

        [Fact]
        public void Options_parses_four_comma_separated_options()
        {
            var io = new FakeInstrument();
            io.Responses["*OPT?"] = "001,602,UNT,UNU";
            var esg = new EsgInstrument(io);

            string[] opts = esg.Options();

            Assert.Equal(new[] { "001", "602", "UNT", "UNU" }, opts);
        }

        [Fact]
        public void Options_trims_and_removes_empty_entries()
        {
            var io = new FakeInstrument();
            io.Responses["*OPT?"] = " 001 , , 602 ,";
            var esg = new EsgInstrument(io);

            Assert.Equal(new[] { "001", "602" }, esg.Options());
        }

        [Fact]
        public void HasBasebandGenerator_true_when_baseband_option_present()
        {
            var io = new FakeInstrument();
            io.Responses["*OPT?"] = "001,602,UNT,UNU";
            var esg = new EsgInstrument(io);

            Assert.True(esg.HasBasebandGenerator());
        }

        [Fact]
        public void HasBasebandGenerator_false_when_no_baseband_option()
        {
            var io = new FakeInstrument();
            io.Responses["*OPT?"] = "UNT,UNU";
            var esg = new EsgInstrument(io);

            Assert.False(esg.HasBasebandGenerator());
        }

        [Fact]
        public void BuildGpibResource_formats_board_and_address()
        {
            Assert.Equal("GPIB0::19::INSTR", EsgInstrument.BuildGpibResource(0, 19));
        }

        [Fact]
        public void Passthrough_Write_and_Query_reach_the_transport()
        {
            var io = new FakeInstrument();
            io.Responses[":FOO?"] = "bar";
            var esg = new EsgInstrument(io);

            esg.Write(":FOO ON");
            string answer = esg.Query(":FOO?");

            Assert.Contains(":FOO ON", io.Writes);
            Assert.Equal("bar", answer);
        }

        [Fact]
        public void ResourceName_and_TimeoutMilliseconds_passthrough_to_transport()
        {
            var io = new FakeInstrument();
            var esg = new EsgInstrument(io);

            esg.TimeoutMilliseconds = 1234;

            Assert.Equal("FAKE::INSTR", esg.ResourceName);
            Assert.Equal(1234, esg.TimeoutMilliseconds);
            Assert.Equal(1234, io.TimeoutMilliseconds);
        }

        [Fact]
        public void Dispose_disposes_the_wrapped_transport()
        {
            var io = new FakeInstrument();
            var esg = new EsgInstrument(io);

            esg.Dispose();

            Assert.True(io.Disposed);
        }
    }
}
