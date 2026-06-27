using System.Collections.Generic;
using EsgSignalCreator.Validation;
using Newtonsoft.Json.Linq;

namespace EsgSignalCreator.Assistant.Host
{
    /// <summary>Current app state for <c>get_app_state</c> (#81).</summary>
    public sealed class AppStateSnapshot
    {
        public string PersonalityName { get; set; }
        public bool Connected { get; set; }
        public string InstrumentModel { get; set; }
        public string[] InstrumentOptions { get; set; }
        public string PipelineStage { get; set; }
        public long MemoryUsedSamples { get; set; }
        public long MemoryAvailableSamples { get; set; }
        public string LastError { get; set; }
    }

    /// <summary>Computed results strip for <c>get_results_readout</c> (#81).</summary>
    public sealed class ReadoutSnapshot
    {
        public long SampleCount { get; set; }
        public double SampleRateHz { get; set; }
        public double DurationSeconds { get; set; }
        public double PeakDbfs { get; set; }
        public double RmsDbfs { get; set; }
        public double PaprDb { get; set; }
        public double OccupiedBwHz { get; set; }
        public double DacHeadroomDb { get; set; }
    }

    /// <summary>A source personality and its parameter schema for <c>list_personalities</c> (#81).</summary>
    public sealed class PersonalityInfo
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public JObject Parameters { get; set; }
    }

    /// <summary>
    /// Read-only window into the running app for the assistant's read tools (#81). The host (App)
    /// computes these snapshots from the same Core services the UI uses; tools only format them as JSON.
    /// Read tools are never gated, so this interface exposes no mutating members.
    /// </summary>
    public interface IAssistantReadHost
    {
        AppStateSnapshot GetAppState();
        IReadOnlyList<PersonalityInfo> ListPersonalities();
        JObject GetCurrentConfig();
        IReadOnlyList<ValidationResult> GetValidation();

        /// <summary>The current results readout, or null if nothing has been calculated yet.</summary>
        ReadoutSnapshot GetReadout();
    }
}
