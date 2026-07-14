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
        /// Extract the payload from a definite-length block <b>response</b> (the inverse of
        /// <see cref="Frame"/>). Skips any bytes before the leading <c>#</c>, reads the length prefix,
        /// and returns exactly that many payload bytes. Tolerant of a trailing terminator after the
        /// payload. If no <c>#</c> header is present (or it is malformed), the raw bytes are returned
        /// unchanged (best effort) so a non-block response still yields something usable.
        /// <para>Special case <c>#0</c> (indefinite length): returns everything after the header, minus a
        /// single trailing newline if present.</para>
        /// </summary>
        public static byte[] ParsePayload(byte[] response)
        {
            if (response == null) throw new ArgumentNullException(nameof(response));
            if (response.Length == 0) return response;

            // Find the '#' that starts the block (instruments may emit leading whitespace).
            int hash = -1;
            for (int i = 0; i < response.Length; i++)
            {
                if (response[i] == (byte)'#') { hash = i; break; }
                if (response[i] == (byte)' ' || response[i] == (byte)'\r' || response[i] == (byte)'\n' || response[i] == (byte)'\t')
                    continue;
                break; // a non-whitespace, non-'#' byte before any '#': not a framed block
            }
            if (hash < 0 || hash + 1 >= response.Length) return response;

            char nDigitsChar = (char)response[hash + 1];
            if (nDigitsChar < '0' || nDigitsChar > '9') return response;
            int nDigits = nDigitsChar - '0';

            int payloadStart;
            int payloadLen;
            if (nDigits == 0)
            {
                // Indefinite length (#0): payload runs to the end, minus one trailing newline if present.
                payloadStart = hash + 2;
                payloadLen = response.Length - payloadStart;
                if (payloadLen > 0 && response[response.Length - 1] == (byte)'\n') payloadLen--;
            }
            else
            {
                int lenStart = hash + 2;
                if (lenStart + nDigits > response.Length) return response;
                long len = 0;
                for (int i = 0; i < nDigits; i++)
                {
                    byte d = response[lenStart + i];
                    if (d < (byte)'0' || d > (byte)'9') return response;
                    len = len * 10 + (d - (byte)'0');
                }
                payloadStart = lenStart + nDigits;
                payloadLen = (int)Math.Min(len, response.Length - payloadStart);
            }

            var payload = new byte[payloadLen];
            Buffer.BlockCopy(response, payloadStart, payload, 0, payloadLen);
            return payload;
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
