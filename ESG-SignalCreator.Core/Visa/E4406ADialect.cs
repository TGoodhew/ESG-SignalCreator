using System;

namespace EsgSignalCreator.Visa
{
    /// <summary>
    /// SCPI dialect for the Agilent E4406A. These are exactly the mnemonics the measurement classes
    /// have always used, now expressed through <see cref="IVsaDialect"/> so the N9010A can supply its
    /// own variant without forking the measurement code. Every Basic-mode measurement runs in
    /// <c>BASIC</c>, span is per-measurement (no global span).
    /// </summary>
    public sealed class E4406ADialect : IVsaDialect
    {
        public VsaModel Model => VsaModel.E4406A;

        public bool HasGlobalSpan => false;

        public string InstrumentModeFor(VsaMeasurement measurement) => "BASIC";

        public string RootFor(VsaMeasurement measurement)
        {
            switch (measurement)
            {
                case VsaMeasurement.ChannelPower: return "CHPower";
                case VsaMeasurement.Acp: return "ACP";
                case VsaMeasurement.Ccdf: return "PSTatistic";
                case VsaMeasurement.Spectrum: return "SPECtrum";
                case VsaMeasurement.Waveform: return "WAVeform";
                default: throw new ArgumentOutOfRangeException(nameof(measurement), measurement, null);
            }
        }
    }
}
