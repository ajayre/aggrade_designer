namespace AgGradeDesigner.Core.Export
{
    /// <summary>
    /// Result of pre-export validation for an <c>.agcontrol</c> write (EXPORT-SPEC Phase 2).
    /// </summary>
    public sealed class AgControlExportValidationResult
    {
        /// <summary>
        /// Creates a validation result with blocking errors and optional warnings.
        /// </summary>
        /// <param name="Errors">Blocking error messages; empty when valid.</param>
        /// <param name="Warnings">Non-blocking warnings shown to the user.</param>
        public AgControlExportValidationResult
            (
            IReadOnlyList<string> Errors,
            IReadOnlyList<string> Warnings
            )
        {
            this.Errors = Errors ?? Array.Empty<string>();
            this.Warnings = Warnings ?? Array.Empty<string>();
        }

        /// <summary>True when export may proceed (no blocking errors).</summary>
        public bool IsValid => Errors.Count == 0;

        /// <summary>Blocking validation failures.</summary>
        public IReadOnlyList<string> Errors { get; }

        /// <summary>Non-blocking advisories (for example missing optional haul data).</summary>
        public IReadOnlyList<string> Warnings { get; }

        /// <summary>
        /// Formats errors into a single user-facing message.
        /// </summary>
        /// <returns>Joined error text, or an empty string when valid.</returns>
        public string FormatErrors
            (
            )
        {
            if (Errors.Count == 0)
                return string.Empty;

            return string.Join(Environment.NewLine, Errors);
        }

        /// <summary>
        /// Formats warnings into a single user-facing message.
        /// </summary>
        /// <returns>Joined warning text, or an empty string when none.</returns>
        public string FormatWarnings
            (
            )
        {
            if (Warnings.Count == 0)
                return string.Empty;

            return string.Join(Environment.NewLine, Warnings);
        }
    }
}
