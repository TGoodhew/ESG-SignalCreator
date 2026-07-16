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

        // ---- v2 (#180): per-tone magnitude/phase tables + pre-distortion (R-4, R-7) ----

        [Fact]
        public void ComputePerTone_uniform_when_no_tables()
        {
            var cfg = new MultitoneDistortionConfig { PowerDbPerTone = -3.0 };
            MultitoneDistortionPersonality.ComputePerTone(cfg, 5, out double[] mag, out _, out bool manual);

            Assert.False(manual);
            Assert.All(mag, m => Assert.Equal(-3.0, m, 9));
        }

        [Fact]
        public void ComputePerTone_magnitude_table_cycles_and_overrides_uniform()
        {
            var cfg = new MultitoneDistortionConfig
            {
                PowerDbPerTone = 0.0,
                PerToneMagnitudeDb = new[] { 0.0, -6.0 }
            };
            MultitoneDistortionPersonality.ComputePerTone(cfg, 5, out double[] mag, out _, out _);

            Assert.Equal(new[] { 0.0, -6.0, 0.0, -6.0, 0.0 }, mag);
        }

        [Fact]
        public void ComputePerTone_phase_table_forces_manual_and_cycles()
        {
            var cfg = new MultitoneDistortionConfig { PerTonePhaseDeg = new[] { 0.0, 90.0, 180.0 } };
            MultitoneDistortionPersonality.ComputePerTone(cfg, 4, out _, out double[] ph, out bool manual);

            Assert.True(manual);
            Assert.Equal(new[] { 0.0, 90.0, 180.0, 0.0 }, ph);
        }

        [Fact]
        public void ComputePerTone_predistortion_inverts_measured_error()
        {
            // Base 0 dB / 0 deg; measured error +2 dB / +30 deg => corrected -2 dB / -30 deg.
            var cfg = new MultitoneDistortionConfig
            {
                PowerDbPerTone = 0.0,
                PredistortionEnabled = true,
                MeasuredToneMagnitudeErrorDb = new[] { 2.0 },
                MeasuredTonePhaseErrorDeg = new[] { 30.0 }
            };
            MultitoneDistortionPersonality.ComputePerTone(cfg, 3, out double[] mag, out double[] ph, out bool manual);

            Assert.True(manual); // phase error forces the manual path
            Assert.All(mag, m => Assert.Equal(-2.0, m, 9));
            Assert.All(ph, p => Assert.Equal(-30.0, p, 9));
        }

        [Fact]
        public void ComputePerTone_predistortion_composes_with_base_tables()
        {
            var cfg = new MultitoneDistortionConfig
            {
                PerToneMagnitudeDb = new[] { -1.0, -2.0 },
                PerTonePhaseDeg = new[] { 10.0, 20.0 },
                PredistortionEnabled = true,
                MeasuredToneMagnitudeErrorDb = new[] { 0.5 },
                MeasuredTonePhaseErrorDeg = new[] { 5.0 }
            };
            MultitoneDistortionPersonality.ComputePerTone(cfg, 2, out double[] mag, out double[] ph, out _);

            Assert.Equal(new[] { -1.5, -2.5 }, mag); // base - measured error
            Assert.Equal(new[] { 5.0, 15.0 }, ph);
        }

        [Fact]
        public void Predistortion_disabled_ignores_measured_error()
        {
            var cfg = new MultitoneDistortionConfig
            {
                PowerDbPerTone = 0.0,
                PredistortionEnabled = false,
                MeasuredToneMagnitudeErrorDb = new[] { 5.0 }
            };
            MultitoneDistortionPersonality.ComputePerTone(cfg, 3, out double[] mag, out _, out _);

            Assert.All(mag, m => Assert.Equal(0.0, m, 9));
        }

        [Fact]
        public void Per_tone_tables_end_to_end_stay_unit_peak_and_finite()
        {
            var cfg = new MultitoneDistortionConfig
            {
                SampleRateHz = 40e6, Length = 8192, ToneCount = 16, ToneSpacingHz = 100e3,
                PerToneMagnitudeDb = new[] { 0.0, -3.0, -6.0 },
                PerTonePhaseDeg = new[] { 0.0, 45.0, 90.0, 135.0 },
                PredistortionEnabled = true,
                MeasuredToneMagnitudeErrorDb = new[] { 0.5, -0.5 },
                MeasuredTonePhaseErrorDeg = new[] { 2.0, -2.0 }
            };

            var p = new MultitoneDistortionPersonality();
            p.LoadConfig(cfg);
            WaveformModel wf = p.Calculate(null);

            Assert.True(p.LastUsedManualPhase);
            Assert.Equal(1.0, wf.PeakMagnitude(), 4);
            for (int s = 0; s < wf.Length; s++)
                Assert.False(float.IsNaN(wf.I[s]) || float.IsNaN(wf.Q[s]));
        }

        [Fact]
        public void Uniform_per_tone_magnitude_table_matches_uniform_power()
        {
            // A flat magnitude table equal to the uniform power must reproduce the uniform waveform.
            var baseCfg = new MultitoneDistortionConfig
            {
                SampleRateHz = 40e6, Length = 8192, ToneCount = 16, ToneSpacingHz = 100e3,
                Phase = MultitonePhasePreset.Constant, PowerDbPerTone = 0.0
            };
            var tableCfg = new MultitoneDistortionConfig
            {
                SampleRateHz = 40e6, Length = 8192, ToneCount = 16, ToneSpacingHz = 100e3,
                Phase = MultitonePhasePreset.Constant, PerToneMagnitudeDb = new[] { 0.0 }
            };

            WaveformModel a = Calc(baseCfg);
            WaveformModel b = Calc(tableCfg);
            for (int s = 0; s < a.Length; s += 97)
                Assert.Equal(a.I[s], b.I[s], 5);
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
