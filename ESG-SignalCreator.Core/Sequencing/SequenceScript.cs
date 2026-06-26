using System;
using System.Globalization;
using System.Text;

namespace EsgSignalCreator.Sequencing
{
    /// <summary>
    /// Text form of a <see cref="Sequence"/> for the power-user script view (UX brief §5.2), with a
    /// lossless round-trip to/from the table model. One <c>play</c> line per step:
    /// <code>play &lt;waveform&gt; x&lt;repeat&gt; pwr=&lt;dB&gt; wait=&lt;off|trigA|trigB|internal&gt; goto=&lt;next|index&gt; flags=&lt;ABCD|-&gt; marker=&lt;0|1&gt; idle=&lt;n&gt;</code>
    /// </summary>
    public static class SequenceScript
    {
        public static string Format(Sequence sequence)
        {
            var sb = new StringBuilder();
            foreach (SequenceStep s in sequence.Steps)
            {
                string repeat = s.Repeat == SequenceStep.InfiniteRepeat
                    ? "inf"
                    : s.Repeat.ToString(CultureInfo.InvariantCulture);
                string goTo = s.GoTo < 0 ? "next" : s.GoTo.ToString(CultureInfo.InvariantCulture);
                sb.Append("play ").Append(string.IsNullOrEmpty(s.Waveform) ? "_" : s.Waveform)
                  .Append(" x").Append(repeat)
                  .Append(" pwr=").Append(s.PowerDb.ToString("0.###", CultureInfo.InvariantCulture))
                  .Append(" wait=").Append(s.Wait.ToString().ToLowerInvariant())
                  .Append(" goto=").Append(goTo)
                  .Append(" flags=").Append(FormatFlags(s.Flags))
                  .Append(" marker=").Append(s.MarkerEnabled ? "1" : "0")
                  .Append(" idle=").Append(s.IdleSamples.ToString(CultureInfo.InvariantCulture))
                  .Append('\n');
            }
            return sb.ToString();
        }

        public static Sequence Parse(string text)
        {
            var seq = new Sequence();
            if (string.IsNullOrEmpty(text)) return seq;

            foreach (string raw in text.Replace("\r", "").Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;

                string[] tok = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (tok.Length < 2 || tok[0] != "play")
                    throw new FormatException("Each line must start with 'play <waveform>': " + line);

                var step = new SequenceStep { Waveform = tok[1] == "_" ? "" : tok[1] };
                for (int i = 2; i < tok.Length; i++)
                {
                    string t = tok[i];
                    if (t.StartsWith("x")) step.Repeat = ParseRepeat(t.Substring(1));
                    else if (t.StartsWith("pwr=")) step.PowerDb = ParseDouble(t.Substring(4));
                    else if (t.StartsWith("wait=")) step.Wait = ParseWait(t.Substring(5));
                    else if (t.StartsWith("goto=")) step.GoTo = t.Substring(5) == "next" ? -1 : int.Parse(t.Substring(5), CultureInfo.InvariantCulture);
                    else if (t.StartsWith("flags=")) step.Flags = ParseFlags(t.Substring(6));
                    else if (t.StartsWith("marker=")) step.MarkerEnabled = t.Substring(7) == "1";
                    else if (t.StartsWith("idle=")) step.IdleSamples = int.Parse(t.Substring(5), CultureInfo.InvariantCulture);
                }
                seq.Steps.Add(step);
            }
            return seq;
        }

        private static int ParseRepeat(string s) =>
            s == "inf" ? SequenceStep.InfiniteRepeat : int.Parse(s, CultureInfo.InvariantCulture);

        private static double ParseDouble(string s) => double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

        private static WaitMode ParseWait(string s)
        {
            switch (s)
            {
                case "triga": return WaitMode.TrigA;
                case "trigb": return WaitMode.TrigB;
                case "internal": return WaitMode.Internal;
                default: return WaitMode.Off;
            }
        }

        private static string FormatFlags(bool[] flags)
        {
            if (flags == null) return "-";
            var sb = new StringBuilder();
            char[] names = { 'A', 'B', 'C', 'D' };
            for (int i = 0; i < 4; i++) sb.Append(i < flags.Length && flags[i] ? names[i] : '-');
            return sb.ToString();
        }

        private static bool[] ParseFlags(string s)
        {
            var flags = new bool[4];
            if (s == "-" ) return flags;
            for (int i = 0; i < 4 && i < s.Length; i++) flags[i] = s[i] != '-';
            return flags;
        }
    }
}
