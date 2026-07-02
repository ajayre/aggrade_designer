using System.Net.Http;

using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Rendering;

using AgGradeDesigner.Core.Tests.TestFixtures;

namespace AgGradeDesigner.Core.Tests.Rendering
{
    /// <summary>
    /// Unit tests for <see cref="SatelliteMapRenderer"/> tile drawing and placeholder fallback.
    /// </summary>
    public sealed class SatelliteMapRendererTests
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
        /// Expected value used when asserting placeholder count.
        /// </summary>

        private const int EXPECTED_PLACEHOLDER_COUNT = 0;
        /// <summary>
        /// Min png byte length used by this component.
        /// </summary>

        private const int MIN_PNG_BYTE_LENGTH = 1000;

        /// <summary>
        /// Creates an HTTP client configured for Esri tile requests in tests.
        /// </summary>
        /// <returns>Client with the test user-agent header applied.</returns>
        static private HttpClient CreateTestClient
            (
            )
        {
            HttpClient Client = new HttpClient();
            Client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "AgGradeDesigner-Tests/0.1");
            return Client;
        }

        /// <summary>
        /// Verifies rendering with downloaded tiles draws imagery without placeholders.
        /// </summary>
        [Fact]
        public async Task Render_WithDownloadedTiles_DrawsImageryWithoutPlaceholders
            (
            )
        {
            using HttpClient Client = CreateTestClient();
            SatelliteTileService Service = new SatelliteTileService(Client);

            double MetresPerPixel = SatelliteMapLayout.EstimateMetresPerPixel(DEFAULT_CENTRE_LAT, ZOOM_FACTOR);

            SatelliteMapViewport Layout = SatelliteMapLayout.Compute(
                DEFAULT_CENTRE_LAT,
                DEFAULT_CENTRE_LON,
                VIEWPORT_WIDTH,
                VIEWPORT_HEIGHT,
                MetresPerPixel);

            foreach (SatelliteTileRequest Tile in Layout.Tiles)
            {
                byte[]? Data = await Service.EnsureTileAsync(Layout.Zoom, Tile.TileX, Tile.TileY);
                Assert.NotNull(Data);
                Assert.True(ImageBytes.LooksLikeImage(Data));
            }

            SatelliteMapRenderResult Result = SatelliteMapRenderer.Render(
                DEFAULT_CENTRE_LAT,
                DEFAULT_CENTRE_LON,
                VIEWPORT_WIDTH,
                VIEWPORT_HEIGHT,
                MetresPerPixel,
                Service);

            // Pre-cached tiles must render as real imagery with no placeholder tiles.
            Assert.True(Result.HasImagery);
            Assert.Equal(EXPECTED_PLACEHOLDER_COUNT, Result.PlaceholdersDrawn);
            Assert.Equal(Layout.Tiles.Count, Result.TilesDrawn);
            Assert.NotNull(Result.PngBytes);
            Assert.True(Result.PngBytes.Length > MIN_PNG_BYTE_LENGTH);
            Assert.True(ImageBytes.LooksLikeImage(Result.PngBytes));
            Assert.True(ImageBytes.HasVariedPixels(Result.PngBytes));
        }

        /// <summary>
        /// Verifies rendering without cached tiles falls back to placeholders.
        /// </summary>
        [Fact]
        public void Render_WithoutCachedTiles_UsesPlaceholders
            (
            )
        {
            SatelliteTileService Service = new SatelliteTileService(new HttpClient());

            double MetresPerPixel = SatelliteMapLayout.EstimateMetresPerPixel(DEFAULT_CENTRE_LAT, ZOOM_FACTOR);

            SatelliteMapRenderResult Result = SatelliteMapRenderer.Render(
                DEFAULT_CENTRE_LAT,
                DEFAULT_CENTRE_LON,
                VIEWPORT_WIDTH,
                VIEWPORT_HEIGHT,
                MetresPerPixel,
                Service);

            // Empty cache must synthesize placeholders for every requested tile.
            Assert.False(Result.HasImagery);
            Assert.Equal(EXPECTED_PLACEHOLDER_COUNT, Result.TilesDrawn);
            Assert.True(Result.PlaceholdersDrawn > 0);
            Assert.Equal(Result.TilesRequested, Result.PlaceholdersDrawn);
        }
    }
}
