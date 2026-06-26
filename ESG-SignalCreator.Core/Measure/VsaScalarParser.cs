using System.Collections.Generic;
using System.Globalization;

namespace EsgSignalCreator.Measure
{
    /// <summary>Parses an E4406A comma-separated scalar/trace response into doubles.</summary>
    public static class VsaScalarParser
    {
        /// <summary>
        /// Split a comma-separated response into doubles, tolerant of surrounding whitespace and
        /// quotes. Non-numeric fields are skipped. An empty/null response yields an empty array.
        /// </summary>
        public static double[] ParseScalars(string response)
        {
            var values = new List<double>();
            if (string.IsNullOrEmpty(response)) return values.ToArray();

            foreach (string part in response.Split(','))
            {
                string s = part.Trim().Trim('"');
                if (s.Length == 0) continue;
                if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
                    values.Add(v);
            }
            return values.ToArray();
        }
    }
}
