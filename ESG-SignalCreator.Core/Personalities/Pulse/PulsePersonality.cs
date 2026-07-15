using System;
using EsgSignalCreator.Model;

namespace EsgSignalCreator.Personalities.Pulse
{
    /// <summary>
    /// A pulsed-signal personality (Signal Studio for Pulse Building, N7620A): builds a single
    /// pulse — with optional raised-cosine edges and intra-pulse modulation (unmodulated,
    /// linear-FM chirp, or a Barker phase code) — and repeats it at a fixed pulse-repetition
    /// interval (PRI) to fill the requested waveform length. Targets radar / EW receiver test.
    /// </summary>
    /// <remarks>
    /// The pulse envelope peaks at unit magnitude, so the summed waveform is already normalized to
    /// a peak vector magnitude of 1.0 (a final normalize pass guards against rounding). Advanced
    /// N7620A features (per-pulse frequency/phase/power offsets, staggered/jittered PRI, antenna
    /// scan patterns, pattern nesting, CSV import/export) are out of scope for this v1 — see the
    /// N7620A requirements doc and the verification epic.
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

        /// <summary>Pulse duty cycle (pulse width / PRI), 0..1, of the most recent configuration.</summary>
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

            double fs = cfg.SampleRateHz;
            int n = cfg.Length;
            int pulseN = (int)Math.Round(cfg.PulseWidthSec * fs);
            int priN = (int)Math.Round(cfg.PriSec * fs);
            int startN = (int)Math.Round(cfg.StartDelaySec * fs);
            if (pulseN < 1)
                throw new InvalidOperationException("PulseWidthSec is too short for the sample rate (< 1 sample).");
            if (priN < 1)
                throw new InvalidOperationException("PriSec is too short for the sample rate (< 1 sample).");

            // Build one pulse template (unit-peak envelope × intra-pulse modulation).
            float[] tplI = new float[pulseN];
            float[] tplQ = new float[pulseN];
            BuildPulseTemplate(cfg, fs, pulseN, tplI, tplQ);

            var i = new float[n];
            var q = new float[n];
            byte[] markers = cfg.EmitPulseMarkers ? new byte[n] : null;

            // Tile the pulse across the waveform at each PRI. A pulse is counted "complete" only
            // if it fits entirely before the end of the buffer.
            int pulseCount = 0;
            int reportEvery = Math.Max(1, n / 100);
            int lastPct = -1;
            for (long pos = startN; pos < n; pos += priN)
            {
                bool complete = pos + pulseN <= n;
                if (complete) pulseCount++;
                if (markers != null && pos >= 0 && pos < n) markers[(int)pos] = 1;

                int copy = (int)Math.Min(pulseN, n - pos);
                for (int k = 0; k < copy; k++)
                {
                    int dst = (int)pos + k;
                    i[dst] = tplI[k];
                    q[dst] = tplQ[k];
                }

                if (progress != null)
                {
                    int pct = (int)((pos - startN) * 100 / Math.Max(1, n - startN));
                    if (pct != lastPct) { progress.Report(Math.Min(99, Math.Max(0, pct))); lastPct = pct; }
                }
            }

            NormalizePeak(i, q);

            LastPulseCount = pulseCount;
            LastDutyCycle = priN > 0 ? (double)pulseN / priN : 0.0;
            progress?.Report(100);

            return new WaveformModel(i, q, fs, "Pulse", markers);
        }

        /// <summary>
        /// Fill <paramref name="tplI"/>/<paramref name="tplQ"/> with a single unit-peak pulse:
        /// a raised-cosine-edged envelope multiplied by the configured intra-pulse modulation.
        /// </summary>
        private static void BuildPulseTemplate(PulseConfig cfg, double fs, int pulseN, float[] tplI, float[] tplQ)
        {
            int riseN = (int)Math.Round(cfg.RiseFallSec * fs);
            if (riseN < 0) riseN = 0;
            if (riseN * 2 > pulseN) riseN = pulseN / 2; // ramps may meet but not overlap

            double T = pulseN / fs;               // pulse duration, seconds
            double b = cfg.ChirpBandwidthHz;      // total swept bandwidth for LFM
            double chirpRate = T > 0 ? b / T : 0; // Hz per second

            int[] barker = cfg.Modulation == IntraPulseModulation.BarkerPhase
                ? BarkerCode(cfg.BarkerLength)
                : null;

            for (int k = 0; k < pulseN; k++)
            {
                double env = Envelope(k, pulseN, riseN);
                double phase = 0.0;

                switch (cfg.Modulation)
                {
                    case IntraPulseModulation.LinearFmChirp:
                    {
                        // phi(t) = 2*pi*(-B/2 * t + 0.5*chirpRate*t^2), t = k/fs.
                        double t = k / fs;
                        phase = 2.0 * Math.PI * (-0.5 * b * t + 0.5 * chirpRate * t * t);
                        break;
                    }
                    case IntraPulseModulation.BarkerPhase:
                    {
                        int len = barker.Length;
                        int chip = (int)((long)k * len / pulseN); // 0..len-1
                        if (chip >= len) chip = len - 1;
                        phase = barker[chip] >= 0 ? 0.0 : Math.PI;
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
