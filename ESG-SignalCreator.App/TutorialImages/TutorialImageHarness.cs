using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using EsgSignalCreator.Impairments;
using EsgSignalCreator.Model;
using EsgSignalCreator.Personalities.Awgn;
using EsgSignalCreator.Personalities.CustomMod;
using EsgSignalCreator.Personalities.Cw;
using EsgSignalCreator.Personalities.MultiCarrier;
using EsgSignalCreator.Personalities.Multitone;
using EsgSignalCreator.Ui.Plots;

namespace EsgSignalCreator.Ui.TutorialImages
{
    /// <summary>
    /// Regenerates the offline tutorial plot images in one pass (issue #150). Each shot builds the exact
    /// signal a tutorial describes (same personality + values as the tutorial text) and renders the plot
    /// view(s) that tutorial references to PNG via <see cref="PlotImageRenderer"/>. Deterministic — fixed
    /// values, no RNG left to chance beyond the personalities' own generation — so the images are stable
    /// across runs. Analyzer (Part F) images come from the HilHarness capture tool (#143), not here.
    /// <para>Invoked headlessly with <c>ESG-SignalCreator.exe --tutorial-images &lt;dir&gt;</c>.</para>
    /// </summary>
    public static class TutorialImageHarness
    {
        private sealed class Shot
        {
            public string File;               // output filename (under the target dir)
            public string Tutorial;           // e.g. "Tutorial 5"
            public string Caption;            // one line for the manifest
            public PlotPane.ViewType View;
            public WaveformModel Waveform;
            public int SamplesPerSymbol;      // for the eye view
        }

        private static readonly Size ImageSize = new Size(720, 480);

        /// <summary>Generate every offline tutorial image into <paramref name="outputDir"/> + an index.md manifest.</summary>
        public static int Run(string outputDir)
        {
            Directory.CreateDirectory(outputDir);
            List<Shot> shots = BuildShots();

            foreach (Shot s in shots)
            {
                string path = Path.Combine(outputDir, s.File);
                PlotImageRenderer.SaveImage(path, s.View, s.Waveform, s.SamplesPerSymbol, ImageSize);
                Console.WriteLine("  wrote " + s.File + "  (" + s.Tutorial + " — " + s.Caption + ")");
            }

            WriteManifest(outputDir, shots);
            Console.WriteLine("Generated " + shots.Count + " tutorial images + index.md in " + outputDir);
            return 0;
        }

        private static List<Shot> BuildShots()
        {
            var shots = new List<Shot>();

            // T1 — CW / Single tone at +100 kHz (Tutorial 1's worked example).
            WaveformModel cw100 = Cw(100e3);
            shots.Add(new Shot { File = "t01-cw-spectrum.png", Tutorial = "Tutorial 1", Caption = "CW tone at +100 kHz — spectrum", View = PlotPane.ViewType.Spectrum, Waveform = cw100 });
            shots.Add(new Shot { File = "t01-cw-iq.png", Tutorial = "Tutorial 1", Caption = "CW tone — I/Q vs time", View = PlotPane.ViewType.IqVsTime, Waveform = cw100 });

            // T3 — Multitone (8 tones, 1 MHz), Newman vs Equal phasing → CCDF/PAPR comparison.
            WaveformModel mtNewman = Multitone(8, 1e6, PhaseStrategy.Newman);
            WaveformModel mtEqual = Multitone(8, 1e6, PhaseStrategy.Equal);
            shots.Add(new Shot { File = "t03-multitone-newman-ccdf.png", Tutorial = "Tutorial 3", Caption = "8-tone Newman multitone — CCDF (low PAPR)", View = PlotPane.ViewType.Ccdf, Waveform = mtNewman });
            shots.Add(new Shot { File = "t03-multitone-equal-ccdf.png", Tutorial = "Tutorial 3", Caption = "8-tone Equal-phased multitone — CCDF (high PAPR)", View = PlotPane.ViewType.Ccdf, Waveform = mtEqual });
            shots.Add(new Shot { File = "t03-multitone-spectrum.png", Tutorial = "Tutorial 3", Caption = "8-tone multitone — spectrum", View = PlotPane.ViewType.Spectrum, Waveform = mtNewman });

            // T4 — AWGN (5 MHz noise BW) → CCDF (high crest) + spectrum (flat noise).
            WaveformModel awgn = Awgn(5e6);
            shots.Add(new Shot { File = "t04-awgn-ccdf.png", Tutorial = "Tutorial 4", Caption = "Band-limited AWGN — CCDF (~10 dB crest)", View = PlotPane.ViewType.Ccdf, Waveform = awgn });
            shots.Add(new Shot { File = "t04-awgn-spectrum.png", Tutorial = "Tutorial 4", Caption = "Band-limited AWGN — spectrum", View = PlotPane.ViewType.Spectrum, Waveform = awgn });

            // T5 — QPSK, 1 Msym/s, RRC α=0.35 → constellation, eye, spectrum.
            const int qpskSps = 8;
            WaveformModel qpsk = Qpsk(qpskSps);
            shots.Add(new Shot { File = "t05-qpsk-constellation.png", Tutorial = "Tutorial 5", Caption = "QPSK — constellation", View = PlotPane.ViewType.Constellation, Waveform = qpsk });
            shots.Add(new Shot { File = "t05-qpsk-eye.png", Tutorial = "Tutorial 5", Caption = "QPSK — eye diagram", View = PlotPane.ViewType.Eye, Waveform = qpsk, SamplesPerSymbol = qpskSps });
            shots.Add(new Shot { File = "t05-qpsk-spectrum.png", Tutorial = "Tutorial 5", Caption = "QPSK RRC α=0.35 — spectrum", View = PlotPane.ViewType.Spectrum, Waveform = qpsk });

            // T6 — Multi-carrier, 3 carriers at ±5 MHz → composite spectrum.
            WaveformModel mc = MultiCarrier(3, 5e6);
            shots.Add(new Shot { File = "t06-multicarrier-spectrum.png", Tutorial = "Tutorial 6", Caption = "3-carrier composite — spectrum", View = PlotPane.ViewType.Spectrum, Waveform = mc });

            // T8 — I/Q gain-imbalance image (before vs after) → spectrum.
            WaveformModel clean = Cw(1e6);
            WaveformModel imbalanced = IqImpairments.Apply(Cw(1e6), new IqImpairmentConfig { GainImbalanceDb = 3.0 });
            shots.Add(new Shot { File = "t08-iq-clean-spectrum.png", Tutorial = "Tutorial 8", Caption = "Clean tone at +1 MHz — spectrum (baseline)", View = PlotPane.ViewType.Spectrum, Waveform = clean });
            shots.Add(new Shot { File = "t08-iq-imbalance-spectrum.png", Tutorial = "Tutorial 8", Caption = "3 dB I/Q gain imbalance — image tone at −1 MHz", View = PlotPane.ViewType.Spectrum, Waveform = imbalanced });

            return shots;
        }

        // ---- signal builders (match the tutorial worked-example values) ----

        private static WaveformModel Cw(double offsetHz)
        {
            var p = new CwPersonality();
            p.LoadConfig(new CwConfig { SampleRateHz = 10e6, Length = 4096, FreqOffsetHz = offsetHz });
            return p.Calculate(new Progress<int>());
        }

        private static WaveformModel Multitone(int tones, double spacingHz, PhaseStrategy phase)
        {
            var p = new MultitonePersonality();
            p.LoadConfig(new MultitoneConfig
            {
                SampleRateHz = 10e6, Length = 16384, Phase = phase,
                Tones = MultitonePersonality.AutoSpacing(tones, spacingHz, 0, 0)
            });
            return p.Calculate(new Progress<int>());
        }

        private static WaveformModel Awgn(double noiseBwHz)
        {
            var p = new AwgnPersonality();
            p.LoadConfig(new AwgnConfig { SampleRateHz = 10e6, Length = 32768, NoiseBandwidthHz = noiseBwHz, CrestFactorDb = 10 });
            return p.Calculate(new Progress<int>());
        }

        private static WaveformModel Qpsk(int samplesPerSymbol)
        {
            var p = new CustomModPersonality();
            p.LoadConfig(new CustomModConfig
            {
                Modulation = Modulation.QPSK, SymbolRateHz = 1e6, SamplesPerSymbol = samplesPerSymbol, Alpha = 0.35, SymbolCount = 1024
            });
            return p.Calculate(new Progress<int>());
        }

        private static WaveformModel MultiCarrier(int carriers, double spacingHz)
        {
            var p = new MultiCarrierPersonality();
            p.LoadConfig(new MultiCarrierConfig
            {
                SampleRateHz = 10e6, Length = 16384,
                Carriers = MultiCarrierPersonality.EvenlySpaced(carriers, spacingHz, 0)
            });
            return p.Calculate(new Progress<int>());
        }

        private static void WriteManifest(string outputDir, List<Shot> shots)
        {
            var md = new StringBuilder();
            md.AppendLine("# Tutorial images (auto-generated)");
            md.AppendLine();
            md.AppendLine("Regenerated by `ESG-SignalCreator.exe --tutorial-images docs/images/tutorials` (issue #150).");
            md.AppendLine("**Do not edit by hand** — update the tutorial-image harness and re-run instead.");
            md.AppendLine();
            string lastTut = null;
            foreach (Shot s in shots)
            {
                if (s.Tutorial != lastTut) { md.AppendLine("## " + s.Tutorial); md.AppendLine(); lastTut = s.Tutorial; }
                md.AppendLine("- `" + s.File + "` — " + s.Caption);
                md.AppendLine();
                md.AppendLine("  ![" + s.Caption + "](" + s.File + ")");
                md.AppendLine();
            }
            File.WriteAllText(Path.Combine(outputDir, "index.md"), md.ToString());
        }
    }
}
