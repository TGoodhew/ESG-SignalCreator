using System;
using System.Collections.Generic;
using System.Globalization;
using EsgSignalCreator.Instruments;

namespace EsgSignalCreator.Visa
{
    /// <summary>
    /// High-level facade for an Agilent E4406A VSA Series Transmitter Tester — the measurement peer
    /// to <see cref="EsgInstrument"/>. Wraps any <see cref="IInstrument"/> transport (so it is
    /// testable with a fake) and provides identity, options, Basic-mode selection, frequency setup,
    /// and error polling. The analyzer only ever <b>receives</b> RF.
    /// </summary>
    public sealed class VsaInstrument : IDisposable
    {
        private readonly IInstrument _io;
        private VsaModel? _model;
        private IVsaDialect _dialect;

        public VsaInstrument(IInstrument transport)
        {
            _io = transport ?? throw new ArgumentNullException(nameof(transport));
        }

        /// <summary>The wrapped transport, for components that need raw <see cref="IInstrument"/> access.</summary>
        public IInstrument Transport => _io;

        public string ResourceName => _io.ResourceName;

        public int TimeoutMilliseconds
        {
            get => _io.TimeoutMilliseconds;
            set => _io.TimeoutMilliseconds = value;
        }

        /// <summary>Query <c>*IDN?</c> and parse into an <see cref="InstrumentIdentity"/>.</summary>
        public InstrumentIdentity Identify() => InstrumentIdentity.Parse(_io.Query("*IDN?"));

        /// <summary>
        /// The analyzer model resolved from <c>*IDN?</c> (see <see cref="VsaModels.Detect"/>). Cached on
        /// first access; call <see cref="DetectModel"/> to re-query. Drives <see cref="Dialect"/> and the
        /// connect-time model guards.
        /// </summary>
        public VsaModel Model => _model ?? (_model = VsaModels.Detect(Identify().Model)).Value;

        /// <summary>Re-query <c>*IDN?</c> and refresh the cached <see cref="Model"/> and <see cref="Dialect"/>.</summary>
        public VsaModel DetectModel()
        {
            _model = VsaModels.Detect(Identify().Model);
            _dialect = null;
            return _model.Value;
        }

        /// <summary>True if the connected instrument's <see cref="Model"/> is <paramref name="model"/>
        /// (use to refuse driving the wrong instrument).</summary>
        public bool IsModel(VsaModel model) => Model == model;

        /// <summary>
        /// The SCPI dialect for the resolved <see cref="Model"/>. Measurement code reads mnemonics from
        /// here so it stays instrument-agnostic (issue #106 seam; consumed by the SCPI sub-issues).
        /// </summary>
        public IVsaDialect Dialect => _dialect ?? (_dialect = VsaDialects.For(Model));

        /// <summary>Installed options from <c>*OPT?</c> (comma-separated, trimmed, empties removed).</summary>
        public string[] Options()
        {
            string raw = _io.Query("*OPT?") ?? string.Empty;
            var options = new List<string>();
            foreach (string part in raw.Split(','))
            {
                string o = part.Trim();
                if (o.Length > 0) options.Add(o);
            }
            return options.ToArray();
        }

        /// <summary>Read the head of the analyzer's error queue (<c>:SYSTem:ERRor?</c>).</summary>
        public string GetError() => _io.Query(":SYSTem:ERRor?");

        /// <summary>Clear status (<c>*CLS</c>).</summary>
        public void Clear() => _io.Write("*CLS");

        /// <summary>Reset and clear (<c>*RST</c>, <c>*CLS</c>). Use sparingly.</summary>
        public void Reset() { _io.Write("*RST"); _io.Write("*CLS"); }

        /// <summary>Select Basic (format-independent) measurement mode (<c>:INSTrument:SELect BASIC</c>).</summary>
        public void SelectBasicMode() => _io.Write(":INSTrument:SELect BASIC");

        /// <summary>
        /// Select a measurement mode by its SCPI mnemonic (<c>:INSTrument:SELect</c>), e.g. GSM, WCDMA,
        /// CDMA2K — only legal for modes the unit actually has installed (see <see cref="ModeCatalog"/>) (#76).
        /// </summary>
        public void SelectMode(string mnemonic)
        {
            if (string.IsNullOrWhiteSpace(mnemonic)) throw new ArgumentException("Mode mnemonic required.", nameof(mnemonic));
            _io.Write(":INSTrument:SELect " + mnemonic.Trim());
        }

        /// <summary>Read the current measurement mode mnemonic (<c>:INSTrument:SELect?</c>).</summary>
        public string GetMode() => (_io.Query(":INSTrument:SELect?") ?? string.Empty).Trim().Trim('"');

        /// <summary>
        /// The measurement modes installed on this unit, from <c>:INSTrument:CATalog?</c>. This is the
        /// authoritative option gate for standard personalities.
        /// <para>
        /// Handles both response dialects: the E4406A returns individually quoted items
        /// (<c>"BASIC","GSM","WCDMA"</c>) while the X-Series/N9010A returns a single quoted CSV
        /// (<c>"SA,PNOISE,BASIC"</c>). Splitting on comma then trimming quotes from each part parses both.
        /// </para>
        /// </summary>
        public string[] ModeCatalog()
        {
            string raw = _io.Query(":INSTrument:CATalog?") ?? string.Empty;
            var names = new List<string>();
            foreach (string part in raw.Split(','))
            {
                string s = part.Trim().Trim('"').Trim();
                if (s.Length > 0) names.Add(s);
            }
            return names.ToArray();
        }

        /// <summary>Single-measurement mode (<c>:INITiate:CONTinuous OFF</c>) so READ?/MEASure? block to completion.</summary>
        public void SetSingleMeasurement() => _io.Write(":INITiate:CONTinuous OFF");

        /// <summary>
        /// Continuous (running) vs single measurement mode (<c>:INITiate:CONTinuous</c>). Continuous
        /// keeps the front-panel trace updating live (so a watcher sees the current signal, not the
        /// last frozen sweep); single is used for blocking READ?/MEASure? acquisitions.
        /// </summary>
        public void SetContinuous(bool on) => _io.Write(":INITiate:CONTinuous " + (on ? "ON" : "OFF"));

        /// <summary>Set the analyzer center frequency (<c>:SENSe:FREQuency:CENTer</c>), in hertz.</summary>
        public void SetCenterFrequencyHz(double hertz) =>
            _io.Write(":SENSe:FREQuency:CENTer " + hertz.ToString("G17", CultureInfo.InvariantCulture) + " Hz");

        // NOTE: the E4406A has no global :SENSe:FREQuency:SPAN — span is per-measurement
        // (:SENSe:SPECtrum:FREQuency:SPAN, :SENSe:CHPower:FREQuency:SPAN, …), set by each measurement.

        /// <summary>Set the input attenuation (<c>:SENSe:POWer:RF:ATTenuation</c>), in dB.</summary>
        public void SetInputAttenuationDb(double db) =>
            _io.Write(":SENSe:POWer:RF:ATTenuation " + db.ToString("0.###", CultureInfo.InvariantCulture) + " dB");

        /// <summary>
        /// Select the analyzer's timebase (<c>:SENSe:ROSCillator:SOURce</c>): internal, or external to
        /// discipline it to a common 10 MHz with the ESG for clean frequency comparisons (#75).
        /// </summary>
        public void SetReferenceSource(ReferenceSource source) =>
            _io.Write(":SENSe:ROSCillator:SOURce " + ReferenceSourceText.Scpi(source));

        /// <summary>Read which timebase the analyzer is using (<c>:SENSe:ROSCillator:SOURce?</c>).</summary>
        public ReferenceSource GetReferenceSource() =>
            ReferenceSourceText.Parse(_io.Query(":SENSe:ROSCillator:SOURce?"));

        public void Write(string command) => _io.Write(command);
        public string Query(string command) => _io.Query(command);

        public void Dispose() => _io.Dispose();
    }
}
