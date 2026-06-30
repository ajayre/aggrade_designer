using AgGradeDesigner.Core.Geo;

namespace AgGradeDesigner.Core.Tests.Geo
{
    /// <summary>
    /// Unit tests for <see cref="WebMercator"/> tile math and URL construction.
    /// </summary>
    public sealed class WebMercatorTests
    {
        /// <summary>
        /// Verifies the default view centre maps to in-bounds world pixel coordinates.
        /// </summary>
        [Fact]
        public void LatLonToWorldPixel_DefaultViewCentre_IsInsideWorld
            (
            )
        {
            WebMercator.LatLonToWorldPixel(-90.0, 36.0, 10, out double Wx, out double Wy);
            double WorldSize = WebMercator.WorldSizePixels(10);
            // Default centre must land inside the zoom-10 world pixel extent.
            Assert.InRange(Wx, 0, WorldSize);
            Assert.InRange(Wy, 0, WorldSize);
        }

        /// <summary>
        /// Verifies world pixel to lat/lon round-trips the default view centre.
        /// </summary>
        [Fact]
        public void WorldPixelToLatLon_RoundTripsDefaultViewCentre
            (
            )
        {
            WebMercator.LatLonToWorldPixel(-90.0, 36.0, 10, out double Wx, out double Wy);
            WebMercator.WorldPixelToLatLon(Wx, Wy, 10, out double Lon, out double Lat);
            // Inverse projection should recover the reference camera location.
            Assert.Equal(-90.0, Lon, 4);
            Assert.Equal(36.0, Lat, 4);
        }

        /// <summary>
        /// Verifies zoom level selection returns a reasonable level for field-scale metres per pixel.
        /// </summary>
        [Fact]
        public void PickZoomLevel_ReturnsReasonableLevelForFieldScale
            (
            )
        {
            int Zoom = WebMercator.PickZoomLevel(140.0, 36.0);
            // Field-scale viewport should pick a mid zoom band suitable for imagery tiles.
            Assert.InRange(Zoom, 8, 12);
        }

        /// <summary>
        /// Verifies Esri World Imagery tile URLs follow the expected path pattern.
        /// </summary>
        [Fact]
        public void BuildEsriWorldImageryTileUrl_MatchesExpectedPattern
            (
            )
        {
            string Url = WebMercator.BuildEsriWorldImageryTileUrl(10, 256, 402);
            // Tile coordinates must appear in Esri y/x order within the REST tile path.
            Assert.Contains("/tile/10/402/256", Url);
            Assert.StartsWith("https://services.arcgisonline.com/", Url);
        }
    }
}
