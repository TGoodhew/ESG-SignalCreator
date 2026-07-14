using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using EsgSignalCreator;
using EsgSignalCreator.Export;
using EsgSignalCreator.Instruments;
using EsgSignalCreator.Impairments;
using EsgSignalCreator.Personalities.CustomIq;
using EsgSignalCreator.Measure;
using EsgSignalCreator.Measure.Results;
using EsgSignalCreator.Model;
using EsgSignalCreator.Personalities.Awgn;
using EsgSignalCreator.Personalities.CustomMod;
using EsgSignalCreator.Personalities.Cw;
using EsgSignalCreator.Personalities.MultiCarrier;
using EsgSignalCreator.Personalities.Multitone;
using EsgSignalCreator.Verify;
using EsgSignalCreator.Visa;
using EsgSignalCreator.Waveform;

namespace EsgSignalCreator.HilHarness
{
    /// <summary>
    /// Hardware-in-the-loop test harness (issues #59, #73). Drives a <b>real</b> E4438C over VISA to
    /// validate the ARB download/play path the offline tests can't reach, and — with <c>--vsa</c> —
    /// runs a <b>closed-loop</b> generate→measure→compare against a real E4406A on the ESG output.
    ///
    /// Usage:
    ///   ESG-SignalCreator.HilHarness [esgResource] [--rf-on]
    ///                                [--vsa [vsaResource]] [--verify-power-dbm X] [--carrier-hz X]
    ///                                [--offset-hz X] [--max-input-dbm X] [--path-loss-db X]
    ///
    /// Safety: in ESG-only mode RF stays OFF unless --rf-on. In closed-loop (--vsa) mode RF IS
    /// enabled at the (low) verify power so the analyzer can measure it — but only after the
    /// PowerSafetyGate confirms it is below the analyzer's max safe input (E4406A rating +35 dBm;
    /// gate default +30 dBm). The error queue is polled after every step. Exit 0 = all pass.
    /// </summary>
    internal static class Program
    {
        private const string DefaultEsgResource = "TCPIP0::192.168.1.82::inst1::INSTR";
        private const string DefaultVsaResource = "GPIB0::17::INSTR";
        private const double SafeAmplitudeDbm = -30.0;

        private static int _failures;
        private static string _jsonPath;
        private sealed class CheckRecord { public string Name; public bool Ok; public string Detail; }
        private static readonly List<CheckRecord> _results = new List<CheckRecord>();

        private static int Main(string[] args)
        {
            var positionals = new List<string>();
            var opts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var valueFlags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "--esg", "--vsa", "--vsa-model", "--verify-power-dbm", "--carrier-hz", "--offset-hz", "--max-input-dbm", "--path-loss-db", "--dwell-seconds", "--points", "--start-hz", "--stop-hz", "--signal", "--json",
                  "--capture-screen", "--capture-dir", "--capture-save-cmd", "--capture-data-query", "--capture-cleanup-cmd", "--capture-temp-path" };
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (a.StartsWith("--"))
                {
                    if (valueFlags.Contains(a) && i + 1 < args.Length && !args[i + 1].StartsWith("--")) opts[a] = args[++i];
                    else flags.Add(a);
                }
                else positionals.Add(a);
            }

            string esgResource = Opt(opts, "--esg") ?? positionals.FirstOrDefault()
                ?? Environment.GetEnvironmentVariable("ESG_VISA_RESOURCE") ?? DefaultEsgResource;
            bool rfOn = flags.Contains("--rf-on");
            bool closedLoop = flags.Contains("--vsa") || opts.ContainsKey("--vsa");
            string vsaResource = Opt(opts, "--vsa") ?? DefaultVsaResource;
            VsaModel vsaModel = ParseVsaModel(Opt(opts, "--vsa-model"));
            double verifyPowerDbm = Num(opts, "--verify-power-dbm", -10.0);
            double offsetHz = Num(opts, "--offset-hz", 1e6);
            // Default the input-damage limit from the target analyzer (E4406A +30 dBm, N9010A +25 dBm).
            double maxInputDbm = Num(opts, "--max-input-dbm", AnalyzerInputLimits.DefaultMaxSafeInputDbm(vsaModel));
            double pathLossDb = Num(opts, "--path-loss-db", 0.0);
            double dwellSeconds = Num(opts, "--dwell-seconds", 0.0);
            // Frequency sweep across the E4438C's range (the default for hardware tests). A single
            // --carrier-hz overrides the sweep with one point.
            double carrierOverride = Num(opts, "--carrier-hz", 0.0);
            int points = (int)Num(opts, "--points", 7);
            double startHz = Num(opts, "--start-hz", 50e6);
            double stopHz = Num(opts, "--stop-hz", 0.0); // 0 = query the ESG's max (capped to the VSA's 4 GHz)
            // Which signal personalities to verify. --all runs the full battery; --signal X runs one.
            _jsonPath = Opt(opts, "--json");
            bool flatness = flags.Contains("--flatness");
            bool installVerify = flags.Contains("--install-verify");
            if (installVerify) closedLoop = true; // install-verify drives the analyzer + RF like closed-loop
            string[] signals = flags.Contains("--all")
                ? new[] { "cw", "multitone", "awgn", "custom-mod", "multi-carrier", "iq-impair", "import-iq" }
                : new[] { Opt(opts, "--signal") ?? "cw" };

            // Screen-capture mode (#143): analyzer-only, no ESG/RF. Grabs the VSA display to an image file.
            if (flags.Contains("--capture-screen") || opts.ContainsKey("--capture-screen"))
            {
                return RunCaptureScreen(
                    vsaResource, vsaModel,
                    outputPath: Opt(opts, "--capture-screen") ?? "vsa-capture.png",
                    saveCmd: Opt(opts, "--capture-save-cmd"),
                    dataQuery: Opt(opts, "--capture-data-query"),
                    cleanupCmd: Opt(opts, "--capture-cleanup-cmd"),
                    tempPath: Opt(opts, "--capture-temp-path"));
            }

            Console.WriteLine("ESG-SignalCreator hardware-in-the-loop harness");
            Console.WriteLine("ESG       : " + esgResource);
            Console.WriteLine("VSA       : " + (closedLoop ? vsaResource + "  (" + vsaModel + ", CLOSED-LOOP)" : "(not used)"));
            Console.WriteLine(closedLoop
                ? "RF        : WILL be enabled at " + verifyPowerDbm.ToString(CultureInfo.InvariantCulture) +
                  " dBm into the analyzer (limit " + maxInputDbm.ToString(CultureInfo.InvariantCulture) +
                  " dBm, path loss " + pathLossDb.ToString(CultureInfo.InvariantCulture) + " dB)"
                : "RF        : " + (rfOn ? "WILL be enabled briefly (--rf-on)" : "OFF (safe default)"));
            Console.WriteLine(new string('-', 64));

            IInstrument io = null;
            EsgController esg = null;
            try
            {
                io = Step("Open ESG VISA session", () => new VisaInstrument(esgResource));
                if (io == null) return Finish();

                var inst = new EsgInstrument(io);
                Step("ESG *IDN? identifies an E4438C", () =>
                {
                    InstrumentIdentity id = inst.Identify();
                    Console.WriteLine("            " + id.Manufacturer + " / " + id.Model + " / FW " + id.FirmwareRevision);
                    if (id.Model == null || id.Model.IndexOf("E4438C", StringComparison.OrdinalIgnoreCase) < 0)
                        throw new Exception("Model is not E4438C: '" + id.Model + "'");
                });
                Warn("ESG baseband generator option present", () =>
                {
                    string[] opt = inst.Options();
                    Console.WriteLine("            *OPT? = " + string.Join(",", opt));
                    return inst.HasBasebandGenerator();
                }, "no 001/002/601/602 option — ARB playback needs the baseband hardware");

                esg = new EsgController(io);
                Step("ESG safe state (RF off, mod off, low power, *CLS)", () =>
                {
                    io.Write("*CLS");
                    esg.SetRfOutput(false);
                    esg.SetModulation(false);
                    esg.SetAmplitudeDbm(SafeAmplitudeDbm);
                });
                CheckErrorQueue(esg, "after safe state");

                EsgSignalCreator.Waveform.WaveformResult gen = Step("Generate CW waveform (Core, +" + (offsetHz / 1e6) + " MHz offset)", () =>
                    WaveformGenerator.Generate(new WaveformSpec
                    {
                        Type = SignalType.SingleTone, SampleRateHz = 10e6, TargetLength = 4096, OffsetHz = offsetHz
                    }));
                IqWaveform wf = gen?.Waveform;

                if (wf != null)
                {
                    Step("Download waveform to WFM1", () => esg.DownloadWaveform("HILTEST", wf));
                    CheckErrorQueue(esg, "after download");
                    Step("Select + arm ARB (sample clock, 70% scaling)", () => esg.PlayWaveform("HILTEST", wf.SampleRateHz, 70));
                    CheckErrorQueue(esg, "after ARB arm (catches DAC over-range)");
                }

                Step("ESG frequency set/read-back (1 GHz)", () =>
                {
                    esg.SetFrequencyHz(1e9);
                    double back = esg.GetFrequencyHz();
                    if (Math.Abs(back - 1e9) > 1.0) throw new Exception("read back " + back.ToString("G", CultureInfo.InvariantCulture) + " Hz");
                });
                Step("ESG amplitude set/read-back (" + SafeAmplitudeDbm + " dBm)", () =>
                {
                    esg.SetAmplitudeDbm(SafeAmplitudeDbm);
                    double back = esg.GetAmplitudeDbm();
                    if (Math.Abs(back - SafeAmplitudeDbm) > 0.5) throw new Exception("read back " + back + " dBm");
                });

                if (installVerify)
                {
                    var capture = new CaptureOptions
                    {
                        Dir = Opt(opts, "--capture-dir"),
                        SaveCmd = Opt(opts, "--capture-save-cmd"),
                        DataQuery = Opt(opts, "--capture-data-query"),
                        CleanupCmd = Opt(opts, "--capture-cleanup-cmd"),
                        TempPath = Opt(opts, "--capture-temp-path")
                    };
                    RunInstallVerify(esg, vsaResource, vsaModel, carrierOverride, verifyPowerDbm, maxInputDbm, pathLossDb, capture);
                }
                else if (closedLoop)
                {
                    double[] sweep = BuildSweep(esg, carrierOverride, points, startHz, stopHz, AnalyzerCeilingHz(vsaModel));
                    if (flatness)
                        RunFlatness(esg, vsaResource, vsaModel, sweep, maxInputDbm, pathLossDb, dwellSeconds);
                    else
                        RunClosedLoop(esg, vsaResource, vsaModel, sweep, signals, verifyPowerDbm, offsetHz, maxInputDbm, pathLossDb, dwellSeconds);
                }
                else if (rfOn)
                {
                    Step("RF-on smoke test (low power, 2 s)", () =>
                    {
                        esg.SetRfOutput(true);
                        Thread.Sleep(2000);
                        esg.SetRfOutput(false);
                    });
                    CheckErrorQueue(esg, "after RF smoke test");
                }
            }
            catch (Exception ex)
            {
                Fail("Unhandled", ex.Message);
            }
            finally
            {
                try { esg?.SetArbState(false); esg?.SetRfOutput(false); } catch { /* best effort */ }
                try { io?.Dispose(); } catch { /* best effort */ }
            }

            return Finish();
        }

        // ---- closed-loop (ESG -> E4406A) ----

        /// <summary>Build the carrier sweep: a single --carrier-hz override, else N points start→stop
        /// (stop defaults to the ESG's queried max, capped to the analyzer's frequency ceiling).</summary>
        private static double[] BuildSweep(EsgController esg, double carrierOverride, int points, double startHz, double stopHz, double analyzerCeilingHz)
        {
            if (carrierOverride > 0) return new[] { carrierOverride };
            if (points < 1) points = 1;
            if (stopHz <= 0)
            {
                try { stopHz = Math.Min(esg.GetMaxFrequencyHz(), analyzerCeilingHz); }
                catch { stopHz = 3e9; }
            }
            if (stopHz < startHz) stopHz = startHz;
            if (points == 1) return new[] { startHz };
            var f = new double[points];
            double step = (stopHz - startHz) / (points - 1);
            for (int i = 0; i < points; i++) f[i] = startHz + i * step;
            return f;
        }

        /// <summary>Parse --vsa-model (e.g. "e4406a" / "n9010a"); defaults to E4406A when absent/unknown.</summary>
        private static VsaModel ParseVsaModel(string arg)
        {
            if (string.IsNullOrWhiteSpace(arg)) return VsaModel.E4406A;
            VsaModel m = VsaModels.Detect(arg);
            return m == VsaModel.Unknown ? VsaModel.E4406A : m;
        }

        /// <summary>Upper frequency the analyzer can measure to, used to cap the auto sweep (the ESG's own
        /// max usually limits first). The E4406A tops out ~4 GHz; the N9010A (EXA) reaches much higher.</summary>
        private static double AnalyzerCeilingHz(VsaModel model) => model == VsaModel.N9010A ? 44e9 : 4e9;

        /// <summary>
        /// Screen-capture mode (#143): connect to the analyzer only (no ESG, no RF), grab its display via
        /// the model's <see cref="ScreenCaptureRecipe"/> (overridable per flag), and write the image bytes
        /// to <paramref name="outputPath"/>. Whatever the analyzer is showing at that moment is captured —
        /// so drive/settle the signal first (e.g. with a separate closed-loop run), then capture.
        /// </summary>
        private static int RunCaptureScreen(string vsaResource, VsaModel vsaModel, string outputPath,
            string saveCmd, string dataQuery, string cleanupCmd, string tempPath)
        {
            Console.WriteLine("ESG-SignalCreator — VSA screen capture");
            Console.WriteLine("VSA       : " + vsaResource + "  (requested " + vsaModel + ")");
            Console.WriteLine("Output    : " + outputPath);
            Console.WriteLine(new string('-', 64));

            IInstrument io = null;
            try
            {
                io = new VisaInstrument(vsaResource);
                var vsa = new VsaInstrument(io);
                InstrumentIdentity id = vsa.Identify();
                Console.WriteLine("Connected : " + id.Manufacturer + " / " + id.Model + " / FW " + id.FirmwareRevision +
                                  "  -> dialect " + vsa.Model);

                // Start from the model's default recipe, then apply any per-flag overrides.
                ScreenCaptureRecipe recipe = vsa.Dialect.ScreenCapture;
                if (recipe == null && dataQuery == null)
                {
                    Console.WriteLine("FAIL      : no default screen-capture recipe for " + vsa.Model +
                                      " — supply --capture-data-query (and --capture-save-cmd/--capture-temp-path).");
                    return 2;
                }
                recipe = (recipe ?? new ScreenCaptureRecipe(dataQuery))
                    .With(dataQueryFormat: dataQuery, saveCommandFormat: saveCmd,
                          cleanupCommandFormat: cleanupCmd, tempPath: tempPath);

                Console.WriteLine("Recipe    : save=" + (recipe.SaveCommandFormat ?? "(direct)") +
                                  "  data=" + recipe.DataQueryFormat +
                                  "  temp=" + (recipe.TempPath ?? "(none)"));

                byte[] image = vsa.CaptureScreen(recipe);
                if (image == null || image.Length == 0)
                {
                    Console.WriteLine("FAIL      : capture returned no data.");
                    return 2;
                }

                string dir = Path.GetDirectoryName(Path.GetFullPath(outputPath));
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllBytes(outputPath, image);

                Console.WriteLine("OK        : wrote " + image.Length + " bytes to " + Path.GetFullPath(outputPath));
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine("FAIL      : " + ex.Message);
                return 2;
            }
            finally
            {
                try { io?.Dispose(); } catch { /* best effort */ }
            }
        }

        /// <summary>Image file extension for a model's screen capture (X-Series writes PNG, E4406A GIF).</summary>
        private static string CaptureExtension(VsaModel model) => model == VsaModel.N9010A ? ".png" : ".gif";

        /// <summary>Filesystem-safe lowercase slug for a signal name, e.g. "IQ (multitone)" -> "iq-multitone".</summary>
        private static string Slug(string name)
        {
            var sb = new StringBuilder();
            bool lastDash = false;
            foreach (char c in (name ?? "").ToLowerInvariant())
            {
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')) { sb.Append(c); lastDash = false; }
                else if (!lastDash && sb.Length > 0) { sb.Append('-'); lastDash = true; }
            }
            return sb.ToString().Trim('-');
        }

        /// <summary>Write a markdown index (index.md) embedding the captured screenshots, ready to paste
        /// into the tutorials / Manual Verification doc.</summary>
        private static void WriteCaptureMarkdown(string dir, VsaModel model, InstallVerificationReport report,
            List<KeyValuePair<string, string>> captured)
        {
            if (captured.Count == 0) return;
            var byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in captured) byName[kv.Key] = kv.Value;

            var md = new StringBuilder();
            md.AppendLine("# VSA verification screenshots (" + model + ")");
            md.AppendLine();
            md.AppendLine("Captured automatically by `--install-verify --capture-dir`. Verdict: "
                + (report.AllPass ? "PASS" : "FAIL") + ".");
            md.AppendLine();
            foreach (InstallVerificationStep step in report.Steps)
            {
                if (!byName.TryGetValue(step.Name, out string file)) continue;
                md.AppendLine("## " + step.Name + (step.Pass ? "" : " — FAIL"));
                md.AppendLine();
                md.AppendLine("*" + step.Detail + "*");
                md.AppendLine();
                md.AppendLine("![" + step.Name + " on the analyzer (" + model + ")](" + file + ")");
                md.AppendLine();
            }
            File.WriteAllText(Path.Combine(dir, "index.md"), md.ToString());
            Console.WriteLine("            wrote " + Path.Combine(dir, "index.md"));
        }

        /// <summary>Headless install/configuration self-test (#126): run the shared InstallVerification
        /// battery (CW/AM/FM/I/Q) on the one user-selected analyzer, recording each expected-vs-measured
        /// result into the harness pass/fail + JSON report.</summary>
        /// <summary>Per-flag screen-capture options threaded into the install-verify run (#143).</summary>
        private sealed class CaptureOptions
        {
            public string Dir;
            public string SaveCmd, DataQuery, CleanupCmd, TempPath;
            public bool Enabled => !string.IsNullOrWhiteSpace(Dir);
        }

        private static void RunInstallVerify(EsgController esg, string vsaResource, VsaModel vsaModel,
            double carrierOverride, double verifyPowerDbm, double maxInputDbm, double pathLossDb,
            CaptureOptions capture = null)
        {
            Console.WriteLine(new string('-', 64));
            Console.WriteLine("Install verification (" + vsaModel + ") — CW / AM / FM / I/Q"
                + (capture != null && capture.Enabled ? "  (capturing screenshots to " + capture.Dir + ")" : ""));

            var safety = new RfPathSafety { Armed = true, AnalyzerMaxSafeInputDbm = maxInputDbm, PathLossDb = pathLossDb };
            Step("Safety gate allows verify power", () => PowerSafetyGate.Guard(verifyPowerDbm, safety));

            IInstrument vio = Step("Open VSA VISA session", () => new VisaInstrument(vsaResource));
            if (vio == null) return;
            try
            {
                var vsa = new VsaInstrument(vio);
                vsa.TimeoutMilliseconds = 30000;
                try { vsa.Write(":ABORt"); vsa.Clear(); } catch { /* recover a prior run */ }
                Step("VSA *IDN? identifies a " + vsaModel, () =>
                {
                    InstrumentIdentity id = vsa.Identify();
                    Console.WriteLine("            " + id.Manufacturer + " / " + id.Model + " / FW " + id.FirmwareRevision);
                    if (!vsa.IsModel(vsaModel)) throw new Exception("Model is not " + vsaModel + ": '" + id.Model + "'");
                });

                var opts = new InstallVerificationOptions
                {
                    CarrierHz = carrierOverride > 0 ? carrierOverride : 1e9,
                    PowerDbm = verifyPowerDbm,
                    PathLossDb = pathLossDb
                };

                // If capturing, build the recipe once (model default + any CLI overrides) and grab the
                // analyzer's display after each signal is measured, into <dir>/<slug>.<ext>.
                var captured = new List<KeyValuePair<string, string>>();
                Action<InstallVerificationStep> onStep = null;
                if (capture != null && capture.Enabled)
                {
                    Directory.CreateDirectory(capture.Dir);
                    string ext = CaptureExtension(vsaModel);
                    ScreenCaptureRecipe recipe = (vsa.Dialect.ScreenCapture ?? new ScreenCaptureRecipe(capture.DataQuery))
                        .With(capture.DataQuery, capture.SaveCmd, capture.CleanupCmd, capture.TempPath);
                    onStep = step =>
                    {
                        string file = Path.Combine(capture.Dir, Slug(step.Name) + ext);
                        byte[] img = vsa.CaptureScreen(recipe);
                        File.WriteAllBytes(file, img);
                        captured.Add(new KeyValuePair<string, string>(step.Name, Path.GetFileName(file)));
                        Console.WriteLine("            captured " + file + " (" + img.Length + " bytes)");
                    };
                }

                InstallVerificationReport report = InstallVerification.Run(esg, vsa, safety, opts,
                    msg => Console.WriteLine("            " + msg), onStepMeasured: onStep);

                if (capture != null && capture.Enabled)
                    WriteCaptureMarkdown(capture.Dir, vsaModel, report, captured);

                foreach (InstallVerificationStep step in report.Steps)
                {
                    Console.WriteLine("Signal '" + step.Name + "' — " + step.Detail);
                    foreach (VerificationResult r in step.Results)
                        Compare(step.Name + " · " + r.Metric, r.Expected, r.Measured, r.Tolerance, r.Unit);
                    CheckErrorQueueVsa(vsa, step.Name);
                }
            }
            catch (Exception ex) { Fail("Install verification", ex.Message); }
            finally
            {
                try { esg.SetArbState(false); esg.SetRfOutput(false); } catch { /* best effort */ }
                try { vio?.Dispose(); } catch { /* best effort */ }
            }
        }

        private static void RunClosedLoop(EsgController esg, string vsaResource, VsaModel vsaModel, double[] sweepHz, string[] signals,
            double verifyPowerDbm, double offsetHz, double maxInputDbm, double pathLossDb, double dwellSeconds)
        {
            Console.WriteLine(new string('-', 64));
            Console.WriteLine("Closed-loop verification (" + vsaModel + ") — signals: " + string.Join(", ", signals));
            Console.WriteLine("Sweep: " + sweepHz.Length + " point(s): "
                + string.Join(", ", sweepHz.Select(x => (x / 1e6).ToString("0.###", CultureInfo.InvariantCulture) + " MHz")));

            var safety = new RfPathSafety { Armed = true, AnalyzerMaxSafeInputDbm = maxInputDbm, PathLossDb = pathLossDb };
            Step("Safety gate allows verify power", () => PowerSafetyGate.Guard(verifyPowerDbm, safety));

            IInstrument vio = Step("Open VSA VISA session", () => new VisaInstrument(vsaResource));
            if (vio == null) return;

            try
            {
                var vsa = new VsaInstrument(vio);
                vsa.TimeoutMilliseconds = 30000; // averaged measurements (CCDF/ACP) can take many seconds
                try { vsa.Write(":ABORt"); vsa.Clear(); } catch { /* recover any measurement left running by a prior run */ }
                Step("VSA *IDN? identifies a " + vsaModel, () =>
                {
                    InstrumentIdentity id = vsa.Identify();
                    Console.WriteLine("            " + id.Manufacturer + " / " + id.Model + " / FW " + id.FirmwareRevision);
                    if (!vsa.IsModel(vsaModel)) throw new Exception("Model is not " + vsaModel + ": '" + id.Model + "'");
                });
                Step("VSA options", () => Console.WriteLine("            *OPT? = " + string.Join(",", vsa.Options())));
                vsa.Clear();

                double expectedAnalyzerDbm = verifyPowerDbm - pathLossDb;
                bool armed = false;

                foreach (string sigName in signals)
                {
                    SignalCase sig = BuildSignal(sigName, offsetHz);
                    Console.WriteLine(new string('-', 64));
                    Console.WriteLine("Signal '" + sig.Name + "': " + sig.Waveform.Length + " samples @ "
                        + (sig.Waveform.SampleRateHz / 1e6).ToString("0.###", CultureInfo.InvariantCulture)
                        + " MHz, expected PAPR " + sig.ExpectedPaprDb.ToString("0.##", CultureInfo.InvariantCulture) + " dB");

                    Step("Download '" + sig.Name + "' to WFM1", () => esg.DownloadWaveform("HILTEST", sig.Waveform));
                    CheckErrorQueue(esg, "after '" + sig.Name + "' download");
                    Step("Select + arm ARB ('" + sig.Name + "')", () => esg.PlayWaveform("HILTEST", sig.Waveform.SampleRateHz, 70));

                    for (int p = 0; p < sweepHz.Length; p++)
                    {
                        double carrierHz = sweepHz[p];
                        string label = sig.Name + " [" + (p + 1) + "/" + sweepHz.Length + "] " +
                            (carrierHz / 1e6).ToString("0.###", CultureInfo.InvariantCulture) + " MHz";

                        Step("Drive ESG " + label + " @ " + verifyPowerDbm + " dBm, RF ON", () =>
                        {
                            PowerSafetyGate.Guard(verifyPowerDbm, safety); // re-check immediately before emitting
                            esg.SetFrequencyHz(carrierHz);
                            esg.SetAmplitudeDbm(verifyPowerDbm);
                            esg.SetArbState(true);
                            esg.SetModulation(true); // ARB I/Q only reaches the RF output when modulation is ON
                            if (!armed) { esg.SetRfOutput(true); armed = true; }
                        });
                        vsa.SetSingleMeasurement(); // accurate, triggered reads
                        Thread.Sleep(1200); // settle

                        ChannelPowerResult cp = Step("Channel power " + label, () =>
                        {
                            ChannelPowerResult r = ChannelPower.Measure(vsa, carrierHz, sig.SpanHz, sig.SpanHz);
                            Console.WriteLine("            total power = " + r.TotalPowerDbm.ToString("0.##", CultureInfo.InvariantCulture) + " dBm");
                            return r;
                        });
                        CcdfResult ccdf = Step("CCDF / PAPR " + label, () =>
                        {
                            CcdfResult r = Ccdf.Measure(vsa, carrierHz);
                            Console.WriteLine("            PAPR = " + r.PaprDb.ToString("0.##", CultureInfo.InvariantCulture) + " dB");
                            return r;
                        });
                        if (cp != null) Compare("Channel power " + label, expectedAnalyzerDbm, cp.TotalPowerDbm, 3.0, "dBm");
                        if (ccdf != null) Compare("PAPR " + label, sig.ExpectedPaprDb, ccdf.PaprDb, 2.5, "dB");

                        if (sig.CheckTone)
                        {
                            SpectrumResult sp = Step("Spectrum peak " + label, () =>
                            {
                                SpectrumResult r = SpectrumMarker.MeasurePeak(vsa, carrierHz, sig.SpanHz);
                                Console.WriteLine("            peak = " + (r.MarkerFrequencyHz / 1e6).ToString("0.######", CultureInfo.InvariantCulture)
                                    + " MHz @ " + r.MarkerPowerDbm.ToString("0.##", CultureInfo.InvariantCulture) + " dBm");
                                return r;
                            });
                            if (sp != null) Compare("Tone frequency " + label, carrierHz + offsetHz, sp.MarkerFrequencyHz, 50e3, "Hz");
                        }
                        if (sig.CheckImage)
                        {
                            // Narrow span so the wanted tone (carrier+offset) and its image
                            // (carrier-offset) are isolated by separate peak searches.
                            double imgSpan = Math.Max(0.5e6, Math.Abs(sig.WantedOffsetHz));
                            SpectrumResult wanted = Step("Wanted tone " + label, () =>
                                SpectrumMarker.MeasurePeak(vsa, carrierHz + sig.WantedOffsetHz, imgSpan));
                            SpectrumResult image = Step("Image tone " + label, () =>
                                SpectrumMarker.MeasurePeak(vsa, carrierHz - sig.WantedOffsetHz, imgSpan));
                            if (wanted != null && image != null)
                            {
                                double supp = image.MarkerPowerDbm - wanted.MarkerPowerDbm;
                                Console.WriteLine("            wanted " + wanted.MarkerPowerDbm.ToString("0.#", CultureInfo.InvariantCulture)
                                    + " dBm, image " + image.MarkerPowerDbm.ToString("0.#", CultureInfo.InvariantCulture)
                                    + " dBm -> suppression " + supp.ToString("0.#", CultureInfo.InvariantCulture) + " dBc");
                                // A 3 dB gain imbalance yields a measurable, suppressed image.
                                Check("Image present + suppressed " + label, supp < -3.0 && supp > -45.0,
                                    "suppression " + supp.ToString("0.#", CultureInfo.InvariantCulture) + " dBc");
                            }
                        }
                        if (sig.CheckAcp)
                        {
                            AcpResult acp = Step("ACP " + label, () =>
                            {
                                AcpResult r = Acp.Measure(vsa, carrierHz, sig.AcpCarrierBwHz);
                                Console.WriteLine("            adj L/U = " + r.LowerAdjacentDbc.ToString("0.#", CultureInfo.InvariantCulture)
                                    + "/" + r.UpperAdjacentDbc.ToString("0.#", CultureInfo.InvariantCulture)
                                    + " dBc; offsets L=" + Fmt(r.LowerOffsetsDbc) + " U=" + Fmt(r.UpperOffsetsDbc) + " dBc");
                                return r;
                            });
                            if (acp != null)
                            {
                                double worst = WorstAcprDbc(acp);
                                // A real modulated carrier has well-suppressed adjacent power; garbage would be near 0 dBc.
                                if (!double.IsNaN(worst))
                                    Check("ACPR bounded " + label, worst < -20.0,
                                        "worst offset " + worst.ToString("0.#", CultureInfo.InvariantCulture) + " dBc (want < -20)");
                            }
                        }
                        CheckErrorQueueVsa(vsa, label); // graded

                        if (dwellSeconds > 0)
                        {
                            vsa.SetContinuous(true); // running mode -> live front-panel sweep
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.WriteLine("            >>> RF ON, analyzer running — '" + sig.Name + "' near " +
                                (carrierHz / 1e6).ToString("0.###", CultureInfo.InvariantCulture) + " MHz for " +
                                dwellSeconds.ToString(CultureInfo.InvariantCulture) + " s…");
                            Console.ResetColor();
                            Thread.Sleep((int)(dwellSeconds * 1000));
                        }
                    }
                }

                // Clean end state: RF off, but leave the analyzer running so the panel shows the
                // true (no-signal) sweep live rather than a frozen last-triggered trace.
                Step("RF off, analyzer left running (live no-signal display)", () =>
                {
                    esg.SetRfOutput(false);
                    esg.SetArbState(false);
                    vsa.SetContinuous(true);
                });
            }
            finally
            {
                try { esg.SetRfOutput(false); esg.SetArbState(false); } catch { /* best effort */ }
                try { vio.Dispose(); } catch { /* best effort */ }
                Console.WriteLine("            RF returned to OFF.");
            }
        }

        /// <summary>
        /// Amplitude accuracy / flatness (#99): a CW carrier stepped over several power levels at each
        /// frequency, verifying the measured channel power tracks the commanded level (path-loss aware).
        /// </summary>
        private static void RunFlatness(EsgController esg, string vsaResource, VsaModel vsaModel, double[] sweepHz,
            double maxInputDbm, double pathLossDb, double dwellSeconds)
        {
            double[] powers = { -40, -30, -20, -10 };
            Console.WriteLine(new string('-', 64));
            Console.WriteLine("Amplitude accuracy / flatness — levels: "
                + string.Join(", ", powers.Select(p => p.ToString("0", CultureInfo.InvariantCulture) + " dBm"))
                + " over " + sweepHz.Length + " freq point(s)");

            var safety = new RfPathSafety { Armed = true, AnalyzerMaxSafeInputDbm = maxInputDbm, PathLossDb = pathLossDb };
            Step("Safety gate allows max level", () => PowerSafetyGate.Guard(powers.Max(), safety));

            IInstrument vio = Step("Open VSA VISA session", () => new VisaInstrument(vsaResource));
            if (vio == null) return;
            try
            {
                var vsa = new VsaInstrument(vio);
                vsa.TimeoutMilliseconds = 30000;
                try { vsa.Write(":ABORt"); vsa.Clear(); } catch { /* recover */ }
                Step("VSA *IDN? identifies a " + vsaModel, () => { if (!vsa.IsModel(vsaModel)) throw new Exception("not a " + vsaModel); });

                var src = new CwPersonality();
                src.LoadConfig(new CwConfig { SampleRateHz = 10e6, Length = 4096, FreqOffsetHz = 1e6 });
                WaveformModel wf = src.Calculate(new Progress<int>());
                const double span = 5e6;
                Step("Download + arm CW", () => { esg.DownloadWaveform("HILTEST", wf); esg.PlayWaveform("HILTEST", wf.SampleRateHz, 70); });
                bool armed = false;

                foreach (double f in sweepHz)
                {
                    foreach (double pw in powers)
                    {
                        string label = (f / 1e6).ToString("0.###", CultureInfo.InvariantCulture) + " MHz @ " + pw.ToString("0", CultureInfo.InvariantCulture) + " dBm";
                        Step("Set " + label, () =>
                        {
                            PowerSafetyGate.Guard(pw, safety);
                            esg.SetFrequencyHz(f);
                            esg.SetAmplitudeDbm(pw);
                            esg.SetArbState(true);
                            esg.SetModulation(true);
                            if (!armed) { esg.SetRfOutput(true); armed = true; }
                        });
                        vsa.SetSingleMeasurement();
                        Thread.Sleep(1000);
                        ChannelPowerResult cp = Step("Channel power " + label, () =>
                        {
                            ChannelPowerResult r = ChannelPower.Measure(vsa, f, span, span);
                            Console.WriteLine("            measured " + r.TotalPowerDbm.ToString("0.##", CultureInfo.InvariantCulture) + " dBm");
                            return r;
                        });
                        if (cp != null) Compare("Level accuracy " + label, pw - pathLossDb, cp.TotalPowerDbm, 3.0, "dBm");
                        CheckErrorQueueVsa(vsa, label);
                    }
                    if (dwellSeconds > 0) { vsa.SetContinuous(true); Thread.Sleep((int)(dwellSeconds * 1000)); }
                }
                Step("RF off, analyzer left running", () => { esg.SetRfOutput(false); esg.SetArbState(false); vsa.SetContinuous(true); });
            }
            finally
            {
                try { esg.SetRfOutput(false); esg.SetArbState(false); } catch { /* best effort */ }
                try { vio.Dispose(); } catch { /* best effort */ }
                Console.WriteLine("            RF returned to OFF.");
            }
        }

        /// <summary>A signal personality to verify: its generated waveform + expected metrics + which checks apply.</summary>
        private sealed class SignalCase
        {
            public string Name;
            public WaveformModel Waveform;
            public double ExpectedPaprDb; // computed from the generated I/Q (the RF should match)
            public double SpanHz;
            public double AcpCarrierBwHz;  // ACP carrier integration bandwidth (modulated signals)
            public bool CheckTone;        // CW: verify the tone lands at carrier+offset
            public bool CheckAcp;         // modulated signals: measure adjacent-channel power
            public bool CheckImage;       // I/Q impairment: verify the gain-imbalance image
            public double WantedOffsetHz; // tone offset for the image check (image at carrier - offset)
        }

        /// <summary>Generate a signal with a Core personality and capture its expected metrics.</summary>
        private static SignalCase BuildSignal(string name, double offsetHz)
        {
            var progress = new Progress<int>();
            switch ((name ?? "cw").ToLowerInvariant())
            {
                case "multitone":
                {
                    var p = new MultitonePersonality();
                    p.LoadConfig(new MultitoneConfig
                    {
                        SampleRateHz = 10e6, Length = 16384, Phase = PhaseStrategy.Newman,
                        Tones = MultitonePersonality.AutoSpacing(4, 1e6, 0, 0)
                    });
                    WaveformModel wf = p.Calculate(progress);
                    return new SignalCase { Name = "multitone", Waveform = wf, ExpectedPaprDb = Papr(wf), SpanHz = 10e6 };
                }
                case "awgn":
                {
                    var p = new AwgnPersonality();
                    p.LoadConfig(new AwgnConfig { SampleRateHz = 10e6, Length = 32768, NoiseBandwidthHz = 2e6, CrestFactorDb = 10 });
                    WaveformModel wf = p.Calculate(progress);
                    return new SignalCase { Name = "awgn", Waveform = wf, ExpectedPaprDb = Papr(wf), SpanHz = 5e6 };
                }
                case "custom-mod":
                case "qam":
                {
                    var p = new CustomModPersonality();
                    p.LoadConfig(new CustomModConfig
                    {
                        Modulation = Modulation.QAM16, SymbolRateHz = 1e6, SamplesPerSymbol = 8, Alpha = 0.35, SymbolCount = 1024
                    });
                    WaveformModel wf = p.Calculate(progress);
                    // Carrier reference BW ≈ symbol rate × (1 + alpha) = 1e6 × 1.35.
                    return new SignalCase { Name = "custom-mod", Waveform = wf, ExpectedPaprDb = Papr(wf), SpanHz = 5e6, AcpCarrierBwHz = 1.35e6, CheckAcp = true };
                }
                case "multi-carrier":
                {
                    var p = new MultiCarrierPersonality();
                    p.LoadConfig(new MultiCarrierConfig
                    {
                        SampleRateHz = 10e6, Length = 16384,
                        Carriers = MultiCarrierPersonality.EvenlySpaced(3, 1e6, 0)
                    });
                    WaveformModel wf = p.Calculate(progress);
                    return new SignalCase { Name = "multi-carrier", Waveform = wf, ExpectedPaprDb = Papr(wf), SpanHz = 10e6 };
                }
                case "import-iq":
                {
                    // Round-trip: generate a CW, export it to an I/Q CSV, re-import it, and verify.
                    var src = new CwPersonality();
                    src.LoadConfig(new CwConfig { SampleRateHz = 10e6, Length = 4096, FreqOffsetHz = offsetHz });
                    string path = Path.Combine(Path.GetTempPath(), "hil-import-iq.csv");
                    WaveformExporter.SaveCsv(path, src.Calculate(progress));
                    var p = new ImportIqPersonality();
                    p.LoadConfig(new ImportIqConfig { Path = path, SampleRateHz = 10e6 });
                    WaveformModel wf = p.Calculate(progress);
                    return new SignalCase
                    {
                        Name = "import-iq", Waveform = wf, ExpectedPaprDb = Papr(wf),
                        SpanHz = Math.Max(1e6, 4 * Math.Abs(offsetHz) + 1e6), CheckTone = true
                    };
                }
                case "iq-impair":
                {
                    // A CW tone at +offset with a deliberate I/Q gain imbalance produces a measurable
                    // image at carrier - offset; verify the image is present and suppressed.
                    var p = new CwPersonality();
                    p.LoadConfig(new CwConfig { SampleRateHz = 10e6, Length = 4096, FreqOffsetHz = offsetHz });
                    WaveformModel wf = IqImpairments.Apply(p.Calculate(progress), new IqImpairmentConfig { GainImbalanceDb = 3.0 });
                    return new SignalCase
                    {
                        Name = "iq-impair", Waveform = wf, ExpectedPaprDb = Papr(wf),
                        SpanHz = Math.Max(1e6, 4 * Math.Abs(offsetHz) + 1e6),
                        CheckImage = true, WantedOffsetHz = offsetHz
                    };
                }
                default:
                {
                    var p = new CwPersonality();
                    p.LoadConfig(new CwConfig { SampleRateHz = 10e6, Length = 4096, FreqOffsetHz = offsetHz });
                    WaveformModel wf = p.Calculate(progress);
                    return new SignalCase
                    {
                        Name = "cw", Waveform = wf, ExpectedPaprDb = Papr(wf),
                        SpanHz = Math.Max(1e6, 4 * Math.Abs(offsetHz) + 1e6), CheckTone = true
                    };
                }
            }
        }

        // Expected PAPR of the generated baseband I/Q (the RF envelope should match it). Dsp.Ccdf is
        // fully qualified to avoid the name clash with the Measure.Ccdf analyzer measurement.
        private static double Papr(WaveformModel wf) => EsgSignalCreator.Dsp.Ccdf.PaprDb(ToD(wf.I), ToD(wf.Q));

        private static double[] ToD(float[] x)
        {
            var d = new double[x.Length];
            for (int i = 0; i < x.Length; i++) d[i] = x[i];
            return d;
        }

        private static string Fmt(double[] a) =>
            a == null || a.Length == 0 ? "—" : string.Join("/", a.Select(v => v.ToString("0.#", CultureInfo.InvariantCulture)));

        private static void Check(string name, bool ok, string detail)
        {
            if (ok) Pass(name); else Fail(name, detail);
        }

        /// <summary>Worst (least-negative) real offset ACPR in dBc, ignoring 0 and the ~-999 sentinel.</summary>
        private static double WorstAcprDbc(AcpResult a)
        {
            double worst = double.NaN;
            foreach (double v in a.LowerOffsetsDbc.Concat(a.UpperOffsetsDbc))
                if (v > -200 && v < -1 && (double.IsNaN(worst) || v > worst)) worst = v;
            return worst;
        }

        private static void Compare(string metric, double expected, double measured, double tol, string unit)
        {
            double delta = measured - expected;
            string name = string.Format(CultureInfo.InvariantCulture,
                "{0}: expected {1:0.###} {4}, measured {2:0.###} {4} (Δ {3:+0.###;-0.###}, tol ±{5:0.###})",
                metric, expected, measured, delta, unit, tol);
            if (Math.Abs(delta) <= tol) Pass(name);
            else Fail(name, "out of tolerance");
        }

        // ---- step helpers ----

        private static T Step<T>(string name, Func<T> action)
        {
            try { T result = action(); Pass(name); return result; }
            catch (Exception ex) { Fail(name, ex.Message); return default(T); }
        }

        private static void Step(string name, Action action) => Step<object>(name, () => { action(); return null; });

        private static void Warn(string name, Func<bool> action, string warning)
        {
            try
            {
                if (action()) Pass(name);
                else { Console.ForegroundColor = ConsoleColor.Yellow; Console.WriteLine("[WARN] " + name + " — " + warning); Console.ResetColor(); }
            }
            catch (Exception ex) { Fail(name, ex.Message); }
        }

        private static void CheckErrorQueue(EsgController esg, string when) =>
            Step("ESG error queue clean " + when, () => { if (!IsClean(esg.GetError())) throw new Exception(":SYSTem:ERRor? not clean"); });

        private static void CheckErrorQueueVsa(VsaInstrument vsa, string when) =>
            Step("VSA error queue clean " + when, () =>
            {
                // Drain the queue; classify benign data-clip/range warnings (e.g. -222 "value clipped")
                // separately from genuine command errors (e.g. -113 "undefined header").
                var errs = new List<string>();
                var warns = new List<string>();
                for (int i = 0; i < 20; i++)
                {
                    string e = vsa.GetError();
                    if (IsClean(e)) break;
                    string t = e.Trim();
                    // Benign advisories: positive codes (device advisories like +36 "Signal near noise
                    // floor"), data-clip/range warnings (-222). Genuine command errors (e.g. -113) fail.
                    bool advisory = t.StartsWith("+")
                        || t.IndexOf("clip", StringComparison.OrdinalIgnoreCase) >= 0
                        || t.IndexOf("noise floor", StringComparison.OrdinalIgnoreCase) >= 0
                        || t.StartsWith("-222");
                    if (advisory) warns.Add(t);
                    else errs.Add(t);
                }
                if (warns.Count > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("            (warning) " + string.Join(" | ", warns));
                    Console.ResetColor();
                }
                if (errs.Count > 0) throw new Exception(string.Join(" | ", errs));
            });

        private static bool IsClean(string err)
        {
            if (string.IsNullOrEmpty(err)) return true;
            string e = err.TrimStart();
            return e.StartsWith("0") || e.StartsWith("+0");
        }

        private static string Opt(Dictionary<string, string> o, string k) => o.TryGetValue(k, out string v) ? v : null;
        private static double Num(Dictionary<string, string> o, string k, double def) =>
            o.TryGetValue(k, out string v) && double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? d : def;

        private static void Pass(string name)
        {
            _results.Add(new CheckRecord { Name = name, Ok = true });
            Console.ForegroundColor = ConsoleColor.Green; Console.Write("[PASS] "); Console.ResetColor();
            Console.WriteLine(name);
        }

        private static void Fail(string name, string message)
        {
            _failures++;
            _results.Add(new CheckRecord { Name = name, Ok = false, Detail = message });
            Console.ForegroundColor = ConsoleColor.Red; Console.Write("[FAIL] "); Console.ResetColor();
            Console.WriteLine(name + " — " + message);
        }

        private static int Finish()
        {
            if (_jsonPath != null)
            {
                try { WriteJson(_jsonPath); Console.WriteLine("Wrote JSON report: " + _jsonPath); }
                catch (Exception ex) { Console.WriteLine("JSON report failed: " + ex.Message); }
            }
            Console.WriteLine(new string('-', 64));
            if (_failures == 0) { Console.ForegroundColor = ConsoleColor.Green; Console.WriteLine("ALL CHECKS PASSED"); }
            else { Console.ForegroundColor = ConsoleColor.Red; Console.WriteLine(_failures + " CHECK(S) FAILED"); }
            Console.ResetColor();
            return _failures == 0 ? 0 : 1;
        }

        private static void WriteJson(string path)
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"passed\": ").Append(_results.Count(r => r.Ok)).Append(",\n");
            sb.Append("  \"failed\": ").Append(_failures).Append(",\n");
            sb.Append("  \"checks\": [\n");
            for (int i = 0; i < _results.Count; i++)
            {
                CheckRecord r = _results[i];
                sb.Append("    { \"name\": ").Append(JsonStr(r.Name)).Append(", \"ok\": ").Append(r.Ok ? "true" : "false");
                if (!string.IsNullOrEmpty(r.Detail)) sb.Append(", \"detail\": ").Append(JsonStr(r.Detail));
                sb.Append(i < _results.Count - 1 ? " },\n" : " }\n");
            }
            sb.Append("  ]\n}\n");
            File.WriteAllText(path, sb.ToString());
        }

        private static string JsonStr(string s) =>
            "\"" + (s ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}
