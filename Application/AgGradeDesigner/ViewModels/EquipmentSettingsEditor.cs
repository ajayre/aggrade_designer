using AgGradeDesigner.Core.Haul;
using AgGradeDesigner.Core.Units;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AgGradeDesigner.ViewModels
{
    /// <summary>
    /// Editable equipment and pass-limit values for the settings dialog.
    /// Canonical values stay in feet and millimetres. The text boxes follow the
    /// display units selected in the same dialog.
    /// </summary>
    public sealed partial class EquipmentSettingsEditor : ViewModelBase
    {
        /// <summary>
        /// Equipment values in canonical units, independent of the text boxes.
        /// </summary>
        private readonly HaulSettings _Canonical = new HaulSettings();

        /// <summary>
        /// Units the text boxes are currently showing.
        /// </summary>
        private DisplayUnitSystem _Units = DisplayUnitSystem.Imperial;

        /// <summary>
        /// True while the text boxes are being filled from <see cref="_Canonical"/>.
        /// </summary>
        private bool _SuppressWrite;

        /// <summary>
        /// True after the user edits a box. Unit changes and OK then read the boxes.
        /// An untouched section keeps the stored values, so display rounding cannot rewrite them.
        /// </summary>
        private bool _Edited;

        /// <summary>
        /// Scraper capacity in the active display units.
        /// </summary>
        [ObservableProperty]
        private double _ScraperCapacityDisplay;

        /// <summary>
        /// Blade width in the active display units.
        /// </summary>
        [ObservableProperty]
        private double _BladeWidthDisplay;

        /// <summary>
        /// Maximum cut depth in the active display units.
        /// </summary>
        [ObservableProperty]
        private double _MaxDepthDisplay;

        /// <summary>
        /// Finishing tolerance in the active display units.
        /// </summary>
        [ObservableProperty]
        private double _ToleranceDisplay;

        /// <summary>
        /// Equipment width in the active display units.
        /// </summary>
        [ObservableProperty]
        private double _EquipmentWidthDisplay;

        /// <summary>
        /// Equipment length in the active display units.
        /// </summary>
        [ObservableProperty]
        private double _EquipmentLengthDisplay;

        /// <summary>
        /// Turning diameter in the active display units.
        /// </summary>
        [ObservableProperty]
        private double _TurningCircleDisplay;

        /// <summary>
        /// True when a minimum blade-down length is required.
        /// </summary>
        [ObservableProperty]
        private bool _EnableMinimumWorkLength;

        /// <summary>
        /// Minimum blade-down length in the active display units.
        /// </summary>
        [ObservableProperty]
        private double _MinimumWorkLengthDisplay;

        /// <summary>
        /// True when the minimum-work-length box can be edited.
        /// </summary>
        public bool IsMinimumWorkLengthEnabled => EnableMinimumWorkLength;

        /// <summary>
        /// Scraper capacity unit shown beside the box.
        /// </summary>
        public string CapacityUnitSuffix =>
            _Units == DisplayUnitSystem.Metric ? "m³" : "LCY";

        /// <summary>
        /// Blade width unit shown beside the box.
        /// </summary>
        public string BladeWidthUnitSuffix =>
            _Units == DisplayUnitSystem.Metric ? "m" : "ft";

        /// <summary>
        /// Depth and tolerance unit shown beside those boxes.
        /// </summary>
        public string DepthUnitSuffix =>
            _Units == DisplayUnitSystem.Metric ? "mm" : "in";

        /// <summary>
        /// Length unit shown beside the size and turning boxes.
        /// </summary>
        public string LengthUnitSuffix =>
            _Units == DisplayUnitSystem.Metric ? "m" : "ft";

        /// <summary>
        /// Loads equipment values and shows them in the given units.
        /// </summary>
        /// <param name="Source">Project or user equipment values.</param>
        /// <param name="Units">Units selected in the settings dialog.</param>
        public void Load
            (
            HaulSettings Source,
            DisplayUnitSystem Units
            )
        {
            _Canonical.CopyEquipmentFrom(Source);
            _Units = Units;
            _Edited = false;
            PushToDisplay();
        }

        /// <summary>
        /// Converts the current text-box values into the newly selected units.
        /// </summary>
        /// <param name="Units">Units the user just selected.</param>
        public void UseDisplayUnits
            (
            DisplayUnitSystem Units
            )
        {
            if (Units == _Units)
                return;

            // intent: a typed number is in the old units; an untouched section still has the stored value
            if (_Edited)
                PullFromDisplay();

            _Units = Units;
            PushToDisplay();
            NotifyUnitSuffixes();
        }

        /// <summary>
        /// Writes the text-box values onto a haul settings instance.
        /// Only equipment and pass-limit fields are changed.
        /// </summary>
        /// <param name="Target">Project or user settings to update.</param>
        public void ApplyTo
            (
            HaulSettings Target
            )
        {
            if (_Edited)
                PullFromDisplay();

            Target.CopyEquipmentFrom(_Canonical);
        }

        /// <summary>
        /// Enables or disables the minimum-work-length box when the toggle changes.
        /// </summary>
        /// <param name="Value">New toggle state.</param>
        partial void OnEnableMinimumWorkLengthChanged
            (
            bool Value
            )
        {
            MarkEdited();
            OnPropertyChanged(nameof(IsMinimumWorkLengthEnabled));
        }

        /// <summary>
        /// Records a user edit of the scraper capacity box.
        /// </summary>
        /// <param name="Value">New displayed capacity.</param>
        partial void OnScraperCapacityDisplayChanged
            (
            double Value
            )
        {
            MarkEdited();
        }

        /// <summary>
        /// Records a user edit of the blade width box.
        /// </summary>
        /// <param name="Value">New displayed blade width.</param>
        partial void OnBladeWidthDisplayChanged
            (
            double Value
            )
        {
            MarkEdited();
        }

        /// <summary>
        /// Records a user edit of the max-depth box.
        /// </summary>
        /// <param name="Value">New displayed depth.</param>
        partial void OnMaxDepthDisplayChanged
            (
            double Value
            )
        {
            MarkEdited();
        }

        /// <summary>
        /// Records a user edit of the tolerance box.
        /// </summary>
        /// <param name="Value">New displayed tolerance.</param>
        partial void OnToleranceDisplayChanged
            (
            double Value
            )
        {
            MarkEdited();
        }

        /// <summary>
        /// Records a user edit of the equipment width box.
        /// </summary>
        /// <param name="Value">New displayed width.</param>
        partial void OnEquipmentWidthDisplayChanged
            (
            double Value
            )
        {
            MarkEdited();
        }

        /// <summary>
        /// Records a user edit of the equipment length box.
        /// </summary>
        /// <param name="Value">New displayed length.</param>
        partial void OnEquipmentLengthDisplayChanged
            (
            double Value
            )
        {
            MarkEdited();
        }

        /// <summary>
        /// Records a user edit of the turning diameter box.
        /// </summary>
        /// <param name="Value">New displayed turning diameter.</param>
        partial void OnTurningCircleDisplayChanged
            (
            double Value
            )
        {
            MarkEdited();
        }

        /// <summary>
        /// Records a user edit of the minimum-work-length box.
        /// </summary>
        /// <param name="Value">New displayed length.</param>
        partial void OnMinimumWorkLengthDisplayChanged
            (
            double Value
            )
        {
            MarkEdited();
        }

        /// <summary>
        /// Marks the section edited unless the boxes are being filled from stored values.
        /// </summary>
        private void MarkEdited
            (
            )
        {
            if (!_SuppressWrite)
                _Edited = true;
        }

        /// <summary>
        /// Copies the canonical equipment values into the text boxes.
        /// </summary>
        private void PushToDisplay
            (
            )
        {
            _SuppressWrite = true;
            ScraperCapacityDisplay = HaulDisplayUnits.ScraperCapacityToDisplay(_Canonical.ScraperCapacityLcy, _Units);
            BladeWidthDisplay = HaulDisplayUnits.BladeWidthToDisplay(_Canonical.BladeWidthMm, _Units);
            MaxDepthDisplay = HaulDisplayUnits.DepthToDisplay(_Canonical.MaxDepthMm, _Units);
            ToleranceDisplay = HaulDisplayUnits.DepthToDisplay(_Canonical.ToleranceMm, _Units);
            EquipmentWidthDisplay = HaulDisplayUnits.LengthToDisplay(_Canonical.EquipmentWidthFt, _Units);
            EquipmentLengthDisplay = HaulDisplayUnits.LengthToDisplay(_Canonical.EquipmentLengthFt, _Units);
            TurningCircleDisplay = HaulDisplayUnits.LengthToDisplay(_Canonical.TurningCircleFt, _Units);
            EnableMinimumWorkLength = _Canonical.EnableMinimumWorkLength;
            MinimumWorkLengthDisplay = HaulDisplayUnits.LengthToDisplay(_Canonical.MinimumWorkLengthFt, _Units);
            _SuppressWrite = false;
            NotifyUnitSuffixes();
        }

        /// <summary>
        /// Copies the text boxes into the canonical equipment values.
        /// </summary>
        private void PullFromDisplay
            (
            )
        {
            if (_SuppressWrite)
                return;

            _Canonical.ScraperCapacityLcy = HaulDisplayUnits.ScraperCapacityLcyFromDisplay(ScraperCapacityDisplay, _Units);
            _Canonical.BladeWidthMm = HaulDisplayUnits.BladeWidthMmFromDisplay(BladeWidthDisplay, _Units);
            _Canonical.MaxDepthMm = HaulDisplayUnits.DepthMmFromDisplay(MaxDepthDisplay, _Units);
            _Canonical.ToleranceMm = HaulDisplayUnits.DepthMmFromDisplay(ToleranceDisplay, _Units);
            _Canonical.EquipmentWidthFt = HaulDisplayUnits.LengthFtFromDisplay(EquipmentWidthDisplay, _Units);
            _Canonical.EquipmentLengthFt = HaulDisplayUnits.LengthFtFromDisplay(EquipmentLengthDisplay, _Units);
            _Canonical.TurningCircleFt = HaulDisplayUnits.LengthFtFromDisplay(TurningCircleDisplay, _Units);
            _Canonical.EnableMinimumWorkLength = EnableMinimumWorkLength;
            _Canonical.MinimumWorkLengthFt = HaulDisplayUnits.LengthFtFromDisplay(MinimumWorkLengthDisplay, _Units);
        }

        /// <summary>
        /// Refreshes the unit labels after the display system changes.
        /// </summary>
        private void NotifyUnitSuffixes
            (
            )
        {
            OnPropertyChanged(nameof(CapacityUnitSuffix));
            OnPropertyChanged(nameof(BladeWidthUnitSuffix));
            OnPropertyChanged(nameof(DepthUnitSuffix));
            OnPropertyChanged(nameof(LengthUnitSuffix));
        }
    }
}
