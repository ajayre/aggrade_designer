using AgGradeDesigner.Core.Units;

namespace AgGradeDesigner.Core.Haul
{
    /// <summary>
    /// Converts between user display units and canonical <see cref="HaulBuilder.HaulBuilderOptions"/> units (§7.6.3–§7.6.4).
    /// </summary>
    public static class HaulDisplayUnits
    {
        /// <summary>Decimal places shown for non-distance haul settings numeric fields.</summary>
        private const int DISPLAY_DECIMAL_PLACES = 2;

        /// <summary>Decimal places allowed for imperial distance fields (inches and feet).</summary>
        private const int DISTANCE_IMPERIAL_DECIMAL_PLACES = 5;

        /// <summary>Decimal places allowed for millimetre distance fields.</summary>
        private const int DISTANCE_MM_DECIMAL_PLACES = 0;

        /// <summary>Decimal places allowed for metre distance fields.</summary>
        private const int DISTANCE_METRE_DECIMAL_PLACES = 5;

        /// <summary>Millimetres per inch for depth and blade-width conversions.</summary>
        private const double MM_PER_INCH = 25.4;

        /// <summary>Millimetres per metre for blade-width metric conversion.</summary>
        private const double MM_PER_METRE = 1000.0;

        /// <summary>Inches per foot for blade-width imperial conversion.</summary>
        private const double INCHES_PER_FOOT = 12.0;

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
        /// Rounds a distance display value to the allowed unit precision
        /// (5 dp for inches/feet/metres, 0 dp for millimetres).
        /// </summary>
        /// <param name="Value">Raw distance in display units.</param>
        /// <param name="System">Active display unit system.</param>
        /// <param name="IsMillimetreUnit">True when the display unit is millimetres.</param>
        /// <returns>Distance rounded for entry, storage conversion, and UI display.</returns>
        public static double RoundDistanceForDisplay
            (
            double Value,
            DisplayUnitSystem System,
            bool IsMillimetreUnit
            )
        {
            int Places;
            if (System == DisplayUnitSystem.Metric)
            {
                Places = IsMillimetreUnit
                    ? DISTANCE_MM_DECIMAL_PLACES
                    : DISTANCE_METRE_DECIMAL_PLACES;
            }
            else
            {
                Places = DISTANCE_IMPERIAL_DECIMAL_PLACES;
            }

            return Math.Round(Value, Places, MidpointRounding.AwayFromZero);
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
        /// Normalizes to the allowed distance precision first so stored and calculated
        /// millimetres match the value the user entered.
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
            // intent: clamp to allowed entry precision before converting so engine and persistence keep user intent
            double Normalized = RoundDistanceForDisplay(DisplayValue, System, IsMillimetreUnit: false);
            if (System == DisplayUnitSystem.Metric)
                return Normalized * MM_PER_METRE;

            return Normalized * INCHES_PER_FOOT * MM_PER_INCH;
        }

        /// <summary>
        /// Converts blade width from millimetres to display units at the allowed distance precision.
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
            double Raw = System == DisplayUnitSystem.Metric
                ? Mm / MM_PER_METRE
                : (Mm / MM_PER_INCH) / INCHES_PER_FOOT;
            return RoundDistanceForDisplay(Raw, System, IsMillimetreUnit: false);
        }

        /// <summary>
        /// Converts a depth from display units to millimetres.
        /// Normalizes to the allowed display precision first so stored and calculated
        /// millimetres match the value the user entered.
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
            // intent: clamp to allowed entry precision before converting so engine and persistence keep user intent
            bool IsMillimetreUnit = System == DisplayUnitSystem.Metric;
            double Normalized = RoundDistanceForDisplay(DisplayValue, System, IsMillimetreUnit);
            if (IsMillimetreUnit)
                return Normalized;

            return Normalized * MM_PER_INCH;
        }

        /// <summary>
        /// Converts a depth from millimetres to display units at the allowed entry precision.
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
            bool IsMillimetreUnit = System == DisplayUnitSystem.Metric;
            double Raw = IsMillimetreUnit
                ? Mm
                : Mm / MM_PER_INCH;
            return RoundDistanceForDisplay(Raw, System, IsMillimetreUnit);
        }

        /// <summary>
        /// Converts a length from display units to feet.
        /// Normalizes to the allowed distance precision first so stored and calculated
        /// feet match the value the user entered.
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
            // intent: clamp to allowed entry precision before converting so engine and persistence keep user intent
            double Normalized = RoundDistanceForDisplay(DisplayValue, System, IsMillimetreUnit: false);
            if (System == DisplayUnitSystem.Metric)
                return Normalized / METRES_PER_FOOT;

            return Normalized;
        }

        /// <summary>
        /// Converts a length from feet to display units at the allowed distance precision.
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
            double Raw = System == DisplayUnitSystem.Metric
                ? Feet * METRES_PER_FOOT
                : Feet;
            return RoundDistanceForDisplay(Raw, System, IsMillimetreUnit: false);
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
