namespace AgGradeDesigner.Core.Geo
{
    /// <summary>
    /// One Web Mercator tile positioned in viewport screen space for compositing.
    /// </summary>
    public sealed class SatelliteTileRequest
    {
        /// <summary>Tile column index at the layout zoom level.</summary>
        public int TileX { get; init; }

        /// <summary>Tile row index at the layout zoom level.</summary>
        public int TileY { get; init; }

        /// <summary>Left edge of the tile in viewport pixels.</summary>
        public float ScreenX { get; init; }

        /// <summary>Top edge of the tile in viewport pixels.</summary>
        public float ScreenY { get; init; }

        /// <summary>Rendered width and height of the tile in viewport pixels.</summary>
        public float ScreenSize { get; init; }
    }
}
