using System;
using System.Collections.Generic;
using System.Threading;
using EsgSignalCreator.Model;
using EsgSignalCreator.Personalities.Multitone;
using EsgSignalCreator.Visa;
using EsgSignalCreator.Waveform;

namespace EsgSignalCreator.Verify
{
    /// <summary>Options for the install/configuration self-test (issue #125).</summary>
    public sealed class InstallVerificationOptions
    {
        /// <summary>Carrier the battery is played/measured at, in hertz.</summary>
        public double CarrierHz { get; set; } = 1e9;

        /// <summary>Commanded ESG output power, in dBm (kept low for the analyzer).</summary>
        public double PowerDbm { get; set; } = -10.0;

        /// <summary>Inline path loss between ESG and analyzer, in dB.</summary>
        public double PathLossDb { get; set; } = 0.0;

        /// <summary>ARB sample rate for the synthesized signals, in hertz.</summary>
        public double SampleRateHz { get; set; } = 10e6;

        /// <summary>
        /// Settle delay after arming RF before measuring, in ms (0 to skip — used by tests). Generous by
        /// default so the ESG ALC re-levels for each new ARB waveform and the analyzer auto-ranges before
        /// the read — a short wait made the highest-PAPR signal (the multitone) read low intermittently.
        /// </summary>
        public int SettleMs { get; set; } = 3000;

        public double PowerToleranceDb { get; set; } = 3.0;
        public double PaprToleranceDb { get; set; } = 2.5;
        public double ToneToleranceHz { get; set; } = 50e3;
        public double MeasurementSpanHz { get; set; } = 5e6;
    }

    /// <summary>One signal's expected-vs-measured results within an install-verification run.</summary>
    public sealed class InstallVerificationStep
    {
        public InstallVerificationStep(string name, string detail, IReadOnlyList<VerificationResult> results,
            IReadOnlyList<string> warnings = null)
        {
            Name = name;
            Detail = detail;
            Results = results ?? new List<VerificationResult>();
            Warnings = warnings ?? new List<string>();
        }

        public string Name { get; }
        public string Detail { get; }
        public IReadOnlyList<VerificationResult> Results { get; }

        /// <summary>Analyzer error-queue entries seen during this step (e.g. input overload) — #120.</summary>
        public IReadOnlyList<string> Warnings { get; }

        public bool Pass => VerificationHarness.AllPass(Results);
    }

    /// <summary>The full install-verification run: one step per signal type, with an overall verdict.</summary>
    public sealed class InstallVerificationReport
    {
        public InstallVerificationReport(IReadOnlyList<InstallVerificationStep> steps)
        {
            Steps = steps ?? new List<InstallVerificationStep>();
        }

        public IReadOnlyList<InstallVerificationStep> Steps { get; }

        public bool AllPass
        {
            get
            {
                if (Steps.Count == 0) return false;
                foreach (InstallVerificationStep s in Steps) if (!s.Pass) return false;
                return true;
            }
        }

        /// <summary>All results across steps, each metric prefixed with its step name, for a single table.</summary>
        public IReadOnlyList<VerificationResult> Flatten()
        {
            var flat = new List<VerificationResult>();
            foreach (InstallVerificationStep s in Steps)
                foreach (VerificationResult r in s.Results)
                    flat.Add(new VerificationResult(s.Name + " · " + r.Metric, r.Expected, r.Measured, r.Tolerance, r.Unit));
            return flat;
        }
    }

    /// <summary>
    /// Install/configuration self-test (issue #125): synthesizes a short battery of signals — a CW tone,
    /// AM, FM, and a simple digital I/Q multitone — plays each through the ESG's ARB, and measures it on
    /// the connected analyzer (E4406A or N9010A), comparing expected-vs-measured. Together (unmodulated →
    /// amplitude → frequency/phase → complex I/Q) they exercise the full generate→measure range and prove
    /// the install + configuration end-to-end. Reuses <see cref="VerificationHarness.Verify"/> per signal.
    /// </summary>
    public static class InstallVerification
    {
        private sealed class Signal
        {
            public string Name;
            public string Detail;
            public WaveformModel Model;
            public double ToneOffsetHz; // >0 adds a spectrum-peak frequency check (CW only)
        }

        /// <summary>
        /// Run the battery. Drives ESG power, so the <see cref="PowerSafetyGate"/> is enforced before any
        /// RF is emitted. Always returns RF to off. <paramref name="progress"/> receives a line per step;
        /// <paramref name="cancelled"/> (optional) is polled between steps for cooperative cancellation.
        /// </summary>
        public static InstallVerificationReport Run(
            EsgController esg,
            VsaInstrument vsa,
            RfPathSafety safety,
            InstallVerificationOptions opts,
            Action<string> progress = null,
            Func<bool> cancelled = null,
            Action<InstallVerificationStep> onStepMeasured = null)
        {
            if (esg == null) throw new ArgumentNullException(nameof(esg));
            if (vsa == null) throw new ArgumentNullException(nameof(vsa));
            opts = opts ?? new InstallVerificationOptions();
            safety = safety ?? new RfPathSafety();

            var profile = new VerificationProfile
            {
                PathLossDb = opts.PathLossDb,
                PowerToleranceDb = opts.PowerToleranceDb,
                PaprToleranceDb = opts.PaprToleranceDb,
                FrequencyToleranceHz = opts.ToneToleranceHz,
                MeasurementSpanHz = opts.MeasurementSpanHz
            };

            var steps = new List<InstallVerificationStep>();
            bool rfArmed = false;
            try
            {
                foreach (Signal sig in BuildSignals(opts))
                {
                    if (cancelled != null && cancelled()) break;
                    progress?.Invoke("Verifying " + sig.Name + "…");

                    // Enforce the input-damage gate immediately before commanding power / emitting RF.
                    PowerSafetyGate.Guard(opts.PowerDbm, safety);

                    esg.DownloadWaveform("INSTVERIFY", sig.Model);
                    esg.PlayWaveform("INSTVERIFY", sig.Model.SampleRateHz);
                    esg.SetFrequencyHz(opts.CarrierHz);
                    esg.SetAmplitudeDbm(opts.PowerDbm);
                    esg.SetArbState(true);
                    esg.SetModulation(true); // ARB I/Q only reaches the RF output when modulation is ON
                    if (!rfArmed) { esg.SetRfOutput(true); rfArmed = true; }

                    vsa.SetSingleMeasurement();
                    if (opts.SettleMs > 0) Thread.Sleep(opts.SettleMs);

                    IReadOnlyList<VerificationResult> results =
                        VerificationHarness.Verify(vsa, sig.Model, opts.CarrierHz, opts.PowerDbm, profile, sig.ToneOffsetHz);
                    IReadOnlyList<string> warnings = vsa.ReadErrorQueue(); // surface input overload etc. (#120)
                    var step = new InstallVerificationStep(sig.Name, sig.Detail, results, warnings);
                    steps.Add(step);

                    // The signal is still armed and on the analyzer here, so a caller can capture the
                    // analyzer's display (e.g. a screenshot) for this exact signal before the next one
                    // is loaded (#143).
                    if (onStepMeasured != null)
                    {
                        try { onStepMeasured(step); }
                        catch { /* a capture failure must not fail the verification run */ }
                    }
                }
            }
            finally
            {
                try { esg.SetArbState(false); esg.SetRfOutput(false); } catch { /* best effort */ }
            }

            return new InstallVerificationReport(steps);
        }

        /// <summary>The fixed CW → AM → FM → I/Q battery, synthesized as ARB I/Q.</summary>
        private static IEnumerable<Signal> BuildSignals(InstallVerificationOptions opts)
        {
            double sr = opts.SampleRateHz;
            const double toneOffset = 1e6;

            // 1) CW tone — unmodulated carrier offset by 1 MHz (constant envelope, PAPR ≈ 0 dB).
            yield return new Signal
            {
                Name = "CW",
                Detail = "Unmodulated tone at carrier + 1 MHz",
                Model = ToModel(WaveformGenerator.Generate(new WaveformSpec
                {
                    Type = SignalType.SingleTone, SampleRateHz = sr, TargetLength = 4096, OffsetHz = toneOffset
                }).Waveform, "instverify-cw"),
                ToneOffsetHz = toneOffset
            };

            // 2) AM — 50% depth at 100 kHz, placed on a +1 MHz complex subcarrier. The raw AM baseband
            //    (I = 1 + m·sin, Q = 0) is real-only with a large DC term, which the E4438C ARB does not
            //    reproduce at level (the carrier came out ~60 dB low). Rotating it onto an offset carrier
            //    (like CW) gives a proper complex signal with no DC and non-zero Q; the envelope — and so
            //    the 3 dB PAPR — is unchanged.
            yield return new Signal
            {
                Name = "AM",
                Detail = "50% AM at 100 kHz on a +1 MHz subcarrier",
                Model = AmOnSubcarrier(sr, toneOffset)
            };

            // 3) FM — 500 kHz deviation at 100 kHz (constant envelope, PAPR ≈ 0 dB — the frequency path).
            yield return new Signal
            {
                Name = "FM",
                Detail = "500 kHz deviation at 100 kHz",
                Model = ToModel(WaveformGenerator.Generate(new WaveformSpec
                {
                    Type = SignalType.Fm, SampleRateHz = sr, TargetLength = 8192, RateHz = 100e3, FmDeviationHz = 500e3
                }).Waveform, "instverify-fm")
            };

            // 4) Simple digital I/Q — a 4-tone Newman multitone (higher PAPR — the full complex path).
            var mt = new MultitonePersonality();
            mt.LoadConfig(new MultitoneConfig
            {
                SampleRateHz = sr,
                Length = 16384,
                Phase = PhaseStrategy.Newman,
                Tones = MultitonePersonality.AutoSpacing(4, 1e6, 0, 0)
            });
            yield return new Signal
            {
                Name = "IQ (multitone)",
                Detail = "4-tone Newman multitone, 1 MHz spacing",
                Model = mt.Calculate(new Progress<int>())
            };
        }

        /// <summary>
        /// Generate 50%/100 kHz AM and rotate it onto a complex subcarrier at <paramref name="offsetHz"/>
        /// (seamless: the nearest whole number of cycles over the waveform length), removing the DC term
        /// and the all-zero Q of the raw AM baseband. The envelope (and 3 dB PAPR) is preserved.
        /// </summary>
        private static WaveformModel AmOnSubcarrier(double sr, double offsetHz)
        {
            IqWaveform am = WaveformGenerator.Generate(new WaveformSpec
            {
                Type = SignalType.Am, SampleRateHz = sr, TargetLength = 8192, RateHz = 100e3, AmDepthPercent = 50
            }).Waveform;

            int n = am.Length;
            int cycles = (int)Math.Round(offsetHz * n / sr); // integer cycles -> seamless loop
            double w = 2.0 * Math.PI * cycles / n;

            var i = new float[n];
            var q = new float[n];
            for (int k = 0; k < n; k++)
            {
                double c = Math.Cos(w * k), s = Math.Sin(w * k);
                i[k] = (float)(am.I[k] * c - am.Q[k] * s);
                q[k] = (float)(am.I[k] * s + am.Q[k] * c);
            }
            return new WaveformModel(i, q, sr, "instverify-am");
        }

        private static WaveformModel ToModel(IqWaveform wf, string name)
        {
            var i = new float[wf.I.Length];
            var q = new float[wf.Q.Length];
            for (int n = 0; n < i.Length; n++) { i[n] = (float)wf.I[n]; q[n] = (float)wf.Q[n]; }
            return new WaveformModel(i, q, wf.SampleRateHz, name);
        }
    }
}
