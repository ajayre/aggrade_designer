using System.Globalization;

using AgGradeDesigner.Core;

namespace AgGradeDesigner.Core.Features
{
    /// <summary>
    /// Bed width, height, and side slope for a field road, and the plan distances drawn beside its centreline.
    /// </summary>
    static public class RoadCrossSection
    {
        /// <summary>Default embankment height in feet.</summary>
        public const double DEFAULT_HEIGHT_FT = 1.0;

        /// <summary>Default side slope as horizontal run per one vertical (the X in X:1).</summary>
        public const double DEFAULT_SIDE_SLOPE = 3.0;

        /// <summary>Blade width used when a project has no positive blade width, in feet.</summary>
        public const double REFERENCE_BLADE_WIDTH_FT = 8.5;

        /// <summary>How many blade widths are spanned by the inner dashed pair.</summary>
        private const double BLADE_WIDTH_MULTIPLIER = 2.0;

        /// <summary>Smallest bed the default formula may produce, in feet.</summary>
        private const double MIN_BED_WIDTH_FT = 1.0;

        /// <summary>Half of a full width. The toe and the blade pair are measured from the centreline.</summary>
        private const double HALF = 0.5;

        /// <summary>Millimetres in one metre.</summary>
        private const double MM_PER_METRE = 1000.0;

        /// <summary>Display format for the side-slope number.</summary>
        private const string SLOPE_DISPLAY_FORMAT = "0.####";

        /// <summary>Default embankment height in metres.</summary>
        static public double DefaultHeightM => DEFAULT_HEIGHT_FT * FieldConstants.METERS_PER_FOOT;

        /// <summary>
        /// Bed width for a new road: twice the blade width, minus the side-slope number, in feet.
        /// An 8.5 ft blade and a slope of 3 give 14 ft.
        /// </summary>
        /// <param name="BladeWidthMm">Project blade width in millimetres.</param>
        /// <param name="SideSlope">Horizontal run per one vertical.</param>
        /// <returns>Bed width in metres.</returns>
        static public double DefaultBedWidthM
            (
            double BladeWidthMm,
            double SideSlope
            )
        {
            double Slope = SideSlope;
            if (double.IsNaN(Slope) || double.IsInfinity(Slope) || (Slope < 0.0))
                Slope = DEFAULT_SIDE_SLOPE;

            // The slope number is subtracted in feet, matching  (2 × 8.5) − 3 = 14.
            double BedFt = (BLADE_WIDTH_MULTIPLIER * BladeWidthFt(BladeWidthMm)) - Slope;
            if (BedFt <= 0.0)
                BedFt = MIN_BED_WIDTH_FT;

            return BedFt * FieldConstants.METERS_PER_FOOT;
        }

        /// <summary>
        /// Distance from a centre to the 2× blade-width circle, or from a centreline to that pair.
        /// The circle's diameter, and the span between the two lines, is twice the blade width.
        /// </summary>
        /// <param name="BladeWidthMm">Project blade width in millimetres.</param>
        /// <returns>Radius or offset in metres.</returns>
        static public double BladePairOffsetM
            (
            double BladeWidthMm
            )
        {
            return BladeWidthFt(BladeWidthMm) * FieldConstants.METERS_PER_FOOT;
        }

        /// <summary>
        /// Distance from the centreline to one edge of the road bed.
        /// </summary>
        /// <param name="BedWidthM">Full road bed width in metres.</param>
        /// <returns>Offset from the centreline in metres.</returns>
        static public double BedEdgeOffsetM
            (
            double BedWidthM
            )
        {
            return BedWidthM > 0.0 ? BedWidthM * HALF : 0.0;
        }

        /// <summary>
        /// Distance from the centreline to the toe. The side slope runs outward from each edge of the bed.
        /// </summary>
        /// <param name="BedWidthM">Full road bed width in metres.</param>
        /// <param name="HeightM">Embankment height in metres.</param>
        /// <param name="SideSlope">Horizontal run per one vertical.</param>
        /// <returns>Offset from the centreline in metres.</returns>
        static public double ToeOffsetM
            (
            double BedWidthM,
            double HeightM,
            double SideSlope
            )
        {
            double Slope = SideSlope > 0.0 ? SideSlope : 0.0;
            double Height = HeightM > 0.0 ? HeightM : 0.0;
            return BedEdgeOffsetM(BedWidthM) + (Slope * Height);
        }

        /// <summary>
        /// Formats the side-slope number for a text box, without the ":1" suffix.
        /// </summary>
        /// <param name="SideSlope">Horizontal run per one vertical.</param>
        /// <returns>Display number text.</returns>
        static public string FormatSideSlope
            (
            double SideSlope
            )
        {
            double Shown = (SideSlope >= 0.0) && !double.IsNaN(SideSlope) && !double.IsInfinity(SideSlope)
                ? SideSlope
                : DEFAULT_SIDE_SLOPE;
            return Shown.ToString(SLOPE_DISPLAY_FORMAT, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Parses a side-slope text box. Zero is a vertical face. Negative numbers are rejected.
        /// </summary>
        /// <param name="Text">Text box contents.</param>
        /// <param name="SideSlope">Horizontal run per one vertical when the text is valid.</param>
        /// <returns>True when the text is zero or a positive finite number.</returns>
        static public bool TryParseSideSlope
            (
            string? Text,
            out double SideSlope
            )
        {
            SideSlope = 0.0;
            if (string.IsNullOrWhiteSpace(Text))
                return false;

            if (!double.TryParse(Text, NumberStyles.Float, CultureInfo.InvariantCulture, out SideSlope) &&
                !double.TryParse(Text, NumberStyles.Float, CultureInfo.CurrentCulture, out SideSlope))
            {
                return false;
            }

            if ((SideSlope < 0.0) || double.IsNaN(SideSlope) || double.IsInfinity(SideSlope))
                return false;

            return true;
        }

        /// <summary>
        /// Converts a blade width to feet. A missing or invalid width uses <see cref="REFERENCE_BLADE_WIDTH_FT"/>.
        /// </summary>
        /// <param name="BladeWidthMm">Blade width in millimetres.</param>
        /// <returns>Blade width in feet.</returns>
        static private double BladeWidthFt
            (
            double BladeWidthMm
            )
        {
            if ((BladeWidthMm <= 0.0) || double.IsNaN(BladeWidthMm) || double.IsInfinity(BladeWidthMm))
                return REFERENCE_BLADE_WIDTH_FT;

            return (BladeWidthMm / MM_PER_METRE) / FieldConstants.METERS_PER_FOOT;
        }
    }
}
