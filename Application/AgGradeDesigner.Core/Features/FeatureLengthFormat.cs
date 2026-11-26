using System.Globalization;

using AgGradeDesigner.Core;
using AgGradeDesigner.Core.Units;

namespace AgGradeDesigner.Core.Features
{
    /// <summary>
    /// Converts well pad diameters and road crown widths between metres and the active display unit.
    /// </summary>
    static public class FeatureLengthFormat
    {
        /// <summary>Display format that keeps feet and metres distinguishable without trailing zeros.</summary>
        private const string DISPLAY_FORMAT = "0.####";

        /// <summary>
        /// Short unit suffix for the active display system.
        /// </summary>
        /// <param name="System">Active display unit system.</param>
        /// <returns>"ft" or "m".</returns>
        static public string UnitLabel
            (
            DisplayUnitSystem System
            )
        {
            return System == DisplayUnitSystem.Imperial ? "ft" : "m";
        }

        /// <summary>
        /// Formats a metre length for a text box, without the unit suffix.
        /// </summary>
        /// <param name="Metres">Length in metres.</param>
        /// <param name="System">Active display unit system.</param>
        /// <returns>Display number text.</returns>
        static public string FormatDisplay
            (
            double Metres,
            DisplayUnitSystem System
            )
        {
            double Display = System == DisplayUnitSystem.Imperial
                ? Metres / FieldConstants.METERS_PER_FOOT
                : Metres;
            return Display.ToString(DISPLAY_FORMAT, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Parses a display-unit length into metres.
        /// </summary>
        /// <param name="Text">Text box contents.</param>
        /// <param name="System">Active display unit system.</param>
        /// <param name="Metres">Parsed length in metres when the text is a positive number.</param>
        /// <returns>True when the text is a positive length.</returns>
        static public bool TryParseDisplay
            (
            string? Text,
            DisplayUnitSystem System,
            out double Metres
            )
        {
            Metres = 0.0;
            if (string.IsNullOrWhiteSpace(Text))
                return false;

            if (!double.TryParse(Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double Display) &&
                !double.TryParse(Text, NumberStyles.Float, CultureInfo.CurrentCulture, out Display))
            {
                return false;
            }

            if (Display <= 0.0 || double.IsNaN(Display) || double.IsInfinity(Display))
                return false;

            Metres = System == DisplayUnitSystem.Imperial
                ? Display * FieldConstants.METERS_PER_FOOT
                : Display;
            return true;
        }
    }
}
