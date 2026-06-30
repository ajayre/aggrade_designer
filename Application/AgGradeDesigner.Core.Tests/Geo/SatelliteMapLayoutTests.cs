using AgGradeDesigner.Core.Geo;

namespace AgGradeDesigner.Core.Tests.Geo
{
    /// <summary>
    /// Unit tests for <see cref="SatelliteMapLayout"/> tile selection and screen placement.
    /// </summary>
    public sealed class SatelliteMapLayoutTests
    {
        /// <summary>
        /// Verifies the default view centre requests tiles near a known Esri tile index.
        /// </summary>
        [Fact]
        public void Compute_DefaultViewCentre_RequestsTilesNearKnownEsriTile
            (
            )
        {
            double CentreLat = 36.0;
            double CentreLon = -90.0;
            double MetresPerPixel = SatelliteMapLayout.EstimateMetresPerPixel(CentreLat, 1.0);

            SatelliteMapViewport Viewport = SatelliteMapLayout.Compute(
                CentreLat,
                CentreLon,
                800,
                600,
                MetresPerPixel);

            // Default centre should resolve to zoom 10 with the reference tile in the set.
            Assert.Equal(10, Viewport.Zoom);
            Assert.NotEmpty(Viewport.Tiles);
            Assert.Contains(Viewport.Tiles, Tile => (Tile.TileX == 256) && (Tile.TileY == 402));
        }

        /// <summary>
        /// Verifies computed tile screen rectangles overlap the viewport bounds.
        /// </summary>
        [Fact]
        public void Compute_TileScreenRects_CoverViewport
            (
            )
        {
            double CentreLat = 36.0;
            double CentreLon = -90.0;
            double MetresPerPixel = SatelliteMapLayout.EstimateMetresPerPixel(CentreLat, 1.0);

            SatelliteMapViewport Viewport = SatelliteMapLayout.Compute(
                CentreLat,
                CentreLon,
                800,
                600,
                MetresPerPixel);

            foreach (SatelliteTileRequest Tile in Viewport.Tiles)
            {
                // Each tile must intersect the viewport, allowing partial off-screen overlap.
                Assert.True(Tile.ScreenSize > 0);
                Assert.True(Tile.ScreenX + Tile.ScreenSize >= -Tile.ScreenSize);
                Assert.True(Tile.ScreenY + Tile.ScreenSize >= -Tile.ScreenSize);
                Assert.True(Tile.ScreenX <= 800 + Tile.ScreenSize);
                Assert.True(Tile.ScreenY <= 600 + Tile.ScreenSize);
            }
        }
    }
}
