using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using EsgSignalCreator;
using EsgSignalCreator.Instruments;
using EsgSignalCreator.Measure;
using EsgSignalCreator.Measure.Results;
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
                { "--esg", "--vsa", "--verify-power-dbm", "--carrier-hz", "--offset-hz", "--max-input-dbm", "--path-loss-db", "--dwell-seconds" };
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
            double carrierHz = Num(opts, "--carrier-hz", 1e9);
            double offsetHz = Num(opts, "--offset-hz", 1e6);
            double maxInputDbm = Num(opts, "--max-input-dbm", 30.0);
            double pathLossDb = Num(opts, "--path-loss-db", 0.0);
            double dwellSeconds = Num(opts, "--dwell-seconds", 0.0);

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

                WaveformResult gen = Step("Generate CW waveform (Core, +" + (offsetHz / 1e6) + " MHz offset)", () =>
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
                    RunClosedLoop(esg, vsaResource, verifyPowerDbm, carrierHz, offsetHz, maxInputDbm, pathLossDb, dwellSeconds);
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

        private static void RunClosedLoop(EsgController esg, string vsaResource,
            double verifyPowerDbm, double carrierHz, double offsetHz, double maxInputDbm, double pathLossDb,
            double dwellSeconds)
        {
            Console.WriteLine(new string('-', 64));
            Console.WriteLine("Closed-loop verification (E4406A)");

            var safety = new RfPathSafety { Armed = true, AnalyzerMaxSafeInputDbm = maxInputDbm, PathLossDb = pathLossDb };
            Step("Safety gate allows verify power", () => PowerSafetyGate.Guard(verifyPowerDbm, safety));

            IInstrument vio = Step("Open VSA VISA session", () => new VisaInstrument(vsaResource));
            if (vio == null) return;

            try
            {
                var vsa = new VsaInstrument(vio);
                Step("VSA *IDN? identifies an E4406A", () =>
                {
                    InstrumentIdentity id = vsa.Identify();
                    Console.WriteLine("            " + id.Manufacturer + " / " + id.Model + " / FW " + id.FirmwareRevision);
                    if (!vsa.IsE4406A()) throw new Exception("Model is not E4406A: '" + id.Model + "'");
                });
                Step("VSA options", () => Console.WriteLine("            *OPT? = " + string.Join(",", vsa.Options())));
                vsa.Clear();

                double expectedToneHz = carrierHz + offsetHz;
                double expectedAnalyzerDbm = verifyPowerDbm - pathLossDb;
                double spanHz = Math.Max(1e6, 4 * Math.Abs(offsetHz) + 1e6);

                Step("Drive ESG: carrier " + (carrierHz / 1e9) + " GHz, " + verifyPowerDbm + " dBm, RF ON", () =>
                {
                    PowerSafetyGate.Guard(verifyPowerDbm, safety); // re-check immediately before emitting
                    esg.SetFrequencyHz(carrierHz);
                    esg.SetAmplitudeDbm(verifyPowerDbm);
                    esg.SetArbState(true);
                    esg.SetModulation(true); // ARB I/Q only reaches the RF output when modulation is ON
                    esg.SetRfOutput(true);
                });
                Thread.Sleep(1500); // settle

                ChannelPowerResult cp = Step("Measure channel power (E4406A)", () =>
                {
                    // Integrate across the span so the +offset CW tone is inside the channel.
                    ChannelPowerResult r = ChannelPower.Measure(vsa, carrierHz, spanHz, spanHz);
                    Console.WriteLine("            total power = " + r.TotalPowerDbm.ToString("0.##", CultureInfo.InvariantCulture) + " dBm");
                    return r;
                });
                SpectrumResult sp = Step("Measure spectrum peak (E4406A)", () =>
                {
                    SpectrumResult r = SpectrumMarker.MeasurePeak(vsa, carrierHz, spanHz);
                    Console.WriteLine("            peak = " + (r.MarkerFrequencyHz / 1e6).ToString("0.######", CultureInfo.InvariantCulture)
                        + " MHz @ " + r.MarkerPowerDbm.ToString("0.##", CultureInfo.InvariantCulture) + " dBm");
                    return r;
                });

                if (sp != null)
                    Compare("Tone frequency", expectedToneHz, sp.MarkerFrequencyHz, 50e3, "Hz");
                if (cp != null)
                    Compare("Channel power", expectedAnalyzerDbm, cp.TotalPowerDbm, 3.0, "dBm");

                CheckErrorQueueVsa(vsa, "after measurements");

                if (dwellSeconds > 0)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("            >>> RF held ON for " + dwellSeconds.ToString(CultureInfo.InvariantCulture) +
                        " s — watch the ESG (1 GHz, " + verifyPowerDbm + " dBm, ARB) and the E4406A display…");
                    Console.ResetColor();
                    Thread.Sleep((int)(dwellSeconds * 1000));
                }
            }
            finally
            {
                try { esg.SetRfOutput(false); esg.SetArbState(false); } catch { /* best effort */ }
                try { vio.Dispose(); } catch { /* best effort */ }
                Console.WriteLine("            RF returned to OFF.");
            }
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
                // Drain the queue so a single stale error doesn't mask later ones; report all.
                var errs = new List<string>();
                for (int i = 0; i < 20; i++)
                {
                    string e = vsa.GetError();
                    if (IsClean(e)) break;
                    errs.Add(e.Trim());
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
