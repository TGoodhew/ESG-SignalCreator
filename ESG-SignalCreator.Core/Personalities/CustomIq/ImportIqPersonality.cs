using System;
using EsgSignalCreator.Io;
using EsgSignalCreator.Model;

namespace EsgSignalCreator.Personalities.CustomIq
{
    /// <summary>
    /// Signal source that loads a baseband I/Q waveform from an external file (CSV/TSV, raw
    /// interleaved int16, or 16-bit PCM WAV) via <see cref="IqFileReader"/>. Output samples are
    /// clamped to [-1, +1] after any configured scaling.
    /// </summary>
    public sealed class ImportIqPersonality : IWaveformPersonality
    {
        /// <summary>Current settings. Never null.</summary>
        public ImportIqConfig Config { get; private set; } = new ImportIqConfig();

        public string Id => "import-iq";

        public string DisplayName => "Import I/Q";

        /// <summary>Imported I/Q is generic ARB; no special E4438C option is required to play it.</summary>
        public int? RequiredOption => null;

        public object GetConfig() => Config;

        public void LoadConfig(object cfg)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            var c = cfg as ImportIqConfig;
            if (c == null)
                throw new ArgumentException("Expected an ImportIqConfig.", nameof(cfg));
            Config = c;
        }

        public WaveformModel Calculate(IProgress<int> progress)
        {
            if (string.IsNullOrWhiteSpace(Config.Path))
                throw new InvalidOperationException("No I/Q file path is configured.");

            progress?.Report(0);

            WaveformModel raw = IqFileReader.Read(
                Config.Path,
                Config.SampleRateHz,
                Config.SwapIq,
                Config.Scale,
                Config.Format);

            progress?.Report(50);

            // Clamp to [-1, +1] so downstream scaling/encoding never sees out-of-range samples.
            float[] i = raw.I;
            float[] q = raw.Q;
            for (int n = 0; n < i.Length; n++)
            {
                i[n] = Clamp(i[n]);
                q[n] = Clamp(q[n]);
            }

            progress?.Report(100);

            return new WaveformModel(i, q, raw.SampleRateHz, raw.Name);
        }

        private static float Clamp(float v)
        {
            if (v > 1f) return 1f;
            if (v < -1f) return -1f;
            return v;
        }
    }
}
