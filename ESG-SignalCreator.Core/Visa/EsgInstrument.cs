using System;
using System.Collections.Generic;
using EsgSignalCreator.Instruments;

namespace EsgSignalCreator.Visa
{
    /// <summary>How <see cref="EsgInstrument.Open"/> should construct the underlying transport.</summary>
    public enum ConnectionKind
    {
        /// <summary>Use NI-VISA (<see cref="VisaInstrument"/>) with a VISA resource string.</summary>
        Visa,

        /// <summary>Use NI-488.2 (<see cref="Gpib488Instrument"/>) addressed by board and primary address.</summary>
        Gpib
    }

    /// <summary>
    /// Where and how to connect to an instrument. Consumed by <see cref="EsgInstrument.Open"/>.
    /// </summary>
    public sealed class ConnectionSettings
    {
        /// <summary>Selects the transport backend.</summary>
        public ConnectionKind Kind { get; set; } = ConnectionKind.Visa;

        /// <summary>VISA resource string, used when <see cref="Kind"/> is <see cref="ConnectionKind.Visa"/>.</summary>
        public string VisaResource { get; set; }

        /// <summary>GPIB board index, used when <see cref="Kind"/> is <see cref="ConnectionKind.Gpib"/>.</summary>
        public int GpibBoard { get; set; }

        /// <summary>GPIB primary address, used when <see cref="Kind"/> is <see cref="ConnectionKind.Gpib"/>.</summary>
        public byte GpibAddress { get; set; }

        /// <summary>I/O timeout in milliseconds applied to the opened transport.</summary>
        public int TimeoutMs { get; set; } = 5000;
    }

    /// <summary>
    /// High-level facade over an <see cref="IInstrument"/> transport that adds the common
    /// IEEE-488.2 identity/option queries (<c>*IDN?</c>, <c>*OPT?</c>) and ESG-specific capability checks.
    /// Wrapping an <see cref="IInstrument"/> keeps it fully testable with a fake transport.
    /// </summary>
    public sealed class EsgInstrument : IDisposable
    {
        private readonly IInstrument _io;

        /// <summary>ESG baseband-generator option numbers (ARB / real-time). Presence of any means the box can play ARB waveforms.</summary>
        private static readonly HashSet<string> BasebandOptions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "001", "002", "601", "602" };

        /// <summary>Wrap an already-open transport.</summary>
        public EsgInstrument(IInstrument transport)
        {
            _io = transport ?? throw new ArgumentNullException(nameof(transport));
        }

        /// <summary>The wrapped transport, for components that need raw <see cref="IInstrument"/>
        /// access (e.g. building an <c>EsgController</c> or attaching a SCPI console).</summary>
        public IInstrument Transport => _io;

        /// <summary>The wrapped transport's resource description (e.g. "GPIB0::19::INSTR").</summary>
        public string ResourceName => _io.ResourceName;

        /// <summary>I/O timeout (ms) of the wrapped transport.</summary>
        public int TimeoutMilliseconds
        {
            get => _io.TimeoutMilliseconds;
            set => _io.TimeoutMilliseconds = value;
        }

        /// <summary>Query <c>*IDN?</c> and parse the response into an <see cref="InstrumentIdentity"/>.</summary>
        public InstrumentIdentity Identify()
        {
            return InstrumentIdentity.Parse(_io.Query("*IDN?"));
        }

        /// <summary>
        /// Query <c>*OPT?</c> and return the installed option strings: comma-separated, trimmed,
        /// with empty entries removed.
        /// </summary>
        public string[] Options()
        {
            string raw = _io.Query("*OPT?") ?? string.Empty;
            var options = new List<string>();
            foreach (string part in raw.Split(','))
            {
                string opt = part.Trim();
                if (opt.Length > 0) options.Add(opt);
            }
            return options.ToArray();
        }

        /// <summary>
        /// True if the instrument reports any baseband-generator option (001/002/601/602),
        /// i.e. it can play internal ARB waveforms.
        /// </summary>
        public bool HasBasebandGenerator()
        {
            foreach (string opt in Options())
                if (BasebandOptions.Contains(opt)) return true;
            return false;
        }

        /// <summary>Passthrough write to the wrapped transport.</summary>
        public void Write(string command) => _io.Write(command);

        /// <summary>Passthrough query to the wrapped transport.</summary>
        public string Query(string command) => _io.Query(command);

        /// <summary>Format a GPIB VISA resource string from a board and primary address.</summary>
        public static string BuildGpibResource(int board, byte address)
        {
            return string.Format("GPIB{0}::{1}::INSTR", board, address);
        }

        /// <summary>Open the transport described by <paramref name="settings"/> and wrap it in an <see cref="EsgInstrument"/>.</summary>
        public static EsgInstrument Open(ConnectionSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            IInstrument transport;
            switch (settings.Kind)
            {
                case ConnectionKind.Visa:
                    transport = new VisaInstrument(settings.VisaResource, settings.TimeoutMs);
                    break;
                case ConnectionKind.Gpib:
                    transport = new Gpib488Instrument(settings.GpibBoard, settings.GpibAddress, settings.TimeoutMs);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(settings), settings.Kind, "Unsupported connection kind.");
            }

            return new EsgInstrument(transport);
        }

        /// <summary>Dispose the wrapped transport.</summary>
        public void Dispose() => _io.Dispose();
    }
}
