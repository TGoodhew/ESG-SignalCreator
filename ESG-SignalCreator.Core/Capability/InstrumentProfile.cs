using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace EsgSignalCreator.Capability
{
    /// <summary>
    /// A target instrument's capability profile: the frequency / sample-clock / memory limits the
    /// validator and offline ("virtual instrument") mode check a generated waveform against.
    /// Profiles ship as embedded JSON resources so new targets need no code change.
    /// </summary>
    [DataContract]
    public sealed class InstrumentProfile
    {
        [DataMember(Name = "model")]
        public string Model { get; set; }

        [DataMember(Name = "minFrequencyHz")]
        public double MinFrequencyHz { get; set; }

        [DataMember(Name = "maxFrequencyHz")]
        public double MaxFrequencyHz { get; set; }

        [DataMember(Name = "maxSampleClockHz")]
        public double MaxSampleClockHz { get; set; }

        [DataMember(Name = "minSamples")]
        public int MinSamples { get; set; }

        [DataMember(Name = "basebandOptions")]
        public BasebandOption[] BasebandOptions { get; set; }

        /// <summary>Largest sample capacity across the installed baseband options, or 0 if none.</summary>
        public long MaxSamples =>
            BasebandOptions == null || BasebandOptions.Length == 0
                ? 0
                : BasebandOptions.Max(o => o.MaxSamples);
    }

    /// <summary>One baseband generator option and the ARB sample capacity it provides.</summary>
    [DataContract]
    public sealed class BasebandOption
    {
        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "maxSamples")]
        public long MaxSamples { get; set; }

        [DataMember(Name = "memoryMB")]
        public int MemoryMB { get; set; }
    }

    /// <summary>Loads <see cref="InstrumentProfile"/>s from the embedded JSON resources in this assembly.</summary>
    public static class InstrumentProfiles
    {
        private const string ResourceSuffix = ".json";
        private const string ResourceMarker = ".Capability.Profiles.";

        /// <summary>Load every embedded capability profile, keyed by model name (case-insensitive).</summary>
        public static IReadOnlyDictionary<string, InstrumentProfile> LoadAll()
        {
            var asm = typeof(InstrumentProfiles).Assembly;
            var map = new Dictionary<string, InstrumentProfile>(StringComparer.OrdinalIgnoreCase);

            foreach (string name in asm.GetManifestResourceNames())
            {
                if (name.IndexOf(ResourceMarker, StringComparison.Ordinal) < 0) continue;
                if (!name.EndsWith(ResourceSuffix, StringComparison.OrdinalIgnoreCase)) continue;

                using (Stream s = asm.GetManifestResourceStream(name))
                {
                    if (s == null) continue;
                    var profile = Deserialize(s);
                    if (!string.IsNullOrEmpty(profile?.Model))
                        map[profile.Model] = profile;
                }
            }
            return map;
        }

        /// <summary>Load a single profile by model name (e.g. "E4438C"); null if not present.</summary>
        public static InstrumentProfile Load(string model)
        {
            LoadAll().TryGetValue(model, out var profile);
            return profile;
        }

        private static InstrumentProfile Deserialize(Stream s)
        {
            var serializer = new DataContractJsonSerializer(typeof(InstrumentProfile));
            return (InstrumentProfile)serializer.ReadObject(s);
        }
    }
}
