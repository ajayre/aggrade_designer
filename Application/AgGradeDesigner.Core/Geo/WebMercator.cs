using AgGradeDesigner.Core;

namespace AgGradeDesigner.Core.Geo
{
    /// <summary>
    /// Web Mercator tile coordinate helpers for satellite basemap layout.
    /// </summary>
    static public class WebMercator
    {
        /// <summary>Standard Web Mercator tile size in pixels.</summary>
        public const int TILE_SIZE_PX = 256;

        /// <summary>Maximum latitude clamped before Web Mercator projection.</summary>
        private const double MAX_LATITUDE_CLAMP_DEG = 85.05112878;

        /// <summary>Epsilon subtracted from world pixel height when clamping Y.</summary>
        private const double WORLD_PIXEL_CLAMP_EPSILON = FieldConstants.EPSILON;

        /// <summary>Fallback zoom level when metres-per-pixel input is invalid.</summary>
        private const int DEFAULT_ZOOM_WHEN_INVALID = 14;

        /// <summary>Minimum supported Web Mercator zoom level.</summary>
        private const int MIN_ZOOM_LEVEL = 0;

        /// <summary>Maximum supported Web Mercator zoom level.</summary>
        private const int MAX_ZOOM_LEVEL = 19;

        /// <summary>Divisor used in the Web Mercator Y projection formula.</summary>
        private const double MERCATOR_Y_DOUBLE = 2.0;

        /// <summary>Base of the zoom-level power-of-two world size calculation.</summary>
        private const int ZOOM_LEVEL_BASE = 2;

        /// <summary>Numerator offset in the Web Mercator Y projection formula.</summary>
        private const double MERCATOR_Y_NUMERATOR_OFFSET = 1.0;

        /// <summary>Scale applied to latitude radians in the inverse Mercator formula.</summary>
        private const double MERCATOR_LAT_RAD_SCALE = 1.0;

        /// <summary>Minimum world pixel Y coordinate after clamping.</summary>
        private const double WORLD_PIXEL_MIN = 0.0;

        /// <summary>
        /// Returns the world size in pixels at the given zoom level.
        /// </summary>
        /// <param name="Zoom">Web Mercator zoom level.</param>
        /// <returns>World width and height in pixels.</returns>
        static public double WorldSizePixels
            (
            int Zoom
            )
        {
            return TILE_SIZE_PX * Math.Pow(ZOOM_LEVEL_BASE, Zoom);
        }

        /// <summary>
        /// Converts WGS-84 coordinates to world pixel coordinates at a zoom level.
        /// </summary>
        /// <param name="Lon">Longitude in decimal degrees.</param>
        /// <param name="Lat">Latitude in decimal degrees.</param>
        /// <param name="Zoom">Web Mercator zoom level.</param>
        /// <param name="Wx">Output world X in pixels.</param>
        /// <param name="Wy">Output world Y in pixels.</param>
        static public void LatLonToWorldPixel
            (
            double Lon,
            double Lat,
            int Zoom,
            out double Wx,
            out double Wy
            )
        {
            // Clamp to Web Mercator valid latitude range before projecting.
            Lat = Math.Clamp(Lat, -MAX_LATITUDE_CLAMP_DEG, MAX_LATITUDE_CLAMP_DEG);
            double LonWrapped = (((Lon + GeoConstants.DEGREES_IN_HALF_CIRCLE) % GeoConstants.DEGREES_IN_FULL_CIRCLE) + GeoConstants.DEGREES_IN_FULL_CIRCLE) % GeoConstants.DEGREES_IN_FULL_CIRCLE - GeoConstants.DEGREES_IN_HALF_CIRCLE;
            double WorldSize = WorldSizePixels(Zoom);
            Wx = ((LonWrapped + GeoConstants.DEGREES_IN_HALF_CIRCLE) / GeoConstants.DEGREES_IN_FULL_CIRCLE) * WorldSize;
            double LatRad = Lat * Math.PI / GeoConstants.DEGREES_IN_HALF_CIRCLE;
            Wy = (MERCATOR_Y_NUMERATOR_OFFSET - (Math.Log(Math.Tan(LatRad) + (MERCATOR_Y_NUMERATOR_OFFSET / Math.Cos(LatRad))) / Math.PI)) / MERCATOR_Y_DOUBLE * WorldSize;
        }

        /// <summary>
        /// Converts world pixel coordinates back to WGS-84 at a zoom level.
        /// </summary>
        /// <param name="Wx">World X in pixels.</param>
        /// <param name="Wy">World Y in pixels.</param>
        /// <param name="Zoom">Web Mercator zoom level.</param>
        /// <param name="Lon">Output longitude in decimal degrees.</param>
        /// <param name="Lat">Output latitude in decimal degrees.</param>
        static public void WorldPixelToLatLon
            (
            double Wx,
            double Wy,
            int Zoom,
            out double Lon,
            out double Lat
            )
        {
            double WorldSize = WorldSizePixels(Zoom);
            Wx = ((Wx % WorldSize) + WorldSize) % WorldSize;
            Wy = Math.Clamp(Wy, WORLD_PIXEL_MIN, WorldSize - WORLD_PIXEL_CLAMP_EPSILON);
            Lon = (Wx / WorldSize) * GeoConstants.DEGREES_IN_FULL_CIRCLE - GeoConstants.DEGREES_IN_HALF_CIRCLE;
            double LatRad = Math.Atan(Math.Sinh(Math.PI * (MERCATOR_LAT_RAD_SCALE - (MERCATOR_Y_DOUBLE * Wy / WorldSize))));
            Lat = LatRad * GeoConstants.DEGREES_IN_HALF_CIRCLE / Math.PI;
        }

        /// <summary>
        /// Chooses the Web Mercator zoom level closest to the desired ground resolution.
        /// </summary>
        /// <param name="MetresPerPixel">Target ground metres per screen pixel.</param>
        /// <param name="CentreLat">Map centre latitude used for scale correction.</param>
        /// <returns>Zoom level clamped to the supported 0–19 range.</returns>
        static public int PickZoomLevel
            (
            double MetresPerPixel,
            double CentreLat
            )
        {
            if (MetresPerPixel <= 0)
                return DEFAULT_ZOOM_WHEN_INVALID;

            double MetresPerPixelAtLat = GeoConstants.EQUATOR_METRES_PER_PIXEL_AT_ZOOM_0 * Math.Cos(CentreLat * Math.PI / GeoConstants.DEGREES_IN_HALF_CIRCLE);
            int Level = (int)Math.Round(Math.Log(MetresPerPixelAtLat / MetresPerPixel) / Math.Log(ZOOM_LEVEL_BASE));
            return Math.Clamp(Level, MIN_ZOOM_LEVEL, MAX_ZOOM_LEVEL);
        }

        /// <summary>
        /// Builds the Esri World Imagery tile URL for a Web Mercator tile index.
        /// </summary>
        /// <param name="Zoom">Web Mercator zoom level.</param>
        /// <param name="TileX">Tile column index.</param>
        /// <param name="TileY">Tile row index.</param>
        /// <returns>HTTPS URL for the raster tile.</returns>
        static public string BuildEsriWorldImageryTileUrl
            (
            int Zoom,
            int TileX,
            int TileY
            )
        {
            return string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "https://services.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{0}/{1}/{2}",
                Zoom,
                TileY,
                TileX);
        }

        /// <summary>
        /// Returns the WGS-84 bounding box of a Web Mercator tile.
        /// </summary>
        /// <param name="Zoom">Web Mercator zoom level.</param>
        /// <param name="TileX">Tile column index.</param>
        /// <param name="TileY">Tile row index.</param>
        /// <param name="MinLon">Output minimum longitude.</param>
        /// <param name="MinLat">Output minimum latitude.</param>
        /// <param name="MaxLon">Output maximum longitude.</param>
        /// <param name="MaxLat">Output maximum latitude.</param>
        static public void TileLatLonBounds
            (
            int Zoom,
            int TileX,
            int TileY,
            out double MinLon,
            out double MinLat,
            out double MaxLon,
            out double MaxLat
            )
        {
            double WorldSize = WorldSizePixels(Zoom);
            double Wx0 = TileX * TILE_SIZE_PX;
            double Wy0 = TileY * TILE_SIZE_PX;
            double Wx1 = Wx0 + TILE_SIZE_PX;
            double Wy1 = Wy0 + TILE_SIZE_PX;
            WorldPixelToLatLon(Wx0, Wy1, Zoom, out MinLon, out MaxLat);
            WorldPixelToLatLon(Wx1, Wy0, Zoom, out MaxLon, out MinLat);
        }

        /// <summary>
        /// Computes inclusive tile index ranges covering a WGS-84 bounding box.
        /// </summary>
        /// <param name="MinLat">Minimum latitude in decimal degrees.</param>
        /// <param name="MaxLat">Maximum latitude in decimal degrees.</param>
        /// <param name="MinLon">Minimum longitude in decimal degrees.</param>
        /// <param name="MaxLon">Maximum longitude in decimal degrees.</param>
        /// <param name="Zoom">Web Mercator zoom level.</param>
        /// <param name="TileXStart">Output first tile column index.</param>
        /// <param name="TileXEnd">Output last tile column index.</param>
        /// <param name="TileYStart">Output first tile row index.</param>
        /// <param name="TileYEnd">Output last tile row index.</param>
        static public void TilesCoveringBounds
            (
            double MinLat,
            double MaxLat,
            double MinLon,
            double MaxLon,
            int Zoom,
            out int TileXStart,
            out int TileXEnd,
            out int TileYStart,
            out int TileYEnd
            )
        {
            LatLonToWorldPixel(MinLon, MaxLat, Zoom, out double Wx0, out double Wy0);
            LatLonToWorldPixel(MaxLon, MinLat, Zoom, out double Wx1, out double Wy1);
            TileXStart = (int)Math.Floor(Math.Min(Wx0, Wx1) / TILE_SIZE_PX);
            TileXEnd = (int)Math.Floor(Math.Max(Wx0, Wx1) / TILE_SIZE_PX);
            TileYStart = (int)Math.Floor(Math.Min(Wy0, Wy1) / TILE_SIZE_PX);
            TileYEnd = (int)Math.Floor(Math.Max(Wy0, Wy1) / TILE_SIZE_PX);
        }
    }
}
