using Newtonsoft.Json.Linq;

namespace EsgSignalCreator.Assistant.Host
{
    /// <summary>
    /// Mutating host surface for the assistant's <see cref="EsgSignalCreator.Assistant.Tools.ToolEffect.Configure"/>
    /// tools (#82): change project/source state and run Calculate — never hardware. The App implements
    /// this against the same Core services + UI commands the user drives. Methods throw on invalid input
    /// (unknown personality, bad file path, …); the dispatcher converts those into is_error tool results.
    /// Each returns a JObject describing the resulting state for Claude.
    /// </summary>
    public interface IAssistantConfigureHost
    {
        /// <summary>Choose the active source personality (CW / Multitone / CustomMod / AWGN / Import-IQ / …).</summary>
        JObject SetSourcePersonality(string name);

        /// <summary>
        /// Apply a personality-specific configuration. <paramref name="personality"/> is one of
        /// multitone / custom_modulation / awgn / cw / import_iq; <paramref name="args"/> are the
        /// tool's validated arguments.
        /// </summary>
        JObject Configure(string personality, JObject args);

        /// <summary>Set a verification pane (top/middle/bottom) to a view (IQ/Spectrum/Constellation/Eye/CCDF).</summary>
        JObject SelectPlotView(string pane, string view);

        /// <summary>Project action: save / load / reset (load/reset must warn on unsaved changes).</summary>
        JObject SetProject(string action, string path);

        /// <summary>Run Calculate (off the UI thread in the host); returns readout + validation summary.</summary>
        JObject CalculateWaveform();
    }
}
