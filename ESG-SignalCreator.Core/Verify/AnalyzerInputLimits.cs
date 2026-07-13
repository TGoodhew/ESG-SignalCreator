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
        /// N9010A (EXA) default: a <b>conservative backstop</b>. The X-Series RF input maximum is
        /// commonly cited around +30 dBm average, so this is a 5 dB backstop. This value is NOT
        /// confirmed by the supplied X-Series manuals (IQ Analyzer / Programmers / Messages / SA
        /// guides do not state a damage limit) — <b>confirm against the unit's data sheet</b> and
        /// adjust. Erring low protects the front end until then.
        /// </summary>
        public const double N9010AMaxSafeInputDbm = 25.0;

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
        /// value the user should verify against the instrument's specifications.</summary>
        public static bool IsConservativeDefault(VsaModel model) => model == VsaModel.N9010A;
    }
}
