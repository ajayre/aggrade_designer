using System.Collections.ObjectModel;

using AgGradeDesigner.Core.Haul;
using AgGradeDesigner.Core.Units;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgGradeDesigner.ViewModels
{
    /// <summary>
    /// View model for the haul configuration dialog (HAUL-SPEC §7.6).
    /// </summary>
    public sealed partial class HaulDialogViewModel : ViewModelBase
    {
        /// <summary>
        /// Identifies a haul configuration dialog content section.
        /// </summary>
        public enum HaulSectionKind
        {
            /// <summary>Scraper capacity, blade width, depth, and equipment limits.</summary>
            Equipment,

            /// <summary>Field efficiency and slope heading preferences.</summary>
            FieldOperation,

            /// <summary>Loading, haul, and turning travel parameters.</summary>
            Travel
        }

        /// <summary>Working copy of haul settings edited in the dialog session.</summary>
        private HaulSettings _SessionHaulSettings = new HaulSettings();

        /// <summary>Display unit system used for haul field labels and conversions.</summary>
        private DisplayUnitSystem _DisplayUnitSystem;

        /// <summary>
        /// Initializes haul bindings from the active project.
        /// </summary>
        /// <param name="ProjectSettings">Project haul settings to edit.</param>
        /// <param name="DisplayUnitSystem">Active display unit system.</param>
        public HaulDialogViewModel
            (
            HaulSettings ProjectSettings,
            DisplayUnitSystem DisplayUnitSystem
            )
        {
            _DisplayUnitSystem = DisplayUnitSystem;
            _SessionHaulSettings = ProjectSettings.Clone();

            HaulSectionItemViewModel EquipmentSection = new HaulSectionItemViewModel(this, HaulSectionKind.Equipment, "Equipment");
            HaulSectionItemViewModel FieldOperationSection = new HaulSectionItemViewModel(this, HaulSectionKind.FieldOperation, "Field operation");
            HaulSectionItemViewModel TravelSection = new HaulSectionItemViewModel(this, HaulSectionKind.Travel, "Travel");
            Sections = new ReadOnlyCollection<HaulSectionItemViewModel>([EquipmentSection, FieldOperationSection, TravelSection]);
            SelectedSection = HaulSectionKind.Equipment;
            EquipmentSection.IsSelected = true;

            ApplySessionToBindings();
        }

        /// <summary>
        /// Sidebar entries for choosing a haul settings section.
        /// </summary>
        public ReadOnlyCollection<HaulSectionItemViewModel> Sections { get; }

        /// <summary>
        /// Currently displayed haul settings section.
        /// </summary>
        [ObservableProperty]
        private HaulSectionKind _SelectedSection;

        /// <summary>
        /// Whether the equipment section is currently displayed.
        /// </summary>
        public bool IsEquipmentSectionSelected => SelectedSection == HaulSectionKind.Equipment;

        /// <summary>
        /// Whether the field operation section is currently displayed.
        /// </summary>
        public bool IsFieldOperationSectionSelected => SelectedSection == HaulSectionKind.FieldOperation;

        /// <summary>
        /// Whether the travel section is currently displayed.
        /// </summary>
        public bool IsTravelSectionSelected => SelectedSection == HaulSectionKind.Travel;

        /// <summary>
        /// Switches the visible haul settings section and updates sidebar selection.
        /// </summary>
        /// <param name="Kind">Section to display.</param>
        internal void SelectSection
            (
            HaulSectionKind Kind
            )
        {
            SelectedSection = Kind;
            foreach (HaulSectionItemViewModel Section in Sections)
                Section.IsSelected = Section.Kind == Kind;
            OnPropertyChanged(nameof(IsEquipmentSectionSelected));
            OnPropertyChanged(nameof(IsFieldOperationSectionSelected));
            OnPropertyChanged(nameof(IsTravelSectionSelected));
        }

        /// <summary>Scraper capacity in display units.</summary>
        [ObservableProperty]
        private double _ScraperCapacityDisplay;

        /// <summary>Blade width in display units.</summary>
        [ObservableProperty]
        private double _BladeWidthDisplay;

        /// <summary>Max depth per pass in display units.</summary>
        [ObservableProperty]
        private double _MaxDepthDisplay;

        /// <summary>Finishing tolerance in display units.</summary>
        [ObservableProperty]
        private double _ToleranceDisplay;

        /// <summary>Equipment width in display units.</summary>
        [ObservableProperty]
        private double _EquipmentWidthDisplay;

        /// <summary>Loading speed in display units.</summary>
        [ObservableProperty]
        private double _LoadingSpeedDisplay;

        /// <summary>Haul speed in display units.</summary>
        [ObservableProperty]
        private double _HaulSpeedDisplay;

        /// <summary>Turning circle in display units.</summary>
        [ObservableProperty]
        private double _TurningCircleDisplay;

        /// <summary>Swell factor (unitless).</summary>
        [ObservableProperty]
        private double _SwellFactor;

        /// <summary>Field efficiency in percent (0–100).</summary>
        [ObservableProperty]
        private double _FieldEfficiencyPercent;

        /// <summary>Whether heading follows the design main slope direction.</summary>
        [ObservableProperty]
        private bool _UseDesignMainSlopeDirection = true;

        /// <summary>Unit suffix for scraper capacity labels.</summary>
        public string CapacityUnitSuffix =>
            _DisplayUnitSystem == DisplayUnitSystem.Metric ? "m³" : "LCY";

        /// <summary>Unit suffix for blade width labels.</summary>
        public string BladeWidthUnitSuffix =>
            _DisplayUnitSystem == DisplayUnitSystem.Metric ? "m" : "ft";

        /// <summary>Unit suffix for depth labels.</summary>
        public string DepthUnitSuffix =>
            _DisplayUnitSystem == DisplayUnitSystem.Metric ? "mm" : "in";

        /// <summary>Unit suffix for length labels.</summary>
        public string LengthUnitSuffix =>
            _DisplayUnitSystem == DisplayUnitSystem.Metric ? "m" : "ft";

        /// <summary>Unit suffix for speed labels.</summary>
        public string SpeedUnitSuffix =>
            _DisplayUnitSystem == DisplayUnitSystem.Metric ? "km/h" : "mph";

        /// <summary>Whether the user accepted the edited haul settings.</summary>
        [ObservableProperty]
        private bool _Confirmed;

        /// <summary>Incremented to signal the view to close the dialog.</summary>
        [ObservableProperty]
        private int _CloseToken;

        /// <summary>
        /// Returns accepted haul settings after successful validation.
        /// </summary>
        /// <returns>Canonical haul settings written to the project on OK.</returns>
        public HaulSettings ToHaulSettings
            (
            )
        {
            return _SessionHaulSettings.Clone();
        }

        /// <summary>
        /// Accepts edited haul settings and closes the dialog.
        /// </summary>
        [RelayCommand]
        private void Ok
            (
            )
        {
            CopyBindingsToSession();
            Confirmed = true;
            CloseToken++;
        }

        /// <summary>
        /// Discards edits and closes the dialog.
        /// </summary>
        [RelayCommand]
        private void Cancel
            (
            )
        {
            Confirmed = false;
            CloseToken++;
        }

        /// <summary>
        /// Copies persisted haul settings into dialog bindings.
        /// </summary>
        private void ApplySessionToBindings
            (
            )
        {
            DisplayUnitSystem Units = _DisplayUnitSystem;
            ScraperCapacityDisplay = HaulDisplayUnits.RoundForDisplay(
                HaulDisplayUnits.ScraperCapacityToDisplay(_SessionHaulSettings.ScraperCapacityLcy, Units));
            BladeWidthDisplay = HaulDisplayUnits.RoundForDisplay(
                HaulDisplayUnits.BladeWidthToDisplay(_SessionHaulSettings.BladeWidthMm, Units));
            MaxDepthDisplay = HaulDisplayUnits.RoundForDisplay(
                HaulDisplayUnits.DepthToDisplay(_SessionHaulSettings.MaxDepthMm, Units));
            ToleranceDisplay = HaulDisplayUnits.RoundForDisplay(
                HaulDisplayUnits.DepthToDisplay(_SessionHaulSettings.ToleranceMm, Units));
            EquipmentWidthDisplay = HaulDisplayUnits.RoundForDisplay(
                HaulDisplayUnits.LengthToDisplay(_SessionHaulSettings.EquipmentWidthFt, Units));
            LoadingSpeedDisplay = HaulDisplayUnits.RoundForDisplay(
                HaulDisplayUnits.SpeedToDisplay(_SessionHaulSettings.LoadingSpeedMph, Units));
            HaulSpeedDisplay = HaulDisplayUnits.RoundForDisplay(
                HaulDisplayUnits.SpeedToDisplay(_SessionHaulSettings.HaulSpeedMph, Units));
            TurningCircleDisplay = HaulDisplayUnits.RoundForDisplay(
                HaulDisplayUnits.LengthToDisplay(_SessionHaulSettings.TurningCircleFt, Units));
            SwellFactor = HaulDisplayUnits.RoundForDisplay(_SessionHaulSettings.SwellFactor);
            FieldEfficiencyPercent = HaulDisplayUnits.RoundForDisplay(_SessionHaulSettings.FieldEfficiency * 100.0);
            UseDesignMainSlopeDirection = _SessionHaulSettings.UseDesignMainSlopeDirection;
        }

        /// <summary>
        /// Copies dialog bindings into the session haul settings snapshot.
        /// </summary>
        private void CopyBindingsToSession
            (
            )
        {
            DisplayUnitSystem Units = _DisplayUnitSystem;
            _SessionHaulSettings.ScraperCapacityLcy = HaulDisplayUnits.ScraperCapacityLcyFromDisplay(ScraperCapacityDisplay, Units);
            _SessionHaulSettings.BladeWidthMm = HaulDisplayUnits.BladeWidthMmFromDisplay(BladeWidthDisplay, Units);
            _SessionHaulSettings.MaxDepthMm = HaulDisplayUnits.DepthMmFromDisplay(MaxDepthDisplay, Units);
            _SessionHaulSettings.ToleranceMm = HaulDisplayUnits.DepthMmFromDisplay(ToleranceDisplay, Units);
            _SessionHaulSettings.EquipmentWidthFt = HaulDisplayUnits.LengthFtFromDisplay(EquipmentWidthDisplay, Units);
            _SessionHaulSettings.LoadingSpeedMph = HaulDisplayUnits.SpeedMphFromDisplay(LoadingSpeedDisplay, Units);
            _SessionHaulSettings.HaulSpeedMph = HaulDisplayUnits.SpeedMphFromDisplay(HaulSpeedDisplay, Units);
            _SessionHaulSettings.TurningCircleFt = HaulDisplayUnits.LengthFtFromDisplay(TurningCircleDisplay, Units);
            _SessionHaulSettings.SwellFactor = SwellFactor;
            _SessionHaulSettings.FieldEfficiency = FieldEfficiencyPercent / 100.0;
            _SessionHaulSettings.UseDesignMainSlopeDirection = UseDesignMainSlopeDirection;
        }
    }
}
