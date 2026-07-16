using System;
using EsgSignalCreator.Dsp;
using EsgSignalCreator.Model;
using EsgSignalCreator.Personalities.CustomMod;

namespace EsgSignalCreator.Personalities.GsmEdge
{
    /// <summary>
    /// A GSM-family GMSK personality (Signal Studio for GSM/EDGE, N7602B v1): generates a
    /// Gaussian-minimum-shift-keyed carrier — the modulation used by GSM/GPRS — as a constant-envelope
    /// baseband I/Q signal. Data bits drive an NRZ frequency pulse that is Gaussian-filtered (BT = 0.3)
    /// and integrated to continuous phase (modulation index 0.5).
    /// </summary>
    /// <remarks>
    /// GMSK is generated as continuous-phase modulation: phase[n] = (π/2) · Σ f[n]/sps, where f is the
    /// unit-DC-gain Gaussian-filtered ±1 NRZ stream, so each symbol advances the phase by ±π/2. EDGE
    /// 8PSK (3π/8-rotated, linearised pulse) and full burst/TSC framing are deferred (see the N7602B doc).
    /// </remarks>
    public sealed class GsmEdgePersonality : IWaveformPersonality
    {
        private GsmEdgeConfig _config = new GsmEdgeConfig();

        /// <inheritdoc/>
        public string Id => "gsm-edge";

        /// <inheritdoc/>
        public string DisplayName => "GSM/EDGE (GMSK)";

        /// <inheritdoc/>
        public int? RequiredOption => null;

        /// <inheritdoc/>
        public object GetConfig() => _config;

        /// <inheritdoc/>
        public void LoadConfig(object cfg)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            if (!(cfg is GsmEdgeConfig gc))
                throw new ArgumentException("Expected a GsmEdgeConfig.", nameof(cfg));
            _config = gc;
        }

        /// <inheritdoc/>
        public WaveformModel Calculate(IProgress<int> progress)
        {
            GsmEdgeConfig cfg = _config ?? new GsmEdgeConfig();

            if (cfg.SymbolRateHz <= 0)
                throw new InvalidOperationException("SymbolRateHz must be positive.");
            if (cfg.SamplesPerSymbol < 1)
                throw new InvalidOperationException("SamplesPerSymbol must be at least 1.");
            if (cfg.SymbolCount < 1)
                throw new InvalidOperationException("SymbolCount must be at least 1.");
            if (cfg.Bt <= 0)
                throw new InvalidOperationException("Bt must be positive.");
            if (cfg.GaussianSpanSymbols < 1)
                throw new InvalidOperationException("GaussianSpanSymbols must be at least 1.");

            if (cfg.Modulation == GsmModulation.Edge8Psk)
                return GenerateEdge(cfg, progress);

            int sps = cfg.SamplesPerSymbol;
            int n = cfg.SymbolCount * sps;
            double sampleRate = cfg.SymbolRateHz * sps;

            progress?.Report(5);

            // NRZ frequency pulse: hold ±1 for each bit across the symbol period.
            var nrz = new double[n];
            Func<int> bit = Prbs.CreateBitGenerator(cfg.Data);
            for (int k = 0; k < cfg.SymbolCount; k++)
            {
                double d = bit() == 1 ? 1.0 : -1.0;
                int baseIdx = k * sps;
                for (int j = 0; j < sps; j++) nrz[baseIdx + j] = d;
            }

            progress?.Report(30);

            // Gaussian frequency shaping (unit DC gain), then integrate to phase with index 0.5.
            double[] taps = Fir.Gaussian(cfg.Bt, sps, cfg.GaussianSpanSymbols);
            double[] f = Fir.Apply(nrz, taps);

            progress?.Report(70);

            var i = new float[n];
            var q = new float[n];
            double phase = 0.0;
            double kf = Math.PI / (2.0 * sps); // per-sample phase gain => ±π/2 per symbol
            for (int s = 0; s < n; s++)
            {
                phase += kf * f[s];
                i[s] = (float)Math.Cos(phase);
                q[s] = (float)Math.Sin(phase);
            }

            progress?.Report(100);

            return new WaveformModel(i, q, sampleRate, "GSM/EDGE");
        }

        /// <summary>
        /// EDGE modulation: 3π/8-continuously-rotated 8-PSK (3 bits/symbol), zero-stuffed to the sample
        /// rate and RRC pulse-shaped (a representative stand-in for the linearised GMSK pulse). Unlike
        /// GMSK this is not constant-envelope. Peak-normalized to 1.0.
        /// </summary>
        private static WaveformModel GenerateEdge(GsmEdgeConfig cfg, IProgress<int> progress)
        {
            if (cfg.EdgeRrcBeta < 0 || cfg.EdgeRrcBeta > 1)
                throw new InvalidOperationException("EdgeRrcBeta must be in [0,1].");

            int sps = cfg.SamplesPerSymbol;
            int n = cfg.SymbolCount * sps;
            double sampleRate = cfg.SymbolRateHz * sps;

            progress?.Report(10);

            Func<int> bit = Prbs.CreateBitGenerator(cfg.Data);
            var upI = new double[n];
            var upQ = new double[n];
            const double rot = 3.0 * Math.PI / 8.0; // per-symbol continuous rotation

            for (int k = 0; k < cfg.SymbolCount; k++)
            {
                int idx = (bit() << 2) | (bit() << 1) | bit(); // 3 bits -> 8-PSK point (0..7)
                double phase = 2.0 * Math.PI * idx / 8.0 + k * rot;
                upI[k * sps] = Math.Cos(phase); // zero-stuff one symbol per sps samples
                upQ[k * sps] = Math.Sin(phase);
            }

            progress?.Report(50);

            double[] taps = Fir.RootRaisedCosine(cfg.EdgeRrcBeta, sps, cfg.GaussianSpanSymbols);
            Fir.ApplyComplex(upI, upQ, taps, out double[] fi, out double[] fq);

            progress?.Report(85);

            double peak = 0.0;
            for (int s = 0; s < n; s++)
            {
                double m = Math.Sqrt(fi[s] * fi[s] + fq[s] * fq[s]);
                if (m > peak) peak = m;
            }
            var i = new float[n];
            var q = new float[n];
            double scale = peak > 0 ? 1.0 / peak : 1.0;
            for (int s = 0; s < n; s++) { i[s] = (float)(fi[s] * scale); q[s] = (float)(fq[s] * scale); }

            progress?.Report(100);
            return new WaveformModel(i, q, sampleRate, "GSM/EDGE (8PSK)");
        }
    }
}
