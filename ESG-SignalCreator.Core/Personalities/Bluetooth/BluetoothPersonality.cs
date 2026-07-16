using System;
using EsgSignalCreator.Dsp;
using EsgSignalCreator.Model;
using EsgSignalCreator.Personalities.CustomMod;

namespace EsgSignalCreator.Personalities.Bluetooth
{
    /// <summary>
    /// A Bluetooth GFSK personality (Signal Studio for Bluetooth, N7606B v1): generates a
    /// Gaussian-frequency-shift-keyed carrier — the Basic Rate / LE modulation — as a constant-envelope
    /// baseband I/Q signal. Data bits drive an NRZ frequency pulse, Gaussian-filtered (BT = 0.5) and
    /// integrated to continuous phase at the configured modulation index.
    /// </summary>
    /// <remarks>
    /// GFSK is continuous-phase FSK: each symbol advances the phase by ±π·h (h = modulation index).
    /// This shares the CPM approach with GMSK (which is GFSK at h = 0.5, BT = 0.3). EDR (π/4-DQPSK /
    /// 8DPSK), the LE coded PHY, packet framing, and frequency hopping are deferred (see the N7606B doc).
    /// </remarks>
    public sealed class BluetoothPersonality : IWaveformPersonality
    {
        private BluetoothConfig _config = new BluetoothConfig();

        /// <inheritdoc/>
        public string Id => "bluetooth";

        /// <inheritdoc/>
        public string DisplayName => "Bluetooth (GFSK)";

        /// <inheritdoc/>
        public int? RequiredOption => null;

        /// <inheritdoc/>
        public object GetConfig() => _config;

        /// <inheritdoc/>
        public void LoadConfig(object cfg)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            if (!(cfg is BluetoothConfig bc))
                throw new ArgumentException("Expected a BluetoothConfig.", nameof(cfg));
            _config = bc;
        }

        /// <inheritdoc/>
        public WaveformModel Calculate(IProgress<int> progress)
        {
            BluetoothConfig cfg = _config ?? new BluetoothConfig();

            if (cfg.SymbolRateHz <= 0)
                throw new InvalidOperationException("SymbolRateHz must be positive.");
            if (cfg.SamplesPerSymbol < 1)
                throw new InvalidOperationException("SamplesPerSymbol must be at least 1.");
            if (cfg.SymbolCount < 1)
                throw new InvalidOperationException("SymbolCount must be at least 1.");
            if (cfg.ModulationIndex <= 0)
                throw new InvalidOperationException("ModulationIndex must be positive.");
            if (cfg.Bt <= 0)
                throw new InvalidOperationException("Bt must be positive.");
            if (cfg.GaussianSpanSymbols < 1)
                throw new InvalidOperationException("GaussianSpanSymbols must be at least 1.");

            if (cfg.Modulation != BluetoothModulation.Gfsk)
                return GenerateEdr(cfg, progress);

            int sps = cfg.SamplesPerSymbol;
            int n = cfg.SymbolCount * sps;
            double sampleRate = cfg.SymbolRateHz * sps;

            progress?.Report(5);

            var nrz = new double[n];
            Func<int> bit = Prbs.CreateBitGenerator(cfg.Data);
            for (int k = 0; k < cfg.SymbolCount; k++)
            {
                double d = bit() == 1 ? 1.0 : -1.0;
                int baseIdx = k * sps;
                for (int j = 0; j < sps; j++) nrz[baseIdx + j] = d;
            }

            progress?.Report(30);

            double[] taps = Fir.Gaussian(cfg.Bt, sps, cfg.GaussianSpanSymbols);
            double[] f = Fir.Apply(nrz, taps);

            progress?.Report(70);

            var i = new float[n];
            var q = new float[n];
            double phase = 0.0;
            double kf = Math.PI * cfg.ModulationIndex / sps; // ±π·h per symbol
            for (int s = 0; s < n; s++)
            {
                phase += kf * f[s];
                i[s] = (float)Math.Cos(phase);
                q[s] = (float)Math.Sin(phase);
            }

            progress?.Report(100);

            return new WaveformModel(i, q, sampleRate, "Bluetooth");
        }

        /// <summary>
        /// EDR modulation: differential π/4-DQPSK (2 Mbps, 2 bits/symbol) or 8-DPSK (3 Mbps, 3 bits/symbol)
        /// at the 1 Msym/s symbol rate, zero-stuffed and RRC pulse-shaped, peak-normalized. Unlike GFSK
        /// this is not constant-envelope.
        /// </summary>
        private static WaveformModel GenerateEdr(BluetoothConfig cfg, IProgress<int> progress)
        {
            if (cfg.EdrRrcBeta < 0 || cfg.EdrRrcBeta > 1)
                throw new InvalidOperationException("EdrRrcBeta must be in [0,1].");

            bool eightDpsk = cfg.Modulation == BluetoothModulation.Edr3Mbps;
            int bitsPerSym = eightDpsk ? 3 : 2;
            int sps = cfg.SamplesPerSymbol;
            int n = cfg.SymbolCount * sps;
            double sampleRate = cfg.SymbolRateHz * sps;

            progress?.Report(10);

            Func<int> bit = Prbs.CreateBitGenerator(cfg.Data);
            var upI = new double[n];
            var upQ = new double[n];
            double phase = 0.0;
            const double q = Math.PI / 4.0;

            for (int k = 0; k < cfg.SymbolCount; k++)
            {
                int idx = 0;
                for (int b = 0; b < bitsPerSym; b++) idx = (idx << 1) | bit();
                // Differential phase step: 8-DPSK uses multiples of π/4; π/4-DQPSK uses the odd multiples.
                double delta = eightDpsk ? idx * q : (2 * idx + 1) * q;
                phase += delta;
                upI[k * sps] = Math.Cos(phase);
                upQ[k * sps] = Math.Sin(phase);
            }

            progress?.Report(50);

            double[] taps = Fir.RootRaisedCosine(cfg.EdrRrcBeta, sps, cfg.GaussianSpanSymbols);
            Fir.ApplyComplex(upI, upQ, taps, out double[] fi, out double[] fq);

            progress?.Report(85);

            double peak = 0.0;
            for (int s = 0; s < n; s++)
            {
                double m = Math.Sqrt(fi[s] * fi[s] + fq[s] * fq[s]);
                if (m > peak) peak = m;
            }
            var i2 = new float[n];
            var q2 = new float[n];
            double scale = peak > 0 ? 1.0 / peak : 1.0;
            for (int s = 0; s < n; s++) { i2[s] = (float)(fi[s] * scale); q2[s] = (float)(fq[s] * scale); }

            progress?.Report(100);
            return new WaveformModel(i2, q2, sampleRate, eightDpsk ? "Bluetooth EDR 8DPSK" : "Bluetooth EDR π/4-DQPSK");
        }
    }
}
