namespace AgGradeDesigner.ViewModels
{
    /// <summary>
    /// One selectable haul string setting entry for combo boxes in the haul dialog.
    /// </summary>
    public sealed class HaulSettingOptionViewModel
    {
        /// <summary>
        /// Creates a haul setting option row.
        /// </summary>
        /// <param name="Value">Canonical value stored in <see cref="AgGradeDesigner.Core.Haul.HaulSettings"/>.</param>
        /// <param name="DisplayName">User-visible label.</param>
        public HaulSettingOptionViewModel
            (
            string Value,
            string DisplayName
            )
        {
            this.Value = Value;
            this.DisplayName = DisplayName;
        }

        /// <summary>Canonical value persisted in haul settings.</summary>
        public string Value { get; }

        /// <summary>Display label shown in the combo box.</summary>
        public string DisplayName { get; }
    }
}
