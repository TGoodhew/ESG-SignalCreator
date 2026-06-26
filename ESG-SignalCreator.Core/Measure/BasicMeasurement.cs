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

        /// <summary>Put the analyzer into Basic single-measurement mode at the given center (and span if &gt; 0).</summary>
        public void Setup(double centerHz, double spanHz = 0)
        {
            _vsa.SelectBasicMode();
            _vsa.SetSingleMeasurement();
            _vsa.SetCenterFrequencyHz(centerHz);
            if (spanHz > 0) _vsa.SetSpanHz(spanHz);
        }

        public double[] Read(string root, int n = 1) => VsaScalarParser.ParseScalars(_vsa.Query(VsaCommands.Read(root, n)));
        public double[] Measure(string root, int n = 1) => VsaScalarParser.ParseScalars(_vsa.Query(VsaCommands.Measure(root, n)));
        public double[] Fetch(string root, int n = 1) => VsaScalarParser.ParseScalars(_vsa.Query(VsaCommands.Fetch(root, n)));
        public void Configure(string root) => _vsa.Write(VsaCommands.Configure(root));
    }
}
