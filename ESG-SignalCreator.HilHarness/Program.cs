using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using EsgSignalCreator;
using EsgSignalCreator.Instruments;
using EsgSignalCreator.Visa;
using EsgSignalCreator.Waveform;

namespace EsgSignalCreator.HilHarness
{
    /// <summary>
    /// Hardware-in-the-loop test harness (issue #59). Drives a <b>real</b> E4438C over VISA to
    /// validate the Core ARB download/play path that the offline unit tests can't reach.
    ///
    /// Usage:  ESG-SignalCreator.HilHarness [resource] [--rf-on]
    ///   resource : VISA resource string (default: $ESG_VISA_RESOURCE or the bench unit).
    ///   --rf-on  : briefly enable RF at low power for the smoke test (off by default).
    ///
    /// Safety: RF stays OFF and amplitude LOW (-30 dBm) unless --rf-on is given; the error queue
    /// is polled after every step. Exit code 0 = all pass, non-zero = a failure occurred.
    /// </summary>
    internal static class Program
    {
        private const string DefaultResource = "TCPIP0::192.168.1.82::inst1::INSTR";
        private const double SafeAmplitudeDbm = -30.0;

        private static int _failures;

        private static int Main(string[] args)
        {
            bool rfOn = args.Any(a => string.Equals(a, "--rf-on", StringComparison.OrdinalIgnoreCase));
            string resource = args.FirstOrDefault(a => !a.StartsWith("--"))
                ?? Environment.GetEnvironmentVariable("ESG_VISA_RESOURCE")
                ?? DefaultResource;

            Console.WriteLine("ESG-SignalCreator hardware-in-the-loop harness");
            Console.WriteLine("Resource : " + resource);
            Console.WriteLine("RF        : " + (rfOn ? "WILL be enabled briefly (--rf-on)" : "OFF (safe default)"));
            Console.WriteLine(new string('-', 60));

            IInstrument io = null;
            EsgController esg = null;
            try
            {
                io = Step("Open VISA session", () => new VisaInstrument(resource));
                if (io == null) return Finish();

                var inst = new EsgInstrument(io);
                Step("*IDN? identifies an E4438C", () =>
                {
                    InstrumentIdentity id = inst.Identify();
                    Console.WriteLine("            " + id.Manufacturer + " / " + id.Model + " / FW " + id.FirmwareRevision);
                    if (id.Model == null || id.Model.IndexOf("E4438C", StringComparison.OrdinalIgnoreCase) < 0)
                        throw new Exception("Model is not E4438C: '" + id.Model + "'");
                });
                Warn("Baseband generator option present", () =>
                {
                    string[] opts = inst.Options();
                    Console.WriteLine("            *OPT? = " + string.Join(",", opts));
                    return inst.HasBasebandGenerator();
                }, "no 001/002/601/602 option — ARB playback needs the baseband hardware");

                esg = new EsgController(io);

                Step("Set safe state (RF off, mod off, low power, *CLS)", () =>
                {
                    io.Write("*CLS");
                    esg.SetRfOutput(false);
                    esg.SetModulation(false);
                    esg.SetAmplitudeDbm(SafeAmplitudeDbm);
                });
                CheckErrorQueue(esg, "after safe state");

                WaveformResult gen = Step("Generate CW waveform (Core)", () =>
                    WaveformGenerator.Generate(new WaveformSpec
                    {
                        Type = SignalType.SingleTone,
                        SampleRateHz = 10e6,
                        TargetLength = 4096,
                        OffsetHz = 1e6
                    }));
                IqWaveform wf = gen?.Waveform;

                if (wf != null)
                {
                    Step("Download waveform to WFM1", () => esg.DownloadWaveform("HILTEST", wf));
                    CheckErrorQueue(esg, "after download");

                    Step("Select + arm ARB (sample clock, 70% scaling)", () =>
                    {
                        esg.PlayWaveform("HILTEST", wf.SampleRateHz, 70);
                    });
                    CheckErrorQueue(esg, "after ARB arm (catches DAC over-range)");
                }

                Step("Frequency set/read-back (1 GHz)", () =>
                {
                    esg.SetFrequencyHz(1e9);
                    double back = esg.GetFrequencyHz();
                    if (Math.Abs(back - 1e9) > 1.0) throw new Exception("read back " + back.ToString("G", CultureInfo.InvariantCulture) + " Hz");
                });
                Step("Amplitude set/read-back (" + SafeAmplitudeDbm + " dBm)", () =>
                {
                    esg.SetAmplitudeDbm(SafeAmplitudeDbm);
                    double back = esg.GetAmplitudeDbm();
                    if (Math.Abs(back - SafeAmplitudeDbm) > 0.5) throw new Exception("read back " + back + " dBm");
                });

                if (rfOn)
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

        private static void CheckErrorQueue(EsgController esg, string when)
        {
            Step("Error queue clean " + when, () =>
            {
                string err = esg.GetError();
                if (!IsClean(err)) throw new Exception(":SYSTem:ERRor? = " + err);
            });
        }

        private static bool IsClean(string err)
        {
            if (string.IsNullOrEmpty(err)) return true;
            string e = err.TrimStart();
            return e.StartsWith("0") || e.StartsWith("+0");
        }

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
            Console.WriteLine(new string('-', 60));
            if (_failures == 0) { Console.ForegroundColor = ConsoleColor.Green; Console.WriteLine("ALL CHECKS PASSED"); }
            else { Console.ForegroundColor = ConsoleColor.Red; Console.WriteLine(_failures + " CHECK(S) FAILED"); }
            Console.ResetColor();
            return _failures == 0 ? 0 : 1;
        }
    }
}
