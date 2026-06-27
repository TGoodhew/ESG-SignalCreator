using System;
using System.Globalization;

namespace EsgSignalCreator.Verify
{
    /// <summary>One expected-vs-measured comparison from the closed-loop verification harness.</summary>
    public sealed class VerificationResult
    {
        public VerificationResult(string metric, double expected, double measured, double tolerance, string unit)
        {
            Metric = metric;
            Expected = expected;
            Measured = measured;
            Tolerance = tolerance;
            Unit = unit;
        }

        public string Metric { get; }
        public double Expected { get; }
        public double Measured { get; }
        public double Tolerance { get; }
        public string Unit { get; }

        /// <summary>Measured − Expected.</summary>
        public double Delta => Measured - Expected;

        /// <summary>True when |Delta| is within Tolerance (NaN measured/expected fails).</summary>
        public bool Pass => !double.IsNaN(Measured) && !double.IsNaN(Expected) && Math.Abs(Delta) <= Tolerance;

        public override string ToString() => string.Format(CultureInfo.InvariantCulture,
            "{0}: expected {1:0.###} {5}, measured {2:0.###} {5} (Δ {3:+0.###;-0.###}, tol ±{4:0.###}) → {6}",
            Metric, Expected, Measured, Delta, Tolerance, Unit, Pass ? "PASS" : "FAIL");
    }
}
