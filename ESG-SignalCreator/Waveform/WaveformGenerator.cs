using System;

namespace EsgSignalCreator.Waveform
{
    public enum SignalType
    {
        SingleTone,
        Am,
        Fm,
        Pm
    }

    /// <summary>Parameters describing a baseband signal to synthesize.</summary>
    public sealed class WaveformSpec
    {
        public SignalType Type { get; set; }
        public double SampleRateHz { get; set; } = 10e6;
        public int TargetLength { get; set; } = 4000;

        // Single tone
        public double OffsetHz { get; set; } = 100e3;

        // AM / FM / PM modulating tone
        public double RateHz { get; set; } = 1e3;
        public double AmDepthPercent { get; set; } = 50;
        public double FmDeviationHz { get; set; } = 10e3;
        public double PmDeviationDeg { get; set; } = 90;
    }

    /// <summary>Result of synthesis: the waveform plus the parameters actually achieved.</summary>
    public sealed class WaveformResult
    {
        public IqWaveform Waveform { get; set; }
        public string Summary { get; set; }
    }

    /// <summary>
    /// Synthesizes baseband I/Q waveforms for the E4438C ARB. All waveforms are built to loop
    /// seamlessly: the sample count is adjusted so the dominant tone completes a whole number of
    /// cycles, eliminating the discontinuity (and spectral splatter) at the wrap point.
    /// </summary>
    public static class WaveformGenerator
    {
        public const int MinLength = 60; // E4438C minimum ARB segment length

        public static WaveformResult Generate(WaveformSpec spec)
        {
            if (spec.SampleRateHz <= 0) throw new ArgumentException("Sample rate must be positive.");

            switch (spec.Type)
            {
                case SignalType.SingleTone: return Tone(spec);
                case SignalType.Am: return Am(spec);
                case SignalType.Fm: return Fm(spec);
                case SignalType.Pm: return Pm(spec);
                default: throw new ArgumentOutOfRangeException();
            }
        }

        private static WaveformResult Tone(WaveformSpec spec)
        {
            double fs = spec.SampleRateHz;
            int n;
            double fActual;

            if (Math.Abs(spec.OffsetHz) < 1e-9)
            {
                n = ClampLength(spec.TargetLength);
                fActual = 0;
            }
            else
            {
                n = SeamlessLength(spec.TargetLength, Math.Abs(spec.OffsetHz), fs, out int cycles);
                fActual = Math.Sign(spec.OffsetHz) * cycles * fs / n;
            }

            var i = new double[n];
            var q = new double[n];
            double w = 2 * Math.PI * fActual / fs;
            for (int k = 0; k < n; k++)
            {
                i[k] = Math.Cos(w * k);
                q[k] = Math.Sin(w * k);
            }

            return Result(i, q, fs, string.Format(
                "Single tone: offset {0}, {1} samples, constant envelope.",
                Hz(fActual), n));
        }

        private static WaveformResult Am(WaveformSpec spec)
        {
            double fs = spec.SampleRateHz;
            int n = SeamlessLength(spec.TargetLength, spec.RateHz, fs, out int cycles);
            double fmActual = cycles * fs / n;
            double m = spec.AmDepthPercent / 100.0;

            var i = new double[n];
            var q = new double[n];
            double w = 2 * Math.PI * fmActual / fs;
            for (int k = 0; k < n; k++)
            {
                i[k] = 1.0 + m * Math.Sin(w * k);
                q[k] = 0.0;
            }

            return Result(i, q, fs, string.Format(
                "AM: rate {0}, depth {1:0.#}%, {2} samples.",
                Hz(fmActual), spec.AmDepthPercent, n));
        }

        private static WaveformResult Fm(WaveformSpec spec)
        {
            double fs = spec.SampleRateHz;
            int n = SeamlessLength(spec.TargetLength, spec.RateHz, fs, out int cycles);
            double fmActual = cycles * fs / n;
            double beta = fmActual > 0 ? spec.FmDeviationHz / fmActual : 0; // modulation index

            var i = new double[n];
            var q = new double[n];
            double w = 2 * Math.PI * fmActual / fs;
            for (int k = 0; k < n; k++)
            {
                double phase = beta * Math.Sin(w * k);
                i[k] = Math.Cos(phase);
                q[k] = Math.Sin(phase);
            }

            return Result(i, q, fs, string.Format(
                "FM: rate {0}, deviation {1}, index {2:0.##}, {3} samples.",
                Hz(fmActual), Hz(spec.FmDeviationHz), beta, n));
        }

        private static WaveformResult Pm(WaveformSpec spec)
        {
            double fs = spec.SampleRateHz;
            int n = SeamlessLength(spec.TargetLength, spec.RateHz, fs, out int cycles);
            double fmActual = cycles * fs / n;
            double beta = spec.PmDeviationDeg * Math.PI / 180.0; // peak phase deviation, radians

            var i = new double[n];
            var q = new double[n];
            double w = 2 * Math.PI * fmActual / fs;
            for (int k = 0; k < n; k++)
            {
                double phase = beta * Math.Sin(w * k);
                i[k] = Math.Cos(phase);
                q[k] = Math.Sin(phase);
            }

            return Result(i, q, fs, string.Format(
                "PM: rate {0}, deviation {1:0.#}° ({2:0.##} rad), {3} samples.",
                Hz(fmActual), spec.PmDeviationDeg, beta, n));
        }

        private static WaveformResult Result(double[] i, double[] q, double fs, string summary)
        {
            return new WaveformResult { Waveform = new IqWaveform(i, q, fs), Summary = summary };
        }

        /// <summary>
        /// Choose a sample count near <paramref name="target"/> that holds a whole number of cycles
        /// of <paramref name="freq"/> at sample rate <paramref name="fs"/>, for seamless looping.
        /// </summary>
        private static int SeamlessLength(int target, double freq, double fs, out int cycles)
        {
            target = ClampLength(target);
            if (freq <= 0) { cycles = 0; return target; }

            cycles = (int)Math.Round(freq * target / fs);
            if (cycles < 1) cycles = 1;

            int n = (int)Math.Round(cycles * fs / freq);
            n = ClampLength(n);
            return n;
        }

        private static int ClampLength(int n)
        {
            if (n < MinLength) return MinLength;
            return n;
        }

        private static string Hz(double hz)
        {
            double a = Math.Abs(hz);
            if (a >= 1e6) return (hz / 1e6).ToString("0.######") + " MHz";
            if (a >= 1e3) return (hz / 1e3).ToString("0.######") + " kHz";
            return hz.ToString("0.######") + " Hz";
        }
    }
}
