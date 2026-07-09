namespace AgGradeDesigner.Core.Water
{
    /// <summary>
    /// Stored surface water layer result bound to one basis surface and the settings used to compute it (WATER-SPEC §4.5, §6.3.2).
    /// </summary>
    public sealed class WaterLayerCacheEntry
    {
        /// <summary>Basis surface this cache entry was computed from.</summary>
        public WaterBasisSurfaceKind BasisSurface { get; set; }

        /// <summary>Route scrub index when <see cref="BasisSurface"/> is <see cref="WaterBasisSurfaceKind.HaulCutFill"/>.</summary>
        public int? RouteScrubIndex { get; set; }

        /// <summary>Ponding rainfall in millimetres used when this cache was produced.</summary>
        public uint PondingRainfallMm { get; set; }

        /// <summary>Ponding curve number used when this cache was produced.</summary>
        public uint PondingCurveNumber { get; set; }

        /// <summary>Grid column count for pond depth samples.</summary>
        public int GridWidth { get; set; }

        /// <summary>Grid row count for pond depth samples.</summary>
        public int GridHeight { get; set; }

        /// <summary>Cell size in metres for the pond depth grid.</summary>
        public double CellSizeM { get; set; }

        /// <summary>Southern edge latitude of the pond grid in decimal degrees.</summary>
        public double MinLat { get; set; }

        /// <summary>Northern edge latitude of the pond grid in decimal degrees.</summary>
        public double MaxLat { get; set; }

        /// <summary>Western edge longitude of the pond grid in decimal degrees.</summary>
        public double MinLon { get; set; }

        /// <summary>Eastern edge longitude of the pond grid in decimal degrees.</summary>
        public double MaxLon { get; set; }

        /// <summary>Row-major pond depth samples in metres (NaN for dry or invalid cells).</summary>
        public float[] PondDepthM { get; set; } = Array.Empty<float>();

        /// <summary>Row-major RGBA overlay bytes (four bytes per cell) for map compositing.</summary>
        public byte[] OverlayRgba { get; set; } = Array.Empty<byte>();

        /// <summary>Opaque binary payload persisted in project files (Appendix A).</summary>
        public byte[] PayloadBlob { get; set; } = Array.Empty<byte>();

        /// <summary>
        /// Returns true when the cache contains a complete grid and overlay suitable for display.
        /// </summary>
        /// <returns>True when dimensions and buffers are consistent.</returns>
        public bool IsValidForDisplay
            (
            )
        {
            int ExpectedCells = GridWidth * GridHeight;
            if ((GridWidth <= 0) || (GridHeight <= 0) || (ExpectedCells <= 0))
                return false;

            if ((PondDepthM == null) || (PondDepthM.Length != ExpectedCells))
                return false;

            if ((OverlayRgba == null) || (OverlayRgba.Length != ExpectedCells * 4))
                return false;

            return true;
        }

        /// <summary>
        /// Returns true when the cache settings match the supplied project water settings.
        /// </summary>
        /// <param name="Settings">Active project water settings.</param>
        /// <returns>True when rainfall and curve number match.</returns>
        public bool MatchesSettings
            (
            WaterSettings Settings
            )
        {
            return (PondingRainfallMm == Settings.PondingRainfallMm) &&
                (PondingCurveNumber == Settings.PondingCurveNumber);
        }
    }
}
