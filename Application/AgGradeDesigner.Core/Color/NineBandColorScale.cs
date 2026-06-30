using System.Globalization;

using AgGradeDesigner.Core;

namespace AgGradeDesigner.Core.Color
{
    /// <summary>
    /// Nine-band elevation colour scale matching OptiSurface-style plan view rendering.
    /// </summary>
    static public class NineBandColorScale
    {
        /// <summary>Fraction of the display range reserved as margin at each end for saturated bands.</summary>
        private const double BAND_END_MARGIN_FRACTION = 0.05;

        /// <summary>Fraction of the display range spanned by the seven interior band steps.</summary>
        private const double BAND_CORE_RANGE_FRACTION = 0.9;

        /// <summary>Number of equal-width steps between the interior band boundaries.</summary>
        private const double INTERIOR_BAND_STEP_COUNT = 7.0;

        /// <summary>Count of interior band index slots between the end margins.</summary>
        private const int INTERIOR_BAND_INDEX_COUNT = 6;

        /// <summary>Band index returned when the display range is degenerate.</summary>
        private const int DEGENERATE_BAND_INDEX = 4;

        /// <summary>Band index for values below the lower margin.</summary>
        private const int LOWEST_BAND_INDEX = 8;

        /// <summary>Band index for values above the upper margin.</summary>
        private const int HIGHEST_BAND_INDEX = 0;

        /// <summary>Default fraction blended toward white for contour line colours.</summary>
        private const double DEFAULT_LIGHTEN_FRACTION = 0.42;

        /// <summary>ARGB colours for bands 1 (high) through 9 (low).</summary>
        static public readonly uint[] BandColorsArgb =
        {
            0xFFB40000,
            0xFFFF0000,
            0xFFFF8000,
            0xFFFFFF00,
            0xFF00CD00,
            0xFF36F5F5,
            0xFF0066FF,
            0xFF800080,
            0xFF9A31FF
        };

        /// <summary>Human-readable labels for each colour band.</summary>
        static public readonly string[] BandNames =
        {
            "Dark red", "Red", "Orange", "Yellow", "Green", "Cyan", "Blue", "Violet", "Indigo"
        };

        /// <summary>
        /// Maps a scalar value to a band index between 0 (highest) and 8 (lowest).
        /// </summary>
        /// <param name="Value">Elevation or cut/fill value to classify.</param>
        /// <param name="VMin">Minimum value of the display range.</param>
        /// <param name="VMax">Maximum value of the display range.</param>
        /// <returns>Band index 0–8, or 4 (green) when the range is degenerate.</returns>
        static public int GetBandIndex
            (
            double Value,
            double VMin,
            double VMax
            )
        {
            double Range = VMax - VMin;
            if (Range <= 0)
                return DEGENERATE_BAND_INDEX;

            // Reserve margins at each end so extreme values map to saturated high/low bands.
            double LowerMargin = VMin + BAND_END_MARGIN_FRACTION * Range;
            double UpperMargin = VMax - BAND_END_MARGIN_FRACTION * Range;
            double StepWidth = BAND_CORE_RANGE_FRACTION * Range / INTERIOR_BAND_STEP_COUNT;

            if (Value >= UpperMargin)
                return HIGHEST_BAND_INDEX;
            if (Value < LowerMargin)
                return LOWEST_BAND_INDEX;

            int InteriorIndex = (int)Math.Floor((UpperMargin - Value) / StepWidth);
            if (InteriorIndex < 0)
                InteriorIndex = 0;
            if (InteriorIndex > INTERIOR_BAND_INDEX_COUNT)
                InteriorIndex = INTERIOR_BAND_INDEX_COUNT;
            return InteriorIndex + 1;
        }

        /// <summary>
        /// Returns the ARGB colour for a value within the display range.
        /// </summary>
        /// <param name="Value">Elevation or cut/fill value.</param>
        /// <param name="VMin">Minimum value of the display range.</param>
        /// <param name="VMax">Maximum value of the display range.</param>
        /// <returns>32-bit ARGB colour.</returns>
        static public uint GetColor
            (
            double Value,
            double VMin,
            double VMax
            )
        {
            // Delegate band selection so colour and legend share one classification path.
            return BandColorsArgb[GetBandIndex(Value, VMin, VMax)];
        }

        /// <summary>
        /// Returns a band colour blended toward white for contour line drawing.
        /// </summary>
        /// <param name="Value">Elevation or cut/fill value.</param>
        /// <param name="VMin">Minimum value of the display range.</param>
        /// <param name="VMax">Maximum value of the display range.</param>
        /// <param name="LightenFraction">Fraction of remaining distance to white (0–1).</param>
        /// <returns>Lightened 32-bit ARGB colour.</returns>
        static public uint GetLightenedColor
            (
            double Value,
            double VMin,
            double VMax,
            double LightenFraction = DEFAULT_LIGHTEN_FRACTION
            )
        {
            uint Argb = GetColor(Value, VMin, VMax);
            byte Alpha = (byte)((Argb >> 24) & 0xFF);
            byte Red = (byte)((Argb >> 16) & 0xFF);
            byte Green = (byte)((Argb >> 8) & 0xFF);
            byte Blue = (byte)(Argb & 0xFF);
            // Interpolate each channel toward white so contours remain readable over the fill.
            Red = (byte)Math.Clamp(Red + ((255 - Red) * LightenFraction), 0, 255);
            Green = (byte)Math.Clamp(Green + ((255 - Green) * LightenFraction), 0, 255);
            Blue = (byte)Math.Clamp(Blue + ((255 - Blue) * LightenFraction), 0, 255);
            return (uint)(Alpha << 24 | Red << 16 | Green << 8 | Blue);
        }

        /// <summary>
        /// Returns the ten boundary elevations separating the nine display bands.
        /// </summary>
        /// <param name="VMin">Minimum value of the display range.</param>
        /// <param name="VMax">Maximum value of the display range.</param>
        /// <returns>Descending array of band boundary values.</returns>
        static public double[] GetBandBoundaries
            (
            double VMin,
            double VMax
            )
        {
            double Range = VMax - VMin;
            if (Range <= 0)
                return new double[] { VMin, VMax };

            double LowerMargin = VMin + BAND_END_MARGIN_FRACTION * Range;
            double UpperMargin = VMax - BAND_END_MARGIN_FRACTION * Range;
            double StepWidth = BAND_CORE_RANGE_FRACTION * Range / INTERIOR_BAND_STEP_COUNT;
            double[] Boundaries = new double[10];
            Boundaries[0] = VMax;
            Boundaries[1] = UpperMargin;
            for (int Band = 0; Band < INTERIOR_BAND_INDEX_COUNT; Band++)
                Boundaries[Band + 2] = UpperMargin - (Band + 1) * StepWidth;
            Boundaries[8] = LowerMargin;
            Boundaries[9] = VMin;
            return Boundaries;
        }

        /// <summary>
        /// Formats a metre elevation for legend and label display.
        /// </summary>
        /// <param name="Value">Elevation in metres.</param>
        /// <returns>Invariant-culture string with three decimal places.</returns>
        static public string FormatMetres
            (
            double Value
            )
        {
            // Invariant formatting keeps legend labels stable across user locales.
            return Value.ToString("F3", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Formats a metre elevation as feet for legend and label display.
        /// </summary>
        /// <param name="ValueMetres">Elevation in metres.</param>
        /// <returns>Invariant-culture string with two decimal places.</returns>
        static public string FormatFeet
            (
            double ValueMetres
            )
        {
            double Feet = ValueMetres / FieldConstants.METERS_PER_FOOT;
            return Feet.ToString("F2", CultureInfo.InvariantCulture);
        }
    }
}
