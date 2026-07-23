using System.Collections.ObjectModel;

using AgGradeDesigner.Core.Haul;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Units;
using AgGradeDesigner.Helpers;
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
        /// <summary>Multiplier applied when converting lane overlap fraction to percent display.</summary>
        private const double LANE_OVERLAP_FRACTION_TO_PERCENT = 100.0;

        /// <summary>Maximum lane overlap fraction accepted by HaulBuilder.</summary>
        private const double MAX_LANE_OVERLAP_FRACTION = 0.9;

        /// <summary>
        /// Identifies a haul configuration dialog content section.
        /// </summary>
        public enum HaulSectionKind
        {
            /// <summary>Scraper capacity, blade width, depth, equipment size, and turning diameter.</summary>
            Equipment,

            /// <summary>Field efficiency, swell, grid size, hop/overlap, sequencing, and slope heading.</summary>
            FieldOperation,

            /// <summary>Loading, haul, and other travel speed parameters.</summary>
            Travel,

            /// <summary>Center-pivot analysis parameters.</summary>
            Pivot
        }

        /// <summary>Working copy of haul settings edited in the dialog session.</summary>
        private HaulSettings _SessionHaulSettings = new HaulSettings();

        /// <summary>Display unit system used for haul field labels and conversions.</summary>
        private DisplayUnitSystem _DisplayUnitSystem;

        /// <summary>Active project used to validate pivot centre against the field boundary.</summary>
        private readonly ProjectDocument? _Project;

        /// <summary>
        /// Initializes haul bindings from the active project.
        /// </summary>
        /// <param name="ProjectSettings">Project haul settings to edit.</param>
        /// <param name="DisplayUnitSystem">Active display unit system.</param>
        /// <param name="Project">Optional project used to validate pivot centre on OK &amp; Run.</param>
        public HaulDialogViewModel
            (
            HaulSettings ProjectSettings,
            DisplayUnitSystem DisplayUnitSystem,
            ProjectDocument? Project = null
            )
        {
            _DisplayUnitSystem = DisplayUnitSystem;
            _SessionHaulSettings = ProjectSettings.Clone();
            _Project = Project;

            TransportModeOptions = new ReadOnlyCollection<HaulSettingOptionViewModel>(
            [
                new HaulSettingOptionViewModel("poisson", "Poisson"),
                new HaulSettingOptionViewModel("mincostflow", "Min-cost flow")
            ]);

            BalanceModeOptions = new ReadOnlyCollection<HaulSettingOptionViewModel>(
            [
                new HaulSettingOptionViewModel("check", "Check"),
                new HaulSettingOptionViewModel("adjust", "Adjust")
            ]);

            HaulSectionItemViewModel EquipmentSection = new HaulSectionItemViewModel(this, HaulSectionKind.Equipment, "Equipment");
            HaulSectionItemViewModel FieldOperationSection = new HaulSectionItemViewModel(this, HaulSectionKind.FieldOperation, "Field operation");
            HaulSectionItemViewModel TravelSection = new HaulSectionItemViewModel(this, HaulSectionKind.Travel, "Travel");
            HaulSectionItemViewModel PivotSection = new HaulSectionItemViewModel(this, HaulSectionKind.Pivot, "Pivot");
            Sections = new ReadOnlyCollection<HaulSectionItemViewModel>(
                [EquipmentSection, FieldOperationSection, TravelSection, PivotSection]);
            SelectedSection = HaulSectionKind.Equipment;
            EquipmentSection.IsSelected = true;

            ApplySessionToBindings();
        }

        /// <summary>
        /// Sidebar entries for choosing a haul settings section.
        /// </summary>
        public ReadOnlyCollection<HaulSectionItemViewModel> Sections { get; }

        /// <summary>
        /// Transport mode choices shown in the field operation section combo box.
        /// </summary>
        public ReadOnlyCollection<HaulSettingOptionViewModel> TransportModeOptions { get; }

        /// <summary>
        /// Balance mode choices shown in the field operation section combo box.
        /// </summary>
        public ReadOnlyCollection<HaulSettingOptionViewModel> BalanceModeOptions { get; }

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
        /// Whether the pivot section is currently displayed.
        /// </summary>
        public bool IsPivotSectionSelected => SelectedSection == HaulSectionKind.Pivot;

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
            OnPropertyChanged(nameof(IsPivotSectionSelected));
        }

        /// <summary>Scraper capacity in display units.</summary>
        [ObservableProperty]
        private double _ScraperCapacityDisplay;

        /// <summary>Blade width in display units.</summary>
        [ObservableProperty]
        private double _BladeWidthDisplay;

        /// <summary>Haul grid cell size in display units.</summary>
        [ObservableProperty]
        private double _GridSizeDisplay;

        /// <summary>Max depth per pass in display units.</summary>
        [ObservableProperty]
        private double _MaxDepthDisplay;

        /// <summary>Finishing tolerance in display units.</summary>
        [ObservableProperty]
        private double _ToleranceDisplay;

        /// <summary>Maximum multi-drop hop distance in display units.</summary>
        [ObservableProperty]
        private double _MaxDropHopDisplay;

        /// <summary>Minimum load fill percentage before stopping extra hops.</summary>
        [ObservableProperty]
        private double _MinLoadFillPct;

        /// <summary>Equipment width in display units.</summary>
        [ObservableProperty]
        private double _EquipmentWidthDisplay;

        /// <summary>Equipment length in display units.</summary>
        [ObservableProperty]
        private double _EquipmentLengthDisplay;

        /// <summary>Lane overlap in percent (0–90).</summary>
        [ObservableProperty]
        private double _LaneOverlapPercent;

        /// <summary>Loading speed in display units.</summary>
        [ObservableProperty]
        private double _LoadingSpeedDisplay;

        /// <summary>Haul speed in display units.</summary>
        [ObservableProperty]
        private double _HaulSpeedDisplay;

        /// <summary>Dumping speed in display units.</summary>
        [ObservableProperty]
        private double _DumpingSpeedDisplay;

        /// <summary>Return speed in display units.</summary>
        [ObservableProperty]
        private double _ReturnSpeedDisplay;

        /// <summary>Turn speed in display units.</summary>
        [ObservableProperty]
        private double _TurnSpeedDisplay;

        /// <summary>Grade/compact speed in display units.</summary>
        [ObservableProperty]
        private double _GradeSpeedDisplay;

        /// <summary>Turning diameter in display units.</summary>
        [ObservableProperty]
        private double _TurningCircleDisplay;

        /// <summary>Acceleration phase duration in seconds.</summary>
        [ObservableProperty]
        private double _AccelTimeS;

        /// <summary>Deceleration phase duration in seconds.</summary>
        [ObservableProperty]
        private double _DecelTimeS;

        /// <summary>Heading-change threshold treated as a turn, in degrees.</summary>
        [ObservableProperty]
        private double _TurnThresholdDeg;

        /// <summary>Swell factor (unitless).</summary>
        [ObservableProperty]
        private double _SwellFactor;

        /// <summary>Field efficiency in percent (0–100).</summary>
        [ObservableProperty]
        private double _FieldEfficiencyPercent;

        /// <summary>Cut zone count for sequencing.</summary>
        [ObservableProperty]
        private int _Zones;

        /// <summary>Whether heading comes from the open project design main slope direction.</summary>
        [ObservableProperty]
        private bool _UseDesignMainSlopeDirection = true;

        /// <summary>User heading in degrees when <see cref="UseDesignMainSlopeDirection"/> is false.</summary>
        [ObservableProperty]
        private double _HeadingDeg;

        /// <summary>Selected transport mode combo box entry.</summary>
        [ObservableProperty]
        private HaulSettingOptionViewModel? _SelectedTransportModeOption;

        /// <summary>Selected balance mode combo box entry.</summary>
        [ObservableProperty]
        private HaulSettingOptionViewModel? _SelectedBalanceModeOption;

        /// <summary>When true, flow-aligned strip growth is enabled.</summary>
        [ObservableProperty]
        private bool _FlowAlignedStripGrowth = true;

        /// <summary>When true, grade-return passes are enabled.</summary>
        [ObservableProperty]
        private bool _GradeReturnPasses = true;

        /// <summary>Whether center-pivot analysis is enabled.</summary>
        [ObservableProperty]
        private bool _Pivot;

        /// <summary>Pivot centre latitude in decimal degrees (WGS-84).</summary>
        [ObservableProperty]
        private double _PivotLatitudeDeg;

        /// <summary>Pivot centre longitude in decimal degrees (WGS-84).</summary>
        [ObservableProperty]
        private double _PivotLongitudeDeg;

        /// <summary>Pivot arm wheelbase in display units.</summary>
        [ObservableProperty]
        private double _PivotWheelbaseDisplay;

        /// <summary>Extra pivot arm clearance per side in display units.</summary>
        [ObservableProperty]
        private double _PivotClearanceDisplay;

        /// <summary>Pivot move cost in detour-equivalent passes.</summary>
        [ObservableProperty]
        private double _PivotMoveCost;

        /// <summary>Whether custom heading can be edited.</summary>
        public bool IsCustomHeadingEnabled => !UseDesignMainSlopeDirection;

        /// <summary>Whether pivot location and arm fields can be edited.</summary>
        public bool IsPivotSettingsEnabled => Pivot;

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

        /// <summary>When true, the caller should run haul generation after the dialog closes.</summary>
        [ObservableProperty]
        private bool _RunHaulAfterClose;

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
        /// Commits edited haul settings and closes the dialog without running haul generation.
        /// </summary>
        [RelayCommand]
        private void Ok
            (
            )
        {
            CopyBindingsToSession();
            Confirmed = true;
            RunHaulAfterClose = false;
            CloseToken++;
        }

        /// <summary>
        /// Commits edited haul settings, closes the dialog, and requests haul generation.
        /// </summary>
        [RelayCommand]
        private void OkAndRun
            (
            )
        {
            CopyBindingsToSession();
            if (!HaulPivotOverlay.TryValidatePivotCenterForRun(
                    _SessionHaulSettings,
                    _Project,
                    out string ErrorMessage))
            {
                _ = DialogService.ShowMessageAsync("Haul settings", ErrorMessage);
                return;
            }

            Confirmed = true;
            RunHaulAfterClose = true;
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
        /// Updates custom-heading field availability when the design-heading toggle changes.
        /// </summary>
        /// <param name="Value">New toggle value.</param>
        partial void OnUseDesignMainSlopeDirectionChanged
            (
            bool Value
            )
        {
            OnPropertyChanged(nameof(IsCustomHeadingEnabled));
        }

        /// <summary>
        /// Updates pivot field availability when pivot analysis is toggled.
        /// </summary>
        /// <param name="Value">New toggle value.</param>
        partial void OnPivotChanged
            (
            bool Value
            )
        {
            OnPropertyChanged(nameof(IsPivotSettingsEnabled));
        }

        /// <summary>
        /// Copies the selected transport mode option into session state when the combo selection changes.
        /// </summary>
        /// <param name="Value">New transport mode option.</param>
        partial void OnSelectedTransportModeOptionChanged
            (
            HaulSettingOptionViewModel? Value
            )
        {
            if (Value != null)
                _SessionHaulSettings.TransportMode = Value.Value;
        }

        /// <summary>
        /// Copies the selected balance mode option into session state when the combo selection changes.
        /// </summary>
        /// <param name="Value">New balance mode option.</param>
        partial void OnSelectedBalanceModeOptionChanged
            (
            HaulSettingOptionViewModel? Value
            )
        {
            if (Value != null)
                _SessionHaulSettings.BalanceMode = Value.Value;
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
            // intent: distance fields keep full allowed precision (5 dp imperial / metres, 0 dp mm)
            BladeWidthDisplay = HaulDisplayUnits.BladeWidthToDisplay(_SessionHaulSettings.BladeWidthMm, Units);
            GridSizeDisplay = HaulDisplayUnits.LengthToDisplay(_SessionHaulSettings.GridSizeFt, Units);
            MaxDepthDisplay = HaulDisplayUnits.DepthToDisplay(_SessionHaulSettings.MaxDepthMm, Units);
            ToleranceDisplay = HaulDisplayUnits.DepthToDisplay(_SessionHaulSettings.ToleranceMm, Units);
            MaxDropHopDisplay = HaulDisplayUnits.LengthToDisplay(_SessionHaulSettings.MaxDropHopFt, Units);
            MinLoadFillPct = HaulDisplayUnits.RoundForDisplay(_SessionHaulSettings.MinLoadFillPct);
            EquipmentWidthDisplay = HaulDisplayUnits.LengthToDisplay(_SessionHaulSettings.EquipmentWidthFt, Units);
            EquipmentLengthDisplay = HaulDisplayUnits.LengthToDisplay(_SessionHaulSettings.EquipmentLengthFt, Units);
            LaneOverlapPercent = HaulDisplayUnits.RoundForDisplay(
                _SessionHaulSettings.LaneOverlap * LANE_OVERLAP_FRACTION_TO_PERCENT);
            LoadingSpeedDisplay = HaulDisplayUnits.RoundForDisplay(
                HaulDisplayUnits.SpeedToDisplay(_SessionHaulSettings.LoadingSpeedMph, Units));
            HaulSpeedDisplay = HaulDisplayUnits.RoundForDisplay(
                HaulDisplayUnits.SpeedToDisplay(_SessionHaulSettings.HaulSpeedMph, Units));
            DumpingSpeedDisplay = HaulDisplayUnits.RoundForDisplay(
                HaulDisplayUnits.SpeedToDisplay(_SessionHaulSettings.DumpingSpeedMph, Units));
            ReturnSpeedDisplay = HaulDisplayUnits.RoundForDisplay(
                HaulDisplayUnits.SpeedToDisplay(_SessionHaulSettings.ReturnSpeedMph, Units));
            TurnSpeedDisplay = HaulDisplayUnits.RoundForDisplay(
                HaulDisplayUnits.SpeedToDisplay(_SessionHaulSettings.TurnSpeedMph, Units));
            GradeSpeedDisplay = HaulDisplayUnits.RoundForDisplay(
                HaulDisplayUnits.SpeedToDisplay(_SessionHaulSettings.GradeSpeedMph, Units));
            TurningCircleDisplay = HaulDisplayUnits.LengthToDisplay(_SessionHaulSettings.TurningCircleFt, Units);
            AccelTimeS = HaulDisplayUnits.RoundForDisplay(_SessionHaulSettings.AccelTimeS);
            DecelTimeS = HaulDisplayUnits.RoundForDisplay(_SessionHaulSettings.DecelTimeS);
            TurnThresholdDeg = HaulDisplayUnits.RoundForDisplay(_SessionHaulSettings.TurnThresholdDeg);
            GradeReturnPasses = !_SessionHaulSettings.NoGradeReturn;
            SwellFactor = HaulDisplayUnits.RoundForDisplay(_SessionHaulSettings.SwellFactor);
            FieldEfficiencyPercent = HaulDisplayUnits.RoundForDisplay(_SessionHaulSettings.FieldEfficiency * LANE_OVERLAP_FRACTION_TO_PERCENT);
            Zones = _SessionHaulSettings.Zones;
            UseDesignMainSlopeDirection = _SessionHaulSettings.UseDesignMainSlopeDirection;
            HeadingDeg = HaulDisplayUnits.RoundForDisplay(_SessionHaulSettings.HeadingDeg);
            SelectedTransportModeOption = FindOption(TransportModeOptions, _SessionHaulSettings.TransportMode)
                ?? TransportModeOptions[0];
            SelectedBalanceModeOption = FindOption(BalanceModeOptions, _SessionHaulSettings.BalanceMode)
                ?? BalanceModeOptions[0];
            FlowAlignedStripGrowth = !_SessionHaulSettings.NoFlowAligned;
            Pivot = _SessionHaulSettings.Pivot;
            PivotLatitudeDeg = CoordinateFormat.RoundCoordinate(_SessionHaulSettings.PivotLatitudeDeg);
            PivotLongitudeDeg = CoordinateFormat.RoundCoordinate(_SessionHaulSettings.PivotLongitudeDeg);
            PivotWheelbaseDisplay = HaulDisplayUnits.LengthToDisplay(_SessionHaulSettings.PivotWheelbaseFt, Units);
            PivotClearanceDisplay = HaulDisplayUnits.LengthToDisplay(_SessionHaulSettings.PivotClearanceFt, Units);
            PivotMoveCost = HaulDisplayUnits.RoundForDisplay(_SessionHaulSettings.PivotMoveCost);
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
            _SessionHaulSettings.GridSizeFt = HaulDisplayUnits.LengthFtFromDisplay(GridSizeDisplay, Units);
            _SessionHaulSettings.MaxDepthMm = HaulDisplayUnits.DepthMmFromDisplay(MaxDepthDisplay, Units);
            _SessionHaulSettings.ToleranceMm = HaulDisplayUnits.DepthMmFromDisplay(ToleranceDisplay, Units);
            _SessionHaulSettings.MaxDropHopFt = HaulDisplayUnits.LengthFtFromDisplay(MaxDropHopDisplay, Units);
            _SessionHaulSettings.MinLoadFillPct = MinLoadFillPct;
            _SessionHaulSettings.EquipmentWidthFt = HaulDisplayUnits.LengthFtFromDisplay(EquipmentWidthDisplay, Units);
            _SessionHaulSettings.EquipmentLengthFt = HaulDisplayUnits.LengthFtFromDisplay(EquipmentLengthDisplay, Units);
            _SessionHaulSettings.LaneOverlap = Math.Clamp(
                LaneOverlapPercent / LANE_OVERLAP_FRACTION_TO_PERCENT,
                0.0,
                MAX_LANE_OVERLAP_FRACTION);
            _SessionHaulSettings.LoadingSpeedMph = HaulDisplayUnits.SpeedMphFromDisplay(LoadingSpeedDisplay, Units);
            _SessionHaulSettings.HaulSpeedMph = HaulDisplayUnits.SpeedMphFromDisplay(HaulSpeedDisplay, Units);
            _SessionHaulSettings.DumpingSpeedMph = HaulDisplayUnits.SpeedMphFromDisplay(DumpingSpeedDisplay, Units);
            _SessionHaulSettings.ReturnSpeedMph = HaulDisplayUnits.SpeedMphFromDisplay(ReturnSpeedDisplay, Units);
            _SessionHaulSettings.TurnSpeedMph = HaulDisplayUnits.SpeedMphFromDisplay(TurnSpeedDisplay, Units);
            _SessionHaulSettings.GradeSpeedMph = HaulDisplayUnits.SpeedMphFromDisplay(GradeSpeedDisplay, Units);
            _SessionHaulSettings.TurningCircleFt = HaulDisplayUnits.LengthFtFromDisplay(TurningCircleDisplay, Units);
            _SessionHaulSettings.AccelTimeS = AccelTimeS;
            _SessionHaulSettings.DecelTimeS = DecelTimeS;
            _SessionHaulSettings.TurnThresholdDeg = TurnThresholdDeg;
            _SessionHaulSettings.NoGradeReturn = !GradeReturnPasses;
            _SessionHaulSettings.SwellFactor = SwellFactor;
            _SessionHaulSettings.FieldEfficiency = FieldEfficiencyPercent / LANE_OVERLAP_FRACTION_TO_PERCENT;
            _SessionHaulSettings.Zones = Math.Max(1, Zones);
            _SessionHaulSettings.UseDesignMainSlopeDirection = UseDesignMainSlopeDirection;
            _SessionHaulSettings.HeadingDeg = HeadingDeg;
            // intent: combo partial handlers keep session modes current; re-copy on OK for safety
            if (SelectedTransportModeOption != null)
                _SessionHaulSettings.TransportMode = SelectedTransportModeOption.Value;
            if (SelectedBalanceModeOption != null)
                _SessionHaulSettings.BalanceMode = SelectedBalanceModeOption.Value;
            _SessionHaulSettings.NoFlowAligned = !FlowAlignedStripGrowth;
            _SessionHaulSettings.Pivot = Pivot;
            _SessionHaulSettings.PivotLatitudeDeg = CoordinateFormat.RoundCoordinate(PivotLatitudeDeg);
            _SessionHaulSettings.PivotLongitudeDeg = CoordinateFormat.RoundCoordinate(PivotLongitudeDeg);
            _SessionHaulSettings.PivotWheelbaseFt = HaulDisplayUnits.LengthFtFromDisplay(PivotWheelbaseDisplay, Units);
            _SessionHaulSettings.PivotClearanceFt = HaulDisplayUnits.LengthFtFromDisplay(PivotClearanceDisplay, Units);
            _SessionHaulSettings.PivotMoveCost = PivotMoveCost;
        }

        /// <summary>
        /// Finds a combo box option matching the persisted haul setting value.
        /// </summary>
        /// <param name="Options">Available options.</param>
        /// <param name="Value">Persisted setting value.</param>
        /// <returns>Matching option, or null when unknown.</returns>
        private static HaulSettingOptionViewModel? FindOption
            (
            ReadOnlyCollection<HaulSettingOptionViewModel> Options,
            string Value
            )
        {
            foreach (HaulSettingOptionViewModel Option in Options)
            {
                if (string.Equals(Option.Value, Value, StringComparison.OrdinalIgnoreCase))
                    return Option;
            }

            return null;
        }
    }
}
