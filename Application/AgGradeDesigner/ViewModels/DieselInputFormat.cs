using System.Globalization;

namespace AgGradeDesigner.ViewModels
{
    /// <summary>
    /// Text for the diesel fuel-rate, drawbar-horsepower, and price boxes.
    /// Those quantities stay in gallons per hour, horsepower, and dollars per gallon.
    /// An empty box means the value is unset, which leaves the haul diesel estimate null.
    /// </summary>
    public static class DieselInputFormat
    {
        /// <summary>Format that keeps up to five decimal places and drops trailing zeros.</summary>
        private const string DISPLAY_FORMAT = "0.#####";

        /// <summary>
        /// Formats one diesel setting for a text box.
        /// </summary>
        /// <param name="Value">Stored value. Null is an empty box.</param>
        /// <returns>Display text in the current culture, or empty when unset.</returns>
        public static string Format
            (
            double? Value
            )
        {
            if (Value is not double Number)
                return string.Empty;

            return Number.ToString(DISPLAY_FORMAT, CultureInfo.CurrentCulture);
        }

        /// <summary>
        /// Reads a diesel text box.
        /// </summary>
        /// <param name="Text">Text the user left in the box.</param>
        /// <param name="Fallback">Value kept when the text is not a number.</param>
        /// <returns>The parsed number, null when the box is empty, or <paramref name="Fallback"/> when the text cannot be read.</returns>
        public static double? Parse
            (
            string? Text,
            double? Fallback
            )
        {
            if (string.IsNullOrWhiteSpace(Text))
                return null;

            string Trimmed = Text.Trim();
            if (TryParse(Trimmed, CultureInfo.CurrentCulture, out double Parsed) ||
                TryParse(Trimmed, CultureInfo.InvariantCulture, out Parsed))
                return Parsed;

            return Fallback;
        }

        /// <summary>
        /// Parses one finite number in the given culture.
        /// </summary>
        /// <param name="Text">Trimmed text.</param>
        /// <param name="Culture">Culture whose decimal separator is accepted.</param>
        /// <param name="Parsed">The number when the text is a finite value.</param>
        /// <returns>True when the text is a finite number.</returns>
        private static bool TryParse
            (
            string Text,
            CultureInfo Culture,
            out double Parsed
            )
        {
            if (double.TryParse(Text, NumberStyles.Float, Culture, out Parsed) && double.IsFinite(Parsed))
                return true;

            Parsed = 0.0;
            return false;
        }
    }
}
