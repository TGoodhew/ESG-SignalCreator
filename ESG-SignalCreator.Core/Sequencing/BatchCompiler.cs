using System.Collections.Generic;
using System.Globalization;

namespace EsgSignalCreator.Sequencing
{
    /// <summary>One value in a batch-compile parameter sweep.</summary>
    public sealed class BatchPoint
    {
        public BatchPoint(string name, double value) { Name = name; Value = value; }
        public string Name { get; }
        public double Value { get; }
    }

    /// <summary>
    /// Batch compile (UX brief §5.1): expand a parameter sweep low→high by step into a family of
    /// named points, e.g. for emitting a waveform per value during receiver-margin testing.
    /// </summary>
    public static class BatchCompiler
    {
        public static IReadOnlyList<BatchPoint> Expand(string parameter, double low, double high, double step,
            int maxPoints = 100000)
        {
            var list = new List<BatchPoint>();
            if (step <= 0 || high < low)
            {
                list.Add(new BatchPoint(Name(parameter, low), low));
                return list;
            }
            for (double v = low; v <= high + step * 1e-9 && list.Count < maxPoints; v += step)
                list.Add(new BatchPoint(Name(parameter, v), v));
            return list;
        }

        private static string Name(string parameter, double value) =>
            parameter + "_" + value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
