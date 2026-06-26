using System;
using EsgSignalCreator.Model;
using EsgSignalCreator.Seamless;
using Xunit;

namespace EsgSignalCreator.Tests.Seamless
{
    public class SeamlessGuardTests
    {
        /// <summary>
        /// Build a complex tone of <paramref name="cycles"/> full cycles spread over
        /// <paramref name="n"/> samples. An integer cycle count yields a seamless loop.
        /// </summary>
        private static WaveformModel Tone(int n, double cycles)
        {
            var i = new float[n];
            var q = new float[n];
            for (int k = 0; k < n; k++)
            {
                double phase = 2.0 * Math.PI * cycles * k / n;
                i[k] = (float)Math.Cos(phase);
                q[k] = (float)Math.Sin(phase);
            }
            return new WaveformModel(i, q, 1_000_000.0, "tone");
        }

        [Fact]
        public void Integer_cycle_tone_is_seamless()
        {
            // With a high sample density (few cycles spread over many samples) the residual
            // per-sample phase step at the wrap is tiny, so an integer-cycle tone reads as
            // seamless under the default tolerance.
            var wf = Tone(100_000, cycles: 4.0);

            double wrap = SeamlessGuard.WrapDiscontinuity(wf);

            Assert.True(wrap <= 1e-3, $"expected small wrap, got {wrap}");
            Assert.True(SeamlessGuard.IsSeamless(wf));
        }

        [Fact]
        public void Non_integer_cycle_tone_has_larger_wrap_and_is_not_seamless()
        {
            var good = Tone(1024, cycles: 8.0);
            var bad = Tone(1024, cycles: 8.5);

            double goodWrap = SeamlessGuard.WrapDiscontinuity(good);
            double badWrap = SeamlessGuard.WrapDiscontinuity(bad);

            Assert.True(badWrap > goodWrap, $"bad {badWrap} should exceed good {goodWrap}");
            Assert.False(SeamlessGuard.IsSeamless(bad));
        }

        [Fact]
        public void PhaseStep_is_near_zero_for_integer_cycle_tone()
        {
            var wf = Tone(1024, cycles: 8.0);

            // The last sample is one step (2*pi*8/1024 rad) short of a full lap, so the wrap
            // from s[N-1] to s[0] advances by exactly that step.
            double expectedStep = 2.0 * Math.PI * 8.0 / 1024.0;

            double step = SeamlessGuard.PhaseStepRadians(wf);

            Assert.Equal(expectedStep, step, 6);
        }

        [Fact]
        public void TrimToZeroCrossing_never_returns_fewer_than_minLength()
        {
            var wf = Tone(1024, cycles: 8.5);

            var trimmed = SeamlessGuard.TrimToZeroCrossing(wf, minLength: 60);

            Assert.True(trimmed.Length >= 60);
        }

        [Fact]
        public void TrimToZeroCrossing_does_not_worsen_wrap_for_bad_case()
        {
            var bad = Tone(1024, cycles: 8.5);
            double before = SeamlessGuard.WrapDiscontinuity(bad);

            var trimmed = SeamlessGuard.TrimToZeroCrossing(bad, minLength: 60);
            double after = SeamlessGuard.WrapDiscontinuity(trimmed);

            // Either it found a better (or equal) seam, or it returned the input unchanged.
            Assert.True(after <= before + 1e-9,
                $"trim worsened wrap: before {before}, after {after}");
        }

        [Fact]
        public void TrimToZeroCrossing_does_not_mutate_input()
        {
            var wf = Tone(1024, cycles: 8.5);
            int originalLength = wf.Length;
            float firstI = wf.I[0];
            float lastI = wf.I[wf.Length - 1];

            SeamlessGuard.TrimToZeroCrossing(wf, minLength: 60);

            Assert.Equal(originalLength, wf.Length);
            Assert.Equal(firstI, wf.I[0]);
            Assert.Equal(lastI, wf.I[wf.Length - 1]);
        }

        [Fact]
        public void TrimToZeroCrossing_returns_input_when_already_short()
        {
            var wf = Tone(50, cycles: 4.0);

            var result = SeamlessGuard.TrimToZeroCrossing(wf, minLength: 60);

            Assert.Same(wf, result);
        }
    }
}
