using System.Collections.Generic;
using System.Linq;
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
            public string IdnResponse = "Agilent Technologies, E4406A, US44210123, A.05.00";
            public string ResourceName => "GPIB0::17::INSTR";
            public bool IsConnected => true;
            public int TimeoutMilliseconds { get; set; }
            public void Write(string command) => Writes.Add(command);
            public string ReadString() => "";
            public string Query(string command)
            {
                Writes.Add(command);
                if (command == "*IDN?") return IdnResponse;
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
            m.Setup(VsaMeasurement.ChannelPower, 1e9);
            double[] result = m.Read("CHPower");

            Assert.Equal(new[] { -10.2, -50.1 }, result);
            Assert.Contains(":INSTrument:SELect BASIC", io.Writes);
            Assert.Contains(io.Writes, w => w.StartsWith(":SENSe:FREQuency:CENTer"));
            Assert.Contains(":READ:CHPower?", io.Writes);
        }

        // #107: Setup enters the correct instrument mode per model. On the E4406A every measurement is
        // BASIC; on the N9010A, Channel Power/ACP/CCDF are SA-mode while Spectrum/Waveform stay BASIC.
        [Theory]
        [InlineData("Agilent Technologies, E4406A, US1, A.05.00", ":INSTrument:SELect BASIC")]
        [InlineData("Keysight Technologies,N9010A,MY51234567,A.20.14", ":INSTrument:SELect SA")]
        public void Setup_selects_the_right_mode_for_channel_power_per_model(string idn, string expectedMode)
        {
            var io = new FakeVsa { IdnResponse = idn };
            new BasicMeasurement(new VsaInstrument(io)).Setup(VsaMeasurement.ChannelPower, 1e9);
            Assert.Contains(expectedMode, io.Writes);
        }

        [Fact]
        public void Setup_selects_basic_for_spectrum_on_the_n9010a()
        {
            var io = new FakeVsa { IdnResponse = "Keysight Technologies,N9010A,MY51234567,A.20.14" };
            new BasicMeasurement(new VsaInstrument(io)).Setup(VsaMeasurement.Spectrum, 1e9);
            Assert.Contains(":INSTrument:SELect BASIC", io.Writes);
            Assert.DoesNotContain(":INSTrument:SELect SA", io.Writes);
        }

        // #110: the WAVeform scalar ordering differs by model — peak is at [0] on the E4406A but at [5]
        // (the Maximum) on the N9010A; mean [1] and peak-to-mean [4] are shared.
        [Theory]
        [InlineData("Agilent Technologies, E4406A, US1, A.05.00", 1.0)]
        [InlineData("Keysight Technologies,N9010A,MY1,A.20.14", 6.0)]
        public void Waveform_peak_uses_the_model_specific_index(string idn, double expectedPeak)
        {
            var io = new FakeVsa { IdnResponse = idn, ScalarResponse = "1,2,3,4,5,6" };
            var wf = WaveformMeasurement.Measure(new VsaInstrument(io), 1e9);
            Assert.Equal(expectedPeak, wf.PeakPowerDbm, 9);
            Assert.Equal(2.0, wf.MeanPowerDbm, 9);   // mean at [1] on both
            Assert.Equal(5.0, wf.PeakToMeanDb, 9);   // peak-to-mean at [4] on both
        }

        // #110: the Spectrum marker workflow is identical SCPI on both models (only the mode differs,
        // and Spectrum is BASIC on both).
        [Theory]
        [InlineData("Agilent Technologies, E4406A, US1, A.05.00")]
        [InlineData("Keysight Technologies,N9010A,MY1,A.20.14")]
        public void Spectrum_marker_emits_the_same_scpi_for_both_models(string idn)
        {
            var io = new FakeVsa { IdnResponse = idn, ScalarResponse = "1000000000,-20" };
            SpectrumMarker.MeasurePeak(new VsaInstrument(io), 1e9, 5e6);
            Assert.Contains(":INSTrument:SELect BASIC", io.Writes);
            Assert.Contains(io.Writes, w => w.StartsWith(":SENSe:SPECtrum:FREQuency:SPAN"));
            Assert.Contains(":CALCulate:SPECtrum:MARKer1:TRACe ASP", io.Writes);
            Assert.Contains(":CALCulate:SPECtrum:MARKer1:MAXimum", io.Writes);
            Assert.Contains(":CALCulate:SPECtrum:MARKer1:X?", io.Writes);
            Assert.Contains(":CALCulate:SPECtrum:MARKer1:Y?", io.Writes);
        }

        // #111: Channel Power is cross-model (same root/config/order); only the mode differs (#107).
        [Theory]
        [InlineData("Agilent Technologies, E4406A, US1, A.05.00")]
        [InlineData("Keysight Technologies,N9010A,MY1,A.20.14")]
        public void ChannelPower_emits_the_same_scpi_for_both_models(string idn)
        {
            var io = new FakeVsa { IdnResponse = idn, ScalarResponse = "-10.0,-70.0" };
            var r = ChannelPower.Measure(new VsaInstrument(io), 1e9, spanHz: 1e6, channelBandwidthHz: 1e6);
            Assert.Contains(io.Writes, w => w.StartsWith(":SENSe:CHPower:FREQuency:SPAN"));
            Assert.Contains(io.Writes, w => w.StartsWith(":SENSe:CHPower:BANDwidth:INTegration"));
            Assert.Contains(":READ:CHPower?", io.Writes);
            Assert.Equal(-10.0, r.TotalPowerDbm, 9);
            Assert.Equal(-70.0, r.PowerSpectralDensityDbmHz, 9);
        }

        // #111: CCDF scalars live at n=1 on the E4406A but n=2 on the N9010A; PAPR stays at [8].
        [Theory]
        [InlineData("Agilent Technologies, E4406A, US1, A.05.00", ":READ:PSTatistic?")]
        [InlineData("Keysight Technologies,N9010A,MY1,A.20.14", ":READ:PSTatistic2?")]
        public void Ccdf_reads_scalars_at_the_model_specific_index(string idn, string expectedRead)
        {
            var io = new FakeVsa { IdnResponse = idn, ScalarResponse = "1,2,3,4,5,6,7,8,9,10" };
            var r = Ccdf.Measure(new VsaInstrument(io), 1e9);
            Assert.Contains(expectedRead, io.Writes);
            Assert.Equal(1.0, r.AveragePowerDbm, 9);
            Assert.Equal(9.0, r.PaprDb, 9);   // peak power at [8]
        }

        // #111: ACP root, offset count and adjacent-channel positions are per model.
        [Fact]
        public void Acp_root_and_layout_are_per_model()
        {
            string vec = string.Join(",", Enumerable.Range(0, 32)); // value == index

            var e = new FakeVsa { IdnResponse = "Agilent Technologies, E4406A, US1, A.05.00", ScalarResponse = vec };
            var er = Acp.Measure(new VsaInstrument(e), 1e9);
            Assert.Contains(":READ:ACP?", e.Writes);
            Assert.Equal(0.0, er.UpperAdjacentDbc, 9);
            Assert.Equal(2.0, er.LowerAdjacentDbc, 9);
            Assert.Equal(5, er.LowerOffsetsDbc.Length);

            var n = new FakeVsa { IdnResponse = "Keysight Technologies,N9010A,MY1,A.20.14", ScalarResponse = vec };
            var nr = Acp.Measure(new VsaInstrument(n), 1e9);
            Assert.Contains(":READ:ACPower?", n.Writes);
            Assert.Equal(6.0, nr.UpperAdjacentDbc, 9);   // upper offset A rel
            Assert.Equal(4.0, nr.LowerAdjacentDbc, 9);   // lower offset A rel
            Assert.Equal(6, nr.LowerOffsetsDbc.Length);
        }
    }
}
