using Newtonsoft.Json.Linq;

namespace EsgSignalCreator.Assistant.Host
{
    /// <summary>
    /// Analyzer measurement + verification host for the assistant's measure_* / verify_signal tools
    /// (#90, §8/§9) — works with the connected analyzer (E4406A or N9010A). The App implements it
    /// against the connected <c>VsaInstrument</c> and the same Core
    /// Measure/* + VerificationHarness the in-app Verify uses. Measurements only READ the analyzer
    /// (no RF is emitted), so the tools are not confirmation-gated; methods throw if the VSA (or, for
    /// verify, a calculated waveform) is missing. Each returns a JObject of results.
    /// </summary>
    public interface IAssistantMeasureHost
    {
        JObject GetVsaState();
        JObject MeasureChannelPower(double centerHz, double spanHz);
        JObject MeasureAcp(double centerHz, double carrierBandwidthHz);
        JObject MeasureCcdf(double centerHz);
        JObject MeasureSpectrumPeak(double centerHz, double spanHz);
        JObject MeasureWaveform(double centerHz);

        /// <summary>Compare the played signal to expectations. Null params fall back to the ESG's commanded settings.</summary>
        JObject VerifySignal(double? carrierHz, double? commandedPowerDbm, double? toneOffsetHz);
    }
}
