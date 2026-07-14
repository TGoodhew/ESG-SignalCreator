namespace EsgSignalCreator.Visa
{
    /// <summary>The Basic-mode measurement families the verification loop uses.</summary>
    public enum VsaMeasurement
    {
        ChannelPower,
        Acp,
        Ccdf,
        Spectrum,
        Waveform
    }

    /// <summary>
    /// 0-based positions of the peak, mean and peak-to-mean scalars within the <c>:READ:WAVeform?</c>
    /// result set. The ordering differs by model: the E4406A returns [peak, mean, mean-avg, aux,
    /// peak-to-mean]; the N9010A returns [sample-time, mean, mean-avg, num-samples, peak-to-mean, max].
    /// </summary>
    public struct WaveformScalarLayout
    {
        public WaveformScalarLayout(int peakIndex, int meanIndex, int peakToMeanIndex)
        {
            PeakIndex = peakIndex;
            MeanIndex = meanIndex;
            PeakToMeanIndex = peakToMeanIndex;
        }

        public int PeakIndex { get; }
        public int MeanIndex { get; }
        public int PeakToMeanIndex { get; }
    }

    /// <summary>
    /// Layout of the ACP scalar result set. The per-offset block is identical on both models — four
    /// values per offset (lower-relative dB, lower-absolute dBm, upper-relative dB, upper-absolute dBm)
    /// starting at <see cref="OffsetBaseIndex"/> — but the offset <b>count</b> and the summary
    /// adjacent-channel positions differ (the E4406A carries adjacent dBc in the header; the N9010A's
    /// header is total/reference carrier power, so its adjacent values come from offset A).
    /// </summary>
    public struct AcpScalarLayout
    {
        public AcpScalarLayout(int offsetCount, int offsetBaseIndex, int upperAdjacentDbcIndex, int lowerAdjacentDbcIndex)
        {
            OffsetCount = offsetCount;
            OffsetBaseIndex = offsetBaseIndex;
            UpperAdjacentDbcIndex = upperAdjacentDbcIndex;
            LowerAdjacentDbcIndex = lowerAdjacentDbcIndex;
        }

        public int OffsetCount { get; }
        public int OffsetBaseIndex { get; }
        public int UpperAdjacentDbcIndex { get; }
        public int LowerAdjacentDbcIndex { get; }
    }

    /// <summary>
    /// The model-varying parts of the analyzer's SCPI, so measurement code can stay instrument-agnostic
    /// and read the right mnemonics from <see cref="VsaInstrument.Dialect"/> instead of hard-coding an
    /// E4406A dialect. This is the seam introduced for the N9010A port (issue #106); the concrete
    /// per-measurement strings are filled in by the SCPI sub-issues (#107 control plane, #110 Spectrum /
    /// Waveform, #111 Channel Power / ACP / CCDF).
    /// </summary>
    public interface IVsaDialect
    {
        /// <summary>Which model this dialect drives.</summary>
        VsaModel Model { get; }

        /// <summary>
        /// The <c>:INSTrument:SELect</c> mnemonic the given measurement runs in. On the E4406A every
        /// Basic-mode measurement is <c>BASIC</c>; on the N9010A, Spectrum/Waveform live in the IQ
        /// Analyzer (<c>BASIC</c>) mode while Channel Power/ACP/CCDF live in Spectrum Analyzer (<c>SA</c>).
        /// </summary>
        string InstrumentModeFor(VsaMeasurement measurement);

        /// <summary>
        /// The measurement root used by <c>:CONFigure/:READ/:FETCh/:MEASure</c> (e.g. <c>CHPower</c>).
        /// Mostly shared, but note the E4406A's <c>ACP</c> becomes the X-Series <c>ACPower</c>.
        /// </summary>
        string RootFor(VsaMeasurement measurement);

        /// <summary>
        /// True when the analyzer exposes a single global <c>:SENSe:FREQuency:SPAN</c> (X-Series);
        /// false when span is per-measurement (<c>:SENSe:CHPower:FREQuency:SPAN</c> etc., the E4406A).
        /// </summary>
        bool HasGlobalSpan { get; }

        /// <summary>Where peak/mean/peak-to-mean sit in the <c>:READ:WAVeform?</c> scalar set for this model.</summary>
        WaveformScalarLayout WaveformScalars { get; }

        /// <summary>
        /// The result index <c>n</c> for the CCDF data from <c>:READ:PSTatistic[n]?</c>. The E4406A
        /// returns the 10-value scalar set at n=1 (omitted). The N9010A at n=2 returns the 5001-point
        /// CCDF <b>trace</b> (probability % vs dB-above-average) — see <see cref="CcdfResultIsTrace"/>.
        /// </summary>
        int CcdfScalarResultIndex { get; }

        /// <summary>
        /// True when the CCDF read returns the 5001-point probability <b>trace</b> rather than the 10
        /// scalars, so PAPR must be derived from the trace (the highest dB-above-average level still
        /// reached) instead of a scalar index. The N9010A (X-Series) does this; the E4406A returns scalars.
        /// </summary>
        bool CcdfResultIsTrace { get; }

        /// <summary>Offset count and adjacent-channel positions of the <c>:READ:ACP?</c> scalar set.</summary>
        AcpScalarLayout AcpScalars { get; }

        /// <summary>
        /// True if measurement reads should wait for completion via SRQ (Status-Byte MAV) rather than a
        /// fixed read timeout, so an auto-alignment of arbitrary length can't trip a spurious timeout
        /// (#129). The N9010A opts in; the E4406A uses the plain blocking read.
        /// </summary>
        bool UsesServiceRequestCompletion { get; }

        /// <summary>
        /// The default SCPI recipe for capturing the display as an image (issue #143), or null if the
        /// model has no built-in default. The capturing tool may override any field. The defaults are
        /// manual-derived and should be confirmed on the bench.
        /// </summary>
        ScreenCaptureRecipe ScreenCapture { get; }
    }
}
