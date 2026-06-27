using System;
using System.IO;
using EsgSignalCreator.Assistant.Api;
using Newtonsoft.Json;

namespace EsgSignalCreator.Assistant.Secrets
{
    /// <summary>
    /// Non-secret assistant preferences (#85). The master <see cref="Enabled"/> switch defaults OFF — the
    /// feature is opt-in. Persisted as plain JSON (no secrets here; the API key lives in <see cref="ApiKeyStore"/>).
    /// </summary>
    public sealed class AssistantSettings
    {
        /// <summary>Master on/off for the assistant feature (default off).</summary>
        public bool Enabled { get; set; }

        /// <summary>Per-session auto-approve-hardware default (still never silences play_rf/connect).</summary>
        public bool AutoApproveHardware { get; set; }

        public string Model { get; set; } = ClaudeModels.DefaultPlanning;
        public string FastModel { get; set; } = ClaudeModels.FastTurns;
    }

    /// <summary>Loads/saves <see cref="AssistantSettings"/> as JSON (path injectable for tests).</summary>
    public sealed class AssistantSettingsStore
    {
        private readonly string _path;

        public AssistantSettingsStore(string filePath = null) => _path = filePath ?? DefaultPath();

        public static string DefaultPath()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ESG-SignalCreator");
            return Path.Combine(dir, "assistant.settings.json");
        }

        public AssistantSettings Load()
        {
            if (!File.Exists(_path)) return new AssistantSettings();
            try { return JsonConvert.DeserializeObject<AssistantSettings>(File.ReadAllText(_path)) ?? new AssistantSettings(); }
            catch { return new AssistantSettings(); }
        }

        public void Save(AssistantSettings settings)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path));
            File.WriteAllText(_path, JsonConvert.SerializeObject(settings ?? new AssistantSettings(), Formatting.Indented));
        }
    }
}
