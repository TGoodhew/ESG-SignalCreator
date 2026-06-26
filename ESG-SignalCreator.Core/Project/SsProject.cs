using System.Runtime.Serialization;

namespace EsgSignalCreator.Project
{
    /// <summary>
    /// A single-file project artifact (<c>*.ssproj</c>, JSON) capturing the active waveform source
    /// (personality + its configuration) together with the instrument settings needed to play it.
    /// </summary>
    /// <remarks>
    /// The personality configuration is stored opaquely as <see cref="ConfigJson"/> (a
    /// JSON-serialized config object) plus <see cref="ConfigTypeName"/> (the config's type name),
    /// so the Core library can round-trip a project without compile-time knowledge of any concrete
    /// personality config type.
    /// </remarks>
    [DataContract]
    public sealed class SsProject
    {
        /// <summary>The current on-disk schema version of the project artifact.</summary>
        public const int CurrentVersion = 1;

        /// <summary>Identifier of the personality that produced the waveform (e.g. "Cw", "Awgn").</summary>
        [DataMember]
        public string PersonalityId { get; set; }

        /// <summary>
        /// Full or assembly-qualified type name of the personality's config object, used to
        /// reconstruct the strongly-typed config from <see cref="ConfigJson"/> on load.
        /// </summary>
        [DataMember]
        public string ConfigTypeName { get; set; }

        /// <summary>The personality's config object serialized to JSON.</summary>
        [DataMember]
        public string ConfigJson { get; set; }

        /// <summary>Instrument-level settings to apply when the project is opened.</summary>
        [DataMember]
        public InstrumentSettings Instrument { get; set; } = new InstrumentSettings();

        /// <summary>Schema version of this artifact. Defaults to <see cref="CurrentVersion"/>.</summary>
        [DataMember]
        public int Version { get; set; } = CurrentVersion;

        /// <summary>
        /// Instrument-level settings captured alongside the waveform source. These mirror the
        /// front-panel / SCPI state that the application drives when playing a signal.
        /// </summary>
        [DataContract]
        public sealed class InstrumentSettings
        {
            /// <summary>RF carrier frequency, in hertz.</summary>
            [DataMember]
            public double FrequencyHz { get; set; }

            /// <summary>RF output amplitude, in dBm.</summary>
            [DataMember]
            public double AmplitudeDbm { get; set; }

            /// <summary>ARB sample (playback) clock, in hertz.</summary>
            [DataMember]
            public double SampleClockHz { get; set; }

            /// <summary>Runtime waveform scaling, as a percentage (typically 0–100).</summary>
            [DataMember]
            public double RuntimeScalingPercent { get; set; }

            /// <summary>Whether the RF output is enabled.</summary>
            [DataMember]
            public bool RfOn { get; set; }

            /// <summary>Whether modulation (the ARB / I-Q source) is enabled.</summary>
            [DataMember]
            public bool ModulationOn { get; set; }
        }
    }
}
