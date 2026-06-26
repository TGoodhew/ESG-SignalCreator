using System;

namespace EsgSignalCreator.Instruments
{
    /// <summary>
    /// Common message-based transport for a SCPI instrument. Implemented by both the
    /// NI-VISA (<see cref="VisaInstrument"/>) and NI-488.2 / GPIB (<see cref="Gpib488Instrument"/>) backends.
    /// </summary>
    public interface IInstrument : IDisposable
    {
        /// <summary>Human-readable description of the open resource (e.g. "GPIB0::19::INSTR").</summary>
        string ResourceName { get; }

        /// <summary>True once the underlying session/handle is open.</summary>
        bool IsConnected { get; }

        /// <summary>I/O timeout applied to reads and writes.</summary>
        int TimeoutMilliseconds { get; set; }

        /// <summary>Send a command. A trailing newline is appended if not already present.</summary>
        void Write(string command);

        /// <summary>Read a full response string from the instrument.</summary>
        string ReadString();

        /// <summary>Write a command then read the response (e.g. "*IDN?").</summary>
        string Query(string command);

        /// <summary>
        /// Send a fully-formed raw message in a single bus transaction, asserting END only on the
        /// final byte. Used for IEEE-488.2 definite-length binary blocks (ASCII header + binary payload)
        /// such as the ARB waveform download, where a mid-message END would truncate the block.
        /// </summary>
        void WriteBinaryBlock(byte[] message);
    }
}
