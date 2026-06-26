using System;
using System.Globalization;

namespace EsgSignalCreator.Verify
{
    /// <summary>
    /// Thrown when an ESG output power command would exceed the analyzer's safe RF input level.
    /// </summary>
    [Serializable]
    public class RfSafetyException : Exception
    {
        public RfSafetyException() { }
        public RfSafetyException(string message) : base(message) { }
        public RfSafetyException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Core enforcement of the input-damage safety gate (GitHub issue #64).
    /// </summary>
    /// <remarks>
    /// Every path that commands ESG output power — the manual UI today and the Claude assistant
    /// in future — must route through this gate so an armed analyzer link is always protected.
    /// </remarks>
    public static class PowerSafetyGate
    {
        /// <summary>
        /// Predicted RF power arriving at the analyzer input, in dBm, for the given ESG output
        /// power: the ESG power reduced by the inline path loss.
        /// </summary>
        public static double AnalyzerInputDbm(double esgPowerDbm, RfPathSafety cfg)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            return esgPowerDbm - cfg.PathLossDb;
        }

        /// <summary>
        /// Determines whether commanding <paramref name="esgPowerDbm"/> is safe for the analyzer.
        /// When the link is not armed, every power is considered safe. When armed, it is unsafe
        /// if the predicted analyzer input exceeds <see cref="RfPathSafety.AnalyzerMaxSafeInputDbm"/>.
        /// </summary>
        /// <param name="reason">
        /// A human-readable explanation naming the predicted analyzer input, the limit, and the
        /// path loss when unsafe; an empty string when safe.
        /// </param>
        public static bool IsSafe(double esgPowerDbm, RfPathSafety cfg, out string reason)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));

            if (!cfg.Armed)
            {
                reason = string.Empty;
                return true;
            }

            double analyzerInputDbm = AnalyzerInputDbm(esgPowerDbm, cfg);
            if (analyzerInputDbm > cfg.AnalyzerMaxSafeInputDbm)
            {
                reason = string.Format(
                    CultureInfo.InvariantCulture,
                    "RF safety: commanding {0:0.###} dBm would put {1:0.###} dBm at the analyzer input " +
                    "(path loss {2:0.###} dB), exceeding the safe limit of {3:0.###} dBm.",
                    esgPowerDbm, analyzerInputDbm, cfg.PathLossDb, cfg.AnalyzerMaxSafeInputDbm);
                return false;
            }

            reason = string.Empty;
            return true;
        }

        /// <summary>
        /// Throws <see cref="RfSafetyException"/> with the explanatory reason when commanding
        /// <paramref name="esgPowerDbm"/> would be unsafe; no-op when safe.
        /// </summary>
        public static void Guard(double esgPowerDbm, RfPathSafety cfg)
        {
            string reason;
            if (!IsSafe(esgPowerDbm, cfg, out reason))
            {
                throw new RfSafetyException(reason);
            }
        }
    }
}
