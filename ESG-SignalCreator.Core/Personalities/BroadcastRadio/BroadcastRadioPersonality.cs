using System;
using EsgSignalCreator.Model;

namespace EsgSignalCreator.Personalities.BroadcastRadio
{
    /// <summary>
    /// A broadcast-radio personality (Signal Studio for Broadcast Radio, N7611B v1 core): generates an
    /// analog FM broadcast signal. The baseband multiplex is an audio test tone (mono), optionally with
    /// a 19 kHz stereo pilot and a 38 kHz DSB-SC stereo subcarrier, frequency-modulated onto the carrier
    /// (constant envelope).
    /// </summary>
    /// <remarks>
    /// Representative v1 core: a single audio test tone rather than real program audio, and no RDS (57 kHz)
    /// data, pre-emphasis, or SCA subcarriers. The digital broadcast formats the product also covers
    /// (DAB/DAB+ — see the T-DMB personality — and XM/HD Radio) are deferred.
    /// </remarks>
    public sealed class BroadcastRadioPersonality : IWaveformPersonality
    {
        private const double PilotHz = 19e3;
        private const double StereoSubHz = 38e3;
        private const double RdsSubHz = 57e3;      // 3 × the 19 kHz pilot
        private const double RdsBitRate = 1187.5;  // RDS data rate (57000 / 48), bps

        private BroadcastRadioConfig _config = new BroadcastRadioConfig();

        /// <inheritdoc/>
        public string Id => "broadcast-radio";

        /// <inheritdoc/>
        public string DisplayName => "Broadcast Radio (FM)";

        /// <inheritdoc/>
        public int? RequiredOption => null;

        /// <inheritdoc/>
        public object GetConfig() => _config;

        /// <inheritdoc/>
        public void LoadConfig(object cfg)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            if (!(cfg is BroadcastRadioConfig bc))
                throw new ArgumentException("Expected a BroadcastRadioConfig.", nameof(cfg));
            _config = bc;
        }

        /// <inheritdoc/>
        public WaveformModel Calculate(IProgress<int> progress)
        {
            BroadcastRadioConfig cfg = _config ?? new BroadcastRadioConfig();

            if (cfg.SampleRateHz <= 0)
                throw new InvalidOperationException("SampleRateHz must be positive.");
            if (cfg.Length <= 0)
                throw new InvalidOperationException("Length must be positive.");
            if (cfg.AudioToneHz <= 0)
                throw new InvalidOperationException("AudioToneHz must be positive.");
            if (cfg.PeakDeviationHz <= 0)
                throw new InvalidOperationException("PeakDeviationHz must be positive.");
            if (cfg.Stereo && cfg.SampleRateHz < 2.5 * (StereoSubHz + cfg.AudioToneHz))
                throw new InvalidOperationException("SampleRateHz is too low for the stereo multiplex (needs to exceed ~2.5×(38 kHz + audio)).");

            int n = cfg.Length;
            double fs = cfg.SampleRateHz;
            var i = new float[n];
            var q = new float[n];

            // RDS: a differentially-encoded PRBS at 1187.5 bps, one bit per RDS bit period.
            int[] rdsBits = null;
            double rdsWeight = 0.0;
            if (cfg.Rds)
            {
                if (cfg.RdsDeviationHz < 0)
                    throw new InvalidOperationException("RdsDeviationHz must be >= 0.");
                int numBits = (int)Math.Ceiling(n / fs * RdsBitRate) + 1;
                rdsBits = new int[numBits];
                var rng = new Random(cfg.RdsSeed);
                int prev = 1;
                for (int b = 0; b < numBits; b++)
                {
                    int data = rng.Next(0, 2);          // raw payload bit
                    prev = data ^ (prev == 1 ? 0 : 1);  // differential encoding (representative)
                    // store as ±1
                    rdsBits[b] = prev == 1 ? 1 : -1;
                }
                rdsWeight = cfg.PeakDeviationHz > 0 ? cfg.RdsDeviationHz / cfg.PeakDeviationHz : 0.0;
            }

            double phase = 0.0;
            double twoPi = 2.0 * Math.PI;
            int reportEvery = Math.Max(1, n / 100);
            int lastPct = -1;
            for (int s = 0; s < n; s++)
            {
                double t = s / fs;
                double audio = Math.Sin(twoPi * cfg.AudioToneHz * t);

                double mpx;
                if (cfg.Stereo)
                {
                    double pilot = Math.Sin(twoPi * PilotHz * t);
                    double stereoSub = audio * Math.Sin(twoPi * StereoSubHz * t); // L-R DSB-SC (L-only tone)
                    mpx = 0.45 * audio + 0.45 * stereoSub + 0.10 * pilot;
                }
                else
                {
                    mpx = audio;
                }

                if (rdsBits != null)
                {
                    // Biphase (Manchester) symbol: +d for the first half of the bit, -d for the second,
                    // DSB-SC modulated onto the 57 kHz subcarrier.
                    double bitPos = t * RdsBitRate;
                    int bitIdx = (int)bitPos;
                    if (bitIdx >= rdsBits.Length) bitIdx = rdsBits.Length - 1;
                    double frac = bitPos - Math.Floor(bitPos);
                    double biphase = (frac < 0.5 ? 1.0 : -1.0) * rdsBits[bitIdx];
                    mpx += rdsWeight * biphase * Math.Sin(twoPi * RdsSubHz * t);
                }

                phase += twoPi * cfg.PeakDeviationHz * mpx / fs;
                i[s] = (float)Math.Cos(phase);
                q[s] = (float)Math.Sin(phase);

                if (progress != null && (s % reportEvery == 0))
                {
                    int pct = (int)((long)s * 100 / n);
                    if (pct != lastPct) { progress.Report(pct); lastPct = pct; }
                }
            }

            progress?.Report(100);
            string name = (cfg.Stereo ? "FM Stereo" : "FM Mono") + (cfg.Rds ? " + RDS" : "");
            return new WaveformModel(i, q, fs, name);
        }
    }
}
