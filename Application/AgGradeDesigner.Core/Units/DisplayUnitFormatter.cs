using System.Globalization;

using AgGradeDesigner.Core.Color;

namespace AgGradeDesigner.Core.Units
{
    /// <summary>
    /// Formats internal SI values for display according to the selected unit system.
    /// </summary>
    static public class DisplayUnitFormatter
    {
        /// <summary>Square metres in one hectare.</summary>
        private const double SQUARE_METRES_PER_HECTARE = 10000.0;

        /// <summary>
        /// Formats a metre elevation with its unit suffix for UI labels.
        /// </summary>
        /// <param name="ValueMetres">Elevation in metres.</param>
        /// <param name="System">Display unit system.</param>
        /// <returns>Formatted value with unit, e.g. "328.08 ft" or "100.000 m".</returns>
        static public string FormatElevation
            (
            double ValueMetres,
            DisplayUnitSystem System
            )
        {
            if (System == DisplayUnitSystem.Imperial)
                return NineBandColorScale.FormatFeet(ValueMetres) + " ft";

            return NineBandColorScale.FormatMetres(ValueMetres) + " m";
        }

        /// <summary>
        /// Formats field area from acres for UI labels.
        /// </summary>
        /// <param name="Acres">Area in acres.</param>
        /// <param name="System">Display unit system.</param>
        /// <returns>Formatted area label, e.g. "Area: 12.34 acres" or "Area: 4.99 ha".</returns>
        static public string FormatAreaFromAcres
            (
            double Acres,
            DisplayUnitSystem System
            )
        {
            if (System == DisplayUnitSystem.Imperial)
            {
                return string.Format(CultureInfo.InvariantCulture, "Area: {0:F2} acres", Acres);
            }

            double Hectares = Acres * FieldConstants.SQUARE_METERS_PER_ACRE / SQUARE_METRES_PER_HECTARE;
            return string.Format(CultureInfo.InvariantCulture, "Area: {0:F2} ha", Hectares);
        }

        /// <summary>
        /// Label for import/export volume fields in the design dialog.
        /// </summary>
        /// <param name="IsImport">True for import; false for export.</param>
        /// <param name="System">Display unit system.</param>
        /// <returns>Field label including unit abbreviation.</returns>
        static public string DesignVolumeFieldLabel
            (
            bool IsImport,
            DisplayUnitSystem System
            )
        {
            string Action = IsImport ? "Import to field" : "Export from field";
            string Unit = System == DisplayUnitSystem.Imperial ? "CY" : "m³";
            return $"{Action} ({Unit})";
        }

        /// <summary>
        /// Converts stored cubic yards to the value shown in the design dialog.
        /// </summary>
        /// <param name="CubicYards">Volume in cubic yards.</param>
        /// <param name="System">Display unit system.</param>
        /// <returns>Display volume in cubic yards or cubic metres.</returns>
        static public double CubicYardsToDisplayVolume
            (
            double CubicYards,
            DisplayUnitSystem System
            )
        {
            if (System == DisplayUnitSystem.Imperial)
                return CubicYards;

            return CubicYards / FieldConstants.CUBIC_YARDS_PER_CUBIC_METER;
        }

        /// <summary>
        /// Converts a design dialog display volume back to cubic yards for storage.
        /// </summary>
        /// <param name="DisplayVolume">Volume shown in the dialog.</param>
        /// <param name="System">Display unit system.</param>
        /// <returns>Volume in cubic yards.</returns>
        static public double DisplayVolumeToCubicYards
            (
            double DisplayVolume,
            DisplayUnitSystem System
            )
        {
            if (System == DisplayUnitSystem.Imperial)
                return DisplayVolume;

            return DisplayVolume * FieldConstants.CUBIC_YARDS_PER_CUBIC_METER;
        }
    }
}
