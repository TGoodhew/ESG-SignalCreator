using System;
using System.IO;
using EsgSignalCreator.Visa;
using Newtonsoft.Json;

namespace EsgSignalCreator.Ui
{
    /// <summary>
    /// Small user-scoped UI preferences persisted to <c>%AppData%\ESG-SignalCreator\ui-settings.json</c>
    /// (Newtonsoft.Json, matching the assistant layer). Best effort: load/save failures never throw — a
    /// missing or corrupt file just yields defaults.
    /// </summary>
    public sealed class UiSettings
    {
        /// <summary>The last analyzer model the user selected in the VSA toggle. Defaults to E4406A.</summary>
        [JsonProperty("vsaModel")]
        public string VsaModelName { get; set; } = VsaModel.E4406A.ToString();

        /// <summary>The selected VSA model as an enum (falls back to E4406A for unknown/blank values).</summary>
        [JsonIgnore]
        public VsaModel VsaModel
        {
            get => Enum.TryParse(VsaModelName, out VsaModel m) && m != VsaModel.Unknown ? m : VsaModel.E4406A;
            set => VsaModelName = value.ToString();
        }

        private static string FilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ESG-SignalCreator", "ui-settings.json");

        /// <summary>Load settings, or defaults if the file is absent/unreadable.</summary>
        public static UiSettings Load()
        {
            try
            {
                string path = FilePath;
                if (!File.Exists(path)) return new UiSettings();
                return JsonConvert.DeserializeObject<UiSettings>(File.ReadAllText(path)) ?? new UiSettings();
            }
            catch
            {
                return new UiSettings();
            }
        }

        /// <summary>Persist settings (best effort — swallows I/O errors).</summary>
        public void Save()
        {
            try
            {
                string path = FilePath;
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, JsonConvert.SerializeObject(this, Formatting.Indented));
            }
            catch
            {
                // Preferences are non-critical; never let a save failure disrupt the UI.
            }
        }
    }
}
