using System.Collections.ObjectModel;
using System.Globalization;

using Avalonia.Media;
using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using AgGradeDesigner.Core.Color;
using AgGradeDesigner.Core.Design;
using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Persistence;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Core.Survey;
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
        private const double DEFAULT_STATUS_LATITUDE = 36.0;
        private const double DEFAULT_STATUS_LONGITUDE = -90.0;
        private const double DEFAULT_VERTICAL_EXAGGERATION = 1.0;
        private const int DEFAULT_VIEWPORT_WIDTH = 800;
        private const int DEFAULT_VIEWPORT_HEIGHT = 600;
        private const double CAMERA_DISTANCE_EXTENT_FACTOR = 0.85;
        private const double CAMERA_DISTANCE_MIN_M = 80.0;
        private const double CAMERA_DISTANCE_MAX_M = 5000.0;
        private const int ARGB_ALPHA_SHIFT = 24;
        private const int ARGB_RED_SHIFT = 16;
        private const int ARGB_GREEN_SHIFT = 8;
        private const int ARGB_BYTE_MASK = 0xFF;

        private ProjectDocument _Project = new ProjectDocument();

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
        /// Whether the central viewer is performing background preparation work.
        /// </summary>
        [ObservableProperty]
        private bool _IsViewerBusy;

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

        private bool _SuppressModeChange;

        private bool _SuppressDisplaySync;

        private bool _ApplyInitial3DCameraOnNextEntry;

        private static readonly IProgress<int> EmptyViewerProgress = new Progress<int>(_ => { });

        /// <summary>
        /// Optional callback that rebuilds and warms the field viewer on a worker thread.
        /// </summary>
        public Func<IProgress<int>, ViewMode, Task<bool>>? PrepareViewerAsync { get; set; }

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
        /// Optional callback that discards cached 3D frame bitmaps after the orbit camera changes.
        /// </summary>
        public Action? InvalidateViewer3DFrameCache { get; set; }

        /// <summary>
        /// Optional callback that discards cached 2D frame bitmaps before entering 2D view.
        /// </summary>
        public Action? InvalidateViewer2DFrameCache { get; set; }

        /// <summary>
        /// Optional callback invoked before viewer preparation so the UI thread does not sync-render.
        /// </summary>
        public Action? EnterViewerTransition { get; set; }

        /// <summary>
        /// Optional callback invoked after viewer preparation completes.
        /// </summary>
        public Action? ExitViewerTransition { get; set; }

        /// <summary>
        /// Optional callback that publishes the latest cached viewer frame to the screen.
        /// </summary>
        public Action? PresentViewerCachedFrame { get; set; }

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
            RefreshAll();
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
            ViewerBusyProgress = 0;
            ExitViewerTransition?.Invoke();
            PresentViewerCachedFrame?.Invoke();
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
            if (!Value || _SuppressModeChange)
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
            if (!Value || _SuppressModeChange)
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
                OnPropertyChanged(nameof(ViewMode));
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
        private void BeginViewerBusy
            (
            string Message,
            int StartPercent = 0
            )
        {
            IsViewerBusy = true;
            EnterViewerTransition?.Invoke();
            SetViewerBusyProgress(StartPercent, Message);
        }

        /// <summary>
        /// Updates viewer overlay progress and message with a percentage suffix.
        /// </summary>
        private void SetViewerBusyProgress
            (
            int Percent,
            string Message
            )
        {
            void Apply()
            {
                ViewerBusyProgress = Math.Clamp(Percent, 0, 100);
                ViewerBusyMessage = $"{Message} ({ViewerBusyProgress:0}%)";
            }

            if (Dispatcher.UIThread.CheckAccess())
                Apply();
            else
                Dispatcher.UIThread.Post(Apply);
        }

        /// <summary>
        /// Creates a progress reporter that maps 0–100 sub-progress into a percent range on the viewer overlay.
        /// </summary>
        private IProgress<int> CreateViewerProgressReporter
            (
            string Message,
            int MinPercent = 0,
            int MaxPercent = 100
            )
        {
            return new Progress<int>(Value =>
            {
                int Scaled = MinPercent + (int)Math.Round(Value * (MaxPercent - MinPercent) / 100.0);
                SetViewerBusyProgress(Scaled, Message);
            });
        }

        /// <summary>
        /// Creates a progress reporter for AGD import with phase-specific overlay messages.
        /// </summary>
        private IProgress<int> CreateImportProgressReporter
            (
            )
        {
            return new Progress<int>(Percent => SetViewerBusyProgress(Percent, GetImportPhaseMessage(Percent)));
        }

        /// <summary>
        /// Returns the viewer overlay message for an AGD import progress percent.
        /// </summary>
        private static string GetImportPhaseMessage
            (
            int Percent
            )
        {
            if (Percent < 18)
                return "Reading design file";
            if (Percent < 45)
                return "Building surface";
            if (Percent < 72)
                return "Preparing preview";
            return "Finishing display";
        }

        /// <summary>
        /// Maps field-creator status strings to design progress percentages.
        /// </summary>
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
                            return 15 + (int)Math.Round(40.0 * Current / Total);
                        }
                    }
                }

                return 30;
            }

            return Message switch
            {
                "Building bin grid and existing surface…" => 5,
                "Smoothing baseline and warping toward survey bins…" => 58,
                "Existing surface ready." => 62,
                "Building per-bin samples and solving design plane…" => 72,
                "Solving cut/fill ratio (plane offset)…" => 82,
                "Computing per-bin targets and volumes…" => 90,
                "Done." => 100,
                _ => 50
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

            _Project.ViewerState.Display.VerticalExaggeration = Value;
            OnPropertyChanged(nameof(Display));
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
            string? Path = await DialogService.PickOpenFileAsync("Open Project", "*.agdesign");
            if (Path == null)
                return;

            try
            {
                ProjectDocument Loaded = await Task.Run(() => AgDesignSerializer.Load(Path)).ConfigureAwait(true);
                await RunViewerPrepareAsync(
                    "Loading project…",
                    () =>
                    {
                        _Project = Loaded;
                        ApplyOpenFieldContentView();
                        FitViewerCameraToField();
                        RefreshAll();
                    }).ConfigureAwait(true);
            }
            catch (Exception Ex)
            {
                await DialogService.ShowMessageAsync("Open failed", Ex.Message);
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
            string? Path = await DialogService.PickSaveFileAsync("Save Project As", "Project.agdesign", "*.agdesign");
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
                DesignBusyProgress = 0;
                Progress<int> DesignProgress = new Progress<int>(Percent => DesignBusyProgress = Percent);
                (DesignGrid Grid, FieldCreator.Statistics Stats) = await Task.Run(
                    () =>
                    {
                        FieldCreator Creator = new FieldCreator(Message =>
                            ((IProgress<int>)DesignProgress).Report(MapDesignProgress(Message)));
                        return Creator.CreateDesign(_Project.Survey!, Params);
                    }).ConfigureAwait(true);
                DesignBusyProgress = 100;
                await RunViewerPrepareAsync(
                    "Updating field view",
                    () =>
                    {
                        _Project.DesignParameters = Params;
                        _Project.DesignGrid = Grid;
                        _Project.ViewerState.FieldContentView = FieldContentView.Proposed;
                        _Project.MarkModified();
                        FitViewerCameraToField(ScheduleInitial3DView: true);
                        RefreshAll();
                    }).ConfigureAwait(true);
            }
            catch (Exception Ex)
            {
                await DialogService.ShowMessageAsync("Design failed", Ex.Message);
            }
            finally
            {
                IsDesignBusy = false;
                DesignBusyProgress = 0;
            }
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
        /// Switches the viewer to the existing-surface field content view.
        /// </summary>
        [RelayCommand]
        private async Task SelectExistingAsync
            (
            )
        {
            if (!HasSurvey)
                return;

            await TransitionFieldContentViewAsync(FieldContentView.Existing, "Switching to existing view").ConfigureAwait(true);
        }

        /// <summary>
        /// Switches the viewer to the proposed design field content view.
        /// </summary>
        [RelayCommand]
        private async Task SelectProposedAsync
            (
            )
        {
            if (!CanProposedView)
                return;

            await TransitionFieldContentViewAsync(FieldContentView.Proposed, "Switching to proposed view").ConfigureAwait(true);
        }

        /// <summary>
        /// Switches the viewer to the cut/fill comparison field content view.
        /// </summary>
        [RelayCommand]
        private async Task SelectCutFillAsync
            (
            )
        {
            if (!CanCutFillView)
                return;

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
            if (PrepareViewerAsync == null)
                return;

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

                SetViewerBusyProgress(20, "Reading survey file");

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
                        _Project.ViewerState.FieldContentView = FieldContentView.Existing;
                        _Project.ViewerState.Display.SurveyPoints = false;
                        _Project.MarkModified();
                        FitViewerCameraToField(ScheduleInitial3DView: true);
                        RefreshAll();
                    },
                    ProgressMin: 20).ConfigureAwait(true);
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
                BeginViewerBusy("Reading design file");
                await YieldUiAsync().ConfigureAwait(true);

                IProgress<int> ImportProgress = CreateImportProgressReporter();
                AgdImportResult Result = await Task.Run(() =>
                    AgdService.Import(Path, ProgressMapper.Subrange(ImportProgress, 0, 18))).ConfigureAwait(true);

                SuppressViewerFrameInvalidation = true;
                try
                {
                    _Project.Survey = Result.Survey;
                    _Project.DesignParameters = Result.Parameters;
                    _Project.DesignGrid = Result.Grid;
                    _Project.SurveySourceFormat = SurveySourceFormat.Agd;
                    _Project.ViewerState.FieldContentView = FieldContentView.Proposed;
                    _Project.MarkModified();
                    FitViewerCameraToField(ScheduleInitial3DView: true);
                    RefreshAll();
                }
                finally
                {
                    SuppressViewerFrameInvalidation = false;
                }

                await YieldUiAsync().ConfigureAwait(true);

                if (PrepareViewerProgressiveAsync == null)
                {
                    await RunViewerPrepareAsync("Loading design…", ProgressMin: 18).ConfigureAwait(true);
                    return;
                }

                bool OverlayDismissed = false;
                await PrepareViewerProgressiveAsync(
                    ProgressMapper.Subrange(ImportProgress, 18, 100),
                    () =>
                    {
                        if (OverlayDismissed)
                            return;

                        OverlayDismissed = true;
                        IsViewerBusy = false;
                        ViewerBusyProgress = 0;
                        PresentViewerCachedFrame?.Invoke();
                    },
                    _Project.ViewerState.ViewMode).ConfigureAwait(true);
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
            _SuppressDisplaySync = false;

            StatusLatitude = CoordinateFormat.FormatLatitude(_Project.ViewerState.Camera2D.CentreLat);
            StatusLongitude = CoordinateFormat.FormatLongitude(_Project.ViewerState.Camera2D.CentreLon);

            RefreshTitle();
            RefreshViewButtons();
            UpdateLegend();
            NotifyViewerProperties();
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
                    BoundaryText = NineBandColorScale.FormatFeet(Boundaries[BandIndex]) + " ft",
                    Swatch = new SolidColorBrush(Color.FromArgb(A, R, G, B))
                });
            }
        }
    }
}
