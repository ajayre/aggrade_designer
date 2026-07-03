using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

using SkiaSharp;

using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Import;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Diagnostics;
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
        /// <summary>Default map centre latitude when no project is loaded.</summary>
        private const double DEFAULT_CENTRE_LATITUDE = 36.0;
        /// <summary>Default map centre longitude when no project is loaded.</summary>
        private const double DEFAULT_CENTRE_LONGITUDE = -90.0;
        /// <summary>Default 2D zoom factor (metres per pixel scale).</summary>
        private const double DEFAULT_ZOOM_FACTOR = 1.0;
        /// <summary>Default 3D orbit heading in degrees.</summary>
        private const double DEFAULT_ORBIT_HEADING_DEG = 45.0;
        /// <summary>Default 3D orbit pitch in degrees.</summary>
        private const double DEFAULT_ORBIT_PITCH_DEG = 45.0;
        /// <summary>Default orbit camera distance in metres.</summary>
        private const double DEFAULT_ORBIT_DISTANCE_M = 500.0;
        /// <summary>Pointer drag scale for orbit heading changes.</summary>
        private const double ORBIT_HEADING_SENSITIVITY = 0.4;
        /// <summary>Pointer drag scale for orbit pitch changes.</summary>
        private const double ORBIT_PITCH_SENSITIVITY = 0.3;
        /// <summary>Maximum allowed orbit pitch in degrees.</summary>
        private const double MAX_ORBIT_PITCH_DEG = 89.0;
        /// <summary>2D wheel zoom multiplier when zooming in.</summary>
        private const double WHEEL_ZOOM_IN_FACTOR = 1.12;
        /// <summary>2D wheel zoom multiplier when zooming out.</summary>
        private const double WHEEL_ZOOM_OUT_FACTOR = 0.89;
        /// <summary>3D wheel zoom multiplier when moving closer.</summary>
        private const double ORBIT_WHEEL_ZOOM_IN_FACTOR = 0.9;
        /// <summary>3D wheel zoom multiplier when moving farther.</summary>
        private const double ORBIT_WHEEL_ZOOM_OUT_FACTOR = 1.1;
        /// <summary>Minimum orbit camera distance in metres.</summary>
        private const double MIN_ORBIT_DISTANCE_M = 50.0;
        /// <summary>Maximum orbit camera distance in metres.</summary>
        private const double MAX_ORBIT_DISTANCE_M = 5000.0;
        /// <summary>Approximate metres per degree of latitude for pan math.</summary>
        private const double METRES_PER_DEGREE_LATITUDE = 111320.0;
        /// <summary>Degrees in a full circle for heading wrap.</summary>
        private const double DEGREES_IN_FULL_CIRCLE = 360.0;
        /// <summary>Orbit heading that presents north toward the top of the screen.</summary>
        private const double NORTH_UP_HEADING_DEG = 180.0;
        /// <summary>Duration of view-cube orientation animation in milliseconds.</summary>
        private const int VIEW_CUBE_ANIMATION_MS = 420;
        /// <summary>Number of rotating slots in the frame cache ring buffer.</summary>
        private const int FRAME_CACHE_SLOT_COUNT = 4;
        /// <summary>Bytes per RGBA pixel when copying frame buffers.</summary>
        private const int BYTES_PER_PIXEL = 4;
        /// <summary>Minimum viewport width or height for rendering.</summary>
        private const int MIN_VIEWPORT_DIMENSION = 1;
        private const int MIN_INTERACTIVE_VIEWPORT_DIMENSION = 32;
        /// <summary>Opacity percent above which 3D uses monolithic instead of layered compositing.</summary>
        private const double TOPO_OPACITY_LAYERED_THRESHOLD_PERCENT = 99.999;
        /// <summary>Cache key tolerance for topo opacity percent changes.</summary>
        private const double TOPO_OPACITY_CACHE_TOLERANCE = 0.01;
        /// <summary>Delay before releasing stale layered 3D bitmaps after defer.</summary>
        private const int LAYERED_3D_DEFER_RELEASE_MS = 200;
        /// <summary>Fraction used when defaulting view centre to field midpoint.</summary>
        private const double FIELD_CENTRE_FRACTION = 0.5;
        /// <summary>Divisor converting opacity percent to 0–1 unit range.</summary>
        private const double OPACITY_PERCENT_TO_UNIT = 100.0;
        /// <summary>Minimum topo overlay opacity in unit range.</summary>
        private const double MIN_OPACITY_UNIT = 0.0;
        /// <summary>Opacity percent representing fully opaque topo overlay.</summary>
        private const double FULL_TOPO_OPACITY_PERCENT = 100.0;
        /// <summary>Multiplier for plinth diagonal when testing tile coverage.</summary>
        private const double PLINTH_DIAGONAL_SIDE_MULTIPLIER = 2.0;
        /// <summary>Progress percent reported during frame pixel copy.</summary>
        private const int FRAME_PIXEL_COPY_PROGRESS_PERCENT = 96;
        /// <summary>Target frames per second for view-cube animation timer.</summary>
        private const int VIEW_CUBE_ANIMATION_FPS = 60;
        /// <summary>Ease-in exponent for view-cube animation interpolation.</summary>
        private const double ANIMATION_EASE_IN_FACTOR = 1.0;
        /// <summary>Offset added before heading lerp to handle 360° wrap.</summary>
        private const double LERP_HEADING_WRAP_OFFSET_DEG = 540.0;
        /// <summary>Half circle in degrees for heading wrap normalization.</summary>
        private const double LERP_HEADING_HALF_CIRCLE_DEG = 180.0;
        /// <summary>Default vertical exaggeration when none is specified.</summary>
        private const double DEFAULT_VERTICAL_EXAGGERATION = 1.0;
        /// <summary>DPI used when creating writeable bitmaps.</summary>
        private const double BITMAP_DPI = 96.0;
        /// <summary>Progress percent indicating prepare/render complete.</summary>
        private const int PROGRESS_COMPLETE_PERCENT = 100;
        /// <summary>Progress percent at start of view prepare.</summary>
        private const int PROGRESS_PREPARE_START_PERCENT = 5;
        /// <summary>Progress percent when surface sampler build starts.</summary>
        private const int PROGRESS_SURFACE_BUILD_START_PERCENT = 25;
        /// <summary>Progress percent when surface sampler build ends.</summary>
        private const int PROGRESS_SURFACE_BUILD_END_PERCENT = 42;
        /// <summary>Prewarm start percent when surface was rebuilt.</summary>
        private const int PROGRESS_PREWARM_START_WITH_SURFACE_PERCENT = 42;
        /// <summary>Prewarm end percent when surface was rebuilt.</summary>
        private const int PROGRESS_PREWARM_END_WITH_SURFACE_PERCENT = 58;
        /// <summary>Prewarm start percent without surface rebuild.</summary>
        private const int PROGRESS_PREWARM_START_PERCENT = 25;
        /// <summary>Prewarm end percent without surface rebuild.</summary>
        private const int PROGRESS_PREWARM_END_PERCENT = 40;
        /// <summary>Frame render start percent when surface was rebuilt.</summary>
        private const int PROGRESS_FRAME_START_WITH_SURFACE_PERCENT = 58;
        /// <summary>Frame render end percent before completion.</summary>
        private const int PROGRESS_FRAME_END_PERCENT = 99;
        /// <summary>Frame render start percent without surface rebuild.</summary>
        private const int PROGRESS_FRAME_START_PERCENT = 42;
        /// <summary>Progress percent at start of initial 3D camera fit.</summary>
        private const int PROGRESS_INITIAL_FIT_PERCENT = 2;
        /// <summary>Progress percent when initial fit surface step completes.</summary>
        private const int PROGRESS_INITIAL_FIT_SURFACE_END_PERCENT = 20;
        /// <summary>Progress percent when initial 3D fit completes.</summary>
        private const int PROGRESS_INITIAL_FIT_COMPLETE_PERCENT = 22;
        /// <summary>Progressive prepare: surface build start.</summary>
        private const int PROGRESS_PROGRESSIVE_SURFACE_START_PERCENT = 5;
        /// <summary>Progressive prepare: surface build end.</summary>
        private const int PROGRESS_PROGRESSIVE_SURFACE_END_PERCENT = 40;
        /// <summary>Progressive prepare: early preview frame start.</summary>
        private const int PROGRESS_PROGRESSIVE_PREVIEW_START_PERCENT = 55;
        /// <summary>Progressive prepare: early preview frame end.</summary>
        private const int PROGRESS_PROGRESSIVE_PREVIEW_END_PERCENT = 72;
        /// <summary>Progressive prepare: prewarm start.</summary>
        private const int PROGRESS_PROGRESSIVE_PREWARM_START_PERCENT = 40;
        /// <summary>Progressive prepare: prewarm end.</summary>
        private const int PROGRESS_PROGRESSIVE_PREWARM_END_PERCENT = 55;
        /// <summary>Progressive prepare: alternate content view prewarm start.</summary>
        private const int PROGRESS_PROGRESSIVE_UPGRADE_START_PERCENT = 72;
        /// <summary>Progressive prepare: alternate content view prewarm end.</summary>
        private const int PROGRESS_PROGRESSIVE_UPGRADE_END_PERCENT = 80;
        /// <summary>Progressive prepare: active view quality upgrade start.</summary>
        private const int PROGRESS_PROGRESSIVE_QUALITY_START_PERCENT = 80;
        /// <summary>Progressive prepare: active view quality upgrade end.</summary>
        private const int PROGRESS_PROGRESSIVE_QUALITY_END_PERCENT = 90;
        /// <summary>Progressive prepare: full-quality frame start.</summary>
        private const int PROGRESS_PROGRESSIVE_FULL_START_PERCENT = 90;
        /// <summary>Progressive prepare: full-quality frame end.</summary>
        private const int PROGRESS_PROGRESSIVE_FULL_END_PERCENT = 98;
        /// <summary>Progressive prepare: nearly complete tick.</summary>
        private const int PROGRESS_PROGRESSIVE_ALMOST_DONE_PERCENT = 99;
        /// <summary>2D layered prepare: base layer end percent.</summary>
        private const int PROGRESS_2D_BASE_LAYER_END_PERCENT = 34;
        /// <summary>2D layered prepare: topo layer end percent.</summary>
        private const int PROGRESS_2D_TOPO_LAYER_END_PERCENT = 67;

        /// <summary>Sampled field surface rebuilt when the project or grid changes.</summary>
        private readonly FieldSurfaceSampler _Surface = new FieldSurfaceSampler();
        /// <summary>Satellite tile service shared across frame prepares.</summary>
        private readonly SatelliteTileService _Satellite = new SatelliteTileService();
        /// <summary>Current display toggles and opacity settings for rendering.</summary>
        private DisplaySettings _Display = new DisplaySettings();
        /// <summary>True while the user is left-dragging to pan or orbit.</summary>
        private bool _IsDragging;
        /// <summary>True while the user is right-dragging to pan the orbit pivot.</summary>
        private bool _IsRightDragging;
        /// <summary>Last pointer position in control coordinates for drag deltas.</summary>
        private Point _LastPointer;
        /// <summary>True when the surface sampler must be rebuilt before render.</summary>
        private bool _SurfaceDirty = true;
        /// <summary>Cached full 2D frame bitmaps by ring-buffer slot.</summary>
        private readonly WriteableBitmap?[] _FrameCaches2D = new WriteableBitmap?[FRAME_CACHE_SLOT_COUNT];
        /// <summary>Cached 2D frame pixel packets by ring-buffer slot.</summary>
        private readonly RenderedFramePacket?[] _FramePixelCaches2D = new RenderedFramePacket?[FRAME_CACHE_SLOT_COUNT];
        /// <summary>Cache keys for 2D full-frame slots.</summary>
        private readonly string[] _FrameCacheKeys2D = new string[FRAME_CACHE_SLOT_COUNT];
        /// <summary>Satellite cache generation per 2D frame slot.</summary>
        private readonly int[] _FrameSatelliteGenerations2D = new int[FRAME_CACHE_SLOT_COUNT];
        /// <summary>Whether all satellite tiles finished loading per 2D slot.</summary>
        private readonly bool[] _FrameSatelliteTilesComplete2D = new bool[FRAME_CACHE_SLOT_COUNT];
        /// <summary>Cached 2D topo overlay bitmaps by slot.</summary>
        private readonly WriteableBitmap?[] _FrameTopoCaches2D = new WriteableBitmap?[FRAME_CACHE_SLOT_COUNT];
        /// <summary>Cached 2D topo overlay pixel packets by slot.</summary>
        private readonly RenderedFramePacket?[] _FrameTopoPixelCaches2D = new RenderedFramePacket?[FRAME_CACHE_SLOT_COUNT];
        /// <summary>Cached 2D decoration overlay bitmaps by slot.</summary>
        private readonly WriteableBitmap?[] _FrameDecorationsCaches2D = new WriteableBitmap?[FRAME_CACHE_SLOT_COUNT];
        /// <summary>Cached 2D decoration pixel packets by slot.</summary>
        private readonly RenderedFramePacket?[] _FrameDecorationsPixelCaches2D = new RenderedFramePacket?[FRAME_CACHE_SLOT_COUNT];
        /// <summary>Cached 3D base layer bitmaps by slot.</summary>
        private readonly WriteableBitmap?[] _FrameCaches3D = new WriteableBitmap?[FRAME_CACHE_SLOT_COUNT];
        /// <summary>Cached 3D base layer pixel packets by slot.</summary>
        private readonly RenderedFramePacket?[] _FramePixelCaches3D = new RenderedFramePacket?[FRAME_CACHE_SLOT_COUNT];
        /// <summary>Cache keys for 3D base-layer slots.</summary>
        private readonly string[] _FrameCacheKeys3D = new string[FRAME_CACHE_SLOT_COUNT];
        /// <summary>Satellite cache generation per 3D frame slot.</summary>
        private readonly int[] _FrameSatelliteGenerations3D = new int[FRAME_CACHE_SLOT_COUNT];
        /// <summary>Whether all satellite tiles finished loading per 3D slot.</summary>
        private readonly bool[] _FrameSatelliteTilesComplete3D = new bool[FRAME_CACHE_SLOT_COUNT];
        /// <summary>Cached 3D topo overlay bitmaps by slot.</summary>
        private readonly WriteableBitmap?[] _FrameTopoCaches3D = new WriteableBitmap?[FRAME_CACHE_SLOT_COUNT];
        /// <summary>Cached 3D topo overlay pixel packets by slot.</summary>
        private readonly RenderedFramePacket?[] _FrameTopoPixelCaches3D = new RenderedFramePacket?[FRAME_CACHE_SLOT_COUNT];
        /// <summary>Cached 3D decoration overlay bitmaps by slot.</summary>
        private readonly WriteableBitmap?[] _FrameDecorationsCaches3D = new WriteableBitmap?[FRAME_CACHE_SLOT_COUNT];
        /// <summary>Cached 3D decoration pixel packets by slot.</summary>
        private readonly RenderedFramePacket?[] _FrameDecorationsPixelCaches3D = new RenderedFramePacket?[FRAME_CACHE_SLOT_COUNT];
        /// <summary>Cached monolithic 3D frame bitmaps by slot.</summary>
        private readonly WriteableBitmap?[] _FrameMonolithicCaches3D = new WriteableBitmap?[FRAME_CACHE_SLOT_COUNT];
        /// <summary>Cached monolithic 3D pixel packets by slot.</summary>
        private readonly RenderedFramePacket?[] _FrameMonolithicPixelCaches3D = new RenderedFramePacket?[FRAME_CACHE_SLOT_COUNT];
        /// <summary>Incremented to cancel in-flight background prewarm work.</summary>
        /// <summary>Coordinates pause/resume and invalidation for background prewarm workers.</summary>
        private readonly BackgroundPrewarmCoordinator _BackgroundPrewarm = new BackgroundPrewarmCoordinator();
        /// <summary>Generation counter for interactive 3D render invalidation.</summary>
        private int _Interactive3DRenderGeneration;
        /// <summary>Cache key of the 3D frame currently rendering on a worker.</summary>
        private string _InFlightInteractive3DCacheKey = "";
        /// <summary>Cache key of the last successfully presented 3D frame.</summary>
        private string _LastSuccessful3DCacheKey = "";
        /// <summary>When true, layered 3D rebuild is deferred until orbit ends.</summary>
        private bool _DeferLayered3DRebuild;
        /// <summary>Test hook: import handoff completed and first render should be observed.</summary>
        private bool _ImportHandoffCompleteForTests;
        /// <summary>When true, pointer and wheel input are ignored during AGD import handoff.</summary>
        private bool _ImportInputBlocked;
        /// <summary>When true, background prewarm waits for the first composite draw after handoff.</summary>
        private bool _DeferBackgroundPrewarmUntilFirstCompositeDraw;
        /// <summary>Test hook: synchronous frame builds during instrumented render.</summary>
        internal int TestSyncFrameBuildCount { get; private set; }
        /// <summary>Test hook: synchronous surface rebuilds during instrumented render.</summary>
        internal int TestSurfaceRebuildCount { get; private set; }
        /// <summary>Test hook: first render after import handoff was observed.</summary>
        internal bool TestFirstRenderAfterHandoffObserved { get; private set; }
        /// <summary>Timer that releases stale layered 3D bitmaps after defer.</summary>
        private Avalonia.Threading.DispatcherTimer? _Layered3DDeferReleaseTimer;
        /// <summary>Bitmap currently presented on the control surface.</summary>
        private WriteableBitmap? _PresentedFrame;
        /// <summary>Previous monolithic 3D frame kept during layered transition.</summary>
        private WriteableBitmap? _Stale3DMonolithicFrame;
        /// <summary>Previous 3D base layer kept during layered transition.</summary>
        private WriteableBitmap? _Stale3DBaseFrame;
        /// <summary>Previous 3D topo layer kept during layered transition.</summary>
        private WriteableBitmap? _Stale3DTopoFrame;
        /// <summary>Previous 3D decorations layer kept during layered transition.</summary>
        private WriteableBitmap? _Stale3DDecorationsFrame;
        /// <summary>Serializes synchronous and background view prepare paths.</summary>
        private readonly object _PrepareLock = new object();

        /// <summary>Count of timed-out prepare lock waits observed on the UI thread (test instrumentation).</summary>
        internal int TestPrepareLockUiTimeoutCount { get; private set; }

        /// <summary>
        /// Attempts to enter the prepare lock on the UI thread without blocking indefinitely.
        /// </summary>
        private bool TryEnterPrepareLockOnUi
            (
            int TimeoutMs,
            [CallerMemberName] string Caller = ""
            )
        {
            bool Entered = ViewerPrepareLock.TryEnterForUi(_PrepareLock, TimeoutMs, Caller);
            if (!Entered)
                TestPrepareLockUiTimeoutCount++;

            return Entered;
        }

        /// <summary>
        /// Attempts to acquire the prepare lock for interactive UI rendering.
        /// </summary>
        private bool TryEnterPrepareLockForInteractiveRender
            (
            )
        {
            return TryEnterPrepareLockOnUi(ViewerPrepareLock.UiRenderAcquireTimeoutMs);
        }

        /// <summary>
        /// Attempts to acquire the prepare lock on a worker thread, preempting background prewarm when interactive.
        /// </summary>
        private bool TryEnterPrepareLockForWorker
            (
            int? BackgroundPrewarmGeneration,
            string Reason,
            int MaxWaitMs = 30000
            )
        {
            const int SliceMs = 50;
            Stopwatch Wait = Stopwatch.StartNew();
            while (Wait.ElapsedMilliseconds < MaxWaitMs)
            {
                if (Monitor.TryEnter(_PrepareLock, SliceMs))
                    return true;

                if (BackgroundPrewarmGeneration.HasValue)
                {
                    if (BackgroundPrewarmGeneration.Value != _BackgroundPrewarm.Generation)
                        return false;

                    if (!WaitAtBackgroundPrewarmCheckpoint(BackgroundPrewarmGeneration.Value, $"{Reason}-wait"))
                        return false;
                }
                else
                {
                    CancelBackgroundPrewarm();
                    ImportDiagnosticsLog.Write("PrepareLock", "WorkerPreempt", Reason);
                }
            }

            ImportDiagnosticsLog.Write("PrepareLock", "WorkerWaitTimeout", $"{Reason} timeout={MaxWaitMs}ms");
            return false;
        }

        /// <summary>Active view-cube hover target for highlight and click.</summary>
        private ViewCubeHit _ViewCubeHover = ViewCubeHit.None;
        /// <summary>Timer driving animated view-cube orientation transitions.</summary>
        private Avalonia.Threading.DispatcherTimer? _ViewCubeAnimTimer;
        /// <summary>Heading at the start of view-cube animation.</summary>
        private double _ViewCubeAnimStartHeadingDeg;
        /// <summary>Pitch at the start of view-cube animation.</summary>
        private double _ViewCubeAnimStartPitchDeg;
        /// <summary>Target heading for view-cube animation.</summary>
        private double _ViewCubeAnimTargetHeadingDeg;
        /// <summary>Target pitch for view-cube animation.</summary>
        private double _ViewCubeAnimTargetPitchDeg;
        /// <summary>Stopwatch ticks when view-cube animation started.</summary>
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
        /// Styled property backing <see cref="IsPresentationActive"/>.
        /// </summary>
        static public readonly StyledProperty<bool> IsPresentationActiveProperty =
            AvaloniaProperty.Register<FieldViewerControl, bool>(nameof(IsPresentationActive), true);

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
                SuppressSynchronousPrepareProperty,
                IsPresentationActiveProperty);
            SuppressSynchronousPrepareProperty.Changed.AddClassHandler<FieldViewerControl>((Control, Args) =>
            {
                if (!ImportDiagnosticsLog.ShouldLog())
                    return;

                bool NewValue = Args.NewValue is bool Value ? Value : false;
                ImportDiagnosticsLog.Write(
                    "Viewer",
                    "SuppressSynchronousPrepare",
                    $"{NewValue} arranged={Control._LastArrangedSize.Width:0}x{Control._LastArrangedSize.Height:0}");
            });
            BoundsProperty.Changed.AddClassHandler<FieldViewerControl>((Control, Args) =>
            {
                if (!ImportDiagnosticsLog.ShouldLog())
                    return;

                (int Width, int Height) Dimensions = Control.GetRenderDimensions();
                ImportDiagnosticsLog.WriteViewport(
                    "ViewerBounds",
                    Dimensions.Width,
                    Dimensions.Height,
                    $"bounds={Control.Bounds.Width:0}x{Control.Bounds.Height:0}");
            });
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
        /// Logs detailed import handoff cache state for diagnostic sessions.
        /// </summary>
        internal void WriteImportDiagnostics
            (
            string Phase
            )
        {
            if (!ImportDiagnosticsLog.ShouldLog())
                return;

            ImportHandoffDiagnosticSnapshot Diagnostics = TestCollectImportHandoffDiagnostics();
            ImportDiagnosticsLog.Write(
                "HandoffState",
                Phase,
                $"vp={Diagnostics.ViewportWidth}x{Diagnostics.ViewportHeight} mode={ViewMode} content={ResolveContentView()} " +
                $"keyMatch={Diagnostics.CacheKeysMatch} materialized={Diagnostics.HasMaterializedComposite} " +
                $"baseBmp={Diagnostics.HasBaseBitmap} topoBmp={Diagnostics.HasTopoBitmap} decBmp={Diagnostics.HasDecorationsBitmap} " +
                $"basePkt={Diagnostics.HasBasePacket} topoPkt={Diagnostics.HasTopoPacket} decPkt={Diagnostics.HasDecorationsPacket} " +
                $"surfaceDirty={Diagnostics.SurfaceDirty} suppress={Diagnostics.SuppressSynchronousPrepare} " +
                $"curKey={Diagnostics.CurrentCacheKey} storedKey={Diagnostics.StoredProposedCacheKey}");
        }

        /// <summary>
        /// Logs viewer bounds changes triggered from the main window layout.
        /// </summary>
        internal void NotifyDiagnosticsBoundsChanged
            (
            string Label
            )
        {
            if (!ImportDiagnosticsLog.ShouldLog())
                return;

            (int Width, int Height) Dimensions = GetRenderDimensions();
            ImportDiagnosticsLog.WriteViewport(Label, Dimensions.Width, Dimensions.Height, $"bounds={Bounds.Width:0}x{Bounds.Height:0}");
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
                // New tiles may have arrived after a partial basemap was cached — drop base slots only.
                InvalidateSatelliteBaseFrameCaches();
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
        /// When false, the viewer is hidden and must not run synchronous surface or frame rebuilds.
        /// </summary>
        public bool IsPresentationActive
        {
            get => GetValue(IsPresentationActiveProperty);
            set => SetValue(IsPresentationActiveProperty, value);
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
            return GetRenderDimensions();
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
        /// Returns true when all composite layer bitmaps are materialized for the current view state.
        /// </summary>
        public bool HasMaterializedCompositeFrameForCurrentView
            (
            )
        {
            if ((Project == null) || !Project.HasSurvey)
                return false;

            (int Width, int Height) Dimensions = GetRenderDimensions();
            if ((Width <= 0) || (Height <= 0))
                return false;

            RenderSnapshot Snapshot = CaptureRenderSnapshot(null);
            FieldContentView ContentView = ResolveContentView();
            string CacheKey = BuildFrameCacheKey(Dimensions.Width, Dimensions.Height, ViewMode, Snapshot);
            if (ViewMode == ViewMode.ThreeD)
            {
                return TryGetMaterializedOverlayBitmap(
                    ViewMode,
                    ContentView,
                    CacheKey,
                    Dimensions.Width,
                    Dimensions.Height,
                    _FrameMonolithicCaches3D) != null;
            }

            return TryGetMaterializedOverlayBitmap(
                ViewMode,
                ContentView,
                CacheKey,
                Dimensions.Width,
                Dimensions.Height,
                _FrameCaches2D) != null
                && TryGetMaterializedOverlayBitmap(
                    ViewMode,
                    ContentView,
                    CacheKey,
                    Dimensions.Width,
                    Dimensions.Height,
                    _FrameTopoCaches2D) != null
                && TryGetMaterializedOverlayBitmap(
                    ViewMode,
                    ContentView,
                    CacheKey,
                    Dimensions.Width,
                    Dimensions.Height,
                    _FrameDecorationsCaches2D) != null;
        }

        /// <summary>
        /// When true, pointer and wheel input are ignored to keep cache keys stable during import.
        /// </summary>
        public bool IsImportInputBlocked => _ImportInputBlocked;

        /// <summary>
        /// Blocks or unblocks viewer pointer and wheel input during AGD import handoff.
        /// </summary>
        public void SetImportInputBlocked
            (
            bool Blocked
            )
        {
            if (_ImportInputBlocked == Blocked)
                return;

            _ImportInputBlocked = Blocked;
            ImportDiagnosticsLog.Write(
                "ImportInput",
                Blocked ? "BLOCKED" : "UNBLOCKED",
                $"viewport={GetRenderDimensions().Width}x{GetRenderDimensions().Height}");
        }

        /// <summary>
        /// Materializes cached pixel layers at the live view and reports whether all layers are drawable.
        /// </summary>
        public bool TryMaterializeCompositeForCurrentView
            (
            )
        {
            if ((Project == null) || !Project.HasSurvey)
                return false;

            ImportDiagnosticsLog.Write("Materialize", "START", $"suppress={SuppressSynchronousPrepare}");
            MaterializeCachedLayerBitmapsForCurrentView();
            bool Presented = TryPresentCachedFrame();
            bool Materialized = HasMaterializedCompositeFrameForCurrentView();
            ImportDiagnosticsLog.Write(
                "Materialize",
                Materialized ? "READY" : "INCOMPLETE",
                $"presented={Presented} viewport={GetRenderDimensions().Width}x{GetRenderDimensions().Height}");
            return Materialized;
        }

        /// <summary>
        /// Defers background prewarm until the first composite draw after import handoff completes.
        /// </summary>
        public void MarkImportHandoffPendingFirstCompositeDraw
            (
            )
        {
            _DeferBackgroundPrewarmUntilFirstCompositeDraw = true;
            ImportDiagnosticsLog.Write("Import", "DeferPrewarm", "until first composite draw");
        }

        /// <summary>
        /// Marks the surface sampler clean after import prepare so Render does not sync-rebuild it.
        /// </summary>
        public void MarkImportPrepareComplete
            (
            )
        {
            _SurfaceDirty = false;
        }

        /// <summary>
        /// Resets import handoff instrumentation counters used by regression tests.
        /// </summary>
        internal void TestResetImportInstrumentation
            (
            )
        {
            _ImportHandoffCompleteForTests = false;
            TestSyncFrameBuildCount = 0;
            TestSurfaceRebuildCount = 0;
            TestFirstRenderAfterHandoffObserved = false;
            TestPrepareLockUiTimeoutCount = 0;
        }

        /// <summary>
        /// Pauses background prewarm without invalidating completed cache entries.
        /// </summary>
        internal void TestRequestBackgroundPrewarmPause
            (
            string Reason
            )
        {
            _BackgroundPrewarm.RequestPause(Reason);
        }

        /// <summary>
        /// Resumes background prewarm after a test pause request.
        /// </summary>
        internal void TestReleaseBackgroundPrewarmPause
            (
            string Reason
            )
        {
            _BackgroundPrewarm.ReleasePause(Reason);
        }

        /// <summary>
        /// Returns the current background prewarm generation for regression tests.
        /// </summary>
        internal int TestBackgroundPrewarmGeneration => _BackgroundPrewarm.Generation;

        /// <summary>
        /// Holds the prepare lock on a worker thread for contention regression tests.
        /// </summary>
        internal Task TestHoldPrepareLockAsync
            (
            int HoldMs
            )
        {
            return Task.Run(() =>
            {
                lock (_PrepareLock)
                {
                    Thread.Sleep(HoldMs);
                }
            });
        }

        /// <summary>
        /// Schedules background prewarm while holding the prepare lock to reproduce UI contention.
        /// </summary>
        internal void TestScheduleLockedBackgroundPrewarm
            (
            int HoldMs
            )
        {
            RenderSnapshot Snapshot = CaptureRenderSnapshot(null);
            if ((Snapshot.Width <= 0) || (Snapshot.Height <= 0) || (Snapshot.Project == null) || !Snapshot.Project.HasSurvey)
                return;

            int PrewarmVersion = _BackgroundPrewarm.Generation;
            DisplaySettings Display = Snapshot.Display ?? _Display;
            _ = Task.Run(() =>
            {
                if (PrewarmVersion != _BackgroundPrewarm.Generation)
                    return;

                lock (_PrepareLock)
                {
                    _Surface.PrewarmDisplayCaches(
                        FieldContentView.CutFill,
                        Display,
                        null,
                        IncludeContours: Display.Contours);
                    Thread.Sleep(HoldMs);
                }
            });
        }

        /// <summary>
        /// Builds and caches all frame layers for one content view on the UI thread (tests only).
        /// </summary>
        internal void TestBuildAndCacheContentView
            (
            FieldContentView ContentView
            )
        {
            RenderSnapshot Snapshot = CaptureRenderSnapshot(null);
            RenderSnapshot ViewSnapshot = CloneSnapshotForContentView(Snapshot, ContentView);
            lock (_PrepareLock)
            {
                if (ViewSnapshot.Display != null)
                {
                    _Surface.PrewarmDisplayCaches(
                        ResolveContentView(ViewSnapshot.Project, ContentView),
                        ViewSnapshot.Display,
                        null,
                        IncludeContours: ViewSnapshot.Display.Contours);
                }

                BuildAndStoreFrame(ViewSnapshot, null);
            }
        }

        /// <summary>
        /// Returns a copy of the cached topo packet bytes for one content view (tests only).
        /// </summary>
        internal byte[]? TestCopyTopoPacketBytes
            (
            FieldContentView ContentView
            )
        {
            int Index = ContentViewCacheIndex(ContentView);
            if (Index < 0)
                return null;

            lock (_PrepareLock)
            {
                if (_FrameTopoPixelCaches2D[Index] is not RenderedFramePacket Packet)
                    return null;

                byte[] Copy = new byte[Packet.Pixels.Length];
                Buffer.BlockCopy(Packet.Pixels, 0, Copy, 0, Packet.Pixels.Length);
                return Copy;
            }
        }

        /// <summary>
        /// Builds one content view without holding the prepare lock (tests concurrent corruption only).
        /// </summary>
        internal void TestBuildContentViewOutsidePrepareLock
            (
            FieldContentView ContentView
            )
        {
            RenderSnapshot Snapshot = CaptureRenderSnapshot(null);
            RenderSnapshot ViewSnapshot = CloneSnapshotForContentView(Snapshot, ContentView);
            if (ViewSnapshot.Display != null)
            {
                _Surface.PrewarmDisplayCaches(
                    ResolveContentView(ViewSnapshot.Project, ContentView),
                    ViewSnapshot.Display,
                    null,
                    IncludeContours: ViewSnapshot.Display.Contours);
            }

            BuildAndStoreFrame(ViewSnapshot, null);
        }

        /// <summary>
        /// Marks import handoff complete so the next render is treated as the post-import frame.
        /// </summary>
        internal void TestMarkImportHandoffComplete
            (
            )
        {
            _ImportHandoffCompleteForTests = true;
        }

        /// <summary>
        /// Completes import handoff using the same contract as the main window view model.
        /// </summary>
        internal bool TestCompleteImportHandoff
            (
            )
        {
            bool HandoffComplete = ImportViewerHandoff.CompleteImport(
                ImportViewerHandoffTelemetry.Recorder,
                HasMaterializedCompositeFrameForCurrentView,
                () => TryMaterializeCompositeForCurrentView(),
                MarkImportPrepareComplete,
                () => SuppressSynchronousPrepare = false);

            if (HandoffComplete)
            {
                MarkImportHandoffPendingFirstCompositeDraw();
                _ImportHandoffCompleteForTests = true;
            }

            return HandoffComplete;
        }

        /// <summary>
        /// Simulates the first composite draw after handoff for harness prewarm scheduling.
        /// </summary>
        internal void TestNotifyFirstCompositeDrawAfterHandoff
            (
            )
        {
            MaybeScheduleDeferredBackgroundPrewarm();
        }

        /// <summary>
        /// Snapshot of cache and layer state for import handoff regression diagnostics.
        /// </summary>
        internal readonly struct ImportHandoffDiagnosticSnapshot
        {
            /// <summary>Live viewport width in pixels.</summary>
            public int ViewportWidth { get; init; }

            /// <summary>Live viewport height in pixels.</summary>
            public int ViewportHeight { get; init; }

            /// <summary>Cache key for the current view state.</summary>
            public string CurrentCacheKey { get; init; }

            /// <summary>Stored proposed-view cache key from the last prepare.</summary>
            public string StoredProposedCacheKey { get; init; }

            /// <summary>Whether stored and current cache keys match.</summary>
            public bool CacheKeysMatch { get; init; }

            /// <summary>Whether all composite layer bitmaps are materialized.</summary>
            public bool HasMaterializedComposite { get; init; }

            /// <summary>Whether the base layer bitmap is materialized.</summary>
            public bool HasBaseBitmap { get; init; }

            /// <summary>Whether the topo layer bitmap is materialized.</summary>
            public bool HasTopoBitmap { get; init; }

            /// <summary>Whether the decorations layer bitmap is materialized.</summary>
            public bool HasDecorationsBitmap { get; init; }

            /// <summary>Whether the base layer pixel packet exists.</summary>
            public bool HasBasePacket { get; init; }

            /// <summary>Whether the topo layer pixel packet exists.</summary>
            public bool HasTopoPacket { get; init; }

            /// <summary>Whether the decorations layer pixel packet exists.</summary>
            public bool HasDecorationsPacket { get; init; }

            /// <summary>Whether the surface sampler is marked dirty.</summary>
            public bool SurfaceDirty { get; init; }

            /// <summary>Whether synchronous prepare fallback is suppressed.</summary>
            public bool SuppressSynchronousPrepare { get; init; }
        }

        /// <summary>
        /// Captures cache and layer state for import handoff regression diagnostics.
        /// </summary>
        internal ImportHandoffDiagnosticSnapshot TestCollectImportHandoffDiagnostics
            (
            )
        {
            (int Width, int Height) Dimensions = GetRenderDimensions();
            RenderSnapshot Snapshot = CaptureRenderSnapshot(null);
            FieldContentView ContentView = ResolveContentView();
            string CurrentCacheKey = BuildFrameCacheKey(Dimensions.Width, Dimensions.Height, ViewMode, Snapshot);
            int ProposedIndex = ContentViewCacheIndex(FieldContentView.Proposed);

            string? StoredCacheKey = null;
            bool HasBaseBitmap = false;
            bool HasTopoBitmap = false;
            bool HasDecorationsBitmap = false;
            bool HasBasePacket = false;
            bool HasTopoPacket = false;
            bool HasDecorationsPacket = false;

            if (ProposedIndex >= 0)
            {
                if (TryEnterPrepareLockOnUi(ViewerPrepareLock.UiAcquireTimeoutMs))
                {
                    try
                    {
                        StoredCacheKey = _FrameCacheKeys2D[ProposedIndex];
                        HasBasePacket = HasCachedLayerPacket(_FramePixelCaches2D[ProposedIndex], Dimensions.Width, Dimensions.Height);
                        HasTopoPacket = HasCachedLayerPacket(_FrameTopoPixelCaches2D[ProposedIndex], Dimensions.Width, Dimensions.Height);
                        HasDecorationsPacket = HasCachedLayerPacket(_FrameDecorationsPixelCaches2D[ProposedIndex], Dimensions.Width, Dimensions.Height);
                        WriteableBitmap? BaseBitmap = _FrameCaches2D[ProposedIndex];
                        WriteableBitmap? TopoBitmap = _FrameTopoCaches2D[ProposedIndex];
                        WriteableBitmap? DecorationsBitmap = _FrameDecorationsCaches2D[ProposedIndex];
                        HasBaseBitmap = BaseBitmap != null &&
                            BaseBitmap.PixelSize.Width == Dimensions.Width &&
                            BaseBitmap.PixelSize.Height == Dimensions.Height;
                        HasTopoBitmap = TopoBitmap != null &&
                            TopoBitmap.PixelSize.Width == Dimensions.Width &&
                            TopoBitmap.PixelSize.Height == Dimensions.Height;
                        HasDecorationsBitmap = DecorationsBitmap != null &&
                            DecorationsBitmap.PixelSize.Width == Dimensions.Width &&
                            DecorationsBitmap.PixelSize.Height == Dimensions.Height;
                    }
                    finally
                    {
                        Monitor.Exit(_PrepareLock);
                    }
                }
            }

            return new ImportHandoffDiagnosticSnapshot
            {
                ViewportWidth = Dimensions.Width,
                ViewportHeight = Dimensions.Height,
                CurrentCacheKey = CurrentCacheKey,
                StoredProposedCacheKey = StoredCacheKey ?? "",
                CacheKeysMatch = string.Equals(StoredCacheKey, CurrentCacheKey, StringComparison.Ordinal),
                HasMaterializedComposite = HasMaterializedCompositeFrameForCurrentView(),
                HasBaseBitmap = HasBaseBitmap,
                HasTopoBitmap = HasTopoBitmap,
                HasDecorationsBitmap = HasDecorationsBitmap,
                HasBasePacket = HasBasePacket,
                HasTopoPacket = HasTopoPacket,
                HasDecorationsPacket = HasDecorationsPacket,
                SurfaceDirty = _SurfaceDirty,
                SuppressSynchronousPrepare = SuppressSynchronousPrepare
            };
        }

        /// <summary>
        /// Exercises the same post-handoff render decisions as <see cref="Render"/> without Avalonia drawing.
        /// </summary>
        internal bool TestSimulatePostHandoffRender
            (
            )
        {
            if ((Project == null) || !Project.HasSurvey)
                return false;

            (int Width, int Height) Dimensions = GetRenderDimensions();
            if ((Dimensions.Width <= 0) || (Dimensions.Height <= 0))
                return false;

            bool Suppress = SuppressSynchronousPrepare;
            if (_SurfaceDirty && !Suppress)
            {
                _Surface.BuildFromProject(Project);
                _SurfaceDirty = false;
                TestSurfaceRebuildCount++;
            }

            if (DisplaySettings != null)
                _Display = DisplaySettings;

            RenderSnapshot Snapshot = CaptureRenderSnapshot(ViewMode);
            string CacheKey = BuildFrameCacheKey(Dimensions.Width, Dimensions.Height, ViewMode, Snapshot);
            FieldContentView ContentView = ResolveContentView();

            if (HasMaterializedCompositeFrameForCurrentView())
            {
                TestRecordFirstRenderAfterHandoff(UsedSyncFallback: false);
                return false;
            }

            if (Suppress)
                return false;

            using (_BackgroundPrewarm.BeginUiPauseScope("test-sync-render"))
            {
                if (!TryEnterPrepareLockForInteractiveRender())
                    return false;

                try
                {
                    BuildAndStoreFrame(Snapshot);
                }
                finally
                {
                    Monitor.Exit(_PrepareLock);
                }
            }

            TestSyncFrameBuildCount++;
            ImportViewerHandoffTelemetry.Recorder.Record(
                _ImportHandoffCompleteForTests
                    ? ImportViewerHandoffEventKind.SyncFrameBuildAfterHandoff
                    : ImportViewerHandoffEventKind.SyncFrameBuildDuringSuppress);
            TestRecordFirstRenderAfterHandoff(UsedSyncFallback: true);
            return true;
        }

        /// <summary>
        /// Discards the cached frame bitmap for one view mode.
        /// </summary>
        public void InvalidateFrameCacheForViewMode
            (
            ViewMode Mode
            )
        {
            InvalidateFrameCacheForMode(Mode);
        }

        /// <summary>
        /// Discards the cached frame bitmap for one view mode.
        /// </summary>
        private void InvalidateFrameCacheForMode
            (
            ViewMode Mode
            )
        {
            // Cancel in-flight opposite-mode prewarm so it cannot repopulate cleared slots.
            InvalidateBackgroundPrewarm("frame-cache-mode-invalidate");
            if (Mode == ViewMode.TwoD)
            {
                ReleaseFrameCacheArray(_FrameCaches2D, _FramePixelCaches2D, _FrameCacheKeys2D, _FrameSatelliteGenerations2D, _FrameSatelliteTilesComplete2D);
                ReleaseTopoFrameCacheArray(_FrameTopoCaches2D, _FrameTopoPixelCaches2D);
                ReleaseTopoFrameCacheArray(_FrameDecorationsCaches2D, _FrameDecorationsPixelCaches2D);
            }
            else
            {
                ReleaseFrameCacheArray(_FrameCaches3D, _FramePixelCaches3D, _FrameCacheKeys3D, _FrameSatelliteGenerations3D, _FrameSatelliteTilesComplete3D);
                ReleaseTopoFrameCacheArray(_FrameTopoCaches3D, _FrameTopoPixelCaches3D);
                ReleaseTopoFrameCacheArray(_FrameDecorationsCaches3D, _FrameDecorationsPixelCaches3D);
                ReleaseTopoFrameCacheArray(_FrameMonolithicCaches3D, _FrameMonolithicPixelCaches3D);
                _LastSuccessful3DCacheKey = "";
                Interlocked.Increment(ref _Interactive3DRenderGeneration);
            }
        }

        /// <summary>
        /// Returns true when 3D topo opacity requires separate surface and decoration layers.
        /// </summary>
        private static bool UsesLayered3DOpacity
            (
            DisplaySettings Display
            )
        {
            return Display.TopoCutFillOpacityPercent < TOPO_OPACITY_LAYERED_THRESHOLD_PERCENT;
        }

        /// <summary>
        /// Returns true when 3D frames should use the single-pass monolithic renderer.
        /// </summary>
        private bool ShouldUseMonolithic3DPath
            (
            DisplaySettings? DisplaySource = null
            )
        {
            DisplaySettings Display = DisplaySource ?? _Display;
            return !UsesLayered3DOpacity(Display) || _DeferLayered3DRebuild;
        }

        /// <summary>
        /// Uses the fast monolithic path while the 3D camera is changing at partial opacity.
        /// </summary>
        private void BeginLayered3DInteractionDefer
            (
            )
        {
            if ((ViewMode != ViewMode.ThreeD) || !UsesLayered3DOpacity(_Display))
                return;

            _DeferLayered3DRebuild = true;
        }

        /// <summary>
        /// Rebuilds accurate opacity layers once 3D camera interaction settles.
        /// </summary>
        private void EndLayered3DInteractionDefer
            (
            )
        {
            if (!_DeferLayered3DRebuild)
                return;

            _DeferLayered3DRebuild = false;
            InvalidateRenderedFrame();
        }

        /// <summary>
        /// Restarts the timer that ends partial-opacity defer after wheel zoom steps.
        /// </summary>
        private void ScheduleLayered3DDeferRelease
            (
            )
        {
            if ((ViewMode != ViewMode.ThreeD) || !UsesLayered3DOpacity(_Display))
                return;

            BeginLayered3DInteractionDefer();
            if (_Layered3DDeferReleaseTimer == null)
            {
                _Layered3DDeferReleaseTimer = new Avalonia.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(LAYERED_3D_DEFER_RELEASE_MS)
                };
                _Layered3DDeferReleaseTimer.Tick += (_, _) =>
                {
                    _Layered3DDeferReleaseTimer?.Stop();
                    EndLayered3DInteractionDefer();
                };
            }

            _Layered3DDeferReleaseTimer.Stop();
            _Layered3DDeferReleaseTimer.Start();
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

            bool TilesComplete = Mode == ViewMode.TwoD
                ? _FrameSatelliteTilesComplete2D[ContentViewIndex]
                : _FrameSatelliteTilesComplete3D[ContentViewIndex];
            if (!TilesComplete)
                return false;

            int StoredGeneration = Mode == ViewMode.TwoD
                ? _FrameSatelliteGenerations2D[ContentViewIndex]
                : _FrameSatelliteGenerations3D[ContentViewIndex];
            int CurrentGeneration = _Satellite.CacheGeneration;

            // Frames rendered before any tile finished loading are not cache-valid.
            if (CurrentGeneration <= 0 && StoredGeneration <= 0)
                return false;

            return StoredGeneration >= CurrentGeneration;
        }

        /// <summary>
        /// Runs an action on the UI thread, inlining when already on it.
        /// </summary>
        private static async Task RunOnUiThreadAsync
            (
            Action Action
            )
        {
            if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
                Action();
            else
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(Action);
        }

        /// <summary>
        /// Invalidates in-flight background prewarm after cache or project changes.
        /// </summary>
        private void InvalidateBackgroundPrewarm
            (
            string Reason
            )
        {
            _BackgroundPrewarm.Invalidate(Reason);
        }

        /// <summary>
        /// Waits at a prewarm checkpoint when UI interaction has requested a pause.
        /// </summary>
        private bool WaitAtBackgroundPrewarmCheckpoint
            (
            int WorkerGeneration,
            string Checkpoint
            )
        {
            return _BackgroundPrewarm.WaitAtCheckpoint(WorkerGeneration, Checkpoint);
        }

        /// <summary>
        /// Releases the prepare lock at layer boundaries so UI sync builds can proceed during prewarm.
        /// Caller must hold <see cref="_PrepareLock"/> on entry; lock is re-acquired before return.
        /// </summary>
        private bool YieldPrepareLockForBackgroundPrewarm
            (
            int? BackgroundPrewarmGeneration,
            string Checkpoint
            )
        {
            if (!BackgroundPrewarmGeneration.HasValue)
                return true;

            int Generation = BackgroundPrewarmGeneration.Value;
            Monitor.Exit(_PrepareLock);
            try
            {
                return WaitAtBackgroundPrewarmCheckpoint(Generation, Checkpoint);
            }
            finally
            {
                Monitor.Enter(_PrepareLock);
            }
        }

        /// <summary>
        /// Bumps the background prewarm generation so in-flight workers exit promptly.
        /// </summary>
        private void CancelBackgroundPrewarm
            (
            )
        {
            InvalidateBackgroundPrewarm("legacy-cancel");
        }

        /// <summary>
        /// Returns true when a matching frame exists in the pixel or bitmap cache.
        /// Does not materialize Avalonia bitmaps; safe from worker threads.
        /// </summary>
        private bool HasCachedFrame
            (
            ViewMode Mode,
            FieldContentView ContentView,
            string CacheKey,
            int Width,
            int Height,
            DisplaySettings? DisplaySource
            )
        {
            int Index = ContentViewCacheIndex(ContentView);
            if (Index < 0)
                return false;

            bool IsUiThread = Avalonia.Threading.Dispatcher.UIThread.CheckAccess();
            if (IsUiThread && !TryEnterPrepareLockOnUi(ViewerPrepareLock.UiAcquireTimeoutMs))
                return false;

            if (!IsUiThread)
                Monitor.Enter(_PrepareLock);

            try
            {
                string[] Keys = Mode == ViewMode.TwoD ? _FrameCacheKeys2D : _FrameCacheKeys3D;
                RenderedFramePacket?[] PixelCaches = Mode == ViewMode.TwoD ? _FramePixelCaches2D : _FramePixelCaches3D;
                RenderedFramePacket?[] TopoPixelCaches = Mode == ViewMode.TwoD ? _FrameTopoPixelCaches2D : _FrameTopoPixelCaches3D;
                RenderedFramePacket?[] DecorationsPixelCaches = Mode == ViewMode.TwoD ? _FrameDecorationsPixelCaches2D : _FrameDecorationsPixelCaches3D;
                WriteableBitmap?[] Caches = Mode == ViewMode.TwoD ? _FrameCaches2D : _FrameCaches3D;
                DisplaySettings Display = DisplaySource ?? _Display;
                if (Keys[Index] != CacheKey ||
                    !IsSatelliteFrameCacheValid(Mode, Index, Display.Satellite))
                {
                    return false;
                }

                if (Mode == ViewMode.ThreeD)
                {
                    if (_FrameMonolithicPixelCaches3D[Index] is RenderedFramePacket Packet &&
                        Math.Abs(Packet.TopoOverlayOpacityPercent - Display.TopoCutFillOpacityPercent) > TOPO_OPACITY_CACHE_TOLERANCE)
                    {
                        return false;
                    }

                    return HasCachedLayerPacket(_FrameMonolithicPixelCaches3D[Index], Width, Height);
                }

                if (!HasCachedLayerPacket(PixelCaches[Index], Width, Height))
                    return false;

                if (!HasCachedLayerPacket(TopoPixelCaches[Index], Width, Height))
                    return false;

                return HasCachedLayerPacket(DecorationsPixelCaches[Index], Width, Height);
            }
            finally
            {
                Monitor.Exit(_PrepareLock);
            }
        }

        /// <summary>
        /// Returns true when a cached layer packet matches the requested dimensions.
        /// </summary>
        private static bool HasCachedLayerPacket
            (
            RenderedFramePacket? Packet,
            int Width,
            int Height
            )
        {
            return Packet is RenderedFramePacket LayerPacket
                && LayerPacket.Width == Width
                && LayerPacket.Height == Height;
        }

        /// <summary>True when initial 3D camera fit is queued for next prepare.</summary>
        private bool _PendingInitial3DCameraFit;

        /// <summary>
        /// Queues a north-east camera fit on the next 3D frame prepare (worker thread).
        /// </summary>
        public void QueueInitial3DCameraFit
            (
            )
        {
            _PendingInitial3DCameraFit = true;
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
            return (Index >= 0 && Index < FRAME_CACHE_SLOT_COUNT) ? Index : -1;
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

        /// <summary>Last size assigned by the layout system.</summary>
        private Size _LastArrangedSize;

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
            if ((FinalSize.Width > MIN_VIEWPORT_DIMENSION) && (FinalSize.Height > MIN_VIEWPORT_DIMENSION))
                _LastArrangedSize = FinalSize;

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
                if (!ReferenceEquals(Change.NewValue, Change.OldValue))
                    InvalidateFrameCaches();
            }
        }

        /// <summary>
        /// Clears cached frames and the elevation mesh after the project becomes empty.
        /// Must run on the UI thread.
        /// </summary>
        public void ResetForEmptyProject
            (
            )
        {
            if (TryEnterPrepareLockOnUi(ViewerPrepareLock.UiRenderAcquireTimeoutMs))
            {
                try
                {
                    _Surface.BuildFromProject(Project);
                    _SurfaceDirty = false;
                }
                finally
                {
                    Monitor.Exit(_PrepareLock);
                }
            }

            InvalidateFrameCaches();
            ClearStale3DFrames();
            _PresentedFrame = null;
            InvalidateVisual();
        }

        /// <summary>
        /// Drops retained 3D fallback bitmaps used while a new frame is rendering.
        /// </summary>
        private void ClearStale3DFrames
            (
            )
        {
            _Stale3DMonolithicFrame = null;
            _Stale3DBaseFrame = null;
            _Stale3DTopoFrame = null;
            _Stale3DDecorationsFrame = null;
        }

        /// <summary>
        /// Publishes the cached frame for the current view/camera state to <see cref="_PresentedFrame"/>.
        /// </summary>
        /// <returns>True when a matching cached frame was found.</returns>
        public bool TryPresentCachedFrame
            (
            )
        {
            if ((Project == null) || !Project.HasSurvey)
                return false;

            (int Width, int Height) Dimensions = GetRenderDimensions();
            RenderSnapshot Snapshot = CaptureRenderSnapshot(null);
            FieldContentView ContentView = ResolveContentView();
            string CacheKey = BuildFrameCacheKey(Dimensions.Width, Dimensions.Height, ViewMode, Snapshot);
            if (ViewMode == ViewMode.ThreeD)
            {
                WriteableBitmap? CachedMonolithic = TryGetCachedMonolithicFrame(
                    ViewMode,
                    ContentView,
                    CacheKey,
                    Dimensions.Width,
                    Dimensions.Height);
                if (CachedMonolithic == null)
                    return false;

                _PresentedFrame = CachedMonolithic;
                return true;
            }

            WriteableBitmap? CachedFrame = TryGetCachedFrame(
                ViewMode,
                ContentView,
                CacheKey,
                Dimensions.Width,
                Dimensions.Height);
            WriteableBitmap? CachedTopo = TryGetCachedTopoFrame(
                ViewMode,
                ContentView,
                CacheKey,
                Dimensions.Width,
                Dimensions.Height);
            WriteableBitmap? CachedDecorations = TryGetCachedDecorationsFrame(
                ViewMode,
                ContentView,
                CacheKey,
                Dimensions.Width,
                Dimensions.Height);
            if ((CachedFrame == null) || (CachedTopo == null) || (CachedDecorations == null))
                return false;

            _PresentedFrame = CachedFrame;
            return true;
        }

        /// <summary>
        /// Materializes cached pixel layers into Avalonia bitmaps for the current view state.
        /// Must run on the UI thread before the first composite draw after a worker-thread render.
        /// </summary>
        public void MaterializeCachedLayerBitmapsForCurrentView
            (
            )
        {
            if ((Project == null) || !Project.HasSurvey)
                return;

            (int Width, int Height) Dimensions = GetRenderDimensions();
            RenderSnapshot Snapshot = CaptureRenderSnapshot(null);
            FieldContentView ContentView = ResolveContentView();
            string CacheKey = BuildFrameCacheKey(Dimensions.Width, Dimensions.Height, ViewMode, Snapshot);
            MaterializeCachedLayerBitmaps(ViewMode, ContentView, CacheKey, Dimensions.Width, Dimensions.Height);
        }

        /// <summary>
        /// Builds and materializes composite frames at the live viewport before import handoff completes.
        /// </summary>
        public async Task EnsureImportHandoffReadyAsync
            (
            )
        {
            ImportDiagnosticsLog.Write("EnsureHandoff", "START");
            WriteImportDiagnostics("ensure-start");
            if ((Project == null) || !Project.HasSurvey)
            {
                ImportDiagnosticsLog.Write("EnsureHandoff", "SKIP", "no survey");
                return;
            }

            if (HasMaterializedCompositeFrameForCurrentView())
            {
                ImportDiagnosticsLog.Write("EnsureHandoff", "SKIP", "already materialized");
                return;
            }

            using (_BackgroundPrewarm.BeginUiPauseScope("ensure-handoff"))
            {
                RenderSnapshot Snapshot = CaptureRenderSnapshot(null);
                if ((Snapshot.Width <= 0) || (Snapshot.Height <= 0))
                {
                    ImportDiagnosticsLog.Write("EnsureHandoff", "SKIP", $"invalid snapshot {Snapshot.Width}x{Snapshot.Height}");
                    return;
                }

                FieldContentView ContentView = ResolveContentView(Snapshot.Project, Snapshot.ContentView);
                string CacheKey = BuildFrameCacheKey(
                    Snapshot.Width,
                    Snapshot.Height,
                    Snapshot.ViewMode,
                    Snapshot);
                ImportDiagnosticsLog.Write(
                    "EnsureHandoff",
                    "Snapshot",
                    $"mode={Snapshot.ViewMode} content={ContentView} size={Snapshot.Width}x{Snapshot.Height} key={CacheKey}");

                if (HasCachedFrame(
                    Snapshot.ViewMode,
                    ContentView,
                    CacheKey,
                    Snapshot.Width,
                    Snapshot.Height,
                    Snapshot.Display))
                {
                    ImportDiagnosticsLog.Write("EnsureHandoff", "MaterializeCached", CacheKey);
                    await RunOnUiThreadAsync(() =>
                    {
                        TryMaterializeCompositeForCurrentView();
                        InvalidateVisual();
                        WriteImportDiagnostics("ensure-materialized-cached");
                    }).ConfigureAwait(true);
                    return;
                }

                ImportDiagnosticsLog.Write("EnsureHandoff", "WorkerBuild START", CacheKey);
                using (ImportDiagnosticsLog.Time("EnsureHandoff", "WorkerBuildAndStoreFrame", CacheKey))
                {
                    await Task.Run(() =>
                    {
                        lock (_PrepareLock)
                        {
                            BuildAndStoreFrame(Snapshot);
                        }
                    }).ConfigureAwait(false);
                }

                ImportDiagnosticsLog.Write("EnsureHandoff", "UiMaterialize START");
                await RunOnUiThreadAsync(() =>
                {
                    TryMaterializeCompositeForCurrentView();
                    InvalidateVisual();
                    WriteImportDiagnostics("ensure-ui-materialized");
                }).ConfigureAwait(true);

                if (!HasMaterializedCompositeFrameForCurrentView())
                {
                    ImportDiagnosticsLog.Write("EnsureHandoff", "VERIFY_FAIL", "composite still not materialized after UI pass");
                }

                ImportDiagnosticsLog.Write("EnsureHandoff", "END");
            }
        }

        /// <summary>
        /// Schedules deferred background prewarm after the first composite draw when requested.
        /// </summary>
        private void MaybeScheduleDeferredBackgroundPrewarm
            (
            )
        {
            if (!_DeferBackgroundPrewarmUntilFirstCompositeDraw)
                return;

            _DeferBackgroundPrewarmUntilFirstCompositeDraw = false;
            ImportDiagnosticsLog.Write("Import", "ScheduleDeferredPrewarm", "first composite draw");
            ScheduleBackgroundPrewarmForCurrentView();
        }

        /// <summary>
        /// Materializes cached pixel layers into Avalonia bitmaps for the given cache key.
        /// </summary>
        private void MaterializeCachedLayerBitmaps
            (
            ViewMode Mode,
            FieldContentView ContentView,
            string CacheKey,
            int Width,
            int Height
            )
        {
            if (Mode == ViewMode.ThreeD)
            {
                TryGetCachedMonolithicFrame(Mode, ContentView, CacheKey, Width, Height);
                return;
            }

            TryGetCachedFrame(Mode, ContentView, CacheKey, Width, Height);
            TryGetCachedTopoFrame(Mode, ContentView, CacheKey, Width, Height);
            if (Mode != ViewMode.ThreeD)
                TryGetCachedDecorationsFrame(Mode, ContentView, CacheKey, Width, Height);
        }

        /// <summary>
        /// Schedules a background rebuild of the current 3D frame while the last good frame stays visible.
        /// </summary>
        private void ScheduleInteractive3DFrameBuild
            (
            RenderSnapshot Snapshot,
            string CacheKey
            )
        {
            if (string.Equals(_InFlightInteractive3DCacheKey, CacheKey, StringComparison.Ordinal))
                return;

            _InFlightInteractive3DCacheKey = CacheKey;
            int Generation = Interlocked.Increment(ref _Interactive3DRenderGeneration);
            FieldContentView ContentView = ResolveContentView(Snapshot.Project, Snapshot.ContentView);
            _ = Task.Run(() =>
            {
                if (Generation != _Interactive3DRenderGeneration)
                    return;

                lock (_PrepareLock)
                {
                    if (Generation != _Interactive3DRenderGeneration)
                        return;

                    BuildAndStoreFrame(Snapshot);
                }
            }).ContinueWith(
                _ =>
                {
                    if (Generation != _Interactive3DRenderGeneration)
                    {
                        if (string.Equals(_InFlightInteractive3DCacheKey, CacheKey, StringComparison.Ordinal))
                            _InFlightInteractive3DCacheKey = "";

                        return Task.CompletedTask;
                    }

                    return RunOnUiThreadAsync(() =>
                    {
                        if (Generation != _Interactive3DRenderGeneration)
                            return;

                        MaterializeCachedLayerBitmaps(
                            Snapshot.ViewMode,
                            ContentView,
                            CacheKey,
                            Snapshot.Width,
                            Snapshot.Height);
                        _LastSuccessful3DCacheKey = CacheKey;
                        _InFlightInteractive3DCacheKey = "";
                        TryPresentCachedFrame();
                        InvalidateVisual();
                    });
                },
                TaskScheduler.Default);
        }

        /// <summary>
        /// Draws the last successfully presented 3D frame while a new one renders.
        /// </summary>
        private bool TryDrawStale3DFrame
            (
            DrawingContext Context,
            int Width,
            int Height
            )
        {
            WriteableBitmap? MonolithicFrame = _Stale3DMonolithicFrame;
            if ((MonolithicFrame != null) &&
                (MonolithicFrame.PixelSize.Width == Width) &&
                (MonolithicFrame.PixelSize.Height == Height))
            {
                Context.DrawImage(MonolithicFrame, new Rect(0, 0, Width, Height));
                DrawViewCubeOverlay(Context, Width, Height);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Retains the last drawn 3D bitmaps so cache invalidation does not flash black.
        /// </summary>
        private void RememberStale3DFrames
            (
            ViewMode Mode,
            WriteableBitmap? MonolithicFrame,
            WriteableBitmap? BaseFrame,
            WriteableBitmap? TopoFrame,
            WriteableBitmap? DecorationsFrame
            )
        {
            if (Mode == ViewMode.ThreeD)
            {
                _Stale3DMonolithicFrame = MonolithicFrame;
                _Stale3DBaseFrame = null;
                _Stale3DTopoFrame = null;
                _Stale3DDecorationsFrame = null;
                return;
            }

            _Stale3DBaseFrame = BaseFrame;
            _Stale3DTopoFrame = TopoFrame;
            _Stale3DDecorationsFrame = DecorationsFrame;
        }

        /// <summary>
        /// Draws the last successfully presented 2D composite while a new frame is blocked on the prepare lock.
        /// </summary>
        private bool TryDrawStale2DFrame
            (
            DrawingContext Context,
            int Width,
            int Height
            )
        {
            WriteableBitmap? BaseFrame = _Stale3DBaseFrame;
            WriteableBitmap? TopoFrame = _Stale3DTopoFrame;
            WriteableBitmap? DecorationsFrame = _Stale3DDecorationsFrame;
            if ((BaseFrame == null) ||
                (TopoFrame == null) ||
                (DecorationsFrame == null) ||
                (BaseFrame.PixelSize.Width != Width) ||
                (BaseFrame.PixelSize.Height != Height))
            {
                return false;
            }

            double Opacity = Math.Clamp(_Display.TopoCutFillOpacityPercent, MIN_OPACITY_UNIT, OPACITY_PERCENT_TO_UNIT) / OPACITY_PERCENT_TO_UNIT;
            Context.DrawImage(BaseFrame, new Rect(0, 0, Width, Height));
            if (Opacity > MIN_OPACITY_UNIT)
            {
                using (Context.PushOpacity(Opacity))
                    Context.DrawImage(TopoFrame, new Rect(0, 0, Width, Height));
            }

            Context.DrawImage(DecorationsFrame, new Rect(0, 0, Width, Height));
            return true;
        }

        /// <summary>
        /// Returns the pixel width and height used for rendering and cache keys.
        /// </summary>
        /// <summary>
        /// Returns true when this viewer should perform interactive surface and frame work.
        /// </summary>
        private bool ShouldPerformInteractiveRender
            (
            )
        {
            if (!IsPresentationActive)
                return false;

            int Width = (int)Bounds.Width;
            int Height = (int)Bounds.Height;
            if ((Width <= MIN_INTERACTIVE_VIEWPORT_DIMENSION) || (Height <= MIN_INTERACTIVE_VIEWPORT_DIMENSION))
                return false;

            return true;
        }

        /// <summary>
        /// Renders a placeholder frame for hidden or collapsed viewer instances.
        /// </summary>
        private void DrawInactiveViewerPlaceholder
            (
            DrawingContext Context,
            int Width,
            int Height,
            Stopwatch RenderWatch,
            string Reason
            )
        {
            if (_PresentedFrame != null)
                Context.DrawImage(_PresentedFrame, new Rect(0, 0, Width, Height));
            else
                Context.FillRectangle(Brushes.Black, new Rect(0, 0, Width, Height));

            ImportDiagnosticsLog.WriteRender(
                "inactive-skip",
                RenderWatch.ElapsedMilliseconds,
                $"{Width}x{Height} reason={Reason} active={IsPresentationActive} bounds={Bounds.Width:0}x{Bounds.Height:0}");
        }

        private (int Width, int Height) GetRenderDimensions
            (
            )
        {
            int Width = Math.Max(MIN_VIEWPORT_DIMENSION, (int)Bounds.Width);
            int Height = Math.Max(MIN_VIEWPORT_DIMENSION, (int)Bounds.Height);
            if (((Width <= MIN_VIEWPORT_DIMENSION) || (Height <= MIN_VIEWPORT_DIMENSION))
                && (_LastArrangedSize.Width > MIN_VIEWPORT_DIMENSION)
                && (_LastArrangedSize.Height > MIN_VIEWPORT_DIMENSION))
            {
                Width = Math.Max(MIN_VIEWPORT_DIMENSION, (int)_LastArrangedSize.Width);
                Height = Math.Max(MIN_VIEWPORT_DIMENSION, (int)_LastArrangedSize.Height);
            }

            return (Width, Height);
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
                Progress?.Report(PROGRESS_COMPLETE_PERCENT);
                return false;
            }

            string CacheKey = BuildFrameCacheKey(
                Snapshot.Width,
                Snapshot.Height,
                Snapshot.ViewMode,
                Snapshot);
            FieldContentView ContentView = ResolveContentView(Snapshot.Project, Snapshot.ContentView);
            if (!Snapshot.ApplyInitial3DCameraFit
                && HasCachedFrame(Snapshot.ViewMode, ContentView, CacheKey, Snapshot.Width, Snapshot.Height, Snapshot.Display))
            {
                await RunOnUiThreadAsync(() =>
                {
                    Progress?.Report(PROGRESS_COMPLETE_PERCENT);
                    TryPresentCachedFrame();
                    InvalidateVisual();
                });
                return true;
            }

            Progress?.Report(PROGRESS_PREPARE_START_PERCENT);
            bool HasSurvey = Snapshot.Project?.HasSurvey ?? false;
            ClearPresentedFrameReference();
            if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
            {
                CancelBackgroundPrewarm();
                InvalidateFrameCaches();
            }
            else
            {
                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    CancelBackgroundPrewarm();
                    InvalidateFrameCaches();
                });
            }

            InvalidateVisual();

            await Task.Run(() =>
            {
                RenderPreparedFrameSnapshot(Snapshot, Progress);
            }).ConfigureAwait(false);

            await RunOnUiThreadAsync(() =>
            {
                if (Snapshot.ApplyInitial3DCameraFit && (Snapshot.Project != null))
                {
                    Camera3DState Camera = Snapshot.Project.ViewerState.Camera3D;
                    OrbitHeadingDeg = Camera.OrbitHeadingDeg;
                    OrbitPitchDeg = Camera.OrbitPitchDeg;
                    OrbitDistanceM = Camera.DistanceM;
                    OrbitCentreLatitude = Camera.OrbitCentreLat;
                    OrbitCentreLongitude = Camera.OrbitCentreLon;
                }

                if (!HasSurvey)
                    _PresentedFrame = null;

                MaterializeCachedLayerBitmaps(
                    Snapshot.ViewMode,
                    ContentView,
                    CacheKey,
                    Snapshot.Width,
                    Snapshot.Height);
                TryPresentCachedFrame();
                InvalidateVisual();
                Progress?.Report(PROGRESS_COMPLETE_PERCENT);

                if (HasSurvey)
                    ScheduleUnifiedBackgroundPrewarm(Snapshot);
            });

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
            ImportDiagnosticsLog.Write("Prepare", "START", $"targetMode={TargetMode?.ToString() ?? ViewMode.ToString()}");
            Progress?.Report(0);
            RenderSnapshot Snapshot = CaptureRenderSnapshot(TargetMode);
            ImportDiagnosticsLog.WriteViewport("PrepareSnapshot", Snapshot.Width, Snapshot.Height, $"mode={Snapshot.ViewMode}");
            if ((Snapshot.Width <= 0) || (Snapshot.Height <= 0))
            {
                ImportDiagnosticsLog.Write("Prepare", "SKIP", "invalid dimensions");
                Progress?.Report(PROGRESS_COMPLETE_PERCENT);
                OnEarlyDisplayReady?.Invoke();
                return;
            }

            FieldContentView ContentView = ResolveContentView(Snapshot.Project, Snapshot.ContentView);
            string CacheKey = BuildFrameCacheKey(
                Snapshot.Width,
                Snapshot.Height,
                Snapshot.ViewMode,
                Snapshot);
            if (HasCachedFrame(Snapshot.ViewMode, ContentView, CacheKey, Snapshot.Width, Snapshot.Height, Snapshot.Display))
            {
                ImportDiagnosticsLog.Write("Prepare", "CacheHit", CacheKey);
                await RunOnUiThreadAsync(() =>
                {
                    MaterializeCachedLayerBitmaps(
                        Snapshot.ViewMode,
                        ContentView,
                        CacheKey,
                        Snapshot.Width,
                        Snapshot.Height);
                    Progress?.Report(PROGRESS_COMPLETE_PERCENT);
                    TryPresentCachedFrame();
                    InvalidateVisual();
                    OnEarlyDisplayReady?.Invoke();
                });
                return;
            }

            CancelBackgroundPrewarm();
            Progress?.Report(PROGRESS_INITIAL_FIT_PERCENT);
            ImportViewerHandoffTelemetry.Recorder.Record(ImportViewerHandoffEventKind.SurfaceBuildStarted);
            ImportDiagnosticsLog.Write("Prepare", "SurfaceBuild START");
            using (ImportDiagnosticsLog.Time("Prepare", "SurfaceBuild"))
            {
                await Task.Run(() =>
                {
                    lock (_PrepareLock)
                    {
                        Progress?.Report(PROGRESS_PREPARE_START_PERCENT);
                        _Surface.BuildFromProject(
                            Snapshot.Project,
                            Progress: ProgressMapper.Subrange(Progress, PROGRESS_PROGRESSIVE_SURFACE_START_PERCENT, PROGRESS_PROGRESSIVE_SURFACE_END_PERCENT));
                        _SurfaceDirty = false;
                    }
                }).ConfigureAwait(true);
            }

            RenderSnapshot PreviewSnapshot = Snapshot;
            FieldContentView PreviewContentView = ContentView;
            string PreviewCacheKey = CacheKey;
            await RunOnUiThreadAsync(() =>
            {
                PreviewSnapshot = CaptureRenderSnapshot(TargetMode);
                PreviewContentView = ResolveContentView(PreviewSnapshot.Project, PreviewSnapshot.ContentView);
                PreviewCacheKey = BuildFrameCacheKey(
                    PreviewSnapshot.Width,
                    PreviewSnapshot.Height,
                    PreviewSnapshot.ViewMode,
                    PreviewSnapshot);
                ImportDiagnosticsLog.WriteViewport(
                    "PreparePreviewSnapshot",
                    PreviewSnapshot.Width,
                    PreviewSnapshot.Height,
                    $"key={PreviewCacheKey}");
            }).ConfigureAwait(true);

            ImportDiagnosticsLog.Write("Prepare", "PreviewBuild START", PreviewCacheKey);
            using (ImportDiagnosticsLog.Time("Prepare", "PreviewBuild", PreviewCacheKey))
            {
                await Task.Run(() =>
            {
                lock (_PrepareLock)
                {
                    if (PreviewSnapshot.Display != null)
                    {
                        _Surface.PrewarmDisplayCaches(
                            PreviewContentView,
                            PreviewSnapshot.Display,
                            ProgressMapper.Subrange(Progress, PROGRESS_PROGRESSIVE_PREWARM_START_PERCENT, PROGRESS_PROGRESSIVE_PREWARM_END_PERCENT),
                            PlanViewColorMapBuilder.PreviewSupersampleFactor,
                            IncludeContours: false);
                    }

                    RenderedFramePacket? PreviewPacket = BuildAndStoreFrame(
                        PreviewSnapshot,
                        ProgressMapper.Subrange(Progress, PROGRESS_PROGRESSIVE_PREVIEW_START_PERCENT, PROGRESS_PROGRESSIVE_PREVIEW_END_PERCENT));
                    if (PreviewPacket.HasValue)
                    {
                        ImportViewerHandoffTelemetry.Recorder.Record(
                            ImportViewerHandoffEventKind.PreviewFrameStored,
                            PreviewCacheKey,
                            PreviewSnapshot.Width,
                            PreviewSnapshot.Height);
                    }
                }
            }).ConfigureAwait(true);
            }

            await RunOnUiThreadAsync(() =>
            {
                ImportDiagnosticsLog.Write("Prepare", "PreviewMaterialize UI");
                MaterializeCachedLayerBitmaps(
                    PreviewSnapshot.ViewMode,
                    PreviewContentView,
                    PreviewCacheKey,
                    PreviewSnapshot.Width,
                    PreviewSnapshot.Height);
                ImportViewerHandoffTelemetry.Recorder.Record(
                    ImportViewerHandoffEventKind.PreviewMaterialized,
                    PreviewCacheKey,
                    PreviewSnapshot.Width,
                    PreviewSnapshot.Height);
                OnEarlyDisplayReady?.Invoke();
                TryPresentCachedFrame();
                InvalidateVisual();
            }).ConfigureAwait(true);

            ImportDiagnosticsLog.Write("Prepare", "FullQualityBuild START", PreviewCacheKey);
            using (ImportDiagnosticsLog.Time("Prepare", "FullQualityBuild", PreviewCacheKey))
            {
                await Task.Run(() =>
            {
                lock (_PrepareLock)
                {
                    if ((PreviewSnapshot.Project?.HasDesign ?? false) && PreviewSnapshot.Display != null)
                    {
                        _Surface.PrewarmDisplayCaches(
                            FieldContentView.Existing,
                            PreviewSnapshot.Display,
                            ProgressMapper.Subrange(Progress, PROGRESS_PROGRESSIVE_UPGRADE_START_PERCENT, PROGRESS_PROGRESSIVE_UPGRADE_END_PERCENT));
                    }

                    if (PreviewSnapshot.Display != null)
                    {
                        _Surface.UpgradeDisplayCacheQuality(
                            PreviewContentView,
                            PreviewSnapshot.Display,
                            ProgressMapper.Subrange(Progress, PROGRESS_PROGRESSIVE_QUALITY_START_PERCENT, PROGRESS_PROGRESSIVE_QUALITY_END_PERCENT));
                    }

                    BuildAndStoreFrame(
                        PreviewSnapshot,
                        ProgressMapper.Subrange(Progress, PROGRESS_PROGRESSIVE_FULL_START_PERCENT, PROGRESS_PROGRESSIVE_FULL_END_PERCENT));
                }
            }).ConfigureAwait(true);
            }

            await RunOnUiThreadAsync(() =>
            {
                ImportDiagnosticsLog.Write("Prepare", "FullQualityMaterialize UI");
                MaterializeCachedLayerBitmaps(
                    PreviewSnapshot.ViewMode,
                    PreviewContentView,
                    PreviewCacheKey,
                    PreviewSnapshot.Width,
                    PreviewSnapshot.Height);
                _SurfaceDirty = false;
                Progress?.Report(PROGRESS_PROGRESSIVE_ALMOST_DONE_PERCENT);
                TryPresentCachedFrame();
                InvalidateVisual();
                Progress?.Report(PROGRESS_COMPLETE_PERCENT);
                WriteImportDiagnostics("prepare-complete");
            }).ConfigureAwait(true);
            ImportDiagnosticsLog.Write("Prepare", "END");
        }

        /// <summary>
        /// Queues opposite-mode and alternate content-view prewarm after import handoff completes.
        /// </summary>
        public void ScheduleBackgroundPrewarmForCurrentView
            (
            )
        {
            RenderSnapshot Snapshot = CaptureRenderSnapshot(null);
            if ((Snapshot.Width <= 0) || (Snapshot.Height <= 0) || (Snapshot.Project == null) || !Snapshot.Project.HasSurvey)
                return;

            Avalonia.Threading.Dispatcher.UIThread.Post(
                () => ScheduleUnifiedBackgroundPrewarm(Snapshot),
                Avalonia.Threading.DispatcherPriority.ApplicationIdle);
        }

        /// <summary>
        /// Schedules deferred 3D opposite-mode prewarm after import handoff. Alternate content views
        /// (Existing/CutFill) are built on demand because CutFill can hold the prepare lock for many seconds.
        /// </summary>
        private void ScheduleUnifiedBackgroundPrewarm
            (
            RenderSnapshot CompletedSnapshot
            )
        {
            int PrewarmVersion = _BackgroundPrewarm.Generation;
            _ = Task.Run(async () =>
            {
                await Task.Delay(5000).ConfigureAwait(false);

                if (PrewarmVersion != _BackgroundPrewarm.Generation)
                    return;

                if (!WaitAtBackgroundPrewarmCheckpoint(PrewarmVersion, "opposite-mode-idle"))
                    return;

                RunOppositeModePrewarmWorker(CompletedSnapshot, PrewarmVersion);
            });
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
            bool ApplyInitial3DCameraFit = _PendingInitial3DCameraFit && Mode == ViewMode.ThreeD;
            if (ApplyInitial3DCameraFit)
                _PendingInitial3DCameraFit = false;

            (int Width, int Height) Dimensions = GetRenderDimensions();

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
                Width = Dimensions.Width,
                Height = Dimensions.Height,
                SurfaceDirty = _SurfaceDirty,
                ApplyInitial3DCameraFit = ApplyInitial3DCameraFit
            };
        }

        /// <summary>
        /// Rebuilds the elevation mesh and renders a frame on a worker thread.
        /// </summary>
        /// <param name="Snapshot">Frozen render state captured on the UI thread.</param>
        /// <returns>Rendered pixels when a frame was produced.</returns>
        private RenderedFramePacket? RenderPreparedFrameSnapshot
            (
            RenderSnapshot Snapshot,
            IProgress<int>? Progress = null,
            int? BackgroundPrewarmGeneration = null
            )
        {
            if (BackgroundPrewarmGeneration.HasValue &&
                !WaitAtBackgroundPrewarmCheckpoint(BackgroundPrewarmGeneration.Value, "prepared-snapshot-start"))
            {
                return null;
            }

            RenderSnapshot FrameSnapshot = Snapshot;
            IProgress<int>? FrameProgress = null;

            if (!TryEnterPrepareLockForWorker(BackgroundPrewarmGeneration, "prepared-snapshot"))
                return null;

            try
            {
                FrameSnapshot = Snapshot;
                if (Snapshot.ApplyInitial3DCameraFit && (Snapshot.Project != null))
                {
                    Progress?.Report(PROGRESS_INITIAL_FIT_PERCENT);
                    Camera3DState Camera = Snapshot.Project.ViewerState.Camera3D;
                    OrbitViewFitter.ApplyInitialOrientation(Camera);

                    bool HasTopo = Snapshot.Project.HasSurvey;
                    if (HasTopo)
                    {
                        if (ViewerFrameCacheKey.NeedsSurfaceRebuild(Snapshot.SurfaceDirty, _Surface.SampleWidth))
                        {
                            _Surface.BuildFromProject(Snapshot.Project, Progress: ProgressMapper.Subrange(Progress, PROGRESS_INITIAL_FIT_PERCENT, PROGRESS_INITIAL_FIT_SURFACE_END_PERCENT));
                            _SurfaceDirty = false;
                        }

                        if (_Surface.SampleWidth > 0)
                        {
                            OrbitViewFitter.FitToField(
                                Snapshot.Project,
                                _Surface,
                                Camera,
                                Snapshot.Display?.VerticalExaggeration ?? DEFAULT_VERTICAL_EXAGGERATION,
                                Snapshot.Width,
                                Snapshot.Height);
                        }
                    }

                    FrameSnapshot = WithOrbitCamera(Snapshot, Camera);
                    Progress?.Report(PROGRESS_INITIAL_FIT_COMPLETE_PERCENT);
                }

                bool HasTopoForFrame = (FrameSnapshot.Project != null) && FrameSnapshot.Project.HasSurvey;
                if (!HasTopoForFrame)
                {
                    _Surface.BuildFromProject(Snapshot.Project, Progress: Progress);
                    _SurfaceDirty = false;
                    Progress?.Report(PROGRESS_COMPLETE_PERCENT);
                    return null;
                }

                bool NeedsSurfaceBuild = ViewerFrameCacheKey.NeedsSurfaceRebuild(FrameSnapshot.SurfaceDirty, _Surface.SampleWidth);
                if (NeedsSurfaceBuild)
                {
                    Progress?.Report(PROGRESS_SURFACE_BUILD_START_PERCENT);
                    _Surface.BuildFromProject(FrameSnapshot.Project, Progress: ProgressMapper.Subrange(Progress, PROGRESS_SURFACE_BUILD_START_PERCENT, PROGRESS_SURFACE_BUILD_END_PERCENT));
                    _SurfaceDirty = false;
                }

                if (FrameSnapshot.Display != null)
                {
                    FieldContentView ContentView = ResolveContentView(FrameSnapshot.Project, FrameSnapshot.ContentView);
                    IProgress<int>? PrewarmProgress = NeedsSurfaceBuild
                        ? ProgressMapper.Subrange(Progress, PROGRESS_PREWARM_START_WITH_SURFACE_PERCENT, PROGRESS_PREWARM_END_WITH_SURFACE_PERCENT)
                        : ProgressMapper.Subrange(Progress, PROGRESS_PREWARM_START_PERCENT, PROGRESS_PREWARM_END_PERCENT);
                    Progress?.Report(NeedsSurfaceBuild ? PROGRESS_PREWARM_START_WITH_SURFACE_PERCENT : PROGRESS_PREWARM_START_PERCENT + 3);
                    _Surface.PrewarmDisplayCaches(ContentView, FrameSnapshot.Display, PrewarmProgress);
                }

                Progress?.Report(NeedsSurfaceBuild ? PROGRESS_FRAME_START_WITH_SURFACE_PERCENT : PROGRESS_FRAME_START_PERCENT);
                FrameProgress = NeedsSurfaceBuild
                    ? ProgressMapper.Subrange(Progress, PROGRESS_FRAME_START_WITH_SURFACE_PERCENT, PROGRESS_FRAME_END_PERCENT)
                    : ProgressMapper.Subrange(Progress, PROGRESS_FRAME_START_PERCENT, PROGRESS_FRAME_END_PERCENT);

                RenderedFramePacket? FramePacket = BuildAndStoreFrame(
                    FrameSnapshot,
                    FrameProgress,
                    BackgroundPrewarmGeneration);
                Progress?.Report(PROGRESS_PROGRESSIVE_ALMOST_DONE_PERCENT);
                return FramePacket;
            }
            finally
            {
                Monitor.Exit(_PrepareLock);
            }
        }

        /// <summary>
        /// Returns a snapshot copy with orbit camera fields taken from project viewer state.
        /// </summary>
        static private RenderSnapshot WithOrbitCamera
            (
            RenderSnapshot Source,
            Camera3DState Camera
            )
        {
            return new RenderSnapshot
            {
                Project = Source.Project,
                ViewMode = Source.ViewMode,
                ContentView = Source.ContentView,
                Display = Source.Display,
                CentreLatitude = Source.CentreLatitude,
                CentreLongitude = Source.CentreLongitude,
                ZoomFactor = Source.ZoomFactor,
                OrbitHeadingDeg = Camera.OrbitHeadingDeg,
                OrbitPitchDeg = Camera.OrbitPitchDeg,
                OrbitDistanceM = Camera.DistanceM,
                OrbitCentreLatitude = Camera.OrbitCentreLat,
                OrbitCentreLongitude = Camera.OrbitCentreLon,
                Width = Source.Width,
                Height = Source.Height,
                SurfaceDirty = Source.SurfaceDirty,
                ApplyInitial3DCameraFit = false
            };
        }

        /// <summary>
        /// Discards cached 2D and 3D frame bitmaps.
        /// </summary>
        private void InvalidateFrameCaches
            (
            )
        {
            _BackgroundPrewarm.Invalidate("frame-cache-invalidate");
            ClearPresentedFrameReference();
            ReleaseFrameCacheArray(_FrameCaches2D, _FramePixelCaches2D, _FrameCacheKeys2D, _FrameSatelliteGenerations2D, _FrameSatelliteTilesComplete2D);
            ReleaseTopoFrameCacheArray(_FrameTopoCaches2D, _FrameTopoPixelCaches2D);
            ReleaseTopoFrameCacheArray(_FrameDecorationsCaches2D, _FrameDecorationsPixelCaches2D);
            ReleaseFrameCacheArray(_FrameCaches3D, _FramePixelCaches3D, _FrameCacheKeys3D, _FrameSatelliteGenerations3D, _FrameSatelliteTilesComplete3D);
            ReleaseTopoFrameCacheArray(_FrameTopoCaches3D, _FrameTopoPixelCaches3D);
            ReleaseTopoFrameCacheArray(_FrameDecorationsCaches3D, _FrameDecorationsPixelCaches3D);
            ReleaseTopoFrameCacheArray(_FrameMonolithicCaches3D, _FrameMonolithicPixelCaches3D);
            _LastSuccessful3DCacheKey = "";
            Interlocked.Increment(ref _Interactive3DRenderGeneration);
            Field3DGeometryCache.Invalidate();
        }

        /// <summary>
        /// Drops the displayed frame reference before cache slots are recycled.
        /// </summary>
        private void ClearPresentedFrameReference
            (
            )
        {
            _PresentedFrame = null;
        }

        /// <summary>
        /// Clears only base-layer frame caches when new satellite tiles arrive after a partial basemap.
        /// </summary>
        private void InvalidateSatelliteBaseFrameCaches
            (
            )
        {
            if (!TryEnterPrepareLockOnUi(ViewerPrepareLock.UiAcquireTimeoutMs))
                return;

            try
            {
                for (int I = 0; I < _FrameCaches2D.Length; I++)
                {
                    _FrameCaches2D[I] = null;
                    _FramePixelCaches2D[I] = null;
                    _FrameSatelliteGenerations2D[I] = 0;
                    _FrameSatelliteTilesComplete2D[I] = false;
                }

                for (int I = 0; I < _FrameCaches3D.Length; I++)
                {
                    _FrameCaches3D[I] = null;
                    _FramePixelCaches3D[I] = null;
                    _FrameMonolithicCaches3D[I] = null;
                    _FrameMonolithicPixelCaches3D[I] = null;
                    _FrameSatelliteGenerations3D[I] = 0;
                    _FrameSatelliteTilesComplete3D[I] = false;
                }
            }
            finally
            {
                Monitor.Exit(_PrepareLock);
            }
        }

        /// <summary>
        /// Disposes cached frame bitmaps and clears their slot metadata.
        /// </summary>
        private static void ReleaseFrameCacheArray
            (
            WriteableBitmap?[] Caches,
            RenderedFramePacket?[] PixelCaches,
            string[] Keys,
            int[] SatelliteGenerations,
            bool[] SatelliteTilesComplete
            )
        {
            for (int I = 0; I < Caches.Length; I++)
            {
                // Do not Dispose() Avalonia bitmaps here: they may still be referenced by an
                // in-flight Render pass. Recycling the slot is enough; GC reclaims unused bitmaps.
                Caches[I] = null;
                PixelCaches[I] = null;
                Keys[I] = "";
                SatelliteGenerations[I] = 0;
                SatelliteTilesComplete[I] = false;
            }
        }

        /// <summary>
        /// Disposes cached topo overlay bitmaps.
        /// </summary>
        private static void ReleaseTopoFrameCacheArray
            (
            WriteableBitmap?[] Caches,
            RenderedFramePacket?[] PixelCaches
            )
        {
            for (int I = 0; I < Caches.Length; I++)
            {
                Caches[I] = null;
                PixelCaches[I] = null;
            }
        }

        /// <summary>
        /// Pre-renders alternate field content views on a background thread.
        /// </summary>
        /// <summary>
        /// Pre-renders uncached alternate content views on the unified prewarm worker.
        /// </summary>
        private void RunAlternateContentViewPrewarmWorker
            (
            RenderSnapshot CompletedSnapshot,
            int PrewarmVersion
            )
        {
            if ((CompletedSnapshot.Project == null) || (CompletedSnapshot.Display == null) || !CompletedSnapshot.Project.HasSurvey)
                return;

            FieldContentView[] AlternateViews = CompletedSnapshot.Project.HasDesign
                ? new[] { FieldContentView.Existing, FieldContentView.Proposed, FieldContentView.CutFill }
                : new[] { FieldContentView.Existing };

            DisplaySettings Display = CompletedSnapshot.Display;
            bool ScheduledRedraw = false;
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
                if (HasCachedFrame(
                    AlternateSnapshot.ViewMode,
                    ResolvedView,
                    CacheKey,
                    AlternateSnapshot.Width,
                    AlternateSnapshot.Height,
                    AlternateSnapshot.Display))
                {
                    continue;
                }

                if (!WaitAtBackgroundPrewarmCheckpoint(PrewarmVersion, $"alternate-{AlternateView}-start"))
                    return;

                lock (_PrepareLock)
                {
                    if (PrewarmVersion != _BackgroundPrewarm.Generation)
                        return;

                    _Surface.PrewarmDisplayCaches(
                        AlternateView,
                        Display,
                        null,
                        IncludeContours: Display.Contours);
                }

                if (!WaitAtBackgroundPrewarmCheckpoint(PrewarmVersion, $"alternate-{AlternateView}-build"))
                    return;

                lock (_PrepareLock)
                {
                    if (PrewarmVersion != _BackgroundPrewarm.Generation)
                        return;

                    BuildAndStoreFrame(AlternateSnapshot, null, PrewarmVersion);
                }

                ScheduledRedraw = true;
            }

            if (ScheduledRedraw)
                Avalonia.Threading.Dispatcher.UIThread.Post(InvalidateVisual);
        }

        /// <summary>
        /// Pre-renders the opposite view mode on a background thread so the next toggle is instant.
        /// </summary>
        /// <summary>
        /// Pre-renders the opposite view mode on the unified prewarm worker when needed.
        /// </summary>
        /// <returns>False when the worker should exit due to invalidation.</returns>
        private bool RunOppositeModePrewarmWorker
            (
            RenderSnapshot CompletedSnapshot,
            int PrewarmVersion
            )
        {
            ViewMode OtherMode = CompletedSnapshot.ViewMode == ViewMode.TwoD ? ViewMode.ThreeD : ViewMode.TwoD;
            RenderSnapshot OtherSnapshot = CloneSnapshotForViewMode(CompletedSnapshot, OtherMode);
            if ((OtherSnapshot.Width <= 0) || (OtherSnapshot.Height <= 0))
                return true;

            string CacheKey = BuildFrameCacheKey(
                OtherSnapshot.Width,
                OtherSnapshot.Height,
                OtherMode,
                OtherSnapshot);
            FieldContentView OtherContentView = ResolveContentView(OtherSnapshot.Project, OtherSnapshot.ContentView);
            if (HasCachedFrame(OtherMode, OtherContentView, CacheKey, OtherSnapshot.Width, OtherSnapshot.Height, OtherSnapshot.Display))
                return true;

            if (!WaitAtBackgroundPrewarmCheckpoint(PrewarmVersion, "opposite-mode-build"))
                return false;

            RenderedFramePacket? Packet = RenderPreparedFrameSnapshot(OtherSnapshot, null, PrewarmVersion);
            if (PrewarmVersion != _BackgroundPrewarm.Generation)
                return false;

            if (Packet.HasValue)
                Avalonia.Threading.Dispatcher.UIThread.Post(InvalidateVisual);

            return true;
        }

        /// <summary>
        /// Copies a render snapshot with a different view mode.
        /// </summary>
        private static RenderSnapshot CloneSnapshotForViewMode
            (
            RenderSnapshot Source,
            ViewMode Mode
            )
        {
            return new RenderSnapshot
            {
                Project = Source.Project,
                ViewMode = Mode,
                ContentView = Source.ContentView,
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
                SurfaceDirty = false,
                ApplyInitial3DCameraFit = false
            };
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
                SurfaceDirty = false,
                ApplyInitial3DCameraFit = false
            };
        }

        /// <summary>
        /// Renders one frame layer and returns pixel data for UI-thread cache commit.
        /// </summary>
        private RenderedFramePacket? BuildLayerFrame
            (
            RenderSnapshot Snapshot,
            FieldRenderPass Pass,
            string CacheKey,
            IProgress<int>? Progress = null
            )
        {
            if ((Snapshot.Project == null) || !Snapshot.Project.HasSurvey)
                return null;

            DisplaySettings Display = Snapshot.Display ?? _Display;
            bool SatelliteTilesComplete = true;
            Progress?.Report(0);
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
                    Display,
                    Snapshot.CentreLatitude,
                    Snapshot.CentreLongitude,
                    MetresPerPixel,
                    Display.Satellite ? _Satellite : null,
                    Pass,
                    out int SatellitePlaceholdersDrawn,
                    Progress);

                if (Pass == FieldRenderPass.Base)
                {
                    SatelliteTilesComplete = !Display.Satellite || (SatellitePlaceholdersDrawn == 0);
                }
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
                    Display,
                    Snapshot.OrbitHeadingDeg,
                    Snapshot.OrbitPitchDeg,
                    Snapshot.OrbitDistanceM,
                    Display.VerticalExaggeration,
                    OrbitLat,
                    OrbitLon,
                    Display.Satellite ? _Satellite : null,
                    Pass,
                    Progress);

                if (Pass is FieldRenderPass.Base or FieldRenderPass.Monolithic)
                {
                    SatelliteTilesComplete = !Display.Satellite
                        || AreVisibleSatelliteTilesCached(Snapshot);
                }
            }

            Canvas.Flush();

            Progress?.Report(FRAME_PIXEL_COPY_PROGRESS_PERCENT);
            byte[] Pixels = CopySkiaPixelsToBuffer(FrameBitmap, Snapshot.Width, Snapshot.Height);
            Progress?.Report(PROGRESS_COMPLETE_PERCENT);
            return new RenderedFramePacket(
                Pixels,
                Snapshot.Width,
                Snapshot.Height,
                Snapshot.ViewMode,
                ContentView,
                CacheKey,
                Display.Satellite,
                Display.TopoCutFillOpacityPercent,
                SatelliteTilesComplete);
        }

        /// <summary>
        /// Returns true when every satellite tile for the active view is present in the tile cache.
        /// </summary>
        private bool AreVisibleSatelliteTilesCached
            (
            RenderSnapshot Snapshot
            )
        {
            if (Snapshot.ViewMode == ViewMode.TwoD)
            {
                double MetresPerPixel = FieldRenderer.EstimateMetresPerPixel(
                    Snapshot.CentreLatitude,
                    Snapshot.ZoomFactor);
                SatelliteMapViewport Layout = SatelliteMapLayout.Compute(
                    Snapshot.CentreLatitude,
                    Snapshot.CentreLongitude,
                    Snapshot.Width,
                    Snapshot.Height,
                    MetresPerPixel);
                foreach (SatelliteTileRequest Tile in Layout.Tiles)
                {
                    if (!_Satellite.IsCached(Layout.Zoom, Tile.TileX, Tile.TileY))
                        return false;
                }

                return Layout.Tiles.Count > 0;
            }

            if ((Snapshot.Project == null) || (_Surface.SampleWidth <= 0))
                return false;

            double OrbitLat = ResolveOrbitCentreLatitude(Snapshot.OrbitCentreLatitude);
            double OrbitLon = ResolveOrbitCentreLongitude(Snapshot.OrbitCentreLongitude);
            double FieldCentreLat = (_Surface.MinLat + _Surface.MaxLat) * FIELD_CENTRE_FRACTION;
            double FieldCentreLon = (_Surface.MinLon + _Surface.MaxLon) * FIELD_CENTRE_FRACTION;
            FieldCamera3D Camera = new FieldCamera3D(
                _Surface,
                Snapshot.OrbitHeadingDeg,
                Snapshot.OrbitPitchDeg,
                Snapshot.OrbitDistanceM,
                Snapshot.Display?.VerticalExaggeration ?? DEFAULT_VERTICAL_EXAGGERATION,
                OrbitLat,
                OrbitLon,
                ResolveContentView(Snapshot.Project, Snapshot.ContentView),
                GroundPlaneClearanceM: Field3DGeometryBuilder.ComputeFlatOverlayZLift(_Surface));
            double MetresPerPixel3D = Camera.EstimateGroundMetresPerScreenPixel(
                SKRect.Create(0, 0, Snapshot.Width, Snapshot.Height)) * RenderingConstants.PLINTH_TILE_ZOOM_FACTOR;
            const double PlinthHalfSpanM = RenderingConstants.METRES_PER_MILE;
            double PlinthDiagonalM = PlinthHalfSpanM * PLINTH_DIAGONAL_SIDE_MULTIPLIER * Math.Sqrt(PLINTH_DIAGONAL_SIDE_MULTIPLIER);
            (int Zoom, IReadOnlyList<(int TileX, int TileY)> TileIndices) = SatelliteMapLayout3D.ComputePlinthTiles(
                FieldCentreLat,
                FieldCentreLon,
                PlinthDiagonalM,
                MetresPerPixel3D);
            foreach ((int TileX, int TileY) in TileIndices)
            {
                if (!_Satellite.IsCached(Zoom, TileX, TileY))
                    return false;
            }

            return TileIndices.Count > 0;
        }

        /// <summary>
        /// Renders base and surface layers and returns the base packet.
        /// </summary>
        /// <param name="Snapshot">Frozen render state.</param>
        private RenderedFramePacket? BuildAndStoreFrame
            (
            RenderSnapshot Snapshot,
            IProgress<int>? Progress = null,
            int? BackgroundPrewarmGeneration = null
            )
        {
            if ((Snapshot.Project == null) || !Snapshot.Project.HasSurvey)
                return null;

            Stopwatch BuildWatch = Stopwatch.StartNew();
            DisplaySettings Display = Snapshot.Display ?? _Display;
            Progress?.Report(0);
            string CacheKey = BuildFrameCacheKey(
                Snapshot.Width,
                Snapshot.Height,
                Snapshot.ViewMode,
                Snapshot);
            ImportDiagnosticsLog.Write(
                "FrameBuild",
                "START",
                $"{Snapshot.ViewMode} {Snapshot.Width}x{Snapshot.Height} content={ResolveContentView(Snapshot.Project, Snapshot.ContentView)} key={CacheKey}");
            RenderedFramePacket? Result;
            if ((Snapshot.ViewMode == ViewMode.ThreeD))
            {
                if (!YieldPrepareLockForBackgroundPrewarm(BackgroundPrewarmGeneration, "frame-3d-monolithic"))
                    return null;

                Result = BuildAndStoreMonolithicFrame3D(Snapshot, CacheKey, Progress);
            }
            else
            {
                RenderedFramePacket? BasePacket = BuildLayerFrame(
                    Snapshot,
                    FieldRenderPass.Base,
                    CacheKey,
                    ProgressMapper.Subrange(Progress, 0, PROGRESS_2D_BASE_LAYER_END_PERCENT));
                if (!YieldPrepareLockForBackgroundPrewarm(BackgroundPrewarmGeneration, "frame-base-layer"))
                    return BasePacket;

                RenderedFramePacket? TopoPacket = BuildLayerFrame(
                    Snapshot,
                    FieldRenderPass.SurfaceOverlay,
                    CacheKey,
                    ProgressMapper.Subrange(Progress, PROGRESS_2D_BASE_LAYER_END_PERCENT, PROGRESS_2D_TOPO_LAYER_END_PERCENT));
                if (!YieldPrepareLockForBackgroundPrewarm(BackgroundPrewarmGeneration, "frame-topo-layer"))
                    return BasePacket;

                RenderedFramePacket? DecorationsPacket = BuildLayerFrame(
                    Snapshot,
                    FieldRenderPass.Decorations,
                    CacheKey,
                    ProgressMapper.Subrange(Progress, PROGRESS_2D_TOPO_LAYER_END_PERCENT, PROGRESS_COMPLETE_PERCENT));
                if (!YieldPrepareLockForBackgroundPrewarm(BackgroundPrewarmGeneration, "frame-decorations-layer"))
                    return BasePacket;

                CommitTwoDFrameLayerCaches(BasePacket, TopoPacket, DecorationsPacket);

                Result = BasePacket;
            }

            ImportDiagnosticsLog.Write(
                "FrameBuild",
                "END",
                $"{BuildWatch.ElapsedMilliseconds}ms {Snapshot.Width}x{Snapshot.Height} key={CacheKey}");
            return Result;
        }

        /// <summary>
        /// Renders and caches a single full 3D frame for 100% topo opacity.
        /// </summary>
        private RenderedFramePacket? BuildAndStoreMonolithicFrame3D
            (
            RenderSnapshot Snapshot,
            string CacheKey,
            IProgress<int>? Progress
            )
        {
            RenderedFramePacket? MonolithicPacket = BuildLayerFrame(
                Snapshot,
                FieldRenderPass.Monolithic,
                CacheKey,
                Progress);
            if (MonolithicPacket.HasValue)
                StoreMonolithicFramePixelCacheForMode(MonolithicPacket.Value);

            return MonolithicPacket;
        }

        /// <summary>
        /// Stores all 2D frame layers atomically so cache keys never advance ahead of topo pixels.
        /// Caller must hold <see cref="_PrepareLock"/>.
        /// </summary>
        private void CommitTwoDFrameLayerCaches
            (
            RenderedFramePacket? BasePacket,
            RenderedFramePacket? TopoPacket,
            RenderedFramePacket? DecorationsPacket
            )
        {
            if (!BasePacket.HasValue || !TopoPacket.HasValue || !DecorationsPacket.HasValue)
                return;

            RenderedFramePacket Base = BasePacket.Value;
            int Index = ContentViewCacheIndex(Base.ContentView);
            if (Index < 0)
                return;

            _FrameCaches2D[Index] = null;
            _FramePixelCaches2D[Index] = Base;
            _FrameCacheKeys2D[Index] = Base.CacheKey;
            _FrameSatelliteGenerations2D[Index] = Base.ShowSatellite ? _Satellite.CacheGeneration : 0;
            _FrameSatelliteTilesComplete2D[Index] = !Base.ShowSatellite || Base.SatelliteTilesComplete;
            _FrameTopoCaches2D[Index] = null;
            _FrameTopoPixelCaches2D[Index] = TopoPacket.Value;
            _FrameDecorationsCaches2D[Index] = null;
            _FrameDecorationsPixelCaches2D[Index] = DecorationsPacket.Value;
        }

        /// <summary>
        /// Stores rendered monolithic 3D pixels in the frame cache.
        /// </summary>
        private void StoreMonolithicFramePixelCacheForMode
            (
            RenderedFramePacket Packet
            )
        {
            int Index = ContentViewCacheIndex(Packet.ContentView);
            if (Index < 0)
                return;

            lock (_PrepareLock)
            {
                _FrameMonolithicCaches3D[Index] = null;
                _FrameMonolithicPixelCaches3D[Index] = Packet;
                _FrameCacheKeys3D[Index] = Packet.CacheKey;
                _FrameSatelliteGenerations3D[Index] = Packet.ShowSatellite ? _Satellite.CacheGeneration : 0;
                _FrameSatelliteTilesComplete3D[Index] = !Packet.ShowSatellite || Packet.SatelliteTilesComplete;
            }
        }

        /// <summary>
        /// Stores rendered pixels in the frame cache. Safe to call from worker threads.
        /// </summary>
        private void StoreFramePixelCacheForMode
            (
            RenderedFramePacket Packet
            )
        {
            int Index = ContentViewCacheIndex(Packet.ContentView);
            if (Index < 0)
                return;

            lock (_PrepareLock)
            {
                if (Packet.Mode == ViewMode.TwoD)
                {
                    _FrameCaches2D[Index] = null;
                    _FramePixelCaches2D[Index] = Packet;
                    _FrameCacheKeys2D[Index] = Packet.CacheKey;
                    _FrameSatelliteGenerations2D[Index] = Packet.ShowSatellite ? _Satellite.CacheGeneration : 0;
                    _FrameSatelliteTilesComplete2D[Index] = !Packet.ShowSatellite || Packet.SatelliteTilesComplete;
                }
                else
                {
                    _FrameCaches3D[Index] = null;
                    _FramePixelCaches3D[Index] = Packet;
                    _FrameCacheKeys3D[Index] = Packet.CacheKey;
                    _FrameSatelliteGenerations3D[Index] = Packet.ShowSatellite ? _Satellite.CacheGeneration : 0;
                    _FrameSatelliteTilesComplete3D[Index] = !Packet.ShowSatellite || Packet.SatelliteTilesComplete;
                }
            }
        }

        /// <summary>
        /// Stores rendered topo overlay pixels in the frame cache.
        /// </summary>
        private void StoreTopoFramePixelCacheForMode
            (
            RenderedFramePacket Packet
            )
        {
            int Index = ContentViewCacheIndex(Packet.ContentView);
            if (Index < 0)
                return;

            lock (_PrepareLock)
            {
                if (Packet.Mode == ViewMode.TwoD)
                {
                    _FrameTopoCaches2D[Index] = null;
                    _FrameTopoPixelCaches2D[Index] = Packet;
                }
                else
                {
                    _FrameTopoCaches3D[Index] = null;
                    _FrameTopoPixelCaches3D[Index] = Packet;
                }
            }
        }

        /// <summary>
        /// Stores rendered decoration overlay pixels in the frame cache.
        /// </summary>
        private void StoreDecorationsFramePixelCacheForMode
            (
            RenderedFramePacket Packet
            )
        {
            int Index = ContentViewCacheIndex(Packet.ContentView);
            if (Index < 0)
                return;

            lock (_PrepareLock)
            {
                if (Packet.Mode == ViewMode.TwoD)
                {
                    _FrameDecorationsCaches2D[Index] = null;
                    _FrameDecorationsPixelCaches2D[Index] = Packet;
                }
                else
                {
                    _FrameDecorationsCaches3D[Index] = null;
                    _FrameDecorationsPixelCaches3D[Index] = Packet;
                }
            }
        }

        /// <summary>
        /// Stores a rendered frame in the cache for a specific view mode and content view.
        /// </summary>
        private void StoreFrameCacheForMode
            (
            ViewMode Mode,
            FieldContentView ContentView,
            string CacheKey,
            WriteableBitmap Frame,
            bool ShowSatellite
            )
        {
            int Index = ContentViewCacheIndex(ContentView);
            if (Index < 0)
                return;

            if (Mode == ViewMode.TwoD)
            {
                _FrameCaches2D[Index] = Frame;
                _FramePixelCaches2D[Index] = null;
                _FrameCacheKeys2D[Index] = CacheKey;
                _FrameSatelliteGenerations2D[Index] = ShowSatellite ? _Satellite.CacheGeneration : 0;
                _FrameSatelliteTilesComplete2D[Index] = !ShowSatellite;
            }
            else
            {
                _FrameCaches3D[Index] = Frame;
                _FramePixelCaches3D[Index] = null;
                _FrameCacheKeys3D[Index] = CacheKey;
                _FrameSatelliteGenerations3D[Index] = ShowSatellite ? _Satellite.CacheGeneration : 0;
                _FrameSatelliteTilesComplete3D[Index] = !ShowSatellite;
            }
        }

        /// <summary>
        /// Pixel buffer and metadata for one rendered viewer frame.
        /// </summary>
        private readonly struct RenderedFramePacket
        {
            /// <summary>Creates a rendered frame packet with pixel data and cache metadata.</summary>
            public RenderedFramePacket
                (
                byte[] Pixels,
                int Width,
                int Height,
                ViewMode Mode,
                FieldContentView ContentView,
                string CacheKey,
                bool ShowSatellite,
                double TopoOverlayOpacityPercent = FULL_TOPO_OPACITY_PERCENT,
                bool SatelliteTilesComplete = true
                )
            {
                this.Pixels = Pixels;
                this.Width = Width;
                this.Height = Height;
                this.Mode = Mode;
                this.ContentView = ContentView;
                this.CacheKey = CacheKey;
                this.ShowSatellite = ShowSatellite;
                this.TopoOverlayOpacityPercent = TopoOverlayOpacityPercent;
                this.SatelliteTilesComplete = SatelliteTilesComplete;
            }

            /// <summary>RGBA pixel bytes row-major.</summary>
            public byte[] Pixels { get; }
            /// <summary>Frame width in pixels.</summary>
            public int Width { get; }
            /// <summary>Frame height in pixels.</summary>
            public int Height { get; }
            /// <summary>View mode (2D or 3D) for this frame.</summary>
            public ViewMode Mode { get; }
            /// <summary>Active field content view when rendered.</summary>
            public FieldContentView ContentView { get; }
            /// <summary>Fingerprint string used for frame cache lookup.</summary>
            public string CacheKey { get; }
            /// <summary>Whether satellite imagery was enabled for this frame.</summary>
            public bool ShowSatellite { get; }
            /// <summary>Topo/cut-fill overlay opacity when rendered.</summary>
            public double TopoOverlayOpacityPercent { get; }
            /// <summary>True when all required satellite tiles were loaded.</summary>
            public bool SatelliteTilesComplete { get; }
        }

        /// <summary>
        /// Immutable render inputs captured on the UI thread for background work.
        /// </summary>
        private sealed class RenderSnapshot
        {
            /// <summary>Project document captured for background render.</summary>
            public ProjectDocument? Project { get; init; }
            /// <summary>Active view mode at capture time.</summary>
            public ViewMode ViewMode { get; init; }
            /// <summary>Field content view at capture time.</summary>
            public FieldContentView ContentView { get; init; }
            /// <summary>Display settings at capture time.</summary>
            public DisplaySettings? Display { get; init; }
            /// <summary>2D view centre latitude at capture time.</summary>
            public double CentreLatitude { get; init; }
            /// <summary>2D view centre longitude at capture time.</summary>
            public double CentreLongitude { get; init; }
            /// <summary>2D zoom factor at capture time.</summary>
            public double ZoomFactor { get; init; }
            /// <summary>3D orbit heading at capture time.</summary>
            public double OrbitHeadingDeg { get; init; }
            /// <summary>3D orbit pitch at capture time.</summary>
            public double OrbitPitchDeg { get; init; }
            /// <summary>3D orbit distance at capture time.</summary>
            public double OrbitDistanceM { get; init; }
            /// <summary>3D orbit pivot latitude at capture time.</summary>
            public double OrbitCentreLatitude { get; init; }
            /// <summary>3D orbit pivot longitude at capture time.</summary>
            public double OrbitCentreLongitude { get; init; }
            /// <summary>Viewport width at capture time.</summary>
            public int Width { get; init; }
            /// <summary>Viewport height at capture time.</summary>
            public int Height { get; init; }
            /// <summary>Whether surface was dirty at capture time.</summary>
            public bool SurfaceDirty { get; init; }
            /// <summary>Whether initial 3D fit should run on this prepare.</summary>
            public bool ApplyInitial3DCameraFit { get; init; }
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
            Stopwatch RenderWatch = Stopwatch.StartNew();
            int Width;
            int Height;
            (Width, Height) = GetRenderDimensions();
            bool Suppress = SuppressSynchronousPrepare;
            bool HasTopo = (Project != null) && Project.HasSurvey;

            if (!HasTopo)
            {
                Context.FillRectangle(Brushes.Black, new Rect(0, 0, Width, Height));
                ImportDiagnosticsLog.WriteRender("empty", RenderWatch.ElapsedMilliseconds, $"{Width}x{Height}");
                return;
            }

            if (!ShouldPerformInteractiveRender())
            {
                DrawInactiveViewerPlaceholder(Context, Width, Height, RenderWatch, "hidden-viewer");
                return;
            }

            if (_SurfaceDirty && !Suppress)
            {
                ImportDiagnosticsLog.Write("Render", "SyncSurfaceRebuild START");
                Stopwatch SurfaceWatch = Stopwatch.StartNew();
                if (TryEnterPrepareLockForInteractiveRender())
                {
                    try
                    {
                        _Surface.BuildFromProject(Project);
                        _SurfaceDirty = false;
                        TestSurfaceRebuildCount++;
                    }
                    finally
                    {
                        Monitor.Exit(_PrepareLock);
                    }
                }

                ImportDiagnosticsLog.WriteRender(
                    "sync-surface-rebuild",
                    SurfaceWatch.ElapsedMilliseconds,
                    $"{Width}x{Height}");
            }

            if (DisplaySettings != null)
                _Display = DisplaySettings;

            FieldContentView ContentView = ResolveContentView();
            RenderSnapshot Snapshot = CaptureRenderSnapshot(ViewMode);
            string CacheKey = BuildFrameCacheKey(Width, Height, ViewMode, Snapshot);
            if (TryDrawCompositeCachedFrames(Context, ViewMode, ContentView, CacheKey, Width, Height))
            {
                if (ViewMode == ViewMode.ThreeD)
                    _LastSuccessful3DCacheKey = CacheKey;

                MaybeScheduleDeferredBackgroundPrewarm();
                TestRecordFirstRenderAfterHandoff(UsedSyncFallback: false);
                ImportDiagnosticsLog.WriteRender(
                    "composite-hit",
                    RenderWatch.ElapsedMilliseconds,
                    $"{Width}x{Height} mode={ViewMode} content={ContentView} suppress={Suppress} key={CacheKey}");
                return;
            }

            if (Suppress)
            {
                // During async preparation keep the UI thread free: never sync-build frames here.
                bool DrewFrame = false;
                if (_PresentedFrame != null)
                {
                    Context.DrawImage(_PresentedFrame, new Rect(0, 0, Width, Height));
                    DrewFrame = true;
                }
                else if (ViewMode == ViewMode.ThreeD)
                {
                    DrewFrame = TryDrawStale3DFrame(Context, Width, Height);
                }
                else
                {
                    DrewFrame = TryDrawStale2DFrame(Context, Width, Height);
                }

                if (!DrewFrame)
                    Context.FillRectangle(Brushes.Black, new Rect(0, 0, Width, Height));

                if (ViewMode == ViewMode.ThreeD)
                    DrawViewCubeOverlay(Context, Width, Height);

                ImportDiagnosticsLog.WriteRender(
                    "suppress-stale",
                    RenderWatch.ElapsedMilliseconds,
                    $"{Width}x{Height} presented={_PresentedFrame != null} surfaceDirty={_SurfaceDirty} key={CacheKey}");
                return;
            }

            ImportDiagnosticsLog.Write("Render", "SyncBuildAndStoreFrame START", $"{Width}x{Height} key={CacheKey}");
            Stopwatch SyncBuildWatch = Stopwatch.StartNew();
            using (_BackgroundPrewarm.BeginUiPauseScope("sync-render"))
            {
                bool LockEntered = TryEnterPrepareLockForInteractiveRender();
                if (!LockEntered)
                {
                    CancelBackgroundPrewarm();
                    ImportDiagnosticsLog.Write("Prewarm", "PreemptForSyncRender", CacheKey);
                    LockEntered = TryEnterPrepareLockForInteractiveRender();
                }

                if (!LockEntered)
                {
                    ImportDiagnosticsLog.WriteRender(
                        "sync-build-lock-timeout",
                        SyncBuildWatch.ElapsedMilliseconds,
                        $"{Width}x{Height} key={CacheKey}");
                    if (!TryDrawStale2DFrame(Context, Width, Height))
                    {
                        if (_PresentedFrame != null)
                        {
                            Context.DrawImage(_PresentedFrame, new Rect(0, 0, Width, Height));
                            DrawViewCubeOverlay(Context, Width, Height);
                        }
                        else
                            Context.FillRectangle(Brushes.Black, new Rect(0, 0, Width, Height));
                    }

                    return;
                }

                try
                {
                    BuildAndStoreFrame(Snapshot);
                }
                finally
                {
                    Monitor.Exit(_PrepareLock);
                }
            }

            SyncBuildWatch.Stop();

            TestSyncFrameBuildCount++;
            ImportViewerHandoffTelemetry.Recorder.Record(
                _ImportHandoffCompleteForTests
                    ? ImportViewerHandoffEventKind.SyncFrameBuildAfterHandoff
                    : ImportViewerHandoffEventKind.SyncFrameBuildDuringSuppress);
            ImportDiagnosticsLog.WriteRender(
                "sync-build",
                SyncBuildWatch.ElapsedMilliseconds,
                $"{Width}x{Height} mode={ViewMode} content={ContentView} key={CacheKey} total={RenderWatch.ElapsedMilliseconds}ms");
            WriteImportDiagnostics("after-sync-build");

            FieldContentView RefreshedContentView = ResolveContentView(Snapshot.Project, Snapshot.ContentView);
            MaterializeCachedLayerBitmaps(Snapshot.ViewMode, RefreshedContentView, CacheKey, Width, Height);
            if (TryDrawCompositeCachedFrames(Context, ViewMode, RefreshedContentView, CacheKey, Width, Height))
            {
                if (ViewMode == ViewMode.ThreeD)
                    _LastSuccessful3DCacheKey = CacheKey;

                MaybeScheduleDeferredBackgroundPrewarm();
                TestRecordFirstRenderAfterHandoff(UsedSyncFallback: true);
                ImportDiagnosticsLog.WriteRender(
                    "sync-build-draw",
                    RenderWatch.ElapsedMilliseconds,
                    $"{Width}x{Height} key={CacheKey}");
                return;
            }

            if (_PresentedFrame != null)
            {
                Context.DrawImage(_PresentedFrame, new Rect(0, 0, Width, Height));
                DrawViewCubeOverlay(Context, Width, Height);
            }
            else
                Context.FillRectangle(Brushes.Black, new Rect(0, 0, Width, Height));

            TestRecordFirstRenderAfterHandoff(UsedSyncFallback: true);
            ImportDiagnosticsLog.WriteRender(
                "sync-build-fallback-draw",
                RenderWatch.ElapsedMilliseconds,
                $"{Width}x{Height} presented={_PresentedFrame != null} key={CacheKey}");
        }

        /// <summary>
        /// Records the first post-handoff render milestone for regression tests.
        /// </summary>
        private void TestRecordFirstRenderAfterHandoff
            (
            bool UsedSyncFallback
            )
        {
            if (!_ImportHandoffCompleteForTests || TestFirstRenderAfterHandoffObserved)
                return;

            TestFirstRenderAfterHandoffObserved = true;
            ImportViewerHandoffTelemetry.Recorder.Record(
                ImportViewerHandoffEventKind.FirstRenderAfterHandoff,
                Detail: UsedSyncFallback ? "sync-fallback" : "composite");
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
        /// Draws cached base and topo layers with the current surface opacity.
        /// </summary>
        private bool TryDrawCompositeCachedFrames
            (
            DrawingContext Context,
            ViewMode Mode,
            FieldContentView ContentView,
            string CacheKey,
            int Width,
            int Height
            )
        {
            if (Mode == ViewMode.ThreeD)
            {
                WriteableBitmap? MonolithicFrame = TryGetCachedMonolithicFrame(Mode, ContentView, CacheKey, Width, Height);
                if (MonolithicFrame == null)
                    return false;

                _PresentedFrame = MonolithicFrame;
                Context.DrawImage(MonolithicFrame, new Rect(0, 0, Width, Height));
                DrawViewCubeOverlay(Context, Width, Height);
                RememberStale3DFrames(Mode, MonolithicFrame, null, null, null);
                return true;
            }

            WriteableBitmap? BaseFrame = TryGetCachedFrame(Mode, ContentView, CacheKey, Width, Height);
            WriteableBitmap? TopoFrame = TryGetCachedTopoFrame(Mode, ContentView, CacheKey, Width, Height);
            WriteableBitmap? DecorationsFrame = TryGetCachedDecorationsFrame(Mode, ContentView, CacheKey, Width, Height);
            if ((BaseFrame == null) || (TopoFrame == null) || (DecorationsFrame == null))
                return false;

            _PresentedFrame = BaseFrame;
            double Opacity = Math.Clamp(_Display.TopoCutFillOpacityPercent, MIN_OPACITY_UNIT, OPACITY_PERCENT_TO_UNIT) / OPACITY_PERCENT_TO_UNIT;
            Context.DrawImage(BaseFrame, new Rect(0, 0, Width, Height));
            if (Opacity > MIN_OPACITY_UNIT)
            {
                using (Context.PushOpacity(Opacity))
                    Context.DrawImage(TopoFrame, new Rect(0, 0, Width, Height));
            }

            if (DecorationsFrame != null)
                Context.DrawImage(DecorationsFrame, new Rect(0, 0, Width, Height));

            DrawViewCubeOverlay(Context, Width, Height);
            RememberStale3DFrames(Mode, null, BaseFrame, TopoFrame, DecorationsFrame);
            return true;
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
            int Height,
            bool AllowMaterialize = true
            )
        {
            return TryGetCachedOverlayFrame(
                Mode,
                ContentView,
                CacheKey,
                Width,
                Height,
                Mode == ViewMode.TwoD ? _FrameCaches2D : _FrameCaches3D,
                Mode == ViewMode.TwoD ? _FramePixelCaches2D : _FramePixelCaches3D,
                AllowMaterialize);
        }

        /// <summary>
        /// Returns a cached monolithic 3D frame bitmap when the fingerprint matches the current view state.
        /// </summary>
        private WriteableBitmap? TryGetCachedMonolithicFrame
            (
            ViewMode Mode,
            FieldContentView ContentView,
            string CacheKey,
            int Width,
            int Height,
            bool AllowMaterialize = true
            )
        {
            int Index = ContentViewCacheIndex(ContentView);
            if (Index < 0)
                return null;

            if (TryEnterPrepareLockOnUi(ViewerPrepareLock.UiAcquireTimeoutMs))
            {
                try
                {
                    if (_FrameMonolithicPixelCaches3D[Index] is RenderedFramePacket Packet &&
                        Math.Abs(Packet.TopoOverlayOpacityPercent - _Display.TopoCutFillOpacityPercent) > TOPO_OPACITY_CACHE_TOLERANCE)
                    {
                        return null;
                    }
                }
                finally
                {
                    Monitor.Exit(_PrepareLock);
                }
            }

            return TryGetCachedOverlayFrame(
                Mode,
                ContentView,
                CacheKey,
                Width,
                Height,
                _FrameMonolithicCaches3D,
                _FrameMonolithicPixelCaches3D,
                AllowMaterialize);
        }

        /// <summary>
        /// Returns a cached topo overlay bitmap when the fingerprint matches the current view state.
        /// </summary>
        private WriteableBitmap? TryGetCachedTopoFrame
            (
            ViewMode Mode,
            FieldContentView ContentView,
            string CacheKey,
            int Width,
            int Height,
            bool AllowMaterialize = true
            )
        {
            return TryGetCachedOverlayFrame(
                Mode,
                ContentView,
                CacheKey,
                Width,
                Height,
                Mode == ViewMode.TwoD ? _FrameTopoCaches2D : _FrameTopoCaches3D,
                Mode == ViewMode.TwoD ? _FrameTopoPixelCaches2D : _FrameTopoPixelCaches3D,
                AllowMaterialize);
        }

        /// <summary>
        /// Returns a cached decorations overlay bitmap when the fingerprint matches the current view state.
        /// </summary>
        private WriteableBitmap? TryGetCachedDecorationsFrame
            (
            ViewMode Mode,
            FieldContentView ContentView,
            string CacheKey,
            int Width,
            int Height,
            bool AllowMaterialize = true
            )
        {
            return TryGetCachedOverlayFrame(
                Mode,
                ContentView,
                CacheKey,
                Width,
                Height,
                Mode == ViewMode.TwoD ? _FrameDecorationsCaches2D : _FrameDecorationsCaches3D,
                Mode == ViewMode.TwoD ? _FrameDecorationsPixelCaches2D : _FrameDecorationsPixelCaches3D,
                AllowMaterialize);
        }

        /// <summary>
        /// Returns a cached overlay bitmap when already materialized; never creates one.
        /// </summary>
        private WriteableBitmap? TryGetMaterializedOverlayBitmap
            (
            ViewMode Mode,
            FieldContentView ContentView,
            string CacheKey,
            int Width,
            int Height,
            WriteableBitmap?[] Caches
            )
        {
            int Index = ContentViewCacheIndex(ContentView);
            if (Index < 0)
                return null;

            if (!TryEnterPrepareLockOnUi(ViewerPrepareLock.UiAcquireTimeoutMs))
                return null;

            try
            {
                string[] Keys = Mode == ViewMode.TwoD ? _FrameCacheKeys2D : _FrameCacheKeys3D;
                if (Keys[Index] != CacheKey ||
                    !IsSatelliteFrameCacheValid(Mode, Index, _Display.Satellite))
                {
                    return null;
                }

                WriteableBitmap? CachedBitmap = Caches[Index];
                if ((CachedBitmap != null) &&
                    (CachedBitmap.PixelSize.Width == Width) &&
                    (CachedBitmap.PixelSize.Height == Height))
                {
                    return CachedBitmap;
                }
            }
            finally
            {
                Monitor.Exit(_PrepareLock);
            }

            return null;
        }

        /// <summary>
        /// Returns a cached overlay bitmap from the supplied cache slots.
        /// </summary>
        private WriteableBitmap? TryGetCachedOverlayFrame
            (
            ViewMode Mode,
            FieldContentView ContentView,
            string CacheKey,
            int Width,
            int Height,
            WriteableBitmap?[] Caches,
            RenderedFramePacket?[] PixelCaches,
            bool AllowMaterialize = true
            )
        {
            int Index = ContentViewCacheIndex(ContentView);
            if (Index < 0)
                return null;

            if (!Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
                return null;

            RenderedFramePacket? PixelPacket = null;

            if (!TryEnterPrepareLockOnUi(ViewerPrepareLock.UiAcquireTimeoutMs))
                return null;

            try
            {
                string[] Keys = Mode == ViewMode.TwoD ? _FrameCacheKeys2D : _FrameCacheKeys3D;
                if (Keys[Index] != CacheKey ||
                    !IsSatelliteFrameCacheValid(Mode, Index, _Display.Satellite))
                {
                    return null;
                }

                WriteableBitmap? CachedBitmap = Caches[Index];
                if ((CachedBitmap != null) &&
                    (CachedBitmap.PixelSize.Width == Width) &&
                    (CachedBitmap.PixelSize.Height == Height))
                {
                    return CachedBitmap;
                }

                if (AllowMaterialize &&
                    PixelCaches[Index] is RenderedFramePacket Packet &&
                    Packet.Width == Width &&
                    Packet.Height == Height)
                {
                    PixelPacket = Packet;
                }
            }
            finally
            {
                Monitor.Exit(_PrepareLock);
            }

            if (!PixelPacket.HasValue)
                return null;

            RenderedFramePacket PacketToMaterialize = PixelPacket.Value;
            WriteableBitmap Materialized;
            try
            {
                Materialized = CreateWriteableBitmapFromBgra(PacketToMaterialize.Pixels, Width, Height);
            }
            catch (Exception)
            {
                if (TryEnterPrepareLockOnUi(ViewerPrepareLock.UiAcquireTimeoutMs))
                {
                    try
                    {
                        PixelCaches[Index] = null;
                        Caches[Index] = null;
                    }
                    finally
                    {
                        Monitor.Exit(_PrepareLock);
                    }
                }

                return null;
            }

            if (!TryEnterPrepareLockOnUi(ViewerPrepareLock.UiAcquireTimeoutMs))
                return null;

            try
            {
                string[] Keys = Mode == ViewMode.TwoD ? _FrameCacheKeys2D : _FrameCacheKeys3D;
                if (Keys[Index] != CacheKey ||
                    !IsSatelliteFrameCacheValid(Mode, Index, _Display.Satellite))
                {
                    return null;
                }

                if (PixelCaches[Index] is not RenderedFramePacket CurrentPacket ||
                    CurrentPacket.Width != Width ||
                    CurrentPacket.Height != Height ||
                    !ReferenceEquals(CurrentPacket.Pixels, PacketToMaterialize.Pixels))
                {
                    return null;
                }

                WriteableBitmap? Existing = Caches[Index];
                if ((Existing != null) &&
                    (Existing.PixelSize.Width == Width) &&
                    (Existing.PixelSize.Height == Height))
                {
                    return Existing;
                }

                Caches[Index] = Materialized;
                return Materialized;
            }
            finally
            {
                Monitor.Exit(_PrepareLock);
            }
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
            StoreFrameCacheForMode(ViewMode, ResolveContentView(), CacheKey, Frame, _Display.Satellite);
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
            return CreateWriteableBitmapFromBgra(CopySkiaPixelsToBuffer(FrameBitmap, Width, Height), Width, Height);
        }

        /// <summary>
        /// Copies BGRA8888 pixels from a Skia bitmap into a managed buffer.
        /// </summary>
        private static byte[] CopySkiaPixelsToBuffer
            (
            SKBitmap FrameBitmap,
            int Width,
            int Height
            )
        {
            byte[] Buffer = new byte[Width * Height * BYTES_PER_PIXEL];
            using SKPixmap Pixmap = FrameBitmap.PeekPixels();
            IntPtr SrcPixels = Pixmap.GetPixels();
            if (SrcPixels == IntPtr.Zero)
                return Buffer;

            int BytesPerRow = Math.Min(Pixmap.RowBytes, Width * BYTES_PER_PIXEL);
            if (Pixmap.RowBytes == Width * BYTES_PER_PIXEL)
            {
                Marshal.Copy(SrcPixels, Buffer, 0, Buffer.Length);
                return Buffer;
            }

            for (int Row = 0; Row < Height; Row++)
            {
                Marshal.Copy(
                    SrcPixels + (Row * Pixmap.RowBytes),
                    Buffer,
                    Row * Width * BYTES_PER_PIXEL,
                    BytesPerRow);
            }

            return Buffer;
        }

        /// <summary>
        /// Creates an Avalonia writeable bitmap from a BGRA8888 premultiplied buffer.
        /// </summary>
        private static WriteableBitmap CreateWriteableBitmapFromBgra
            (
            byte[] Pixels,
            int Width,
            int Height
            )
        {
            PixelSize PixelSize = new PixelSize(Width, Height);
            WriteableBitmap AvaloniaFrame = new WriteableBitmap(
                PixelSize,
                new Vector(BITMAP_DPI, BITMAP_DPI),
                PixelFormat.Bgra8888,
                AlphaFormat.Premul);
            using (ILockedFramebuffer Locked = AvaloniaFrame.Lock())
            {
                int BytesPerRow = Math.Min(Width * BYTES_PER_PIXEL, Locked.RowBytes);
                int TotalBytes = BytesPerRow * Height;
                if (Locked.RowBytes == Width * BYTES_PER_PIXEL)
                {
                    Marshal.Copy(Pixels, 0, Locked.Address, Math.Min(Pixels.Length, TotalBytes));
                }
                else
                {
                    for (int Row = 0; Row < Height; Row++)
                    {
                        Marshal.Copy(
                            Pixels,
                            Row * Width * BYTES_PER_PIXEL,
                            Locked.Address + (Row * Locked.RowBytes),
                            BytesPerRow);
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
                return (_Surface.MinLat + _Surface.MaxLat) * FIELD_CENTRE_FRACTION;
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
                return (_Surface.MinLon + _Surface.MaxLon) * FIELD_CENTRE_FRACTION;
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
            Point Position = Args.GetPosition(this);
            PointerPointProperties Props = Args.GetCurrentPoint(this).Properties;
            string Button = Props.IsLeftButtonPressed ? "left"
                : Props.IsRightButtonPressed ? "right"
                : Props.IsMiddleButtonPressed ? "middle"
                : "other";
            ImportDiagnosticsLog.WritePointer(
                "Pressed",
                Position.X,
                Position.Y,
                Button,
                $"mode={ViewMode} suppress={SuppressSynchronousPrepare} importBlocked={_ImportInputBlocked}");

            if (_ImportInputBlocked)
            {
                Args.Handled = true;
                return;
            }

            if (Props.IsLeftButtonPressed && (ViewMode == ViewMode.ThreeD))
            {
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
                if (ViewMode == ViewMode.ThreeD)
                    BeginLayered3DInteractionDefer();

                _IsDragging = true;
                _LastPointer = Args.GetPosition(this);
                Args.Pointer.Capture(this);
            }
            else if (Props.IsRightButtonPressed && (ViewMode == ViewMode.ThreeD))
            {
                BeginLayered3DInteractionDefer();
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
                Point HoverPosition = Args.GetPosition(this);
                if (_ImportInputBlocked)
                {
                    Args.Handled = true;
                    return;
                }

                ImportDiagnosticsLog.WritePointerMoveThrottled(HoverPosition.X, HoverPosition.Y, $"mode={ViewMode}");
                UpdateViewCubeHover(HoverPosition);
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
            Point Position = Args.GetPosition(this);
            ImportDiagnosticsLog.WritePointer(
                "Released",
                Position.X,
                Position.Y,
                Detail: $"dragging={_IsDragging} rightDrag={_IsRightDragging} mode={ViewMode} importBlocked={_ImportInputBlocked}");
            if (_ImportInputBlocked)
            {
                Args.Handled = true;
                return;
            }

            _IsDragging = false;
            _IsRightDragging = false;
            Args.Pointer.Capture(null);
            if (ViewMode == ViewMode.ThreeD)
                EndLayered3DInteractionDefer();
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
            int Width = Math.Max(MIN_VIEWPORT_DIMENSION, (int)Bounds.Width);
            int Height = Math.Max(MIN_VIEWPORT_DIMENSION, (int)Bounds.Height);
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
            Point Position = Args.GetPosition(this);
            if (_ImportInputBlocked)
            {
                ImportDiagnosticsLog.WritePointer(
                    "WheelBlocked",
                    Position.X,
                    Position.Y,
                    Detail: $"delta={Delta:0.##} mode={ViewMode}");
                Args.Handled = true;
                return;
            }

            ImportDiagnosticsLog.WritePointer(
                "Wheel",
                Position.X,
                Position.Y,
                Detail: $"delta={Delta:0.##} mode={ViewMode}");
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
                ScheduleLayered3DDeferRelease();
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
            int Width = Math.Max(MIN_VIEWPORT_DIMENSION, (int)Bounds.Width);
            int Height = Math.Max(MIN_VIEWPORT_DIMENSION, (int)Bounds.Height);
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
            BeginLayered3DInteractionDefer();

            if (_ViewCubeAnimTimer == null)
            {
                _ViewCubeAnimTimer = new Avalonia.Threading.DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(1000.0 / VIEW_CUBE_ANIMATION_FPS)
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

            int Width = Math.Max(MIN_VIEWPORT_DIMENSION, (int)Bounds.Width);
            int Height = Math.Max(MIN_VIEWPORT_DIMENSION, (int)Bounds.Height);
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
            double LinearT = Math.Clamp(ElapsedMs / VIEW_CUBE_ANIMATION_MS, MIN_OPACITY_UNIT, ANIMATION_EASE_IN_FACTOR);
            double T = ANIMATION_EASE_IN_FACTOR - ((ANIMATION_EASE_IN_FACTOR - LinearT) * (ANIMATION_EASE_IN_FACTOR - LinearT));

            OrbitHeadingDeg = LerpHeadingDegrees(_ViewCubeAnimStartHeadingDeg, _ViewCubeAnimTargetHeadingDeg, T);
            OrbitPitchDeg = _ViewCubeAnimStartPitchDeg + ((_ViewCubeAnimTargetPitchDeg - _ViewCubeAnimStartPitchDeg) * T);
            RaiseViewCameraChanged();
            InvalidateVisual();

            if (LinearT >= ANIMATION_EASE_IN_FACTOR)
            {
                OrbitHeadingDeg = _ViewCubeAnimTargetHeadingDeg;
                OrbitPitchDeg = _ViewCubeAnimTargetPitchDeg;
                _ViewCubeAnimTimer?.Stop();
                EndLayered3DInteractionDefer();
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
            double Delta = ((ToDeg - FromDeg + LERP_HEADING_WRAP_OFFSET_DEG) % DEGREES_IN_FULL_CIRCLE) - LERP_HEADING_HALF_CIRCLE_DEG;
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

            int Width = Math.Max(MIN_VIEWPORT_DIMENSION, (int)Bounds.Width);
            int Height = Math.Max(MIN_VIEWPORT_DIMENSION, (int)Bounds.Height);
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
