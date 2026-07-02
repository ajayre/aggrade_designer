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

        /// <summary>Offset from bin index to bin centre in bin-width units.</summary>
        public const double BIN_CENTER_OFFSET = 0.5;

        /// <summary>Fraction of a span used to locate the geographic midpoint of an extent.</summary>
        public const double EXTENT_MIDPOINT_FRACTION = 0.5;

        /// <summary>Degrees in a full circle.</summary>
        public const double DEGREES_FULL_CIRCLE = 360.0;

        /// <summary>Degrees per radian (180/π).</summary>
        public const double DEGREES_PER_RADIAN = 180.0;

        /// <summary>General-purpose floating-point comparison tolerance.</summary>
        public const double EPSILON = 1e-9;

        /// <summary>Fine floating-point comparison tolerance for geometry and interpolation.</summary>
        public const double EPSILON_FINE = 1e-12;

        /// <summary>Approximate ground metres per degree of latitude.</summary>
        public const double METRES_PER_DEGREE_LAT = 111320.0;
    }
}
