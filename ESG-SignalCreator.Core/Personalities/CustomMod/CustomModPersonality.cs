using System;
using EsgSignalCreator.Dsp;
using EsgSignalCreator.Model;

namespace EsgSignalCreator.Personalities.CustomMod
{
    /// <summary>
    /// A configurable linearly-modulated digital source. The pipeline is:
    /// <list type="number">
    ///   <item>generate a bit stream from the chosen <see cref="DataSource"/> (MSB-first);</item>
    ///   <item>map bits to complex symbols via <see cref="SymbolMapper"/> (unit average power);</item>
    ///   <item>upsample by <see cref="CustomModConfig.SamplesPerSymbol"/> (zero-insertion);</item>
    ///   <item>pulse-shape with a <see cref="Fir"/> filter (RRC/RC/Gaussian) or a rectangular hold;</item>
    ///   <item>normalize the peak vector magnitude to 1.0.</item>
    /// </list>
    /// The output sample rate is <c>SymbolRateHz * SamplesPerSymbol</c>.
    ///
    /// <para><b>Length / filter-tail convention.</b> The shaping filters use a 'same'-length
    /// convolution (<see cref="Fir.Apply"/>), so the output length is exactly
    /// <c>SymbolCount * SamplesPerSymbol</c> — the linear-phase filter delay is removed and the
    /// transient tail is not appended. Rectangular shaping likewise holds each symbol for
    /// <c>SamplesPerSymbol</c> samples, giving the same length.</para>
    ///
    /// <para><b>MSK.</b> MSK is generated as a constant-envelope continuous-phase signal rather
    /// than via linear pulse shaping: each input bit advances the carrier phase by ±π/2 over one
    /// symbol with a half-sine frequency pulse, bypassing the FIR stage.</para>
    /// </summary>
    public sealed class CustomModPersonality : IWaveformPersonality
    {
        private CustomModConfig _config = new CustomModConfig();

        /// <inheritdoc/>
        public string Id => "custom-mod";

        /// <inheritdoc/>
        public string DisplayName => "Custom Digital Modulation";

        /// <inheritdoc/>
        public int? RequiredOption => null;

        /// <inheritdoc/>
        public object GetConfig() => _config;

        /// <inheritdoc/>
        public void LoadConfig(object cfg)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            if (!(cfg is CustomModConfig cm))
                throw new ArgumentException("Expected a CustomModConfig.", nameof(cfg));
            _config = cm;
        }

        /// <inheritdoc/>
        public WaveformModel Calculate(IProgress<int> progress)
        {
            CustomModConfig cfg = _config ?? new CustomModConfig();

            if (cfg.SymbolRateHz <= 0)
                throw new InvalidOperationException("SymbolRateHz must be positive.");
            if (cfg.SamplesPerSymbol < 1)
                throw new InvalidOperationException("SamplesPerSymbol must be >= 1.");
            if (cfg.SymbolCount < 1)
                throw new InvalidOperationException("SymbolCount must be >= 1.");
            if (cfg.FilterSpanSymbols < 1)
                throw new InvalidOperationException("FilterSpanSymbols must be >= 1.");

            int sps = cfg.SamplesPerSymbol;
            double sampleRate = cfg.SymbolRateHz * sps;

            progress?.Report(0);

            // 1) Bit source.
            Func<int> nextBit = Prbs.CreateBitGenerator(cfg.Data);

            float[] i, q;
            if (cfg.Modulation == Modulation.MSK)
            {
                GenerateMsk(cfg, nextBit, sps, out i, out q, progress);
            }
            else
            {
                GenerateLinear(cfg, nextBit, sps, out i, out q, progress);
            }

            NormalizePeak(i, q);
            progress?.Report(100);

            return new WaveformModel(i, q, sampleRate, "CustomMod");
        }

        /// <summary>
        /// Generate a linearly-modulated waveform: map bits to symbols, upsample by zero
        /// insertion, then pulse-shape (FIR for RRC/RC/Gaussian; rectangular = sample-and-hold).
        /// </summary>
        private static void GenerateLinear(CustomModConfig cfg, Func<int> nextBit, int sps,
            out float[] i, out float[] q, IProgress<int> progress)
        {
            var mapper = new SymbolMapper(cfg.Modulation);
            int bps = mapper.BitsPerSymbol;
            int symCount = cfg.SymbolCount;
            int n = symCount * sps;

            // 2) Map bits -> symbols.
            var symI = new double[symCount];
            var symQ = new double[symCount];
            var bitBuf = new int[bps];
            for (int s = 0; s < symCount; s++)
            {
                for (int b = 0; b < bps; b++) bitBuf[b] = nextBit();
                mapper.Map(bitBuf, out double si, out double sq);
                symI[s] = si;
                symQ[s] = sq;
                ReportRange(progress, s, symCount, 0, 40);
            }

            if (cfg.Shape == PulseShape.Rectangular)
            {
                // Sample-and-hold: each symbol fills sps consecutive samples.
                i = new float[n];
                q = new float[n];
                for (int s = 0; s < symCount; s++)
                {
                    for (int k = 0; k < sps; k++)
                    {
                        int idx = s * sps + k;
                        i[idx] = (float)symI[s];
                        q[idx] = (float)symQ[s];
                    }
                    ReportRange(progress, s, symCount, 40, 95);
                }
                return;
            }

            // 3) Upsample by zero insertion: one impulse per symbol every sps samples.
            var upI = new double[n];
            var upQ = new double[n];
            for (int s = 0; s < symCount; s++)
            {
                upI[s * sps] = symI[s];
                upQ[s * sps] = symQ[s];
            }
            progress?.Report(50);

            // 4) Pulse-shape via the shared FIR designer (reuses EsgSignalCreator.Dsp.Fir).
            double[] taps = DesignTaps(cfg, sps);
            Fir.ApplyComplex(upI, upQ, taps, out double[] oi, out double[] oq);
            progress?.Report(90);

            i = new float[n];
            q = new float[n];
            for (int idx = 0; idx < n; idx++)
            {
                i[idx] = (float)oi[idx];
                q[idx] = (float)oq[idx];
            }
        }

        /// <summary>Design the FIR shaping taps for the configured filter (non-rectangular).</summary>
        private static double[] DesignTaps(CustomModConfig cfg, int sps)
        {
            switch (cfg.Shape)
            {
                case PulseShape.RootRaisedCosine:
                    return Fir.RootRaisedCosine(cfg.Alpha, sps, cfg.FilterSpanSymbols);
                case PulseShape.RaisedCosine:
                    return Fir.RaisedCosine(cfg.Alpha, sps, cfg.FilterSpanSymbols);
                case PulseShape.Gaussian:
                    return Fir.Gaussian(cfg.Alpha, sps, cfg.FilterSpanSymbols);
                default:
                    throw new InvalidOperationException("Rectangular shaping is handled separately.");
            }
        }

        /// <summary>
        /// Generate MSK as a constant-envelope continuous-phase signal. Each bit maps to a
        /// frequency-shift direction d = ±1 (0 -> +1, 1 -> -1); the phase accrues by d·π/2 over
        /// one symbol via a half-sine frequency pulse, so the envelope stays at magnitude 1.
        /// </summary>
        private static void GenerateMsk(CustomModConfig cfg, Func<int> nextBit, int sps,
            out float[] i, out float[] q, IProgress<int> progress)
        {
            int symCount = cfg.SymbolCount;
            int n = symCount * sps;
            i = new float[n];
            q = new float[n];

            // Half-sine frequency pulse g(frac) = sin(pi*frac), frac in [0,1]. Its discrete sum
            // over the sps sub-samples of one symbol is precomputed so we can scale each per-sample
            // phase step to make the per-symbol phase accrual exactly dir*pi/2.
            double pulseSum = 0.0;
            for (int k = 0; k < sps; k++)
            {
                double frac = (k + 0.5) / sps;
                pulseSum += Math.Sin(Math.PI * frac);
            }

            double phase = 0.0;
            int idx = 0;
            for (int s = 0; s < symCount; s++)
            {
                int bit = nextBit();
                double dir = (bit == 0) ? 1.0 : -1.0;
                for (int k = 0; k < sps; k++)
                {
                    double frac = (k + 0.5) / sps; // 0..1 across the symbol
                    // Per-sample phase increment; the sum over one symbol == dir*pi/2.
                    phase += dir * (Math.PI / 2.0) * Math.Sin(Math.PI * frac) / pulseSum;
                    i[idx] = (float)Math.Cos(phase);
                    q[idx] = (float)Math.Sin(phase);
                    idx++;
                }
                ReportRange(progress, s, symCount, 0, 95);
            }
        }

        /// <summary>Normalize so the peak vector magnitude sqrt(I²+Q²) is exactly 1.0.</summary>
        private static void NormalizePeak(float[] i, float[] q)
        {
            double peak = 0.0;
            for (int s = 0; s < i.Length; s++)
            {
                double m = Math.Sqrt((double)i[s] * i[s] + (double)q[s] * q[s]);
                if (m > peak) peak = m;
            }
            if (peak > 0.0)
            {
                double scale = 1.0 / peak;
                for (int s = 0; s < i.Length; s++)
                {
                    i[s] = (float)(i[s] * scale);
                    q[s] = (float)(q[s] * scale);
                }
            }
        }

        private static void ReportRange(IProgress<int> progress, int s, int total, int lo, int hi)
        {
            if (progress == null || total <= 0) return;
            int pct = lo + (int)((long)s * (hi - lo) / total);
            progress.Report(pct);
        }
    }
}
