using System;
using EsgSignalCreator.Model;
using EsgSignalCreator.Personalities.BroadcastRadio;
using Xunit;

namespace EsgSignalCreator.Tests.Personalities
{
    public class BroadcastRadioPersonalityTests
    {
        [Fact]
        public void Output_length_and_sample_rate_match_config()
        {
            var cfg = new BroadcastRadioConfig { SampleRateHz = 400e3, Length = 8000 };
            WaveformModel wf = Calc(cfg);
            Assert.Equal(8000, wf.Length);
            Assert.Equal(400e3, wf.SampleRateHz, 3);
        }

        [Fact]
        public void Fm_is_constant_envelope()
        {
            WaveformModel wf = Calc(new BroadcastRadioConfig { Length = 8000, Stereo = true });
            for (int s = 0; s < wf.Length; s++)
            {
                double mag = Math.Sqrt(wf.I[s] * wf.I[s] + wf.Q[s] * wf.Q[s]);
                Assert.Equal(1.0, mag, 4);
            }
        }

        [Fact]
        public void Stereo_and_mono_differ()
        {
            var mono = Calc(new BroadcastRadioConfig { Length = 8000, Stereo = false });
            var stereo = Calc(new BroadcastRadioConfig { Length = 8000, Stereo = true });
            double maxDiff = 0;
            for (int s = 0; s < mono.Length; s++)
                maxDiff = Math.Max(maxDiff, Math.Abs(mono.I[s] - stereo.I[s]));
            Assert.True(maxDiff > 0.1, "stereo multiplex should change the signal");
        }

        [Fact]
        public void Low_sample_rate_for_stereo_is_rejected()
        {
            var cfg = new BroadcastRadioConfig { SampleRateHz = 60e3, Length = 8000, Stereo = true };
            var p = new BroadcastRadioPersonality();
            p.LoadConfig(cfg);
            Assert.Throws<InvalidOperationException>(() => p.Calculate(null));
        }

        // ---- v2 (#194): RDS 57 kHz subcarrier ----

        [Fact]
        public void Rds_adds_energy_around_57_khz_in_the_fm_multiplex()
        {
            const int len = 8192;
            const double fs = 400e3;
            var with = Calc(new BroadcastRadioConfig { SampleRateHz = fs, Length = len, Stereo = true, Rds = true, RdsDeviationHz = 4e3 });
            var without = Calc(new BroadcastRadioConfig { SampleRateHz = fs, Length = len, Stereo = true, Rds = false });

            double eWith = BandEnergy(FmDemod(with), fs, 55e3, 59e3);
            double eWithout = BandEnergy(FmDemod(without), fs, 55e3, 59e3);
            Assert.True(eWith > 10 * (eWithout + 1e-9), $"RDS should add 57 kHz energy (with {eWith:F2} vs without {eWithout:F4})");
        }

        [Fact]
        public void Rds_negative_deviation_is_rejected()
        {
            var cfg = new BroadcastRadioConfig { Rds = true, RdsDeviationHz = -1 };
            var p = new BroadcastRadioPersonality();
            p.LoadConfig(cfg);
            Assert.Throws<InvalidOperationException>(() => p.Calculate(null));
        }

        /// <summary>FM-demodulate to the baseband multiplex: instantaneous frequency = arg(z[s]·conj(z[s-1])).</summary>
        private static double[] FmDemod(WaveformModel wf)
        {
            var m = new double[wf.Length];
            for (int s = 1; s < wf.Length; s++)
            {
                double re = wf.I[s] * wf.I[s - 1] + wf.Q[s] * wf.Q[s - 1];
                double im = wf.Q[s] * wf.I[s - 1] - wf.I[s] * wf.Q[s - 1];
                m[s] = Math.Atan2(im, re);
            }
            return m;
        }

        private static double BandEnergy(double[] signal, double fs, double loHz, double hiHz)
        {
            int size = 1; while (size < signal.Length) size <<= 1;
            var re = new double[size];
            var im = new double[size];
            for (int k = 0; k < signal.Length; k++) re[k] = signal[k];
            EsgSignalCreator.Dsp.Fft.Forward(re, im);
            int lo = (int)(loHz / fs * size), hi = (int)(hiHz / fs * size);
            double e = 0;
            for (int k = lo; k <= hi && k < size; k++) e += re[k] * re[k] + im[k] * im[k];
            return e;
        }

        private static WaveformModel Calc(BroadcastRadioConfig cfg)
        {
            var p = new BroadcastRadioPersonality();
            p.LoadConfig(cfg);
            return p.Calculate(null);
        }
    }
}
