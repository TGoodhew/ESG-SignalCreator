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

        // N9010A :READ:WAVeform? scalar set, bench-confirmed on A.07.05 (7 values):
        // [0] sample-time, [1] mean (dBm), [2] mean-over-avg, [3] num-samples, [4] peak-to-mean (dB),
        // [5] peak/max (dBm), [6] min (dBm). Peak is the Maximum at index 5, not index 0.
        public WaveformScalarLayout WaveformScalars => new WaveformScalarLayout(peakIndex: 5, meanIndex: 1, peakToMeanIndex: 4);

        // N9010A CCDF: :READ:PSTatistic1? returns the 10-value scalar set in the SAME order as the
        // E4406A, with PAPR (peak-above-average) at index [8] — bench-confirmed on A.07.05
        // (e.g. [0]=avg dBm, [8]=crest dB, [9]=count). An earlier/newer firmware was seen to return a
        // long probability trace at index 2 instead; the 64-point ":READ:PSTatistic2?" here is a
        // display-resolution curve in a different representation and is NOT reliable for PAPR. Reading
        // the scalar summary at index 1 is the stable, firmware-independent choice.
        public int CcdfScalarResultIndex => 1;
        public bool CcdfResultIsTrace => false;

        // N9010A ACP result layout per the SA Mode Reference (9018-06099, p.1586, "Remote Command Results
        // for ACP Measurement"), bench-confirmed on A.07.05. The result format is CONFIG-dependent, not a
        // firmware limitation: in the default SA config (Radio Std = None, 1 carrier, only offset A on),
        // :READ:ACP? returns 3 scalars — [0] reference carrier power (dBm), [1] lower-adjacent (dBc),
        // [2] upper-adjacent (dBc) — so OffsetCount is 0 and the adjacent dBc come straight from [1]/[2].
        // (Enabling more offsets switches to the Total-power-reference format: header [0, total, 0, ref]
        // then 6 offsets x (Lrel, Labs, Urel, Uabs) from index 4 — the app reads the default single-offset
        // result and does not enable the extra offsets.)
        public AcpScalarLayout AcpScalars =>
            new AcpScalarLayout(offsetCount: 0, offsetBaseIndex: 3, upperAdjacentDbcIndex: 2, lowerAdjacentDbcIndex: 1);

        // Force offset A only so :READ:ACP? deterministically returns the 3-scalar result above, regardless
        // of any persistent multi-offset state left on the instrument (which would switch it to the
        // 28-value Total-power-reference table and misalign the indices). The :SENSe:ACPower:… config nodes
        // accept the long form on A.07.05 (only the result verbs require "ACP").
        public string AcpSetupCommand => ":SENSe:ACPower:OFFSet:LIST:STATe ON,OFF,OFF,OFF,OFF,OFF";

        // Wait for measurement completion via SRQ so an X-Series auto-alignment of any length can't trip
        // a fixed read timeout (#129).
        public bool UsesServiceRequestCompletion => true;

        // X-Series screen capture: save a PNG to the instrument's local disk, read it back as a block,
        // then delete it. Manual-derived (X-Series MMEMory subsystem) — confirm on hardware (#143).
        public ScreenCaptureRecipe ScreenCapture => new ScreenCaptureRecipe(
            dataQueryFormat: ":MMEMory:DATA? \"{0}\"",
            saveCommandFormat: ":MMEMory:STORe:SCReen \"{0}\"",
            cleanupCommandFormat: ":MMEMory:DELete \"{0}\"",
            tempPath: "C:\\Temp\\ESGCAP.png");

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
                // Use the SCPI short form "ACP": A.07.05 rejects the long form :READ/FETCh/MEASure:ACPower?
                // with -113 (the :SENSe:ACPower:… config nodes accept the long form; only the result verbs
                // require the short form). "ACP" is documented (short form of ACPower) and portable.
                case VsaMeasurement.Acp: return "ACP";
                case VsaMeasurement.Ccdf: return "PSTatistic";
                case VsaMeasurement.Spectrum: return "SPECtrum";
                case VsaMeasurement.Waveform: return "WAVeform";
                default: throw new ArgumentOutOfRangeException(nameof(measurement), measurement, null);
            }
        }
    }
}
