using System;
using System.Globalization;
using System.IO;
using System.Text;
using EsgSignalCreator.Io;
using EsgSignalCreator.Model;
using EsgSignalCreator.Personalities.CustomIq;
using Xunit;

namespace EsgSignalCreator.Tests.Io
{
    public sealed class IqFileReaderTests
    {
        private const double Rate = 10e6;

        // --- CSV ----------------------------------------------------------------------

        [Fact]
        public void ReadsCsvWithHeaderAndBlankLines()
        {
            string path = TempPath(".csv");
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("I,Q");          // non-numeric header — should be skipped
                sb.AppendLine("0.5,-0.25");
                sb.AppendLine("");             // blank line — tolerated
                sb.AppendLine("-1.0,1.0");
                sb.AppendLine("0,0");
                File.WriteAllText(path, sb.ToString());

                WaveformModel m = IqFileReader.Read(path, Rate);

                Assert.Equal(3, m.Length);
                Assert.Equal(Rate, m.SampleRateHz);
                Assert.Equal(0.5f, m.I[0], 6);
                Assert.Equal(-0.25f, m.Q[0], 6);
                Assert.Equal(-1.0f, m.I[1], 6);
                Assert.Equal(1.0f, m.Q[1], 6);
                Assert.Equal(0f, m.I[2], 6);
                Assert.Equal(0f, m.Q[2], 6);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void ReadsTabSeparatedValues()
        {
            string path = TempPath(".tsv");
            try
            {
                File.WriteAllText(path, "0.1\t0.2\n0.3\t0.4\n");
                WaveformModel m = IqFileReader.Read(path, Rate);

                Assert.Equal(2, m.Length);
                Assert.Equal(0.1f, m.I[0], 6);
                Assert.Equal(0.4f, m.Q[1], 6);
            }
            finally { File.Delete(path); }
        }

        // --- Raw int16 ----------------------------------------------------------------

        [Fact]
        public void ReadsRawInt16AndScales()
        {
            string path = TempPath(".bin");
            try
            {
                short[] vals = { 32767, 0, -32768, 0 }; // I,Q,I,Q
                var bytes = new byte[vals.Length * 2];
                for (int k = 0; k < vals.Length; k++)
                {
                    ushort u = unchecked((ushort)vals[k]);
                    bytes[2 * k] = (byte)(u & 0xFF);     // little-endian
                    bytes[2 * k + 1] = (byte)(u >> 8);
                }
                File.WriteAllBytes(path, bytes);

                WaveformModel m = IqFileReader.Read(path, Rate);

                Assert.Equal(2, m.Length);
                Assert.Equal(Rate, m.SampleRateHz);
                Assert.Equal(0.99997f, m.I[0], 4); // 32767/32768
                Assert.Equal(0f, m.Q[0], 6);
                Assert.Equal(-1.0f, m.I[1], 6);    // -32768/32768
                Assert.Equal(0f, m.Q[1], 6);
            }
            finally { File.Delete(path); }
        }

        // --- WAV ----------------------------------------------------------------------

        [Fact]
        public void ReadsStereoWavMappingChannelsAndHeaderRate()
        {
            string path = TempPath(".wav");
            try
            {
                // Three stereo frames: (ch0=I, ch1=Q)
                short[][] frames =
                {
                    new short[] { 16384, -16384 },
                    new short[] { 0, 32767 },
                    new short[] { -32768, 0 },
                };
                WriteWav(path, sampleRate: 48000, channels: 2, frames: frames);

                // sampleRateHzOverride = 0 -> header rate must win
                WaveformModel m = IqFileReader.Read(path, 0);

                Assert.Equal(3, m.Length);
                Assert.Equal(48000.0, m.SampleRateHz);
                Assert.Equal(0.5f, m.I[0], 3);    // 16384/32768
                Assert.Equal(-0.5f, m.Q[0], 3);
                Assert.Equal(0f, m.I[1], 6);
                Assert.Equal(0.99997f, m.Q[1], 4);
                Assert.Equal(-1.0f, m.I[2], 6);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void ReadsMonoWavWithZeroQ()
        {
            string path = TempPath(".wav");
            try
            {
                short[][] frames =
                {
                    new short[] { 16384 },
                    new short[] { -16384 },
                };
                WriteWav(path, sampleRate: 44100, channels: 1, frames: frames);

                WaveformModel m = IqFileReader.Read(path, 0);

                Assert.Equal(2, m.Length);
                Assert.Equal(44100.0, m.SampleRateHz);
                Assert.Equal(0.5f, m.I[0], 3);
                Assert.Equal(0f, m.Q[0], 6);
                Assert.Equal(-0.5f, m.I[1], 3);
                Assert.Equal(0f, m.Q[1], 6);
            }
            finally { File.Delete(path); }
        }

        // --- Options ------------------------------------------------------------------

        [Fact]
        public void SwapIqSwapsChannels()
        {
            string path = TempPath(".csv");
            try
            {
                File.WriteAllText(path, "0.5,-0.25\n");
                WaveformModel m = IqFileReader.Read(path, Rate, swapIq: true);

                Assert.Equal(-0.25f, m.I[0], 6);
                Assert.Equal(0.5f, m.Q[0], 6);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void ScaleMultipliesSamples()
        {
            string path = TempPath(".csv");
            try
            {
                File.WriteAllText(path, "0.2,0.1\n");
                WaveformModel m = IqFileReader.Read(path, Rate, scale: 2.0);

                Assert.Equal(0.4f, m.I[0], 6);
                Assert.Equal(0.2f, m.Q[0], 6);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void CsvWithoutRateThrows()
        {
            string path = TempPath(".csv");
            try
            {
                File.WriteAllText(path, "0.1,0.2\n");
                Assert.Throws<ArgumentException>(() => IqFileReader.Read(path, 0));
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void MatFileThrowsNotSupported()
        {
            string path = TempPath(".mat");
            try
            {
                File.WriteAllBytes(path, new byte[] { 1, 2, 3, 4 });
                Assert.Throws<NotSupportedException>(() => IqFileReader.Read(path, Rate));
            }
            finally { File.Delete(path); }
        }

        // --- Personality --------------------------------------------------------------

        [Fact]
        public void PersonalityLoadsAndClampsToUnitRange()
        {
            string path = TempPath(".csv");
            try
            {
                File.WriteAllText(path, "0.5,0.5\n0.6,0.6\n"); // scale 2 -> 1.2 should clamp to 1.0
                var p = new ImportIqPersonality();
                p.LoadConfig(new ImportIqConfig { Path = path, SampleRateHz = Rate, Scale = 2.0 });

                WaveformModel m = p.Calculate(null);

                Assert.Equal("import-iq", p.Id);
                Assert.Equal("Import I/Q", p.DisplayName);
                Assert.Null(p.RequiredOption);
                Assert.Equal(2, m.Length);
                Assert.Equal(1.0f, m.I[0], 6); // 0.5*2 = 1.0
                Assert.Equal(1.0f, m.I[1], 6); // 0.6*2 -> clamped to 1.0
                Assert.Equal(1.0f, m.Q[1], 6);
            }
            finally { File.Delete(path); }
        }

        // --- helpers ------------------------------------------------------------------

        private static string TempPath(string ext)
        {
            return System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "iqtest_" + Guid.NewGuid().ToString("N") + ext);
        }

        private static void WriteWav(string path, int sampleRate, int channels, short[][] frames)
        {
            const int bitsPerSample = 16;
            int blockAlign = channels * bitsPerSample / 8;
            int byteRate = sampleRate * blockAlign;
            int dataLen = frames.Length * blockAlign;

            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var w = new BinaryWriter(fs))
            {
                w.Write(Encoding.ASCII.GetBytes("RIFF"));
                w.Write(36 + dataLen);
                w.Write(Encoding.ASCII.GetBytes("WAVE"));

                w.Write(Encoding.ASCII.GetBytes("fmt "));
                w.Write(16);                       // PCM fmt chunk size
                w.Write((short)1);                 // audioFormat = PCM
                w.Write((short)channels);
                w.Write(sampleRate);
                w.Write(byteRate);
                w.Write((short)blockAlign);
                w.Write((short)bitsPerSample);

                w.Write(Encoding.ASCII.GetBytes("data"));
                w.Write(dataLen);
                foreach (short[] frame in frames)
                    foreach (short s in frame)
                        w.Write(s);
            }
        }
    }
}
