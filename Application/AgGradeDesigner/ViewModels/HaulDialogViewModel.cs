using AgGradeDesigner.Core.Haul;
using AgGradeDesigner.Core.Units;
using AgGradeDesigner.Services;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgGradeDesigner.ViewModels
{
    /// <summary>
    /// View model for the haul configuration dialog (HAUL-SPEC §7.6).
    /// </summary>
    public sealed partial class HaulDialogViewModel : ViewModelBase
    {
        /// <summary>User settings service supplying persisted haul configuration.</summary>
        private readonly UserSettingsService _SettingsService;

        /// <summary>Working copy of haul settings edited in the dialog session.</summary>
        private HaulSettings _SessionHaulSettings = new HaulSettings();

        /// <summary>Display unit system used for haul field labels and conversions.</summary>
        private DisplayUnitSystem _DisplayUnitSystem;

        /// <summary>
        /// Initializes haul bindings from persisted settings.
        /// </summary>
        /// <param name="SettingsService">User settings service; defaults to the app singleton.</param>
        public HaulDialogViewModel
            (
            UserSettingsService? SettingsService = null
            )
        {
            _SettingsService = SettingsService ?? UserSettingsService.Instance;
            _DisplayUnitSystem = _SettingsService.ActiveDisplayUnitsSettings.System;
            _SessionHaulSettings = _SettingsService.LoadHaulSettingsForDialog();
            ApplySessionToBindings();
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
        /// Accepts edited haul settings, persists them, and closes the dialog.
        /// </summary>
        [RelayCommand]
        private void Ok
            (
            )
        {
            CopyBindingsToSession();
            _SettingsService.SaveHaulSettings(_SessionHaulSettings);
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
