namespace AgGradeDesigner.Core.Rendering
{
    /// <summary>
    /// Outcome of compositing satellite imagery tiles into a single PNG bitmap.
    /// </summary>
    public sealed class SatelliteMapRenderResult
    {
        /// <summary>Total tiles considered for the viewport layout.</summary>
        public int TilesRequested { get; init; }

        /// <summary>Tiles successfully decoded and drawn.</summary>
        public int TilesDrawn { get; init; }

        /// <summary>Tiles replaced with placeholder colour because imagery was unavailable.</summary>
        public int PlaceholdersDrawn { get; init; }

        /// <summary>PNG-encoded composite image, or null when rendering was skipped.</summary>
        public byte[]? PngBytes { get; init; }

        /// <summary>True when at least one tile contributed real imagery pixels.</summary>
        public bool HasImagery => TilesDrawn > 0;
    }
}
