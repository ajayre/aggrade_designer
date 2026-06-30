namespace AgGradeDesigner.Core.Geo
{
    /// <summary>
    /// Computes visible Web Mercator tile requests for a north-up map viewport.
    /// </summary>
    static public class SatelliteMapLayout
    {
        /// <summary>Minimum allowed zoom factor (maximum zoom-out).</summary>
        public const double MIN_ZOOM_FACTOR = 0.05;

        /// <summary>Maximum allowed zoom factor (maximum zoom-in).</summary>
        public const double MAX_ZOOM_FACTOR = 5000.0;

        /// <summary>
        /// Computes tile layout for a north-up viewport centred at the given location.
        /// </summary>
        /// <param name="CentreLat">Map centre latitude in decimal degrees.</param>
        /// <param name="CentreLon">Map centre longitude in decimal degrees.</param>
        /// <param name="ViewportWidth">Viewport width in pixels.</param>
        /// <param name="ViewportHeight">Viewport height in pixels.</param>
        /// <param name="MetresPerPixel">Ground metres per screen pixel.</param>
        /// <returns>Zoom level, scale, world origin, and screen-placed tile requests.</returns>
        static public SatelliteMapViewport Compute
            (
            double CentreLat,
            double CentreLon,
            float ViewportWidth,
            float ViewportHeight,
            double MetresPerPixel
            )
        {
            // Match Web Mercator tile resolution to the requested ground metres per pixel.
            int Zoom = WebMercator.PickZoomLevel(MetresPerPixel, CentreLat);
            double MetresPerWorldPixel = 156543.03392804097 * Math.Cos(CentreLat * Math.PI / 180.0) / Math.Pow(2, Zoom);
            double Scale = MetresPerWorldPixel / Math.Max(MetresPerPixel, 1e-9);

            WebMercator.LatLonToWorldPixel(CentreLon, CentreLat, Zoom, out double CentreWx, out double CentreWy);
            double LeftWorld = CentreWx - (ViewportWidth * 0.5 / Scale);
            double TopWorld = CentreWy - (ViewportHeight * 0.5 / Scale);

            int TilesPerAxis = 1 << Zoom;
            int TxStart = (int)Math.Floor(LeftWorld / WebMercator.TILE_SIZE_PX);
            int TxEnd = (int)Math.Floor((LeftWorld + ViewportWidth / Scale) / WebMercator.TILE_SIZE_PX);
            int TyStart = (int)Math.Floor(TopWorld / WebMercator.TILE_SIZE_PX);
            int TyEnd = (int)Math.Floor((TopWorld + ViewportHeight / Scale) / WebMercator.TILE_SIZE_PX);

            List<SatelliteTileRequest> Tiles = new List<SatelliteTileRequest>();
            for (int Ty = TyStart; Ty <= TyEnd; Ty++)
            {
                if ((Ty < 0) || (Ty >= TilesPerAxis))
                    continue;

                for (int Tx = TxStart; Tx <= TxEnd; Tx++)
                {
                    int Tcx = ((Tx % TilesPerAxis) + TilesPerAxis) % TilesPerAxis;
                    double TileLeftWorld = Tx * WebMercator.TILE_SIZE_PX;
                    double TileTopWorld = Ty * WebMercator.TILE_SIZE_PX;
                    float ScreenX = (float)((TileLeftWorld - LeftWorld) * Scale);
                    float ScreenY = (float)((TileTopWorld - TopWorld) * Scale);
                    float TileScreenSize = (float)(WebMercator.TILE_SIZE_PX * Scale);

                    Tiles.Add(new SatelliteTileRequest
                    {
                        TileX = Tcx,
                        TileY = Ty,
                        ScreenX = ScreenX,
                        ScreenY = ScreenY,
                        ScreenSize = TileScreenSize
                    });
                }
            }

            return new SatelliteMapViewport
            {
                Zoom = Zoom,
                Scale = Scale,
                LeftWorld = LeftWorld,
                TopWorld = TopWorld,
                Tiles = Tiles
            };
        }

        /// <summary>
        /// Estimates ground metres per pixel from latitude and zoom factor.
        /// </summary>
        /// <param name="CentreLat">Map centre latitude for scale correction.</param>
        /// <param name="ZoomFactor">Zoom multiplier relative to the default base resolution.</param>
        /// <returns>Metres per screen pixel at the given zoom.</returns>
        static public double EstimateMetresPerPixel
            (
            double CentreLat,
            double ZoomFactor
            )
        {
            double MetresPerDegreeLat = 111320.0;
            double MetresPerDegreeLon = MetresPerDegreeLat * Math.Cos(CentreLat * Math.PI / 180.0);
            double Base = Math.Max(MetresPerDegreeLat, MetresPerDegreeLon) / 800.0;
            return Base / Math.Max(MIN_ZOOM_FACTOR, ZoomFactor);
        }
    }
}
