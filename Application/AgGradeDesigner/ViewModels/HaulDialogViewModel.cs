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
    /// View model for the haul configuration dialog. Sections follow the current HaulBuilder option groups.
    /// </summary>
    public sealed partial class HaulDialogViewModel : ViewModelBase
    {
        /// <summary>Multiplier applied when converting a fraction in [0, 1] to a percent display.</summary>
        private const double FRACTION_TO_PERCENT = 100.0;

        /// <summary>Millimetres per inch for design-rainfall display conversion.</summary>
        private const double MM_PER_INCH = 25.4;

        /// <summary>
        /// Identifies a haul configuration dialog content section.
        /// </summary>
        public enum HaulSectionKind
        {
            /// <summary>Equipment values still round-trip with haul settings. The section itself is edited in Settings.</summary>
            Equipment,

            /// <summary>Field efficiency, swell, grid size, and zone count.</summary>
            FieldOperation,

            /// <summary>Loading, haul, and other travel speed parameters.</summary>
            Travel,

            /// <summary>Dubins trip-guidance rules.</summary>
            Guidance,

            /// <summary>Expert haul-selection and route-order tuning.</summary>
            Optimization,

            /// <summary>Optional ponding audit and reorder settings.</summary>
            Ponding,

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

            PondingObjectiveOptions = new ReadOnlyCollection<HaulSettingOptionViewModel>(
            [
                new HaulSettingOptionViewModel(
                    HaulBuilder.PondingObjectiveMode.PeakVolume.ToString(),
                    "Peak volume"),
                new HaulSettingOptionViewModel(
                    HaulBuilder.PondingObjectiveMode.AverageDepth.ToString(),
                    "Average depth")
            ]);

            HaulSectionItemViewModel FieldOperationSection = new HaulSectionItemViewModel(this, HaulSectionKind.FieldOperation, "Field operation");
            HaulSectionItemViewModel TravelSection = new HaulSectionItemViewModel(this, HaulSectionKind.Travel, "Travel");
            HaulSectionItemViewModel GuidanceSection = new HaulSectionItemViewModel(this, HaulSectionKind.Guidance, "Guidance");
            HaulSectionItemViewModel OptimizationSection = new HaulSectionItemViewModel(this, HaulSectionKind.Optimization, "Optimization");
            HaulSectionItemViewModel PondingSection = new HaulSectionItemViewModel(this, HaulSectionKind.Ponding, "Ponding");
            HaulSectionItemViewModel PivotSection = new HaulSectionItemViewModel(this, HaulSectionKind.Pivot, "Pivot");
            Sections = new ReadOnlyCollection<HaulSectionItemViewModel>(
            [
                FieldOperationSection,
                TravelSection,
                GuidanceSection,
                OptimizationSection,
                PondingSection,
                PivotSection
            ]);
            // Equipment is edited in the settings dialog. This dialog still keeps those values so OK does not wipe them.
            SelectedSection = HaulSectionKind.FieldOperation;
            FieldOperationSection.IsSelected = true;

            ApplySessionToBindings();
        }

        /// <summary>
        /// Sidebar entries for choosing a haul settings section.
        /// </summary>
        public ReadOnlyCollection<HaulSectionItemViewModel> Sections { get; }

        /// <summary>
        /// Ponding objective choices shown in the ponding section combo box.
        /// </summary>
        public ReadOnlyCollection<HaulSettingOptionViewModel> PondingObjectiveOptions { get; }

        /// <summary>
        /// Currently displayed haul settings section.
        /// </summary>
        [ObservableProperty]
        private HaulSectionKind _SelectedSection;

        /// <summary>Whether the equipment section is currently displayed.</summary>
        public bool IsEquipmentSectionSelected => SelectedSection == HaulSectionKind.Equipment;

        /// <summary>Whether the field operation section is currently displayed.</summary>
        public bool IsFieldOperationSectionSelected => SelectedSection == HaulSectionKind.FieldOperation;

        /// <summary>Whether the travel section is currently displayed.</summary>
        public bool IsTravelSectionSelected => SelectedSection == HaulSectionKind.Travel;

        /// <summary>Whether the guidance section is currently displayed.</summary>
        public bool IsGuidanceSectionSelected => SelectedSection == HaulSectionKind.Guidance;

        /// <summary>Whether the optimization section is currently displayed.</summary>
        public bool IsOptimizationSectionSelected => SelectedSection == HaulSectionKind.Optimization;

        /// <summary>Whether the ponding section is currently displayed.</summary>
        public bool IsPondingSectionSelected => SelectedSection == HaulSectionKind.Ponding;

        /// <summary>Whether the pivot section is currently displayed.</summary>
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
            OnPropertyChanged(nameof(IsGuidanceSectionSelected));
            OnPropertyChanged(nameof(IsOptimizationSectionSelected));
            OnPropertyChanged(nameof(IsPondingSectionSelected));
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

        /// <summary>Equipment width in display units.</summary>
        [ObservableProperty]
        private double _EquipmentWidthDisplay;

        /// <summary>Equipment length in display units.</summary>
        [ObservableProperty]
        private double _EquipmentLengthDisplay;

        /// <summary>Whether a minimum blade-down run length is applied.</summary>
        [ObservableProperty]
        private bool _EnableMinimumWorkLength = true;

        /// <summary>Minimum blade-down run length in display units. Zero means half the equipment length.</summary>
        [ObservableProperty]
        private double _MinimumWorkLengthDisplay;

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

        /// <summary>Gap, in turning radii, that is still a short joint.</summary>
        [ObservableProperty]
        private double _ShortJointRadii;

        /// <summary>Connector tie band as a fraction of the shortest connector.</summary>
        [ObservableProperty]
        private double _ConnectorTieFraction;

        /// <summary>Connector tie band in turning radii.</summary>
        [ObservableProperty]
        private double _ConnectorTieRadii;

        /// <summary>Heading change, in degrees, below which a short joint may steer straight across.</summary>
        [ObservableProperty]
        private double _SteerHeadingDeg;

        /// <summary>Cut lines, and spread lines, kept on one correcting trip.</summary>
        [ObservableProperty]
        private int _MaxCorrectingLines;

        /// <summary>Connector length over productive length above which a correcting trip may merge.</summary>
        [ObservableProperty]
        private double _LeftoverRatio;

        /// <summary>How close, in turning radii, a later route must pass to take a leftover.</summary>
        [ObservableProperty]
        private double _MergeRouteRadii;

        /// <summary>End-fragment length, in turning radii, that is short enough to defer.</summary>
        [ObservableProperty]
        private double _EndFragmentSpanRadii;

        /// <summary>Gap, in turning radii, past the next fragment that defers a short end.</summary>
        [ObservableProperty]
        private double _EndFragmentGapRadii;

        /// <summary>Lane length, in turning radii, below which a cut or spread does not separate connectors.</summary>
        [ObservableProperty]
        private double _NearEmptyRadii;

        /// <summary>Lane volume, as a fraction of the bowl, below which a cut or spread does not separate connectors.</summary>
        [ObservableProperty]
        private double _NearEmptyBowlFraction;

        /// <summary>Dubins arc speed in display units.</summary>
        [ObservableProperty]
        private double _ArcSpeedDisplay;

        /// <summary>Dubins grade-lane speed in display units.</summary>
        [ObservableProperty]
        private double _GradeLaneSpeedDisplay;

        /// <summary>Whether capacity-flexible route consolidation runs.</summary>
        [ObservableProperty]
        private bool _EnableRouteConsolidation;

        /// <summary>Maximum routes pooled in one consolidation transaction.</summary>
        [ObservableProperty]
        private int _RouteConsolidationGroupSize;

        /// <summary>Maximum cycles removed by consolidation.</summary>
        [ObservableProperty]
        private int _RouteConsolidationMaximumCycles;

        /// <summary>Equal-load fill-pair refinement rounds.</summary>
        [ObservableProperty]
        private int _PairRefinementRounds;

        /// <summary>Whether the closed multi-route exchange runs.</summary>
        [ObservableProperty]
        private bool _EnableMultiRouteExchange;

        /// <summary>Multi-route exchange rounds.</summary>
        [ObservableProperty]
        private int _MultiRouteExchangeRounds;

        /// <summary>Maximum routes in one multi-route exchange.</summary>
        [ObservableProperty]
        private int _MultiRouteExchangeGroupSize;

        /// <summary>Maximum committed multi-route exchanges.</summary>
        [ObservableProperty]
        private int _MultiRouteExchangeMaximumGroups;

        /// <summary>Minimum compact-guidance improvement per exchange, in metres.</summary>
        [ObservableProperty]
        private double _MultiRouteExchangeMinimumImprovementM;

        /// <summary>Whether field-wide fill-chain refinement runs.</summary>
        [ObservableProperty]
        private bool _EnableCycleChainOptimization;

        /// <summary>Field-wide fill-chain rounds.</summary>
        [ObservableProperty]
        private int _CycleChainRounds;

        /// <summary>Exact candidates checked per route.</summary>
        [ObservableProperty]
        private int _CycleChainCandidatesPerRoute;

        /// <summary>Minimum production-objective saving per chain exchange, in seconds.</summary>
        [ObservableProperty]
        private double _CycleChainMinimumImprovementSeconds;

        /// <summary>Route relocation search window. Zero disables relocation.</summary>
        [ObservableProperty]
        private int _RouteReorderWindow;

        /// <summary>Whether routes are driven as strip bands before the field edge.</summary>
        [ObservableProperty]
        private bool _EnableEarlyFullStripBanding;

        /// <summary>Whether the next load is chosen by metres rather than seconds.</summary>
        [ObservableProperty]
        private bool _OptimizeRouteOrderByDistance;

        /// <summary>Upcoming trips priced against cluster and thin-cut plans. Zero keeps the flow ribbon.</summary>
        [ObservableProperty]
        private int _ClusterModeAverageWindow;

        /// <summary>Share of a bowl that may leave the flow ribbon.</summary>
        [ObservableProperty]
        private double _ClusterModeLoadFraction;

        /// <summary>Whether drainage-aware pass reordering runs.</summary>
        [ObservableProperty]
        private bool _PondingOptimize;

        /// <summary>Selected ponding objective combo box entry.</summary>
        [ObservableProperty]
        private HaulSettingOptionViewModel? _SelectedPondingObjectiveOption;

        /// <summary>Whether ponding metrics are collected without reordering.</summary>
        [ObservableProperty]
        private bool _PondingAudit;

        /// <summary>Whether optimisation skips the implicit pre-audit pass.</summary>
        [ObservableProperty]
        private bool _PondingNoImplicitAudit;

        /// <summary>Whether target-spill cells are marked as outlets.</summary>
        [ObservableProperty]
        private bool _TargetSpillOutlets = true;

        /// <summary>Target-spill outlet tolerance in display length units.</summary>
        [ObservableProperty]
        private double _SpillToleranceDisplay;

        /// <summary>Seconds applied per unit of the selected ponding penalty.</summary>
        [ObservableProperty]
        private double _PondingWeight;

        /// <summary>Maximum pass gap between checkpoints. Zero lets the engine choose.</summary>
        [ObservableProperty]
        private int _CheckpointInterval;

        /// <summary>WhiteBoxTools executable or install directory.</summary>
        [ObservableProperty]
        private string _WhiteBoxToolsPath = string.Empty;

        /// <summary>Scratch directory for external ponding rasters.</summary>
        [ObservableProperty]
        private string _WhiteBoxToolsWorkDirectory = string.Empty;

        /// <summary>Warning threshold for maximum pond depth in display length units.</summary>
        [ObservableProperty]
        private double _WarnDepthDisplay;

        /// <summary>Warning threshold for trapped volume in display volume units.</summary>
        [ObservableProperty]
        private double _WarnVolumeDisplay;

        /// <summary>Design rainfall depth in inches or millimetres.</summary>
        [ObservableProperty]
        private double _DesignRainfallDisplay;

        /// <summary>Whether a ponding warning fails the run.</summary>
        [ObservableProperty]
        private bool _FailOnWarn;

        /// <summary>Whether a worst-checkpoint ponding map is requested.</summary>
        [ObservableProperty]
        private bool _IncludePondingMap = true;

        /// <summary>Raster scale for the ponding map.</summary>
        [ObservableProperty]
        private int _MapPixelsPerCell;

        /// <summary>Culvert outlet points, one <c>lat,lon</c> per line.</summary>
        [ObservableProperty]
        private string _OutletSpecsText = string.Empty;

        /// <summary>Culvert outlet polygons, one <c>lat,lon;lat,lon;...</c> per line.</summary>
        [ObservableProperty]
        private string _OutletPolygonSpecsText = string.Empty;

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

        /// <summary>Pivot move cost after guidance conflicts have been minimized.</summary>
        [ObservableProperty]
        private double _PivotMoveCost;

        /// <summary>Whether the minimum-work-length field can be edited.</summary>
        public bool IsMinimumWorkLengthEnabled => EnableMinimumWorkLength;

        /// <summary>Whether pivot location and arm fields can be edited.</summary>
        public bool IsPivotSettingsEnabled => Pivot;

        /// <summary>Unit suffix for scraper capacity labels.</summary>
        public string CapacityUnitSuffix =>
            _DisplayUnitSystem == DisplayUnitSystem.Metric ? "m³" : "LCY";

        /// <summary>Unit suffix for bank-volume labels.</summary>
        public string BankVolumeUnitSuffix =>
            _DisplayUnitSystem == DisplayUnitSystem.Metric ? "m³" : "BCY";

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
        /// Updates minimum-work-length field availability when the rule is toggled.
        /// </summary>
        /// <param name="Value">New toggle value.</param>
        partial void OnEnableMinimumWorkLengthChanged
            (
            bool Value
            )
        {
            OnPropertyChanged(nameof(IsMinimumWorkLengthEnabled));
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
        /// Copies persisted haul settings into dialog bindings.
        /// </summary>
        private void ApplySessionToBindings
            (
            )
        {
            DisplayUnitSystem Units = _DisplayUnitSystem;
            HaulSettings Session = _SessionHaulSettings;
            HaulGuidanceSettings Guidance = Session.Guidance;
            HaulOptimizationSettings Optimization = Session.Optimization;
            HaulPondingSettings Ponding = Session.Ponding;

            ScraperCapacityDisplay = HaulDisplayUnits.RoundForDisplay(
                HaulDisplayUnits.ScraperCapacityToDisplay(Session.ScraperCapacityLcy, Units));
            // intent: distance fields keep full allowed precision (5 dp imperial / metres, 0 dp mm)
            BladeWidthDisplay = HaulDisplayUnits.BladeWidthToDisplay(Session.BladeWidthMm, Units);
            GridSizeDisplay = HaulDisplayUnits.LengthToDisplay(Session.GridSizeFt, Units);
            MaxDepthDisplay = HaulDisplayUnits.DepthToDisplay(Session.MaxDepthMm, Units);
            ToleranceDisplay = HaulDisplayUnits.DepthToDisplay(Session.ToleranceMm, Units);
            EquipmentWidthDisplay = HaulDisplayUnits.LengthToDisplay(Session.EquipmentWidthFt, Units);
            EquipmentLengthDisplay = HaulDisplayUnits.LengthToDisplay(Session.EquipmentLengthFt, Units);
            EnableMinimumWorkLength = Session.EnableMinimumWorkLength;
            MinimumWorkLengthDisplay = HaulDisplayUnits.LengthToDisplay(Session.MinimumWorkLengthFt, Units);
            LoadingSpeedDisplay = HaulDisplayUnits.RoundForDisplay(
                HaulDisplayUnits.SpeedToDisplay(Session.LoadingSpeedMph, Units));
            HaulSpeedDisplay = HaulDisplayUnits.RoundForDisplay(
                HaulDisplayUnits.SpeedToDisplay(Session.HaulSpeedMph, Units));
            DumpingSpeedDisplay = HaulDisplayUnits.RoundForDisplay(
                HaulDisplayUnits.SpeedToDisplay(Session.DumpingSpeedMph, Units));
            ReturnSpeedDisplay = HaulDisplayUnits.RoundForDisplay(
                HaulDisplayUnits.SpeedToDisplay(Session.ReturnSpeedMph, Units));
            TurnSpeedDisplay = HaulDisplayUnits.RoundForDisplay(
                HaulDisplayUnits.SpeedToDisplay(Session.TurnSpeedMph, Units));
            GradeSpeedDisplay = HaulDisplayUnits.RoundForDisplay(
                HaulDisplayUnits.SpeedToDisplay(Session.GradeSpeedMph, Units));
            TurningCircleDisplay = HaulDisplayUnits.LengthToDisplay(Session.TurningCircleFt, Units);
            AccelTimeS = HaulDisplayUnits.RoundForDisplay(Session.AccelTimeS);
            DecelTimeS = HaulDisplayUnits.RoundForDisplay(Session.DecelTimeS);
            TurnThresholdDeg = HaulDisplayUnits.RoundForDisplay(Session.TurnThresholdDeg);
            SwellFactor = HaulDisplayUnits.RoundForDisplay(Session.SwellFactor);
            FieldEfficiencyPercent = HaulDisplayUnits.RoundForDisplay(Session.FieldEfficiency * FRACTION_TO_PERCENT);
            Zones = Session.Zones;

            ShortJointRadii = Guidance.ShortJointRadii;
            ConnectorTieFraction = Guidance.ConnectorTieFraction;
            ConnectorTieRadii = Guidance.ConnectorTieRadii;
            SteerHeadingDeg = Guidance.SteerHeadingDeg;
            MaxCorrectingLines = Guidance.MaxCorrectingLines;
            LeftoverRatio = Guidance.LeftoverRatio;
            MergeRouteRadii = Guidance.MergeRouteRadii;
            EndFragmentSpanRadii = Guidance.EndFragmentSpanRadii;
            EndFragmentGapRadii = Guidance.EndFragmentGapRadii;
            NearEmptyRadii = Guidance.NearEmptyRadii;
            NearEmptyBowlFraction = Guidance.NearEmptyBowlFraction;
            ArcSpeedDisplay = HaulDisplayUnits.RoundForDisplay(
                HaulDisplayUnits.SpeedToDisplay(Guidance.ArcSpeedMph, Units));
            GradeLaneSpeedDisplay = HaulDisplayUnits.RoundForDisplay(
                HaulDisplayUnits.SpeedToDisplay(Guidance.GradeLaneSpeedMph, Units));

            EnableRouteConsolidation = Optimization.EnableRouteConsolidation;
            RouteConsolidationGroupSize = Optimization.RouteConsolidationGroupSize;
            RouteConsolidationMaximumCycles = Optimization.RouteConsolidationMaximumCycles;
            PairRefinementRounds = Optimization.PairRefinementRounds;
            EnableMultiRouteExchange = Optimization.EnableMultiRouteExchange;
            MultiRouteExchangeRounds = Optimization.MultiRouteExchangeRounds;
            MultiRouteExchangeGroupSize = Optimization.MultiRouteExchangeGroupSize;
            MultiRouteExchangeMaximumGroups = Optimization.MultiRouteExchangeMaximumGroups;
            MultiRouteExchangeMinimumImprovementM = Optimization.MultiRouteExchangeMinimumImprovementM;
            EnableCycleChainOptimization = Optimization.EnableCycleChainOptimization;
            CycleChainRounds = Optimization.CycleChainRounds;
            CycleChainCandidatesPerRoute = Optimization.CycleChainCandidatesPerRoute;
            CycleChainMinimumImprovementSeconds = Optimization.CycleChainMinimumImprovementSeconds;
            RouteReorderWindow = Optimization.RouteReorderWindow;
            EnableEarlyFullStripBanding = Optimization.EnableEarlyFullStripBanding;
            OptimizeRouteOrderByDistance = Optimization.OptimizeRouteOrderByDistance;
            ClusterModeAverageWindow = Optimization.ClusterModeAverageWindow;
            ClusterModeLoadFraction = Optimization.ClusterModeLoadFraction;

            PondingOptimize = Ponding.Optimize;
            SelectedPondingObjectiveOption = FindOption(PondingObjectiveOptions, Ponding.ObjectiveMode.ToString())
                ?? PondingObjectiveOptions[0];
            PondingAudit = Ponding.Audit;
            PondingNoImplicitAudit = Ponding.NoImplicitAudit;
            TargetSpillOutlets = Ponding.TargetSpillOutlets;
            SpillToleranceDisplay = HaulDisplayUnits.LengthToDisplay(Ponding.SpillToleranceFt, Units);
            PondingWeight = Ponding.Weight;
            CheckpointInterval = Ponding.CheckpointInterval ?? 0;
            WhiteBoxToolsPath = Ponding.WhiteBoxToolsPath;
            WhiteBoxToolsWorkDirectory = Ponding.WhiteBoxToolsWorkDirectory;
            WarnDepthDisplay = HaulDisplayUnits.LengthToDisplay(Ponding.WarnDepthFt, Units);
            WarnVolumeDisplay = HaulDisplayUnits.RoundForDisplay(
                HaulDisplayUnits.ScraperCapacityToDisplay(Ponding.WarnVolumeBcy, Units));
            DesignRainfallDisplay = HaulDisplayUnits.DepthToDisplay(Ponding.DesignRainfallIn * MM_PER_INCH, Units);
            FailOnWarn = Ponding.FailOnWarn;
            IncludePondingMap = Ponding.IncludeMap;
            MapPixelsPerCell = Ponding.MapPixelsPerCell;
            OutletSpecsText = string.Join(Environment.NewLine, Ponding.OutletSpecs);
            OutletPolygonSpecsText = string.Join(Environment.NewLine, Ponding.OutletPolygonSpecs);

            Pivot = Session.Pivot;
            PivotLatitudeDeg = CoordinateFormat.RoundCoordinate(Session.PivotLatitudeDeg);
            PivotLongitudeDeg = CoordinateFormat.RoundCoordinate(Session.PivotLongitudeDeg);
            PivotWheelbaseDisplay = HaulDisplayUnits.LengthToDisplay(Session.PivotWheelbaseFt, Units);
            PivotClearanceDisplay = HaulDisplayUnits.LengthToDisplay(Session.PivotClearanceFt, Units);
            PivotMoveCost = HaulDisplayUnits.RoundForDisplay(Session.PivotMoveCost);
        }

        /// <summary>
        /// Copies dialog bindings into the session haul settings snapshot.
        /// </summary>
        private void CopyBindingsToSession
            (
            )
        {
            DisplayUnitSystem Units = _DisplayUnitSystem;
            HaulSettings Session = _SessionHaulSettings;
            HaulGuidanceSettings Guidance = Session.Guidance;
            HaulOptimizationSettings Optimization = Session.Optimization;
            HaulPondingSettings Ponding = Session.Ponding;

            Session.ScraperCapacityLcy = HaulDisplayUnits.ScraperCapacityLcyFromDisplay(ScraperCapacityDisplay, Units);
            Session.BladeWidthMm = HaulDisplayUnits.BladeWidthMmFromDisplay(BladeWidthDisplay, Units);
            Session.GridSizeFt = HaulDisplayUnits.LengthFtFromDisplay(GridSizeDisplay, Units);
            Session.MaxDepthMm = HaulDisplayUnits.DepthMmFromDisplay(MaxDepthDisplay, Units);
            Session.ToleranceMm = HaulDisplayUnits.DepthMmFromDisplay(ToleranceDisplay, Units);
            Session.EquipmentWidthFt = HaulDisplayUnits.LengthFtFromDisplay(EquipmentWidthDisplay, Units);
            Session.EquipmentLengthFt = HaulDisplayUnits.LengthFtFromDisplay(EquipmentLengthDisplay, Units);
            Session.EnableMinimumWorkLength = EnableMinimumWorkLength;
            Session.MinimumWorkLengthFt = HaulDisplayUnits.LengthFtFromDisplay(MinimumWorkLengthDisplay, Units);
            Session.LoadingSpeedMph = HaulDisplayUnits.SpeedMphFromDisplay(LoadingSpeedDisplay, Units);
            Session.HaulSpeedMph = HaulDisplayUnits.SpeedMphFromDisplay(HaulSpeedDisplay, Units);
            Session.DumpingSpeedMph = HaulDisplayUnits.SpeedMphFromDisplay(DumpingSpeedDisplay, Units);
            Session.ReturnSpeedMph = HaulDisplayUnits.SpeedMphFromDisplay(ReturnSpeedDisplay, Units);
            Session.TurnSpeedMph = HaulDisplayUnits.SpeedMphFromDisplay(TurnSpeedDisplay, Units);
            Session.GradeSpeedMph = HaulDisplayUnits.SpeedMphFromDisplay(GradeSpeedDisplay, Units);
            Session.TurningCircleFt = HaulDisplayUnits.LengthFtFromDisplay(TurningCircleDisplay, Units);
            Session.AccelTimeS = AccelTimeS;
            Session.DecelTimeS = DecelTimeS;
            Session.TurnThresholdDeg = TurnThresholdDeg;
            Session.SwellFactor = SwellFactor;
            Session.FieldEfficiency = FieldEfficiencyPercent / FRACTION_TO_PERCENT;
            Session.Zones = Math.Max(0, Zones);

            Guidance.ShortJointRadii = ShortJointRadii;
            Guidance.ConnectorTieFraction = ConnectorTieFraction;
            Guidance.ConnectorTieRadii = ConnectorTieRadii;
            Guidance.SteerHeadingDeg = SteerHeadingDeg;
            Guidance.MaxCorrectingLines = Math.Max(1, MaxCorrectingLines);
            Guidance.LeftoverRatio = LeftoverRatio;
            Guidance.MergeRouteRadii = MergeRouteRadii;
            Guidance.EndFragmentSpanRadii = EndFragmentSpanRadii;
            Guidance.EndFragmentGapRadii = EndFragmentGapRadii;
            Guidance.NearEmptyRadii = NearEmptyRadii;
            Guidance.NearEmptyBowlFraction = NearEmptyBowlFraction;
            Guidance.ArcSpeedMph = HaulDisplayUnits.SpeedMphFromDisplay(ArcSpeedDisplay, Units);
            Guidance.GradeLaneSpeedMph = HaulDisplayUnits.SpeedMphFromDisplay(GradeLaneSpeedDisplay, Units);

            Optimization.EnableRouteConsolidation = EnableRouteConsolidation;
            Optimization.RouteConsolidationGroupSize = Math.Max(1, RouteConsolidationGroupSize);
            Optimization.RouteConsolidationMaximumCycles = Math.Max(0, RouteConsolidationMaximumCycles);
            Optimization.PairRefinementRounds = Math.Max(0, PairRefinementRounds);
            Optimization.EnableMultiRouteExchange = EnableMultiRouteExchange;
            Optimization.MultiRouteExchangeRounds = Math.Max(0, MultiRouteExchangeRounds);
            Optimization.MultiRouteExchangeGroupSize = Math.Max(1, MultiRouteExchangeGroupSize);
            Optimization.MultiRouteExchangeMaximumGroups = Math.Max(0, MultiRouteExchangeMaximumGroups);
            Optimization.MultiRouteExchangeMinimumImprovementM = MultiRouteExchangeMinimumImprovementM;
            Optimization.EnableCycleChainOptimization = EnableCycleChainOptimization;
            Optimization.CycleChainRounds = Math.Max(0, CycleChainRounds);
            Optimization.CycleChainCandidatesPerRoute = Math.Max(0, CycleChainCandidatesPerRoute);
            Optimization.CycleChainMinimumImprovementSeconds = CycleChainMinimumImprovementSeconds;
            Optimization.RouteReorderWindow = Math.Max(0, RouteReorderWindow);
            Optimization.EnableEarlyFullStripBanding = EnableEarlyFullStripBanding;
            Optimization.OptimizeRouteOrderByDistance = OptimizeRouteOrderByDistance;
            Optimization.ClusterModeAverageWindow = Math.Max(0, ClusterModeAverageWindow);
            Optimization.ClusterModeLoadFraction = ClusterModeLoadFraction;

            Ponding.Optimize = PondingOptimize;
            Ponding.ObjectiveMode = ParsePondingObjective(SelectedPondingObjectiveOption);
            Ponding.Audit = PondingAudit;
            Ponding.NoImplicitAudit = PondingNoImplicitAudit;
            Ponding.TargetSpillOutlets = TargetSpillOutlets;
            Ponding.SpillToleranceFt = HaulDisplayUnits.LengthFtFromDisplay(SpillToleranceDisplay, Units);
            Ponding.Weight = PondingWeight;
            // intent: zero is the dialog's "let the engine choose" sentinel
            Ponding.CheckpointInterval = CheckpointInterval > 0 ? CheckpointInterval : null;
            Ponding.WhiteBoxToolsPath = WhiteBoxToolsPath?.Trim() ?? string.Empty;
            Ponding.WhiteBoxToolsWorkDirectory = WhiteBoxToolsWorkDirectory?.Trim() ?? string.Empty;
            Ponding.WarnDepthFt = HaulDisplayUnits.LengthFtFromDisplay(WarnDepthDisplay, Units);
            Ponding.WarnVolumeBcy = HaulDisplayUnits.ScraperCapacityLcyFromDisplay(WarnVolumeDisplay, Units);
            Ponding.DesignRainfallIn = HaulDisplayUnits.DepthMmFromDisplay(DesignRainfallDisplay, Units) / MM_PER_INCH;
            Ponding.FailOnWarn = FailOnWarn;
            Ponding.IncludeMap = IncludePondingMap;
            Ponding.MapPixelsPerCell = Math.Max(1, MapPixelsPerCell);
            Ponding.OutletSpecs = SplitSpecLines(OutletSpecsText);
            Ponding.OutletPolygonSpecs = SplitSpecLines(OutletPolygonSpecsText);

            Session.Pivot = Pivot;
            Session.PivotLatitudeDeg = CoordinateFormat.RoundCoordinate(PivotLatitudeDeg);
            Session.PivotLongitudeDeg = CoordinateFormat.RoundCoordinate(PivotLongitudeDeg);
            Session.PivotWheelbaseFt = HaulDisplayUnits.LengthFtFromDisplay(PivotWheelbaseDisplay, Units);
            Session.PivotClearanceFt = HaulDisplayUnits.LengthFtFromDisplay(PivotClearanceDisplay, Units);
            Session.PivotMoveCost = PivotMoveCost;
        }

        /// <summary>
        /// Parses the selected ponding objective, falling back to peak volume.
        /// </summary>
        /// <param name="Option">Combo entry, or null when nothing is selected.</param>
        /// <returns>Engine objective mode.</returns>
        private static HaulBuilder.PondingObjectiveMode ParsePondingObjective
            (
            HaulSettingOptionViewModel? Option
            )
        {
            if (Option != null
                && Enum.TryParse(Option.Value, true, out HaulBuilder.PondingObjectiveMode Mode))
            {
                return Mode;
            }

            return HaulBuilder.PondingObjectiveMode.PeakVolume;
        }

        /// <summary>
        /// Splits a multiline spec box into trimmed non-empty lines.
        /// </summary>
        /// <param name="Text">Raw text box contents.</param>
        /// <returns>One spec per non-empty line.</returns>
        private static List<string> SplitSpecLines
            (
            string? Text
            )
        {
            List<string> Lines = new List<string>();
            if (string.IsNullOrWhiteSpace(Text))
                return Lines;

            foreach (string Line in Text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                string Trimmed = Line.Trim();
                if (Trimmed.Length > 0)
                    Lines.Add(Trimmed);
            }

            return Lines;
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
