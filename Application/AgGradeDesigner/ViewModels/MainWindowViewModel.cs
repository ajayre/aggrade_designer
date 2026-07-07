using System.Collections.ObjectModel;
using System.Globalization;

using Avalonia.Media;
using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using AgGradeDesigner.Core.Ai.SurfaceTools;
using AgGradeDesigner.Core.Color;
using AgGradeDesigner.Core.Design;
using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Import;
using AgGradeDesigner.Core.MapTools;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Persistence;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Core.Units;
using AgGradeDesigner.Core.Survey;
using AgGradeDesigner.Diagnostics;
using AgGradeDesigner.Helpers;
using AgGradeDesigner.Rendering;
using AgGradeDesigner.Services;

namespace AgGradeDesigner.ViewModels
{
    /// <summary>
    /// Main window state: project, viewer bindings, toolbar and sidebar commands.
    /// </summary>
    public sealed partial class MainWindowViewModel : ViewModelBase
    {
        /// <summary>Default status-bar latitude before a project is loaded.</summary>
        private const double DEFAULT_STATUS_LATITUDE = Camera2DState.DEFAULT_CENTRE_LAT;

        /// <summary>Default status-bar longitude before a project is loaded.</summary>
        private const double DEFAULT_STATUS_LONGITUDE = Camera2DState.DEFAULT_CENTRE_LON;

        /// <summary>Default vertical exaggeration multiplier for 3D view.</summary>
        private const double DEFAULT_VERTICAL_EXAGGERATION = DisplaySettings.DEFAULT_VERTICAL_EXAGGERATION;

        /// <summary>Default viewport width used before the viewer reports its size.</summary>
        private const int DEFAULT_VIEWPORT_WIDTH = 800;

        /// <summary>Default viewport height used before the viewer reports its size.</summary>
        private const int DEFAULT_VIEWPORT_HEIGHT = 600;

        /// <summary>Fraction of field extent used when computing initial 3D camera distance.</summary>
        private const double CAMERA_DISTANCE_EXTENT_FACTOR = GeoConstants.MAP_FIT_VIEWPORT_FILL_FRACTION;

        /// <summary>Minimum orbit camera distance from the field in metres.</summary>
        private const double CAMERA_DISTANCE_MIN_M = 80.0;

        /// <summary>Maximum orbit camera distance from the field in metres.</summary>
        private const double CAMERA_DISTANCE_MAX_M = 5000.0;

        /// <summary>Bit shift used to extract the alpha channel from ARGB integers.</summary>
        private const int ARGB_ALPHA_SHIFT = 24;

        /// <summary>Bit shift used to extract the red channel from ARGB integers.</summary>
        private const int ARGB_RED_SHIFT = 16;

        /// <summary>Bit shift used to extract the green channel from ARGB integers.</summary>
        private const int ARGB_GREEN_SHIFT = 8;

        /// <summary>Bitmask used to isolate one ARGB byte.</summary>
        private const int ARGB_BYTE_MASK = 0xFF;

        /// <summary>Minimum progress percentage displayed in the status bar.</summary>
        private const int PROGRESS_PERCENT_MIN = 0;

        /// <summary>Maximum progress percentage displayed in the status bar.</summary>
        private const int PROGRESS_PERCENT_MAX = 100;

        /// <summary>Progress cap for the survey import reading phase.</summary>
        private const int IMPORT_PHASE_READING_MAX_PERCENT = 18;

        /// <summary>Progress cap for the survey import grid-building phase.</summary>
        private const int IMPORT_PHASE_BUILDING_MAX_PERCENT = 45;

        /// <summary>Progress cap for the survey import preview phase.</summary>
        private const int IMPORT_PHASE_PREVIEW_MAX_PERCENT = 72;

        /// <summary>Progress cap for the survey import finishing phase.</summary>
        private const int IMPORT_PHASE_FINISHING_MAX_PERCENT = 92;

        /// <summary>Progress cap for the project open reading phase.</summary>
        private const int OPEN_PHASE_READING_MAX_PERCENT = 12;

        /// <summary>Base progress percent when MLS smoothing begins.</summary>
        private const int MLS_PROGRESS_BASE_PERCENT = 15;

        /// <summary>Progress span allocated to MLS smoothing updates.</summary>
        private const int MLS_PROGRESS_SPAN_PERCENT = 40;

        /// <summary>Fallback progress percent when MLS phase is unknown.</summary>
        private const int MLS_PROGRESS_FALLBACK_PERCENT = 30;

        /// <summary>Progress percent reported when design grid build starts.</summary>
        private const int DESIGN_PROGRESS_GRID_BUILD_PERCENT = 5;

        /// <summary>Progress percent reported during design smoothing.</summary>
        private const int DESIGN_PROGRESS_SMOOTHING_PERCENT = 58;

        /// <summary>Progress percent reported when existing surface is ready.</summary>
        private const int DESIGN_PROGRESS_EXISTING_READY_PERCENT = 62;

        /// <summary>Progress percent reported during plane solve.</summary>
        private const int DESIGN_PROGRESS_PLANE_SOLVE_PERCENT = 72;

        /// <summary>Progress percent reported during cut/fill ratio solve.</summary>
        private const int DESIGN_PROGRESS_RATIO_SOLVE_PERCENT = 82;

        /// <summary>Progress percent reported during volume calculation.</summary>
        private const int DESIGN_PROGRESS_VOLUMES_PERCENT = 90;

        /// <summary>Progress percent reported when design generation completes.</summary>
        private const int DESIGN_PROGRESS_COMPLETE_PERCENT = 100;

        /// <summary>Fallback progress percent for unknown design phases.</summary>
        private const int DESIGN_PROGRESS_UNKNOWN_PERCENT = 50;

        /// <summary>Progress percent reported immediately after survey file read completes.</summary>
        private const int SURVEY_IMPORT_PROGRESS_AFTER_READ_PERCENT = 20;

        /// <summary>Active project document owned by the main window.</summary>
        private ProjectDocument _Project = new ProjectDocument();

        /// <summary>Session map-editing tool data overlay (TOOLS-SPEC §4).</summary>
        private readonly ToolDataSession _ToolDataSession = new ToolDataSession();

        /// <summary>Currently active map-editing toggle tool, if any.</summary>
        private MapEditingToolKind _ActiveMapTool = MapEditingToolKind.None;

        /// <summary>
        /// Window title derived from the project name and modified flag.
        /// </summary>
        [ObservableProperty]
        private string _ProjectTitle = "Untitled";

        /// <summary>
        /// Application name and version shown in the toolbar.
        /// </summary>
        public string ApplicationBranding { get; } = FormatApplicationBranding();

        /// <summary>
        /// Formats the product name and major.minor version for the toolbar.
        /// </summary>
        /// <returns>Branding text shown in the main window toolbar.</returns>
        static private string FormatApplicationBranding
            (
            )
        {
            Version Version = Version.Parse(ProjectDocument.APPLICATION_VERSION);
            return $"AgGrade Designer V{Version.Major}.{Version.Minor}";
        }

        /// <summary>
        /// Status bar latitude formatted for display.
        /// </summary>
        [ObservableProperty]
        private string _StatusLatitude = CoordinateFormat.FormatLatitude(DEFAULT_STATUS_LATITUDE);

        /// <summary>
        /// Status bar longitude formatted for display.
        /// </summary>
        [ObservableProperty]
        private string _StatusLongitude = CoordinateFormat.FormatLongitude(DEFAULT_STATUS_LONGITUDE);

        /// <summary>
        /// Whether the 2D map view mode toggle is active.
        /// </summary>
        [ObservableProperty]
        private bool _IsTwoD = true;

        /// <summary>
        /// Whether the 3D orbit view mode toggle is active.
        /// </summary>
        [ObservableProperty]
        private bool _IsThreeD;

        /// <summary>
        /// Whether the existing-surface content view button is highlighted.
        /// </summary>
        [ObservableProperty]
        private bool _IsExistingActive;

        /// <summary>
        /// Whether the proposed design content view button is highlighted.
        /// </summary>
        [ObservableProperty]
        private bool _IsProposedActive;

        /// <summary>
        /// Whether the cut/fill content view button is highlighted.
        /// </summary>
        [ObservableProperty]
        private bool _IsCutFillActive;

        /// <summary>
        /// Whether the Add Point tool button is highlighted.
        /// </summary>
        [ObservableProperty]
        private bool _IsAddPointToolActive;

        /// <summary>
        /// Whether the Add Polyline tool button is highlighted.
        /// </summary>
        [ObservableProperty]
        private bool _IsAddPolylineToolActive;

        /// <summary>
        /// Whether the Erase tool button is highlighted.
        /// </summary>
        [ObservableProperty]
        private bool _IsEraseToolActive;

        /// <summary>
        /// Whether the Polygon tool button is highlighted.
        /// </summary>
        [ObservableProperty]
        private bool _IsPolygonToolActive;

        /// <summary>
        /// Session map-editing tool data exposed to the field viewer and AI tools API.
        /// </summary>
        public ToolDataSession ToolDataSession => _ToolDataSession;

        /// <summary>
        /// Active map-editing tool forwarded to the field viewer.
        /// </summary>
        public MapEditingToolKind ActiveMapTool => _ActiveMapTool;

        /// <summary>
        /// Whether map-editing tool buttons are enabled (requires a design).
        /// </summary>
        public bool CanUseMapTools => HasDesign;

        /// <summary>True when Add Point and Add Polyline may create new tool data (2D mode only).</summary>
        public bool CanAddMapToolData => HasDesign && IsTwoD;

        /// <summary>
        /// Whether the current project includes survey data.
        /// </summary>
        [ObservableProperty]
        private bool _HasSurvey;

        /// <summary>
        /// Whether the current project includes a generated design.
        /// </summary>
        [ObservableProperty]
        private bool _HasDesign;

        /// <summary>
        /// Whether the design dialog command is enabled.
        /// </summary>
        [ObservableProperty]
        private bool _CanDesign;

        /// <summary>
        /// Whether the proposed surface view can be selected.
        /// </summary>
        [ObservableProperty]
        private bool _CanProposedView;

        /// <summary>
        /// Whether the cut/fill comparison view can be selected.
        /// </summary>
        [ObservableProperty]
        private bool _CanCutFillView;

        /// <summary>
        /// Whether the elevation legend sidebar is visible.
        /// </summary>
        [ObservableProperty]
        private bool _ShowLegend;

        /// <summary>
        /// Whether the vertical exaggeration slider is visible (3D mode).
        /// </summary>
        [ObservableProperty]
        private bool _ShowVerticalExaggeration;

        /// <summary>
        /// Vertical exaggeration multiplier bound to the 3D viewer.
        /// </summary>
        [ObservableProperty]
        private double _VerticalExaggeration = DEFAULT_VERTICAL_EXAGGERATION;

        /// <summary>
        /// Formatted vertical exaggeration label for the status bar.
        /// </summary>
        [ObservableProperty]
        private string _VerticalExaggerationLabel = "1.0×";

        /// <summary>
        /// Opacity percentage for the active topo or cut/fill colour surface.
        /// </summary>
        [ObservableProperty]
        private double _TopoCutFillOpacity = DisplaySettings.OPACITY_PERCENT_MAX;

        /// <summary>
        /// Formatted topo/cut-fill opacity label for the sidebar slider.
        /// </summary>
        [ObservableProperty]
        private string _TopoCutFillOpacityLabel = "100%";

        /// <summary>
        /// Survey point overlay visibility toggle.
        /// </summary>
        [ObservableProperty]
        private bool _SurveyPointsToggle = true;

        /// <summary>
        /// Whether the survey points toggle is available (disabled in Proposed view).
        /// </summary>
        [ObservableProperty]
        private bool _SurveyPointsToggleEnabled = true;

        /// <summary>
        /// Contour overlay visibility toggle.
        /// </summary>
        [ObservableProperty]
        private bool _ContoursToggle = true;

        /// <summary>
        /// Hillshade overlay visibility toggle.
        /// </summary>
        [ObservableProperty]
        private bool _HillshadeToggle = true;

        /// <summary>
        /// Benchmark marker overlay visibility toggle.
        /// </summary>
        [ObservableProperty]
        private bool _BenchmarksToggle = true;

        /// <summary>
        /// Proposed design overlay visibility toggle in non-proposed views.
        /// </summary>
        [ObservableProperty]
        private bool _DesignOverlayToggle;

        /// <summary>
        /// Satellite basemap visibility toggle.
        /// </summary>
        [ObservableProperty]
        private bool _SatelliteToggle;

        /// <summary>
        /// Whether the benchmarks overlay toggle is enabled (benchmarks present).
        /// </summary>
        [ObservableProperty]
        private bool _BenchmarksToggleEnabled;

        /// <summary>
        /// Whether the contours overlay toggle is enabled.
        /// </summary>
        public bool ContoursToggleEnabled => HasSurvey && !IsHaulActive;

        /// <summary>
        /// Whether the hillshade overlay toggle is enabled.
        /// </summary>
        public bool HillshadeToggleEnabled => HasSurvey && !IsHaulActive;

        /// <summary>
        /// Whether the central viewer is performing background preparation work.
        /// </summary>
        [ObservableProperty]
        private bool _IsViewerBusy;

        /// <summary>
        /// True when 2D/3D mode toggles are allowed (including while the haul view is active).
        /// </summary>
        public bool CanSwitchViewerView => !IsViewerBusy;

        /// <summary>
        /// True when the 3D view toggle is allowed (haul view requires a cached cut/fill frame).
        /// </summary>
        public bool CanSwitchToThreeDView =>
            !IsViewerBusy &&
            (!IsHaulActive || HasCurrentHaulCutFillFrame);

        partial void OnIsViewerBusyChanged
            (
            bool Value
            )
        {
            NotifyViewSwitchAvailability();
            NotifyGenerateHaulAvailability();
        }

        /// <summary>
        /// Refreshes generate-haul availability when design busy state changes.
        /// </summary>
        /// <param name="Value">New design-busy flag.</param>
        partial void OnIsDesignBusyChanged
            (
            bool Value
            )
        {
            NotifyGenerateHaulAvailability();
        }

        /// <summary>
        /// Notifies bindings and commands that depend on viewer busy state.
        /// </summary>
        private void NotifyViewSwitchAvailability
            (
            )
        {
            OnPropertyChanged(nameof(CanSwitchViewerView));
            OnPropertyChanged(nameof(CanSwitchToThreeDView));
            OnPropertyChanged(nameof(ContoursToggleEnabled));
            OnPropertyChanged(nameof(HillshadeToggleEnabled));
            SelectExistingCommand.NotifyCanExecuteChanged();
            SelectProposedCommand.NotifyCanExecuteChanged();
            SelectCutFillCommand.NotifyCanExecuteChanged();
        }

        private bool CanSelectExistingView
            (
            )
        {
            return HasSurvey && !IsViewerBusy;
        }

        private bool CanSelectProposedView
            (
            )
        {
            return CanProposedView && !IsViewerBusy;
        }

        private bool CanSelectCutFillView
            (
            )
        {
            return CanCutFillView && !IsViewerBusy;
        }

        /// <summary>
        /// Status text shown over the viewer progress overlay.
        /// </summary>
        [ObservableProperty]
        private string _ViewerBusyMessage = "";

        /// <summary>
        /// Graphics runtime summary shown in the status bar (Vulkan probe at startup).
        /// </summary>
        [ObservableProperty]
        private string _GraphicsRuntimeStatus = "";

        /// <summary>
        /// Whether the AI chat panel is visible below the field viewer.
        /// </summary>
        [ObservableProperty]
        private bool _IsAiPanelVisible;

        /// <summary>
        /// True when the AI chat panel is hidden and the viewer uses full height.
        /// </summary>
        public bool IsAiPanelHidden => !IsAiPanelVisible;

        /// <summary>
        /// View model for the embedded AI chat panel.
        /// </summary>
        public AiChatViewModel AiChat { get; }

        /// <summary>
        /// Viewer preparation progress from 0 to 100.
        /// </summary>
        [ObservableProperty]
        private double _ViewerBusyProgress;

        /// <summary>
        /// Whether a design calculation is running on a worker thread.
        /// </summary>
        [ObservableProperty]
        private bool _IsDesignBusy;

        /// <summary>
        /// Design calculation progress from 0 to 100.
        /// </summary>
        [ObservableProperty]
        private double _DesignBusyProgress;

        /// <summary>
        /// Status text shown on the design-generation overlay in the central viewer.
        /// </summary>
        [ObservableProperty]
        private string _DesignBusyMessage = "";

        /// <summary>Prevents view-mode toggle handlers from re-entering during programmatic sync.</summary>
        private bool _SuppressModeChange;

        /// <summary>Prevents display-toggle handlers from writing back during programmatic sync.</summary>
        private bool _SuppressDisplaySync;

        /// <summary>Ensures viewer overlay progress never decreases during one busy session.</summary>
        private readonly MonotonicProgressTracker _ViewerBusyProgressTracker = new MonotonicProgressTracker();

        /// <summary>True when the next 3D entry should queue the standard initial camera fit.</summary>
        private bool _ApplyInitial3DCameraOnNextEntry;

        /// <summary>No-op progress reporter used when viewer preparation does not report progress.</summary>
        private static readonly IProgress<int> EmptyViewerProgress = new Progress<int>(_ => { });

        /// <summary>
        /// Optional callback that rebuilds and warms the field viewer on a worker thread.
        /// </summary>
        public Func<IProgress<int>, ViewMode, Task<bool>>? PrepareViewerAsync { get; set; }

        /// <summary>
        /// Optional callback that queues post-import background prewarm on the viewer.
        /// </summary>
        public Action? ScheduleViewerBackgroundPrewarm { get; set; }

        /// <summary>
        /// Optional callback that rebuilds composite frames at the live viewport before import handoff completes.
        /// </summary>
        public Func<Task>? EnsureViewerImportHandoffReadyAsync { get; set; }

        /// <summary>
        /// Optional callback for progressive viewer prepare (preview frame, then full quality).
        /// </summary>
        public Func<IProgress<int>, Action, ViewMode, Task>? PrepareViewerProgressiveAsync { get; set; }

        /// <summary>
        /// Optional callback that reports whether a cached frame exists for the target view mode.
        /// </summary>
        public Func<ViewMode, bool>? HasCachedViewerFrame { get; set; }

        /// <summary>
        /// When true, the main window skips frame-cache invalidation during view-mode transitions.
        /// </summary>
        internal bool SuppressViewerFrameInvalidation { get; set; }

        /// <summary>
        /// Optional callback that queues initial 3D camera fit on the worker thread.
        /// </summary>
        public Action? QueueInitial3DCameraFit { get; set; }

        /// <summary>
        /// Optional callback that pushes the pinned haul 3D scrub index to viewer controls before prepare.
        /// </summary>
        public Action<int>? SyncHaulThreeDRouteScrubIndexToViewer { get; set; }

        /// <summary>
        /// Optional callback that discards cached 3D frame bitmaps after the orbit camera changes.
        /// </summary>
        public Action? InvalidateViewer3DFrameCache { get; set; }

        /// <summary>
        /// Optional callback that discards cached 2D frame bitmaps before entering 2D view.
        /// </summary>
        public Action? InvalidateViewer2DFrameCache { get; set; }

        /// <summary>
        /// Rebuilds the elevation mesh from the current design grid on all viewers.
        /// </summary>
        public Action? InvalidateViewerSurface { get; set; }

        /// <summary>
        /// Requests a lightweight redraw on all viewers without rebuilding topo or frame caches.
        /// </summary>
        public Action? InvalidateViewerVisual { get; set; }

        /// <summary>
        /// While true, haul frame binding updates coalesce to a single viewer redraw.
        /// </summary>
        internal bool IsApplyingHaulFrame { get; set; }

        /// <summary>
        /// Optional callback invoked before viewer preparation so the UI thread does not sync-render.
        /// </summary>
        public Action? EnterViewerTransition { get; set; }

        /// <summary>
        /// Optional callback that blocks viewer pointer and wheel input during AGD import.
        /// </summary>
        public Action? EnterImportInputBlock { get; set; }

        /// <summary>
        /// Optional callback that restores viewer pointer and wheel input after AGD import handoff.
        /// </summary>
        public Action? ExitImportInputBlock { get; set; }

        /// <summary>
        /// Optional callback invoked after import handoff completes successfully.
        /// </summary>
        public Action? MarkViewerImportHandoffPendingFirstDraw { get; set; }

        /// <summary>
        /// Optional callback invoked after viewer preparation completes.
        /// </summary>
        public Action? ExitViewerTransition { get; set; }

        /// <summary>
        /// Optional callback that publishes the latest cached viewer frame to the screen.
        /// </summary>
        public Action? PresentViewerCachedFrame { get; set; }

        /// <summary>
        /// Optional callback returning true when all composite layer bitmaps are ready for the current view.
        /// </summary>
        public Func<bool>? HasMaterializedViewerCompositeFrame { get; set; }

        /// <summary>
        /// Optional callback returning the active viewer viewport size in pixels.
        /// </summary>
        public Func<(int Width, int Height)>? GetViewerViewportSize { get; set; }

        /// <summary>
        /// Optional callback that writes detailed import handoff diagnostics from the active viewer.
        /// </summary>
        public Action<string>? WriteViewerImportDiagnosticsDelegate { get; set; }

        /// <summary>
        /// Optional callback invoked when import prepare finishes so the viewer surface stays clean.
        /// </summary>
        public Action? MarkViewerImportPrepareComplete { get; set; }

        /// <summary>
        /// Optional callback that clears the viewer after the project is reset to empty.
        /// </summary>
        public Action? ResetViewerForEmptyProject { get; set; }

        /// <summary>
        /// Legend colour bands for the active field content view.
        /// </summary>
        public ObservableCollection<LegendBandViewModel> LegendBands { get; } = new ObservableCollection<LegendBandViewModel>();

        /// <summary>
        /// The open project document shared with persistence and rendering.
        /// </summary>
        public ProjectDocument Project => _Project;

        /// <summary>
        /// Current 2D or 3D viewer mode from project viewer state.
        /// </summary>
        public ViewMode ViewMode => _Project.ViewerState.ViewMode;

        /// <summary>
        /// Active field surface view (existing, proposed, cut/fill).
        /// </summary>
        public FieldContentView FieldContentView => _Project.ViewerState.FieldContentView;

        /// <summary>
        /// Layer and overlay display settings bound to the map viewer.
        /// </summary>
        public DisplaySettings Display => _Project.ViewerState.Display;

        /// <summary>
        /// Map centre latitude in degrees (WGS84).
        /// </summary>
        public double CentreLat => _Project.ViewerState.Camera2D.CentreLat;

        /// <summary>
        /// Map centre longitude in degrees (WGS84).
        /// </summary>
        public double CentreLon => _Project.ViewerState.Camera2D.CentreLon;

        /// <summary>
        /// Map zoom factor for the 2D camera.
        /// </summary>
        public double Zoom => _Project.ViewerState.Camera2D.Zoom;

        /// <summary>
        /// 3D orbit heading in degrees.
        /// </summary>
        public double OrbitHeading => _Project.ViewerState.Camera3D.OrbitHeadingDeg;

        /// <summary>
        /// 3D orbit pitch in degrees.
        /// </summary>
        public double OrbitPitch => _Project.ViewerState.Camera3D.OrbitPitchDeg;

        /// <summary>
        /// 3D camera distance from the orbit pivot in metres.
        /// </summary>
        public double OrbitDistance => _Project.ViewerState.Camera3D.DistanceM;

        /// <summary>
        /// 3D orbit pivot latitude in degrees.
        /// </summary>
        public double OrbitCentreLat => _Project.ViewerState.Camera3D.OrbitCentreLat;

        /// <summary>
        /// 3D orbit pivot longitude in degrees.
        /// </summary>
        public double OrbitCentreLon => _Project.ViewerState.Camera3D.OrbitCentreLon;

        /// <summary>
        /// Initializes the main window view model and synchronizes bindings from the default project.
        /// </summary>
        public MainWindowViewModel
            (
            )
        {
            AiChat = new AiChatViewModel(
                () => _Project,
                () => UserSettingsService.Instance.ActiveAiSettings,
                () => _ToolDataSession);
            AiChat.SurfaceEditLifecycleChanged += OnAiSurfaceEditLifecycleChanged;
            _ToolDataSession.Changed += OnToolDataSessionChanged;
            InitializeHaulIntegration();
            RefreshAll();
        }

        /// <summary>
        /// Requests a viewer redraw when AI surface preview or commit state changes.
        /// </summary>
        /// <param name="Kind">Lifecycle event kind.</param>
        private void OnAiSurfaceEditLifecycleChanged
            (
            SurfaceEditLifecycleKind Kind
            )
        {
            if (!Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() => OnAiSurfaceEditLifecycleChanged(Kind));
                return;
            }

            ImportDiagnosticsLog.Write(
                "AiViewer",
                "SurfaceEditLifecycle",
                $"kind={Kind} previewActive={AiChat.EditSession.PreviewActive} " +
                $"lastOperation={AiChat.EditSession.LastOperation} contentView={_Project.ViewerState.FieldContentView} " +
                $"proposedTopoDirty={_Project.TopoGeometryDirty.ProposedIsDirty}");

            RefreshAll();
            InvalidateViewerSurface?.Invoke();
            InvalidateViewer2DFrameCache?.Invoke();
            InvalidateViewer3DFrameCache?.Invoke();
        }

        /// <summary>
        /// Requests a viewer redraw when session tool data changes.
        /// </summary>
        private void OnToolDataSessionChanged
            (
            )
        {
            if (!Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(OnToolDataSessionChanged);
                return;
            }

            OnPropertyChanged(nameof(ToolDataSession));
            InvalidateViewerDecorations?.Invoke();
        }

        /// <summary>
        /// Invalidates only the decorations layer on the active field viewer.
        /// </summary>
        public Action? InvalidateViewerDecorations { get; set; }

        /// <summary>
        /// Toggles visibility of the AI chat panel below the field viewer.
        /// </summary>
        [RelayCommand]
        private void ToggleAiPanel
            (
            )
        {
            DeactivateMapTool();
            IsAiPanelVisible = !IsAiPanelVisible;
            OnPropertyChanged(nameof(IsAiPanelHidden));
        }

        /// <summary>
        /// Updates the 2D camera and status bar when the map view centre or zoom changes.
        /// </summary>
        /// <param name="Lat">New centre latitude in degrees.</param>
        /// <param name="Lon">New centre longitude in degrees.</param>
        /// <param name="Zoom">New zoom factor.</param>
        public void OnViewCentreChanged
            (
            double Lat,
            double Lon,
            double Zoom
            )
        {
            // Mirror camera centre and zoom into project state and status bar labels.
            _Project.ViewerState.Camera2D.CentreLat = Lat;
            _Project.ViewerState.Camera2D.CentreLon = Lon;
            _Project.ViewerState.Camera2D.Zoom = Zoom;
            StatusLatitude = CoordinateFormat.FormatLatitude(Lat);
            StatusLongitude = CoordinateFormat.FormatLongitude(Lon);
        }

        /// <summary>
        /// Fits 2D and 3D cameras to the project field bounds for the given viewport size.
        /// </summary>
        /// <param name="ViewportWidth">Viewport width in pixels.</param>
        /// <param name="ViewportHeight">Viewport height in pixels.</param>
        /// <param name="ScheduleInitial3DView">
        /// When true, the next 3D entry (or an immediate fit when already in 3D) uses the
        /// standard north-east oblique orientation and viewport fit.
        /// </param>
        public void FitViewerCameraToField
            (
            int ViewportWidth = DEFAULT_VIEWPORT_WIDTH,
            int ViewportHeight = DEFAULT_VIEWPORT_HEIGHT,
            bool ScheduleInitial3DView = false
            )
        {
            if (!FieldBounds.TryFromProject(_Project, out FieldBounds Bounds))
                return;

            MapViewFitResult Fit = MapViewFitter.Fit(Bounds, ViewportWidth, ViewportHeight);
            _Project.ViewerState.Camera2D.CentreLat = Fit.CentreLat;
            _Project.ViewerState.Camera2D.CentreLon = Fit.CentreLon;
            _Project.ViewerState.Camera2D.Zoom = Fit.ZoomFactor;
            StatusLatitude = CoordinateFormat.FormatLatitude(Fit.CentreLat);
            StatusLongitude = CoordinateFormat.FormatLongitude(Fit.CentreLon);

            _Project.ViewerState.Camera3D.OrbitCentreLat = Fit.CentreLat;
            _Project.ViewerState.Camera3D.OrbitCentreLon = Fit.CentreLon;

            if (ScheduleInitial3DView)
            {
                OrbitViewFitter.ApplyInitialOrientation(_Project.ViewerState.Camera3D);
                _ApplyInitial3DCameraOnNextEntry = true;
                if (_Project.ViewerState.ViewMode == ViewMode.ThreeD)
                {
                    QueueInitial3DCameraFit?.Invoke();
                    _ApplyInitial3DCameraOnNextEntry = false;
                }
            }
            else
            {
                // Derive 3D distance from field extent so the whole site remains in frame.
                UTM.UTMCoordinate SouthWest = UTM.FromLatLon(Bounds.MinLat, Bounds.MinLon);
                UTM.UTMCoordinate NorthEast = UTM.FromLatLon(Bounds.MaxLat, Bounds.MaxLon);
                double ExtentM = Math.Max(
                    Math.Abs(NorthEast.Easting - SouthWest.Easting),
                    Math.Abs(NorthEast.Northing - SouthWest.Northing));
                _Project.ViewerState.Camera3D.DistanceM = Math.Clamp(
                    ExtentM * CAMERA_DISTANCE_EXTENT_FACTOR,
                    CAMERA_DISTANCE_MIN_M,
                    CAMERA_DISTANCE_MAX_M);
            }

            NotifyViewerProperties();
        }

        /// <summary>
        /// Clears the pending initial 3D camera fit flag once queued on the viewer.
        /// </summary>
        private void ApplyInitial3DViewIfNeeded
            (
            )
        {
            if (!_ApplyInitial3DCameraOnNextEntry || !HasSurvey)
                return;

            QueueInitial3DCameraFit?.Invoke();
            _ApplyInitial3DCameraOnNextEntry = false;
        }

        /// <summary>
        /// Ends the viewer busy overlay and restores normal synchronous render fallback.
        /// </summary>
        private void EndViewerBusy
            (
            )
        {
            IsViewerBusy = false;
            ClearViewerBusyProgress();
            if (FieldContentView != FieldContentView.Haul)
            {
                if (HasMaterializedViewerCompositeFrame?.Invoke() != true)
                    PresentViewerCachedFrame?.Invoke();
            }
            else if (IsThreeD)
            {
                // intent: haul 3D keeps pinned leveled elevations committed during prepare
                InvalidateViewerVisual?.Invoke();
            }
            else
            {
                InvalidateViewerSurface?.Invoke();
            }

            ExitViewerTransition?.Invoke();
        }

        /// <summary>
        /// Hides the viewer busy overlay without re-enabling synchronous render fallback.
        /// </summary>
        private void DismissViewerBusyOverlay
            (
            )
        {
            IsViewerBusy = false;
            ClearViewerBusyProgress();
        }

        /// <summary>
        /// Finishes AGD import: ensure composite bitmaps are ready, then restore normal rendering.
        /// </summary>
        private void CompleteViewerImport
            (
            )
        {
            ImportDiagnosticsLog.Write("Import", "CompleteViewerImport START");
            WriteViewerImportDiagnostics("before-complete");

            bool HandoffComplete = ImportViewerHandoff.CompleteImport(
                ImportViewerHandoffTelemetry.Recorder,
                () => HasMaterializedViewerCompositeFrame?.Invoke() == true,
                () =>
                {
                    PresentViewerCachedFrame?.Invoke();
                    return HasMaterializedViewerCompositeFrame?.Invoke() == true;
                },
                () => MarkViewerImportPrepareComplete?.Invoke(),
                () => ExitViewerTransition?.Invoke());

            if (!HandoffComplete)
            {
                ImportDiagnosticsLog.Write(
                    "Import",
                    "CompleteViewerImport DEFERRED",
                    "composite not materialized; suppress and input block retained");
                WriteViewerImportDiagnostics("complete-deferred");
                return;
            }

            MarkViewerImportHandoffPendingFirstDraw?.Invoke();
            ExitImportInputBlock?.Invoke();
            IsViewerBusy = false;
            ClearViewerBusyProgress();
            ImportViewerHandoff.NotifyOverlayDismissed(ImportViewerHandoffTelemetry.Recorder);
            WriteViewerImportDiagnostics("after-complete");
            ImportDiagnosticsLog.Mark("CompleteViewerImport", "handoff complete, suppress released");
        }

        /// <summary>
        /// Writes import handoff diagnostics from the active viewer when logging is enabled.
        /// </summary>
        private void WriteViewerImportDiagnostics
            (
            string Phase
            )
        {
            WriteViewerImportDiagnosticsDelegate?.Invoke(Phase);
        }

        /// <summary>
        /// Fits the 3D orbit camera using the live viewer surface and viewport size.
        /// </summary>
        public void FitOrbitCameraFromViewer
            (
            FieldSurfaceSampler Surface,
            int ViewportWidth,
            int ViewportHeight
            )
        {
            if (!HasSurvey || (Surface.SampleWidth <= 0))
                return;

            OrbitViewFitter.FitToField(
                _Project,
                Surface,
                _Project.ViewerState.Camera3D,
                _Project.ViewerState.Display.VerticalExaggeration,
                ViewportWidth,
                ViewportHeight);
            NotifyViewerProperties();
        }

        /// <summary>
        /// Updates the 3D orbit pivot stored in project viewer state.
        /// </summary>
        /// <param name="Lat">Orbit pivot latitude in degrees.</param>
        /// <param name="Lon">Orbit pivot longitude in degrees.</param>
        public void OnOrbitCentreChanged
            (
            double Lat,
            double Lon
            )
        {
            _Project.ViewerState.Camera3D.OrbitCentreLat = Lat;
            _Project.ViewerState.Camera3D.OrbitCentreLon = Lon;
        }

        /// <summary>
        /// Mirrors the live 3D orbit camera from the viewer into project viewer state.
        /// </summary>
        public void OnOrbitCameraChanged
            (
            double HeadingDeg,
            double PitchDeg,
            double DistanceM,
            double CentreLat,
            double CentreLon
            )
        {
            Camera3DState Camera = _Project.ViewerState.Camera3D;
            Camera.OrbitHeadingDeg = HeadingDeg;
            Camera.OrbitPitchDeg = PitchDeg;
            Camera.DistanceM = DistanceM;
            Camera.OrbitCentreLat = CentreLat;
            Camera.OrbitCentreLon = CentreLon;
        }

        /// <summary>
        /// Recentres the active view on the field geometric centre (clears pan offset from the prior mode).
        /// </summary>
        private void ResetViewCentreToField
            (
            )
        {
            if (!FieldBounds.TryFromProject(_Project, out FieldBounds Bounds))
                return;

            if (_Project.ViewerState.ViewMode == ViewMode.TwoD)
            {
                _Project.ViewerState.Camera2D.CentreLat = Bounds.CentreLat;
                _Project.ViewerState.Camera2D.CentreLon = Bounds.CentreLon;
                StatusLatitude = CoordinateFormat.FormatLatitude(Bounds.CentreLat);
                StatusLongitude = CoordinateFormat.FormatLongitude(Bounds.CentreLon);
            }
            else
            {
                _Project.ViewerState.Camera3D.OrbitCentreLat = Bounds.CentreLat;
                _Project.ViewerState.Camera3D.OrbitCentreLon = Bounds.CentreLon;
            }
        }

        /// <summary>
        /// Keeps view mode, display toggles, and project viewer state aligned when 2D is selected.
        /// </summary>
        /// <param name="Value">New value of the 2D mode toggle.</param>
        partial void OnIsTwoDChanged
            (
            bool Value
            )
        {
            if (!Value || _SuppressModeChange || IsViewerBusy)
                return;

            _ = TransitionViewModeAsync(ViewMode.TwoD);
        }

        /// <summary>
        /// Keeps view mode, display toggles, and project viewer state aligned when 3D is selected.
        /// </summary>
        /// <param name="Value">New value of the 3D mode toggle.</param>
        partial void OnIsThreeDChanged
            (
            bool Value
            )
        {
            if (!Value || _SuppressModeChange || IsViewerBusy || !CanSwitchToThreeDView)
                return;

            BeginViewerBusy("Switching to 3D view");
            _ = TransitionViewModeAsync(ViewMode.ThreeD);
        }

        /// <summary>
        /// Switches 2D/3D view mode on a worker thread while showing a viewer progress overlay.
        /// </summary>
        /// <param name="Mode">Target view mode.</param>
        private async Task TransitionViewModeAsync
            (
            ViewMode Mode
            )
        {
            if (PrepareViewerAsync == null)
                return;

            string Message = Mode == ViewMode.TwoD ? "Switching to 2D view" : "Switching to 3D view";
            SuppressViewerFrameInvalidation = true;
            if (Mode != ViewMode.ThreeD)
                BeginViewerBusy(Message);

            await YieldUiAsync().ConfigureAwait(true);
            await YieldUiAsync().ConfigureAwait(true);
            try
            {
                // Haul 2D uses bitmap scrub rendering; haul 3D uses the standard existing-surface pipeline.
                if (FieldContentView == FieldContentView.Haul)
                {
                    if (Mode == ViewMode.ThreeD && !HasCurrentHaulCutFillFrame)
                        return;

                    if (Mode == ViewMode.TwoD)
                    {
                        _SuppressModeChange = true;
                        IsThreeD = false;
                        ShowVerticalExaggeration = false;
                        _Project.ViewerState.ViewMode = Mode;
                        SyncHaulRouteSectionVisibility();
                        OnPropertyChanged(nameof(ViewMode));
                        _SuppressModeChange = false;

                        await YieldUiAsync().ConfigureAwait(true);
                        InvalidateViewerVisual?.Invoke();
                        return;
                    }

                    PinHaulThreeDScrubIndex();
                }

                if (Mode == ViewMode.ThreeD)
                {
                    if (_ApplyInitial3DCameraOnNextEntry && HasSurvey)
                    {
                        QueueInitial3DCameraFit?.Invoke();
                        _ApplyInitial3DCameraOnNextEntry = false;
                    }

                    InvalidateViewer3DFrameCache?.Invoke();
                }
                else
                {
                    InvalidateViewer2DFrameCache?.Invoke();
                }

                _SuppressModeChange = true;
                if (Mode == ViewMode.TwoD)
                {
                    IsThreeD = false;
                    ShowVerticalExaggeration = false;
                }
                else
                {
                    IsTwoD = false;
                    ShowVerticalExaggeration = true;
                }

                _Project.ViewerState.ViewMode = Mode;
                SyncDisplayToProject();
                if (FieldContentView == FieldContentView.Haul)
                    SyncHaulRouteSectionVisibility();
                OnPropertyChanged(nameof(ViewMode));
                DeactivateMapTool();

                OnPropertyChanged(nameof(CanAddMapToolData));
                AddPointToolCommand.NotifyCanExecuteChanged();
                AddPolylineToolCommand.NotifyCanExecuteChanged();
                PolygonToolCommand.NotifyCanExecuteChanged();
                _SuppressModeChange = false;

                await YieldUiAsync().ConfigureAwait(true);

                IProgress<int> Progress = CreateViewerProgressReporter(Message);
                await PrepareViewerAsync(Progress, Mode).ConfigureAwait(true);
                SetViewerBusyProgress(100, Message);

                if (Mode == ViewMode.ThreeD)
                    NotifyViewerProperties();
            }
            finally
            {
                SuppressViewerFrameInvalidation = false;
                EndViewerBusy();
            }
        }

        /// <summary>
        /// Runs viewer preparation with a centred progress overlay.
        /// </summary>
        /// <param name="Message">Progress message shown over the viewer.</param>
        /// <param name="ApplyProjectChange">Optional project mutation to run after the overlay is visible.</param>
        /// <param name="ProgressMin">Minimum percent for the preparation phase.</param>
        /// <param name="ProgressMax">Maximum percent for the preparation phase.</param>
        private async Task RunViewerPrepareAsync
            (
            string Message,
            Action? ApplyProjectChange = null,
            int ProgressMin = 0,
            int ProgressMax = 100
            )
        {
            if (PrepareViewerAsync == null)
                return;

            BeginViewerBusy(Message, ProgressMin);
            await YieldUiAsync().ConfigureAwait(true);
            try
            {
                ApplyProjectChange?.Invoke();
                IProgress<int> Progress = CreateViewerProgressReporter(Message, ProgressMin, ProgressMax);
                await PrepareViewerAsync(Progress, _Project.ViewerState.ViewMode).ConfigureAwait(true);
                SetViewerBusyProgress(ProgressMax, Message);
            }
            finally
            {
                EndViewerBusy();
            }
        }

        /// <summary>
        /// Shows the viewer busy overlay and resets progress to the given starting percent.
        /// </summary>
        /// <param name="Message">Overlay status message.</param>
        /// <param name="StartPercent">Initial progress percentage.</param>
        private void BeginViewerBusy
            (
            string Message,
            int StartPercent = 0
            )
        {
            IsViewerBusy = true;
            EnterViewerTransition?.Invoke();
            ResetViewerBusyProgress(StartPercent);
            SetViewerBusyProgress(StartPercent, Message);
        }

        /// <summary>
        /// Shows the viewer busy overlay for AGD import and records handoff telemetry.
        /// </summary>
        private void BeginImportViewerBusy
            (
            string Message,
            int StartPercent = 0
            )
        {
            ImportViewerHandoff.BeginImport(
                ImportViewerHandoffTelemetry.Recorder,
                () =>
                {
                    IsViewerBusy = true;
                    EnterViewerTransition?.Invoke();
                    ResetViewerBusyProgress(StartPercent);
                    SetViewerBusyProgress(StartPercent, Message);
                });
            EnterImportInputBlock?.Invoke();
        }

        /// <summary>
        /// Updates viewer overlay progress and message with a percentage suffix.
        /// </summary>
        /// <param name="Percent">Progress percentage to display.</param>
        /// <param name="Message">Overlay status message without the percent suffix.</param>
        private void SetViewerBusyProgress
            (
            int Percent,
            string Message
            )
        {
            void Apply()
            {
                int Monotonic = _ViewerBusyProgressTracker.Advance(Percent);
                ViewerBusyProgress = Monotonic;
                ViewerBusyMessage = $"{Message} ({Monotonic:0}%)";
            }

            if (Dispatcher.UIThread.CheckAccess())
                Apply();
            else
                Dispatcher.UIThread.Post(Apply, DispatcherPriority.Render);
        }

        /// <summary>
        /// Updates import overlay progress using phase-specific messages derived from the monotonic percent.
        /// </summary>
        /// <param name="Percent">Requested import progress percent.</param>
        private void SetImportViewerBusyProgress
            (
            int Percent
            )
        {
            void Apply()
            {
                int Monotonic = _ViewerBusyProgressTracker.Advance(Percent);
                ViewerBusyProgress = Monotonic;
                ViewerBusyMessage = $"{GetImportPhaseMessage(Monotonic)} ({Monotonic:0}%)";
            }

            if (Dispatcher.UIThread.CheckAccess())
                Apply();
            else
                Dispatcher.UIThread.Post(Apply, DispatcherPriority.Render);
        }

        /// <summary>
        /// Resets monotonic progress tracking for a new viewer busy session.
        /// </summary>
        /// <param name="StartPercent">Initial progress percent.</param>
        private void ResetViewerBusyProgress
            (
            int StartPercent = 0
            )
        {
            void Apply()
            {
                _ViewerBusyProgressTracker.Reset(StartPercent);
            }

            if (Dispatcher.UIThread.CheckAccess())
                Apply();
            else
                Dispatcher.UIThread.Post(Apply, DispatcherPriority.Render);
        }

        /// <summary>
        /// Clears overlay progress state when the viewer busy session ends.
        /// </summary>
        private void ClearViewerBusyProgress
            (
            )
        {
            _ViewerBusyProgressTracker.Reset(0);
            ViewerBusyProgress = 0;
        }

        /// <summary>
        /// Creates a progress reporter that maps 0–100 sub-progress into a percent range on the viewer overlay.
        /// </summary>
        /// <param name="Message">Overlay status message.</param>
        /// <param name="MinPercent">Minimum mapped progress percent.</param>
        /// <param name="MaxPercent">Maximum mapped progress percent.</param>
        /// <returns>Progress reporter for long-running viewer operations.</returns>
        private IProgress<int> CreateViewerProgressReporter
            (
            string Message,
            int MinPercent = 0,
            int MaxPercent = 100
            )
        {
            return new Progress<int>(Value =>
            {
                int Scaled = MinPercent + (int)Math.Round(Value * (MaxPercent - MinPercent) / (double)PROGRESS_PERCENT_MAX);
                SetViewerBusyProgress(Scaled, Message);
            });
        }

        /// <summary>
        /// Creates a progress reporter for AGD import with phase-specific overlay messages.
        /// </summary>
        /// <returns>Progress reporter for AGD import.</returns>
        private IProgress<int> CreateImportProgressReporter
            (
            )
        {
            return new Progress<int>(SetImportViewerBusyProgress);
        }

        /// <summary>
        /// Returns the viewer overlay message for an AGD import progress percent.
        /// </summary>
        /// <param name="Percent">Current import progress percent.</param>
        /// <returns>Phase-specific overlay message.</returns>
        private static string GetImportPhaseMessage
            (
            int Percent
            )
        {
            if (Percent < IMPORT_PHASE_READING_MAX_PERCENT)
                return "Reading design file";
            if (Percent < IMPORT_PHASE_BUILDING_MAX_PERCENT)
                return "Building surface";
            if (Percent < IMPORT_PHASE_PREVIEW_MAX_PERCENT)
                return "Preparing preview";
            if (Percent < IMPORT_PHASE_FINISHING_MAX_PERCENT)
                return "Finishing display";
            return "Preparing view";
        }

        /// <summary>
        /// Returns the viewer overlay message for a project open progress percent.
        /// </summary>
        /// <param name="Percent">Current open progress percent.</param>
        /// <returns>Phase-specific overlay message.</returns>
        private static string GetOpenProjectPhaseMessage
            (
            int Percent
            )
        {
            if (Percent < OPEN_PHASE_READING_MAX_PERCENT)
                return "Reading project file";
            if (Percent < IMPORT_PHASE_BUILDING_MAX_PERCENT)
                return "Building surface";
            if (Percent < IMPORT_PHASE_PREVIEW_MAX_PERCENT)
                return "Preparing preview";
            if (Percent < IMPORT_PHASE_FINISHING_MAX_PERCENT)
                return "Finishing display";
            return "Loading project";
        }

        /// <summary>
        /// Creates a progress reporter for project open with phase-specific overlay messages.
        /// </summary>
        /// <returns>Progress reporter for project open.</returns>
        private IProgress<int> CreateOpenProjectProgressReporter
            (
            )
        {
            return new Progress<int>(Percent => SetViewerBusyProgress(Percent, GetOpenProjectPhaseMessage(Percent)));
        }

        /// <summary>
        /// Updates the design-generation overlay progress and status message on the UI thread.
        /// </summary>
        /// <param name="Percent">Progress percent from 0 to 100.</param>
        /// <param name="Message">Field-creator phase description.</param>
        private void SetDesignBusyProgress
            (
            int Percent,
            string Message
            )
        {
            void Apply()
            {
                DesignBusyProgress = Percent;
                DesignBusyMessage = $"{Message} ({Percent:0}%)";
            }

            if (Dispatcher.UIThread.CheckAccess())
                Apply();
            else
                Dispatcher.UIThread.Post(Apply, DispatcherPriority.Render);
        }

        /// <summary>
        /// Maps field-creator status strings to design progress percentages.
        /// </summary>
        /// <param name="Message">Field creator status message.</param>
        /// <returns>Progress percent for the design busy overlay.</returns>
        private static int MapDesignProgress
            (
            string Message
            )
        {
            if (Message.StartsWith("MLS interpolation", StringComparison.Ordinal))
            {
                int SlashIndex = Message.IndexOf('/', StringComparison.Ordinal);
                int RowsIndex = Message.IndexOf("rows", StringComparison.Ordinal);
                if ((SlashIndex > 0) && (RowsIndex > SlashIndex))
                {
                    ReadOnlySpan<char> RowPart = Message.AsSpan(0, SlashIndex).Trim();
                    int LastSpace = RowPart.LastIndexOf(' ');
                    if (LastSpace >= 0)
                    {
                        ReadOnlySpan<char> CurrentText = RowPart[(LastSpace + 1)..];
                        ReadOnlySpan<char> TotalText = Message.AsSpan(SlashIndex + 1, RowsIndex - SlashIndex - 1).Trim();
                        if (int.TryParse(CurrentText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int Current) &&
                            int.TryParse(TotalText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int Total) &&
                            (Total > 0))
                        {
                            return MLS_PROGRESS_BASE_PERCENT + (int)Math.Round(MLS_PROGRESS_SPAN_PERCENT * (double)Current / Total);
                        }
                    }
                }

                return MLS_PROGRESS_FALLBACK_PERCENT;
            }

            return Message switch
            {
                "Building bin grid and existing surface…" => DESIGN_PROGRESS_GRID_BUILD_PERCENT,
                "Smoothing baseline and warping toward survey bins…" => DESIGN_PROGRESS_SMOOTHING_PERCENT,
                "Existing surface ready." => DESIGN_PROGRESS_EXISTING_READY_PERCENT,
                "Building per-bin samples and solving design plane…" => DESIGN_PROGRESS_PLANE_SOLVE_PERCENT,
                "Solving cut/fill ratio (plane offset)…" => DESIGN_PROGRESS_RATIO_SOLVE_PERCENT,
                "Computing per-bin targets and volumes…" => DESIGN_PROGRESS_VOLUMES_PERCENT,
                "Done." => DESIGN_PROGRESS_COMPLETE_PERCENT,
                _ => DESIGN_PROGRESS_UNKNOWN_PERCENT
            };
        }

        /// <summary>
        /// Yields one UI frame so bindings (progress overlay) can paint before heavy work.
        /// </summary>
        private static async Task YieldUiAsync
            (
            )
        {
            await Dispatcher.UIThread.InvokeAsync(
                () => { },
                DispatcherPriority.Loaded);
            await Dispatcher.UIThread.InvokeAsync(
                () => { },
                DispatcherPriority.Render);
        }

        /// <summary>
        /// Propagates vertical exaggeration to the status label and project display settings.
        /// </summary>
        /// <param name="Value">New vertical exaggeration multiplier.</param>
        partial void OnVerticalExaggerationChanged
            (
            double Value
            )
        {
            VerticalExaggerationLabel = Value <= 0.0
                ? "True scale"
                : Value.ToString("F1", CultureInfo.InvariantCulture) + "×";
            if (_SuppressDisplaySync)
                return;

            DeactivateMapToolForDisplayChange();
            _Project.ViewerState.Display.VerticalExaggeration = Value;
            _Project.TopoGeometryDirty.MarkAllDirty();
        }

        /// <summary>
        /// Propagates topo/cut-fill opacity to the sidebar label and project display settings.
        /// </summary>
        /// <param name="Value">New opacity percentage (0–100).</param>
        partial void OnTopoCutFillOpacityChanged
            (
            double Value
            )
        {
            double Clamped = Math.Clamp(Value, DisplaySettings.OPACITY_PERCENT_MIN, DisplaySettings.OPACITY_PERCENT_MAX);
            TopoCutFillOpacityLabel = Clamped.ToString("F0", CultureInfo.InvariantCulture) + "%";
            if (_SuppressDisplaySync)
                return;

            DeactivateMapToolForDisplayChange();
            _Project.ViewerState.Display.TopoCutFillOpacityPercent = Clamped;
        }

        /// <summary>
        /// Deactivates the active map tool when a DISPLAY panel toggle changes.
        /// </summary>
        private void DeactivateMapToolForDisplayChange
            (
            )
        {
            if (_SuppressDisplaySync)
                return;

            DeactivateMapTool();
        }

        /// <summary>
        /// Mirrors the survey-points overlay toggle into project display settings.
        /// </summary>
        /// <param name="Value">New survey-points visibility.</param>
        partial void OnSurveyPointsToggleChanged
            (
            bool Value
            )
        {
            DeactivateMapToolForDisplayChange();
            if (_SuppressDisplaySync)
                return;

            SyncDisplayToProject();
            OnPropertyChanged(nameof(Display));
        }

        /// <summary>
        /// Mirrors the contours overlay toggle into project display settings.
        /// </summary>
        /// <param name="Value">New contours visibility.</param>
        partial void OnContoursToggleChanged
            (
            bool Value
            )
        {
            DeactivateMapToolForDisplayChange();
            if (_SuppressDisplaySync)
                return;

            SyncDisplayToProject();
            OnPropertyChanged(nameof(Display));
        }

        /// <summary>
        /// Mirrors the hillshade overlay toggle into project display settings.
        /// </summary>
        /// <param name="Value">New hillshade visibility.</param>
        partial void OnHillshadeToggleChanged
            (
            bool Value
            )
        {
            DeactivateMapToolForDisplayChange();
            if (_SuppressDisplaySync)
                return;

            SyncDisplayToProject();
            OnPropertyChanged(nameof(Display));
        }

        /// <summary>
        /// Mirrors the benchmarks overlay toggle into project display settings.
        /// </summary>
        /// <param name="Value">New benchmarks visibility.</param>
        partial void OnBenchmarksToggleChanged
            (
            bool Value
            )
        {
            DeactivateMapToolForDisplayChange();
            if (_SuppressDisplaySync)
                return;

            SyncDisplayToProject();
            OnPropertyChanged(nameof(Display));
        }

        /// <summary>
        /// Mirrors the design overlay toggle into project display settings.
        /// </summary>
        /// <param name="Value">New design overlay visibility.</param>
        partial void OnDesignOverlayToggleChanged
            (
            bool Value
            )
        {
            DeactivateMapToolForDisplayChange();
            if (_SuppressDisplaySync)
                return;

            SyncDisplayToProject();
            OnPropertyChanged(nameof(Display));
        }

        /// <summary>
        /// Mirrors the satellite basemap toggle into project display settings.
        /// </summary>
        /// <param name="Value">New satellite basemap visibility.</param>
        partial void OnSatelliteToggleChanged
            (
            bool Value
            )
        {
            DeactivateMapToolForDisplayChange();
            if (_SuppressDisplaySync)
                return;

            if (!HasSurvey)
            {
                if (Value)
                {
                    _SuppressDisplaySync = true;
                    SatelliteToggle = false;
                    _SuppressDisplaySync = false;
                }
                return;
            }

            SyncDisplayToProject();
            OnPropertyChanged(nameof(Display));
        }

        /// <summary>
        /// Creates a blank project after optional save of unsaved changes.
        /// </summary>
        [RelayCommand]
        private async Task NewProjectAsync
            (
            )
        {
            if (_Project.IsModified)
            {
                // Offer save-before-discard when the current project has unsaved edits.
                bool Save = await DialogService.ShowConfirmAsync(
                    "Unsaved changes",
                    "Save changes to the current project before starting a new one?");
                if (Save)
                    await SaveProjectAsync();
            }

            _Project.ResetToEmpty();
            ApplyDefaultFieldContentView();
            ClearToolData();
            RefreshAll();

            BeginViewerBusy("Resetting view");
            await YieldUiAsync().ConfigureAwait(true);
            try
            {
                ResetViewerForEmptyProject?.Invoke();
                SetViewerBusyProgress(100, "Resetting view");
            }
            finally
            {
                EndViewerBusy();
            }
        }

        /// <summary>
        /// Opens a project file from disk and refreshes all bindings.
        /// </summary>
        [RelayCommand]
        private async Task OpenProjectAsync
            (
            )
        {
            string? Path = await DialogService.PickOpenFileAsync("Open Project", "*.agproject");
            if (Path == null)
                return;

            try
            {
                BeginViewerBusy("Reading project file");
                await YieldUiAsync().ConfigureAwait(true);

                IProgress<int> OpenProgress = CreateOpenProjectProgressReporter();
                ProjectDocument Loaded = await Task.Run(() => AgDesignSerializer.Load(Path)).ConfigureAwait(true);
                SetViewerBusyProgress(12, GetOpenProjectPhaseMessage(12));

                SuppressViewerFrameInvalidation = true;
                try
                {
                    _Project = Loaded;
                    _Project.TopoGeometryDirty.MarkAllDirty();
                    ClearToolData();
                    ApplyOpenFieldContentView();
                    FitViewerCameraToField();
                    RefreshAll();
                }
                finally
                {
                    SuppressViewerFrameInvalidation = false;
                }

                await YieldUiAsync().ConfigureAwait(true);

                if (PrepareViewerProgressiveAsync == null)
                {
                    await RunViewerPrepareAsync("Loading project…", ProgressMin: 12).ConfigureAwait(true);
                    return;
                }

                bool OverlayDismissed = false;
                await PrepareViewerProgressiveAsync(
                    ProgressMapper.Subrange(OpenProgress, 12, 100),
                    () =>
                    {
                        if (OverlayDismissed)
                            return;

                        OverlayDismissed = true;
                        PresentViewerCachedFrame?.Invoke();
                    },
                    _Project.ViewerState.ViewMode).ConfigureAwait(true);
            }
            catch (Exception Ex)
            {
                await DialogService.ShowMessageAsync("Open failed", Ex.Message);
            }
            finally
            {
                EndViewerBusy();
            }
        }

        /// <summary>
        /// Saves the project to its current path, or prompts for a path when none is set.
        /// </summary>
        [RelayCommand]
        private async Task SaveProjectAsync
            (
            )
        {
            if (string.IsNullOrWhiteSpace(_Project.FilePath))
            {
                await SaveProjectAsAsync();
                return;
            }

            try
            {
                AgDesignSerializer.Save(_Project, _Project.FilePath);
                RefreshTitle();
            }
            catch (Exception Ex)
            {
                await DialogService.ShowMessageAsync("Save failed", Ex.Message);
            }
        }

        /// <summary>
        /// Prompts for a save path and writes the project file.
        /// </summary>
        [RelayCommand]
        private async Task SaveProjectAsAsync
            (
            )
        {
            string? Path = await DialogService.PickSaveFileAsync(
                "Save Project As",
                ProjectDocument.DEFAULT_FILE_NAME,
                "*.agproject");
            if (Path == null)
                return;

            try
            {
                AgDesignSerializer.Save(_Project, Path);
                RefreshTitle();
            }
            catch (Exception Ex)
            {
                await DialogService.ShowMessageAsync("Save failed", Ex.Message);
            }
        }

        /// <summary>
        /// Shows the import choice dialog and runs the selected import workflow.
        /// </summary>
        [RelayCommand]
        private async Task ImportAsync
            (
            )
        {
            ImportChoiceViewModel.ImportKind? Kind = await DialogService.ShowImportChoiceAsync();
            if ((Kind == null) || (Kind == ImportChoiceViewModel.ImportKind.None))
                return;

            if (Kind == ImportChoiceViewModel.ImportKind.Survey)
                await ImportSurveyAsync();
            else
                await ImportDesignAsync();
        }

        /// <summary>
        /// Shows the export choice dialog and writes the selected artefact to disk.
        /// </summary>
        [RelayCommand]
        private async Task ExportAsync
            (
            )
        {
            ExportChoiceViewModel.ExportKind? Kind = await DialogService.ShowExportChoiceAsync(HasSurvey, HasDesign);
            if ((Kind == null) || (Kind == ExportChoiceViewModel.ExportKind.None))
                return;

            try
            {
                if (Kind == ExportChoiceViewModel.ExportKind.SurveyAgs)
                {
                    string? Path = await DialogService.PickSaveFileAsync("Export Survey", "survey.ags", "*.ags");
                    if ((Path != null) && (_Project.Survey != null))
                        _Project.Survey.SaveToAgs(Path);
                }
                else if (Kind == ExportChoiceViewModel.ExportKind.SurveyMultiplane)
                {
                    string? Path = await DialogService.PickSaveFileAsync("Export Survey", "survey.txt", "*.txt");
                    if ((Path != null) && (_Project.Survey != null))
                        _Project.Survey.SaveToMultiplane(Path);
                }
                else if (Kind == ExportChoiceViewModel.ExportKind.DesignAgd)
                {
                    string? Path = await DialogService.PickSaveFileAsync("Export Design", "design.agd", "*.agd");
                    if ((Path != null) && (_Project.Survey != null) && (_Project.DesignGrid != null) && (_Project.DesignParameters != null))
                        AgdService.Export(Path, _Project.Survey, _Project.DesignGrid, _Project.DesignParameters);
                }
            }
            catch (Exception Ex)
            {
                await DialogService.ShowMessageAsync("Export failed", Ex.Message);
            }
        }

        /// <summary>
        /// Opens the design dialog and generates or replaces the field design from survey data.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanDesign))]
        private async Task OpenDesignDialogAsync
            (
            )
        {
            if (!HasSurvey)
                return;

            DesignDialogViewModel? Result = await DialogService.ShowDesignDialogAsync(_Project.DesignParameters);
            if (Result == null)
                return;

            if (HasDesign)
            {
                // Confirm overwrite when a design already exists for this project.
                bool Overwrite = await DialogService.ShowConfirmAsync(
                    "Replace design?",
                    "A design already exists. Replace it with the new parameters?");
                if (!Overwrite)
                    return;
            }

            try
            {
                DesignParameters Params = Result.ToParameters();
                IsDesignBusy = true;
                SetDesignBusyProgress(0, "Generating design…");
                await YieldUiAsync().ConfigureAwait(true);
                (DesignGrid Grid, FieldCreator.Statistics Stats) = await Task.Run(
                    () =>
                    {
                        FieldCreator Creator = new FieldCreator(Message =>
                            SetDesignBusyProgress(MapDesignProgress(Message), Message));
                        return Creator.CreateDesign(_Project.Survey!, Params);
                    }).ConfigureAwait(true);
                ClearDesignBusyOverlay();
                await YieldUiAsync().ConfigureAwait(true);
                bool HadDesign = HasDesign;
                await RunViewerPrepareAsync(
                    "Updating field view",
                    () =>
                    {
                        _Project.DesignParameters = Params;
                        _Project.DesignGrid = Grid;
                        _Project.TopoGeometryDirty.MarkProposedDirty();
                        _Project.ViewerState.FieldContentView = FieldContentView.Proposed;
                        _Project.MarkModified();
                        FitViewerCameraToField(ScheduleInitial3DView: true);
                        if (HadDesign)
                            ClearToolData();
                        RefreshAll();
                    }).ConfigureAwait(true);
            }
            catch (Exception Ex)
            {
                await DialogService.ShowMessageAsync("Design failed", Ex.Message);
            }
            finally
            {
                ClearDesignBusyOverlay();
            }
        }

        /// <summary>
        /// Clears overlay progress state when the design busy session ends.
        /// </summary>
        private void ClearDesignBusyOverlay
            (
            )
        {
            IsDesignBusy = false;
            DesignBusyProgress = 0;
            DesignBusyMessage = "";
        }

        /// <summary>
        /// Shows survey statistics for the current project survey, if any.
        /// </summary>
        [RelayCommand(CanExecute = nameof(HasSurvey))]
        private async Task ShowSurveyStatisticsAsync
            (
            )
        {
            if (!HasSurvey)
                return;

            await DialogService.ShowSurveyStatisticsAsync(_Project.Survey);
        }

        /// <summary>
        /// Opens the settings dialog.
        /// </summary>
        [RelayCommand]
        private async Task OpenSettingsDialogAsync
            (
            )
        {
            DeactivateMapTool();
            bool Confirmed = await DialogService.ShowSettingsDialogAsync();
            if (Confirmed)
                UpdateLegend();
        }

        /// <summary>
        /// Activates or deactivates the Add Point map-editing tool.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanAddMapToolData))]
        private void AddPointTool
            (
            )
        {
            ToggleMapTool(MapEditingToolKind.AddPoint);
        }

        /// <summary>
        /// Activates or deactivates the Add Polyline map-editing tool.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanAddMapToolData))]
        private void AddPolylineTool
            (
            )
        {
            ToggleMapTool(MapEditingToolKind.AddPolyline);
        }

        /// <summary>
        /// Activates or deactivates the Polygon map-editing tool.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanAddMapToolData))]
        private void PolygonTool
            (
            )
        {
            ToggleMapTool(MapEditingToolKind.Polygon);
        }

        /// <summary>
        /// Activates or deactivates the Erase map-editing tool.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanUseMapTools))]
        private void EraseTool
            (
            )
        {
            ToggleMapTool(MapEditingToolKind.Erase);
        }

        /// <summary>
        /// Toggles one map-editing tool on or off, deactivating any other active tool.
        /// </summary>
        /// <param name="Tool">Tool the user selected.</param>
        private void ToggleMapTool
            (
            MapEditingToolKind Tool
            )
        {
            if (!HasDesign)
                return;

            if ((Tool == MapEditingToolKind.AddPoint) || (Tool == MapEditingToolKind.AddPolyline) || (Tool == MapEditingToolKind.Polygon))
            {
                if (!IsTwoD)
                    return;
            }

            if (_ActiveMapTool == Tool)
            {
                DeactivateMapTool();
                return;
            }

            if (_ActiveMapTool == MapEditingToolKind.AddPolyline)
                _ToolDataSession.CompleteCurrentPolyline();

            if (_ActiveMapTool == MapEditingToolKind.Polygon)
                _ToolDataSession.CompleteCurrentPolygon();

            _ActiveMapTool = Tool;
            if (Tool == MapEditingToolKind.AddPolyline)
                _ToolDataSession.BeginNewPolyline();
            if (Tool == MapEditingToolKind.Polygon)
                _ToolDataSession.BeginNewPolygon();

            RefreshMapToolButtons();
        }

        /// <summary>
        /// Deactivates the active map-editing tool with the same effect as toggling it off.
        /// </summary>
        public void DeactivateMapTool
            (
            )
        {
            if (_ActiveMapTool == MapEditingToolKind.None)
                return;

            if (_ActiveMapTool == MapEditingToolKind.AddPolyline)
                _ToolDataSession.CompleteCurrentPolyline();

            if (_ActiveMapTool == MapEditingToolKind.Polygon)
                _ToolDataSession.CompleteCurrentPolygon();

            _ActiveMapTool = MapEditingToolKind.None;
            RefreshMapToolButtons();
        }

        /// <summary>
        /// Clears all session tool data (TOOLS-SPEC §4.2.3).
        /// </summary>
        public void ClearToolData
            (
            )
        {
            DeactivateMapTool();
            _ToolDataSession.Clear();
        }

        /// <summary>
        /// Synchronizes map-editing tool button highlight state from the active tool.
        /// </summary>
        private void RefreshMapToolButtons
            (
            )
        {
            IsAddPointToolActive = (_ActiveMapTool == MapEditingToolKind.AddPoint);
            IsAddPolylineToolActive = (_ActiveMapTool == MapEditingToolKind.AddPolyline);
            IsPolygonToolActive = (_ActiveMapTool == MapEditingToolKind.Polygon);
            IsEraseToolActive = (_ActiveMapTool == MapEditingToolKind.Erase);
            OnPropertyChanged(nameof(ActiveMapTool));
        }

        /// <summary>
        /// Switches the viewer to the existing-surface field content view.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanSelectExistingView))]
        private async Task SelectExistingAsync
            (
            )
        {
            if (!HasSurvey || IsViewerBusy)
                return;

            DeactivateMapTool();
            await TransitionFieldContentViewAsync(FieldContentView.Existing, "Switching to existing view").ConfigureAwait(true);
        }

        /// <summary>
        /// Switches the viewer to the proposed design field content view.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanSelectProposedView))]
        private async Task SelectProposedAsync
            (
            )
        {
            if (!CanProposedView || IsViewerBusy)
                return;

            DeactivateMapTool();
            await TransitionFieldContentViewAsync(FieldContentView.Proposed, "Switching to proposed view").ConfigureAwait(true);
        }

        /// <summary>
        /// Switches the viewer to the cut/fill comparison field content view.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanSelectCutFillView))]
        private async Task SelectCutFillAsync
            (
            )
        {
            if (!CanCutFillView || IsViewerBusy)
                return;

            DeactivateMapTool();
            await TransitionFieldContentViewAsync(FieldContentView.CutFill, "Switching to cut/fill view").ConfigureAwait(true);
        }

        /// <summary>
        /// Switches existing/proposed/cut-fill on a worker thread with frame-cache reuse.
        /// </summary>
        private async Task TransitionFieldContentViewAsync
            (
            FieldContentView View,
            string Message
            )
        {
            if (PrepareViewerAsync == null || IsViewerBusy)
                return;

            FieldContentView PreviousView = _Project.ViewerState.FieldContentView;
            if (PreviousView == FieldContentView.Haul && View != FieldContentView.Haul)
                _HaulScrubController.SuspendForFieldViewSwitch();

            if (View != FieldContentView.Haul)
                ShowHaulRouteSection = false;

            SuppressViewerFrameInvalidation = true;
            try
            {
                _Project.ViewerState.FieldContentView = View;
                RefreshViewButtons();
                UpdateLegend();
                OnPropertyChanged(nameof(FieldContentView));

                bool HasCachedFrame = HasCachedViewerFrame?.Invoke(_Project.ViewerState.ViewMode) ?? false;
                IProgress<int>? Progress = null;
                if (!HasCachedFrame)
                {
                    BeginViewerBusy(Message);
                    await YieldUiAsync().ConfigureAwait(true);
                    Progress = CreateViewerProgressReporter(Message);
                }

                bool CacheHit = await PrepareViewerAsync(
                    Progress ?? EmptyViewerProgress,
                    _Project.ViewerState.ViewMode).ConfigureAwait(true);
                if (!HasCachedFrame && !CacheHit)
                    SetViewerBusyProgress(100, Message);
            }
            finally
            {
                SuppressViewerFrameInvalidation = false;
                EndViewerBusy();
            }
        }

        /// <summary>
        /// Notifies bindings that only the active field content view (and related display toggles) changed.
        /// </summary>
        private void NotifyFieldContentViewChanged
            (
            )
        {
            OnPropertyChanged(nameof(FieldContentView));
        }

        /// <summary>
        /// Imports survey data from AGS or multiplane text, merging or replacing as chosen.
        /// </summary>
        private async Task ImportSurveyAsync
            (
            )
        {
            string? Path = await DialogService.PickOpenFileAsync("Import Survey", "*.ags", "*.txt");
            if (Path == null)
                return;

            try
            {
                BeginViewerBusy("Reading survey file");
                await YieldUiAsync().ConfigureAwait(true);

                SurveyModel Imported = await Task.Run(() =>
                {
                    SurveyModel Model = new SurveyModel();
                    Model.LoadAuto(Path);
                    return Model;
                }).ConfigureAwait(true);

                SetViewerBusyProgress(SURVEY_IMPORT_PROGRESS_AFTER_READ_PERCENT, "Reading survey file");

                bool ReplaceExisting = false;
                if (HasSurvey)
                {
                    EndViewerBusy();
                    ReplaceExisting = await DialogService.ShowConfirmAsync(
                        "Survey import",
                        "Replace existing survey data? Choose No to add imported points to the current survey.").ConfigureAwait(true);
                }

                await RunViewerPrepareAsync(
                    "Loading survey",
                    () =>
                    {
                        if (HasSurvey)
                        {
                            if (ReplaceExisting)
                                _Project.Survey = Imported;
                            else
                            {
                                _Project.Survey ??= new SurveyModel();
                                _Project.Survey.MergeFrom(Imported);
                            }
                        }
                        else
                        {
                            _Project.Survey = Imported;
                        }

                        _Project.SurveySourceFormat = Path.EndsWith(".ags", StringComparison.OrdinalIgnoreCase)
                            ? SurveySourceFormat.Ags
                            : SurveySourceFormat.Multiplane;
                        _Project.TopoGeometryDirty.MarkExistingDirty();
                        _Project.ViewerState.FieldContentView = FieldContentView.Existing;
                        _Project.ViewerState.Display.SurveyPoints = false;
                        _Project.MarkModified();
                        FitViewerCameraToField(ScheduleInitial3DView: true);
                        ClearToolData();
                        RefreshAll();
                    },
                    ProgressMin: SURVEY_IMPORT_PROGRESS_AFTER_READ_PERCENT).ConfigureAwait(true);
            }
            catch (Exception Ex)
            {
                await DialogService.ShowMessageAsync("Import failed", Ex.Message);
            }
            finally
            {
                EndViewerBusy();
            }
        }

        /// <summary>
        /// Imports a complete AGD design package, replacing existing survey and design when confirmed.
        /// </summary>
        private async Task ImportDesignAsync
            (
            )
        {
            string? Path = await DialogService.PickOpenFileAsync("Import Design", "*.agd");
            if (Path == null)
                return;

            if (HasDesign || HasSurvey)
            {
                // AGD import replaces both survey and design; confirm before destructive merge.
                bool Replace = await DialogService.ShowConfirmAsync(
                    "Import design",
                    "Replace existing project survey and design data with the imported AGD file?");
                if (!Replace)
                    return;
            }

            try
            {
                string BusyMessage = ImportDiagnosticsLog.IsEnabled
                    ? $"Reading design file — log: {System.IO.Path.GetFileName(ImportDiagnosticsLog.LogFilePath ?? "aggradedesigner.log")}"
                    : "Reading design file";
                ImportDiagnosticsLog.Write("Import", "BeginImportViewerBusy", BusyMessage);
                ImportDiagnosticsLog.Write("Import", "ImportDesign", Path);
                BeginImportViewerBusy(BusyMessage);
                await YieldUiAsync().ConfigureAwait(true);
                ImportDiagnosticsLog.Mark("AfterYieldUi", "overlay shown");

                IProgress<int> ImportProgress = CreateImportProgressReporter();
                AgdImportResult Result;
                using (ImportDiagnosticsLog.Time("Import", "AgdService.Import", Path))
                {
                    Result = await Task.Run(() =>
                        AgdService.Import(Path, ProgressMapper.Subrange(ImportProgress, 0, 18))).ConfigureAwait(true);
                    ImportDiagnosticsLog.Mark("AgdImportDone", $"interior={Result.Survey?.InteriorPoints.Count ?? 0} boundary={Result.Survey?.BoundaryPoints.Count ?? 0}");
                }

                SuppressViewerFrameInvalidation = true;
                try
                {
                    _Project.Survey = Result.Survey;
                    _Project.DesignParameters = Result.Parameters;
                    _Project.DesignGrid = Result.Grid;
                    _Project.SurveySourceFormat = SurveySourceFormat.Agd;
                    _Project.TopoGeometryDirty.MarkAllDirty();
                    _Project.ViewerState.FieldContentView = FieldContentView.Proposed;
                    _Project.MarkModified();
                    (int ViewportWidth, int ViewportHeight) = GetViewerViewportSize?.Invoke()
                        ?? (DEFAULT_VIEWPORT_WIDTH, DEFAULT_VIEWPORT_HEIGHT);
                    ImportDiagnosticsLog.WriteViewport("FitViewerCamera", ViewportWidth, ViewportHeight);
                    FitViewerCameraToField(ViewportWidth, ViewportHeight, ScheduleInitial3DView: true);
                    ClearToolData();
                    RefreshAll();
                    WriteViewerImportDiagnostics("after-refresh-all");
                }
                finally
                {
                    SuppressViewerFrameInvalidation = false;
                }

                await YieldUiAsync().ConfigureAwait(true);
                ImportDiagnosticsLog.Mark("BeforePrepare", "starting progressive prepare");

                if (PrepareViewerProgressiveAsync == null)
                {
                    ImportDiagnosticsLog.Write("Import", "PrepareFallback", "PrepareViewerProgressiveAsync null, using RunViewerPrepareAsync");
                    await RunViewerPrepareAsync("Loading design…", ProgressMin: 18).ConfigureAwait(true);
                }
                else
                {
                    bool EarlyPreviewPresented = false;
                    using (ImportDiagnosticsLog.Time("Import", "PrepareViewerProgressiveAsync"))
                    {
                        await PrepareViewerProgressiveAsync(
                            ProgressMapper.Subrange(ImportProgress, 18, 100),
                            () =>
                            {
                                if (EarlyPreviewPresented)
                                    return;

                                EarlyPreviewPresented = true;
                                ImportDiagnosticsLog.Write("Import", "EarlyPreviewReady", "presenting preview behind overlay");
                                WriteViewerImportDiagnostics("early-preview");
                                ImportViewerHandoff.NotifyEarlyPreviewReady(
                                    ImportViewerHandoffTelemetry.Recorder,
                                    () => PresentViewerCachedFrame?.Invoke(),
                                    DismissOverlay: null);
                            },
                            _Project.ViewerState.ViewMode).ConfigureAwait(true);
                    }
                }

                ImportViewerHandoffTelemetry.Recorder.Record(ImportViewerHandoffEventKind.PrepareReturned);
                WriteViewerImportDiagnostics("prepare-returned");
                ImportDiagnosticsLog.Mark("PrepareReturned", "progressive prepare finished");
            }
            catch (Exception Ex)
            {
                ImportDiagnosticsLog.WriteException("ImportDesignAsync", Ex);
                ExitImportInputBlock?.Invoke();
                EndViewerBusy();
                await DialogService.ShowMessageAsync("Import failed", Ex.Message).ConfigureAwait(true);
            }
            finally
            {
                if (ImportViewerHandoffTelemetry.Recorder.Contains(ImportViewerHandoffEventKind.PrepareReturned))
                {
                    ImportDiagnosticsLog.Write("Import", "Finally START", "ensure handoff + complete");
                    WriteViewerImportDiagnostics("finally-before-ensure");
                    if (EnsureViewerImportHandoffReadyAsync != null)
                    {
                        using (ImportDiagnosticsLog.Time("Import", "EnsureViewerImportHandoffReadyAsync"))
                        {
                            await EnsureViewerImportHandoffReadyAsync().ConfigureAwait(true);
                        }
                    }

                    WriteViewerImportDiagnostics("finally-after-ensure");
                    CompleteViewerImport();
                    ImportDiagnosticsLog.Write("Import", "Finally END", ImportDiagnosticsLog.LogFilePath);
                }
            }
        }

        /// <summary>
        /// Synchronizes all observable flags and derived UI from the current project document.
        /// </summary>
        private void RefreshAll
            (
            )
        {
            // Push derived flags and toggles from project state into bindable properties.
            HasSurvey = _Project.HasSurvey;
            HasDesign = _Project.HasDesign;
            CanDesign = HasSurvey;
            ShowSurveyStatisticsCommand.NotifyCanExecuteChanged();
            OpenDesignDialogCommand.NotifyCanExecuteChanged();
            CanProposedView = (HasSurvey) && (HasDesign);
            CanCutFillView = (HasSurvey) && (HasDesign);
            RefreshHaulState();
            BenchmarksToggleEnabled = (_Project.Survey != null) && (_Project.Survey.Benchmarks.Count > 0);

            _SuppressModeChange = true;
            IsTwoD = (_Project.ViewerState.ViewMode == ViewMode.TwoD);
            IsThreeD = (_Project.ViewerState.ViewMode == ViewMode.ThreeD);
            ShowVerticalExaggeration = IsThreeD;
            _SuppressModeChange = false;

            _SuppressDisplaySync = true;
            SurveyPointsToggle = _Project.ViewerState.Display.SurveyPoints;
            ContoursToggle = _Project.ViewerState.Display.Contours;
            HillshadeToggle = _Project.ViewerState.Display.Hillshade;
            BenchmarksToggle = _Project.ViewerState.Display.Benchmarks;
            DesignOverlayToggle = _Project.ViewerState.Display.DesignOverlay;
            SatelliteToggle = HasSurvey && _Project.ViewerState.Display.Satellite;
            if (!HasSurvey)
                _Project.ViewerState.Display.Satellite = false;
            VerticalExaggeration = _Project.ViewerState.Display.VerticalExaggeration;
            TopoCutFillOpacity = _Project.ViewerState.Display.TopoCutFillOpacityPercent;
            _SuppressDisplaySync = false;

            StatusLatitude = CoordinateFormat.FormatLatitude(_Project.ViewerState.Camera2D.CentreLat);
            StatusLongitude = CoordinateFormat.FormatLongitude(_Project.ViewerState.Camera2D.CentreLon);

            RefreshTitle();
            RefreshViewButtons();
            RefreshMapToolButtons();
            UpdateLegend();
            NotifyViewerProperties();
            NotifyViewSwitchAvailability();
            OnPropertyChanged(nameof(CanUseMapTools));
            OnPropertyChanged(nameof(CanAddMapToolData));
            AddPointToolCommand.NotifyCanExecuteChanged();
            AddPolylineToolCommand.NotifyCanExecuteChanged();
            PolygonToolCommand.NotifyCanExecuteChanged();
            EraseToolCommand.NotifyCanExecuteChanged();
        }

        /// <summary>
        /// Deactivates map tools when design is removed and refreshes tool command availability.
        /// </summary>
        /// <param name="Value">New design-loaded flag.</param>
        partial void OnHasDesignChanged
            (
            bool Value
            )
        {
            if (!Value)
                DeactivateMapTool();

            OnPropertyChanged(nameof(CanUseMapTools));
            OnPropertyChanged(nameof(CanAddMapToolData));
            AddPointToolCommand.NotifyCanExecuteChanged();
            AddPolylineToolCommand.NotifyCanExecuteChanged();
            PolygonToolCommand.NotifyCanExecuteChanged();
            EraseToolCommand.NotifyCanExecuteChanged();
        }

        /// <summary>
        /// Raises change notification for viewer-facing properties backed by project state.
        /// </summary>
        private void NotifyViewerProperties
            (
            )
        {
            // Re-raise viewer-facing passthrough properties after project mutations.
            OnPropertyChanged(nameof(Project));
            OnPropertyChanged(nameof(ViewMode));
            OnPropertyChanged(nameof(FieldContentView));
            OnPropertyChanged(nameof(Display));
            OnPropertyChanged(nameof(CentreLat));
            OnPropertyChanged(nameof(CentreLon));
            OnPropertyChanged(nameof(Zoom));
            OnPropertyChanged(nameof(OrbitHeading));
            OnPropertyChanged(nameof(OrbitPitch));
            OnPropertyChanged(nameof(OrbitDistance));
            OnPropertyChanged(nameof(OrbitCentreLat));
            OnPropertyChanged(nameof(OrbitCentreLon));
        }

        /// <summary>
        /// Updates the window title from the project title bar name.
        /// </summary>
        private void RefreshTitle
            (
            )
        {
            ProjectTitle = _Project.TitleBarName;
        }

        /// <summary>
        /// Synchronizes field-content view toggle button active states from project state.
        /// </summary>
        private void RefreshViewButtons
            (
            )
        {
            FieldContentView View = _Project.ViewerState.FieldContentView;
            IsExistingActive = (View == FieldContentView.Existing);
            IsProposedActive = (View == FieldContentView.Proposed);
            IsCutFillActive = (View == FieldContentView.CutFill);
            IsHaulActive = (View == FieldContentView.Haul);
            SyncHaulRouteSectionVisibility();
            OnPropertyChanged(nameof(CanSwitchViewerView));
            NotifyHaulThreeDViewAvailability();
            OnPropertyChanged(nameof(ContoursToggleEnabled));
            OnPropertyChanged(nameof(HillshadeToggleEnabled));

            bool ProposedActive = View == FieldContentView.Proposed;
            SurveyPointsToggleEnabled = HasSurvey && !ProposedActive;
            if (ProposedActive && SurveyPointsToggle)
            {
                _SuppressDisplaySync = true;
                SurveyPointsToggle = false;
                _Project.ViewerState.Display.SurveyPoints = false;
                _SuppressDisplaySync = false;
            }
        }

        /// <summary>
        /// Copies overlay and exaggeration toggles from view-model properties into project display settings.
        /// </summary>
        private void SyncDisplayToProject
            (
            )
        {
            DisplaySettings D = _Project.ViewerState.Display;
            D.SurveyPoints = SurveyPointsToggle;
            D.Contours = ContoursToggle;
            D.Hillshade = HillshadeToggle;
            D.Benchmarks = BenchmarksToggle;
            D.DesignOverlay = DesignOverlayToggle;
            D.Satellite = SatelliteToggle;
            D.VerticalExaggeration = VerticalExaggeration;
            D.TopoCutFillOpacityPercent = TopoCutFillOpacity;
        }

        /// <summary>
        /// Resets field content view after creating a new empty project.
        /// </summary>
        private void ApplyDefaultFieldContentView
            (
            )
        {
            _Project.ViewerState.FieldContentView = FieldContentView.None;
        }

        /// <summary>
        /// Chooses an initial field content view after opening a project from disk.
        /// </summary>
        private void ApplyOpenFieldContentView
            (
            )
        {
            if (_Project.HasHaulData)
            {
                ApplyOpenHaulFieldContentView();
                return;
            }

            if (_Project.HasDesign)
                _Project.ViewerState.FieldContentView = FieldContentView.Proposed;
            else if (_Project.HasSurvey)
                _Project.ViewerState.FieldContentView = FieldContentView.Existing;
            else
                _Project.ViewerState.FieldContentView = FieldContentView.None;
        }

        /// <summary>
        /// Rebuilds legend bands from the scalar range for the active field content view.
        /// </summary>
        private void UpdateLegend
            (
            )
        {
            LegendBands.Clear();
            ShowLegend = HasSurvey;

            if (!HasSurvey)
                return;

            FieldContentView View = _Project.ViewerState.FieldContentView;
            // Default legend view when none is selected: proposed if designed, else existing survey.
            if (View == FieldContentView.None)
                View = HasDesign ? FieldContentView.Proposed : FieldContentView.Existing;

            FieldSurfaceSampler.ComputeScalarRangeFromProject(_Project, View, out double VMin, out double VMax);
            DisplayUnitSystem DisplayUnits = UserSettingsService.Instance.ActiveDisplayUnitsSettings.System;

            if (VMax - VMin <= 0)
            {
                // intent: zero cut/fill (or other flat scalar fields) map to one neutral legend band
                uint Argb = NineBandColorScale.GetColor(VMin, VMin, VMax);
                byte A = (byte)((Argb >> ARGB_ALPHA_SHIFT) & ARGB_BYTE_MASK);
                byte R = (byte)((Argb >> ARGB_RED_SHIFT) & ARGB_BYTE_MASK);
                byte G = (byte)((Argb >> ARGB_GREEN_SHIFT) & ARGB_BYTE_MASK);
                byte B = (byte)(Argb & ARGB_BYTE_MASK);

                LegendBands.Add(new LegendBandViewModel
                {
                    BoundaryText = DisplayUnitFormatter.FormatElevation(VMin, DisplayUnits),
                    Swatch = new SolidColorBrush(Color.FromArgb(A, R, G, B))
                });
                return;
            }

            double[] Boundaries = NineBandColorScale.GetBandBoundaries(VMin, VMax);

            for (Int32 BandIndex = 0; BandIndex < NineBandColorScale.BandNames.Length; BandIndex++)
            {
                uint Argb = NineBandColorScale.BandColorsArgb[BandIndex];
                byte A = (byte)((Argb >> ARGB_ALPHA_SHIFT) & ARGB_BYTE_MASK);
                byte R = (byte)((Argb >> ARGB_RED_SHIFT) & ARGB_BYTE_MASK);
                byte G = (byte)((Argb >> ARGB_GREEN_SHIFT) & ARGB_BYTE_MASK);
                byte B = (byte)(Argb & ARGB_BYTE_MASK);

                LegendBands.Add(new LegendBandViewModel
                {
                    BoundaryText = DisplayUnitFormatter.FormatElevation(Boundaries[BandIndex], DisplayUnits),
                    Swatch = new SolidColorBrush(Color.FromArgb(A, R, G, B))
                });
            }
        }
    }
}
