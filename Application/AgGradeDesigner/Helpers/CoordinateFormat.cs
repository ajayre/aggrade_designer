using System.Globalization;

namespace AgGradeDesigner.Helpers
{
    /// <summary>
    /// Formats and parses WGS-84 coordinates for the status bar, haul pivot, and well entry (§7.6.2–§7.6.3).
    /// </summary>
    static public class CoordinateFormat
    {
        /// <summary>Fixed decimal places shown for WGS-84 latitude and longitude.</summary>
        public const int DECIMAL_PLACES = 6;

        /// <summary>Lowest WGS-84 latitude that can be entered, in decimal degrees.</summary>
        public const double MIN_LATITUDE_DEG = -90.0;

        /// <summary>Highest WGS-84 latitude that can be entered, in decimal degrees.</summary>
        public const double MAX_LATITUDE_DEG = 90.0;

        /// <summary>Lowest WGS-84 longitude that can be entered, in decimal degrees.</summary>
        public const double MIN_LONGITUDE_DEG = -180.0;

        /// <summary>Highest WGS-84 longitude that can be entered, in decimal degrees.</summary>
        public const double MAX_LONGITUDE_DEG = 180.0;

        /// <summary>Standard numeric format for fixed-decimal coordinate display.</summary>
        private const string FIXED_DECIMAL_FORMAT = "F6";

        /// <summary>Styles accepted when a coordinate is typed. Exponents and thousands separators are not coordinates.</summary>
        private const NumberStyles COORDINATE_NUMBER_STYLES =
            NumberStyles.AllowLeadingWhite |
            NumberStyles.AllowTrailingWhite |
            NumberStyles.AllowLeadingSign |
            NumberStyles.AllowDecimalPoint;

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

        /// <summary>
        /// Parses a typed latitude in decimal degrees.
        /// </summary>
        /// <param name="Text">Text from the latitude box.</param>
        /// <param name="Latitude">Parsed latitude when the text is in range.</param>
        /// <returns>True when the text is a latitude from -90 to 90.</returns>
        static public bool TryParseLatitude
            (
            string? Text,
            out double Latitude
            )
        {
            return TryParseDegrees(Text, MIN_LATITUDE_DEG, MAX_LATITUDE_DEG, out Latitude);
        }

        /// <summary>
        /// Parses a typed longitude in decimal degrees.
        /// </summary>
        /// <param name="Text">Text from the longitude box.</param>
        /// <param name="Longitude">Parsed longitude when the text is in range.</param>
        /// <returns>True when the text is a longitude from -180 to 180.</returns>
        static public bool TryParseLongitude
            (
            string? Text,
            out double Longitude
            )
        {
            return TryParseDegrees(Text, MIN_LONGITUDE_DEG, MAX_LONGITUDE_DEG, out Longitude);
        }

        /// <summary>
        /// Parses one typed coordinate and rejects values outside its legal range.
        /// </summary>
        /// <param name="Text">Text from a coordinate box.</param>
        /// <param name="Minimum">Lowest accepted value in decimal degrees.</param>
        /// <param name="Maximum">Highest accepted value in decimal degrees.</param>
        /// <param name="Degrees">Parsed value when the text is in range.</param>
        /// <returns>True when the text is a finite coordinate inside the range.</returns>
        static private bool TryParseDegrees
            (
            string? Text,
            double Minimum,
            double Maximum,
            out double Degrees
            )
        {
            Degrees = 0.0;
            if (string.IsNullOrWhiteSpace(Text))
                return false;

            // The status bar prints a period. Also accept the decimal separator on this computer.
            if (!double.TryParse(Text, COORDINATE_NUMBER_STYLES, CultureInfo.InvariantCulture, out double Parsed) &&
                !double.TryParse(Text, COORDINATE_NUMBER_STYLES, CultureInfo.CurrentCulture, out Parsed))
                return false;

            if (double.IsNaN(Parsed) || double.IsInfinity(Parsed) || (Parsed < Minimum) || (Parsed > Maximum))
                return false;

            Degrees = Parsed;
            return true;
        }
    }
}
