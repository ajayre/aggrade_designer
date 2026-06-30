namespace AgGradeDesigner.Core.Geo
{
    /// <summary>
    /// Computes 2D map camera centre and zoom to frame a geographic extent.
    /// </summary>
    static public class MapViewFitter
    {
        /// <summary>
        /// Fits the map camera to the given field bounds within a viewport.
        /// </summary>
        /// <param name="Bounds">Geographic extent to frame.</param>
        /// <param name="ViewportWidth">Viewport width in pixels.</param>
        /// <param name="ViewportHeight">Viewport height in pixels.</param>
        /// <returns>Centre, zoom factor, and metres per pixel for the fitted view.</returns>
        static public MapViewFitResult Fit
            (
            FieldBounds Bounds,
            int ViewportWidth,
            int ViewportHeight
            )
        {
            if ((ViewportWidth <= 0) || (ViewportHeight <= 0))
                throw new ArgumentOutOfRangeException(nameof(ViewportWidth));

            double CentreLat = Bounds.CentreLat;
            double CentreLon = Bounds.CentreLon;
            double SpanLat = Math.Max(Bounds.MaxLat - Bounds.MinLat, 1e-6);
            double SpanLon = Math.Max(Bounds.MaxLon - Bounds.MinLon, 1e-6);
            double SpanMetresLat = SpanLat * 111320.0;
            double SpanMetresLon = SpanLon * 111320.0 * Math.Cos(CentreLat * Math.PI / 180.0);
            double SpanMetres = Math.Max(Math.Max(SpanMetresLat, SpanMetresLon), 50.0);

            // Derive zoom so the larger span occupies ~85% of the shorter viewport axis.
            double BaseMetresPerPixel = SatelliteMapLayout.EstimateMetresPerPixel(CentreLat, 1.0);
            double TargetMetresPerPixel = SpanMetres / (Math.Min(ViewportWidth, ViewportHeight) * 0.85);
            double ZoomFactor = Math.Clamp(
                BaseMetresPerPixel / TargetMetresPerPixel,
                SatelliteMapLayout.MIN_ZOOM_FACTOR,
                SatelliteMapLayout.MAX_ZOOM_FACTOR);

            return new MapViewFitResult
            {
                CentreLat = CentreLat,
                CentreLon = CentreLon,
                ZoomFactor = ZoomFactor,
                MetresPerPixel = BaseMetresPerPixel / ZoomFactor
            };
        }
    }
}
