namespace AgGradeDesigner.Core.Geo
{
    /// <summary>
    /// Pixel dimensions of a map viewport used for lat/lon to screen projection.
    /// </summary>
    public readonly struct MapViewport
    {
        /// <summary>Viewport width in pixels.</summary>
        public float Width { get; init; }

        /// <summary>Viewport height in pixels.</summary>
        public float Height { get; init; }

        /// <summary>Horizontal centre of the viewport in pixels.</summary>
        public float MidX => Width * 0.5f;

        /// <summary>Vertical centre of the viewport in pixels.</summary>
        public float MidY => Height * 0.5f;
    }

    /// <summary>
    /// UTM-based map projection for field overlays aligned with the 2D viewer.
    /// </summary>
    static public class MapProjection
    {
        /// <summary>
        /// Projects a WGS-84 location to viewport screen coordinates.
        /// </summary>
        /// <param name="Lat">Point latitude in decimal degrees.</param>
        /// <param name="Lon">Point longitude in decimal degrees.</param>
        /// <param name="CentreLat">Map centre latitude in decimal degrees.</param>
        /// <param name="CentreLon">Map centre longitude in decimal degrees.</param>
        /// <param name="MetresPerPixel">Ground metres per screen pixel at the map centre.</param>
        /// <param name="Viewport">Viewport size in pixels.</param>
        /// <param name="ScreenX">Output horizontal screen coordinate in pixels.</param>
        /// <param name="ScreenY">Output vertical screen coordinate in pixels.</param>
        /// <returns>True when projection succeeds (always true for valid UTM inputs).</returns>
        static public bool LatLonToScreen
            (
            double Lat,
            double Lon,
            double CentreLat,
            double CentreLon,
            double MetresPerPixel,
            MapViewport Viewport,
            out float ScreenX,
            out float ScreenY
            )
        {
            UTM.UTMCoordinate Centre = UTM.FromLatLon(CentreLat, CentreLon);
            UTM.UTMCoordinate Point = UTM.FromLatLon(Lat, Lon);
            // Express offsets in metres then scale to pixels with Y inverted for screen space.
            double Dx = Point.Easting - Centre.Easting;
            double Dy = Point.Northing - Centre.Northing;
            ScreenX = (float)(Viewport.MidX + (Dx / MetresPerPixel));
            ScreenY = (float)(Viewport.MidY - (Dy / MetresPerPixel));
            return true;
        }

        /// <summary>
        /// Returns whether a screen point lies inside the viewport, optionally expanded by a margin.
        /// </summary>
        /// <param name="ScreenX">Horizontal screen coordinate in pixels.</param>
        /// <param name="ScreenY">Vertical screen coordinate in pixels.</param>
        /// <param name="Viewport">Viewport size in pixels.</param>
        /// <param name="MarginPx">Extra tolerance beyond viewport edges in pixels.</param>
        /// <returns>True when the point is on screen within the margin.</returns>
        static public bool IsOnScreen
            (
            float ScreenX,
            float ScreenY,
            MapViewport Viewport,
            float MarginPx = 0
            )
        {
            return (ScreenX >= -MarginPx) &&
                (ScreenY >= -MarginPx) &&
                (ScreenX <= (Viewport.Width + MarginPx)) &&
                (ScreenY <= (Viewport.Height + MarginPx));
        }
    }
}
