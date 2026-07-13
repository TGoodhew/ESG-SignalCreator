using System;
using EsgSignalCreator.Visa;

namespace EsgSignalCreator.Measure
{
    /// <summary>
    /// Runs Basic-mode measurements on a <see cref="VsaInstrument"/>: shared setup (mode, single,
    /// center/span) and the verb plumbing (READ/MEASure/FETCh/CONFigure) returning parsed scalars.
    /// Concrete measurements (Channel Power, Spectrum, …) build their typed results on top of this.
    /// </summary>
    public sealed class BasicMeasurement
    {
        private readonly VsaInstrument _vsa;

        public BasicMeasurement(VsaInstrument vsa)
        {
            _vsa = vsa ?? throw new ArgumentNullException(nameof(vsa));
        }

        public VsaInstrument Vsa => _vsa;

        /// <summary>
        /// The analyzer's SCPI dialect (issue #106 seam). Measurement classes read model-varying
        /// mnemonics — measurement roots, per-measurement vs global span, mode entry — from here so the
        /// same measurement code drives either the E4406A or the N9010A.
        /// </summary>
        public IVsaDialect Dialect => _vsa.Dialect;

        /// <summary>
        /// Put the analyzer into the correct instrument mode for <paramref name="measurement"/>, single
        /// measurement, at the given center frequency. The mode comes from the dialect: on the E4406A
        /// every measurement is <c>BASIC</c>; on the N9010A, Spectrum/Waveform run in the IQ Analyzer
        /// (<c>BASIC</c>) mode while Channel Power/ACP/CCDF run in Spectrum Analyzer (<c>SA</c>) mode.
        /// Span is applied by each concrete measurement afterwards (per-measurement on the E4406A;
        /// global on the N9010A — see <see cref="IVsaDialect.HasGlobalSpan"/>).
        /// </summary>
        public void Setup(VsaMeasurement measurement, double centerHz)
        {
            _vsa.SelectMode(_vsa.Dialect.InstrumentModeFor(measurement));
            _vsa.SetSingleMeasurement();
            _vsa.SetCenterFrequencyHz(centerHz);
        }

        // Measurement reads go through QueryMeasurement so a model that opts into SRQ completion (#129)
        // waits out an auto-alignment of any length instead of tripping a fixed read timeout.
        public double[] Read(string root, int n = 1) => VsaScalarParser.ParseScalars(_vsa.QueryMeasurement(VsaCommands.Read(root, n)));
        public double[] Measure(string root, int n = 1) => VsaScalarParser.ParseScalars(_vsa.QueryMeasurement(VsaCommands.Measure(root, n)));
        public double[] Fetch(string root, int n = 1) => VsaScalarParser.ParseScalars(_vsa.QueryMeasurement(VsaCommands.Fetch(root, n)));
        public void Configure(string root) => _vsa.Write(VsaCommands.Configure(root));
    }
}
