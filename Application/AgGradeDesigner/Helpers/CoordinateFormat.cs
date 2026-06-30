using System.Globalization;

namespace AgGradeDesigner.Helpers
{
    /// <summary>
    /// Formats WGS-84 coordinates for the status bar (§7.6.2–§7.6.3).
    /// </summary>
    static public class CoordinateFormat
    {
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
            return Latitude.ToString("F5", CultureInfo.InvariantCulture);
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
            return Longitude.ToString("F5", CultureInfo.InvariantCulture);
        }
    }
}
