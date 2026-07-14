using System;
using System.Collections.Generic;
using System.Globalization;
using EsgSignalCreator.Instruments;

namespace EsgSignalCreator.Visa
{
    /// <summary>
    /// High-level facade for a vector signal analyzer — an Agilent E4406A (Basic mode) or a
    /// Keysight N9010A/EXA X-Series (SA / IQ Analyzer) — the measurement peer to
    /// <see cref="EsgInstrument"/>. Per-model SCPI differences are resolved through <see cref="Dialect"/>.
    /// Wraps any <see cref="IInstrument"/> transport (so it is testable with a fake) and provides
    /// identity, options, mode selection, frequency setup, and error polling. The analyzer only ever
    /// <b>receives</b> RF.
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

        /// <summary>
        /// Drain the analyzer's error queue, returning every non-zero entry (empty if clean). Reads
        /// <c>:SYSTem:ERRor?</c> until code 0, so a measurement can surface over/under-range conditions
        /// (e.g. input overload) that the analyzer reports here rather than failing silently (#120).
        /// Bounded so a stuck queue can't loop forever.
        /// </summary>
        public IReadOnlyList<string> ReadErrorQueue(int maxEntries = 20)
        {
            var errors = new List<string>();
            for (int i = 0; i < maxEntries; i++)
            {
                string e = _io.Query(":SYSTem:ERRor?");
                if (string.IsNullOrWhiteSpace(e)) break;
                string code = e.Split(',')[0].Trim();
                if (code == "0" || code == "+0" || code == "-0") break;
                errors.Add(e.Trim());
            }
            return errors;
        }

        /// <summary>Clear status (<c>*CLS</c>).</summary>
        public void Clear() => _io.Write("*CLS");

        /// <summary>Reset and clear (<c>*RST</c>, <c>*CLS</c>). Use sparingly.</summary>
        public void Reset() { _io.Write("*RST"); _io.Write("*CLS"); }

        /// <summary>Select Basic (format-independent) measurement mode (<c>:INSTrument:SELect BASIC</c>).</summary>
        public void SelectBasicMode() => _io.Write(":INSTrument:SELect BASIC");

        /// <summary>
        /// Select a measurement mode by its SCPI mnemonic (<c>:INSTrument:SELect</c>), e.g. GSM, WCDMA,
        /// CDMA2K. When <paramref name="verifyInstalled"/> is true (the user/assistant path), the mnemonic
        /// is first checked against <see cref="ModeCatalog"/> and an uninstalled personality is refused
        /// with a clear message rather than relying on a silent instrument-side rejection (#76/#120). The
        /// internal measurement path leaves it false — the core modes (BASIC/SA) are always present, so it
        /// skips the extra catalog query.
        /// </summary>
        public void SelectMode(string mnemonic, bool verifyInstalled = false)
        {
            if (string.IsNullOrWhiteSpace(mnemonic)) throw new ArgumentException("Mode mnemonic required.", nameof(mnemonic));
            mnemonic = mnemonic.Trim();

            if (verifyInstalled)
            {
                string[] installed = ModeCatalog();
                bool present = false;
                foreach (string m in installed)
                    if (string.Equals(m, mnemonic, StringComparison.OrdinalIgnoreCase)) { present = true; break; }
                if (!present)
                    throw new InvalidOperationException(
                        "Mode '" + mnemonic + "' is not installed on this analyzer. Installed: " +
                        (installed.Length > 0 ? string.Join(", ", installed) : "(none reported)") + ".");
            }

            _io.Write(":INSTrument:SELect " + mnemonic);
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

        // #129: measurement reads on an analyzer that opts into SRQ completion (the N9010A) wait for the
        // response via the Status-Byte MAV bit, re-arming short SRQ waits, so an auto-alignment of any
        // length can't trip a fixed timeout. Other models (and transports without SRQ) fall back to a
        // blocking query (with a raised, alignment-tolerant timeout when the dialect asks for it).
        private const int SrqPerWaitMs = 2000;
        private const int CompletionOverallMs = 120000;

        /// <summary>
        /// Optional diagnostic hook (issue #134): invoked with (command, rawResponse) for every
        /// measurement read, so a caller can surface the exact analyzer traffic — e.g. to see whether
        /// <c>:READ:PSTatistic?</c> returns 10 scalars or a long trace. Null by default (no overhead).
        /// </summary>
        public Action<string, string> MeasurementTrace { get; set; }

        /// <summary>Read a measurement query result, tolerating an arbitrary-length auto-alignment.</summary>
        public string QueryMeasurement(string command)
        {
            string response = QueryMeasurementCore(command);
            MeasurementTrace?.Invoke(command, response);
            return response;
        }

        // Set once the SRQ mechanism proves unusable on this session (e.g. an older N9010A over VXI-11
        // that accepts the SRQ setup but never delivers the Service Request). From then on the session
        // uses the blocking read below, so one broken read doesn't make every measurement pay the cost.
        private bool _serviceRequestUnusable;

        private string QueryMeasurementCore(string command)
        {
            if (Dialect.UsesServiceRequestCompletion && !_serviceRequestUnusable && _io is ISupportsServiceRequest srq)
            {
                try { return QueryViaServiceRequest(srq, command); }
                catch (Exception ex) when (!(ex is TimeoutException))
                {
                    // SRQ setup was accepted but the Service Request was never delivered (seen on an
                    // N9010A/A.07.05 over VXI-11, which raises a miscellaneous VISA error rather than a
                    // clean timeout). Disable SRQ for the rest of the session, clear the aborted state,
                    // and fall through to a blocking read — never turn this into a hard failure.
                    _serviceRequestUnusable = true;
                    try { _io.Write("*CLS"); _io.Write("*SRE 0"); } catch { /* best effort */ }
                }
            }

            if (!Dialect.UsesServiceRequestCompletion)
                return _io.Query(command);

            // SRQ not used (or fell back): block, but raise the timeout to ride out an auto-alignment.
            int previous = _io.TimeoutMilliseconds;
            try { _io.TimeoutMilliseconds = Math.Max(previous, CompletionOverallMs); return _io.Query(command); }
            finally { _io.TimeoutMilliseconds = previous; }
        }

        private string QueryViaServiceRequest(ISupportsServiceRequest srq, string command)
        {
            const int Mav = 0x10; // Status-Byte MAV (message available)
            _io.Write("*CLS");
            _io.Write("*SRE 16"); // MAV summary -> Status Byte -> asserts SRQ when a response is ready
            srq.EnableServiceRequest();
            try
            {
                _io.Write(command);
                int waited = 0;
                while (true)
                {
                    if (srq.WaitForServiceRequest(SrqPerWaitMs) && (srq.ReadStatusByte() & Mav) != 0)
                        break;
                    waited += SrqPerWaitMs;
                    if (waited >= CompletionOverallMs)
                        throw new TimeoutException(
                            "Analyzer response not ready within " + (CompletionOverallMs / 1000) +
                            " s (possible prolonged auto-alignment).");
                }
                return _io.ReadString();
            }
            finally
            {
                srq.DisableServiceRequest();
                try { _io.Write("*SRE 0"); } catch { /* best effort */ }
            }
        }

        /// <summary>
        /// Capture the analyzer's display and return the image bytes (issue #143). Uses the model's
        /// <see cref="IVsaDialect.ScreenCapture"/> recipe unless <paramref name="recipe"/> overrides it:
        /// runs the save command (if any), waits for it (<c>*OPC?</c>), reads the image back as an
        /// IEEE-488.2 block, deletes the instrument-side file, and returns the block payload. The bytes
        /// are whatever format the instrument wrote (PNG on the X-Series, GIF on the E4406A).
        /// </summary>
        /// <exception cref="NotSupportedException">
        /// No recipe is available for the model, or the transport can't do a binary read.
        /// </exception>
        public byte[] CaptureScreen(ScreenCaptureRecipe recipe = null)
        {
            recipe = recipe ?? Dialect.ScreenCapture;
            if (recipe == null)
                throw new NotSupportedException("No screen-capture recipe is defined for model " + Model + ".");
            if (string.IsNullOrWhiteSpace(recipe.DataQueryFormat))
                throw new NotSupportedException("The screen-capture recipe has no data query.");
            if (!(_io is ISupportsBinaryRead reader))
                throw new NotSupportedException("This transport does not support the binary read needed for screen capture.");

            string path = recipe.TempPath ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(recipe.SaveCommandFormat))
            {
                _io.Write(string.Format(recipe.SaveCommandFormat, path));
                if (recipe.OpcAfterSave) _io.Query("*OPC?"); // ensure the file is fully written first
            }

            _io.Write(string.Format(recipe.DataQueryFormat, path));
            byte[] raw = reader.ReadRaw();
            byte[] image = Arb.Ieee4882Block.ParsePayload(raw);

            if (!string.IsNullOrWhiteSpace(recipe.CleanupCommandFormat))
            {
                try { _io.Write(string.Format(recipe.CleanupCommandFormat, path)); } catch { /* best effort */ }
            }

            return image;
        }

        public void Dispose() => _io.Dispose();
    }
}
