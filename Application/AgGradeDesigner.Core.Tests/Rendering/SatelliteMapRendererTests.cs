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

            double CentreLat = 36.0;
            double CentreLon = -90.0;
            int Width = 800;
            int Height = 600;
            double MetresPerPixel = SatelliteMapLayout.EstimateMetresPerPixel(CentreLat, 1.0);

            SatelliteMapViewport Layout = SatelliteMapLayout.Compute(
                CentreLat,
                CentreLon,
                Width,
                Height,
                MetresPerPixel);

            foreach (SatelliteTileRequest Tile in Layout.Tiles)
            {
                byte[]? Data = await Service.EnsureTileAsync(Layout.Zoom, Tile.TileX, Tile.TileY);
                Assert.NotNull(Data);
                Assert.True(ImageBytes.LooksLikeImage(Data));
            }

            SatelliteMapRenderResult Result = SatelliteMapRenderer.Render(
                CentreLat,
                CentreLon,
                Width,
                Height,
                MetresPerPixel,
                Service);

            // Pre-cached tiles must render as real imagery with no placeholder tiles.
            Assert.True(Result.HasImagery);
            Assert.Equal(0, Result.PlaceholdersDrawn);
            Assert.Equal(Layout.Tiles.Count, Result.TilesDrawn);
            Assert.NotNull(Result.PngBytes);
            Assert.True(Result.PngBytes.Length > 1000);
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

            double CentreLat = 36.0;
            double CentreLon = -90.0;
            double MetresPerPixel = SatelliteMapLayout.EstimateMetresPerPixel(CentreLat, 1.0);

            SatelliteMapRenderResult Result = SatelliteMapRenderer.Render(
                CentreLat,
                CentreLon,
                800,
                600,
                MetresPerPixel,
                Service);

            // Empty cache must synthesize placeholders for every requested tile.
            Assert.False(Result.HasImagery);
            Assert.Equal(0, Result.TilesDrawn);
            Assert.True(Result.PlaceholdersDrawn > 0);
            Assert.Equal(Result.TilesRequested, Result.PlaceholdersDrawn);
        }
    }
}
