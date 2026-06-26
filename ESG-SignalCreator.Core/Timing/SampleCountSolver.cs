using System;

namespace EsgSignalCreator.Timing
{
    /// <summary>How a waveform's length was specified by the user.</summary>
    public enum LengthBasis
    {
        /// <summary>Duration in seconds.</summary>
        Time,
        /// <summary>Explicit number of I/Q samples.</summary>
        Samples,
        /// <summary>Number of modulation symbols (requires a symbol rate).</summary>
        Symbols
    }

    /// <summary>
    /// "State intent, not arithmetic": turn a length expressed in time, samples, or symbols into a
    /// concrete sample count (and back), so the UI can let the user pick whichever basis is natural
    /// and solve the sampling math for them (RFXpress Auto-mode pattern, UX brief §2).
    /// </summary>
    public static class SampleCountSolver
    {
        /// <summary>
        /// Solve the sample count for a length given in <paramref name="basis"/> units, clamped to
        /// <paramref name="minSamples"/>. For <see cref="LengthBasis.Symbols"/>, samples-per-symbol
        /// is derived from <paramref name="sampleRateHz"/> / <paramref name="symbolRateHz"/>.
        /// </summary>
        public static int Solve(LengthBasis basis, double value, double sampleRateHz,
            double symbolRateHz = 0, int minSamples = 60)
        {
            if (sampleRateHz <= 0) throw new ArgumentException("Sample rate must be positive.", nameof(sampleRateHz));

            double n;
            switch (basis)
            {
                case LengthBasis.Time:
                    n = value * sampleRateHz; // seconds × Hz
                    break;
                case LengthBasis.Symbols:
                    if (symbolRateHz <= 0)
                        throw new ArgumentException("A positive symbol rate is required for symbol-based length.",
                            nameof(symbolRateHz));
                    n = value * (sampleRateHz / symbolRateHz); // symbols × samples-per-symbol
                    break;
                case LengthBasis.Samples:
                default:
                    n = value;
                    break;
            }

            int count = (int)Math.Round(n, MidpointRounding.AwayFromZero);
            if (count < minSamples) count = minSamples;
            return count;
        }

        /// <summary>Duration in seconds for a given sample count at a sample rate.</summary>
        public static double ToSeconds(int samples, double sampleRateHz)
            => sampleRateHz > 0 ? samples / sampleRateHz : 0;

        /// <summary>Number of whole symbols a sample count represents at the given rates.</summary>
        public static double ToSymbols(int samples, double sampleRateHz, double symbolRateHz)
            => (sampleRateHz > 0 && symbolRateHz > 0) ? samples * symbolRateHz / sampleRateHz : 0;
    }
}
