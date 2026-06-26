using System;

namespace EsgSignalCreator.Personalities.CustomMod
{
    /// <summary>
    /// Bit generators for the configured <see cref="DataSource"/>.
    ///
    /// PN9/PN15/PN23 are maximal-length linear-feedback shift registers using the standard
    /// ITU-T O.150 / 3GPP polynomials. The register is seeded all-ones; each step XORs the two
    /// tapped stages to form the feedback bit, shifts the register, and emits that feedback bit.
    /// Bits are produced MSB-first by the consumer simply by reading them in generation order.
    /// </summary>
    public static class Prbs
    {
        /// <summary>
        /// Create a bit generator (a function returning the next 0/1 bit on each call)
        /// for the requested <paramref name="source"/>.
        /// </summary>
        public static Func<int> CreateBitGenerator(DataSource source)
        {
            switch (source)
            {
                case DataSource.AllZeros:
                    return () => 0;
                case DataSource.AllOnes:
                    return () => 1;
                case DataSource.PN9:
                    return Lfsr(9, FeedbackTaps9);
                case DataSource.PN15:
                    return Lfsr(15, FeedbackTaps15);
                case DataSource.PN23:
                    return Lfsr(23, FeedbackTaps23);
                default:
                    throw new ArgumentOutOfRangeException(nameof(source));
            }
        }

        // Maximal-length feedback taps (stage indices, 1-based from the input end), per the
        // standard primitive polynomials:
        //   PN9:  x^9  + x^5 + 1
        //   PN15: x^15 + x^14 + 1
        //   PN23: x^23 + x^18 + 1
        private static readonly int[] FeedbackTaps9 = { 9, 5 };
        private static readonly int[] FeedbackTaps15 = { 15, 14 };
        private static readonly int[] FeedbackTaps23 = { 23, 18 };

        /// <summary>
        /// Build a Fibonacci LFSR of <paramref name="length"/> stages whose feedback is the XOR
        /// of the stages named in <paramref name="taps"/> (1-based stage numbers). The register
        /// is seeded all-ones. The emitted bit is the feedback bit shifted in each step.
        /// </summary>
        private static Func<int> Lfsr(int length, int[] taps)
        {
            // State stored in the low <length> bits of a uint/ulong; stage i (1-based) is bit (i-1).
            ulong state = (length >= 64) ? ulong.MaxValue : ((1UL << length) - 1UL); // all ones
            ulong mask = (length >= 64) ? ulong.MaxValue : ((1UL << length) - 1UL);

            return () =>
            {
                ulong fb = 0UL;
                for (int t = 0; t < taps.Length; t++)
                    fb ^= (state >> (taps[t] - 1)) & 1UL;

                // Shift toward higher stages, inject feedback at stage 1 (bit 0).
                state = ((state << 1) | fb) & mask;
                return (int)fb;
            };
        }

        /// <summary>
        /// The repeat period (sequence length) of a maximal-length source, or 0 for the
        /// constant sources. Useful for tests and documentation.
        /// </summary>
        public static int Period(DataSource source)
        {
            switch (source)
            {
                case DataSource.PN9: return 511;       // 2^9  - 1
                case DataSource.PN15: return 32767;    // 2^15 - 1
                case DataSource.PN23: return 8388607;  // 2^23 - 1
                default: return 0;
            }
        }
    }
}
