using System.Runtime.Serialization;

namespace EsgSignalCreator.Sequencing
{
    /// <summary>When a sequencer step waits before starting.</summary>
    public enum WaitMode { Off, TrigA, TrigB, Internal }

    /// <summary>
    /// One row of the sequencer table (UX brief §5.1): a named waveform segment played with a
    /// repeat count and per-segment power, optionally gated on a trigger, with event-jump / go-to
    /// flow control, markers, step flags, and inserted idle samples.
    /// </summary>
    [DataContract]
    public sealed class SequenceStep
    {
        [DataMember(Name = "waveform")] public string Waveform { get; set; } = "";

        /// <summary>Loop count for this step (1…1,048,576). <see cref="int.MaxValue"/> means "infinite".</summary>
        [DataMember(Name = "repeat")] public int Repeat { get; set; } = 1;

        [DataMember(Name = "powerDb")] public double PowerDb { get; set; }

        [DataMember(Name = "wait")] public WaitMode Wait { get; set; } = WaitMode.Off;

        /// <summary>Event-jump destination step index, or -1 for "next".</summary>
        [DataMember(Name = "eventJump")] public int EventJump { get; set; } = -1;

        /// <summary>Unconditional go-to step index after this step, or -1 for "next".</summary>
        [DataMember(Name = "goTo")] public int GoTo { get; set; } = -1;

        [DataMember(Name = "marker")] public bool MarkerEnabled { get; set; }

        /// <summary>Step flags A,B,C,D (full-resolution per-step indicators, distinct from markers).</summary>
        [DataMember(Name = "flags")] public bool[] Flags { get; set; } = new bool[4];

        /// <summary>Idle samples inserted before the segment (to create bursts).</summary>
        [DataMember(Name = "idleSamples")] public int IdleSamples { get; set; }

        /// <summary>Name of a sub-sequence this step references instead of a waveform, or null.</summary>
        [DataMember(Name = "subSequence")] public string SubSequence { get; set; }

        public const int InfiniteRepeat = int.MaxValue;
    }
}
