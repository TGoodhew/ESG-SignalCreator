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
