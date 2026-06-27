using System;
using System.Collections.Generic;

namespace EsgSignalCreator.Instruments
{
    /// <summary>
    /// One E4406A measurement mode (#76). The <see cref="Mnemonic"/> is what goes to
    /// <c>:INSTrument:SELect</c>; <see cref="Number"/> is the <c>:INSTrument:NSELect</c> index from the
    /// Programmer's Guide. <see cref="IsStandardPersonality"/> is false for the always-present base
    /// modes (Basic, Service) and true for the option-gated communications-standard personalities.
    /// </summary>
    public sealed class InstrumentMode
    {
        public InstrumentMode(string mnemonic, int number, string displayName, bool isStandardPersonality, string description)
        {
            Mnemonic = mnemonic;
            Number = number;
            DisplayName = displayName;
            IsStandardPersonality = isStandardPersonality;
            Description = description;
        }

        public string Mnemonic { get; }
        public int Number { get; }
        public string DisplayName { get; }
        public bool IsStandardPersonality { get; }
        public string Description { get; }

        public override string ToString() => DisplayName + " (" + Mnemonic + ")";
    }

    /// <summary>
    /// The E4406A measurement modes from the Programmer's Guide INSTrument subsystem, and helpers to
    /// resolve which are actually installed from a live <c>:INSTrument:CATalog?</c> response. Gating off
    /// the catalog is more robust than decoding <c>*OPT?</c> codes (requirements §2, §10).
    /// </summary>
    public static class InstrumentModeCatalog
    {
        private static readonly InstrumentMode[] Known =
        {
            new InstrumentMode("BASIC",     8,  "Basic",            false, "Format-independent measurements (always present)"),
            new InstrumentMode("SERVICE",   1,  "Service",          false, "Service / diagnostics"),
            new InstrumentMode("GSM",       3,  "GSM",              true,  "GSM measurement personality"),
            new InstrumentMode("EDGEGSM",   13, "GSM/EDGE",         true,  "GSM with EDGE measurement personality"),
            new InstrumentMode("CDMA",      4,  "cdmaOne",          true,  "cdmaOne (IS-95) measurement personality"),
            new InstrumentMode("CDMA2K",    10, "cdma2000",         true,  "cdma2000 (with 1xEV-DV) measurement personality"),
            new InstrumentMode("CMDA1XEV",  15, "1xEV-DO",          true,  "1xEV-DO measurement personality"),
            new InstrumentMode("WCDMA",     9,  "W-CDMA (3GPP)",    true,  "3GPP W-CDMA (HSDPA/HSUPA) measurement personality"),
            new InstrumentMode("ARIBWCDMA", 0,  "ARIB W-CDMA",     true,  "ARIB W-CDMA measurement personality"),
            new InstrumentMode("NADC",      5,  "NADC",            true,  "NADC measurement personality"),
            new InstrumentMode("PDC",       6,  "PDC",             true,  "PDC measurement personality"),
            new InstrumentMode("IDEN",      11, "iDEN",            true,  "iDEN measurement personality"),
        };

        /// <summary>All modes the firmware knows about (whether or not licensed on a given unit).</summary>
        public static IReadOnlyList<InstrumentMode> All => Known;

        /// <summary>Look up a known mode by its SCPI mnemonic (case-insensitive); null if unknown.</summary>
        public static InstrumentMode ByMnemonic(string mnemonic)
        {
            if (string.IsNullOrEmpty(mnemonic)) return null;
            string m = mnemonic.Trim().Trim('"');
            foreach (InstrumentMode mode in Known)
                if (string.Equals(mode.Mnemonic, m, StringComparison.OrdinalIgnoreCase)) return mode;
            return null;
        }

        /// <summary>
        /// Resolve the modes a unit actually has from its <c>:INSTrument:CATalog?</c> names. Recognised
        /// mnemonics map to their descriptor; an unrecognised name is passed through as a standard
        /// personality so a newer firmware's mode is still selectable.
        /// </summary>
        public static IReadOnlyList<InstrumentMode> Resolve(IEnumerable<string> catalogNames)
        {
            var list = new List<InstrumentMode>();
            if (catalogNames == null) return list;

            foreach (string raw in catalogNames)
            {
                string name = (raw ?? string.Empty).Trim().Trim('"');
                if (name.Length == 0) continue;
                InstrumentMode known = ByMnemonic(name);
                list.Add(known ?? new InstrumentMode(name, 0, name, true, "Installed mode reported by the analyzer"));
            }
            return list;
        }

        /// <summary>The installed option-gated communications-standard personalities (excludes Basic/Service).</summary>
        public static IReadOnlyList<InstrumentMode> StandardPersonalities(IEnumerable<string> catalogNames)
        {
            var list = new List<InstrumentMode>();
            foreach (InstrumentMode mode in Resolve(catalogNames))
                if (mode.IsStandardPersonality) list.Add(mode);
            return list;
        }
    }
}
