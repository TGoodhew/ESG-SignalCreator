using System;
using System.Collections.Generic;

namespace EsgSignalCreator.Markers
{
    /// <summary>
    /// Builds a per-sample marker stream for an ARB waveform. The stream is one byte per
    /// sample where a value of <c>1</c> means the marker is on at that sample and <c>0</c>
    /// means it is off.
    /// </summary>
    /// <remarks>
    /// Markers are a single bit per sample that travel embedded in the waveform; they are
    /// distinct from per-step "flags" which are separate indicators carried alongside a
    /// sequence. All builder methods clamp sample indices into the valid range
    /// <c>[0, length)</c> so out-of-range inputs never throw.
    /// </remarks>
    public static class MarkerBuilder
    {
        /// <summary>
        /// Builds a marker stream of <paramref name="length"/> samples with every sample
        /// covered by one of <paramref name="spans"/> set to <c>1</c> and all others <c>0</c>.
        /// Span indices are clamped to <c>[0, length)</c>; spans whose start is after their
        /// stop contribute nothing.
        /// </summary>
        public static byte[] FromSpans(int length, IEnumerable<MarkerSpan> spans)
        {
            var markers = NewStream(length);
            if (length == 0 || spans == null)
                return markers;

            foreach (var span in spans)
            {
                int start = Clamp(span.Start, length);
                int stop = Clamp(span.Stop, length);
                for (int i = start; i <= stop; i++)
                    markers[i] = 1;
            }
            return markers;
        }

        /// <summary>
        /// Builds a marker stream with the marker on at sample 0 only (and off everywhere
        /// else). Returns an empty array when <paramref name="length"/> is 0.
        /// </summary>
        public static byte[] AtStart(int length)
        {
            var markers = NewStream(length);
            if (length > 0)
                markers[0] = 1;
            return markers;
        }

        /// <summary>
        /// Builds a marker stream with the marker on at sample indices 0, n, 2n, …
        /// When <paramref name="n"/> is less than 1, only sample 0 is set.
        /// </summary>
        public static byte[] EveryN(int length, int n)
        {
            var markers = NewStream(length);
            if (length == 0)
                return markers;
            if (n < 1)
            {
                markers[0] = 1;
                return markers;
            }
            for (int i = 0; i < length; i += n)
                markers[i] = 1;
            return markers;
        }

        /// <summary>
        /// Convenience builder that sets the marker on over the single inclusive range
        /// <c>[start, stop]</c>, clamped to <c>[0, length)</c>.
        /// </summary>
        public static byte[] RangeOnOff(int length, int start, int stop)
        {
            return FromSpans(length, new[] { new MarkerSpan(start, stop) });
        }

        private static byte[] NewStream(int length)
        {
            if (length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), length, "Marker stream length cannot be negative.");
            return new byte[length];
        }

        private static int Clamp(int index, int length)
        {
            if (index < 0)
                return 0;
            if (index > length - 1)
                return length - 1;
            return index;
        }
    }
}
