using System.Runtime.Serialization;

namespace EsgSignalCreator.Personalities.BroadcastRadio
{
    /// <summary>
    /// Serializable settings for <see cref="BroadcastRadioPersonality"/> — an analog FM broadcast
    /// signal (mono or stereo, with a 19 kHz pilot and 38 kHz stereo subcarrier). A representative v1
    /// core of Signal Studio for Broadcast Radio (N7611B); the digital broadcast formats (DAB/DAB+/XM)
    /// are covered by the OFDM personalities or deferred.
    /// </summary>
    [DataContract]
    public sealed class BroadcastRadioConfig
    {
        /// <summary>I/Q sample (playback clock) rate, in hertz. Must comfortably exceed the FM bandwidth.</summary>
        [DataMember] public double SampleRateHz { get; set; } = 400e3;

        /// <summary>Number of complex samples to generate.</summary>
        [DataMember] public int Length { get; set; } = 40000;

        /// <summary>Audio test-tone frequency, in hertz.</summary>
        [DataMember] public double AudioToneHz { get; set; } = 1000.0;

        /// <summary>When true, add a 19 kHz stereo pilot and a 38 kHz DSB-SC stereo subcarrier.</summary>
        [DataMember] public bool Stereo { get; set; } = true;

        /// <summary>Peak frequency deviation, in hertz (FM broadcast is 75 kHz).</summary>
        [DataMember] public double PeakDeviationHz { get; set; } = 75e3;

        /// <summary>When true, add the RDS 57 kHz subcarrier — a 1187.5 bps biphase (Manchester) data
        /// stream, DSB-SC on 57 kHz (3× the 19 kHz pilot). (N7611B R-2/R-3.)</summary>
        [DataMember] public bool Rds { get; set; } = false;

        /// <summary>RDS frequency deviation contribution, in hertz (typically ~1–4 kHz of the 75 kHz total).</summary>
        [DataMember] public double RdsDeviationHz { get; set; } = 2e3;

        /// <summary>Seed for the RDS payload PRBS (repeatable).</summary>
        [DataMember] public int RdsSeed { get; set; } = 1;
    }
}
