using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows.Forms;
using EsgSignalCreator.Sequencing;
using EsgSignalCreator.Ui.Assistant;
using EsgSignalCreator.Ui.Instrument;
using EsgSignalCreator.Ui.Sequencing;
using EsgSignalCreator.Validation;

namespace EsgSignalCreator.Ui.TutorialImages
{
    /// <summary>
    /// Phase 2 of #150: render the app-UI panels the analyzer can't show — the Notifications dock, the
    /// Sequence table, the SCPI console, and the assistant pane — to PNG for the workflow tutorials
    /// (T9/T11/T12/T17/T18/T19). Each real <see cref="UserControl"/> is hosted off-screen, populated with
    /// canned/mock state via its own public API, and rendered with <see cref="Control.DrawToBitmap"/> —
    /// so a doc image is the actual app UI, produced headlessly and deterministically.
    /// <para>Invoked with <c>ESG-SignalCreator.exe --tutorial-ui-images &lt;dir&gt;</c>.</para>
    /// </summary>
    public static class TutorialUiImageHarness
    {
        public static int Run(string outputDir)
        {
            Directory.CreateDirectory(outputDir);

            // T9 — Notifications dock with a canned set of validation findings.
            var notif = new NotificationsDock();
            Render(Path.Combine(outputDir, "t09-notifications.png"), new Size(820, 240), notif, () =>
                notif.Show(new[]
                {
                    new ValidationResult(ValidationSeverity.Error, "DAC over-range: peak samples would clip at the current runtime scaling — lower scaling or apply CFR.", "Scaling"),
                    new ValidationResult(ValidationSeverity.Error, "Memory cap: 12,000,000 samples exceed the installed baseband option (Option 601 = 8,388,608 samples).", "Length"),
                    new ValidationResult(ValidationSeverity.Warning, "Loop seam discontinuity — the waveform end doesn't line up with its start; choose an integer-cycle length.", "Length"),
                    new ValidationResult(ValidationSeverity.Info, "99% occupied bandwidth ≈ 1.35 MHz.", "Bandwidth"),
                }));

            // T11 — Sequence table with a few steps.
            var seqPanel = new SequencePanel();
            Render(Path.Combine(outputDir, "t11-sequence.png"), new Size(820, 320), seqPanel, () =>
            {
                var seq = new Sequence();
                seq.Steps.Add(new SequenceStep { Waveform = "CW", Repeat = 1 });
                seq.Steps.Add(new SequenceStep { Waveform = "MULTITONE", Repeat = 3, MarkerEnabled = true });
                seq.Steps.Add(new SequenceStep { Waveform = "AWGN", Repeat = 1, Wait = WaitMode.TrigA });
                seqPanel.LoadSequence(seq);
            });

            // T12 — SCPI console with a canned request/response session.
            var console = new ScpiConsolePanel();
            Render(Path.Combine(outputDir, "t12-scpi-console.png"), new Size(820, 300), console, () =>
            {
                console.Log("> *IDN?");
                console.Log("Keysight Technologies,N9010A,MY51120010,A.07.05");
                console.Log("> *OPT?");
                console.Log("B25,EA3,P26");
                console.Log("> :INSTrument:CATalog?");
                console.Log("\"SA,BASIC,PNOISE\"");
            });

            // T17/T18/T19 — the real assistant pane with a mocked-up transcript.
            RenderAssistant(Path.Combine(outputDir, "t17-assistant.png"), pane =>
            {
                pane.AddDemoUserMessage("What's the app state?");
                pane.AddDemoAssistantMessage("The source is CW / Single tone at +100 kHz, 0 dBFS. The last calculated waveform is 4096 samples, PAPR ≈ 0.0 dB, 99% OBW ≈ 200 kHz. No instrument is connected.");
                pane.AddDemoNote("Read-only tools ran with no confirmation card.");
            });
            RenderAssistant(Path.Combine(outputDir, "t18-assistant.png"), pane =>
            {
                pane.AddDemoUserMessage("Set the source to a 4-tone multitone with Newman phasing, then calculate it.");
                pane.AddDemoAssistantMessage("Done — set Multitone (4 tones, 1 MHz spacing, Newman) and calculated. PAPR ≈ 3.6 dB, 99% OBW ≈ 3.0 MHz. The configure + calculate steps touch only PC/project state, so they ran without a confirmation card.");
            });
            RenderAssistant(Path.Combine(outputDir, "t19-assistant.png"), pane =>
            {
                pane.AddDemoUserMessage("Download the waveform and play it.");
                pane.AddDemoAssistantMessage("I'll download the waveform to WFM1 and start playback — please approve:");
                pane.AddDemoCard("download_waveform", "{\"segment\":\"SEG1\",\"samples\":16384}");
            });

            Console.WriteLine("Generated tutorial UI images in " + outputDir);
            return 0;
        }

        private static void RenderAssistant(string path, Action<AssistantPane> populate)
        {
            var pane = new AssistantPane();
            Render(path, new Size(470, 430), pane, () => populate(pane));
        }

        /// <summary>Host <paramref name="panel"/> off-screen, populate it, and render it to a PNG.</summary>
        private static void Render(string path, Size size, Control panel, Action populate)
        {
            using (var host = new Form
            {
                FormBorderStyle = FormBorderStyle.None,
                ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual,
                Location = new Point(-4000, -4000), // off-screen
                ClientSize = size
            })
            {
                panel.Dock = DockStyle.Fill;
                host.Controls.Add(panel);
                host.Show();
                Application.DoEvents();      // lay out + create handles
                populate?.Invoke();
                host.PerformLayout();
                Application.DoEvents();      // let the populated content paint

                int w = Math.Max(1, panel.Width);
                int h = Math.Max(1, panel.Height);
                using (var bmp = new Bitmap(w, h))
                {
                    panel.DrawToBitmap(bmp, new Rectangle(0, 0, w, h));
                    bmp.Save(path, ImageFormat.Png);
                }
                host.Hide();
                Console.WriteLine("  wrote " + Path.GetFileName(path));
            }
        }
    }
}
