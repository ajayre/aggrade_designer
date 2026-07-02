using AgGradeDesigner.Core.Geo;

namespace AgGradeDesigner.Core.Tests.Geo
{
    /// <summary>
    /// Unit tests for <see cref="SatelliteMapLayout"/> tile selection and screen placement.
    /// </summary>
    public sealed class SatelliteMapLayoutTests
    {
        /// <summary>
        /// Default centre lat used by this component.
        /// </summary>

        private const double DEFAULT_CENTRE_LAT = 36.0;
        /// <summary>
        /// Default centre lon used by this component.
        /// </summary>

        private const double DEFAULT_CENTRE_LON = -90.0;
        /// <summary>
        /// Zoom factor used by this component.
        /// </summary>

        private const double ZOOM_FACTOR = 1.0;
        /// <summary>
        /// Viewport width used by this component.
        /// </summary>

        private const int VIEWPORT_WIDTH = 800;
        /// <summary>
        /// Viewport height used by this component.
        /// </summary>

        private const int VIEWPORT_HEIGHT = 600;
        /// <summary>
        /// Expected value used when asserting zoom.
        /// </summary>

        private const int EXPECTED_ZOOM = 10;
        /// <summary>
        /// Reference tile x used by this component.
        /// </summary>

        private const int REFERENCE_TILE_X = 256;
        /// <summary>
        /// Reference tile y used by this component.
        /// </summary>

        private const int REFERENCE_TILE_Y = 402;

        /// <summary>
        /// Verifies the default view centre requests tiles near a known Esri tile index.
        /// </summary>
        [Fact]
        public void Compute_DefaultViewCentre_RequestsTilesNearKnownEsriTile
            (
            )
        {
            double MetresPerPixel = SatelliteMapLayout.EstimateMetresPerPixel(DEFAULT_CENTRE_LAT, ZOOM_FACTOR);

            SatelliteMapViewport Viewport = SatelliteMapLayout.Compute(
                DEFAULT_CENTRE_LAT,
                DEFAULT_CENTRE_LON,
                VIEWPORT_WIDTH,
                VIEWPORT_HEIGHT,
                MetresPerPixel);

            // Default centre should resolve to zoom 10 with the reference tile in the set.
            Assert.Equal(EXPECTED_ZOOM, Viewport.Zoom);
            Assert.NotEmpty(Viewport.Tiles);
            Assert.Contains(Viewport.Tiles, Tile => (Tile.TileX == REFERENCE_TILE_X) && (Tile.TileY == REFERENCE_TILE_Y));
        }

        /// <summary>
        /// Verifies computed tile screen rectangles overlap the viewport bounds.
        /// </summary>
        [Fact]
        public void Compute_TileScreenRects_CoverViewport
            (
            )
        {
            double MetresPerPixel = SatelliteMapLayout.EstimateMetresPerPixel(DEFAULT_CENTRE_LAT, ZOOM_FACTOR);

            SatelliteMapViewport Viewport = SatelliteMapLayout.Compute(
                DEFAULT_CENTRE_LAT,
                DEFAULT_CENTRE_LON,
                VIEWPORT_WIDTH,
                VIEWPORT_HEIGHT,
                MetresPerPixel);

            foreach (SatelliteTileRequest Tile in Viewport.Tiles)
            {
                // Each tile must intersect the viewport, allowing partial off-screen overlap.
                Assert.True(Tile.ScreenSize > 0);
                Assert.True(Tile.ScreenX + Tile.ScreenSize >= -Tile.ScreenSize);
                Assert.True(Tile.ScreenY + Tile.ScreenSize >= -Tile.ScreenSize);
                Assert.True(Tile.ScreenX <= VIEWPORT_WIDTH + Tile.ScreenSize);
                Assert.True(Tile.ScreenY <= VIEWPORT_HEIGHT + Tile.ScreenSize);
            }
        }
    }
}
