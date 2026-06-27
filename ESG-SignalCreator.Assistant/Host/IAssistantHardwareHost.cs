using Newtonsoft.Json.Linq;

namespace EsgSignalCreator.Assistant.Host
{
    /// <summary>
    /// Hardware host surface for the assistant's <see cref="EsgSignalCreator.Assistant.Tools.ToolEffect.Hardware"/>
    /// tools (#86). The App implements this against the same instrument operations the toolbar buttons use.
    /// Every method here touches the instrument and is therefore only reached after the dispatcher has
    /// applied the confirmation policy (§6.1) and the pre-execution validation gate (§6.3). Methods throw
    /// on failure (not connected, unsafe power, no waveform, …); the dispatcher turns those into is_error
    /// results. Each returns a JObject describing the outcome.
    /// </summary>
    public interface IAssistantHardwareHost
    {
        /// <summary>Open a VISA session to <paramref name="resource"/> and identify the instrument.</summary>
        JObject ConnectInstrument(string resource);

        /// <summary>Close the open instrument session.</summary>
        JObject DisconnectInstrument();

        /// <summary>Push the current ARB waveform to the instrument (requires a calculated waveform + connection).</summary>
        JObject DownloadWaveform();

        /// <summary>Arm the ARB and turn RF on.</summary>
        JObject PlayRf();

        /// <summary>Stop the ARB and turn RF off.</summary>
        JObject StopRf();

        /// <summary>
        /// Apply instrument settings (frequency/power/RF/modulation/sample clock/runtime scaling/reference).
        /// Commanding power is checked against the analyzer input-damage gate (§6) before it is applied.
        /// </summary>
        JObject SetInstrumentSettings(JObject args);
    }
}
