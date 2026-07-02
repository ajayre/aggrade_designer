using AgGradeDesigner.Core.Geo;

namespace AgGradeDesigner.Core.Tests.Geo
{
    /// <summary>
    /// Unit tests for <see cref="WebMercator"/> tile math and URL construction.
    /// </summary>
    public sealed class WebMercatorTests
    {
        /// <summary>
        /// Default centre lon used by this component.
        /// </summary>

        private const double DEFAULT_CENTRE_LON = -90.0;
        /// <summary>
        /// Default centre lat used by this component.
        /// </summary>

        private const double DEFAULT_CENTRE_LAT = 36.0;
        /// <summary>
        /// Default zoom used by this component.
        /// </summary>

        private const int DEFAULT_ZOOM = 10;
        /// <summary>
        /// Tile x used by this component.
        /// </summary>

        private const int TILE_X = 256;
        /// <summary>
        /// Tile y used by this component.
        /// </summary>

        private const int TILE_Y = 402;
        /// <summary>
        /// Field scale metres per pixel used by this component.
        /// </summary>

        private const double FIELD_SCALE_METRES_PER_PIXEL = 140.0;
        /// <summary>
        /// Min field zoom used by this component.
        /// </summary>

        private const int MIN_FIELD_ZOOM = 8;
        /// <summary>
        /// Max field zoom used by this component.
        /// </summary>

        private const int MAX_FIELD_ZOOM = 12;
        /// <summary>
        /// Coord precision used by this component.
        /// </summary>

        private const int COORD_PRECISION = 4;

        /// <summary>
        /// Verifies the default view centre maps to in-bounds world pixel coordinates.
        /// </summary>
        [Fact]
        public void LatLonToWorldPixel_DefaultViewCentre_IsInsideWorld
            (
            )
        {
            WebMercator.LatLonToWorldPixel(DEFAULT_CENTRE_LON, DEFAULT_CENTRE_LAT, DEFAULT_ZOOM, out double Wx, out double Wy);
            double WorldSize = WebMercator.WorldSizePixels(DEFAULT_ZOOM);
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
            WebMercator.LatLonToWorldPixel(DEFAULT_CENTRE_LON, DEFAULT_CENTRE_LAT, DEFAULT_ZOOM, out double Wx, out double Wy);
            WebMercator.WorldPixelToLatLon(Wx, Wy, DEFAULT_ZOOM, out double Lon, out double Lat);
            // Inverse projection should recover the reference camera location.
            Assert.Equal(DEFAULT_CENTRE_LON, Lon, COORD_PRECISION);
            Assert.Equal(DEFAULT_CENTRE_LAT, Lat, COORD_PRECISION);
        }

        /// <summary>
        /// Verifies zoom level selection returns a reasonable level for field-scale metres per pixel.
        /// </summary>
        [Fact]
        public void PickZoomLevel_ReturnsReasonableLevelForFieldScale
            (
            )
        {
            int Zoom = WebMercator.PickZoomLevel(FIELD_SCALE_METRES_PER_PIXEL, DEFAULT_CENTRE_LAT);
            // Field-scale viewport should pick a mid zoom band suitable for imagery tiles.
            Assert.InRange(Zoom, MIN_FIELD_ZOOM, MAX_FIELD_ZOOM);
        }

        /// <summary>
        /// Verifies Esri World Imagery tile URLs follow the expected path pattern.
        /// </summary>
        [Fact]
        public void BuildEsriWorldImageryTileUrl_MatchesExpectedPattern
            (
            )
        {
            string Url = WebMercator.BuildEsriWorldImageryTileUrl(DEFAULT_ZOOM, TILE_X, TILE_Y);
            // Tile coordinates must appear in Esri y/x order within the REST tile path.
            Assert.Contains($"/tile/{DEFAULT_ZOOM}/{TILE_Y}/{TILE_X}", Url);
            Assert.StartsWith("https://services.arcgisonline.com/", Url);
        }
    }
}
