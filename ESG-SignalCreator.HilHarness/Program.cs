using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using EsgSignalCreator;
using EsgSignalCreator.Instruments;
using EsgSignalCreator.Measure;
using EsgSignalCreator.Measure.Results;
using EsgSignalCreator.Model;
using EsgSignalCreator.Personalities.Awgn;
using EsgSignalCreator.Personalities.CustomMod;
using EsgSignalCreator.Personalities.Cw;
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

        private static int Main(string[] args)
        {
            var positionals = new List<string>();
            var opts = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var flags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var valueFlags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "--esg", "--vsa", "--verify-power-dbm", "--carrier-hz", "--offset-hz", "--max-input-dbm", "--path-loss-db", "--dwell-seconds", "--points", "--start-hz", "--stop-hz", "--signal" };
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
            double verifyPowerDbm = Num(opts, "--verify-power-dbm", -10.0);
            double offsetHz = Num(opts, "--offset-hz", 1e6);
            double maxInputDbm = Num(opts, "--max-input-dbm", 30.0);
            double pathLossDb = Num(opts, "--path-loss-db", 0.0);
            double dwellSeconds = Num(opts, "--dwell-seconds", 0.0);
            // Frequency sweep across the E4438C's range (the default for hardware tests). A single
            // --carrier-hz overrides the sweep with one point.
            double carrierOverride = Num(opts, "--carrier-hz", 0.0);
            int points = (int)Num(opts, "--points", 7);
            double startHz = Num(opts, "--start-hz", 50e6);
            double stopHz = Num(opts, "--stop-hz", 0.0); // 0 = query the ESG's max (capped to the VSA's 4 GHz)
            // Which signal personalities to verify. --all runs the full battery; --signal X runs one.
            string[] signals = flags.Contains("--all")
                ? new[] { "cw", "multitone", "awgn", "custom-mod" }
                : new[] { Opt(opts, "--signal") ?? "cw" };

            Console.WriteLine("ESG-SignalCreator hardware-in-the-loop harness");
            Console.WriteLine("ESG       : " + esgResource);
            Console.WriteLine("VSA       : " + (closedLoop ? vsaResource + "  (CLOSED-LOOP)" : "(not used)"));
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

                if (closedLoop)
                {
                    double[] sweep = BuildSweep(esg, carrierOverride, points, startHz, stopHz);
                    RunClosedLoop(esg, vsaResource, sweep, signals, verifyPowerDbm, offsetHz, maxInputDbm, pathLossDb, dwellSeconds);
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
        /// (stop defaults to the ESG's queried max, capped to the E4406A's ~4 GHz ceiling).</summary>
        private static double[] BuildSweep(EsgController esg, double carrierOverride, int points, double startHz, double stopHz)
        {
            if (carrierOverride > 0) return new[] { carrierOverride };
            if (points < 1) points = 1;
            if (stopHz <= 0)
            {
                try { stopHz = Math.Min(esg.GetMaxFrequencyHz(), 4e9); } // the E4406A tops out ~4 GHz
                catch { stopHz = 3e9; }
            }
            if (stopHz < startHz) stopHz = startHz;
            if (points == 1) return new[] { startHz };
            var f = new double[points];
            double step = (stopHz - startHz) / (points - 1);
            for (int i = 0; i < points; i++) f[i] = startHz + i * step;
            return f;
        }

        private static void RunClosedLoop(EsgController esg, string vsaResource, double[] sweepHz, string[] signals,
            double verifyPowerDbm, double offsetHz, double maxInputDbm, double pathLossDb, double dwellSeconds)
        {
            Console.WriteLine(new string('-', 64));
            Console.WriteLine("Closed-loop verification (E4406A) — signals: " + string.Join(", ", signals));
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
                Step("VSA *IDN? identifies an E4406A", () =>
                {
                    InstrumentIdentity id = vsa.Identify();
                    Console.WriteLine("            " + id.Manufacturer + " / " + id.Model + " / FW " + id.FirmwareRevision);
                    if (!vsa.IsE4406A()) throw new Exception("Model is not E4406A: '" + id.Model + "'");
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
                        CheckErrorQueueVsa(vsa, label); // graded — runs BEFORE the informational ACP

                        if (sig.CheckAcp)
                        {
                            // ACP is informational: its Basic-mode SCPI is still being hardware-truthed
                            // (#69/#95), so don't let its errors fail the graded battery — clear after.
                            Step("ACP " + label + " (informational)", () =>
                            {
                                AcpResult r = Acp.Measure(vsa, carrierHz, sig.SpanHz);
                                Console.WriteLine("            center = " + r.CenterPowerDbm.ToString("0.##", CultureInfo.InvariantCulture)
                                    + " dBm; lower ACPR = " + Fmt(r.LowerOffsetsDbc) + " dBc; upper = " + Fmt(r.UpperOffsetsDbc) + " dBc");
                            });
                            try { vsa.Clear(); } catch { /* clear any WIP ACP SCPI error */ }
                        }

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

        /// <summary>A signal personality to verify: its generated waveform + expected metrics + which checks apply.</summary>
        private sealed class SignalCase
        {
            public string Name;
            public WaveformModel Waveform;
            public double ExpectedPaprDb; // computed from the generated I/Q (the RF should match)
            public double SpanHz;
            public bool CheckTone;        // CW: verify the tone lands at carrier+offset
            public bool CheckAcp;         // modulated signals: report adjacent-channel power
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
                    return new SignalCase { Name = "custom-mod", Waveform = wf, ExpectedPaprDb = Papr(wf), SpanHz = 5e6, CheckAcp = true };
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
                    if (t.IndexOf("clip", StringComparison.OrdinalIgnoreCase) >= 0 || t.StartsWith("-222"))
                        warns.Add(t);
                    else
                        errs.Add(t);
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
            Console.ForegroundColor = ConsoleColor.Green; Console.Write("[PASS] "); Console.ResetColor();
            Console.WriteLine(name);
        }

        private static void Fail(string name, string message)
        {
            _failures++;
            Console.ForegroundColor = ConsoleColor.Red; Console.Write("[FAIL] "); Console.ResetColor();
            Console.WriteLine(name + " — " + message);
        }

        private static int Finish()
        {
            Console.WriteLine(new string('-', 64));
            if (_failures == 0) { Console.ForegroundColor = ConsoleColor.Green; Console.WriteLine("ALL CHECKS PASSED"); }
            else { Console.ForegroundColor = ConsoleColor.Red; Console.WriteLine(_failures + " CHECK(S) FAILED"); }
            Console.ResetColor();
            return _failures == 0 ? 0 : 1;
        }
    }
}
