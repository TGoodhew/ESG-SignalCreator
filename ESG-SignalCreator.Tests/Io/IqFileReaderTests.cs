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

        // --- Agilent big-endian int16 -------------------------------------------------

        [Fact]
        public void ReadsAgilentInt16BigEndianByExtension()
        {
            string path = TempPath(".agt");
            try
            {
                short[] vals = { 32767, 0, -32768, 0 }; // I,Q,I,Q
                var bytes = new byte[vals.Length * 2];
                for (int k = 0; k < vals.Length; k++)
                {
                    ushort u = unchecked((ushort)vals[k]);
                    bytes[2 * k] = (byte)(u >> 8);       // big-endian: high byte first
                    bytes[2 * k + 1] = (byte)(u & 0xFF);
                }
                File.WriteAllBytes(path, bytes);

                WaveformModel m = IqFileReader.Read(path, Rate);

                Assert.Equal(2, m.Length);
                Assert.Equal(0.99997f, m.I[0], 4); // 32767/32768
                Assert.Equal(-1.0f, m.I[1], 6);    // -32768/32768
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void ExplicitAgilentFormatForcesBigEndianRegardlessOfExtension()
        {
            // Same bytes read little-endian (.bin) vs forced big-endian must differ.
            string path = TempPath(".bin");
            try
            {
                // 0x0102 big-endian = 258; little-endian = 0x0201 = 513.
                byte[] bytes = { 0x01, 0x02, 0x00, 0x00 };
                File.WriteAllBytes(path, bytes);

                WaveformModel le = IqFileReader.Read(path, Rate); // auto => RawInt16 (little-endian)
                WaveformModel be = IqFileReader.Read(path, Rate, false, 1.0, IqFileReader.IqFormat.AgilentInt16Be);

                Assert.Equal(513f / 32768f, le.I[0], 6);
                Assert.Equal(258f / 32768f, be.I[0], 6);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void ImportIqPersonalityHonoursExplicitAgilentFormat()
        {
            string path = TempPath(".dat");
            try
            {
                byte[] bytes = { 0x40, 0x00, 0xC0, 0x00 }; // BE: I=0x4000=16384, Q=0xC000=-16384
                File.WriteAllBytes(path, bytes);

                var p = new ImportIqPersonality();
                p.LoadConfig(new ImportIqConfig
                {
                    Path = path,
                    SampleRateHz = Rate,
                    Format = IqFileReader.IqFormat.AgilentInt16Be
                });
                WaveformModel m = p.Calculate(null);

                Assert.Equal(1, m.Length);
                Assert.Equal(0.5f, m.I[0], 4);
                Assert.Equal(-0.5f, m.Q[0], 4);
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

        // --- 14-bit big-endian --------------------------------------------------------

        [Fact]
        public void ReadsAgilentInt14BigEndian()
        {
            // 14-bit sample left-justified in a 16-bit BE word (low 2 bits are markers, masked off).
            // +0.5 => value14 = 4096 => word 0x4000 (bytes 40 00); -0.5 => word 0xC000 (bytes C0 00).
            string path = TempPath(".agt");
            try
            {
                File.WriteAllBytes(path, new byte[] { 0x40, 0x03, 0xC0, 0x01 }); // I=0x4000|marker, Q=0xC000|marker
                WaveformModel m = IqFileReader.Read(path, Rate, format: IqFileReader.IqFormat.AgilentInt14Be);

                Assert.Equal(1, m.Length);
                Assert.Equal(0.5f, m.I[0], 5);   // 0x4003 >> 2 = 0x1000 = 4096 => 0.5 (marker bits dropped)
                Assert.Equal(-0.5f, m.Q[0], 5);  // 0xC001 >> 2 (arithmetic) = -4096 => -0.5
            }
            finally { File.Delete(path); }
        }

        // --- MATLAB Level-5 .mat -------------------------------------------------------

        [Fact]
        public void ReadsMatComplexVector()
        {
            // A 1x2 complex double array [1+2i, 3+4i] => I=[1,3], Q=[2,4].
            string path = TempPath(".mat");
            try
            {
                byte[] elem = BuildMatMatrix(true, 1, 2, new double[] { 1, 3 }, new double[] { 2, 4 }, "iq");
                File.WriteAllBytes(path, BuildMatFile(elem, true));

                WaveformModel m = IqFileReader.Read(path, Rate, format: IqFileReader.IqFormat.Mat);

                Assert.Equal(2, m.Length);
                Assert.Equal(1f, m.I[0], 6); Assert.Equal(2f, m.Q[0], 6);
                Assert.Equal(3f, m.I[1], 6); Assert.Equal(4f, m.Q[1], 6);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void ReadsMatRealTwoByN()
        {
            // A real 2x3 array (rows = I, Q); column-major storage [1,4,2,5,3,6].
            string path = TempPath(".mat");
            try
            {
                byte[] elem = BuildMatMatrix(true, 2, 3, new double[] { 1, 4, 2, 5, 3, 6 }, null, "wf");
                File.WriteAllBytes(path, BuildMatFile(elem, true));

                WaveformModel m = IqFileReader.Read(path, Rate, format: IqFileReader.IqFormat.Mat);

                Assert.Equal(3, m.Length);
                Assert.Equal(new float[] { 1, 2, 3 }, m.I);
                Assert.Equal(new float[] { 4, 5, 6 }, m.Q);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void ReadsMatCompressedElement()
        {
            // Same complex vector, but the top-level element is zlib-compressed (miCOMPRESSED),
            // exercising the inflate path (MATLAB's default v7 save compresses).
            string path = TempPath(".mat");
            try
            {
                byte[] elem = BuildMatMatrix(true, 1, 2, new double[] { 0.25, -0.5 }, new double[] { 0.75, 1.0 }, "z");
                byte[] file = BuildMatFileCompressed(elem, true);
                File.WriteAllBytes(path, file);

                WaveformModel m = IqFileReader.Read(path, Rate, format: IqFileReader.IqFormat.Mat);

                Assert.Equal(2, m.Length);
                Assert.Equal(0.25f, m.I[0], 6); Assert.Equal(0.75f, m.Q[0], 6);
                Assert.Equal(-0.5f, m.I[1], 6); Assert.Equal(1.0f, m.Q[1], 6);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void MatAutoDetectsByExtension()
        {
            string path = TempPath(".mat");
            try
            {
                byte[] elem = BuildMatMatrix(true, 1, 1, new double[] { 0.1 }, new double[] { 0.2 }, "x");
                File.WriteAllBytes(path, BuildMatFile(elem, true));

                WaveformModel m = IqFileReader.Read(path, Rate); // Auto => Mat by .mat extension
                Assert.Equal(1, m.Length);
                Assert.Equal(0.1f, m.I[0], 6);
                Assert.Equal(0.2f, m.Q[0], 6);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void ShortMatFileThrowsInvalidData()
        {
            string path = TempPath(".mat");
            try
            {
                File.WriteAllBytes(path, new byte[] { 1, 2, 3, 4 });
                Assert.Throws<InvalidDataException>(() => IqFileReader.Read(path, Rate));
            }
            finally { File.Delete(path); }
        }

        // --- marker authoring (N7622A R-7) --------------------------------------------

        [Fact]
        public void ImportMarkers_start_periodic_and_range()
        {
            string path = TempPath(".csv");
            try
            {
                var sb = new StringBuilder();
                for (int n = 0; n < 20; n++) sb.AppendLine("0.1,0.1");
                File.WriteAllText(path, sb.ToString());

                byte[] start = MarkersFor(path, new ImportIqConfig { Path = path, SampleRateHz = Rate, MarkerMode = ImportMarkerMode.Start });
                Assert.Equal(1, start[0]);
                Assert.Equal(0, start[1]);

                byte[] periodic = MarkersFor(path, new ImportIqConfig { Path = path, SampleRateHz = Rate, MarkerMode = ImportMarkerMode.Periodic, MarkerPeriodSamples = 5 });
                Assert.Equal(1, periodic[0]);
                Assert.Equal(1, periodic[5]);
                Assert.Equal(1, periodic[10]);
                Assert.Equal(0, periodic[3]);

                byte[] range = MarkersFor(path, new ImportIqConfig { Path = path, SampleRateHz = Rate, MarkerMode = ImportMarkerMode.Range, MarkerStartSample = 4, MarkerLengthSamples = 3 });
                Assert.Equal(0, range[3]);
                Assert.Equal(1, range[4]);
                Assert.Equal(1, range[6]);
                Assert.Equal(0, range[7]);
            }
            finally { File.Delete(path); }
        }

        [Fact]
        public void ImportMarkers_none_leaves_no_markers()
        {
            string path = TempPath(".csv");
            try
            {
                File.WriteAllText(path, "0.1,0.1\n0.2,0.2\n");
                var p = new ImportIqPersonality();
                p.LoadConfig(new ImportIqConfig { Path = path, SampleRateHz = Rate, MarkerMode = ImportMarkerMode.None });
                WaveformModel m = p.Calculate(null);
                Assert.Null(m.Markers);
            }
            finally { File.Delete(path); }
        }

        private static byte[] MarkersFor(string path, ImportIqConfig cfg)
        {
            var p = new ImportIqPersonality();
            p.LoadConfig(cfg);
            return p.Calculate(null).Markers;
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

        // --- MAT File 5 builders (for the .mat reader tests) --------------------------

        private static byte[] BuildMatFile(byte[] element, bool little)
        {
            var f = new System.Collections.Generic.List<byte>();
            AppendMatHeader(f, little);
            f.AddRange(element);
            return f.ToArray();
        }

        private static byte[] BuildMatFileCompressed(byte[] matrixElement, bool little)
        {
            byte[] comp = ZlibCompress(matrixElement);
            var f = new System.Collections.Generic.List<byte>();
            AppendMatHeader(f, little);
            AppendU32(f, 15, little);                 // miCOMPRESSED
            AppendU32(f, (uint)comp.Length, little);
            f.AddRange(comp);
            return f.ToArray();
        }

        private static void AppendMatHeader(System.Collections.Generic.List<byte> f, bool little)
        {
            var header = new byte[128];
            byte[] text = Encoding.ASCII.GetBytes("MATLAB 5.0 MAT-file (test)");
            Array.Copy(text, header, text.Length);
            header[124] = 0x00; header[125] = 0x01;   // version 0x0100
            header[126] = (byte)(little ? 'I' : 'M'); // endian indicator
            header[127] = (byte)(little ? 'M' : 'I');
            f.AddRange(header);
        }

        /// <summary>Build a numeric miMATRIX element (tag + body) for a real or complex double array.</summary>
        private static byte[] BuildMatMatrix(bool little, int rows, int cols, double[] re, double[] im, string name)
        {
            var body = new System.Collections.Generic.List<byte>();

            // Array flags: class mxDOUBLE (6), complex bit 0x0800 when an imaginary part is present.
            AppendU32(body, 6, little); AppendU32(body, 8, little);
            uint flags = 6u | (im != null ? 0x0800u : 0u);
            AppendU32(body, flags, little); AppendU32(body, 0, little);

            // Dimensions (miINT32, two dims).
            AppendU32(body, 5, little); AppendU32(body, 8, little);
            AppendI32(body, rows, little); AppendI32(body, cols, little);

            // Name (miINT8).
            byte[] nm = Encoding.ASCII.GetBytes(name);
            AppendU32(body, 1, little); AppendU32(body, (uint)nm.Length, little);
            body.AddRange(nm); Pad8(body);

            // Real part (miDOUBLE).
            AppendDoubleElement(body, re, little);
            // Imaginary part.
            if (im != null) AppendDoubleElement(body, im, little);

            var elem = new System.Collections.Generic.List<byte>();
            AppendU32(elem, 14, little);               // miMATRIX
            AppendU32(elem, (uint)body.Count, little);
            elem.AddRange(body);
            return elem.ToArray();
        }

        private static void AppendDoubleElement(System.Collections.Generic.List<byte> b, double[] v, bool little)
        {
            AppendU32(b, 9, little);                    // miDOUBLE
            AppendU32(b, (uint)(v.Length * 8), little);
            foreach (double d in v)
            {
                byte[] bytes = BitConverter.GetBytes(d);
                if (little != BitConverter.IsLittleEndian) Array.Reverse(bytes);
                b.AddRange(bytes);
            }
        }

        private static void AppendU32(System.Collections.Generic.List<byte> b, uint v, bool little)
        {
            byte[] x = { (byte)v, (byte)(v >> 8), (byte)(v >> 16), (byte)(v >> 24) };
            if (!little) Array.Reverse(x);
            b.AddRange(x);
        }

        private static void AppendI32(System.Collections.Generic.List<byte> b, int v, bool little) => AppendU32(b, (uint)v, little);

        private static void Pad8(System.Collections.Generic.List<byte> b)
        {
            while (b.Count % 8 != 0) b.Add(0);
        }

        private static byte[] ZlibCompress(byte[] raw)
        {
            using (var ms = new MemoryStream())
            {
                ms.WriteByte(0x78); ms.WriteByte(0x9C); // zlib header
                using (var ds = new System.IO.Compression.DeflateStream(ms, System.IO.Compression.CompressionMode.Compress, true))
                    ds.Write(raw, 0, raw.Length);
                ms.Write(new byte[4], 0, 4);            // Adler-32 placeholder (ignored by the reader)
                return ms.ToArray();
            }
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
