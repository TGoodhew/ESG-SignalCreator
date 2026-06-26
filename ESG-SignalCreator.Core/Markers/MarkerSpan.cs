namespace EsgSignalCreator.Markers
{
    /// <summary>
    /// An inclusive range of sample indices over which a marker is asserted (turned on).
    /// Both <see cref="Start"/> and <see cref="Stop"/> are sample indices, and the range
    /// <c>[Start, Stop]</c> includes the <see cref="Stop"/> sample itself.
    /// </summary>
    public struct MarkerSpan
    {
        /// <summary>First sample index of the span (inclusive).</summary>
        public int Start;

        /// <summary>Last sample index of the span (inclusive).</summary>
        public int Stop;

        /// <summary>Creates a span covering the inclusive range <c>[start, stop]</c>.</summary>
        public MarkerSpan(int start, int stop)
        {
            Start = start;
            Stop = stop;
        }
    }
}
