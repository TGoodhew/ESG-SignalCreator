using EsgSignalCreator.Visa;

namespace EsgSignalCreator.Verify
{
    /// <summary>
    /// Per-model default limits for the RF-path input-damage gate (<see cref="PowerSafetyGate"/>).
    /// The maximum safe RF input level differs between analyzers, so the default the connect dialog
    /// seeds into <see cref="RfPathSafety.AnalyzerMaxSafeInputDbm"/> is chosen from the selected model.
    /// The user can always override the value in the dialog.
    /// </summary>
    public static class AnalyzerInputLimits
    {
        /// <summary>
        /// E4406A default: its type-N RF input is rated +35 dBm (CW); the gate defaults to a 5 dB
        /// backstop below that.
        /// </summary>
        public const double E4406AMaxSafeInputDbm = 30.0;

        /// <summary>
        /// N9010A (EXA) maximum safe input: <b>+30 dBm (1 W) average total power</b> (with or without
        /// preamp), per the N9010A EXA X-Series data sheet (5989-6529EN, "Amplitude Accuracy and Range
        /// Specifications → Maximum safe input level"). Data-sheet confirmed on hardware; the gate blocks
        /// anything exceeding this, so it permits up to the rated max. (Peak pulse power tolerates more —
        /// +50 dBm for &lt;10 µs / &lt;1 % duty with ≥30 dB attenuation — but the gate uses the average limit.)
        /// </summary>
        public const double N9010AMaxSafeInputDbm = 30.0;

        /// <summary>The default maximum safe input (dBm) to seed the safety gate for a given model.</summary>
        public static double DefaultMaxSafeInputDbm(VsaModel model)
        {
            switch (model)
            {
                case VsaModel.N9010A: return N9010AMaxSafeInputDbm;
                case VsaModel.E4406A:
                default: return E4406AMaxSafeInputDbm;
            }
        }

        /// <summary>True when the default for <paramref name="model"/> is an unconfirmed, conservative
        /// value the user should verify against the instrument's specifications. Both supported models are
        /// now data-sheet confirmed (E4406A and N9010A both +30 dBm), so this is false; kept as an
        /// extension point for any future model whose damage limit isn't yet confirmed.</summary>
        public static bool IsConservativeDefault(VsaModel model) => false;
    }
}
