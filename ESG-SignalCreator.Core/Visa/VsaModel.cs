using System;

namespace EsgSignalCreator.Visa
{
    /// <summary>
    /// The analyzer models this application can drive as the measurement peer to the ESG.
    /// Resolved from the <c>*IDN?</c> model field (see <see cref="VsaModels.Detect"/>) and used to
    /// pick the SCPI dialect (<see cref="IVsaDialect"/>) and to guard against driving the wrong box.
    /// </summary>
    public enum VsaModel
    {
        /// <summary>Model not recognized (or not yet identified).</summary>
        Unknown = 0,

        /// <summary>Agilent E4406A VSA Series Transmitter Tester (legacy "Basic mode").</summary>
        E4406A,

        /// <summary>Keysight N9010A (EXA) X-Series signal analyzer.</summary>
        N9010A
    }

    /// <summary>Helpers for resolving a <see cref="VsaModel"/> from an instrument's identity.</summary>
    public static class VsaModels
    {
        /// <summary>
        /// Resolve a <see cref="VsaModel"/> from the model field of a <c>*IDN?</c> response
        /// (e.g. <c>"E4406A"</c> or <c>"N9010A"</c>). Matching is case-insensitive and substring-based
        /// so it tolerates surrounding whitespace or vendor decoration. Unrecognized models return
        /// <see cref="VsaModel.Unknown"/>.
        /// </summary>
        public static VsaModel Detect(string model)
        {
            if (string.IsNullOrWhiteSpace(model)) return VsaModel.Unknown;
            if (Contains(model, "E4406A")) return VsaModel.E4406A;
            if (Contains(model, "N9010A")) return VsaModel.N9010A;
            return VsaModel.Unknown;
        }

        private static bool Contains(string haystack, string needle) =>
            haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
