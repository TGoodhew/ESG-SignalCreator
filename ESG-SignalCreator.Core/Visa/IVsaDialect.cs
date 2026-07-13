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
    }
}
