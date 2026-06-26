using System;
using EsgSignalCreator.Waveform;
using Xunit;

namespace EsgSignalCreator.Tests.Waveform
{
    public class WaveformGeneratorTests
    {
        [Fact]
        public void SingleTone_has_constant_unit_envelope()
        {
            var spec = new WaveformSpec
            {
                Type = SignalType.SingleTone,
                SampleRateHz = 10e6,
                TargetLength = 4000,
                OffsetHz = 1e6
            };

            WaveformResult r = WaveformGenerator.Generate(spec);
            var wf = r.Waveform;

            Assert.True(wf.Length >= WaveformGenerator.MinLength);
            for (int n = 0; n < wf.Length; n++)
            {
                double mag = Math.Sqrt(wf.I[n] * wf.I[n] + wf.Q[n] * wf.Q[n]);
                Assert.Equal(1.0, mag, 6); // constant-envelope CW, 6-decimal tolerance
            }
        }

        [Fact]
        public void SingleTone_length_holds_an_integer_number_of_cycles_for_seamless_looping()
        {
            var spec = new WaveformSpec
            {
                Type = SignalType.SingleTone,
                SampleRateHz = 10e6,
                TargetLength = 4000,
                OffsetHz = 1e6
            };

            WaveformResult r = WaveformGenerator.Generate(spec);
            var wf = r.Waveform;

            // The phase step times the length should land on a whole number of cycles.
            double cycles = spec.OffsetHz * wf.Length / spec.SampleRateHz;
            Assert.Equal(Math.Round(cycles), cycles, 6);
        }

        [Fact]
        public void Generator_never_returns_fewer_than_the_minimum_samples()
        {
            var spec = new WaveformSpec
            {
                Type = SignalType.SingleTone,
                SampleRateHz = 10e6,
                TargetLength = 1, // absurdly small request
                OffsetHz = 1e6
            };

            WaveformResult r = WaveformGenerator.Generate(spec);
            Assert.True(r.Waveform.Length >= WaveformGenerator.MinLength);
        }
    }
}
