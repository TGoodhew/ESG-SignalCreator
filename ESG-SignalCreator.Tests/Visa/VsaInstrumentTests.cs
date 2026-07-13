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
            public string CatalogResponse = "";

            public string ResourceName => "GPIB0::17::INSTR";
            public bool IsConnected => true;
            public int TimeoutMilliseconds { get; set; }
            public void Write(string command) => Writes.Add(command);
            public string ReadString() => "";
            public string Query(string command)
            {
                if (command == "*IDN?") return IdnResponse;
                if (command == "*OPT?") return OptResponse;
                if (command == ":INSTrument:CATalog?") return CatalogResponse;
                if (command == ":SYSTem:ERRor?") return "+0,\"No error\"";
                return "";
            }
            public void WriteBinaryBlock(byte[] message) { }
            public void Dispose() { }
        }

        [Fact]
        public void Identify_and_model_recognize_the_e4406a()
        {
            var vsa = new VsaInstrument(new FakeVsa());
            Assert.Equal("E4406A", vsa.Identify().Model);
            Assert.Equal(VsaModel.E4406A, vsa.Model);
            Assert.True(vsa.IsModel(VsaModel.E4406A));
            Assert.False(vsa.IsModel(VsaModel.N9010A));
            Assert.IsType<E4406ADialect>(vsa.Dialect);
        }

        [Fact]
        public void Model_recognizes_the_n9010a_from_keysight_idn()
        {
            var io = new FakeVsa { IdnResponse = "Keysight Technologies,N9010A,MY51234567,A.20.14" };
            var vsa = new VsaInstrument(io);
            Assert.Equal(VsaModel.N9010A, vsa.Model);
            Assert.True(vsa.IsModel(VsaModel.N9010A));
            Assert.False(vsa.IsModel(VsaModel.E4406A));
            Assert.IsType<N9010ADialect>(vsa.Dialect);
        }

        [Fact]
        public void Model_is_unknown_for_a_different_model()
        {
            var io = new FakeVsa { IdnResponse = "Agilent Technologies, E4438C, US123, C.05.85" };
            var vsa = new VsaInstrument(io);
            Assert.Equal(VsaModel.Unknown, vsa.Model);
            Assert.False(vsa.IsModel(VsaModel.E4406A));
        }

        [Theory]
        [InlineData(VsaMeasurement.ChannelPower, "BASIC", "CHPower", "SA", "CHPower")]
        [InlineData(VsaMeasurement.Acp, "BASIC", "ACP", "SA", "ACPower")]
        [InlineData(VsaMeasurement.Ccdf, "BASIC", "PSTatistic", "SA", "PSTatistic")]
        [InlineData(VsaMeasurement.Spectrum, "BASIC", "SPECtrum", "BASIC", "SPECtrum")]
        [InlineData(VsaMeasurement.Waveform, "BASIC", "WAVeform", "BASIC", "WAVeform")]
        public void Dialects_map_mode_and_root_per_model(
            VsaMeasurement meas, string e4406Mode, string e4406Root, string n9010Mode, string n9010Root)
        {
            IVsaDialect e4406 = new E4406ADialect();
            IVsaDialect n9010 = new N9010ADialect();

            Assert.Equal(e4406Mode, e4406.InstrumentModeFor(meas));
            Assert.Equal(e4406Root, e4406.RootFor(meas));
            Assert.False(e4406.HasGlobalSpan);

            Assert.Equal(n9010Mode, n9010.InstrumentModeFor(meas));
            Assert.Equal(n9010Root, n9010.RootFor(meas));
            Assert.True(n9010.HasGlobalSpan);
        }

        // #107: ModeCatalog must parse both the E4406A per-item-quoted list and the X-Series/N9010A
        // single-quoted CSV.
        [Theory]
        [InlineData("\"BASIC\",\"GSM\",\"WCDMA\"")]              // E4406A dialect
        [InlineData("\"SA,PNOISE,BASIC,GSM,WCDMA\"")]           // X-Series/N9010A dialect
        public void ModeCatalog_parses_both_response_dialects(string catalog)
        {
            var vsa = new VsaInstrument(new FakeVsa { CatalogResponse = catalog });
            string[] modes = vsa.ModeCatalog();
            Assert.Contains("BASIC", modes);
            Assert.Contains("GSM", modes);
            Assert.Contains("WCDMA", modes);
            Assert.DoesNotContain(modes, m => m.Contains("\""));
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

        [Fact]
        public void SetContinuous_toggles_init_continuous()
        {
            var io = new FakeVsa();
            var vsa = new VsaInstrument(io);
            vsa.SetContinuous(true);
            vsa.SetContinuous(false);
            Assert.Contains(":INITiate:CONTinuous ON", io.Writes);
            Assert.Contains(":INITiate:CONTinuous OFF", io.Writes);
        }
    }
}
