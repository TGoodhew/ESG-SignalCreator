namespace EsgSignalCreator.Validation
{
    /// <summary>
    /// One finding produced by <see cref="WaveformValidator"/>: a severity, a human-readable
    /// message, and an optional <see cref="Field"/> reference the UI can use to jump to / highlight
    /// the offending input control.
    /// </summary>
    public sealed class ValidationResult
    {
        public ValidationResult(ValidationSeverity severity, string message, string field = null)
        {
            Severity = severity;
            Message = message ?? string.Empty;
            Field = field;
        }

        /// <summary>How serious this finding is.</summary>
        public ValidationSeverity Severity { get; }

        /// <summary>Human-readable description of the finding.</summary>
        public string Message { get; }

        /// <summary>
        /// Optional reference to the input field this finding relates to (for jump-to / inline
        /// highlighting). Null when the finding is not tied to a specific field.
        /// </summary>
        public string Field { get; }

        public override string ToString()
        {
            return string.IsNullOrEmpty(Field)
                ? Severity + ": " + Message
                : Severity + " [" + Field + "]: " + Message;
        }
    }
}
