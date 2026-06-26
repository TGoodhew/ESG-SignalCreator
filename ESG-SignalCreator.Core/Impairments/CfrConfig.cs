using System.Runtime.Serialization;

namespace EsgSignalCreator.Impairments
{
    /// <summary>
    /// Settings for crest-factor reduction (CFR): iterative clip-and-filter peak reduction
    /// applied to an already-built waveform to lower its peak-to-average power ratio (PAPR).
    /// </summary>
    /// <remarks>
    /// CFR trades a small amount of in-band distortion (EVM) and out-of-band regrowth for a
    /// lower crest factor, which lets a power amplifier run with more average power for the same
    /// peak headroom. The classic algorithm clips the complex envelope to a threshold derived
    /// from the signal RMS and the desired PAPR, then optionally low-pass filters the clipped
    /// signal to suppress the spectral regrowth the hard clip introduces. Repeating the
    /// clip-then-filter pass several times converges on the target without a single brutal clip.
    /// </remarks>
    [DataContract]
    public sealed class CfrConfig
    {
        /// <summary>
        /// Target peak-to-average power ratio, in dB. The per-iteration clip threshold is
        /// RMS · 10^(TargetPaprDb / 20). Lower values clip harder. Default 8 dB.
        /// </summary>
        [DataMember]
        public double TargetPaprDb { get; set; } = 8.0;

        /// <summary>
        /// Number of clip-and-filter passes. More iterations converge closer to the target
        /// (filtering after each clip re-grows some peaks). Default 4.
        /// </summary>
        [DataMember]
        public int Iterations { get; set; } = 4;

        /// <summary>
        /// When true (default), each clip is followed by a Hann-windowed-sinc low-pass FIR to
        /// limit spectral regrowth. When false, CFR is pure hard clipping (more out-of-band
        /// energy, but a harder peak limit).
        /// </summary>
        [DataMember]
        public bool FilterAfterClip { get; set; } = true;
    }
}
