namespace EsgSignalCreator.Visa
{
    /// <summary>Selects the <see cref="IVsaDialect"/> for a resolved <see cref="VsaModel"/>.</summary>
    public static class VsaDialects
    {
        /// <summary>
        /// Return the dialect for <paramref name="model"/>. Unknown/unsupported analyzers are <b>rejected at
        /// connect</b> (§120): <see cref="IsSupported"/> gates the connect path so an unidentified X-Series
        /// unit is refused rather than driven with a wrong-dialect guess. This resolver keeps the E4406A
        /// (Basic-mode) mnemonics as the default for an unresolved model so measurement code has a safe
        /// baseline, but that default is never reached for a live instrument that failed the connect gate.
        /// </summary>
        public static IVsaDialect For(VsaModel model)
        {
            switch (model)
            {
                case VsaModel.N9010A: return new N9010ADialect();
                case VsaModel.E4406A:
                case VsaModel.Unknown:
                default:
                    return new E4406ADialect();
            }
        }

        /// <summary>
        /// True if <paramref name="model"/> is an analyzer the app can drive (E4406A or N9010A). The VSA
        /// connect path refuses anything else <b>explicitly</b> rather than falling back to a guessed
        /// dialect (§120). <see cref="VsaModel.Unknown"/> — an <c>*IDN?</c> that matched no supported
        /// model — is unsupported.
        /// </summary>
        public static bool IsSupported(VsaModel model) => model == VsaModel.E4406A || model == VsaModel.N9010A;
    }
}
