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
            return new WaveformModel(i, q, fs, cfg.Stereo ? "FM Stereo" : "FM Mono");
        }
    }
}
