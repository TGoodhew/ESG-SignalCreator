using EsgSignalCreator.Measure;
using Xunit;

namespace EsgSignalCreator.Tests.Measure
{
    public class PowerMaskTests
    {
        private static PowerMask Burst() => new PowerMask(new[]
        {
            // On-burst window: -12 .. -8 dBm allowed.
            new PowerMaskSegment(1e-6, 3e-6, lowerDbm: -12.0, upperDbm: -8.0),
        });

        [Fact]
        public void All_samples_within_limits_pass()
        {
            var t = new[] { 1e-6, 2e-6, 3e-6 };
            var p = new[] { -10.0, -9.5, -11.0 };
            PowerMaskResult r = Burst().Evaluate(t, p);

            Assert.True(r.Pass);
            Assert.Empty(r.Violations);
            Assert.Equal(3, r.SamplesChecked);
        }

        [Fact]
        public void Sample_above_upper_limit_is_an_upper_violation()
        {
            var t = new[] { 2e-6 };
            var p = new[] { -5.0 };
            PowerMaskResult r = Burst().Evaluate(t, p);

            Assert.False(r.Pass);
            Assert.Single(r.Violations);
            Assert.True(r.Violations[0].IsUpper);
            Assert.Equal(-8.0, r.Violations[0].LimitDbm, 6);
        }

        [Fact]
        public void Sample_below_lower_limit_is_a_lower_violation()
        {
            var t = new[] { 2e-6 };
            var p = new[] { -20.0 };
            PowerMaskResult r = Burst().Evaluate(t, p);

            Assert.False(r.Pass);
            Assert.Single(r.Violations);
            Assert.False(r.Violations[0].IsUpper);
        }

        [Fact]
        public void Samples_outside_all_segments_are_unconstrained()
        {
            var t = new[] { 0.0, 5e-6 };       // before and after the only segment
            var p = new[] { 50.0, -200.0 };    // wild values, but outside the mask window
            PowerMaskResult r = Burst().Evaluate(t, p);

            Assert.True(r.Pass);
            Assert.Equal(0, r.SamplesChecked);
        }

        [Fact]
        public void Tolerance_widens_both_limits()
        {
            var t = new[] { 2e-6 };
            var p = new[] { -7.5 };            // 0.5 dB over the -8 upper limit
            Assert.False(Burst().Evaluate(t, p).Pass);
            Assert.True(Burst().Evaluate(t, p, toleranceDb: 1.0).Pass);
        }

        [Fact]
        public void Nan_limit_leaves_that_side_open()
        {
            var mask = new PowerMask(new[] { new PowerMaskSegment(0, 10, double.NaN, -8.0) });
            var t = new[] { 1.0, 2.0 };
            var p = new[] { -200.0, -9.0 };    // very low is fine (no lower limit), -9 under upper
            Assert.True(mask.Evaluate(t, p).Pass);
        }
    }
}
