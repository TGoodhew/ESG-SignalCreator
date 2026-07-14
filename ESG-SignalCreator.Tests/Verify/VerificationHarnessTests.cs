using System;
using System.Linq;
using EsgSignalCreator.Instruments;
using EsgSignalCreator.Model;
using EsgSignalCreator.Verify;
using EsgSignalCreator.Visa;
using Xunit;

namespace EsgSignalCreator.Tests.Verify
{
    public class VerificationHarnessTests
    {
        /// <summary>Fake analyzer returning canned channel-power, CCDF and marker responses.</summary>
        private sealed class FakeVsa : IInstrument
        {
            public string ResourceName => "GPIB0::17::INSTR";
            public bool IsConnected => true;
            public int TimeoutMilliseconds { get; set; }
            public void Write(string command) { }
            public string ReadString() => "";
            public string Query(string command)
            {
                if (command == ":READ:CHPower?") return "-10.05,-71.0";
                if (command == ":READ:PSTatistic?") return "-10,50,3,5,7,9,10,11,0.20,1000000"; // PAPR at index 8 = 0.20
                if (command == ":CALCulate:SPECtrum:MARKer1:X?") return "1.001e9";
                if (command == ":CALCulate:SPECtrum:MARKer1:Y?") return "-10.1";
                return command.EndsWith("?") ? "0" : "";
            }
            public void WriteBinaryBlock(byte[] message) { }
            public void Dispose() { }
        }

        private static WaveformModel Cw(int n)
        {
            var i = new float[n];
            var q = new float[n];
            for (int k = 0; k < n; k++) { i[k] = (float)Math.Cos(2 * Math.PI * k / 8); q[k] = (float)Math.Sin(2 * Math.PI * k / 8); }
            return new WaveformModel(i, q, 10e6, "CW");
        }

        [Fact]
        public void Verify_returns_power_papr_and_tone_results_all_passing()
        {
            var vsa = new VsaInstrument(new FakeVsa());
            var results = VerificationHarness.Verify(vsa, Cw(64), 1e9, -10.0, new VerificationProfile(), toneOffsetHz: 1e6);

            Assert.Equal(3, results.Count);

            VerificationResult power = results.First(r => r.Metric == "Channel power");
            Assert.Equal(-10.0, power.Expected, 6);
            Assert.Equal(-10.05, power.Measured, 6);
            Assert.True(power.Pass);

            VerificationResult papr = results.First(r => r.Metric == "PAPR");
            Assert.Equal(0.20, papr.Measured, 6);
            Assert.True(papr.Pass); // constant-envelope CW -> expected ~0, measured 0.2, within 2.5

            VerificationResult tone = results.First(r => r.Metric == "Tone frequency");
            Assert.Equal(1.001e9, tone.Expected, 0);
            Assert.Equal(1.001e9, tone.Measured, 0);
            Assert.True(tone.Pass);

            Assert.True(VerificationHarness.AllPass(results));
        }

        // A 50%-AM envelope (I = 1 + 0.5·sin, Q = 0) over a whole number of modulation cycles: crest
        // factor = 10·log10(2.25 / 1.125) = 3.01 dB.
        private static WaveformModel Am(int cycles)
        {
            int n = 32 * cycles;
            var i = new float[n];
            var q = new float[n];
            for (int k = 0; k < n; k++) i[k] = (float)(1.0 + 0.5 * Math.Sin(2 * Math.PI * k / 32));
            return new WaveformModel(i, q, 10e6, "AM");
        }

        [Fact]
        public void Verify_subtracts_the_crest_factor_from_expected_channel_power()
        {
            // The ARB is peak-normalized, so a 3 dB-crest signal's RMS reads ~3 dB below the commanded
            // level; the expected channel power must subtract the crest (#125/#130 AM & IQ failures).
            var vsa = new VsaInstrument(new FakeVsa());
            var results = VerificationHarness.Verify(vsa, Am(10), 1e9, -10.0, new VerificationProfile());

            VerificationResult power = results.First(r => r.Metric == "Channel power");
            Assert.Equal(-13.01, power.Expected, 2);   // -10 commanded - 0 path loss - 3.01 crest

            VerificationResult papr = results.First(r => r.Metric == "PAPR");
            Assert.Equal(3.01, papr.Expected, 2);       // expected PAPR == the crest factor
        }

        [Fact]
        public void Verify_applies_path_loss_to_the_expected_power()
        {
            var vsa = new VsaInstrument(new FakeVsa());
            var profile = new VerificationProfile { PathLossDb = 6.0 };
            var results = VerificationHarness.Verify(vsa, Cw(64), 1e9, -4.0, profile);

            VerificationResult power = results.First(r => r.Metric == "Channel power");
            Assert.Equal(-10.0, power.Expected, 6); // -4 dBm commanded - 6 dB path loss
            Assert.True(power.Pass);                  // measured -10.05 within ±3
        }

        [Fact]
        public void Verify_without_tone_offset_omits_the_frequency_check()
        {
            var vsa = new VsaInstrument(new FakeVsa());
            var results = VerificationHarness.Verify(vsa, Cw(64), 1e9, -10.0, new VerificationProfile());
            Assert.DoesNotContain(results, r => r.Metric == "Tone frequency");
        }

        [Fact]
        public void A_failing_metric_makes_AllPass_false()
        {
            var r = new VerificationResult("Channel power", -10.0, -20.0, 3.0, "dBm");
            Assert.False(r.Pass);
            Assert.False(VerificationHarness.AllPass(new[] { r }));
        }
    }
}
