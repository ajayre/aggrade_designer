namespace AgGradeDesigner.Core.Geo
{
    /// <summary>
    /// Shared geographic and map projection numeric constants.
    /// </summary>
    static public class GeoConstants
    {
        /// <summary>Approximate ground metres per degree of latitude at mid-latitudes.</summary>
        public const double METRES_PER_DEGREE_LATITUDE = 111320.0;

        /// <summary>Full circle in decimal degrees.</summary>
        public const double DEGREES_IN_FULL_CIRCLE = 360.0;

        /// <summary>Half circle in decimal degrees.</summary>
        public const double DEGREES_IN_HALF_CIRCLE = 180.0;

        /// <summary>Ground metres per pixel at the equator for Web Mercator zoom level 0.</summary>
        public const double EQUATOR_METRES_PER_PIXEL_AT_ZOOM_0 = 156543.03392804097;

        /// <summary>Minimum geographic span in metres when fitting a map view.</summary>
        public const double MIN_MAP_SPAN_METRES = 50.0;

        /// <summary>Fraction of the shorter viewport axis used when framing field bounds.</summary>
        public const double MAP_FIT_VIEWPORT_FILL_FRACTION = 0.85;

        /// <summary>Divisor used to estimate base metres per pixel from degree spans.</summary>
        public const double BASE_METRES_PER_PIXEL_DIVISOR = 800.0;

        /// <summary>Fraction of viewport width/height from centre to edge.</summary>
        public const double VIEWPORT_CENTRE_FRACTION = 0.5;

        /// <summary>Minimum vertices required to form a closed polygon ring.</summary>
        public const int MIN_POLYGON_VERTICES = 3;

        /// <summary>Minimum vertices required to measure an open or closed polyline path.</summary>
        public const int MIN_POLYLINE_VERTICES = 2;

        /// <summary>Shoelace formula divisor for polygon area.</summary>
        public const double SHOELACE_AREA_DIVISOR = 0.5;

        /// <summary>Minimum geographic span in degrees to avoid zero-area fit degeneracy.</summary>
        public const double MIN_GEO_SPAN_DEGREES = 1e-6;

        /// <summary>Minimum positive metres-per-pixel value for map scale calculations.</summary>
        public const double MIN_METRES_PER_PIXEL = 1e-9;
    }
}
