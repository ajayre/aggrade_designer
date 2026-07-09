namespace AgGradeDesigner.Core.Water
{
    /// <summary>
    /// Identifies which elevation field a cached water layer was computed from (WATER-SPEC §4.4, §6.2).
    /// </summary>
    public enum WaterBasisSurfaceKind
    {
        /// <summary>Project existing topology / as-surveyed surface.</summary>
        Existing = 0,

        /// <summary>Project proposed topology / design target surface.</summary>
        Proposed = 1,

        /// <summary>Haul cut/fill leveled surface at a route scrub index (session-only cache).</summary>
        HaulCutFill = 2,
    }
}
