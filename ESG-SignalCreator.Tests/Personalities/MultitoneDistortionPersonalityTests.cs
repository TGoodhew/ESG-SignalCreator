using System;
using EsgSignalCreator.Model;
using EsgSignalCreator.Personalities.MultitoneDistortion;
using Xunit;

namespace EsgSignalCreator.Tests.Personalities
{
    public class MultitoneDistortionPersonalityTests
    {
        [Fact]
        public void Output_length_sample_rate_and_unit_peak()
        {
            var cfg = new MultitoneDistortionConfig
            {
                SampleRateHz = 40e6,
                Length = 16384,
                ToneCount = 32,
                ToneSpacingHz = 200e3
            };

            WaveformModel wf = Calc(cfg);

            Assert.Equal(cfg.Length, wf.Length);
            Assert.Equal(cfg.SampleRateHz, wf.SampleRateHz);
            Assert.Equal(1.0, wf.PeakMagnitude(), 4);
        }

        [Fact]
        public void Noise_bandwidth_is_tone_count_times_spacing()
        {
            var cfg = new MultitoneDistortionConfig
            {
                SampleRateHz = 40e6,
                Length = 8192,
                ToneCount = 50,
                ToneSpacingHz = 100e3
            };

            var p = new MultitoneDistortionPersonality();
            p.LoadConfig(cfg);
            p.Calculate(null);

            Assert.Equal(5e6, p.LastNoiseBandwidthHz, 3);
            Assert.Equal(50, p.LastActiveToneCount);
        }

        [Fact]
        public void Notch_removes_tones_inside_the_notch_band()
        {
            // 41 tones, 100 kHz spacing, centred at 0 => tones at -2.0 MHz .. +2.0 MHz.
            // A 500 kHz notch at centre removes tones at -200,-100,0,+100,+200 kHz => 5 tones.
            var cfg = new MultitoneDistortionConfig
            {
                SampleRateHz = 40e6,
                Length = 8192,
                ToneCount = 41,
                ToneSpacingHz = 100e3,
                CenterOffsetHz = 0.0,
                NotchEnabled = true,
                NotchWidthHz = 500e3,
                NotchOffsetHz = 0.0
            };

            var p = new MultitoneDistortionPersonality();
            p.LoadConfig(cfg);
            p.Calculate(null);

            Assert.Equal(41 - 5, p.LastActiveToneCount);
        }

        [Fact]
        public void Parabolic_papr_is_below_constant_papr()
        {
            double parabolic = PaprFor(MultitonePhasePreset.Parabolic);
            double constant = PaprFor(MultitonePhasePreset.Constant);
            Assert.True(parabolic < constant,
                $"Expected Parabolic PAPR ({parabolic:F3} dB) < Constant PAPR ({constant:F3} dB)");
        }

        [Fact]
        public void Tone_count_below_two_is_rejected()
        {
            var cfg = new MultitoneDistortionConfig { ToneCount = 1 };
            var p = new MultitoneDistortionPersonality();
            p.LoadConfig(cfg);
            Assert.Throws<InvalidOperationException>(() => p.Calculate(null));
        }

        [Fact]
        public void Tone_count_above_4097_is_rejected()
        {
            var cfg = new MultitoneDistortionConfig { ToneCount = 4098 };
            var p = new MultitoneDistortionPersonality();
            p.LoadConfig(cfg);
            Assert.Throws<InvalidOperationException>(() => p.Calculate(null));
        }

        private static double PaprFor(MultitonePhasePreset phase)
        {
            var cfg = new MultitoneDistortionConfig
            {
                SampleRateHz = 40e6,
                Length = 16384,
                ToneCount = 32,
                ToneSpacingHz = 100e3,
                Phase = phase
            };
            var p = new MultitoneDistortionPersonality();
            p.LoadConfig(cfg);
            p.Calculate(null);
            return p.LastPaprDb;
        }

        private static WaveformModel Calc(MultitoneDistortionConfig cfg)
        {
            var p = new MultitoneDistortionPersonality();
            p.LoadConfig(cfg);
            return p.Calculate(null);
        }
    }
}
