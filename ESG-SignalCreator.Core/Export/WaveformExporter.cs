using System;
using System.Globalization;
using System.IO;
using System.Text;
using EsgSignalCreator.Arb;
using EsgSignalCreator.Model;

namespace EsgSignalCreator.Export
{
    /// <summary>
    /// Writes a <see cref="WaveformModel"/> to disk in the three offline interchange formats
    /// requested by issue #55:
    /// <list type="bullet">
    ///   <item><b>Raw ARB</b> — the byte-exact interleaved big-endian int16 payload the E4438C
    ///         ARB expects (no IEEE-488.2 block header), suitable for a separate <c>:MEMory:DATA</c>
    ///         download or comparison against an instrument upload.</item>
    ///   <item><b>CSV I/Q</b> — a human-readable <c>I,Q</c> table, one normalized float pair per row.</item>
    ///   <item><b>SCPI script</b> — a replayable text command sequence that downloads and plays the
    ///         segment. Because SCPI text cannot embed raw binary, the <c>:MEMory:DATA</c> definite-length
    ///         block payload is represented by a <c>#…</c> header preview plus a placeholder comment;
    ///         pair the script with the matching raw-ARB file for the actual bytes.</item>
    /// </list>
    /// Every method creates (and overwrites) the target file and ensures its parent directory exists.
    /// </summary>
    public static class WaveformExporter
    {
        /// <summary>
        /// Write the interleaved big-endian int16 ARB payload (no block header) to a binary file.
        /// Produces exactly <c>wf.Length * 4</c> bytes.
        /// </summary>
        public static void SaveRawArb(string path, WaveformModel wf,
            double backoff = EsgArbEncoder.DefaultBackoff)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (wf == null) throw new ArgumentNullException(nameof(wf));

            byte[] payload = EsgArbEncoder.EncodePayload(wf.I, wf.Q, backoff);
            EnsureDirectory(path);
            File.WriteAllBytes(path, payload);
        }

        /// <summary>
        /// Write the I/Q samples as CSV: a header line <c>I,Q</c> followed by one
        /// <c>i,q</c> float pair per sample, all formatted with <see cref="CultureInfo.InvariantCulture"/>.
        /// </summary>
        public static void SaveCsv(string path, WaveformModel wf)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (wf == null) throw new ArgumentNullException(nameof(wf));

            EnsureDirectory(path);
            var sb = new StringBuilder();
            sb.Append("I,Q\n");
            float[] i = wf.I;
            float[] q = wf.Q;
            for (int n = 0; n < i.Length; n++)
            {
                sb.Append(i[n].ToString("R", CultureInfo.InvariantCulture));
                sb.Append(',');
                sb.Append(q[n].ToString("R", CultureInfo.InvariantCulture));
                sb.Append('\n');
            }
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }

        /// <summary>
        /// Write a replayable <c>.scpi</c> text file with the command sequence to download and play
        /// the waveform on the E4438C: turn the ARB off, download the segment (block header preview),
        /// select it, set the sample clock, carrier frequency and amplitude, then enable the ARB and
        /// RF output.
        /// </summary>
        /// <remarks>
        /// The <c>:MEMory:DATA "WFM1:&lt;seg&gt;",#&lt;header&gt;</c> line carries only the IEEE-488.2
        /// definite-length block <em>header</em> (e.g. <c>#42400</c>) as a preview; the raw payload
        /// bytes cannot live in a text file, so a comment marks where they belong. Use
        /// <see cref="SaveRawArb"/> to emit the matching binary payload.
        /// </remarks>
        public static void SaveScpiScript(string path, WaveformModel wf, string segmentName,
            double carrierHz, double amplitudeDbm)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            if (wf == null) throw new ArgumentNullException(nameof(wf));
            if (string.IsNullOrEmpty(segmentName))
                throw new ArgumentException("Segment name is required.", nameof(segmentName));

            byte[] payload = EsgArbEncoder.EncodePayload(wf.I, wf.Q);
            string blockHeader = Encoding.ASCII.GetString(Ieee4882Block.BuildHeader(payload.Length));

            var sb = new StringBuilder();
            sb.Append("! ESG-SignalCreator export (issue #55) — replayable SCPI download/play script\n");
            sb.Append("! Segment   : ").Append(segmentName).Append('\n');
            sb.Append("! Samples   : ").Append(wf.Length.ToString(CultureInfo.InvariantCulture)).Append('\n');
            sb.Append("! SampleRate: ")
              .Append(wf.SampleRateHz.ToString("G17", CultureInfo.InvariantCulture))
              .Append(" Hz\n");
            sb.Append("! NOTE: the ':MEMory:DATA' line below shows only the IEEE-488.2 block header (")
              .Append(blockHeader)
              .Append("). The ").Append(payload.Length.ToString(CultureInfo.InvariantCulture))
              .Append(" raw payload bytes cannot be embedded in SCPI text; send the matching\n");
            sb.Append("! raw-ARB file in their place, or use the live download path.\n");
            sb.Append('\n');

            // 1. Park the ARB so the download never overwrites a playing segment.
            sb.Append(":SOURce:RADio:ARB:STATe OFF\n");

            // 2. Download the segment payload (header preview only; bytes live in the raw-ARB file).
            sb.Append(string.Format(CultureInfo.InvariantCulture,
                ":MEMory:DATA \"WFM1:{0}\",{1}", segmentName, blockHeader));
            sb.Append("   ! <").Append(payload.Length.ToString(CultureInfo.InvariantCulture))
              .Append(" raw payload bytes follow the header — see the raw-ARB export>\n");

            // 3. Select the segment for the dual ARB player.
            sb.Append(string.Format(CultureInfo.InvariantCulture,
                ":RADio:ARB:WAVeform \"WFM1:{0}\"\n", segmentName));

            // 4. Playback clock = the model's sample rate.
            sb.Append(string.Format(CultureInfo.InvariantCulture,
                ":RADio:ARB:SCLock:RATE {0}\n",
                wf.SampleRateHz.ToString("G17", CultureInfo.InvariantCulture)));

            // 5. Carrier frequency and amplitude.
            sb.Append(string.Format(CultureInfo.InvariantCulture,
                ":FREQuency {0}\n", carrierHz.ToString("G17", CultureInfo.InvariantCulture)));
            sb.Append(string.Format(CultureInfo.InvariantCulture,
                ":POWer {0}\n", amplitudeDbm.ToString("G17", CultureInfo.InvariantCulture)));

            // 6. Arm the ARB and enable RF output.
            sb.Append(":RADio:ARB:STATe ON\n");
            sb.Append(":OUTPut:STATe ON\n");

            EnsureDirectory(path);
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }

        private static void EnsureDirectory(string path)
        {
            string dir = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
        }
    }
}
