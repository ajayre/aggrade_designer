namespace AgGradeDesigner.Core
{
    /// <summary>
    /// Shared numeric constants for field bin geometry, cut-fill defaults, and unit conversions.
    /// </summary>
    static public class FieldConstants
    {
        /// <summary>Standard bin edge length in metres (two feet).</summary>
        public const double BIN_SIZE_M = 0.6096;

        /// <summary>Default cut-to-fill volume ratio used when design parameters omit an override.</summary>
        public const double CUT_FILL_RATIO = 1.2;

        /// <summary>Sentinel elevation value indicating a grid bin has no survey data.</summary>
        public const int BIN_NO_DATA_SENTINEL = -1000;

        /// <summary>Conversion factor from cubic metres to cubic yards.</summary>
        public const double CUBIC_YARDS_PER_CUBIC_METER = 1.30795061931439;

        /// <summary>International acre expressed in square metres.</summary>
        public const double SQUARE_METERS_PER_ACRE = 4046.8564224;

        /// <summary>Metres per foot for elevation display conversion.</summary>
        public const double METERS_PER_FOOT = 0.3048;
    }
}
