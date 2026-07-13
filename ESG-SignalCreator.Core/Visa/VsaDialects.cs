namespace EsgSignalCreator.Visa
{
    /// <summary>Selects the <see cref="IVsaDialect"/> for a resolved <see cref="VsaModel"/>.</summary>
    public static class VsaDialects
    {
        /// <summary>
        /// Return the dialect for <paramref name="model"/>. An unknown model falls back to the E4406A
        /// dialect so behavior is unchanged until a model is positively identified.
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
    }
}
