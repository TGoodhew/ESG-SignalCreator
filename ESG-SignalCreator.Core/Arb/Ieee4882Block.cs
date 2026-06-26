using System;
using System.Globalization;
using System.Text;

namespace EsgSignalCreator.Arb
{
    /// <summary>
    /// IEEE-488.2 definite-length arbitrary block framing.
    /// A block is <c>#</c> + one digit giving the number of length digits + the length digits
    /// (the payload byte count) + the raw payload bytes. Example: a 2400-byte payload is prefixed
    /// with the ASCII header <c>#42400</c> (4 length digits, value 2400).
    /// </summary>
    public static class Ieee4882Block
    {
        /// <summary>Build just the ASCII block header (e.g. <c>#42400</c>) for a given payload size.</summary>
        public static byte[] BuildHeader(int payloadByteCount)
        {
            if (payloadByteCount < 0)
                throw new ArgumentOutOfRangeException(nameof(payloadByteCount));

            string count = payloadByteCount.ToString(CultureInfo.InvariantCulture);
            if (count.Length > 9)
                throw new ArgumentOutOfRangeException(nameof(payloadByteCount),
                    "Payload too large for a single-digit length prefix (IEEE-488.2 allows 1–9 length digits).");

            string header = "#" + count.Length.ToString(CultureInfo.InvariantCulture) + count;
            return Encoding.ASCII.GetBytes(header);
        }

        /// <summary>
        /// Frame a raw payload as a complete definite-length block: header bytes followed by the
        /// payload bytes, ready to be prefixed with a SCPI command and written in one bus transaction.
        /// </summary>
        public static byte[] Frame(byte[] payload)
        {
            if (payload == null) throw new ArgumentNullException(nameof(payload));

            byte[] header = BuildHeader(payload.Length);
            var block = new byte[header.Length + payload.Length];
            Buffer.BlockCopy(header, 0, block, 0, header.Length);
            Buffer.BlockCopy(payload, 0, block, header.Length, payload.Length);
            return block;
        }

        /// <summary>
        /// Compose a full SCPI binary message: <paramref name="commandPrefix"/> (e.g.
        /// <c>:MEMory:DATA "WFM1:seg",</c>) immediately followed by the framed definite-length block.
        /// </summary>
        public static byte[] Message(string commandPrefix, byte[] payload)
        {
            if (commandPrefix == null) throw new ArgumentNullException(nameof(commandPrefix));

            byte[] prefix = Encoding.ASCII.GetBytes(commandPrefix);
            byte[] block = Frame(payload);
            var message = new byte[prefix.Length + block.Length];
            Buffer.BlockCopy(prefix, 0, message, 0, prefix.Length);
            Buffer.BlockCopy(block, 0, message, prefix.Length, block.Length);
            return message;
        }
    }
}
