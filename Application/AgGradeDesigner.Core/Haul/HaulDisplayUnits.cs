using AgGradeDesigner.Core.Units;

namespace AgGradeDesigner.Core.Haul
{
    /// <summary>
    /// Converts between user display units and canonical <see cref="HaulBuilder.HaulBuilderOptions"/> units (§7.6.3–§7.6.4).
    /// </summary>
    public static class HaulDisplayUnits
    {
        /// <summary>Decimal places shown for haul settings numeric fields.</summary>
        private const int DISPLAY_DECIMAL_PLACES = 2;

        /// <summary>Millimetres per inch for depth conversions.</summary>
        private const double MM_PER_INCH = 25.4;

        /// <summary>Metres per foot for length conversions.</summary>
        private const double METRES_PER_FOOT = HaulBuilder.Constants.FEET_TO_METERS;

        /// <summary>Metres per mile for speed conversions (via mph canonical storage).</summary>
        private const double METRES_PER_MILE = 1609.344;

        /// <summary>
        /// Rounds a haul settings display value to two decimal places.
        /// </summary>
        /// <param name="Value">Raw display quantity.</param>
        /// <returns>Value rounded for settings UI display.</returns>
        public static double RoundForDisplay
            (
            double Value
            )
        {
            return Math.Round(Value, DISPLAY_DECIMAL_PLACES, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Derives grid spacing in feet from blade width millimetres (§6.2).
        /// </summary>
        /// <param name="BladeWidthMm">Blade width in millimetres.</param>
        /// <returns>Grid cell edge length in feet.</returns>
        public static double GridSizeFtFromBladeWidthMm
            (
            double BladeWidthMm
            )
        {
            double BladeWidthFt = (BladeWidthMm / MM_PER_INCH) / 12.0;
            return Math.Max(BladeWidthFt, 0.01);
        }

        /// <summary>
        /// Converts scraper capacity from display units to loose cubic yards.
        /// </summary>
        /// <param name="DisplayValue">Capacity shown in the settings UI.</param>
        /// <param name="System">Active display unit system.</param>
        /// <returns>Capacity in LCY.</returns>
        public static double ScraperCapacityLcyFromDisplay
            (
            double DisplayValue,
            DisplayUnitSystem System
            )
        {
            if (System == DisplayUnitSystem.Metric)
                return DisplayValue / HaulBuilder.Constants.M3_TO_CY;

            return DisplayValue;
        }

        /// <summary>
        /// Converts scraper capacity from LCY to display units.
        /// </summary>
        /// <param name="Lcy">Capacity in loose cubic yards.</param>
        /// <param name="System">Active display unit system.</param>
        /// <returns>Capacity for the settings UI.</returns>
        public static double ScraperCapacityToDisplay
            (
            double Lcy,
            DisplayUnitSystem System
            )
        {
            if (System == DisplayUnitSystem.Metric)
                return Lcy * HaulBuilder.Constants.M3_TO_CY;

            return Lcy;
        }

        /// <summary>
        /// Converts blade width from display units to millimetres.
        /// </summary>
        /// <param name="DisplayValue">Blade width shown in the settings UI.</param>
        /// <param name="System">Active display unit system.</param>
        /// <returns>Blade width in millimetres.</returns>
        public static double BladeWidthMmFromDisplay
            (
            double DisplayValue,
            DisplayUnitSystem System
            )
        {
            if (System == DisplayUnitSystem.Metric)
                return DisplayValue * 1000.0;

            return DisplayValue * 12.0 * MM_PER_INCH;
        }

        /// <summary>
        /// Converts blade width from millimetres to display units.
        /// </summary>
        /// <param name="Mm">Blade width in millimetres.</param>
        /// <param name="System">Active display unit system.</param>
        /// <returns>Blade width for the settings UI.</returns>
        public static double BladeWidthToDisplay
            (
            double Mm,
            DisplayUnitSystem System
            )
        {
            if (System == DisplayUnitSystem.Metric)
                return Mm / 1000.0;

            return (Mm / MM_PER_INCH) / 12.0;
        }

        /// <summary>
        /// Converts a depth from display units to millimetres.
        /// </summary>
        /// <param name="DisplayValue">Depth shown in the settings UI.</param>
        /// <param name="System">Active display unit system.</param>
        /// <returns>Depth in millimetres.</returns>
        public static double DepthMmFromDisplay
            (
            double DisplayValue,
            DisplayUnitSystem System
            )
        {
            if (System == DisplayUnitSystem.Metric)
                return DisplayValue;

            return DisplayValue * MM_PER_INCH;
        }

        /// <summary>
        /// Converts a depth from millimetres to display units.
        /// </summary>
        /// <param name="Mm">Depth in millimetres.</param>
        /// <param name="System">Active display unit system.</param>
        /// <returns>Depth for the settings UI.</returns>
        public static double DepthToDisplay
            (
            double Mm,
            DisplayUnitSystem System
            )
        {
            if (System == DisplayUnitSystem.Metric)
                return Mm;

            return Mm / MM_PER_INCH;
        }

        /// <summary>
        /// Converts a length from display units to feet.
        /// </summary>
        /// <param name="DisplayValue">Length shown in the settings UI.</param>
        /// <param name="System">Active display unit system.</param>
        /// <returns>Length in feet.</returns>
        public static double LengthFtFromDisplay
            (
            double DisplayValue,
            DisplayUnitSystem System
            )
        {
            if (System == DisplayUnitSystem.Metric)
                return DisplayValue / METRES_PER_FOOT;

            return DisplayValue;
        }

        /// <summary>
        /// Converts a length from feet to display units.
        /// </summary>
        /// <param name="Feet">Length in feet.</param>
        /// <param name="System">Active display unit system.</param>
        /// <returns>Length for the settings UI.</returns>
        public static double LengthToDisplay
            (
            double Feet,
            DisplayUnitSystem System
            )
        {
            if (System == DisplayUnitSystem.Metric)
                return Feet * METRES_PER_FOOT;

            return Feet;
        }

        /// <summary>
        /// Converts a speed from display units to miles per hour.
        /// </summary>
        /// <param name="DisplayValue">Speed shown in the settings UI.</param>
        /// <param name="System">Active display unit system.</param>
        /// <returns>Speed in mph.</returns>
        public static double SpeedMphFromDisplay
            (
            double DisplayValue,
            DisplayUnitSystem System
            )
        {
            if (System == DisplayUnitSystem.Metric)
                return DisplayValue / 1.609344;

            return DisplayValue;
        }

        /// <summary>
        /// Converts a speed from miles per hour to display units.
        /// </summary>
        /// <param name="Mph">Speed in mph.</param>
        /// <param name="System">Active display unit system.</param>
        /// <returns>Speed for the settings UI.</returns>
        public static double SpeedToDisplay
            (
            double Mph,
            DisplayUnitSystem System
            )
        {
            if (System == DisplayUnitSystem.Metric)
                return Mph * 1.609344;

            return Mph;
        }

        /// <summary>
        /// Converts a bank volume from BCY to display units for haul plan labels.
        /// </summary>
        /// <param name="VolumeBcy">Volume in bank cubic yards.</param>
        /// <param name="System">Active display unit system.</param>
        /// <returns>Volume for haul plan display.</returns>
        public static double BankVolumeBcyToDisplay
            (
            double VolumeBcy,
            DisplayUnitSystem System
            )
        {
            if (System == DisplayUnitSystem.Metric)
                return VolumeBcy / HaulBuilder.Constants.M3_TO_CY;

            return VolumeBcy;
        }
    }
}
