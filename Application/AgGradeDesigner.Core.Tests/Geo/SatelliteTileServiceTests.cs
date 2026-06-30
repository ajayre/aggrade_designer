using System.Net.Http;

using AgGradeDesigner.Core.Geo;

using AgGradeDesigner.Core.Tests.TestFixtures;

namespace AgGradeDesigner.Core.Tests.Geo
{
    /// <summary>
    /// Unit tests for <see cref="SatelliteTileService"/> download and cache behaviour.
    /// </summary>
    public sealed class SatelliteTileServiceTests
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
        /// Verifies a known tile near the default centre downloads and caches successfully.
        /// </summary>
        [Fact]
        public async Task EnsureTileAsync_DownloadsKnownTileNearDefaultCentre
            (
            )
        {
            using HttpClient Client = CreateTestClient();
            SatelliteTileService Service = new SatelliteTileService(Client);

            byte[]? Data = await Service.EnsureTileAsync(10, 256, 402);

            // Live tile fetch must return image bytes and populate the in-memory cache.
            Assert.NotNull(Data);
            Assert.True(Data.Length > 500);
            Assert.True(ImageBytes.LooksLikeImage(Data));
            Assert.True(Service.IsCached(10, 256, 402));
        }

        /// <summary>
        /// Verifies cached bytes are returned after a successful ensure call.
        /// </summary>
        [Fact]
        public async Task GetTile_ReturnsCachedBytesAfterEnsure
            (
            )
        {
            using HttpClient Client = CreateTestClient();
            SatelliteTileService Service = new SatelliteTileService(Client);

            await Service.EnsureTileAsync(10, 256, 402);

            byte[]? Cached = Service.GetTile(10, 256, 402);

            // Second access should hit the cache populated by EnsureTileAsync.
            Assert.NotNull(Cached);
            Assert.True(ImageBytes.LooksLikeImage(Cached));
        }
    }
}
