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
        /// Default zoom used by this component.
        /// </summary>

        private const int DEFAULT_ZOOM = 10;
        /// <summary>
        /// Reference tile x used by this component.
        /// </summary>

        private const int REFERENCE_TILE_X = 256;
        /// <summary>
        /// Reference tile y used by this component.
        /// </summary>

        private const int REFERENCE_TILE_Y = 402;
        /// <summary>
        /// Seed tile x used by this component.
        /// </summary>

        private const int SEED_TILE_X = 1;
        /// <summary>
        /// Seed tile y 1 used by this component.
        /// </summary>

        private const int SEED_TILE_Y_1 = 2;
        /// <summary>
        /// Seed tile y 2 used by this component.
        /// </summary>

        private const int SEED_TILE_Y_2 = 3;
        /// <summary>
        /// Min tile byte length used by this component.
        /// </summary>

        private const int MIN_TILE_BYTE_LENGTH = 500;
        /// <summary>
        /// Initial cache generation used by this component.
        /// </summary>

        private const int INITIAL_CACHE_GENERATION = 0;
        /// <summary>
        /// First cache generation used by this component.
        /// </summary>

        private const int FIRST_CACHE_GENERATION = 1;
        /// <summary>
        /// Second cache generation used by this component.
        /// </summary>

        private const int SECOND_CACHE_GENERATION = 2;

        /// <summary>First four bytes of a valid PNG stream used to validate downloaded tile payloads.</summary>
        private static readonly byte[] PNG_SIGNATURE_BYTES = { 0x89, 0x50, 0x4E, 0x47 };

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

            byte[]? Data = await Service.EnsureTileAsync(DEFAULT_ZOOM, REFERENCE_TILE_X, REFERENCE_TILE_Y);

            // Live tile fetch must return image bytes and populate the in-memory cache.
            Assert.NotNull(Data);
            Assert.True(Data.Length > MIN_TILE_BYTE_LENGTH);
            Assert.True(ImageBytes.LooksLikeImage(Data));
            Assert.True(Service.IsCached(DEFAULT_ZOOM, REFERENCE_TILE_X, REFERENCE_TILE_Y));
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

            await Service.EnsureTileAsync(DEFAULT_ZOOM, REFERENCE_TILE_X, REFERENCE_TILE_Y);

            byte[]? Cached = Service.GetTile(DEFAULT_ZOOM, REFERENCE_TILE_X, REFERENCE_TILE_Y);

            // Second access should hit the cache populated by EnsureTileAsync.
            Assert.NotNull(Cached);
            Assert.True(ImageBytes.LooksLikeImage(Cached));
        }

        /// <summary>
        /// Seeding or downloading tiles must advance the cache generation counter.
        /// </summary>
        [Fact]
        public void CacheGeneration_IncrementsWhenTilesAreStored
            (
            )
        {
            SatelliteTileService Service = new SatelliteTileService();
            Assert.Equal(INITIAL_CACHE_GENERATION, Service.CacheGeneration);

            Service.SeedTile(DEFAULT_ZOOM, SEED_TILE_X, SEED_TILE_Y_1, PNG_SIGNATURE_BYTES);
            Assert.Equal(FIRST_CACHE_GENERATION, Service.CacheGeneration);

            Service.SeedTile(DEFAULT_ZOOM, SEED_TILE_X, SEED_TILE_Y_2, PNG_SIGNATURE_BYTES);
            Assert.Equal(SECOND_CACHE_GENERATION, Service.CacheGeneration);
        }
    }
}
