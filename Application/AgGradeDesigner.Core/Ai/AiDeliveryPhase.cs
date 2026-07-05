namespace AgGradeDesigner.Core.Ai
{
    /// <summary>
    /// Normative AI feature delivery phase controlling registered surface tools.
    /// </summary>
    public enum AiDeliveryPhase
    {
        /// <summary>Phase 1 — read-only surface tools only.</summary>
        Phase1 = 1,

        /// <summary>Phase 2 — edit pipeline and mutating tools.</summary>
        Phase2 = 2,

        /// <summary>Phase 3 — road tools and extended toolbox.</summary>
        Phase3 = 3
    }
}
