using System;
using EsgSignalCreator.Visa;

namespace EsgSignalCreator.Instruments
{
    /// <summary>Which 10 MHz timebase an instrument is using.</summary>
    public enum ReferenceSource
    {
        Internal,
        External
    }

    /// <summary>How the ESG and E4406A timebases are coordinated (#75).</summary>
    public enum ReferenceScheme
    {
        /// <summary>Each instrument runs on its own internal timebase (small frequency offset between them).</summary>
        Independent,

        /// <summary>
        /// Both instruments are disciplined to an external 10 MHz (a house reference, or the ESG's
        /// 10 MHz OUT cabled to the analyzer's EXT REF IN) for clean frequency comparisons.
        /// </summary>
        CommonExternal
    }

    /// <summary>The reference source each instrument reports, and whether they are commonly locked.</summary>
    public sealed class ReferenceStatus
    {
        public ReferenceStatus(ReferenceSource esg, ReferenceSource vsa)
        {
            Esg = esg;
            Vsa = vsa;
        }

        public ReferenceSource Esg { get; }
        public ReferenceSource Vsa { get; }

        /// <summary>True when both instruments are on an external reference (a common timebase).</summary>
        public bool Locked => Esg == ReferenceSource.External && Vsa == ReferenceSource.External;

        public override string ToString() =>
            "ESG=" + Esg + ", VSA=" + Vsa + (Locked ? " (locked to common 10 MHz)" : "");
    }

    /// <summary>Maps <see cref="ReferenceSource"/> to/from the instruments' SCPI INT/EXT tokens.</summary>
    public static class ReferenceSourceText
    {
        public static string Scpi(ReferenceSource source) =>
            source == ReferenceSource.External ? "EXT" : "INT";

        public static ReferenceSource Parse(string response)
        {
            string s = (response ?? string.Empty).Trim().Trim('"').ToUpperInvariant();
            return s.StartsWith("EXT") ? ReferenceSource.External : ReferenceSource.Internal;
        }
    }

    /// <summary>
    /// Coordinates the ESG and E4406A 10 MHz references (#75). The ESG auto-detects a valid external
    /// 10 MHz at its REF IN, so we just enable auto-selection on it and switch the analyzer between
    /// internal and external. Exact <c>ROSCillator</c> handling should be confirmed on the bench unit
    /// (requirements §10); the SCPI emitted here is the standard form.
    /// </summary>
    public static class ReferenceLock
    {
        public static void Apply(EsgController esg, VsaInstrument vsa, ReferenceScheme scheme)
        {
            if (esg == null) throw new ArgumentNullException(nameof(esg));
            if (vsa == null) throw new ArgumentNullException(nameof(vsa));

            // The ESG auto-locks to an external 10 MHz when present; leave auto on either way.
            esg.SetReferenceAuto(true);
            vsa.SetReferenceSource(scheme == ReferenceScheme.CommonExternal
                ? ReferenceSource.External
                : ReferenceSource.Internal);
        }

        public static ReferenceStatus Read(EsgController esg, VsaInstrument vsa)
        {
            if (esg == null) throw new ArgumentNullException(nameof(esg));
            if (vsa == null) throw new ArgumentNullException(nameof(vsa));
            return new ReferenceStatus(esg.GetReferenceSource(), vsa.GetReferenceSource());
        }
    }
}
