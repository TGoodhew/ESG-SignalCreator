using System;
using System.Globalization;
using System.IO;
using EsgSignalCreator.Arb;
using EsgSignalCreator.Export;
using EsgSignalCreator.Model;
using Xunit;

namespace EsgSignalCreator.Tests.Export
{
    public class WaveformExporterTests
    {
        // The encoder requires at least EsgArbEncoder.MinSamples (60) samples.
        private static WaveformModel TinyWaveform(out float[] i, out float[] q)
        {
            int n = EsgArbEncoder.MinSamples; // 60
            i = new float[n];
            q = new float[n];
            // A simple, deterministic ramp so values are non-trivial and unambiguous.
            for (int k = 0; k < n; k++)
            {
                i[k] = (k % 2 == 0) ? 1.0f : -0.5f;
                q[k] = (k % 2 == 0) ? 0.25f : -1.0f;
            }
            return new WaveformModel(i, q, sampleRateHz: 1.0e6, name: "tiny");
        }

        private static string TempPath(string extension)
        {
            string dir = Path.Combine(Path.GetTempPath(),
                "esg-export-tests", Guid.NewGuid().ToString("N"));
            return Path.Combine(dir, "out" + extension);
        }

        [Fact]
        public void SaveRawArb_writes_exactly_length_times_four_bytes_and_matches_encoder()
        {
            var wf = TinyWaveform(out float[] i, out float[] q);
            string path = TempPath(".arb");
            try
            {
                WaveformExporter.SaveRawArb(path, wf);

                byte[] written = File.ReadAllBytes(path);
                Assert.Equal(wf.Length * 4, written.Length);

                // First and last bytes must match a directly-encoded payload (hand-checked sample).
                byte[] expected = EsgArbEncoder.EncodePayload(i, q);
                Assert.Equal(expected.Length, written.Length);
                Assert.Equal(expected[0], written[0]);
                Assert.Equal(expected[1], written[1]);
                Assert.Equal(expected[expected.Length - 2], written[written.Length - 2]);
                Assert.Equal(expected[expected.Length - 1], written[written.Length - 1]);
            }
            finally { Cleanup(path); }
        }

        [Fact]
        public void SaveCsv_round_trips_iq_values_and_row_count()
        {
            var wf = TinyWaveform(out float[] i, out float[] q);
            string path = TempPath(".csv");
            try
            {
                WaveformExporter.SaveCsv(path, wf);

                string[] lines = File.ReadAllLines(path);
                Assert.Equal(wf.Length + 1, lines.Length); // header + one row per sample
                Assert.Equal("I,Q", lines[0]);

                for (int n = 0; n < wf.Length; n++)
                {
                    string[] parts = lines[n + 1].Split(',');
                    Assert.Equal(2, parts.Length);
                    float ri = float.Parse(parts[0], CultureInfo.InvariantCulture);
                    float rq = float.Parse(parts[1], CultureInfo.InvariantCulture);
                    Assert.Equal(i[n], ri);
                    Assert.Equal(q[n], rq);
                }
            }
            finally { Cleanup(path); }
        }

        [Fact]
        public void SaveScpiScript_contains_expected_command_lines()
        {
            var wf = TinyWaveform(out _, out _);
            string path = TempPath(".scpi");
            try
            {
                const double carrierHz = 1.0e9;
                const double amplitudeDbm = -10.0;
                WaveformExporter.SaveScpiScript(path, wf, "seg", carrierHz, amplitudeDbm);

                string text = File.ReadAllText(path);

                Assert.Contains("WFM1:seg", text);
                Assert.Contains(":SOURce:RADio:ARB:STATe OFF", text);
                Assert.Contains(":RADio:ARB:WAVeform \"WFM1:seg\"", text);
                Assert.Contains(":RADio:ARB:SCLock:RATE", text);
                Assert.Contains(":RADio:ARB:STATe ON", text);
                Assert.Contains(":OUTPut:STATe ON", text);

                // Carrier and power values are present (InvariantCulture, G17 formatting).
                Assert.Contains(carrierHz.ToString("G17", CultureInfo.InvariantCulture), text);
                Assert.Contains(amplitudeDbm.ToString("G17", CultureInfo.InvariantCulture), text);

                // The block-header preview matches the real payload size.
                byte[] payload = EsgArbEncoder.EncodePayload(wf.I, wf.Q);
                string header = System.Text.Encoding.ASCII.GetString(
                    Ieee4882Block.BuildHeader(payload.Length));
                Assert.Contains(header, text);
            }
            finally { Cleanup(path); }
        }

        [Fact]
        public void Save_methods_create_missing_directories()
        {
            var wf = TinyWaveform(out _, out _);
            string path = TempPath(".arb"); // parent dir does not exist yet
            Assert.False(Directory.Exists(Path.GetDirectoryName(path)));
            try
            {
                WaveformExporter.SaveRawArb(path, wf);
                Assert.True(File.Exists(path));
            }
            finally { Cleanup(path); }
        }

        private static void Cleanup(string path)
        {
            try
            {
                string dir = Path.GetDirectoryName(path);
                if (dir != null && Directory.Exists(dir))
                    Directory.Delete(dir, recursive: true);
            }
            catch { /* best-effort cleanup */ }
        }
    }
}
