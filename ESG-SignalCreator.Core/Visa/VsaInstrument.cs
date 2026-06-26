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

        /// <summary>True if <c>*IDN?</c> reports an E4406A (use to refuse driving the wrong instrument).</summary>
        public bool IsE4406A()
        {
            string model = Identify().Model;
            return model != null && model.IndexOf("E4406A", StringComparison.OrdinalIgnoreCase) >= 0;
        }

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

        /// <summary>Single-measurement mode (<c>:INITiate:CONTinuous OFF</c>) so READ?/MEASure? block to completion.</summary>
        public void SetSingleMeasurement() => _io.Write(":INITiate:CONTinuous OFF");

        /// <summary>Set the analyzer center frequency (<c>:SENSe:FREQuency:CENTer</c>), in hertz.</summary>
        public void SetCenterFrequencyHz(double hertz) =>
            _io.Write(":SENSe:FREQuency:CENTer " + hertz.ToString("G17", CultureInfo.InvariantCulture) + " Hz");

        /// <summary>Set the analyzer frequency span (<c>:SENSe:FREQuency:SPAN</c>), in hertz.</summary>
        public void SetSpanHz(double hertz) =>
            _io.Write(":SENSe:FREQuency:SPAN " + hertz.ToString("G17", CultureInfo.InvariantCulture) + " Hz");

        /// <summary>Set the input attenuation (<c>:SENSe:POWer:RF:ATTenuation</c>), in dB.</summary>
        public void SetInputAttenuationDb(double db) =>
            _io.Write(":SENSe:POWer:RF:ATTenuation " + db.ToString("0.###", CultureInfo.InvariantCulture) + " dB");

        public void Write(string command) => _io.Write(command);
        public string Query(string command) => _io.Query(command);

        public void Dispose() => _io.Dispose();
    }
}
