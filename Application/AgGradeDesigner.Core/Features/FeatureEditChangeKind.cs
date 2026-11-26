namespace AgGradeDesigner.Core.Features
{
    /// <summary>
    /// Why a feature edit session raised <see cref="FeatureEditSession.Changed"/>.
    /// </summary>
    public enum FeatureEditChangeKind
    {
        /// <summary>Features were added, removed, or the mode changed. Lists should be rebuilt.</summary>
        Structure = 0,

        /// <summary>A draft vertex moved, another road vertex was added, or the road cross-section changed. Labels can update without rebuilding the list.</summary>
        Geometry = 1,
    }
}
