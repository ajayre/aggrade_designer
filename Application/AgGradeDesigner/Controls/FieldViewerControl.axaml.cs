using System;
using System.Runtime.InteropServices;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

using SkiaSharp;

using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Rendering;

namespace AgGradeDesigner.Controls
{
    /// <summary>
    /// Central field viewer with 2D pan/zoom and 3D orbit rendering via SkiaSharp.
    /// GPU-accelerated rendering will use <see cref="GraphicsRuntime"/> when available.
    /// </summary>
    public partial class FieldViewerControl : Control
    {
        /// <summary>True when startup Vulkan probing found a usable graphics device.</summary>
        static public bool IsVulkanGraphicsAvailable => GraphicsRuntime.IsVulkanAvailable;
        private const double DEFAULT_CENTRE_LATITUDE = 36.0;
        private const double DEFAULT_CENTRE_LONGITUDE = -90.0;
        private const double DEFAULT_ZOOM_FACTOR = 1.0;
        private const double DEFAULT_ORBIT_HEADING_DEG = 45.0;
        private const double DEFAULT_ORBIT_PITCH_DEG = 30.0;
        private const double DEFAULT_ORBIT_DISTANCE_M = 500.0;
        private const double ORBIT_HEADING_SENSITIVITY = 0.4;
        private const double ORBIT_PITCH_SENSITIVITY = 0.3;
        private const double MAX_ORBIT_PITCH_DEG = 89.0;
        private const double WHEEL_ZOOM_IN_FACTOR = 1.12;
        private const double WHEEL_ZOOM_OUT_FACTOR = 0.89;
        private const double ORBIT_WHEEL_ZOOM_IN_FACTOR = 0.9;
        private const double ORBIT_WHEEL_ZOOM_OUT_FACTOR = 1.1;
        private const double MIN_ORBIT_DISTANCE_M = 50.0;
        private const double MAX_ORBIT_DISTANCE_M = 5000.0;
        private const double METRES_PER_DEGREE_LATITUDE = 111320.0;
        private const double DEGREES_IN_FULL_CIRCLE = 360.0;
        private const double NORTH_UP_HEADING_DEG = 180.0;
        private const int VIEW_CUBE_ANIMATION_MS = 420;

        private readonly FieldSurfaceSampler _Surface = new FieldSurfaceSampler();
        private readonly SatelliteTileService _Satellite = new SatelliteTileService();
        private DisplaySettings _Display = new DisplaySettings();
        private bool _IsDragging;
        private bool _IsRightDragging;
        private Point _LastPointer;
        private bool _SurfaceDirty = true;
        private readonly WriteableBitmap?[] _FrameCaches2D = new WriteableBitmap?[4];
        private readonly string[] _FrameCacheKeys2D = new string[4];
        private readonly int[] _FrameSatelliteGenerations2D = new int[4];
        private readonly WriteableBitmap?[] _FrameCaches3D = new WriteableBitmap?[4];
        private readonly string[] _FrameCacheKeys3D = new string[4];
        private readonly int[] _FrameSatelliteGenerations3D = new int[4];
        private int _BackgroundPrewarmVersion;
        private WriteableBitmap? _PresentedFrame;
        private readonly object _PrepareLock = new object();
        private ViewCubeHit _ViewCubeHover = ViewCubeHit.None;
        private Avalonia.Threading.DispatcherTimer? _ViewCubeAnimTimer;
        private double _ViewCubeAnimStartHeadingDeg;
        private double _ViewCubeAnimStartPitchDeg;
        private double _ViewCubeAnimTargetHeadingDeg;
        private double _ViewCubeAnimTargetPitchDeg;
        private long _ViewCubeAnimStartTicks;

        /// <summary>
        /// Styled property backing <see cref="Project"/>.
        /// </summary>
        static public readonly StyledProperty<ProjectDocument?> ProjectProperty =
            AvaloniaProperty.Register<FieldViewerControl, ProjectDocument?>(nameof(Project));

        /// <summary>
        /// Styled property backing <see cref="ViewMode"/>.
        /// </summary>
        static public readonly StyledProperty<ViewMode> ViewModeProperty =
            AvaloniaProperty.Register<FieldViewerControl, ViewMode>(nameof(ViewMode));

        /// <summary>
        /// Styled property backing <see cref="ContentView"/>.
        /// </summary>
        static public readonly StyledProperty<FieldContentView> ContentViewProperty =
            AvaloniaProperty.Register<FieldViewerControl, FieldContentView>(nameof(ContentView));

        /// <summary>
        /// Styled property backing <see cref="DisplaySettings"/>.
        /// </summary>
        static public readonly StyledProperty<DisplaySettings?> DisplaySettingsProperty =
            AvaloniaProperty.Register<FieldViewerControl, DisplaySettings?>(nameof(DisplaySettings));

        /// <summary>
        /// Styled property backing <see cref="CentreLatitude"/>.
        /// </summary>
        static public readonly StyledProperty<double> CentreLatitudeProperty =
            AvaloniaProperty.Register<FieldViewerControl, double>(nameof(CentreLatitude), DEFAULT_CENTRE_LATITUDE);

        /// <summary>
        /// Styled property backing <see cref="CentreLongitude"/>.
        /// </summary>
        static public readonly StyledProperty<double> CentreLongitudeProperty =
            AvaloniaProperty.Register<FieldViewerControl, double>(nameof(CentreLongitude), DEFAULT_CENTRE_LONGITUDE);

        /// <summary>
        /// Styled property backing <see cref="ZoomFactor"/>.
        /// </summary>
        static public readonly StyledProperty<double> ZoomFactorProperty =
            AvaloniaProperty.Register<FieldViewerControl, double>(nameof(ZoomFactor), DEFAULT_ZOOM_FACTOR);

        /// <summary>
        /// Styled property backing <see cref="OrbitHeadingDeg"/>.
        /// </summary>
        static public readonly StyledProperty<double> OrbitHeadingDegProperty =
            AvaloniaProperty.Register<FieldViewerControl, double>(nameof(OrbitHeadingDeg), DEFAULT_ORBIT_HEADING_DEG);

        /// <summary>
        /// Styled property backing <see cref="OrbitPitchDeg"/>.
        /// </summary>
        static public readonly StyledProperty<double> OrbitPitchDegProperty =
            AvaloniaProperty.Register<FieldViewerControl, double>(nameof(OrbitPitchDeg), DEFAULT_ORBIT_PITCH_DEG);

        /// <summary>
        /// Styled property backing <see cref="OrbitDistanceM"/>.
        /// </summary>
        static public readonly StyledProperty<double> OrbitDistanceMProperty =
            AvaloniaProperty.Register<FieldViewerControl, double>(nameof(OrbitDistanceM), DEFAULT_ORBIT_DISTANCE_M);

        /// <summary>
        /// Styled property backing <see cref="OrbitCentreLatitude"/>.
        /// </summary>
        static public readonly StyledProperty<double> OrbitCentreLatitudeProperty =
            AvaloniaProperty.Register<FieldViewerControl, double>(nameof(OrbitCentreLatitude), double.NaN);

        /// <summary>
        /// Styled property backing <see cref="OrbitCentreLongitude"/>.
        /// </summary>
        static public readonly StyledProperty<double> OrbitCentreLongitudeProperty =
            AvaloniaProperty.Register<FieldViewerControl, double>(nameof(OrbitCentreLongitude), double.NaN);

        /// <summary>
        /// Styled property backing <see cref="SuppressSynchronousPrepare"/>.
        /// </summary>
        static public readonly StyledProperty<bool> SuppressSynchronousPrepareProperty =
            AvaloniaProperty.Register<FieldViewerControl, bool>(nameof(SuppressSynchronousPrepare));

        /// <summary>
        /// Raised after pan, zoom, or orbit gestures change the view camera.
        /// </summary>
        public event EventHandler? ViewCentreChanged;

        /// <summary>
        /// Registers styled properties that invalidate rendering when they change.
        /// </summary>
        static FieldViewerControl
            (
            )
        {
            AffectsRender<FieldViewerControl>(
                ProjectProperty,
                ViewModeProperty,
                ContentViewProperty,
                DisplaySettingsProperty,
                CentreLatitudeProperty,
                CentreLongitudeProperty,
                ZoomFactorProperty,
                OrbitHeadingDegProperty,
                OrbitPitchDegProperty,
                OrbitDistanceMProperty,
                OrbitCentreLatitudeProperty,
                OrbitCentreLongitudeProperty,
                SuppressSynchronousPrepareProperty);
        }

        /// <summary>
        /// Initializes the control and wires pointer and satellite tile callbacks.
        /// </summary>
        public FieldViewerControl
            (
            )
        {
            InitializeComponent();
            GraphicsRuntime.EnsureInitialized();
            // Refresh the Skia frame when background satellite tiles finish loading.
            _Satellite.TilesUpdated += OnSatelliteTilesUpdated;
            PointerPressed += OnPointerPressed;
            PointerMoved += OnPointerMoved;
            PointerReleased += OnPointerReleased;
            PointerWheelChanged += OnPointerWheelChanged;
            PointerExited += OnPointerExited;
        }

        /// <summary>
        /// Clears view-cube hover when the pointer leaves the control.
        /// </summary>
        private void OnPointerExited
            (
            object? Sender,
            PointerEventArgs Args
            )
        {
            if (_ViewCubeHover.IsHit)
            {
                _ViewCubeHover = ViewCubeHit.None;
                InvalidateVisual();
            }
        }

        /// <summary>
        /// Marshals satellite tile updates onto the UI thread for a safe redraw.
        /// </summary>
        private void OnSatelliteTilesUpdated
            (
            )
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                // New tiles may have arrived after a frame was cached without imagery — redraw.
                InvalidateVisual();
            });
        }

        /// <summary>
        /// Gets or sets the project document rendered in the viewer.
        /// </summary>
        public ProjectDocument? Project
        {
            get => GetValue(ProjectProperty);
            set => SetValue(ProjectProperty, value);
        }

        /// <summary>
        /// Gets or sets the active view mode (2D map or 3D orbit).
        /// </summary>
        public ViewMode ViewMode
        {
            get => GetValue(ViewModeProperty);
            set => SetValue(ViewModeProperty, value);
        }

        /// <summary>
        /// Gets or sets which field content layer is displayed.
        /// </summary>
        public FieldContentView ContentView
        {
            get => GetValue(ContentViewProperty);
            set => SetValue(ContentViewProperty, value);
        }

        /// <summary>
        /// Gets or sets display options such as satellite and contour overlays.
        /// </summary>
        public DisplaySettings? DisplaySettings
        {
            get => GetValue(DisplaySettingsProperty);
            set => SetValue(DisplaySettingsProperty, value);
        }

        /// <summary>
        /// Gets or sets the map centre latitude in decimal degrees.
        /// </summary>
        public double CentreLatitude
        {
            get => GetValue(CentreLatitudeProperty);
            set => SetValue(CentreLatitudeProperty, value);
        }

        /// <summary>
        /// Gets or sets the map centre longitude in decimal degrees.
        /// </summary>
        public double CentreLongitude
        {
            get => GetValue(CentreLongitudeProperty);
            set => SetValue(CentreLongitudeProperty, value);
        }

        /// <summary>
        /// Gets or sets the 2D zoom factor applied to the map view.
        /// </summary>
        public double ZoomFactor
        {
            get => GetValue(ZoomFactorProperty);
            set => SetValue(ZoomFactorProperty, value);
        }

        /// <summary>
        /// Gets or sets the 3D orbit heading in degrees.
        /// </summary>
        public double OrbitHeadingDeg
        {
            get => GetValue(OrbitHeadingDegProperty);
            set => SetValue(OrbitHeadingDegProperty, value);
        }

        /// <summary>
        /// Gets or sets the 3D orbit pitch in degrees.
        /// </summary>
        public double OrbitPitchDeg
        {
            get => GetValue(OrbitPitchDegProperty);
            set => SetValue(OrbitPitchDegProperty, value);
        }

        /// <summary>
        /// Gets or sets the 3D camera distance from the field centre in metres.
        /// </summary>
        public double OrbitDistanceM
        {
            get => GetValue(OrbitDistanceMProperty);
            set => SetValue(OrbitDistanceMProperty, value);
        }

        /// <summary>
        /// Gets or sets the 3D orbit pivot latitude in decimal degrees (NaN uses field centre).
        /// </summary>
        public double OrbitCentreLatitude
        {
            get => GetValue(OrbitCentreLatitudeProperty);
            set => SetValue(OrbitCentreLatitudeProperty, value);
        }

        /// <summary>
        /// Gets or sets the 3D orbit pivot longitude in decimal degrees (NaN uses field centre).
        /// </summary>
        public double OrbitCentreLongitude
        {
            get => GetValue(OrbitCentreLongitudeProperty);
            set => SetValue(OrbitCentreLongitudeProperty, value);
        }

        /// <summary>
        /// When true, skips synchronous surface build and frame rendering (background prep in progress).
        /// </summary>
        public bool SuppressSynchronousPrepare
        {
            get => GetValue(SuppressSynchronousPrepareProperty);
            set => SetValue(SuppressSynchronousPrepareProperty, value);
        }

        /// <summary>
        /// Marks the surface mesh stale and requests a visual redraw.
        /// </summary>
        public void InvalidateSurface
            (
            )
        {
            // Rebuild elevation sampling on the next render pass.
            _SurfaceDirty = true;
            InvalidateFrameCaches();
            InvalidateVisual();
        }

        /// <summary>
        /// Ensures the elevation mesh is built for camera fitting on the UI thread.
        /// </summary>
        public void EnsureSurfaceBuiltForFit
            (
            )
        {
            if (!_SurfaceDirty && (_Surface.SampleWidth > 0))
                return;

            _Surface.BuildFromProject(Project);
            _SurfaceDirty = false;
        }

        /// <summary>
        /// Sampled field surface used for rendering and camera fitting.
        /// </summary>
        public FieldSurfaceSampler FieldSurface => _Surface;

        /// <summary>
        /// Returns the current viewer viewport size in pixels.
        /// </summary>
        public (int Width, int Height) GetViewportSize
            (
            )
        {
            return (Math.Max(1, (int)Bounds.Width), Math.Max(1, (int)Bounds.Height));
        }

        /// <summary>
        /// Discards cached frame bitmaps and requests a redraw.
        /// </summary>
        public void InvalidateRenderedFrame
            (
            )
        {
            InvalidateFrameCaches();
            InvalidateVisual();
        }

        /// <summary>
        /// Returns true when a cached frame exists for the requested view mode and current camera state.
        /// </summary>
        public bool HasCachedFrameForMode
            (
            ViewMode TargetMode
            )
        {
            RenderSnapshot Snapshot = CaptureRenderSnapshot(TargetMode);
            if ((Snapshot.Width <= 0) || (Snapshot.Height <= 0))
                return false;

            string CacheKey = BuildFrameCacheKey(
                Snapshot.Width,
                Snapshot.Height,
                TargetMode,
                Snapshot);
            return TryGetCachedFrame(
                TargetMode,
                ResolveContentView(Snapshot.Project, Snapshot.ContentView),
                CacheKey,
                Snapshot.Width,
                Snapshot.Height) != null;
        }

        /// <summary>
        /// Discards the cached frame bitmap for one view mode.
        /// </summary>
        private void InvalidateFrameCacheForMode
            (
            ViewMode Mode
            )
        {
            WriteableBitmap?[] Caches = Mode == ViewMode.TwoD ? _FrameCaches2D : _FrameCaches3D;
            string[] Keys = Mode == ViewMode.TwoD ? _FrameCacheKeys2D : _FrameCacheKeys3D;
            for (int I = 0; I < 4; I++)
            {
                Caches[I] = null;
                Keys[I] = "";
                if (Mode == ViewMode.TwoD)
                    _FrameSatelliteGenerations2D[I] = 0;
                else
                    _FrameSatelliteGenerations3D[I] = 0;
            }
        }

        /// <summary>
        /// Returns true when a cached frame is still valid for the current satellite tile generation.
        /// </summary>
        private bool IsSatelliteFrameCacheValid
            (
            ViewMode Mode,
            int ContentViewIndex,
            bool SatelliteEnabled
            )
        {
            if (!SatelliteEnabled)
                return true;

            int StoredGeneration = Mode == ViewMode.TwoD
                ? _FrameSatelliteGenerations2D[ContentViewIndex]
                : _FrameSatelliteGenerations3D[ContentViewIndex];
            return StoredGeneration >= _Satellite.CacheGeneration;
        }

        /// <summary>
        /// Discards cached 2D and 3D frame bitmaps.
        /// </summary>
        private void InvalidateFrameCaches
            (
            )
        {
            _BackgroundPrewarmVersion++;
            _PresentedFrame = null;
            for (int I = 0; I < 4; I++)
            {
                _FrameCaches2D[I] = null;
                _FrameCacheKeys2D[I] = "";
                _FrameSatelliteGenerations2D[I] = 0;
                _FrameCaches3D[I] = null;
                _FrameCacheKeys3D[I] = "";
                _FrameSatelliteGenerations3D[I] = 0;
            }
        }

        /// <summary>
        /// Returns a stable array index for a field content view frame cache slot.
        /// </summary>
        private static int ContentViewCacheIndex
            (
            FieldContentView View
            )
        {
            int Index = (int)View;
            return (Index >= 0 && Index < 4) ? Index : -1;
        }

        /// <summary>
        /// Returns the available size unchanged so the viewer fills its parent.
        /// </summary>
        /// <param name="AvailableSize">Size offered by the layout system.</param>
        /// <returns>The same size passed in.</returns>
        protected override Size MeasureOverride
            (
            Size AvailableSize
            )
        {
            return AvailableSize;
        }

        /// <summary>
        /// Arranges the control to the final size assigned by the layout system.
        /// </summary>
        /// <param name="FinalSize">Final arranged size.</param>
        /// <returns>The same size passed in.</returns>
        protected override Size ArrangeOverride
            (
            Size FinalSize
            )
        {
            return FinalSize;
        }

        /// <summary>
        /// Rebuilds the surface mesh when project or display bindings change.
        /// </summary>
        /// <param name="Change">Description of the property change.</param>
        protected override void OnPropertyChanged
            (
            AvaloniaPropertyChangedEventArgs Change
            )
        {
            base.OnPropertyChanged(Change);

            if (Change.Property == ProjectProperty)
            {
                // Rebuild elevation mesh when project changes — not when toggling 2D/3D or scalar view.
                _SurfaceDirty = true;
                InvalidateFrameCaches();
            }
        }

        /// <summary>
        /// Rebuilds the surface mesh and pre-renders the active view on a worker thread.
        /// </summary>
        /// <returns>True when an existing cached frame was reused without re-rendering.</returns>
        public async Task<bool> PrepareViewAsync
            (
            IProgress<int>? Progress = null,
            ViewMode? TargetMode = null
            )
        {
            Progress?.Report(0);
            RenderSnapshot Snapshot = CaptureRenderSnapshot(TargetMode);
            if (Snapshot.Width <= 0 || Snapshot.Height <= 0)
            {
                Progress?.Report(100);
                return false;
            }

            string CacheKey = BuildFrameCacheKey(
                Snapshot.Width,
                Snapshot.Height,
                Snapshot.ViewMode,
                Snapshot);
            FieldContentView ContentView = ResolveContentView(Snapshot.Project, Snapshot.ContentView);
            if (TryGetCachedFrame(Snapshot.ViewMode, ContentView, CacheKey, Snapshot.Width, Snapshot.Height) != null)
            {
                Progress?.Report(100);
                InvalidateVisual();
                return true;
            }

            Progress?.Report(5);
            await Task.Run(() => PrepareViewBackground(Snapshot, Progress)).ConfigureAwait(true);
            Progress?.Report(100);
            InvalidateVisual();
            ScheduleOppositeModePrewarm(Snapshot);
            ScheduleAlternateContentViewPrewarm(Snapshot);
            return false;
        }

        /// <summary>
        /// Progressive prepare for import: surface, quick preview frame, then full-quality finish.
        /// </summary>
        /// <param name="Progress">Optional 0–100 progress reporter.</param>
        /// <param name="OnEarlyDisplayReady">Called on the UI thread once a preview frame is available.</param>
        /// <param name="TargetMode">Target view mode; defaults to the bound view mode.</param>
        public async Task PrepareViewProgressiveAsync
            (
            IProgress<int>? Progress = null,
            Action? OnEarlyDisplayReady = null,
            ViewMode? TargetMode = null
            )
        {
            Progress?.Report(0);
            RenderSnapshot Snapshot = CaptureRenderSnapshot(TargetMode);
            if ((Snapshot.Width <= 0) || (Snapshot.Height <= 0))
            {
                Progress?.Report(100);
                OnEarlyDisplayReady?.Invoke();
                return;
            }

            FieldContentView ContentView = ResolveContentView(Snapshot.Project, Snapshot.ContentView);
            string CacheKey = BuildFrameCacheKey(
                Snapshot.Width,
                Snapshot.Height,
                Snapshot.ViewMode,
                Snapshot);
            if (TryGetCachedFrame(Snapshot.ViewMode, ContentView, CacheKey, Snapshot.Width, Snapshot.Height) != null)
            {
                Progress?.Report(100);
                InvalidateVisual();
                OnEarlyDisplayReady?.Invoke();
                return;
            }

            Progress?.Report(2);
            await Task.Run(() =>
            {
                lock (_PrepareLock)
                {
                    Progress?.Report(5);
                    _Surface.BuildFromProject(
                        Snapshot.Project,
                        Progress: ProgressMapper.Subrange(Progress, 5, 40));
                    _SurfaceDirty = false;
                }
            }).ConfigureAwait(true);

            await Task.Run(() =>
            {
                lock (_PrepareLock)
                {
                    if (Snapshot.Display != null)
                    {
                        _Surface.PrewarmDisplayCaches(
                            ContentView,
                            Snapshot.Display,
                            ProgressMapper.Subrange(Progress, 40, 55),
                            PlanViewColorMapBuilder.PreviewSupersampleFactor,
                            IncludeContours: false);
                    }

                    BuildAndStoreFrame(Snapshot, ProgressMapper.Subrange(Progress, 55, 72));
                }
            }).ConfigureAwait(true);

            OnEarlyDisplayReady?.Invoke();
            InvalidateVisual();

            await Task.Run(() =>
            {
                lock (_PrepareLock)
                {
                    if ((Snapshot.Project?.HasDesign ?? false) && Snapshot.Display != null)
                    {
                        _Surface.PrewarmDisplayCaches(
                            FieldContentView.Existing,
                            Snapshot.Display,
                            ProgressMapper.Subrange(Progress, 72, 80));
                    }

                    if (Snapshot.Display != null)
                    {
                        _Surface.UpgradeDisplayCacheQuality(
                            ContentView,
                            Snapshot.Display,
                            ProgressMapper.Subrange(Progress, 80, 92));
                    }

                    BuildAndStoreFrame(Snapshot, ProgressMapper.Subrange(Progress, 92, 99));
                }
            }).ConfigureAwait(true);

            Progress?.Report(100);
            InvalidateVisual();
            ScheduleOppositeModePrewarm(Snapshot);
            ScheduleAlternateContentViewPrewarm(Snapshot);
        }

        /// <summary>
        /// Captures render inputs from the UI thread before background preparation.
        /// </summary>
        private RenderSnapshot CaptureRenderSnapshot
            (
            ViewMode? TargetMode
            )
        {
            ProjectDocument? ProjectSource = Project;
            ViewerState? Viewer = ProjectSource?.ViewerState;
            ViewMode Mode = TargetMode ?? Viewer?.ViewMode ?? ViewMode;
            FieldContentView ActiveContentView = Viewer?.FieldContentView ?? ContentView;
            DisplaySettings Display = Viewer?.Display ?? DisplaySettings ?? _Display;

            return new RenderSnapshot
            {
                Project = ProjectSource,
                ViewMode = Mode,
                ContentView = ActiveContentView,
                Display = Display,
                CentreLatitude = Viewer?.Camera2D.CentreLat ?? CentreLatitude,
                CentreLongitude = Viewer?.Camera2D.CentreLon ?? CentreLongitude,
                ZoomFactor = Viewer?.Camera2D.Zoom ?? ZoomFactor,
                // Control properties are the live interaction state during orbit drags.
                OrbitHeadingDeg = OrbitHeadingDeg,
                OrbitPitchDeg = OrbitPitchDeg,
                OrbitDistanceM = OrbitDistanceM,
                OrbitCentreLatitude = ResolveOrbitCentreLatitude(),
                OrbitCentreLongitude = ResolveOrbitCentreLongitude(),
                Width = Math.Max(1, (int)Bounds.Width),
                Height = Math.Max(1, (int)Bounds.Height),
                SurfaceDirty = _SurfaceDirty
            };
        }

        /// <summary>
        /// Rebuilds the elevation mesh and frame cache using a UI-thread snapshot.
        /// </summary>
        /// <param name="Snapshot">Frozen render state captured on the UI thread.</param>
        private void PrepareViewBackground
            (
            RenderSnapshot Snapshot,
            IProgress<int>? Progress = null
            )
        {
            lock (_PrepareLock)
            {
                bool HasTopo = (Snapshot.Project != null) && Snapshot.Project.HasSurvey;
                if (!HasTopo)
                {
                    _Surface.BuildFromProject(Snapshot.Project, Progress: Progress);
                    _SurfaceDirty = false;
                    InvalidateFrameCaches();
                    Progress?.Report(100);
                    return;
                }

                bool NeedsSurfaceBuild = ViewerFrameCacheKey.NeedsSurfaceRebuild(Snapshot.SurfaceDirty, _Surface.SampleWidth);
                if (NeedsSurfaceBuild)
                {
                    Progress?.Report(5);
                    _Surface.BuildFromProject(Snapshot.Project, Progress: ProgressMapper.Subrange(Progress, 5, 42));
                    _SurfaceDirty = false;
                }

                if (Snapshot.Display != null)
                {
                    FieldContentView ContentView = ResolveContentView(Snapshot.Project, Snapshot.ContentView);
                    IProgress<int>? PrewarmProgress = NeedsSurfaceBuild
                        ? ProgressMapper.Subrange(Progress, 42, 58)
                        : ProgressMapper.Subrange(Progress, 5, 25);
                    Progress?.Report(NeedsSurfaceBuild ? 42 : 8);
                    _Surface.PrewarmDisplayCaches(ContentView, Snapshot.Display, PrewarmProgress);
                }

                Progress?.Report(NeedsSurfaceBuild ? 58 : 26);
                IProgress<int>? FrameProgress = NeedsSurfaceBuild
                    ? ProgressMapper.Subrange(Progress, 58, 99)
                    : ProgressMapper.Subrange(Progress, 25, 99);
                BuildAndStoreFrame(Snapshot, FrameProgress);
                Progress?.Report(99);
            }
        }

        /// <summary>
        /// Pre-renders alternate field content views on a background thread.
        /// </summary>
        private void ScheduleAlternateContentViewPrewarm
            (
            RenderSnapshot CompletedSnapshot
            )
        {
            if ((CompletedSnapshot.Project == null) || (CompletedSnapshot.Display == null) || !CompletedSnapshot.Project.HasSurvey)
                return;

            FieldContentView[] AlternateViews = CompletedSnapshot.Project.HasDesign
                ? new[] { FieldContentView.Existing, FieldContentView.Proposed, FieldContentView.CutFill }
                : new[] { FieldContentView.Existing };

            List<FieldContentView> PendingViews = new List<FieldContentView>();
            foreach (FieldContentView AlternateView in AlternateViews)
            {
                if (AlternateView == CompletedSnapshot.ContentView)
                    continue;

                RenderSnapshot AlternateSnapshot = CloneSnapshotForContentView(CompletedSnapshot, AlternateView);
                string CacheKey = BuildFrameCacheKey(
                    AlternateSnapshot.Width,
                    AlternateSnapshot.Height,
                    AlternateSnapshot.ViewMode,
                    AlternateSnapshot);
                FieldContentView ResolvedView = ResolveContentView(AlternateSnapshot.Project, AlternateView);
                if (TryGetCachedFrame(
                    AlternateSnapshot.ViewMode,
                    ResolvedView,
                    CacheKey,
                    AlternateSnapshot.Width,
                    AlternateSnapshot.Height) != null)
                {
                    continue;
                }

                PendingViews.Add(AlternateView);
            }

            if (PendingViews.Count == 0)
                return;

            int PrewarmVersion = _BackgroundPrewarmVersion;
            DisplaySettings Display = CompletedSnapshot.Display;
            _ = Task.Run(() =>
            {
                if (PrewarmVersion != _BackgroundPrewarmVersion)
                    return;

                lock (_PrepareLock)
                {
                    foreach (FieldContentView AlternateView in PendingViews)
                    {
                        if (PrewarmVersion != _BackgroundPrewarmVersion)
                            return;

                        _Surface.PrewarmDisplayCaches(
                            AlternateView,
                            Display,
                            null,
                            IncludeContours: Display.Contours);
                    }

                    foreach (FieldContentView AlternateView in PendingViews)
                    {
                        if (PrewarmVersion != _BackgroundPrewarmVersion)
                            return;

                        RenderSnapshot AlternateSnapshot = CloneSnapshotForContentView(CompletedSnapshot, AlternateView);
                        PrepareViewBackground(AlternateSnapshot, null);
                    }
                }

                Avalonia.Threading.Dispatcher.UIThread.Post(InvalidateVisual);
            });
        }

        /// <summary>
        /// Pre-renders the opposite view mode on a background thread so the next toggle is instant.
        /// </summary>
        private void ScheduleOppositeModePrewarm
            (
            RenderSnapshot CompletedSnapshot
            )
        {
            ViewMode OtherMode = CompletedSnapshot.ViewMode == ViewMode.TwoD ? ViewMode.ThreeD : ViewMode.TwoD;
            RenderSnapshot OtherSnapshot = CaptureRenderSnapshot(OtherMode);
            if ((OtherSnapshot.Width <= 0) || (OtherSnapshot.Height <= 0))
                return;

            string CacheKey = BuildFrameCacheKey(
                OtherSnapshot.Width,
                OtherSnapshot.Height,
                OtherMode,
                OtherSnapshot);
            FieldContentView OtherContentView = ResolveContentView(OtherSnapshot.Project, OtherSnapshot.ContentView);
            if (TryGetCachedFrame(OtherMode, OtherContentView, CacheKey, OtherSnapshot.Width, OtherSnapshot.Height) != null)
                return;

            int PrewarmVersion = ++_BackgroundPrewarmVersion;
            _ = Task.Run(() =>
            {
                if (PrewarmVersion != _BackgroundPrewarmVersion)
                    return;

                PrepareViewBackground(OtherSnapshot, null);
                Avalonia.Threading.Dispatcher.UIThread.Post(InvalidateVisual);
            });
        }

        /// <summary>
        /// Copies a render snapshot with a different field content view.
        /// </summary>
        private static RenderSnapshot CloneSnapshotForContentView
            (
            RenderSnapshot Source,
            FieldContentView ContentView
            )
        {
            return new RenderSnapshot
            {
                Project = Source.Project,
                ViewMode = Source.ViewMode,
                ContentView = ContentView,
                Display = Source.Display,
                CentreLatitude = Source.CentreLatitude,
                CentreLongitude = Source.CentreLongitude,
                ZoomFactor = Source.ZoomFactor,
                OrbitHeadingDeg = Source.OrbitHeadingDeg,
                OrbitPitchDeg = Source.OrbitPitchDeg,
                OrbitDistanceM = Source.OrbitDistanceM,
                OrbitCentreLatitude = Source.OrbitCentreLatitude,
                OrbitCentreLongitude = Source.OrbitCentreLongitude,
                Width = Source.Width,
                Height = Source.Height,
                SurfaceDirty = false
            };
        }

        /// <summary>
        /// Renders one frame into the mode-specific cache from a snapshot.
        /// </summary>
        /// <param name="Snapshot">Frozen render state.</param>
        private void BuildAndStoreFrame
            (
            RenderSnapshot Snapshot,
            IProgress<int>? Progress = null
            )
        {
            if ((Snapshot.Project == null) || !Snapshot.Project.HasSurvey)
                return;

            if (Snapshot.Display != null)
                _Display = Snapshot.Display;

            Progress?.Report(0);
            string CacheKey = BuildFrameCacheKey(
                Snapshot.Width,
                Snapshot.Height,
                Snapshot.ViewMode,
                Snapshot);
            using SKBitmap FrameBitmap = new SKBitmap(Snapshot.Width, Snapshot.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using SKCanvas Canvas = new SKCanvas(FrameBitmap);
            SKRect Viewport = SKRect.Create(0, 0, Snapshot.Width, Snapshot.Height);
            FieldContentView ContentView = ResolveContentView(Snapshot.Project, Snapshot.ContentView);

            if (Snapshot.ViewMode == ViewMode.TwoD)
            {
                double MetresPerPixel = FieldRenderer.EstimateMetresPerPixel(Snapshot.CentreLatitude, Snapshot.ZoomFactor);
                FieldRenderer.Draw2D(
                    Canvas,
                    Viewport,
                    Snapshot.Project,
                    _Surface,
                    ContentView,
                    _Display,
                    Snapshot.CentreLatitude,
                    Snapshot.CentreLongitude,
                    MetresPerPixel,
                    _Display.Satellite ? _Satellite : null,
                    Progress);
            }
            else
            {
                double OrbitLat = ResolveOrbitCentreLatitude(Snapshot.OrbitCentreLatitude);
                double OrbitLon = ResolveOrbitCentreLongitude(Snapshot.OrbitCentreLongitude);
                FieldRenderer.Draw3D(
                    Canvas,
                    Viewport,
                    Snapshot.Project,
                    _Surface,
                    ContentView,
                    _Display,
                    Snapshot.OrbitHeadingDeg,
                    Snapshot.OrbitPitchDeg,
                    Snapshot.OrbitDistanceM,
                    _Display.VerticalExaggeration,
                    OrbitLat,
                    OrbitLon,
                    _Display.Satellite ? _Satellite : null,
                    Progress);
            }

            Canvas.Flush();
            Progress?.Report(96);
            WriteableBitmap AvaloniaFrame = CopySkiaToWriteableBitmap(FrameBitmap, Snapshot.Width, Snapshot.Height);
            StoreFrameCacheForMode(Snapshot.ViewMode, ContentView, CacheKey, AvaloniaFrame);
            Progress?.Report(100);
        }

        /// <summary>
        /// Stores a rendered frame in the cache for a specific view mode and content view.
        /// </summary>
        private void StoreFrameCacheForMode
            (
            ViewMode Mode,
            FieldContentView ContentView,
            string CacheKey,
            WriteableBitmap Frame
            )
        {
            int Index = ContentViewCacheIndex(ContentView);
            if (Index < 0)
                return;

            if (Mode == ViewMode.TwoD)
            {
                _FrameCaches2D[Index] = Frame;
                _FrameCacheKeys2D[Index] = CacheKey;
                _FrameSatelliteGenerations2D[Index] = _Display.Satellite ? _Satellite.CacheGeneration : 0;
            }
            else
            {
                _FrameCaches3D[Index] = Frame;
                _FrameCacheKeys3D[Index] = CacheKey;
                _FrameSatelliteGenerations3D[Index] = _Display.Satellite ? _Satellite.CacheGeneration : 0;
            }
        }

        /// <summary>
        /// Immutable render inputs captured on the UI thread for background work.
        /// </summary>
        private sealed class RenderSnapshot
        {
            public ProjectDocument? Project { get; init; }
            public ViewMode ViewMode { get; init; }
            public FieldContentView ContentView { get; init; }
            public DisplaySettings? Display { get; init; }
            public double CentreLatitude { get; init; }
            public double CentreLongitude { get; init; }
            public double ZoomFactor { get; init; }
            public double OrbitHeadingDeg { get; init; }
            public double OrbitPitchDeg { get; init; }
            public double OrbitDistanceM { get; init; }
            public double OrbitCentreLatitude { get; init; }
            public double OrbitCentreLongitude { get; init; }
            public int Width { get; init; }
            public int Height { get; init; }
            public bool SurfaceDirty { get; init; }
        }

        /// <summary>
        /// Renders the field view into a Skia bitmap and draws it to the Avalonia surface.
        /// </summary>
        /// <param name="Context">Drawing context for the control.</param>
        public override void Render
            (
            DrawingContext Context
            )
        {
            int Width = Math.Max(1, (int)Bounds.Width);
            int Height = Math.Max(1, (int)Bounds.Height);
            bool Suppress = SuppressSynchronousPrepare;
            bool HasTopo = (Project != null) && Project.HasSurvey;

            if (!HasTopo)
            {
                Context.FillRectangle(Brushes.Black, new Rect(0, 0, Width, Height));
                return;
            }

            if (_SurfaceDirty && !Suppress)
            {
                // Fallback synchronous rebuild when async preparation was not used.
                _Surface.BuildFromProject(Project);
                _SurfaceDirty = false;
            }

            if (DisplaySettings != null)
                _Display = DisplaySettings;

            FieldContentView ContentView = ResolveContentView();
            RenderSnapshot Snapshot = CaptureRenderSnapshot(ViewMode);
            string CacheKey = BuildFrameCacheKey(Width, Height, ViewMode, Snapshot);
            WriteableBitmap? CachedFrame = TryGetCachedFrame(ViewMode, ContentView, CacheKey, Width, Height);
            if (CachedFrame != null)
            {
                _PresentedFrame = CachedFrame;
                Context.DrawImage(CachedFrame, new Rect(0, 0, Width, Height));
                DrawViewCubeOverlay(Context, Width, Height);
                return;
            }

            if (Suppress)
            {
                if (_PresentedFrame != null)
                {
                    Context.DrawImage(_PresentedFrame, new Rect(0, 0, Width, Height));
                    DrawViewCubeOverlay(Context, Width, Height);
                }
                else
                    Context.FillRectangle(Brushes.Black, new Rect(0, 0, Width, Height));

                return;
            }

            BuildAndStoreFrame(Snapshot);
            FieldContentView RefreshedContentView = ResolveContentView(Snapshot.Project, Snapshot.ContentView);
            CachedFrame = TryGetCachedFrame(ViewMode, RefreshedContentView, CacheKey, Width, Height);
            if (CachedFrame != null)
            {
                _PresentedFrame = CachedFrame;
                Context.DrawImage(CachedFrame, new Rect(0, 0, Width, Height));
                DrawViewCubeOverlay(Context, Width, Height);
            }
            else if (_PresentedFrame != null)
            {
                Context.DrawImage(_PresentedFrame, new Rect(0, 0, Width, Height));
                DrawViewCubeOverlay(Context, Width, Height);
            }
            else
                Context.FillRectangle(Brushes.Black, new Rect(0, 0, Width, Height));
        }

        /// <summary>
        /// Draws the interactive view cube on top of the cached 3D frame.
        /// </summary>
        private void DrawViewCubeOverlay
            (
            DrawingContext Context,
            int Width,
            int Height
            )
        {
            if (ViewMode != ViewMode.ThreeD)
                return;

            SKRect Viewport = SKRect.Create(0, 0, Width, Height);
            SKRect WidgetBounds = ViewCubeRenderer.GetBounds(Viewport);
            int OverlayWidth = Math.Max(1, (int)Math.Ceiling(WidgetBounds.Width));
            int OverlayHeight = Math.Max(1, (int)Math.Ceiling(WidgetBounds.Height));

            using SKBitmap OverlayBitmap = new SKBitmap(OverlayWidth, OverlayHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
            using SKCanvas OverlayCanvas = new SKCanvas(OverlayBitmap);
            OverlayCanvas.Clear(SKColors.Transparent);
            ViewCubeRenderer.Draw(
                OverlayCanvas,
                SKRect.Create(0, 0, OverlayWidth, OverlayHeight),
                OrbitHeadingDeg,
                OrbitPitchDeg,
                _ViewCubeHover);
            OverlayCanvas.Flush();

            WriteableBitmap AvaloniaOverlay = CopySkiaToWriteableBitmap(OverlayBitmap, OverlayWidth, OverlayHeight);
            Context.DrawImage(AvaloniaOverlay, new Rect(WidgetBounds.Left, WidgetBounds.Top, OverlayWidth, OverlayHeight));
        }

        /// <summary>
        /// Builds a cache fingerprint for the active view mode and camera state.
        /// </summary>
        private string BuildFrameCacheKey
            (
            int Width,
            int Height,
            ViewMode Mode,
            RenderSnapshot? Snapshot
            )
        {
            return ViewerFrameCacheKey.Build(CreateViewerFrameState(Width, Height, Mode, Snapshot));
        }

        /// <summary>
        /// Captures cache inputs for the requested view mode.
        /// </summary>
        private ViewerFrameState CreateViewerFrameState
            (
            int Width,
            int Height,
            ViewMode Mode,
            RenderSnapshot? Snapshot
            )
        {
            DisplaySettings DisplaySource = Snapshot?.Display ?? _Display;
            ProjectDocument? ProjectSource = Snapshot?.Project ?? Project;
            FieldContentView View = Snapshot != null
                ? ResolveContentView(Snapshot.Project, Snapshot.ContentView)
                : ResolveContentView();

            double OrbitLat = Snapshot != null
                ? ResolveOrbitCentreLatitude(Snapshot.OrbitCentreLatitude)
                : ResolveOrbitCentreLatitude();
            double OrbitLon = Snapshot != null
                ? ResolveOrbitCentreLongitude(Snapshot.OrbitCentreLongitude)
                : ResolveOrbitCentreLongitude();

            return new ViewerFrameState
            {
                ViewMode = Mode,
                Width = Width,
                Height = Height,
                ContentView = View,
                Display = DisplaySource,
                ProjectKey = ProjectSource?.FilePath ?? "",
                CentreLat = Snapshot?.CentreLatitude ?? CentreLatitude,
                CentreLon = Snapshot?.CentreLongitude ?? CentreLongitude,
                ZoomFactor = Snapshot?.ZoomFactor ?? ZoomFactor,
                OrbitHeadingDeg = Snapshot?.OrbitHeadingDeg ?? OrbitHeadingDeg,
                OrbitPitchDeg = Snapshot?.OrbitPitchDeg ?? OrbitPitchDeg,
                OrbitDistanceM = Snapshot?.OrbitDistanceM ?? OrbitDistanceM,
                OrbitCentreLat = OrbitLat,
                OrbitCentreLon = OrbitLon
            };
        }

        /// <summary>
        /// Returns a cached frame bitmap when the fingerprint matches the current view state.
        /// </summary>
        private WriteableBitmap? TryGetCachedFrame
            (
            ViewMode Mode,
            FieldContentView ContentView,
            string CacheKey,
            int Width,
            int Height
            )
        {
            int Index = ContentViewCacheIndex(ContentView);
            if (Index < 0)
                return null;

            WriteableBitmap? Frame = Mode == ViewMode.TwoD ? _FrameCaches2D[Index] : _FrameCaches3D[Index];
            string StoredKey = Mode == ViewMode.TwoD ? _FrameCacheKeys2D[Index] : _FrameCacheKeys3D[Index];
            DisplaySettings DisplaySource = _Display;
            if ((Frame != null) &&
                (StoredKey == CacheKey) &&
                (Frame.PixelSize.Width == Width) &&
                (Frame.PixelSize.Height == Height) &&
                IsSatelliteFrameCacheValid(Mode, Index, DisplaySource.Satellite))
            {
                return Frame;
            }

            return null;
        }

        /// <summary>
        /// Stores a rendered frame in the cache for the active view mode.
        /// </summary>
        private void StoreFrameCache
            (
            string CacheKey,
            WriteableBitmap Frame
            )
        {
            StoreFrameCacheForMode(ViewMode, ResolveContentView(), CacheKey, Frame);
        }

        /// <summary>
        /// Copies a Skia bitmap into a new Avalonia writeable bitmap.
        /// </summary>
        private static WriteableBitmap CopySkiaToWriteableBitmap
            (
            SKBitmap FrameBitmap,
            int Width,
            int Height
            )
        {
            PixelSize PixelSize = new PixelSize(Width, Height);
            WriteableBitmap AvaloniaFrame = new WriteableBitmap(
                PixelSize,
                new Vector(96, 96),
                PixelFormat.Bgra8888,
                AlphaFormat.Premul);
            using (ILockedFramebuffer Locked = AvaloniaFrame.Lock())
            {
                using SKPixmap Pixmap = FrameBitmap.PeekPixels();
                IntPtr SrcPixels = Pixmap.GetPixels();
                if (SrcPixels != IntPtr.Zero)
                {
                    int BytesPerRow = Math.Min(Pixmap.RowBytes, Locked.RowBytes);
                    int TotalBytes = BytesPerRow * Height;
                    if ((Pixmap.RowBytes == Locked.RowBytes) && (BytesPerRow == Locked.RowBytes))
                    {
                        unsafe
                        {
                            Buffer.MemoryCopy(
                                (void*)SrcPixels,
                                (void*)Locked.Address,
                                TotalBytes,
                                TotalBytes);
                        }
                    }
                    else
                    {
                        for (int Row = 0; Row < Height; Row++)
                        {
                            IntPtr SrcRow = SrcPixels + (Row * Pixmap.RowBytes);
                            IntPtr DstRow = Locked.Address + (Row * Locked.RowBytes);
                            unsafe
                            {
                                Buffer.MemoryCopy((void*)SrcRow, (void*)DstRow, BytesPerRow, BytesPerRow);
                            }
                        }
                    }
                }
            }

            return AvaloniaFrame;
        }

        /// <summary>
        /// Resolves the effective 3D orbit pivot latitude.
        /// </summary>
        /// <param name="ExplicitLatitude">Bound orbit latitude, or NaN to infer from the surface.</param>
        /// <returns>Orbit pivot latitude in decimal degrees.</returns>
        private double ResolveOrbitCentreLatitude
            (
            double ExplicitLatitude = double.NaN
            )
        {
            if (!double.IsNaN(ExplicitLatitude))
                return ExplicitLatitude;
            if (!double.IsNaN(OrbitCentreLatitude))
                return OrbitCentreLatitude;
            if (_Surface.SampleWidth > 0)
                return (_Surface.MinLat + _Surface.MaxLat) * 0.5;
            return CentreLatitude;
        }

        /// <summary>
        /// Resolves the effective 3D orbit pivot longitude.
        /// </summary>
        /// <param name="ExplicitLongitude">Bound orbit longitude, or NaN to infer from the surface.</param>
        /// <returns>Orbit pivot longitude in decimal degrees.</returns>
        private double ResolveOrbitCentreLongitude
            (
            double ExplicitLongitude = double.NaN
            )
        {
            if (!double.IsNaN(ExplicitLongitude))
                return ExplicitLongitude;
            if (!double.IsNaN(OrbitCentreLongitude))
                return OrbitCentreLongitude;
            if (_Surface.SampleWidth > 0)
                return (_Surface.MinLon + _Surface.MaxLon) * 0.5;
            return CentreLongitude;
        }

        /// <summary>
        /// Resolves the effective content view, inferring from project data when unset.
        /// </summary>
        /// <returns>The content layer to draw.</returns>
        private FieldContentView ResolveContentView
            (
            )
        {
            return ResolveContentView(Project, ContentView);
        }

        /// <summary>
        /// Resolves the effective content view from explicit project and view inputs.
        /// </summary>
        /// <param name="ProjectSource">Project document being rendered.</param>
        /// <param name="RequestedView">Requested content view from bindings.</param>
        /// <returns>The content layer to draw.</returns>
        private static FieldContentView ResolveContentView
            (
            ProjectDocument? ProjectSource,
            FieldContentView RequestedView
            )
        {
            if (RequestedView != FieldContentView.None)
                return RequestedView;
            if ((ProjectSource != null) && ProjectSource.HasDesign)
                return FieldContentView.Proposed;
            if ((ProjectSource != null) && ProjectSource.HasSurvey)
                return FieldContentView.Existing;
            return FieldContentView.None;
        }

        /// <summary>
        /// Begins a drag gesture when the left or right mouse button is pressed.
        /// </summary>
        private void OnPointerPressed
            (
            object? Sender,
            PointerPressedEventArgs Args
            )
        {
            PointerPointProperties Props = Args.GetCurrentPoint(this).Properties;
            if (Props.IsLeftButtonPressed && (ViewMode == ViewMode.ThreeD))
            {
                Point Position = Args.GetPosition(this);
                if (HitTestCompass(Position))
                {
                    StartOrientationAnimation(NORTH_UP_HEADING_DEG, OrbitPitchDeg);
                    Args.Handled = true;
                    return;
                }

                ViewCubeHit Hit = HitTestViewCube(Position);
                if (Hit.IsHit)
                {
                    StartViewCubeAnimation(Hit);
                    Args.Handled = true;
                    return;
                }
            }

            if (Props.IsLeftButtonPressed)
            {
                StopViewCubeAnimation();
                _IsDragging = true;
                _LastPointer = Args.GetPosition(this);
                Args.Pointer.Capture(this);
            }
            else if (Props.IsRightButtonPressed && (ViewMode == ViewMode.ThreeD))
            {
                _IsRightDragging = true;
                _LastPointer = Args.GetPosition(this);
                Args.Pointer.Capture(this);
            }
        }

        /// <summary>
        /// Pans, orbits, or shifts the 3D orbit pivot while the user drags.
        /// </summary>
        private void OnPointerMoved
            (
            object? Sender,
            PointerEventArgs Args
            )
        {
            if (!_IsDragging && !_IsRightDragging)
            {
                UpdateViewCubeHover(Args.GetPosition(this));
                return;
            }

            Point Current = Args.GetPosition(this);
            double Dx = Current.X - _LastPointer.X;
            double Dy = Current.Y - _LastPointer.Y;
            _LastPointer = Current;

            if (_IsRightDragging && (ViewMode == ViewMode.ThreeD))
            {
                Pan3DOrbitCentre(Dx, Dy);
            }
            else if (_IsDragging)
            {
                if (ViewMode == ViewMode.TwoD)
                    Pan2D(Dx, Dy);
                else
                {
                    OrbitHeadingDeg = (OrbitHeadingDeg + (Dx * ORBIT_HEADING_SENSITIVITY) + DEGREES_IN_FULL_CIRCLE) % DEGREES_IN_FULL_CIRCLE;
                    OrbitPitchDeg = Math.Clamp(OrbitPitchDeg + (Dy * ORBIT_PITCH_SENSITIVITY), FieldCamera3D.MinimumOrbitPitchDeg, MAX_ORBIT_PITCH_DEG);
                }
            }

            RaiseViewCameraChanged();
            InvalidateVisual();
        }

        /// <summary>
        /// Ends the active drag gesture and releases pointer capture.
        /// </summary>
        private void OnPointerReleased
            (
            object? Sender,
            PointerReleasedEventArgs Args
            )
        {
            _IsDragging = false;
            _IsRightDragging = false;
            Args.Pointer.Capture(null);
        }

        /// <summary>
        /// Shifts the 3D orbit pivot in ground space (right-drag pan).
        /// </summary>
        private void Pan3DOrbitCentre
            (
            double Dx,
            double Dy
            )
        {
            int Width = Math.Max(1, (int)Bounds.Width);
            int Height = Math.Max(1, (int)Bounds.Height);
            SKRect Viewport = SKRect.Create(0, 0, Width, Height);

            FieldCamera3D Camera = new FieldCamera3D(
                _Surface,
                OrbitHeadingDeg,
                OrbitPitchDeg,
                OrbitDistanceM,
                _Display.VerticalExaggeration,
                ResolveOrbitCentreLatitude(),
                ResolveOrbitCentreLongitude(),
                ResolveContentView());

            (double DeltaLat, double DeltaLon) = Camera.ComputeScreenPanLatLonDelta(Dx, Dy, Viewport);
            OrbitCentreLatitude = ResolveOrbitCentreLatitude() + DeltaLat;
            OrbitCentreLongitude = ResolveOrbitCentreLongitude() + DeltaLon;
        }

        /// <summary>
        /// Applies zoom in 2D mode or dolly in 3D mode from the mouse wheel.
        /// </summary>
        /// <param name="Sender">Event source.</param>
        /// <param name="Args">Wheel delta details.</param>
        private void OnPointerWheelChanged
            (
            object? Sender,
            PointerWheelEventArgs Args
            )
        {
            double Delta = Args.Delta.Y;
            if (ViewMode == ViewMode.TwoD)
            {
                double Factor = (Delta > 0) ? WHEEL_ZOOM_IN_FACTOR : WHEEL_ZOOM_OUT_FACTOR;
                ZoomFactor = Math.Clamp(ZoomFactor * Factor, SatelliteMapLayout.MIN_ZOOM_FACTOR, SatelliteMapLayout.MAX_ZOOM_FACTOR);
            }
            else
            {
                OrbitDistanceM = Math.Clamp(
                    OrbitDistanceM * ((Delta > 0) ? ORBIT_WHEEL_ZOOM_IN_FACTOR : ORBIT_WHEEL_ZOOM_OUT_FACTOR),
                    MIN_ORBIT_DISTANCE_M,
                    MAX_ORBIT_DISTANCE_M);
            }

            RaiseViewCameraChanged();
            InvalidateVisual();
        }

        /// <summary>
        /// Converts pointer delta in pixels to a geographic pan in decimal degrees.
        /// </summary>
        /// <param name="Dx">Horizontal pointer movement in pixels.</param>
        /// <param name="Dy">Vertical pointer movement in pixels.</param>
        private void Pan2D
            (
            double Dx,
            double Dy
            )
        {
            double MetresPerPixel = FieldRenderer.EstimateMetresPerPixel(CentreLatitude, ZoomFactor);
            double MetresPerDegreeLon = METRES_PER_DEGREE_LATITUDE * Math.Cos(CentreLatitude * Math.PI / 180.0);
            CentreLongitude -= Dx * MetresPerPixel / MetresPerDegreeLon;
            CentreLatitude += Dy * MetresPerPixel / METRES_PER_DEGREE_LATITUDE;
        }

        /// <summary>
        /// Notifies listeners that the view camera changed after user interaction.
        /// </summary>
        private void RaiseViewCameraChanged
            (
            )
        {
            // Snapshot the delegate so raises remain stable if a handler unsubscribes mid-invoke.
            EventHandler? Handler = ViewCentreChanged;
            Handler?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Hit-tests the view cube at a pointer position in control coordinates.
        /// </summary>
        private ViewCubeHit HitTestViewCube
            (
            Point Position
            )
        {
            int Width = Math.Max(1, (int)Bounds.Width);
            int Height = Math.Max(1, (int)Bounds.Height);
            SKRect Viewport = SKRect.Create(0, 0, Width, Height);
            return ViewCubeRenderer.HitTest(Viewport, OrbitHeadingDeg, OrbitPitchDeg, (float)Position.X, (float)Position.Y);
        }

        /// <summary>
        /// Updates hover highlighting for the view cube.
        /// </summary>
        private void UpdateViewCubeHover
            (
            Point Position
            )
        {
            if (ViewMode != ViewMode.ThreeD)
            {
                if (_ViewCubeHover.IsHit)
                {
                    _ViewCubeHover = ViewCubeHit.None;
                    InvalidateVisual();
                }

                return;
            }

            ViewCubeHit Hit = HitTestViewCube(Position);
            if ((Hit.Kind == _ViewCubeHover.Kind) && (Hit.Index == _ViewCubeHover.Index))
                return;

            _ViewCubeHover = Hit;
            InvalidateVisual();
        }

        /// <summary>
        /// Smoothly animates the orbit camera to the orientation for a view-cube hit.
        /// </summary>
        private void StartViewCubeAnimation
            (
            ViewCubeHit Hit
            )
        {
            (double TargetHeadingDeg, double TargetPitchDeg) = ViewCubeRenderer.ResolveTargetOrientation(Hit, OrbitHeadingDeg);
            StartOrientationAnimation(TargetHeadingDeg, TargetPitchDeg);
        }

        /// <summary>
        /// Smoothly animates the orbit camera to a target heading and pitch.
        /// </summary>
        private void StartOrientationAnimation
            (
            double TargetHeadingDeg,
            double TargetPitchDeg
            )
        {
            _ViewCubeAnimStartHeadingDeg = OrbitHeadingDeg;
            _ViewCubeAnimStartPitchDeg = OrbitPitchDeg;
            _ViewCubeAnimTargetHeadingDeg = TargetHeadingDeg;
            _ViewCubeAnimTargetPitchDeg = TargetPitchDeg;
            _ViewCubeAnimStartTicks = Environment.TickCount64;

            if (_ViewCubeAnimTimer == null)
            {
                _ViewCubeAnimTimer = new Avalonia.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(1000.0 / 60.0)
                };
                _ViewCubeAnimTimer.Tick += OnViewCubeAnimationTick;
            }

            if (!_ViewCubeAnimTimer.IsEnabled)
                _ViewCubeAnimTimer.Start();

            InvalidateVisual();
        }

        /// <summary>
        /// Hit-tests the top-right compass at a pointer position.
        /// </summary>
        private bool HitTestCompass
            (
            Point Position
            )
        {
            if (ViewMode != ViewMode.ThreeD)
                return false;

            int Width = Math.Max(1, (int)Bounds.Width);
            int Height = Math.Max(1, (int)Bounds.Height);
            SKRect Viewport = SKRect.Create(0, 0, Width, Height);
            return ViewCompassRenderer.HitTest(Viewport, (float)Position.X, (float)Position.Y);
        }

        /// <summary>
        /// Stops an in-progress view-cube camera animation.
        /// </summary>
        private void StopViewCubeAnimation
            (
            )
        {
            _ViewCubeAnimTimer?.Stop();
        }

        /// <summary>
        /// Advances the view-cube camera animation by one frame.
        /// </summary>
        private void OnViewCubeAnimationTick
            (
            object? Sender,
            EventArgs Args
            )
        {
            double ElapsedMs = Environment.TickCount64 - _ViewCubeAnimStartTicks;
            double LinearT = Math.Clamp(ElapsedMs / VIEW_CUBE_ANIMATION_MS, 0.0, 1.0);
            double T = 1.0 - ((1.0 - LinearT) * (1.0 - LinearT));

            OrbitHeadingDeg = LerpHeadingDegrees(_ViewCubeAnimStartHeadingDeg, _ViewCubeAnimTargetHeadingDeg, T);
            OrbitPitchDeg = _ViewCubeAnimStartPitchDeg + ((_ViewCubeAnimTargetPitchDeg - _ViewCubeAnimStartPitchDeg) * T);
            RaiseViewCameraChanged();
            InvalidateVisual();

            if (LinearT >= 1.0)
            {
                OrbitHeadingDeg = _ViewCubeAnimTargetHeadingDeg;
                OrbitPitchDeg = _ViewCubeAnimTargetPitchDeg;
                _ViewCubeAnimTimer?.Stop();
            }
        }

        /// <summary>
        /// Interpolates heading along the shortest arc on a 360-degree circle.
        /// </summary>
        private static double LerpHeadingDegrees
            (
            double FromDeg,
            double ToDeg,
            double T
            )
        {
            double Delta = ((ToDeg - FromDeg + 540.0) % DEGREES_IN_FULL_CIRCLE) - 180.0;
            double Value = FromDeg + (Delta * T);
            return (Value + DEGREES_IN_FULL_CIRCLE) % DEGREES_IN_FULL_CIRCLE;
        }

        /// <summary>
        /// Centres and zooms the 2D view to fit the current project field bounds.
        /// </summary>
        public void FitToField
            (
            )
        {
            if (!FieldBounds.TryFromProject(Project, out FieldBounds Field))
                return;

            int Width = Math.Max(1, (int)Bounds.Width);
            int Height = Math.Max(1, (int)Bounds.Height);
            MapViewFitResult Fit = MapViewFitter.Fit(Field, Width, Height);

            CentreLatitude = Fit.CentreLat;
            CentreLongitude = Fit.CentreLon;
            ZoomFactor = Fit.ZoomFactor;
            OrbitCentreLatitude = Fit.CentreLat;
            OrbitCentreLongitude = Fit.CentreLon;
            RaiseViewCameraChanged();
            InvalidateVisual();
        }
    }
}
