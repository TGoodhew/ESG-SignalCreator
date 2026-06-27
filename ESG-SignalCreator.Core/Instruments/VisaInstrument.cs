using System;
using System.Collections.Generic;
using Ivi.Visa;

namespace EsgSignalCreator.Instruments
{
    /// <summary>
    /// Message-based instrument transport over the vendor-neutral IVI VISA.NET shared components
    /// (<see cref="GlobalResourceManager"/>). It works with ANY installed VISA provider — Keysight IO
    /// Libraries, NI-VISA, Rohde &amp; Schwarz, Rigol, Tektronix, … — and any resource string
    /// (TCPIP/LAN, GPIB, USB, serial). The actual provider is chosen at runtime by whatever VISA is
    /// installed; this code never references a vendor-specific assembly (#102).
    /// </summary>
    public sealed class VisaInstrument : IInstrument
    {
        private IMessageBasedSession _session;

        public VisaInstrument(string resourceName, int timeoutMilliseconds = 5000)
        {
            if (string.IsNullOrWhiteSpace(resourceName))
                throw new ArgumentException("A VISA resource name is required.", nameof(resourceName));

            ResourceName = resourceName;

            // GlobalResourceManager dispatches to the installed VISA provider for this resource.
            _session = (IMessageBasedSession)GlobalResourceManager.Open(resourceName);
            _session.TimeoutMilliseconds = timeoutMilliseconds;
            _session.TerminationCharacterEnabled = true;
        }

        public string ResourceName { get; }

        public bool IsConnected => _session != null;

        public int TimeoutMilliseconds
        {
            get => _session?.TimeoutMilliseconds ?? 0;
            set { if (_session != null) _session.TimeoutMilliseconds = value; }
        }

        /// <summary>Enumerate the VISA instrument resources visible to the installed VISA provider(s).</summary>
        public static IEnumerable<string> FindResources(string filter = "?*INSTR")
        {
            try
            {
                return new List<string>(GlobalResourceManager.Find(filter));
            }
            catch (Exception)
            {
                // No matching resources; VISA throws rather than returning an empty set.
                return new List<string>();
            }
        }

        public void Write(string command)
        {
            EnsureOpen();
            if (!command.EndsWith("\n")) command += "\n";
            _session.RawIO.Write(command);
        }

        public string ReadString()
        {
            EnsureOpen();
            return _session.RawIO.ReadString().TrimEnd('\r', '\n');
        }

        public string Query(string command)
        {
            Write(command);
            return ReadString();
        }

        public void WriteBinaryBlock(byte[] message)
        {
            EnsureOpen();
            // A single RawIO.Write(byte[]) call asserts END (EOI) only on the last byte, so the
            // ASCII header and binary payload travel as one uninterrupted block.
            _session.RawIO.Write(message);
        }

        private void EnsureOpen()
        {
            if (_session == null)
                throw new InvalidOperationException("The VISA session is not open.");
        }

        public void Dispose()
        {
            _session?.Dispose();
            _session = null;
        }
    }
}
