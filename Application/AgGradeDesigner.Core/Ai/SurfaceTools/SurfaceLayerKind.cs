namespace AgGradeDesigner.Core.Ai.SurfaceTools
{
    /// <summary>
    /// Permitted surface layer identifiers for read-only and mutating tools.
    /// </summary>
    public enum SurfaceLayerKind
    {
        /// <summary>Existing ground elevations (<see cref="Models.GridBinData.ExistingM"/>).</summary>
        Existing,

        /// <summary>Proposed design elevations (<see cref="Models.GridBinData.ProposedM"/>).</summary>
        Proposed
    }
}
