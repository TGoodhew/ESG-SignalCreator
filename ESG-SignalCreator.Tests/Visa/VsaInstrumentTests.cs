using System.Collections.Generic;
using EsgSignalCreator.Instruments;
using EsgSignalCreator.Visa;
using Xunit;

namespace EsgSignalCreator.Tests.Visa
{
    public class VsaInstrumentTests
    {
        private sealed class FakeVsa : IInstrument
        {
            public readonly List<string> Writes = new List<string>();
            public string IdnResponse = "Agilent Technologies, E4406A, US44210123, A.05.00";
            public string OptResponse = "B7C,200,202";

            public string ResourceName => "GPIB0::17::INSTR";
            public bool IsConnected => true;
            public int TimeoutMilliseconds { get; set; }
            public void Write(string command) => Writes.Add(command);
            public string ReadString() => "";
            public string Query(string command)
            {
                if (command == "*IDN?") return IdnResponse;
                if (command == "*OPT?") return OptResponse;
                if (command == ":SYSTem:ERRor?") return "+0,\"No error\"";
                return "";
            }
            public void WriteBinaryBlock(byte[] message) { }
            public void Dispose() { }
        }

        [Fact]
        public void Identify_and_IsE4406A_recognize_the_analyzer()
        {
            var vsa = new VsaInstrument(new FakeVsa());
            Assert.Equal("E4406A", vsa.Identify().Model);
            Assert.True(vsa.IsE4406A());
        }

        [Fact]
        public void IsE4406A_is_false_for_a_different_model()
        {
            var io = new FakeVsa { IdnResponse = "Agilent Technologies, E4438C, US123, C.05.85" };
            Assert.False(new VsaInstrument(io).IsE4406A());
        }

        [Fact]
        public void Options_are_parsed_and_trimmed()
        {
            var vsa = new VsaInstrument(new FakeVsa { OptResponse = " B7C , 200 ,, 202 " });
            Assert.Equal(new[] { "B7C", "200", "202" }, vsa.Options());
        }

        [Fact]
        public void SelectBasicMode_and_frequency_emit_the_right_scpi()
        {
            var io = new FakeVsa();
            var vsa = new VsaInstrument(io);
            vsa.SelectBasicMode();
            vsa.SetSingleMeasurement();
            vsa.SetCenterFrequencyHz(1e9);

            Assert.Contains(":INSTrument:SELect BASIC", io.Writes);
            Assert.Contains(":INITiate:CONTinuous OFF", io.Writes);
            Assert.Contains(io.Writes, w => w.StartsWith(":SENSe:FREQuency:CENTer") && w.Contains("1000000000"));
        }
    }
}
