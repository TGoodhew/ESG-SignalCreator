using System;
using System.Collections.Generic;
using System.Globalization;

namespace EsgSignalCreator.Measure
{
    /// <summary>
    /// One time window of a power mask: samples whose time falls in [<see cref="StartSeconds"/>,
    /// <see cref="EndSeconds"/>] must sit between <see cref="LowerDbm"/> and <see cref="UpperDbm"/>.
    /// Either limit may be <see cref="double.NaN"/> to leave that side unconstrained.
    /// </summary>
    public sealed class PowerMaskSegment
    {
        public PowerMaskSegment(double startSeconds, double endSeconds, double lowerDbm, double upperDbm)
        {
            StartSeconds = startSeconds;
            EndSeconds = endSeconds;
            LowerDbm = lowerDbm;
            UpperDbm = upperDbm;
        }

        public double StartSeconds { get; }
        public double EndSeconds { get; }
        public double LowerDbm { get; }
        public double UpperDbm { get; }

        public bool Contains(double timeSeconds) => timeSeconds >= StartSeconds && timeSeconds <= EndSeconds;
    }

    /// <summary>A single sample that fell outside the mask.</summary>
    public sealed class PowerMaskViolation
    {
        public PowerMaskViolation(double timeSeconds, double powerDbm, double limitDbm, bool isUpper)
        {
            TimeSeconds = timeSeconds;
            PowerDbm = powerDbm;
            LimitDbm = limitDbm;
            IsUpper = isUpper;
        }

        public double TimeSeconds { get; }
        public double PowerDbm { get; }
        public double LimitDbm { get; }

        /// <summary>True for an upper-limit breach, false for a lower-limit breach.</summary>
        public bool IsUpper { get; }

        public override string ToString() => string.Format(CultureInfo.InvariantCulture,
            "t={0:0.######e0}s: {1:0.##} dBm {2} limit {3:0.##} dBm",
            TimeSeconds, PowerDbm, IsUpper ? "exceeds upper" : "below lower", LimitDbm);
    }

    /// <summary>The outcome of evaluating a <see cref="PowerMask"/> against a power trace.</summary>
    public sealed class PowerMaskResult
    {
        public PowerMaskResult(bool pass, IReadOnlyList<PowerMaskViolation> violations, int samplesChecked)
        {
            Pass = pass;
            Violations = violations;
            SamplesChecked = samplesChecked;
        }

        /// <summary>True when no sample violated any segment limit.</summary>
        public bool Pass { get; }

        /// <summary>The violating samples (empty when <see cref="Pass"/>).</summary>
        public IReadOnlyList<PowerMaskViolation> Violations { get; }

        /// <summary>How many samples fell inside at least one mask segment and were checked.</summary>
        public int SamplesChecked { get; }

        public override string ToString() =>
            Pass ? "MASK PASS (" + SamplesChecked + " samples)"
                 : "MASK FAIL (" + Violations.Count + " violation(s) of " + SamplesChecked + " checked)";
    }

    /// <summary>
    /// A time-windowed power mask (#74): a set of <see cref="PowerMaskSegment"/>s evaluated against a
    /// measured power-vs-time trace for burst/frame pass-fail. Pure logic — no instrument access — so
    /// it is fully unit-testable and reusable for both the analyzer trace and a PC-computed envelope.
    /// </summary>
    public sealed class PowerMask
    {
        private readonly List<PowerMaskSegment> _segments;

        public PowerMask(IEnumerable<PowerMaskSegment> segments)
        {
            if (segments == null) throw new ArgumentNullException(nameof(segments));
            _segments = new List<PowerMaskSegment>(segments);
        }

        public IReadOnlyList<PowerMaskSegment> Segments => _segments;

        /// <summary>
        /// Check each sample against every segment whose window contains its time. Samples outside all
        /// segments are unconstrained. <paramref name="toleranceDb"/> widens both limits (a sample is a
        /// violation only when it exceeds upper+tol or falls below lower−tol). NaN power samples are skipped.
        /// </summary>
        public PowerMaskResult Evaluate(double[] timeSeconds, double[] powerDbm, double toleranceDb = 0.0)
        {
            if (timeSeconds == null) throw new ArgumentNullException(nameof(timeSeconds));
            if (powerDbm == null) throw new ArgumentNullException(nameof(powerDbm));

            var violations = new List<PowerMaskViolation>();
            int checkedSamples = 0;
            int n = Math.Min(timeSeconds.Length, powerDbm.Length);

            for (int k = 0; k < n; k++)
            {
                double t = timeSeconds[k];
                double p = powerDbm[k];
                if (double.IsNaN(p)) continue;

                bool inAnySegment = false;
                foreach (PowerMaskSegment seg in _segments)
                {
                    if (!seg.Contains(t)) continue;
                    inAnySegment = true;

                    if (!double.IsNaN(seg.UpperDbm) && p > seg.UpperDbm + toleranceDb)
                        violations.Add(new PowerMaskViolation(t, p, seg.UpperDbm, true));
                    if (!double.IsNaN(seg.LowerDbm) && p < seg.LowerDbm - toleranceDb)
                        violations.Add(new PowerMaskViolation(t, p, seg.LowerDbm, false));
                }

                if (inAnySegment) checkedSamples++;
            }

            return new PowerMaskResult(violations.Count == 0, violations, checkedSamples);
        }
    }
}
