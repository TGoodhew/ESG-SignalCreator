using System;

namespace EsgSignalCreator.Personalities.CustomMod
{
    /// <summary>
    /// Maps groups of bits to complex constellation points for each <see cref="Modulation"/>.
    ///
    /// Constellations are normalized so the <i>average</i> symbol power over the full set of
    /// points is 1.0 (E{|s|²} = 1). PSK families already sit on the unit circle (|s| = 1);
    /// square-QAM grids are scaled by 1/√(mean power of the grid).
    ///
    /// Bit-to-point assignment is Gray-coded where that is the textbook convention: QPSK/8-PSK
    /// use Gray-coded phases, and square QAM uses independent Gray-coded I and Q PAM levels.
    /// MSK carries no constellation of its own — it is generated in the personality as
    /// continuous-phase / offset-QPSK with half-sine shaping — so this mapper treats it as BPSK
    /// for the purpose of symbol production (the MSK shaping happens later).
    /// </summary>
    public sealed class SymbolMapper
    {
        private readonly Modulation _mod;

        public SymbolMapper(Modulation mod)
        {
            _mod = mod;
        }

        /// <summary>Number of bits consumed to produce one symbol.</summary>
        public int BitsPerSymbol
        {
            get
            {
                switch (_mod)
                {
                    case Modulation.BPSK: return 1;
                    case Modulation.MSK: return 1;   // 1 bit/symbol; shaping applied downstream
                    case Modulation.QPSK: return 2;
                    case Modulation.PSK8: return 3;
                    case Modulation.QAM16: return 4;
                    case Modulation.QAM64: return 6;
                    case Modulation.QAM256: return 8;
                    default: throw new ArgumentOutOfRangeException();
                }
            }
        }

        /// <summary>
        /// Map a symbol's worth of bits (MSB-first in <paramref name="bits"/>, length
        /// == <see cref="BitsPerSymbol"/>) to a complex constellation point (I, Q).
        /// </summary>
        public void Map(int[] bits, out double i, out double q)
        {
            if (bits == null) throw new ArgumentNullException(nameof(bits));
            if (bits.Length != BitsPerSymbol)
                throw new ArgumentException($"Expected {BitsPerSymbol} bits, got {bits.Length}.");

            switch (_mod)
            {
                case Modulation.BPSK:
                case Modulation.MSK:
                    // 0 -> +1, 1 -> -1 on the I axis; Q = 0.
                    i = bits[0] == 0 ? 1.0 : -1.0;
                    q = 0.0;
                    return;

                case Modulation.QPSK:
                    MapPsk(GrayDecode(Bits(bits)), 4, out i, out q);
                    return;

                case Modulation.PSK8:
                    MapPsk(GrayDecode(Bits(bits)), 8, out i, out q);
                    return;

                case Modulation.QAM16:
                    MapQam(bits, 4, out i, out q);
                    return;
                case Modulation.QAM64:
                    MapQam(bits, 8, out i, out q);
                    return;
                case Modulation.QAM256:
                    MapQam(bits, 16, out i, out q);
                    return;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        /// Map a Gray-decoded symbol index onto an <paramref name="m"/>-ary PSK ring on the unit
        /// circle. Index 0 is at angle 0; QPSK is rotated by π/4 to yield the canonical
        /// (±1±j)/√2 points.
        /// </summary>
        private static void MapPsk(int symbol, int m, out double i, out double q)
        {
            double offset = (m == 4) ? Math.PI / 4.0 : 0.0;
            double angle = 2.0 * Math.PI * symbol / m + offset;
            i = Math.Cos(angle);
            q = Math.Sin(angle);
        }

        /// <summary>
        /// Map bits to a square <paramref name="side"/>×<paramref name="side"/> QAM grid. The
        /// high half of the bits selects the I (Gray) PAM level, the low half selects Q. Levels
        /// run over the symmetric odd set {-(side-1), …, -1, +1, …, +(side-1)} and the whole
        /// grid is scaled so its average power is 1.
        /// </summary>
        private void MapQam(int[] bits, int side, out double i, out double q)
        {
            int bitsPerAxis = BitsPerSymbol / 2;
            int iGray = 0, qGray = 0;
            for (int b = 0; b < bitsPerAxis; b++)
            {
                iGray = (iGray << 1) | bits[b];
                qGray = (qGray << 1) | bits[bitsPerAxis + b];
            }
            int iIdx = GrayDecode(iGray);
            int qIdx = GrayDecode(qGray);

            // Level index 0..side-1 -> odd-integer amplitude (2*idx - (side-1)).
            double iLevel = 2 * iIdx - (side - 1);
            double qLevel = 2 * qIdx - (side - 1);

            double scale = QamScale(side);
            i = iLevel * scale;
            q = qLevel * scale;
        }

        /// <summary>
        /// Normalization factor for a square <paramref name="side"/>×<paramref name="side"/> QAM
        /// grid so the average symbol power is 1. The mean square of the per-axis odd levels is
        /// (M-1)/3 with M = side²; total mean power is 2·(side²-1)/3, so the scale is its
        /// reciprocal square root.
        /// </summary>
        private static double QamScale(int side)
        {
            int m = side * side;
            double meanPower = 2.0 * (m - 1) / 3.0;
            return 1.0 / Math.Sqrt(meanPower);
        }

        private static int Bits(int[] bits)
        {
            int v = 0;
            for (int b = 0; b < bits.Length; b++)
                v = (v << 1) | bits[b];
            return v;
        }

        /// <summary>Convert a Gray-coded value to its natural binary index.</summary>
        private static int GrayDecode(int gray)
        {
            int result = gray;
            for (int t = gray >> 1; t != 0; t >>= 1)
                result ^= t;
            return result;
        }
    }
}
