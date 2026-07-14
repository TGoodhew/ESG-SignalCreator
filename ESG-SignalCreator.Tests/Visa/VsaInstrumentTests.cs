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
            // Queued error-queue responses; each :SYSTem:ERRor? dequeues one, then "+0,No error".
            public readonly Queue<string> ErrorQueue = new Queue<string>();

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
                if (command == ":SYSTem:ERRor?")
                    return ErrorQueue.Count > 0 ? ErrorQueue.Dequeue() : "+0,\"No error\"";
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
        [InlineData(VsaModel.E4406A, "Agilent E4406A")]
        [InlineData(VsaModel.N9010A, "Keysight N9010A (EXA)")]
        [InlineData(VsaModel.Unknown, "Unknown")]
        public void DisplayName_is_human_friendly(VsaModel model, string expected)
        {
            Assert.Equal(expected, VsaModels.DisplayName(model));
        }

        // §120: only the two known analyzers are supported; an unidentified model is refused at connect.
        [Theory]
        [InlineData(VsaModel.E4406A, true)]
        [InlineData(VsaModel.N9010A, true)]
        [InlineData(VsaModel.Unknown, false)]
        public void IsSupported_is_true_only_for_known_analyzers(VsaModel model, bool expected)
        {
            Assert.Equal(expected, VsaDialects.IsSupported(model));
        }

        [Theory]
        [InlineData(VsaMeasurement.ChannelPower, "BASIC", "CHPower", "SA", "CHPower")]
        [InlineData(VsaMeasurement.Acp, "BASIC", "ACP", "SA", "ACP")]
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

        [Fact]
        public void Waveform_scalar_layout_is_per_model()
        {
            WaveformScalarLayout e = new E4406ADialect().WaveformScalars;
            Assert.Equal(0, e.PeakIndex);
            Assert.Equal(1, e.MeanIndex);
            Assert.Equal(4, e.PeakToMeanIndex);

            WaveformScalarLayout n = new N9010ADialect().WaveformScalars;
            Assert.Equal(5, n.PeakIndex);   // Maximum, not sample-time at [0]
            Assert.Equal(1, n.MeanIndex);
            Assert.Equal(4, n.PeakToMeanIndex);
        }

        [Fact]
        public void Ccdf_index_and_acp_layout_are_per_model()
        {
            Assert.Equal(1, new E4406ADialect().CcdfScalarResultIndex);
            Assert.Equal(1, new N9010ADialect().CcdfScalarResultIndex); // A.07.05: 10 scalars at index 1, PAPR at [8]
            Assert.False(new N9010ADialect().CcdfResultIsTrace);

            AcpScalarLayout e = new E4406ADialect().AcpScalars;
            Assert.Equal(5, e.OffsetCount);
            Assert.Equal(0, e.UpperAdjacentDbcIndex);
            Assert.Equal(2, e.LowerAdjacentDbcIndex);

            // N9010A A.07.05 (bench-confirmed): :READ:ACP? -> [carrier, lowerAdj, upperAdj], no offset table.
            AcpScalarLayout n = new N9010ADialect().AcpScalars;
            Assert.Equal(0, n.OffsetCount);
            Assert.Equal(2, n.UpperAdjacentDbcIndex);
            Assert.Equal(1, n.LowerAdjacentDbcIndex);
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

        // #120: the user/assistant path (verifyInstalled: true) refuses a mode absent from the catalog.
        [Fact]
        public void SelectMode_refuses_a_mode_not_in_the_catalog_when_verifying()
        {
            var io = new FakeVsa { CatalogResponse = "\"BASIC\",\"GSM\"" };
            var vsa = new VsaInstrument(io);

            var ex = Assert.Throws<System.InvalidOperationException>(() => vsa.SelectMode("WCDMA", verifyInstalled: true));
            Assert.Contains("WCDMA", ex.Message);
            Assert.Contains("BASIC", ex.Message); // message lists what IS installed
            Assert.DoesNotContain(io.Writes, w => w.StartsWith(":INSTrument:SELect WCDMA"));
        }

        [Fact]
        public void SelectMode_allows_an_installed_mode_when_verifying()
        {
            var io = new FakeVsa { CatalogResponse = "\"BASIC\",\"gsm\"" }; // case-insensitive match
            var vsa = new VsaInstrument(io);
            vsa.SelectMode("GSM", verifyInstalled: true);
            Assert.Contains(":INSTrument:SELect GSM", io.Writes);
        }

        [Fact]
        public void SelectMode_default_path_skips_the_catalog_query()
        {
            var io = new FakeVsa();
            var vsa = new VsaInstrument(io);
            vsa.SelectMode("BASIC"); // internal path — verifyInstalled defaults to false
            Assert.Contains(":INSTrument:SELect BASIC", io.Writes);
            // No catalog query issued when not verifying.
            Assert.DoesNotContain(io.Writes, w => w == ":INSTrument:CATalog?");
        }

        // #120: ReadErrorQueue drains every non-zero entry, then stops at code 0.
        [Fact]
        public void ReadErrorQueue_drains_all_nonzero_entries_then_stops()
        {
            var io = new FakeVsa();
            io.ErrorQueue.Enqueue("-221,\"Settings conflict\"");
            io.ErrorQueue.Enqueue("+700,\"Input overload\"");
            io.ErrorQueue.Enqueue("+0,\"No error\"");
            var vsa = new VsaInstrument(io);

            var errors = vsa.ReadErrorQueue();
            Assert.Equal(2, errors.Count);
            Assert.Contains(errors, e => e.Contains("Input overload"));
        }

        [Fact]
        public void ReadErrorQueue_is_empty_when_the_queue_is_clean()
        {
            var vsa = new VsaInstrument(new FakeVsa());
            Assert.Empty(vsa.ReadErrorQueue());
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
