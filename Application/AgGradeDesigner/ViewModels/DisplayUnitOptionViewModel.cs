using AgGradeDesigner.Core.Units;

namespace AgGradeDesigner.ViewModels
{
    /// <summary>
    /// One selectable display unit system entry for the settings dialog combo box.
    /// </summary>
    public sealed class DisplayUnitOptionViewModel
    {
        /// <summary>
        /// Creates a display unit option row.
        /// </summary>
        /// <param name="System">Unit system value.</param>
        /// <param name="DisplayName">User-visible label.</param>
        public DisplayUnitOptionViewModel
            (
            DisplayUnitSystem System,
            string DisplayName
            )
        {
            this.System = System;
            this.DisplayName = DisplayName;
        }

        /// <summary>Unit system stored in settings.</summary>
        public DisplayUnitSystem System { get; }

        /// <summary>Display label shown in the combo box.</summary>
        public string DisplayName { get; }
    }
}
