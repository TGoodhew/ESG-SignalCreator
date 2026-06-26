namespace EsgSignalCreator.Validation
{
    /// <summary>Severity of a single <see cref="ValidationResult"/>, in increasing order of concern.</summary>
    public enum ValidationSeverity
    {
        /// <summary>Purely informational; no action required.</summary>
        Info,

        /// <summary>The waveform will play, but the result may not be what the user intended.</summary>
        Warning,

        /// <summary>The waveform cannot be downloaded / played as-is; the user must fix it.</summary>
        Error
    }
}
