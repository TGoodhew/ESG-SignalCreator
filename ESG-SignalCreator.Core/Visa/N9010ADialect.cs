using System;

namespace EsgSignalCreator.Visa
{
    /// <summary>
    /// SCPI dialect for the Keysight N9010A (EXA) X-Series analyzer.
    /// <para>
    /// Grounded in the supplied X-Series manuals: Spectrum and Waveform are IQ Analyzer (<c>BASIC</c>)
    /// mode measurements (9018-02190); Channel Power, ACP and CCDF are Spectrum Analyzer (<c>SA</c>) mode
    /// measurements (9018-06099, roots <c>CHPower</c> / <c>ACPower</c> / <c>PSTatistic</c>). The X-Series
    /// uses a single global <c>:SENSe:FREQuency:SPAN</c>.
    /// </para>
    /// <para>
    /// This supplies the model routing (mode + root + span style) established by issue #106. The detailed
    /// per-measurement <c>:SENSe:</c> configuration, marker commands, and <c>[n]</c> scalar result
    /// orderings are implemented by the measurement sub-issues (#107, #110, #111).
    /// </para>
    /// </summary>
    public sealed class N9010ADialect : IVsaDialect
    {
        public VsaModel Model => VsaModel.N9010A;

        // X-Series exposes a single global :SENSe:FREQuency:SPAN rather than per-measurement spans.
        public bool HasGlobalSpan => true;

        // N9010A :READ:WAVeform? -> [sample-time, mean, mean-avg, num-samples, peak-to-mean, max(peak)]
        // (IQ Analyzer Mode Reference 9018-02190): peak is the Maximum at index 5, not index 0.
        public WaveformScalarLayout WaveformScalars => new WaveformScalarLayout(peakIndex: 5, meanIndex: 1, peakToMeanIndex: 4);

        // N9010A CCDF scalars are at n=2 (:READ:PSTatistic2?); n=0/1 are I/Q trace data. Within-set
        // ordering matches the E4406A (avg power [0], peak power / PAPR [8]). (SA Reference 9018-06099.)
        public int CcdfScalarResultIndex => 2;

        // N9010A :READ:ACPower? (Total-power-reference) -> 32 values: header [0.0, total-carrier,
        // 0.0, ref-carrier], then 6 offsets A..F x (lowerRel, lowerAbs, upperRel, upperAbs) from index 4.
        // Adjacent dBc comes from offset A (upper rel = index 6, lower rel = index 4). (SA Reference
        // 9018-06099 — manual-derived, confirm against hardware.)
        public AcpScalarLayout AcpScalars =>
            new AcpScalarLayout(offsetCount: 6, offsetBaseIndex: 4, upperAdjacentDbcIndex: 6, lowerAdjacentDbcIndex: 4);

        // Wait for measurement completion via SRQ so an X-Series auto-alignment of any length can't trip
        // a fixed read timeout (#129).
        public bool UsesServiceRequestCompletion => true;

        public string InstrumentModeFor(VsaMeasurement measurement)
        {
            switch (measurement)
            {
                // IQ Analyzer (Basic) mode carries the format-independent Spectrum/Waveform measurements.
                case VsaMeasurement.Spectrum:
                case VsaMeasurement.Waveform:
                    return "BASIC";
                // Channel Power / ACP / CCDF are Spectrum Analyzer (SA) mode measurements on the X-Series.
                case VsaMeasurement.ChannelPower:
                case VsaMeasurement.Acp:
                case VsaMeasurement.Ccdf:
                    return "SA";
                default:
                    throw new ArgumentOutOfRangeException(nameof(measurement), measurement, null);
            }
        }

        public string RootFor(VsaMeasurement measurement)
        {
            switch (measurement)
            {
                case VsaMeasurement.ChannelPower: return "CHPower";
                case VsaMeasurement.Acp: return "ACPower"; // NB: E4406A "ACP" -> X-Series "ACPower"
                case VsaMeasurement.Ccdf: return "PSTatistic";
                case VsaMeasurement.Spectrum: return "SPECtrum";
                case VsaMeasurement.Waveform: return "WAVeform";
                default: throw new ArgumentOutOfRangeException(nameof(measurement), measurement, null);
            }
        }
    }
}
