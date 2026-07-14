namespace EsgSignalCreator.Instruments
{
    /// <summary>
    /// Optional transport capability: read a <b>raw binary response</b> from the instrument (bytes until
    /// END/EOI), rather than a newline-terminated string. Needed for responses that carry an
    /// IEEE-488.2 definite-length block whose payload can contain any byte value — e.g. a screen-capture
    /// PNG returned by <c>:MMEMory:DATA?</c> (issue #143). Transports that can't do a binary read simply
    /// don't implement this; callers that need it check for the capability and fail clearly otherwise.
    /// </summary>
    public interface ISupportsBinaryRead
    {
        /// <summary>
        /// Read a complete raw response as bytes, assembling chunks until the instrument asserts END.
        /// The termination-character stop is disabled for the duration so a <c>0x0A</c> byte inside the
        /// binary payload does not truncate the read. Returns the full response (block header included);
        /// the caller parses the IEEE-488.2 block. <paramref name="maxBytes"/> caps the total to guard a
        /// runaway read.
        /// </summary>
        byte[] ReadRaw(int maxBytes = 8 * 1024 * 1024);
    }
}
