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
                OrbitCentreLongitudeProperty);
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
            Avalonia.Threading.Dispatcher.UIThread.Post(InvalidateVisual);
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

            if (_SurfaceDirty)
            {
                // Sample elevations once per project or binding change, not every frame.
                _Surface.BuildFromProject(Project);
                _SurfaceDirty = false;
            }

            if (DisplaySettings != null)
                _Display = DisplaySettings;

            string CacheKey = BuildFrameCacheKey(Width, Height);
            WriteableBitmap? CachedFrame = TryGetCachedFrame(CacheKey, Width, Height);
            if (CachedFrame != null)
            {
                Context.DrawImage(CachedFrame, new Rect(0, 0, Width, Height));
                return;
            }

            using SKBitmap FrameBitmap = new SKBitmap(Width, Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using SKCanvas Canvas = new SKCanvas(FrameBitmap);
            SKRect Viewport = SKRect.Create(0, 0, Width, Height);

            if (ViewMode == ViewMode.TwoD)
            {
                double MetresPerPixel = FieldRenderer.EstimateMetresPerPixel(CentreLatitude, ZoomFactor);
                FieldRenderer.Draw2D(
                    Canvas,
                    Viewport,
                    Project,
                    _Surface,
                    ResolveContentView(),
                    _Display,
                    CentreLatitude,
                    CentreLongitude,
                    MetresPerPixel,
                    _Display.Satellite ? _Satellite : null);
            }
            else
            {
                FieldRenderer.Draw3D(
                    Canvas,
                    Viewport,
                    Project,
                    _Surface,
                    ResolveContentView(),
                    _Display,
                    OrbitHeadingDeg,
                    OrbitPitchDeg,
                    OrbitDistanceM,
                    _Display.VerticalExaggeration,
                    ResolveOrbitCentreLatitude(),
                    ResolveOrbitCentreLongitude(),
                    _Display.Satellite ? _Satellite : null);
            }

            Canvas.Flush();

            WriteableBitmap AvaloniaFrame = CopySkiaToWriteableBitmap(FrameBitmap, Width, Height);
            StoreFrameCache(CacheKey, AvaloniaFrame);
            Context.DrawImage(AvaloniaFrame, new Rect(0, 0, Width, Height));
        }

        /// <summary>
        /// Builds a cache fingerprint for the active view mode and camera state.
        /// </summary>
        private string BuildFrameCacheKey
            (
            int Width,
            int Height
            )
        {
            string DisplayKey = String.Join(
                "|",
                _Display.Satellite,
                _Display.Contours,
                _Display.Hillshade,
                _Display.SurveyPoints,
                _Display.Benchmarks,
                _Display.DesignOverlay,
                _Display.VerticalExaggeration.ToString("G4"));

            if (ViewMode == ViewMode.TwoD)
            {
                return String.Join(
                    "|",
                    "2D",
                    Width,
                    Height,
                    CentreLatitude.ToString("G8"),
                    CentreLongitude.ToString("G8"),
                    ZoomFactor.ToString("G6"),
                    ResolveContentView(),
                    DisplayKey,
                    Project?.FilePath ?? "");
            }

            return String.Join(
                "|",
                "3D",
                Width,
                Height,
                OrbitHeadingDeg.ToString("G6"),
                OrbitPitchDeg.ToString("G6"),
                OrbitDistanceM.ToString("G6"),
                ResolveOrbitCentreLatitude().ToString("G8"),
                ResolveOrbitCentreLongitude().ToString("G8"),
                ResolveContentView(),
                DisplayKey,
                Project?.FilePath ?? "");
        }

        /// <summary>
        /// Returns a cached frame bitmap when the fingerprint matches the current view state.
        /// </summary>
        private WriteableBitmap? TryGetCachedFrame
            (
            string CacheKey,
            int Width,
            int Height
            )
        {
            if (ViewMode == ViewMode.TwoD)
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
            if (ViewMode == ViewMode.TwoD)
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
        private double ResolveOrbitCentreLatitude
            (
            )
        {
            if (!double.IsNaN(OrbitCentreLatitude))
                return OrbitCentreLatitude;
            if (_Surface.SampleWidth > 0)
                return (_Surface.MinLat + _Surface.MaxLat) * 0.5;
            return CentreLatitude;
        }

        /// <summary>
        /// Resolves the effective 3D orbit pivot longitude.
        /// </summary>
        private double ResolveOrbitCentreLongitude
            (
            )
        {
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
            if (ContentView != FieldContentView.None)
                return ContentView;
            if ((Project != null) && (Project.HasDesign))
                return FieldContentView.Proposed;
            if ((Project != null) && (Project.HasSurvey))
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
