using System;
using EsgSignalCreator.Model;

namespace EsgSignalCreator.Personalities.Pulse
{
    /// <summary>
    /// A pulsed-signal personality (Signal Studio for Pulse Building, N7620A): builds a single
    /// pulse — with optional raised-cosine edges and intra-pulse modulation — and tiles it across
    /// the requested waveform length at a fixed, staggered, or jittered pulse-repetition interval,
    /// optionally applying per-pulse frequency/phase/power offsets. Targets radar / EW receiver test.
    /// </summary>
    /// <remarks>
    /// The pulse envelope peaks at unit magnitude and a final pass peak-normalizes the composite to
    /// 1.0. Intra-pulse modulation covers unmodulated, linear- and non-linear-FM chirp, stepped
    /// frequency/amplitude, BPSK/QPSK/Barker phase codes, and Frank/P4 polyphase codes. The remaining
    /// N7620A Option 205/206 features (antenna-scan patterning, pattern nesting, CSV import/export,
    /// scenario impairments) are out of scope — see the N7620A requirements doc and issue #179.
    /// </remarks>
    public sealed class PulsePersonality : IWaveformPersonality
    {
        private PulseConfig _config = new PulseConfig();

        /// <inheritdoc/>
        public string Id => "pulse";

        /// <inheritdoc/>
        public string DisplayName => "Pulse Building";

        /// <inheritdoc/>
        public int? RequiredOption => null;

        /// <summary>Number of complete pulses placed in the most recent <see cref="Calculate"/> result.</summary>
        public int LastPulseCount { get; private set; }

        /// <summary>Pulse duty cycle (pulse width / mean interval), 0..1, of the most recent configuration.</summary>
        public double LastDutyCycle { get; private set; }

        /// <inheritdoc/>
        public object GetConfig() => _config;

        /// <inheritdoc/>
        public void LoadConfig(object cfg)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            if (!(cfg is PulseConfig pc))
                throw new ArgumentException("Expected a PulseConfig.", nameof(cfg));
            _config = pc;
        }

        /// <inheritdoc/>
        public WaveformModel Calculate(IProgress<int> progress)
        {
            PulseConfig cfg = _config ?? new PulseConfig();

            if (cfg.SampleRateHz <= 0)
                throw new InvalidOperationException("SampleRateHz must be positive.");
            if (cfg.Length <= 0)
                throw new InvalidOperationException("Length must be positive.");
            if (cfg.PulseWidthSec <= 0)
                throw new InvalidOperationException("PulseWidthSec must be positive.");
            if (cfg.PriSec <= 0)
                throw new InvalidOperationException("PriSec must be positive.");
            if (cfg.PriSec < cfg.PulseWidthSec)
                throw new InvalidOperationException("PriSec must be >= PulseWidthSec (pulses would overlap).");
            if (cfg.StartDelaySec < 0)
                throw new InvalidOperationException("StartDelaySec must be >= 0.");
            ValidatePriPattern(cfg);

            double fs = cfg.SampleRateHz;
            int n = cfg.Length;
            int pulseN = (int)Math.Round(cfg.PulseWidthSec * fs);
            int startN = (int)Math.Round(cfg.StartDelaySec * fs);
            if (pulseN < 1)
                throw new InvalidOperationException("PulseWidthSec is too short for the sample rate (< 1 sample).");

            // Build one pulse template (unit-peak envelope × intra-pulse modulation).
            float[] tplI = new float[pulseN];
            float[] tplQ = new float[pulseN];
            BuildPulseTemplate(cfg, fs, pulseN, tplI, tplQ);

            bool hasOffsets =
                (cfg.PerPulseFrequencyOffsetsHz != null && cfg.PerPulseFrequencyOffsetsHz.Length > 0) ||
                (cfg.PerPulsePhaseOffsetsDeg != null && cfg.PerPulsePhaseOffsetsDeg.Length > 0) ||
                (cfg.PerPulsePowerOffsetsDb != null && cfg.PerPulsePowerOffsetsDb.Length > 0);

            var i = new float[n];
            var q = new float[n];
            byte[] markers = cfg.EmitPulseMarkers ? new byte[n] : null;

            var rng = cfg.PriMode == PriMode.Jittered ? new Random(cfg.PriJitterSeed) : null;

            // Tile the pulse across the waveform, advancing by the per-pulse interval. A pulse is
            // counted "complete" only if it fits entirely before the end of the buffer.
            int pulseCount = 0;
            int pulseIndex = 0;
            double gapSum = 0.0;      // accumulated intervals, for mean-duty reporting
            int gapCount = 0;
            double posD = startN;
            int lastPct = -1;
            while (true)
            {
                int pos = (int)Math.Round(posD);
                if (pos >= n) break;

                if (pos + pulseN <= n) pulseCount++;
                if (markers != null && pos >= 0 && pos < n) markers[pos] = 1;

                PlacePulse(cfg, fs, tplI, tplQ, pulseN, pos, pulseIndex, hasOffsets, i, q, n);

                double gapN = NextGapSamples(cfg, fs, pulseIndex, rng);
                gapSum += gapN;
                gapCount++;
                posD += gapN;
                pulseIndex++;

                if (progress != null)
                {
                    int pct = (int)((pos - startN) * 100L / Math.Max(1, n - startN));
                    if (pct != lastPct) { progress.Report(Math.Min(99, Math.Max(0, pct))); lastPct = pct; }
                }
            }

            NormalizePeak(i, q);

            double meanGapN = gapCount > 0 ? gapSum / gapCount : cfg.PriSec * fs;
            LastPulseCount = pulseCount;
            LastDutyCycle = meanGapN > 0 ? pulseN / meanGapN : 0.0;
            progress?.Report(100);

            return new WaveformModel(i, q, fs, "Pulse", markers);
        }

        /// <summary>Validate the staggered/jittered PRI settings against the pulse width.</summary>
        private static void ValidatePriPattern(PulseConfig cfg)
        {
            switch (cfg.PriMode)
            {
                case PriMode.Staggered:
                    if (cfg.StaggerPatternSec != null)
                        foreach (double p in cfg.StaggerPatternSec)
                        {
                            if (p <= 0)
                                throw new InvalidOperationException("StaggerPatternSec values must be positive.");
                            if (p < cfg.PulseWidthSec)
                                throw new InvalidOperationException("StaggerPatternSec values must be >= PulseWidthSec (pulses would overlap).");
                        }
                    break;
                case PriMode.Jittered:
                    if (cfg.PriJitterSec < 0)
                        throw new InvalidOperationException("PriJitterSec must be >= 0.");
                    if (cfg.PriSec - cfg.PriJitterSec < cfg.PulseWidthSec)
                        throw new InvalidOperationException("PriSec - PriJitterSec must be >= PulseWidthSec (pulses could overlap).");
                    break;
            }
        }

        /// <summary>The interval, in samples, from pulse <paramref name="pulseIndex"/> to the next.</summary>
        private static double NextGapSamples(PulseConfig cfg, double fs, int pulseIndex, Random rng)
        {
            switch (cfg.PriMode)
            {
                case PriMode.Staggered:
                {
                    double[] pat = cfg.StaggerPatternSec;
                    double sec = (pat != null && pat.Length > 0) ? pat[pulseIndex % pat.Length] : cfg.PriSec;
                    return sec * fs;
                }
                case PriMode.Jittered:
                {
                    double j = rng != null ? (rng.NextDouble() * 2.0 - 1.0) * cfg.PriJitterSec : 0.0;
                    return (cfg.PriSec + j) * fs;
                }
                case PriMode.Fixed:
                default:
                    return cfg.PriSec * fs;
            }
        }

        /// <summary>
        /// Copy the base template into the output at <paramref name="pos"/>, applying this pulse's
        /// per-pulse frequency/phase/power offset (a fast direct copy when no offsets are configured).
        /// </summary>
        private static void PlacePulse(PulseConfig cfg, double fs, float[] tplI, float[] tplQ, int pulseN,
            int pos, int pulseIndex, bool hasOffsets, float[] i, float[] q, int n)
        {
            int copy = Math.Min(pulseN, n - pos);
            if (!hasOffsets)
            {
                for (int k = 0; k < copy; k++)
                {
                    int dst = pos + k;
                    i[dst] = tplI[k];
                    q[dst] = tplQ[k];
                }
                return;
            }

            double fOff = Cycle(cfg.PerPulseFrequencyOffsetsHz, pulseIndex);
            double phOff = Cycle(cfg.PerPulsePhaseOffsetsDeg, pulseIndex) * Math.PI / 180.0;
            double pwrDb = Cycle(cfg.PerPulsePowerOffsetsDb, pulseIndex);
            double amp = Math.Pow(10.0, pwrDb / 20.0);

            for (int k = 0; k < copy; k++)
            {
                double ang = phOff + 2.0 * Math.PI * fOff * (k / fs);
                double c = Math.Cos(ang), s = Math.Sin(ang);
                double bi = tplI[k], bq = tplQ[k];
                int dst = pos + k;
                i[dst] = (float)(amp * (bi * c - bq * s));
                q[dst] = (float)(amp * (bi * s + bq * c));
            }
        }

        /// <summary>Cyclically index an offset table (returns 0 for a null/empty table).</summary>
        private static double Cycle(double[] table, int index)
            => (table != null && table.Length > 0) ? table[index % table.Length] : 0.0;

        /// <summary>
        /// Fill <paramref name="tplI"/>/<paramref name="tplQ"/> with a single unit-peak pulse:
        /// a raised-cosine-edged envelope (× a per-sample amplitude for AM step) multiplied by the
        /// configured intra-pulse phase modulation.
        /// </summary>
        private static void BuildPulseTemplate(PulseConfig cfg, double fs, int pulseN, float[] tplI, float[] tplQ)
        {
            int riseN = (int)Math.Round(cfg.RiseFallSec * fs);
            if (riseN < 0) riseN = 0;
            if (riseN * 2 > pulseN) riseN = pulseN / 2; // ramps may meet but not overlap

            double b = cfg.ChirpBandwidthHz; // total swept bandwidth for FM formats

            int[] barker = cfg.Modulation == IntraPulseModulation.BarkerPhase
                ? BarkerCode(cfg.BarkerLength)
                : null;
            double[] chipPhase = ChipPhaseCode(cfg, out int chipCount);

            // FM formats build phase by integrating instantaneous frequency sample-by-sample.
            double accPhase = 0.0;

            for (int k = 0; k < pulseN; k++)
            {
                double env = Envelope(k, pulseN, riseN);
                double phase;

                switch (cfg.Modulation)
                {
                    case IntraPulseModulation.LinearFmChirp:
                    {
                        // Closed-form linear chirp: phi(t) = 2*pi*(-B/2 * t + 0.5*rate*t^2), t = k/fs.
                        double t = k / fs;
                        double rate = pulseN / fs > 0 ? b / (pulseN / fs) : 0.0;
                        phase = 2.0 * Math.PI * (-0.5 * b * t + 0.5 * rate * t * t);
                        break;
                    }
                    case IntraPulseModulation.NonLinearFmChirp:
                    {
                        accPhase += 2.0 * Math.PI * NlfmFrequency(cfg, k, pulseN, b) / fs;
                        phase = accPhase;
                        break;
                    }
                    case IntraPulseModulation.FmStep:
                    {
                        accPhase += 2.0 * Math.PI * FmStepFrequency(cfg, k, pulseN, b) / fs;
                        phase = accPhase;
                        break;
                    }
                    case IntraPulseModulation.AmStep:
                    {
                        int steps = Math.Max(1, cfg.IntraPulseStepCount);
                        int s = (int)((long)k * steps / pulseN);
                        if (s >= steps) s = steps - 1;
                        env *= (s + 1.0) / steps; // rising staircase, peak 1.0 in the final step
                        phase = 0.0;
                        break;
                    }
                    case IntraPulseModulation.BarkerPhase:
                    {
                        int len = barker.Length;
                        int chip = (int)((long)k * len / pulseN);
                        if (chip >= len) chip = len - 1;
                        phase = barker[chip] >= 0 ? 0.0 : Math.PI;
                        break;
                    }
                    case IntraPulseModulation.Bpsk:
                    case IntraPulseModulation.Qpsk:
                    case IntraPulseModulation.FrankCode:
                    case IntraPulseModulation.PolyphaseP4:
                    {
                        int chip = (int)((long)k * chipCount / pulseN);
                        if (chip >= chipCount) chip = chipCount - 1;
                        phase = chipPhase[chip];
                        break;
                    }
                    case IntraPulseModulation.None:
                    default:
                        phase = 0.0;
                        break;
                }

                tplI[k] = (float)(env * Math.Cos(phase));
                tplQ[k] = (float)(env * Math.Sin(phase));
            }
        }

        /// <summary>Instantaneous frequency (Hz) of the non-linear (cubic) FM law at sample k.</summary>
        private static double NlfmFrequency(PulseConfig cfg, int k, int pulseN, double b)
        {
            double c = cfg.NlfmCurvature;
            if (c < 0) c = 0; if (c > 0.999) c = 0.999;
            double x = pulseN > 1 ? 2.0 * k / (pulseN - 1) - 1.0 : 0.0; // -1..+1
            // Monotonic cubic mapping: f(±1) = ±B/2 for any c in [0,1).
            return 0.5 * b * ((1.0 - c) * x + c * x * x * x);
        }

        /// <summary>Instantaneous frequency (Hz) of the stepped-frequency law at sample k.</summary>
        private static double FmStepFrequency(PulseConfig cfg, int k, int pulseN, double b)
        {
            int steps = Math.Max(1, cfg.IntraPulseStepCount);
            int s = (int)((long)k * steps / pulseN);
            if (s >= steps) s = steps - 1;
            if (steps == 1) return 0.0;
            return -0.5 * b + s * (b / (steps - 1)); // -B/2 .. +B/2 in equal steps
        }

        /// <summary>
        /// The per-chip phase sequence (radians) for the phase/polyphase code formats
        /// (BPSK, QPSK, Frank, P4). Returns null and <paramref name="chipCount"/> = 0 for other formats.
        /// </summary>
        private static double[] ChipPhaseCode(PulseConfig cfg, out int chipCount)
        {
            switch (cfg.Modulation)
            {
                case IntraPulseModulation.Bpsk:
                {
                    int len = Math.Max(1, cfg.PhaseCodeChips);
                    var ph = new double[len];
                    var rng = new Random(cfg.PhaseCodeSeed);
                    for (int c = 0; c < len; c++) ph[c] = rng.Next(2) == 0 ? 0.0 : Math.PI;
                    chipCount = len; return ph;
                }
                case IntraPulseModulation.Qpsk:
                {
                    int len = Math.Max(1, cfg.PhaseCodeChips);
                    var ph = new double[len];
                    var rng = new Random(cfg.PhaseCodeSeed);
                    for (int c = 0; c < len; c++) ph[c] = rng.Next(4) * (Math.PI / 2.0);
                    chipCount = len; return ph;
                }
                case IntraPulseModulation.FrankCode:
                {
                    int nOrder = Math.Max(1, cfg.FrankOrderN);
                    int len = nOrder * nOrder;
                    var ph = new double[len];
                    for (int m = 0; m < nOrder; m++)
                        for (int p = 0; p < nOrder; p++)
                            ph[m * nOrder + p] = 2.0 * Math.PI / nOrder * m * p;
                    chipCount = len; return ph;
                }
                case IntraPulseModulation.PolyphaseP4:
                {
                    int len = Math.Max(1, cfg.PolyphaseLength);
                    var ph = new double[len];
                    for (int c = 0; c < len; c++) ph[c] = Math.PI * ((double)c * c / len) - Math.PI * c;
                    chipCount = len; return ph;
                }
                default:
                    chipCount = 0; return null;
            }
        }

        /// <summary>
        /// Raised-cosine pulse envelope: ramps up over the first <paramref name="riseN"/> samples,
        /// holds at 1.0, then ramps down over the final <paramref name="riseN"/> samples. With
        /// <paramref name="riseN"/> == 0 the pulse is rectangular (constant 1.0).
        /// </summary>
        private static double Envelope(int k, int pulseN, int riseN)
        {
            if (riseN <= 0) return 1.0;
            if (k < riseN)
                return 0.5 * (1.0 - Math.Cos(Math.PI * (k + 1.0) / (riseN + 1.0)));
            if (k >= pulseN - riseN)
            {
                int fromEnd = pulseN - 1 - k; // 0 at the last sample
                return 0.5 * (1.0 - Math.Cos(Math.PI * (fromEnd + 1.0) / (riseN + 1.0)));
            }
            return 1.0;
        }

        /// <summary>Normalize so the peak vector magnitude sqrt(I²+Q²) is exactly 1.0 (no-op if all zero).</summary>
        private static void NormalizePeak(float[] i, float[] q)
        {
            double peak = 0.0;
            for (int s = 0; s < i.Length; s++)
            {
                double m = Math.Sqrt((double)i[s] * i[s] + (double)q[s] * q[s]);
                if (m > peak) peak = m;
            }
            if (peak <= 0.0) return;
            double scale = 1.0 / peak;
            for (int s = 0; s < i.Length; s++)
            {
                i[s] = (float)(i[s] * scale);
                q[s] = (float)(q[s] * scale);
            }
        }

        /// <summary>
        /// The canonical Barker code of the requested length as ±1 chips. Barker codes exist only
        /// for lengths 2, 3, 4, 5, 7, 11, and 13.
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when no Barker code exists for <paramref name="length"/>.</exception>
        public static int[] BarkerCode(int length)
        {
            switch (length)
            {
                case 2: return new[] { +1, -1 };
                case 3: return new[] { +1, +1, -1 };
                case 4: return new[] { +1, +1, -1, +1 };
                case 5: return new[] { +1, +1, +1, -1, +1 };
                case 7: return new[] { +1, +1, +1, -1, -1, +1, -1 };
                case 11: return new[] { +1, +1, +1, -1, -1, -1, +1, -1, -1, +1, -1 };
                case 13: return new[] { +1, +1, +1, +1, +1, -1, -1, +1, +1, -1, +1, -1, +1 };
                default:
                    throw new ArgumentException(
                        "No Barker code of length " + length + " exists (valid: 2, 3, 4, 5, 7, 11, 13).",
                        nameof(length));
            }
        }
    }
}
