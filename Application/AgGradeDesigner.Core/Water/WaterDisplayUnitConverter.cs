using AgGradeDesigner.Core.Units;

namespace AgGradeDesigner.Core.Water
{
    /// <summary>
    /// Converts ponding rainfall between canonical millimetres and display units (WATER-SPEC §7.1.6–§7.1.8).
    /// </summary>
    public static class WaterDisplayUnitConverter
    {
        /// <summary>
        /// Converts canonical millimetres to an unsigned integer in the user's display units.
        /// </summary>
        /// <param name="RainfallMm">Canonical rainfall depth in millimetres.</param>
        /// <param name="System">Active display unit system.</param>
        /// <returns>Whole-number display value in millimetres or inches.</returns>
        public static uint ToDisplayRainfall
            (
            uint RainfallMm,
            DisplayUnitSystem System
            )
        {
            if (System == DisplayUnitSystem.Imperial)
                return ToDisplayRainfallInches(RainfallMm);

            return RainfallMm;
        }

        /// <summary>
        /// Converts a display-unit rainfall integer to canonical millimetres.
        /// </summary>
        /// <param name="DisplayValue">Whole-number rainfall in display units.</param>
        /// <param name="System">Active display unit system.</param>
        /// <returns>Canonical millimetres rounded to the nearest whole millimetre.</returns>
        public static uint FromDisplayRainfall
            (
            uint DisplayValue,
            DisplayUnitSystem System
            )
        {
            if (System == DisplayUnitSystem.Imperial)
            {
                double Millimetres = DisplayValue * WaterConstants.MM_PER_INCH;
                return (uint)Math.Max(0, Math.Floor(Millimetres + 1e-9));
            }

            return DisplayValue;
        }

        /// <summary>
        /// Returns the unit suffix for ponding rainfall labels.
        /// </summary>
        /// <param name="System">Active display unit system.</param>
        /// <returns><c>mm</c> or <c>in</c>.</returns>
        public static string RainfallUnitSuffix
            (
            DisplayUnitSystem System
            )
        {
            if (System == DisplayUnitSystem.Imperial)
                return "in";

            return "mm";
        }

        /// <summary>
        /// Converts canonical millimetres to the nearest whole inch for imperial display.
        /// </summary>
        /// <param name="RainfallMm">Canonical rainfall depth in millimetres.</param>
        /// <returns>Nearest whole inch value.</returns>
        private static uint ToDisplayRainfallInches
            (
            uint RainfallMm
            )
        {
            double Inches = RainfallMm / WaterConstants.MM_PER_INCH;
            return (uint)Math.Max(0, Math.Round(Inches, MidpointRounding.AwayFromZero));
        }
    }
}
