namespace AgGradeDesigner.Core.Haul
{
    /// <summary>
    /// One held pivot bearing over a contiguous haul-route range.
    /// </summary>
    public sealed class HaulPivotMoveDto
    {
        /// <summary>1-based schedule index from the haul builder pivot plan.</summary>
        public int Index { get; set; }

        /// <summary>1-based first haul route (pass) that holds this bearing.</summary>
        public int FirstRoute { get; set; }

        /// <summary>1-based last haul route (pass) that holds this bearing.</summary>
        public int LastRoute { get; set; }

        /// <summary>Held compass bearing in degrees (0 = north, 90 = east).</summary>
        public double BearingDeg { get; set; }

        /// <summary>Rotation from the previous held bearing in degrees.</summary>
        public double MoveDeg { get; set; }
    }

    /// <summary>
    /// Serializable center-pivot overlay data for haul 2D scrub display and project persistence.
    /// </summary>
    public sealed class HaulPivotOverlayDto
    {
        /// <summary>Pivot arm radius in metres (largest circle that fits inside the field).</summary>
        public double RadiusM { get; set; }

        /// <summary>
        /// Held compass bearing in degrees for each route-ordered pass (0 = north, 90 = east).
        /// </summary>
        public double[] BearingByPassDeg { get; set; } = Array.Empty<double>();

        /// <summary>
        /// Contiguous held-bearing positions with the haul route numbers where the pivot moves.
        /// </summary>
        public List<HaulPivotMoveDto> Moves { get; set; } = new List<HaulPivotMoveDto>();
    }
}
