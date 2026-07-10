using System.Globalization;

namespace AgGradeDesigner.Helpers
{
    /// <summary>
    /// Formats WGS-84 coordinates for the status bar and haul pivot settings (§7.6.2–§7.6.3).
    /// </summary>
    static public class CoordinateFormat
    {
        /// <summary>Fixed decimal places shown for WGS-84 latitude and longitude.</summary>
        public const int DECIMAL_PLACES = 6;

        /// <summary>Standard numeric format for fixed-decimal coordinate display.</summary>
        private const string FIXED_DECIMAL_FORMAT = "F6";

        /// <summary>
        /// Rounds a WGS-84 coordinate to the display precision used in the UI.
        /// </summary>
        /// <param name="Coordinate">Latitude or longitude in decimal degrees.</param>
        /// <returns>Coordinate rounded to <see cref="DECIMAL_PLACES"/> decimal places.</returns>
        static public double RoundCoordinate
            (
            double Coordinate
            )
        {
            return Math.Round(Coordinate, DECIMAL_PLACES, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Formats latitude in fixed decimal degrees for display.
        /// </summary>
        /// <param name="Latitude">WGS-84 latitude in degrees.</param>
        /// <returns>Formatted latitude string.</returns>
        static public string FormatLatitude
            (
            double Latitude
            )
        {
            // Use invariant culture so decimal separator stays consistent in the status bar.
            return RoundCoordinate(Latitude).ToString(FIXED_DECIMAL_FORMAT, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Formats longitude in fixed decimal degrees for display.
        /// </summary>
        /// <param name="Longitude">WGS-84 longitude in degrees.</param>
        /// <returns>Formatted longitude string.</returns>
        static public string FormatLongitude
            (
            double Longitude
            )
        {
            // Use invariant culture so decimal separator stays consistent in the status bar.
            return RoundCoordinate(Longitude).ToString(FIXED_DECIMAL_FORMAT, CultureInfo.InvariantCulture);
        }
    }
}
