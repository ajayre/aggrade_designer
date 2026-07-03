namespace AgGradeDesigner.Core.Import
{
    /// <summary>
    /// Thrown when an AGD import viewer handoff timeline violates the responsiveness contract.
    /// </summary>
    public sealed class ImportViewerHandoffViolationException : Exception
    {
        /// <summary>Contract violations detected in the timeline.</summary>
        public IReadOnlyList<string> Violations { get; }

        /// <summary>
        /// Creates an exception describing one or more handoff contract violations.
        /// </summary>
        public ImportViewerHandoffViolationException
            (
            IReadOnlyList<string> Violations
            )
            : base(BuildMessage(Violations))
        {
            this.Violations = Violations;
        }

        static private string BuildMessage
            (
            IReadOnlyList<string> Violations
            )
        {
            return "AGD import viewer handoff contract violated:" + Environment.NewLine +
                string.Join(Environment.NewLine, Violations.Select(Violation => "- " + Violation));
        }
    }
}
