using Newtonsoft.Json.Linq;

namespace EsgSignalCreator.Assistant.Host
{
    /// <summary>
    /// Host for the gated raw-SCPI passthrough (#88, §6.4). The App implements it against the open
    /// instrument session: send the literal command (query → read the response), then read
    /// <c>:SYSTem:ERRor?</c>, and log both. Throws if nothing is connected.
    /// </summary>
    public interface IAssistantRawScpiHost
    {
        JObject SendRawScpi(string command);
    }
}
