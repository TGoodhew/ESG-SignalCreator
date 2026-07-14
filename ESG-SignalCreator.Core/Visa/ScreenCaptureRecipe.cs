namespace EsgSignalCreator.Visa
{
    /// <summary>
    /// The per-model SCPI recipe for capturing the analyzer's display as an image (issue #143).
    /// Most analyzers save the screen to an instrument-side file, then transfer it back as an
    /// IEEE-488.2 definite-length block; some can dump it directly. This captures that recipe so
    /// <see cref="VsaInstrument.CaptureScreen"/> stays model-agnostic, and every field is overridable
    /// from the tool so the exact commands can be tuned at the bench without a rebuild.
    /// </summary>
    public sealed class ScreenCaptureRecipe
    {
        /// <summary>
        /// Build a recipe. <paramref name="dataQueryFormat"/> is required; the save/cleanup formats are
        /// optional (a direct-dump instrument leaves the save null). Each <c>*Format</c> takes the
        /// instrument-side <paramref name="tempPath"/> as <c>{0}</c>.
        /// </summary>
        public ScreenCaptureRecipe(
            string dataQueryFormat,
            string saveCommandFormat = null,
            string cleanupCommandFormat = null,
            string tempPath = null,
            bool opcAfterSave = true)
        {
            DataQueryFormat = dataQueryFormat;
            SaveCommandFormat = saveCommandFormat;
            CleanupCommandFormat = cleanupCommandFormat;
            TempPath = tempPath;
            OpcAfterSave = opcAfterSave;
        }

        /// <summary>Command that writes the screen to <see cref="TempPath"/> (<c>{0}</c>); null for a direct dump.</summary>
        public string SaveCommandFormat { get; }

        /// <summary>Query that returns the image as an IEEE-488.2 block (<c>{0}</c> = <see cref="TempPath"/>).</summary>
        public string DataQueryFormat { get; }

        /// <summary>Command that deletes the instrument-side file afterwards (<c>{0}</c>); null to skip.</summary>
        public string CleanupCommandFormat { get; }

        /// <summary>Instrument-side path used by the save/data/cleanup commands.</summary>
        public string TempPath { get; }

        /// <summary>Whether to poll <c>*OPC?</c> after the save so the file is fully written before read-back.</summary>
        public bool OpcAfterSave { get; }

        /// <summary>Return a copy with any non-null override applied (used to accept tool/CLI overrides).</summary>
        public ScreenCaptureRecipe With(
            string dataQueryFormat = null,
            string saveCommandFormat = null,
            string cleanupCommandFormat = null,
            string tempPath = null)
        {
            return new ScreenCaptureRecipe(
                dataQueryFormat ?? DataQueryFormat,
                saveCommandFormat ?? SaveCommandFormat,
                cleanupCommandFormat ?? CleanupCommandFormat,
                tempPath ?? TempPath,
                OpcAfterSave);
        }
    }
}
