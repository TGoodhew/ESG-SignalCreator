using System;
using System.Collections.Generic;
using System.Linq;

namespace EsgSignalCreator.Capability
{
    /// <summary>
    /// Reconciles a static capability profile with the <b>live</b> instrument — its <c>*IDN?</c> model,
    /// <c>*OPT?</c> installed options, and queried <c>? MAX/MIN</c> frequency range — so validation and
    /// offline checks reflect the connected unit rather than a fixed configuration (issue #120).
    /// <para>
    /// Safe by construction: unknown or empty inputs fall back to the base profile, so a missing or
    /// quirky <c>*OPT?</c> read never wrongly tightens the memory cap or blocks a capable instrument.
    /// The reconciled memory cap (<see cref="InstrumentProfile.MaxSamples"/>) reflects only the baseband
    /// options actually installed, instead of the largest option the model could carry.
    /// </para>
    /// </summary>
    public static class EffectiveProfile
    {
        /// <summary>
        /// Build the effective profile for a connected unit. <paramref name="installedOptions"/> is the
        /// raw <c>*OPT?</c> list; <paramref name="liveMaxFrequencyHz"/>/<paramref name="liveMinFrequencyHz"/>
        /// are the instrument's queried limits (0 = unknown / not queried).
        /// </summary>
        public static InstrumentProfile Reconcile(
            InstrumentProfile baseProfile,
            string model,
            string[] installedOptions,
            double liveMaxFrequencyHz,
            double liveMinFrequencyHz)
        {
            if (baseProfile == null) return null;

            var opts = new HashSet<string>(
                (installedOptions ?? new string[0])
                    .Where(o => !string.IsNullOrWhiteSpace(o))
                    .Select(o => o.Trim()),
                StringComparer.OrdinalIgnoreCase);

            // Reduce the baseband options to those actually installed, so the memory cap reflects the
            // real card. A profile option name may list slash-separated alternatives (e.g. "001/601"),
            // installed if the unit reports either. Only filter when *OPT? actually reported something —
            // otherwise keep the base set so an empty/failed read doesn't falsely strip capability.
            BasebandOption[] baseband = baseProfile.BasebandOptions;
            if (opts.Count > 0 && baseProfile.BasebandOptions != null)
            {
                baseband = baseProfile.BasebandOptions
                    .Where(o => o.Name != null &&
                                o.Name.Split('/').Any(tok => opts.Contains(tok.Trim())))
                    .ToArray();
            }

            double maxF = baseProfile.MaxFrequencyHz;
            if (liveMaxFrequencyHz > 0)
                maxF = maxF > 0 ? Math.Min(maxF, liveMaxFrequencyHz) : liveMaxFrequencyHz;

            double minF = baseProfile.MinFrequencyHz;
            if (liveMinFrequencyHz > 0)
                minF = minF > 0 ? Math.Max(minF, liveMinFrequencyHz) : liveMinFrequencyHz;

            return new InstrumentProfile
            {
                Model = string.IsNullOrWhiteSpace(model) ? baseProfile.Model : model,
                MinFrequencyHz = minF,
                MaxFrequencyHz = maxF,
                MaxSampleClockHz = baseProfile.MaxSampleClockHz,
                MinSamples = baseProfile.MinSamples,
                BasebandOptions = baseband
            };
        }
    }
}
