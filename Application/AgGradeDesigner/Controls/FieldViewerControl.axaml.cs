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
    /// </summary>
    public partial class FieldViewerControl : Control
    {
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

        private readonly FieldSurfaceSampler _Surface = new FieldSurfaceSampler();
        private readonly SatelliteTileService _Satellite = new SatelliteTileService();
        private DisplaySettings _Display = new DisplaySettings();
        private bool _IsDragging;
        private bool _IsRightDragging;
        private Point _LastPointer;
        private bool _SurfaceDirty = true;
        private WriteableBitmap? _FrameCache2D;
        private WriteableBitmap? _FrameCache3D;
        private string _FrameCacheKey2D = "";
        private string _FrameCacheKey3D = "";

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
            // Refresh the Skia frame when background satellite tiles finish loading.
            _Satellite.TilesUpdated += OnSatelliteTilesUpdated;
            PointerPressed += OnPointerPressed;
            PointerMoved += OnPointerMoved;
            PointerReleased += OnPointerReleased;
            PointerWheelChanged += OnPointerWheelChanged;
        }

        /// <summary>
        /// Marshals satellite tile updates onto the UI thread for a safe redraw.
        /// </summary>
        private void OnSatelliteTilesUpdated
            (
            )
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(InvalidateRenderedFrame);
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
        /// Discards cached 2D and 3D frame bitmaps.
        /// </summary>
        private void InvalidateFrameCaches
            (
            )
        {
            _FrameCache2D = null;
            _FrameCache3D = null;
            _FrameCacheKey2D = "";
            _FrameCacheKey3D = "";
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

            if ((Change.Property == ProjectProperty) ||
                (Change.Property == ContentViewProperty))
            {
                // Rebuild elevation mesh when project or scalar view changes — not when toggling 2D/3D.
                _SurfaceDirty = true;
                InvalidateFrameCaches();
            }
        }

        /// <summary>
        /// Rebuilds the surface mesh and pre-renders the active view on a worker thread.
        /// </summary>
        /// <param name="Progress">Optional 0–100 progress reporter for viewer preparation.</param>
        /// <param name="TargetMode">View mode to prepare, or null for the current mode.</param>
        public async Task PrepareViewAsync
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
                return;
            }

            Progress?.Report(5);
            await Task.Run(() => PrepareViewBackground(Snapshot, Progress)).ConfigureAwait(true);
            Progress?.Report(100);
            InvalidateVisual();
        }

        /// <summary>
        /// Captures render inputs from the UI thread before background preparation.
        /// </summary>
        private RenderSnapshot CaptureRenderSnapshot
            (
            ViewMode? TargetMode
            )
        {
            return new RenderSnapshot
            {
                Project = Project,
                ViewMode = TargetMode ?? ViewMode,
                ContentView = ContentView,
                Display = DisplaySettings ?? _Display,
                CentreLatitude = CentreLatitude,
                CentreLongitude = CentreLongitude,
                ZoomFactor = ZoomFactor,
                OrbitHeadingDeg = OrbitHeadingDeg,
                OrbitPitchDeg = OrbitPitchDeg,
                OrbitDistanceM = OrbitDistanceM,
                OrbitCentreLatitude = OrbitCentreLatitude,
                OrbitCentreLongitude = OrbitCentreLongitude,
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
            bool HasTopo = (Snapshot.Project != null) && Snapshot.Project.HasSurvey;
            if (!HasTopo)
            {
                _Surface.BuildFromProject(Snapshot.Project, Progress: Progress);
                _SurfaceDirty = false;
                InvalidateFrameCaches();
                Progress?.Report(100);
                return;
            }

            bool NeedsSurfaceBuild = Snapshot.SurfaceDirty || (_Surface.SampleWidth == 0);
            if (NeedsSurfaceBuild)
            {
                Progress?.Report(5);
                _Surface.BuildFromProject(Snapshot.Project, Progress: ProgressMapper.Subrange(Progress, 5, 42));
                _SurfaceDirty = false;
            }

            IProgress<int>? FrameProgress = NeedsSurfaceBuild
                ? ProgressMapper.Subrange(Progress, 42, 99)
                : ProgressMapper.Subrange(Progress, 5, 99);
            BuildAndStoreFrame(Snapshot, FrameProgress);
            Progress?.Report(99);
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
            StoreFrameCacheForMode(Snapshot.ViewMode, CacheKey, AvaloniaFrame);
            Progress?.Report(100);
        }

        /// <summary>
        /// Stores a rendered frame in the cache for a specific view mode.
        /// </summary>
        private void StoreFrameCacheForMode
            (
            ViewMode Mode,
            string CacheKey,
            WriteableBitmap Frame
            )
        {
            if (Mode == ViewMode.TwoD)
            {
                _FrameCache2D = Frame;
                _FrameCacheKey2D = CacheKey;
            }
            else
            {
                _FrameCache3D = Frame;
                _FrameCacheKey3D = CacheKey;
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

            string CacheKey = BuildFrameCacheKey(Width, Height, ViewMode, null);
            WriteableBitmap? CachedFrame = TryGetCachedFrame(ViewMode, CacheKey, Width, Height);
            if (CachedFrame != null)
            {
                Context.DrawImage(CachedFrame, new Rect(0, 0, Width, Height));
                return;
            }

            if (Suppress)
            {
                Context.FillRectangle(Brushes.Black, new Rect(0, 0, Width, Height));
                return;
            }

            RenderSnapshot Snapshot = CaptureRenderSnapshot(ViewMode);
            BuildAndStoreFrame(Snapshot);
            CachedFrame = TryGetCachedFrame(ViewMode, CacheKey, Width, Height);
            if (CachedFrame != null)
                Context.DrawImage(CachedFrame, new Rect(0, 0, Width, Height));
            else
                Context.FillRectangle(Brushes.Black, new Rect(0, 0, Width, Height));
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
            DisplaySettings DisplaySource = Snapshot?.Display ?? _Display;
            string DisplayKey = String.Join(
                "|",
                DisplaySource.Satellite,
                DisplaySource.Contours,
                DisplaySource.Hillshade,
                DisplaySource.SurveyPoints,
                DisplaySource.Benchmarks,
                DisplaySource.DesignOverlay,
                DisplaySource.VerticalExaggeration.ToString("G4"));

            ProjectDocument? ProjectSource = Snapshot?.Project ?? Project;
            string ProjectKey = ProjectSource?.FilePath ?? "";

            if (Mode == ViewMode.TwoD)
            {
                double CentreLat = Snapshot?.CentreLatitude ?? CentreLatitude;
                double CentreLon = Snapshot?.CentreLongitude ?? CentreLongitude;
                double Zoom = Snapshot?.ZoomFactor ?? ZoomFactor;
                FieldContentView View = Snapshot != null
                    ? ResolveContentView(Snapshot.Project, Snapshot.ContentView)
                    : ResolveContentView();

                return String.Join(
                    "|",
                    "2D",
                    Width,
                    Height,
                    CentreLat.ToString("G8"),
                    CentreLon.ToString("G8"),
                    Zoom.ToString("G6"),
                    View,
                    DisplayKey,
                    ProjectKey);
            }

            double OrbitLat = Snapshot != null
                ? ResolveOrbitCentreLatitude(Snapshot.OrbitCentreLatitude)
                : ResolveOrbitCentreLatitude();
            double OrbitLon = Snapshot != null
                ? ResolveOrbitCentreLongitude(Snapshot.OrbitCentreLongitude)
                : ResolveOrbitCentreLongitude();
            double Heading = Snapshot?.OrbitHeadingDeg ?? OrbitHeadingDeg;
            double Pitch = Snapshot?.OrbitPitchDeg ?? OrbitPitchDeg;
            double Distance = Snapshot?.OrbitDistanceM ?? OrbitDistanceM;
            FieldContentView View3D = Snapshot != null
                ? ResolveContentView(Snapshot.Project, Snapshot.ContentView)
                : ResolveContentView();

            return String.Join(
                "|",
                "3D",
                Width,
                Height,
                Heading.ToString("G6"),
                Pitch.ToString("G6"),
                Distance.ToString("G6"),
                OrbitLat.ToString("G8"),
                OrbitLon.ToString("G8"),
                View3D,
                DisplayKey,
                ProjectKey);
        }

        /// <summary>
        /// Returns a cached frame bitmap when the fingerprint matches the current view state.
        /// </summary>
        private WriteableBitmap? TryGetCachedFrame
            (
            ViewMode Mode,
            string CacheKey,
            int Width,
            int Height
            )
        {
            if (Mode == ViewMode.TwoD)
            {
                if ((_FrameCache2D != null) &&
                    (_FrameCacheKey2D == CacheKey) &&
                    (_FrameCache2D.PixelSize.Width == Width) &&
                    (_FrameCache2D.PixelSize.Height == Height))
                {
                    return _FrameCache2D;
                }
            }
            else
            {
                if ((_FrameCache3D != null) &&
                    (_FrameCacheKey3D == CacheKey) &&
                    (_FrameCache3D.PixelSize.Width == Width) &&
                    (_FrameCache3D.PixelSize.Height == Height))
                {
                    return _FrameCache3D;
                }
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
            StoreFrameCacheForMode(ViewMode, CacheKey, Frame);
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
                using SKImage SkiaFrame = SKImage.FromBitmap(FrameBitmap);
                using SKPixmap Pixmap = SkiaFrame.PeekPixels();
                IntPtr SrcPixels = Pixmap.GetPixels();
                if (SrcPixels != IntPtr.Zero)
                {
                    int BytesPerRow = Math.Min(Pixmap.RowBytes, Locked.RowBytes);
                    int TotalBytes = BytesPerRow * Height;
                    byte[] Buffer = new byte[TotalBytes];
                    Marshal.Copy(SrcPixels, Buffer, 0, TotalBytes);
                    Marshal.Copy(Buffer, 0, Locked.Address, TotalBytes);
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
            if (Props.IsLeftButtonPressed)
            {
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
                return;

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
                    OrbitHeadingDeg = (OrbitHeadingDeg - (Dx * ORBIT_HEADING_SENSITIVITY) + DEGREES_IN_FULL_CIRCLE) % DEGREES_IN_FULL_CIRCLE;
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
                ResolveOrbitCentreLongitude());

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
