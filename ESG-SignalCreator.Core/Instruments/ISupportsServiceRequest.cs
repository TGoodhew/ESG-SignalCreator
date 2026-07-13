namespace EsgSignalCreator.Instruments
{
    /// <summary>
    /// Optional transport capability: IEEE-488.2 <b>Service Request (SRQ)</b> waiting plus a status-byte
    /// serial poll. Lets a caller wait for a response/operation to complete with <b>no single fixed
    /// timeout</b> — e.g. through a Keysight N9010A auto-alignment of arbitrary length (issue #129) — by
    /// re-arming short SRQ waits until the Status Byte signals the response is ready (MAV, bit 4 / 0x10).
    /// Transports that can't deliver SRQ simply don't implement this; callers fall back to a blocking read.
    /// </summary>
    public interface ISupportsServiceRequest
    {
        /// <summary>Arm SRQ delivery (discarding any stale events first).</summary>
        void EnableServiceRequest();

        /// <summary>Stop SRQ delivery.</summary>
        void DisableServiceRequest();

        /// <summary>Wait up to <paramref name="timeoutMs"/> for an SRQ. True if one arrived; false on timeout.</summary>
        bool WaitForServiceRequest(int timeoutMs);

        /// <summary>Serial-poll and return the Status Byte (also clears a pending SRQ). Bit 4 (0x10) = MAV.</summary>
        int ReadStatusByte();
    }
}
