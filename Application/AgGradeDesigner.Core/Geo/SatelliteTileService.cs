using System.Collections.Concurrent;
using System.Net.Http;

using SkiaSharp;

namespace AgGradeDesigner.Core.Geo
{
    /// <summary>
    /// Fetches and caches Esri World Imagery tiles using Web Mercator tile URLs.
    /// </summary>
    public sealed class SatelliteTileService
    {
        static private readonly HttpClient DefaultClient = CreateDefaultClient();

        readonly HttpClient _Client;
        readonly ConcurrentDictionary<(int Z, int X, int Y), byte[]> _Cache = new ConcurrentDictionary<(int Z, int X, int Y), byte[]>();
        readonly ConcurrentDictionary<(int Z, int X, int Y), byte> _Inflight = new ConcurrentDictionary<(int Z, int X, int Y), byte>();
        readonly ConcurrentDictionary<(int Z, int X, int Y), SKBitmap> _BitmapCache = new ConcurrentDictionary<(int Z, int X, int Y), SKBitmap>();
        private int _CacheGeneration;

        /// <summary>Raised when a background tile fetch completes and the cache may have new imagery.</summary>
        public event Action? TilesUpdated;

        /// <summary>
        /// Increments whenever a new tile is stored; used to detect stale viewer frame caches.
        /// </summary>
        public int CacheGeneration => Volatile.Read(ref _CacheGeneration);

        /// <summary>
        /// Creates a tile service using the shared default HTTP client.
        /// </summary>
        public SatelliteTileService
            (
            )
            : this(DefaultClient)
        {
        }

        /// <summary>
        /// Creates a tile service with a caller-supplied HTTP client.
        /// </summary>
        /// <param name="Client">HTTP client used for tile downloads.</param>
        public SatelliteTileService
            (
            HttpClient Client
            )
        {
            _Client = Client ?? throw new ArgumentNullException(nameof(Client));
        }

        /// <summary>
        /// Returns a cached tile synchronously, or starts a background fetch when absent.
        /// </summary>
        /// <param name="Level">Web Mercator zoom level.</param>
        /// <param name="Col">Tile column index.</param>
        /// <param name="Row">Tile row index.</param>
        /// <returns>Cached PNG/JPEG bytes, or null while the tile is loading.</returns>
        public byte[]? GetTile
            (
            int Level,
            int Col,
            int Row
            )
        {
            (int Z, int X, int Y) Key = (Level, Col, Row);
            if (_Cache.TryGetValue(Key, out byte[]? Cached))
                return Cached;

            RequestTile(Key);
            return null;
        }

        /// <summary>
        /// Returns a decoded tile bitmap, reusing a per-tile decode cache when raw bytes are already cached.
        /// </summary>
        /// <param name="Level">Web Mercator zoom level.</param>
        /// <param name="Col">Tile column index.</param>
        /// <param name="Row">Tile row index.</param>
        /// <returns>Cached bitmap, or null while the tile is loading or decode fails.</returns>
        public SKBitmap? GetTileBitmap
            (
            int Level,
            int Col,
            int Row
            )
        {
            (int Z, int X, int Y) Key = (Level, Col, Row);
            if (_BitmapCache.TryGetValue(Key, out SKBitmap? Cached))
                return Cached;

            byte[]? Data = GetTile(Level, Col, Row);
            if (Data == null)
                return null;

            SKBitmap? Bitmap = SKBitmap.Decode(Data);
            if (Bitmap == null)
                return null;

            _BitmapCache[Key] = Bitmap;
            return Bitmap;
        }

        /// <summary>
        /// Returns whether a tile is already present in the cache.
        /// </summary>
        /// <param name="Level">Web Mercator zoom level.</param>
        /// <param name="Col">Tile column index.</param>
        /// <param name="Row">Tile row index.</param>
        /// <returns>True when the tile bytes are cached.</returns>
        public bool IsCached
            (
            int Level,
            int Col,
            int Row
            )
        {
            return _Cache.ContainsKey((Level, Col, Row));
        }

        /// <summary>
        /// Waits for a tile download to complete and returns the cached bytes.
        /// </summary>
        /// <param name="Level">Web Mercator zoom level.</param>
        /// <param name="Col">Tile column index.</param>
        /// <param name="Row">Tile row index.</param>
        /// <param name="CancellationToken">Cancellation token for the HTTP request.</param>
        /// <returns>Tile bytes after download, or null when fetch failed.</returns>
        public async Task<byte[]?> EnsureTileAsync
            (
            int Level,
            int Col,
            int Row,
            CancellationToken CancellationToken = default
            )
        {
            (int Z, int X, int Y) Key = (Level, Col, Row);
            if (_Cache.TryGetValue(Key, out byte[]? Cached))
                return Cached;

            await FetchTileAsync(Key, CancellationToken).ConfigureAwait(false);
            _Cache.TryGetValue(Key, out Cached);
            return Cached;
        }

        /// <summary>
        /// Inserts pre-fetched tile bytes into the cache (for tests and offline seeding).
        /// </summary>
        /// <param name="Level">Web Mercator zoom level.</param>
        /// <param name="Col">Tile column index.</param>
        /// <param name="Row">Tile row index.</param>
        /// <param name="Data">Raw tile image bytes.</param>
        public void SeedTile
            (
            int Level,
            int Col,
            int Row,
            byte[] Data
            )
        {
            if ((Data == null) || (Data.Length == 0))
                throw new ArgumentException("Tile data is required.", nameof(Data));

            _Cache[(Level, Col, Row)] = Data;
            _BitmapCache.TryRemove((Level, Col, Row), out SKBitmap? OldBitmap);
            OldBitmap?.Dispose();
            Interlocked.Increment(ref _CacheGeneration);
        }

        /// <summary>
        /// Starts a fire-and-forget fetch when the tile is not cached and not already in flight.
        /// </summary>
        /// <param name="Key">Tile index tuple (zoom, column, row).</param>
        private void RequestTile
            (
            (int Z, int X, int Y) Key
            )
        {
            if (!_Inflight.TryAdd(Key, 0))
                return;

            _ = FetchTileAsync(Key, CancellationToken.None);
        }

        /// <summary>
        /// Downloads one tile and updates the cache; swallows network errors for offline use.
        /// </summary>
        /// <param name="Key">Tile index tuple (zoom, column, row).</param>
        /// <param name="CancellationToken">Cancellation token for the HTTP request.</param>
        private async Task FetchTileAsync
            (
            (int Z, int X, int Y) Key,
            CancellationToken CancellationToken
            )
        {
            try
            {
                string Url = WebMercator.BuildEsriWorldImageryTileUrl(Key.Z, Key.X, Key.Y);
                byte[] Data = await _Client.GetByteArrayAsync(Url, CancellationToken).ConfigureAwait(false);
                if (Data.Length > 0)
                {
                    _Cache[Key] = Data;
                    _BitmapCache.TryRemove(Key, out SKBitmap? OldBitmap);
                    OldBitmap?.Dispose();
                    Interlocked.Increment(ref _CacheGeneration);
                }

                TilesUpdated?.Invoke();
            }
            catch
            {
                // Tile unavailable offline or blocked; caller draws placeholder.
            }
            finally
            {
                _Inflight.TryRemove(Key, out _);
            }
        }

        /// <summary>
        /// Creates the shared HTTP client with a product user-agent header.
        /// </summary>
        /// <returns>Configured HTTP client for tile downloads.</returns>
        static HttpClient CreateDefaultClient
            (
            )
        {
            HttpClient Client = new HttpClient();
            Client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "AgGradeDesigner/0.1");
            return Client;
        }
    }
}
