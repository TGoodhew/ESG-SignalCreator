using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using EsgSignalCreator.Model;
using EsgSignalCreator.Personalities.CustomMod;
using EsgSignalCreator.Ui.Plots;
using EsgSignalCreator.Verify;

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
            // Only the plot views the analyzer CAN'T produce belong here — a constellation and an eye
            // diagram need a vector-demod option the N9010A doesn't have. Everything the analyzer can show
            // (spectrum, CCDF/PAPR) is captured as a real N9010A screenshot by the HilHarness
            // --tutorial-captures mode instead (#150), so those aren't duplicated as app renders.
            var shots = new List<Shot>();

            // T5 — QPSK, 1 Msym/s, RRC α=0.35 → constellation + eye (app-only views).
            const int qpskSps = 8;
            WaveformModel qpsk = Qpsk(qpskSps);
            shots.Add(new Shot { File = "t05-qpsk-constellation.png", Tutorial = "Tutorial 5", Caption = "QPSK — constellation (app view)", View = PlotPane.ViewType.Constellation, Waveform = qpsk });
            shots.Add(new Shot { File = "t05-qpsk-eye.png", Tutorial = "Tutorial 5", Caption = "QPSK — eye diagram (app view)", View = PlotPane.ViewType.Eye, Waveform = qpsk, SamplesPerSymbol = qpskSps });

            // Part I — one app-rendered spectrum per source personality, sourced from the canonical
            // VerificationBattery (#228). Iterating the battery makes this pass registry-aware: a new
            // personality is covered automatically, and the battery id set is guarded by
            // VerificationBatteryTests, so an uncovered personality fails loud rather than silently
            // dropping out of the image set. Analyzer spectrum/CCDF captures are separate (#230).
            foreach (BatteryEntry entry in VerificationBattery.All(1e6))
            {
                shots.Add(new Shot
                {
                    File = "ref-" + entry.Id + "-spectrum.png",
                    Tutorial = "Part I — Personality reference",
                    Caption = entry.Description + " — spectrum (app view)",
                    View = PlotPane.ViewType.Spectrum,
                    Waveform = entry.Build(null),
                });
            }

            return shots;
        }

        // ---- signal builders (match the tutorial worked-example values) ----

        private static WaveformModel Qpsk(int samplesPerSymbol)
        {
            var p = new CustomModPersonality();
            p.LoadConfig(new CustomModConfig
            {
                Modulation = Modulation.QPSK, SymbolRateHz = 1e6, SamplesPerSymbol = samplesPerSymbol, Alpha = 0.35, SymbolCount = 1024
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
