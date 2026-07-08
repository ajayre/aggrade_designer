using System.Globalization;

using AgGradeDesigner.Core;
using AgGradeDesigner.Core.Color;
using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Haul;
using AgGradeDesigner.Core.MapTools;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Core.Survey;
using AgGradeDesigner.Services;

using SkiaSharp;

namespace AgGradeDesigner.Rendering
{
    /// <summary>
    /// Which parts of the field view to render into an offscreen layer.
    /// </summary>
    public enum FieldRenderPass
    {
        /// <summary>Background imagery and sky only.</summary>
        Base,

        /// <summary>Coloured topo or cut/fill surface only on a transparent background.</summary>
        SurfaceOverlay,

        /// <summary>Borders and point overlays drawn above the surface (contours are on <see cref="SurfaceOverlay"/>).</summary>
        Decorations,

        /// <summary>Full 3D scene in one pass (used at 100% topo opacity for orbit performance).</summary>
        Monolithic
    }

    /// <summary>
    /// Skia drawing routines for the field viewer (§2).
    /// </summary>
    static public class FieldRenderer
    {
        /// <summary>Screen size of benchmark triangle markers in pixels.</summary>
        private const float BENCHMARK_TRIANGLE_SIZE = 8f;
        /// <summary>Stroke width for field boundary in 2D view.</summary>
        private const float FIELD_BORDER_STROKE_WIDTH_2D = 3.0f;
        /// <summary>Stroke width for field boundary in 3D view.</summary>
        private const float FIELD_BORDER_STROKE_WIDTH_3D = 2.5f;
        /// <summary>Half-span of the satellite plinth square in metres (one mile).</summary>
        private const double SATELLITE_PLINTH_HALF_SPAN_M = RenderingConstants.METRES_PER_MILE;
        /// <summary>Fraction of horizontal extent used for plinth side thickness.</summary>
        private const double SATELLITE_PLINTH_THICKNESS_FRACTION = 0.012;
        /// <summary>Minimum plinth side thickness in model units.</summary>
        private const double SATELLITE_PLINTH_MIN_MODEL_THICKNESS = 1.5;
        /// <summary>Default map background when satellite is off.</summary>
        private static readonly SKColor MAP_BACKGROUND_COLOR = SKColors.White;
        /// <summary>Background colour for satellite ground plane in 3D.</summary>
        private static readonly SKColor SATELLITE_GROUND_BACKGROUND_COLOR = new SKColor(RenderingConstants.SATELLITE_GROUND_BACKGROUND_RED, RenderingConstants.SATELLITE_GROUND_BACKGROUND_GREEN, RenderingConstants.SATELLITE_GROUND_BACKGROUND_BLUE);
        /// <summary>Background colour for the 2D satellite basemap while tiles load.</summary>
        private static readonly SKColor SATELLITE_2D_BACKGROUND_COLOR = new SKColor(RenderingConstants.SATELLITE_2D_BACKGROUND_RED, RenderingConstants.SATELLITE_2D_BACKGROUND_GREEN, RenderingConstants.SATELLITE_2D_BACKGROUND_BLUE);
        /// <summary>Sky gradient colour at the horizon line.</summary>
        private static readonly SKColor SKY_HORIZON_COLOR = new SKColor(186, 214, 244);
        /// <summary>Sky gradient colour at the top of the viewport.</summary>
        private static readonly SKColor SKY_ZENITH_COLOR = new SKColor(106, 158, 210);
        /// <summary>Stroke colour for plinth side edges.</summary>
        private static readonly SKColor PLINTH_EDGE_COLOR = SKColors.Black;
        /// <summary>Fallback fill when a plinth top tile bitmap is missing.</summary>
        private static readonly SKColor PLINTH_TOP_FALLBACK_COLOR = new SKColor(118, 132, 72);
        /// <summary>Fill colour for survey point markers.</summary>
        private static readonly SKColor SURVEY_POINT_COLOR = new SKColor(30, 30, 30);
        /// <summary>Text colour for benchmark labels.</summary>
        private static readonly SKColor ANNOTATION_TEXT_COLOR = new SKColor(30, 30, 30);
        /// <summary>Fill colour for benchmark triangle markers.</summary>
        private static readonly SKColor BENCHMARK_FILL_COLOR = new SKColor(212, 160, 23);

        /// <summary>Radius of survey point circles in pixels.</summary>
        private const float SURVEY_POINT_RADIUS = 2f;
        /// <summary>Horizontal offset of benchmark labels from marker.</summary>
        private const float BENCHMARK_LABEL_OFFSET_X = 6f;
        /// <summary>Vertical offset of benchmark labels from marker.</summary>
        private const float BENCHMARK_LABEL_OFFSET_Y = 4f;
        /// <summary>Font size for benchmark labels.</summary>
        private const float BENCHMARK_LABEL_FONT_SIZE = 11f;
        /// <summary>Stroke colour for benchmark triangle outlines.</summary>
        private static readonly SKColor BENCHMARK_STROKE_COLOR = SKColors.White;
        /// <summary>Stroke width for benchmark triangle outlines in pixels.</summary>
        private const float BENCHMARK_TRIANGLE_STROKE_WIDTH = 1.5f;
        /// <summary>Label colour used when the background under the name is dark.</summary>
        private static readonly SKColor BENCHMARK_LABEL_LIGHT_COLOR = SKColors.White;
        /// <summary>Relative luminance threshold below which benchmark labels switch to white.</summary>
        private const double BENCHMARK_LABEL_DARK_BACKGROUND_LUMINANCE_THRESHOLD = 0.45;
        /// <summary>Width factor for equilateral benchmark triangles (√3/2).</summary>
        private const float EQUILATERAL_TRIANGLE_WIDTH_FACTOR = 0.866f;
        /// <summary>Height factor for equilateral benchmark triangles.</summary>
        private const float EQUILATERAL_TRIANGLE_HEIGHT_FACTOR = (float)GeoConstants.VIEWPORT_CENTRE_FRACTION;
        /// <summary>Minimum grid width or height for valid mesh tessellation.</summary>
        private const int MIN_GRID_DIMENSION = 2;
        /// <summary>Vertices in one screen-space quad.</summary>
        private const int QUAD_VERTEX_COUNT = 4;
        /// <summary>Vertices in one triangle.</summary>
        private const int TRIANGLE_VERTEX_COUNT = 3;
        /// <summary>Index count when splitting a quad into two triangles.</summary>
        private const int TRIANGLE_INDEX_COUNT = 6;
        /// <summary>Maximum subdivisions per colormap cell in 2D.</summary>
        private const int MAX_COLORMAP_SUBDIVISIONS = 32;
        /// <summary>Divisor used to choose colormap subdivision count from grid size.</summary>
        private const int COLORMAP_SUBDIVISION_DIVISOR = 8;
        /// <summary>Multiplier for plinth diagonal when selecting tile coverage.</summary>
        private const double PLINTH_DIAGONAL_SIDE_MULTIPLIER = 2.0;
        /// <summary>Triangles emitted per sampled grid quad in 3D mesh.</summary>
        private const int MESH_TRIANGLES_PER_QUAD = 2;
        /// <summary>Model Z of the plinth top and flat ground plane.</summary>
        private const double PLINTH_TOP_MODEL_Z = 0.0;
        /// <summary>Fraction of four depths averaged for quad sort key.</summary>
        private const double AVERAGE_DEPTH_FRACTION = 0.25;
        /// <summary>Fraction of three depths averaged for triangle sort key.</summary>
        private const double TRIANGLE_DEPTH_AVERAGE_FRACTION = 1.0 / 3.0;
        /// <summary>Divisor converting opacity percent to 0–1 unit range.</summary>
        private const double OPACITY_PERCENT_TO_UNIT = 100.0;
        /// <summary>Minimum opacity in unit range.</summary>
        private const double MIN_OPACITY_UNIT = 0.0;
        /// <summary>Maximum alpha channel value for colour unpacking.</summary>
        private const double ALPHA_CHANNEL_MAX = 255.0;
        /// <summary>Maximum allowed projected quad area as multiple of viewport area.</summary>
        private const double SCREEN_QUAD_MAX_AREA_MULTIPLIER = 2.0;
        /// <summary>Minimum projected quad area before span-based fallback.</summary>
        private const double MIN_PROJECTED_AREA = 1.0;
        /// <summary>Minimum screen span in pixels for usable quads.</summary>
        private const float MIN_SCREEN_SPAN = 1f;
        /// <summary>Tile screen span below which no subdivision is used.</summary>
        private const float TILE_SCREEN_SPAN_LOW_THRESHOLD = 48f;
        /// <summary>Tile screen span below which mid subdivision is used.</summary>
        private const float TILE_SCREEN_SPAN_MID_THRESHOLD = 160f;
        /// <summary>Subdivisions per tile edge when span is below low threshold.</summary>
        private const int TILE_SCREEN_SPAN_LOW_SUBDIVISIONS = 1;
        /// <summary>Subdivisions per tile edge when span is below mid threshold.</summary>
        private const int TILE_SCREEN_SPAN_MID_SUBDIVISIONS = 2;
        /// <summary>Pixel spacing between depth samples along a 3D line segment.</summary>
        private const double DEPTH_SEGMENT_SAMPLE_SPACING = 3.0;
        /// <summary>Minimum depth sample steps along a segment.</summary>
        private const int DEPTH_SEGMENT_MIN_STEPS = 2;
        /// <summary>Maximum depth sample steps along a segment.</summary>
        private const int DEPTH_SEGMENT_MAX_STEPS = 64;
        /// <summary>Distance below which consecutive depth samples are merged.</summary>
        private const float DEPTH_SAMPLE_JOIN_EPSILON = 1e-3f;
        /// <summary>Minimum triangle area for depth-buffer rasterization.</summary>
        private const double TRIANGLE_RASTERIZE_AREA_EPSILON = 1e-6;
        /// <summary>Coordinate tolerance when matching boundary vertices.</summary>
        private const double BOUNDARY_COORDINATE_EPSILON = 1e-8;
        /// <summary>Base mix weight for hillshade modulation.</summary>
        private const double HILLSHADE_BASE_MIX = 0.32;
        /// <summary>Range mix weight for hillshade modulation.</summary>
        private const double HILLSHADE_RANGE_MIX = 0.68;
        /// <summary>Maximum byte value for colour channel unpacking.</summary>
        private const byte COLOR_CHANNEL_MAX_BYTE = 255;
        /// <summary>Bit shift for alpha channel in ARGB packing.</summary>
        private const int ARGB_ALPHA_SHIFT = 24;
        /// <summary>Bit shift for red channel in ARGB packing.</summary>
        private const int ARGB_RED_SHIFT = 16;
        /// <summary>Bit shift for green channel in ARGB packing.</summary>
        private const int ARGB_GREEN_SHIFT = 8;
        /// <summary>Mask for one byte in ARGB unpacking.</summary>
        private const int ARGB_CHANNEL_MASK = 0xFF;
        /// <summary>Factor in Web Mercator latitude conversion.</summary>
        private const double WEB_MERCATOR_PI_FACTOR = 4.0;
        /// <summary>Inverse scale factor in Web Mercator latitude conversion.</summary>
        private const double WEB_MERCATOR_INVERSE_SCALE = 2.0;
        /// <summary>Initial triangle list capacity when grid size is unknown.</summary>
        private const int MESH_INITIAL_CAPACITY = 16;
        /// <summary>Coarse mesh sampling step for large grids.</summary>
        private const int MESH_SAMPLING_COARSE_STEP = 2;
        /// <summary>Progress percent indicating render pass complete.</summary>
        private const int PROGRESS_COMPLETE_PERCENT = 100;
        /// <summary>Progress percent at start of a render pass.</summary>
        private const int PROGRESS_START_PERCENT = 0;
        /// <summary>Progress percent when surface drawing begins in 2D.</summary>
        private const int PROGRESS_SURFACE_START_PERCENT = 15;
        /// <summary>Progress percent when surface drawing ends in 2D.</summary>
        private const int PROGRESS_SURFACE_END_PERCENT = 70;
        /// <summary>Progress percent when decoration pass ends in 2D.</summary>
        private const int PROGRESS_DECORATIONS_END_PERCENT = 85;
        /// <summary>Late progress tick during 2D decoration pass.</summary>
        private const int PROGRESS_DECORATIONS_LATE_PERCENT = 88;
        /// <summary>Early progress tick during base layer draw.</summary>
        private const int PROGRESS_BASE_EARLY_PERCENT = 2;
        /// <summary>Progress percent after satellite tiles drawn in 2D.</summary>
        private const int PROGRESS_SATELLITE_PERCENT = 12;
        /// <summary>Progress percent at start of 3D overlay pass.</summary>
        private const int PROGRESS_3D_OVERLAY_START_PERCENT = 5;
        /// <summary>Progress percent when Vulkan base pass completes.</summary>
        private const int PROGRESS_3D_VULKAN_END_PERCENT = 90;
        /// <summary>Progress segment start after Vulkan base path.</summary>
        private const int PROGRESS_3D_VULKAN_SEGMENT_START_PERCENT = 75;
        /// <summary>Progress segment end for 3D decoration CPU path.</summary>
        private const int PROGRESS_3D_DECORATIONS_SEGMENT_END_PERCENT = 40;
        /// <summary>Progress percent when 3D coloured mesh completes.</summary>
        private const int PROGRESS_3D_MESH_END_PERCENT = 60;
        /// <summary>Progress percent when 3D contours complete.</summary>
        private const int PROGRESS_3D_CONTOUR_END_PERCENT = 75;
        /// <summary>Progress percent when 3D surface overlay mesh completes.</summary>
        private const int PROGRESS_3D_SURFACE_OVERLAY_MESH_END_PERCENT = 70;
        /// <summary>Progress percent when 3D overlay contours start.</summary>
        private const int PROGRESS_3D_SURFACE_OVERLAY_CONTOUR_START_PERCENT = 55;
        /// <summary>Progress percent when overlay mesh finishes before contours.</summary>
        private const int PROGRESS_3D_SURFACE_OVERLAY_CONTOUR_MESH_END_PERCENT = 55;
        /// <summary>Progress percent when monolithic 3D pass completes.</summary>
        private const int PROGRESS_3D_MONOLITHIC_END_PERCENT = 85;
        /// <summary>Progress percent when one mesh row completes.</summary>
        private const int PROGRESS_MESH_ROW_END_PERCENT = 70;
        /// <summary>Progress percent after mesh triangles are sorted.</summary>
        private const int PROGRESS_MESH_SORTED_PERCENT = 80;
        /// <summary>Progress percent when contour polylines finish.</summary>
        private const int PROGRESS_CONTOUR_POLYLINE_END_PERCENT = 80;
        /// <summary>Minimum sky rectangle height in pixels.</summary>
        private const float SKY_MIN_HEIGHT = 1f;
        /// <summary>Viewport inset when testing horizon visibility.</summary>
        private const float SKY_HORIZON_VIEWPORT_INSET = 1f;
        /// <summary>Depth tolerance when testing line visibility against the depth buffer.</summary>
        private const float DEPTH_VISIBLE_EPSILON = 0.05f;
        /// <summary>Minimum depth buffer dimension in pixels.</summary>
        private const int DEPTH_BUFFER_MIN_DIMENSION = 1;

        /// <summary>
        /// Draws the 2D field view: satellite, surface, contours, overlays, and annotations.
        /// </summary>
        static public void Draw2D
            (
            SKCanvas Canvas,
            SKRect Viewport,
            ProjectDocument? Project,
            FieldSurfaceSampler Surface,
            FieldContentView ContentView,
            DisplaySettings Display,
            double CentreLat,
            double CentreLon,
            double MetresPerPixel,
            SatelliteTileService? Satellite,
            FieldRenderPass Pass,
            out int SatellitePlaceholdersDrawn,
            IProgress<int>? Progress = null
            )
        {
            SatellitePlaceholdersDrawn = 0;
            Progress?.Report(PROGRESS_START_PERCENT);

            if (Pass == FieldRenderPass.SurfaceOverlay)
            {
                Canvas.Clear(SKColors.Transparent);
                if (Project != null && Project.HasSurvey && Surface.SampleWidth > 0)
                {
                    if (TryDraw2DWithVulkan(
                            Canvas,
                            Viewport,
                            Project,
                            Surface,
                            ContentView,
                            Display,
                            CentreLat,
                            CentreLon,
                            MetresPerPixel,
                            Satellite,
                            Pass,
                            out _,
                            Progress))
                    {
                        Progress?.Report(PROGRESS_COMPLETE_PERCENT);
                        return;
                    }

                    Surface.ComputeScalarRange(ContentView, out double VMin, out double VMax);
                    Progress?.Report(PROGRESS_SURFACE_START_PERCENT);
                    DrawSurface2D(Canvas, Viewport, Surface, ContentView, Display, CentreLat, CentreLon, MetresPerPixel, VMin, VMax, ProgressMapper.Subrange(Progress, PROGRESS_SURFACE_START_PERCENT, PROGRESS_SURFACE_END_PERCENT));
                    if (ContourVisibility.ShouldDrawContours(Display, Project, ContentView))
                        DrawContours2D(Canvas, Viewport, Surface, ContentView, CentreLat, CentreLon, MetresPerPixel, VMin, VMax, ProgressMapper.Subrange(Progress, PROGRESS_SURFACE_END_PERCENT, PROGRESS_COMPLETE_PERCENT));
                    else
                        Progress?.Report(PROGRESS_COMPLETE_PERCENT);
                }

                Progress?.Report(PROGRESS_COMPLETE_PERCENT);
                return;
            }

            if (Pass == FieldRenderPass.Decorations)
            {
                Canvas.Clear(SKColors.Transparent);
                if (Project != null && Project.HasSurvey && Surface.SampleWidth > 0)
                {
                    bool DrewBorderWithVulkan = TryDraw2DWithVulkan(
                        Canvas,
                        Viewport,
                        Project,
                        Surface,
                        ContentView,
                        Display,
                        CentreLat,
                        CentreLon,
                        MetresPerPixel,
                        Satellite,
                        Pass,
                        out _,
                        Progress);

                    Progress?.Report(PROGRESS_SURFACE_START_PERCENT);
                    if (!DrewBorderWithVulkan)
                    {
                        ProgressMapper.ReportSegment(Progress, PROGRESS_SURFACE_START_PERCENT, PROGRESS_DECORATIONS_END_PERCENT, PROGRESS_COMPLETE_PERCENT);
                        if (Project.Survey != null)
                            DrawFieldBorder2D(Canvas, Viewport, Project.Survey, Surface, CentreLat, CentreLon, MetresPerPixel);
                    }
                    else
                    {
                        Progress?.Report(PROGRESS_DECORATIONS_LATE_PERCENT);
                    }

                    if (Display.SurveyPoints && ContentView != FieldContentView.Proposed && Project.Survey != null)
                        DrawSurveyPoints2D(Canvas, Viewport, Project.Survey, CentreLat, CentreLon, MetresPerPixel);

                    if (Display.Benchmarks && Project.Survey != null)
                    {
                        double BenchmarkVMin = 0.0;
                        double BenchmarkVMax = 0.0;
                        if (Surface.SampleWidth > 0)
                            Surface.ComputeScalarRange(ContentView, out BenchmarkVMin, out BenchmarkVMax);
                        DrawBenchmarks2D(
                            Canvas,
                            Viewport,
                            Project.Survey,
                            CentreLat,
                            CentreLon,
                            MetresPerPixel,
                            Surface: Surface,
                            ContentView: ContentView,
                            VMin: BenchmarkVMin,
                            VMax: BenchmarkVMax);
                    }

                    TryDrawHaulFlowArrows2D(
                        Canvas,
                        Viewport,
                        Project,
                        Display,
                        CentreLat,
                        CentreLon,
                        MetresPerPixel);
                }

                Progress?.Report(PROGRESS_COMPLETE_PERCENT);
                ViewCompassRenderer.Draw(Canvas, Viewport, ViewMode.TwoD);
                return;
            }

            Canvas.Clear(Display.Satellite && Satellite != null ? SATELLITE_2D_BACKGROUND_COLOR : MAP_BACKGROUND_COLOR);
            Progress?.Report(PROGRESS_BASE_EARLY_PERCENT);

            if (Display.Satellite && Satellite != null)
            {
                if (TryDraw2DWithVulkan(
                        Canvas,
                        Viewport,
                        Project,
                        Surface,
                        ContentView,
                        Display,
                        CentreLat,
                        CentreLon,
                        MetresPerPixel,
                        Satellite,
                        Pass,
                        out SatellitePlaceholdersDrawn,
                        Progress))
                {
                    Progress?.Report(PROGRESS_SATELLITE_PERCENT);
                    Progress?.Report(PROGRESS_COMPLETE_PERCENT);
                    return;
                }

                SatellitePlaceholdersDrawn = DrawSatellite2D(Canvas, Viewport, CentreLat, CentreLon, MetresPerPixel, Satellite);
                Progress?.Report(PROGRESS_SATELLITE_PERCENT);
            }

            Progress?.Report(PROGRESS_COMPLETE_PERCENT);
        }

        /// <summary>
        /// When true, <see cref="Draw3D"/> uses the CPU mesh path even if Vulkan is available.
        /// </summary>
        static internal bool SuppressVulkanFor3D { get; set; }

        /// <summary>
        /// When true, <see cref="Draw2D"/> uses the CPU Skia path even if Vulkan is available.
        /// </summary>
        static internal bool SuppressVulkanFor2D { get; set; }

        /// <summary>Session tool data drawn in the decorations pass when non-null.</summary>
        static internal ToolDataSession? ToolDataOverlay { get; set; }

        /// <summary>Optional erase preview drawn interactively above cached frames.</summary>
        static internal ToolErasePreview? ToolErasePreviewOverlay { get; set; }

        /// <summary>
        /// Renders one 2D layer via Vulkan when the GPU path is available.
        /// </summary>
        static private bool TryDraw2DWithVulkan
            (
            SKCanvas Canvas,
            SKRect Viewport,
            ProjectDocument? Project,
            FieldSurfaceSampler Surface,
            FieldContentView ContentView,
            DisplaySettings Display,
            double CentreLat,
            double CentreLon,
            double MetresPerPixel,
            SatelliteTileService? Satellite,
            FieldRenderPass Pass,
            out int SatellitePlaceholdersDrawn,
            IProgress<int>? Progress = null
            )
        {
            SatellitePlaceholdersDrawn = 0;
            if (SuppressVulkanFor2D || !GraphicsRuntime.ShouldUseVulkanFor2D())
                return false;

            try
            {
                using SKPaint Paint = new SKPaint { IsAntialias = true };
                switch (Pass)
                {
                    case FieldRenderPass.Base:
                        if (Satellite == null)
                            return false;

                        if (!VulkanFieldRenderer.TryRenderBase2D(
                                Viewport,
                                CentreLat,
                                CentreLon,
                                MetresPerPixel,
                                Satellite,
                                out SKBitmap? BaseBitmap,
                                out SatellitePlaceholdersDrawn,
                                out _))
                        {
                            return false;
                        }

                        using (BaseBitmap)
                            Canvas.DrawBitmap(BaseBitmap, Viewport, Paint);
                        return true;

                    case FieldRenderPass.SurfaceOverlay:
                        if (Project == null)
                            return false;

                        if (!VulkanFieldRenderer.TryRenderSurfaceOverlay2D(
                                Viewport,
                                Project,
                                Surface,
                                ContentView,
                                Display,
                                CentreLat,
                                CentreLon,
                                MetresPerPixel,
                                Progress,
                                out SKBitmap? OverlayBitmap,
                                out _))
                        {
                            return false;
                        }

                        using (OverlayBitmap)
                            Canvas.DrawBitmap(OverlayBitmap, Viewport, Paint);
                        return true;

                    case FieldRenderPass.Decorations:
                        if (Project == null)
                            return false;

                        if (!VulkanFieldRenderer.TryRenderDecorations2D(
                                Viewport,
                                Project,
                                Surface,
                                CentreLat,
                                CentreLon,
                                MetresPerPixel,
                                out SKBitmap? DecorationsBitmap,
                                out _))
                        {
                            return false;
                        }

                        using (DecorationsBitmap)
                            Canvas.DrawBitmap(DecorationsBitmap, Viewport, Paint);
                        return true;

                    default:
                        return false;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Draws the 3D orbit view: satellite plane, mesh, contours, and annotations.
        /// </summary>
        static public void Draw3D
            (
            SKCanvas Canvas,
            SKRect Viewport,
            ProjectDocument? Project,
            FieldSurfaceSampler Surface,
            FieldContentView ContentView,
            DisplaySettings Display,
            double HeadingDeg,
            double PitchDeg,
            double DistanceM,
            double VerticalExaggeration,
            double OrbitCentreLat,
            double OrbitCentreLon,
            SatelliteTileService? Satellite,
            FieldRenderPass Pass,
            bool UsePlanViewSatellite = false,
            IProgress<int>? Progress = null,
            int MeshSamplingStepOverride = RenderingConstants.MESH_SAMPLING_STEP_AUTO,
            FieldContentView? RequestedContentView = null
            )
        {
            Progress?.Report(PROGRESS_START_PERCENT);

            if (Project == null || !Project.HasSurvey || Surface.SampleWidth == 0)
            {
                if (Pass == FieldRenderPass.SurfaceOverlay || Pass == FieldRenderPass.Decorations)
                    Canvas.Clear(SKColors.Transparent);
                else
                    Canvas.Clear(MAP_BACKGROUND_COLOR);

                Progress?.Report(PROGRESS_COMPLETE_PERCENT);
                return;
            }

            Surface.ComputeScalarRange(ContentView, out double VMin, out double VMax);
            double MinExisting = Surface.MinElevation;
            bool FlatPlane = ContentView == FieldContentView.CutFill;
            Progress?.Report(PROGRESS_BASE_EARLY_PERCENT);

            bool ShowSatellite = Display.Satellite && Satellite != null;
            FieldCamera3D MeshCamera = new FieldCamera3D(
                Surface,
                HeadingDeg,
                PitchDeg,
                DistanceM,
                VerticalExaggeration,
                OrbitCentreLat,
                OrbitCentreLon,
                ContentView,
                GroundPlaneClearanceM: ShowSatellite ? Field3DGeometryBuilder.ComputeFlatOverlayZLift(Surface) : PLINTH_TOP_MODEL_Z);

            if (Pass == FieldRenderPass.SurfaceOverlay)
            {
                Canvas.Clear(SKColors.Transparent);
                if (TryDraw3DOverlayWithVulkan(
                        Canvas,
                        Viewport,
                        Project,
                        Surface,
                        ContentView,
                        Display,
                        MeshCamera,
                        ProgressMapper.Subrange(Progress, PROGRESS_3D_OVERLAY_START_PERCENT, PROGRESS_COMPLETE_PERCENT)))
                {
                    return;
                }

                Draw3DSurfaceOverlay(
                    Canvas,
                    Viewport,
                    Project,
                    Surface,
                    ContentView,
                    Display,
                    VMin,
                    VMax,
                    FlatPlane,
                    MeshCamera,
                    ProgressMapper.Subrange(Progress, PROGRESS_3D_OVERLAY_START_PERCENT, PROGRESS_COMPLETE_PERCENT));
                return;
            }

            if (Pass == FieldRenderPass.Decorations)
            {
                Canvas.Clear(SKColors.Transparent);
                if (Project != null && Project.HasSurvey && Surface.SampleWidth > 0)
                {
                    Progress?.Report(PROGRESS_SURFACE_START_PERCENT);
                    Draw3DDecorations(
                        Canvas,
                        Viewport,
                        Project,
                        Surface,
                        ContentView,
                        Display,
                        VMin,
                        VMax,
                        FlatPlane,
                        MeshCamera,
                        ProgressMapper.Subrange(Progress, PROGRESS_SURFACE_START_PERCENT, PROGRESS_COMPLETE_PERCENT));
                }

                Progress?.Report(PROGRESS_COMPLETE_PERCENT);
                return;
            }

            if (Pass == FieldRenderPass.Monolithic)
            {
                Draw3DMonolithic(
                    Canvas,
                    Viewport,
                    Project,
                    Surface,
                    ContentView,
                    Display,
                    VMin,
                    VMax,
                    FlatPlane,
                    MeshCamera,
                    ShowSatellite ? Satellite : null,
                    UsePlanViewSatellite,
                    ProgressMapper.Subrange(Progress, PROGRESS_3D_OVERLAY_START_PERCENT, PROGRESS_COMPLETE_PERCENT));
                return;
            }

            if (TryDraw3DWithVulkan(
                    Canvas,
                    Viewport,
                    Project,
                    Surface,
                    ContentView,
                    Display,
                    MeshCamera,
                    ShowSatellite ? Satellite : null,
                    IncludeColoredMesh: false,
                    IncludeLineDecorations: false,
                    UsePlanViewSatellite,
                    ProgressMapper.Subrange(Progress, PROGRESS_3D_OVERLAY_START_PERCENT, PROGRESS_3D_VULKAN_END_PERCENT)))
            {
                ProgressMapper.ReportSegment(Progress, PROGRESS_3D_VULKAN_SEGMENT_START_PERCENT, PROGRESS_3D_VULKAN_END_PERCENT, PROGRESS_COMPLETE_PERCENT);
            }
            else
            {
                Canvas.Clear(ResolveMapBackgroundColor(ShowSatellite, UsePlanViewSatellite));
                if (ShowSatellite)
                {
                    double FieldCentreLat = (Surface.MinLat + Surface.MaxLat) * FieldConstants.EXTENT_MIDPOINT_FRACTION;
                    double FieldCentreLon = (Surface.MinLon + Surface.MaxLon) * FieldConstants.EXTENT_MIDPOINT_FRACTION;
                    DrawSky3D(Canvas, Viewport, MeshCamera, MinExisting, FieldCentreLat, FieldCentreLon);
                    DrawSatellite3DPlane(Canvas, Viewport, Surface, MinExisting, Satellite!, MeshCamera);
                    Progress?.Report(PROGRESS_SURFACE_START_PERCENT);
                }
            }

            Draw3DAnnotationOverlays(
                Canvas,
                Viewport,
                Project,
                Surface,
                ContentView,
                Display,
                MeshCamera,
                UsePlanViewSatellite);
            if (Project.Survey != null)
                DrawFieldBorder3D(Canvas, Viewport, Project.Survey, Surface, ContentView, MeshCamera, FlatPlane, MeshDepthBuffer.AlwaysVisible);

            Progress?.Report(PROGRESS_COMPLETE_PERCENT);
        }

        /// <summary>
        /// Renders coloured mesh and contour lines on a transparent background via Vulkan.
        /// </summary>
        static private bool TryDraw3DOverlayWithVulkan
            (
            SKCanvas Canvas,
            SKRect Viewport,
            ProjectDocument Project,
            FieldSurfaceSampler Surface,
            FieldContentView ContentView,
            DisplaySettings Display,
            FieldCamera3D Camera,
            IProgress<int>? Progress
            )
        {
            if (SuppressVulkanFor3D || !GraphicsRuntime.ShouldUseVulkanFor3D())
                return false;

            try
            {
                if (!VulkanFieldRenderer.TryRenderOverlay(
                        Viewport,
                        Project,
                        Surface,
                        ContentView,
                        Display,
                        Camera,
                        Progress,
                        out SKBitmap? SceneBitmap,
                        out _))
                {
                    return false;
                }

                using (SceneBitmap)
                {
                    using SKPaint ScenePaint = new SKPaint { IsAntialias = true };
                    Canvas.DrawBitmap(SceneBitmap, Viewport, ScenePaint);
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Renders the full 3D scene via Vulkan when the runtime is ready.
        /// </summary>
        static private bool TryDraw3DWithVulkan
            (
            SKCanvas Canvas,
            SKRect Viewport,
            ProjectDocument Project,
            FieldSurfaceSampler Surface,
            FieldContentView ContentView,
            DisplaySettings Display,
            FieldCamera3D Camera,
            SatelliteTileService? Satellite,
            bool IncludeColoredMesh,
            bool IncludeLineDecorations,
            bool UsePlanViewSatellite,
            IProgress<int>? Progress
            )
        {
            if (SuppressVulkanFor3D || !GraphicsRuntime.ShouldUseVulkanFor3D())
                return false;

            try
            {
                if (!VulkanFieldRenderer.TryRenderScene(
                        Viewport,
                        Project,
                        Surface,
                        ContentView,
                        Display,
                        Camera,
                        Satellite,
                        IncludeColoredMesh,
                        IncludeLineDecorations,
                        UsePlanViewSatellite,
                        Progress,
                        out SKBitmap? SceneBitmap,
                        out _))
                {
                    return false;
                }

                using (SceneBitmap)
                {
                    using SKPaint ScenePaint = new SKPaint { IsAntialias = true };
                    Canvas.DrawBitmap(SceneBitmap, Viewport, ScenePaint);
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Draws contours, borders, and point overlays above the coloured surface.
        /// </summary>
        static private void Draw3DDecorations
            (
            SKCanvas Canvas,
            SKRect Viewport,
            ProjectDocument Project,
            FieldSurfaceSampler Surface,
            FieldContentView ContentView,
            DisplaySettings Display,
            double VMin,
            double VMax,
            bool FlatPlane,
            FieldCamera3D Camera,
            IProgress<int>? Progress
            )
        {
            MeshDepthBuffer TopoDepth = MeshDepthBuffer.AlwaysVisible;
            ProgressMapper.ReportSegment(Progress, PROGRESS_START_PERCENT, PROGRESS_3D_DECORATIONS_SEGMENT_END_PERCENT, PROGRESS_COMPLETE_PERCENT);

            if (Project.Survey != null)
                DrawFieldBorder3D(Canvas, Viewport, Project.Survey, Surface, ContentView, Camera, FlatPlane, TopoDepth);

            if (Display.SurveyPoints && ContentView != FieldContentView.Proposed && Project.Survey != null)
                DrawSurveyPoints3D(Canvas, Viewport, Project.Survey, Surface, Camera);

            if (Display.Benchmarks && Project.Survey != null)
                DrawBenchmarks3D(Canvas, Viewport, Project.Survey, Surface, ContentView, Camera);

            TryDrawHaulFlowArrows3D(Canvas, Viewport, Project, Display, Surface, ContentView, Camera);

            Progress?.Report(PROGRESS_COMPLETE_PERCENT);
        }

        /// <summary>
        /// CPU fallback for contours and border when Vulkan is unavailable.
        /// </summary>
        static private void Draw3DTopoWithCpu
            (
            SKCanvas Canvas,
            SKRect Viewport,
            ProjectDocument Project,
            FieldSurfaceSampler Surface,
            FieldContentView ContentView,
            DisplaySettings Display,
            double VMin,
            double VMax,
            bool FlatPlane,
            FieldCamera3D Camera,
            bool IncludeColoredMesh,
            bool IncludeFieldBorder = true,
            IProgress<int>? Progress = null
            )
        {
            MeshDepthBuffer TopoDepth;
            bool BuildDepthBuffer = ContourVisibility.ShouldDrawContours(Display, Project, ContentView) && !FlatPlane;
            if (IncludeColoredMesh)
            {
                if (FlatPlane)
                    TopoDepth = DrawCutFillPlane3D(Canvas, Viewport, Surface, VMin, VMax, Camera, BuildDepthBuffer: false, Progress: ProgressMapper.Subrange(Progress, PROGRESS_START_PERCENT, PROGRESS_3D_MESH_END_PERCENT));
                else
                    TopoDepth = DrawMesh3D(Canvas, Viewport, Surface, ContentView, VMin, VMax, Camera, Display.Hillshade, FlatPlane: false, BuildDepthBuffer, Progress: ProgressMapper.Subrange(Progress, PROGRESS_START_PERCENT, PROGRESS_3D_MESH_END_PERCENT));
            }
            else
            {
                TopoDepth = MeshDepthBuffer.AlwaysVisible;
                ProgressMapper.ReportSegment(Progress, PROGRESS_START_PERCENT, PROGRESS_3D_MESH_END_PERCENT, PROGRESS_COMPLETE_PERCENT);
            }

            if (ContourVisibility.ShouldDrawContours(Display, Project, ContentView) && !FlatPlane)
                DrawContours3D(Canvas, Viewport, Surface, ContentView, Camera, TopoDepth, VMin, VMax, ProgressMapper.Subrange(Progress, PROGRESS_3D_MESH_END_PERCENT, PROGRESS_3D_CONTOUR_END_PERCENT));
            else
                ProgressMapper.ReportSegment(Progress, PROGRESS_3D_MESH_END_PERCENT, PROGRESS_3D_CONTOUR_END_PERCENT, PROGRESS_COMPLETE_PERCENT);

            if (IncludeFieldBorder && Project.Survey != null)
                DrawFieldBorder3D(Canvas, Viewport, Project.Survey, Surface, ContentView, Camera, FlatPlane, TopoDepth);
        }

        /// <summary>
        /// Draws only the coloured topo or cut/fill mesh for opacity compositing.
        /// </summary>
        static private void Draw3DSurfaceOverlay
            (
            SKCanvas Canvas,
            SKRect Viewport,
            ProjectDocument Project,
            FieldSurfaceSampler Surface,
            FieldContentView ContentView,
            DisplaySettings Display,
            double VMin,
            double VMax,
            bool FlatPlane,
            FieldCamera3D Camera,
            IProgress<int>? Progress
            )
        {
            MeshDepthBuffer TopoDepth;
            bool DrawContours = ContourVisibility.ShouldDrawContours(Display, Project, ContentView) && !FlatPlane;
            if (FlatPlane)
            {
                TopoDepth = DrawCutFillPlane3D(Canvas, Viewport, Surface, VMin, VMax, Camera, BuildDepthBuffer: false, Progress: ProgressMapper.Subrange(Progress, PROGRESS_START_PERCENT, PROGRESS_3D_SURFACE_OVERLAY_MESH_END_PERCENT));
            }
            else
            {
                TopoDepth = DrawMesh3D(
                    Canvas,
                    Viewport,
                    Surface,
                    ContentView,
                    VMin,
                    VMax,
                    Camera,
                    Display.Hillshade,
                    FlatPlane: false,
                    BuildDepthBuffer: DrawContours,
                    Progress: ProgressMapper.Subrange(Progress, PROGRESS_START_PERCENT, DrawContours ? PROGRESS_3D_SURFACE_OVERLAY_CONTOUR_MESH_END_PERCENT : PROGRESS_COMPLETE_PERCENT));
            }

            if (DrawContours)
                DrawContours3D(Canvas, Viewport, Surface, ContentView, Camera, TopoDepth, VMin, VMax, ProgressMapper.Subrange(Progress, PROGRESS_3D_SURFACE_OVERLAY_CONTOUR_START_PERCENT, PROGRESS_COMPLETE_PERCENT));
        }

        /// <summary>
        /// Draws the full 3D scene in one pass with GPU-applied topo/cut-fill opacity.
        /// </summary>
        static private void Draw3DMonolithic
            (
            SKCanvas Canvas,
            SKRect Viewport,
            ProjectDocument Project,
            FieldSurfaceSampler Surface,
            FieldContentView ContentView,
            DisplaySettings Display,
            double VMin,
            double VMax,
            bool FlatPlane,
            FieldCamera3D Camera,
            SatelliteTileService? Satellite,
            bool UsePlanViewSatellite,
            IProgress<int>? Progress
            )
        {
            bool ShowSatellite = Display.Satellite && Satellite != null;
            if (TryDraw3DWithVulkan(
                    Canvas,
                    Viewport,
                    Project,
                    Surface,
                    ContentView,
                    Display,
                    Camera,
                    ShowSatellite ? Satellite : null,
                    IncludeColoredMesh: true,
                    IncludeLineDecorations: true,
                    UsePlanViewSatellite,
                    ProgressMapper.Subrange(Progress, PROGRESS_START_PERCENT, PROGRESS_3D_MONOLITHIC_END_PERCENT)))
            {
                Draw3DAnnotationOverlays(
                    Canvas,
                    Viewport,
                    Project,
                    Surface,
                    ContentView,
                    Display,
                    Camera,
                    UsePlanViewSatellite);
                Progress?.Report(PROGRESS_COMPLETE_PERCENT);
                return;
            }

            double MinExisting = Surface.MinElevation;
            Canvas.Clear(ResolveMapBackgroundColor(ShowSatellite, UsePlanViewSatellite));
            if (ShowSatellite)
            {
                double FieldCentreLat = (Surface.MinLat + Surface.MaxLat) * FieldConstants.EXTENT_MIDPOINT_FRACTION;
                double FieldCentreLon = (Surface.MinLon + Surface.MaxLon) * FieldConstants.EXTENT_MIDPOINT_FRACTION;
                DrawSky3D(Canvas, Viewport, Camera, MinExisting, FieldCentreLat, FieldCentreLon);
                DrawSatellite3DPlane(Canvas, Viewport, Surface, MinExisting, Satellite!, Camera);
                Progress?.Report(PROGRESS_SURFACE_START_PERCENT);
            }

            double OverlayOpacity = Math.Clamp(Display.TopoCutFillOpacityPercent, MIN_OPACITY_UNIT, OPACITY_PERCENT_TO_UNIT) / OPACITY_PERCENT_TO_UNIT;
            if (OverlayOpacity > MIN_OPACITY_UNIT)
            {
                using SKPaint LayerPaint = new SKPaint
                {
                    Color = SKColors.White.WithAlpha((byte)(OverlayOpacity * ALPHA_CHANNEL_MAX))
                };
                Canvas.SaveLayer(LayerPaint);
                Draw3DTopoWithCpu(
                    Canvas,
                    Viewport,
                    Project,
                    Surface,
                    ContentView,
                    Display,
                    VMin,
                    VMax,
                    FlatPlane,
                    Camera,
                    IncludeColoredMesh: true,
                    IncludeFieldBorder: false,
                    ProgressMapper.Subrange(Progress, PROGRESS_SURFACE_START_PERCENT, PROGRESS_3D_MONOLITHIC_END_PERCENT));
                Canvas.Restore();
            }

            if (Project.Survey != null)
                DrawFieldBorder3D(Canvas, Viewport, Project.Survey, Surface, ContentView, Camera, FlatPlane, MeshDepthBuffer.AlwaysVisible);

            Draw3DAnnotationOverlays(
                Canvas,
                Viewport,
                Project,
                Surface,
                ContentView,
                Display,
                Camera,
                UsePlanViewSatellite);
            Progress?.Report(PROGRESS_COMPLETE_PERCENT);
        }

        /// <summary>
        /// Draws survey points, benchmarks, and session tool data that are not part of the Vulkan line pass.
        /// </summary>
        static private void Draw3DAnnotationOverlays
            (
            SKCanvas Canvas,
            SKRect Viewport,
            ProjectDocument Project,
            FieldSurfaceSampler Surface,
            FieldContentView ContentView,
            DisplaySettings Display,
            FieldCamera3D Camera,
            bool UsePlanViewSatellite
            )
        {
            if (Display.SurveyPoints && ContentView != FieldContentView.Proposed && Project.Survey != null)
                DrawSurveyPoints3D(Canvas, Viewport, Project.Survey, Surface, Camera);

            if (Display.Benchmarks && Project.Survey != null)
                DrawBenchmarks3D(Canvas, Viewport, Project.Survey, Surface, ContentView, Camera);

            TryDrawHaulFlowArrows3D(Canvas, Viewport, Project, Display, Surface, ContentView, Camera);
        }

        /// <summary>
        /// Maps WGS-84 coordinates to screen pixels for the current 2D camera.
        /// </summary>
        /// <returns>True when projection succeeds.</returns>
        static public bool LatLonToScreen
            (
            double Lat,
            double Lon,
            double CentreLat,
            double CentreLon,
            double MetresPerPixel,
            SKRect Viewport,
            out float ScreenX,
            out float ScreenY
            )
        {
            return MapProjection.LatLonToScreen(
                Lat,
                Lon,
                CentreLat,
                CentreLon,
                MetresPerPixel,
                new MapViewport { Width = Viewport.Width, Height = Viewport.Height },
                out ScreenX,
                out ScreenY);
        }

        /// <summary>
        /// Estimates ground metres per pixel for 2D pan/zoom at the given latitude.
        /// </summary>
        /// <param name="CentreLat">Map centre latitude in degrees.</param>
        /// <param name="ZoomFactor">Current zoom factor.</param>
        /// <returns>Metres per screen pixel.</returns>
        static public double EstimateMetresPerPixel
            (
            double CentreLat,
            double ZoomFactor
            )
        {
            return SatelliteMapLayout.EstimateMetresPerPixel(CentreLat, ZoomFactor);
        }

        /// <summary>
        /// Paints the plan-view colour map for the active scalar field.
        /// </summary>
        static private void DrawSurface2D
            (
            SKCanvas Canvas,
            SKRect Viewport,
            FieldSurfaceSampler Surface,
            FieldContentView ContentView,
            DisplaySettings Display,
            double CentreLat,
            double CentreLon,
            double MetresPerPixel,
            double VMin,
            double VMax,
            IProgress<int>? Progress = null
            )
        {
            bool ApplyHillshade = Display.Hillshade && ContentView != FieldContentView.CutFill;
            SKBitmap ColorMap = Surface.GetPlanViewColorMap(ContentView, VMin, VMax, ApplyHillshade, Progress);

            using SKPaint Paint = new SKPaint { IsAntialias = true };
            int Subdivisions = Math.Clamp(
                Math.Max(Surface.SampleWidth, Surface.SampleHeight) / COLORMAP_SUBDIVISION_DIVISOR,
                RenderingConstants.TILE_SUBDIVISIONS,
                MAX_COLORMAP_SUBDIVISIONS);
            DrawColormapSubdivided2D(
                Canvas,
                ColorMap,
                Surface.MinLat,
                Surface.MinLon,
                Surface.MaxLat,
                Surface.MaxLon,
                CentreLat,
                CentreLon,
                MetresPerPixel,
                Viewport,
                Subdivisions,
                Paint);
            Progress?.Report(PROGRESS_COMPLETE_PERCENT);
        }

        /// <summary>
        /// Warps the plan-view colour map through UTM screen projection so it aligns with
        /// contours, borders, and other 2D overlays (axis-aligned stretch misaligns at field edges).
        /// </summary>
        static private void DrawColormapSubdivided2D
            (
            SKCanvas Canvas,
            SKBitmap Bitmap,
            double MinLat,
            double MinLon,
            double MaxLat,
            double MaxLon,
            double CentreLat,
            double CentreLon,
            double MetresPerPixel,
            SKRect Viewport,
            int Subdivisions,
            SKPaint Paint
            )
        {
            int N = Subdivisions;
            float BmpW = Bitmap.Width;
            float BmpH = Bitmap.Height;

            float[,] ScrX = new float[N + 1, N + 1];
            float[,] ScrY = new float[N + 1, N + 1];

            for (int Row = 0; Row <= N; Row++)
            {
                double Lat = MaxLat - (double)Row / N * (MaxLat - MinLat);
                for (int Col = 0; Col <= N; Col++)
                {
                    double Lon = MinLon + (double)Col / N * (MaxLon - MinLon);
                    LatLonToScreen(Lat, Lon, CentreLat, CentreLon, MetresPerPixel, Viewport, out float Sx, out float Sy);
                    ScrX[Row, Col] = Sx;
                    ScrY[Row, Col] = Sy;
                }
            }

            using SKShader Shader = SKShader.CreateBitmap(Bitmap, SKShaderTileMode.Clamp, SKShaderTileMode.Clamp);
            Paint.Shader = Shader;

            SKPoint[] Positions = new SKPoint[QUAD_VERTEX_COUNT];
            SKPoint[] TexCoords = new SKPoint[QUAD_VERTEX_COUNT];
            ushort[] Indices = { 0, 1, 2, 0, 2, 3 };

            for (int Row = 0; Row < N; Row++)
            {
                float TexYTop = (float)Row / N * BmpH;
                float TexYBot = (float)(Row + 1) / N * BmpH;

                for (int Col = 0; Col < N; Col++)
                {
                    float TexXL = (float)Col / N * BmpW;
                    float TexXR = (float)(Col + 1) / N * BmpW;

                    Positions[0] = new SKPoint(ScrX[Row + 1, Col], ScrY[Row + 1, Col]);
                    Positions[1] = new SKPoint(ScrX[Row + 1, Col + 1], ScrY[Row + 1, Col + 1]);
                    Positions[2] = new SKPoint(ScrX[Row, Col + 1], ScrY[Row, Col + 1]);
                    Positions[3] = new SKPoint(ScrX[Row, Col], ScrY[Row, Col]);

                    TexCoords[0] = new SKPoint(TexXL, TexYBot);
                    TexCoords[1] = new SKPoint(TexXR, TexYBot);
                    TexCoords[2] = new SKPoint(TexXR, TexYTop);
                    TexCoords[3] = new SKPoint(TexXL, TexYTop);

                    Canvas.DrawVertices(SKVertexMode.Triangles, Positions, TexCoords, null, Indices, Paint);
                }
            }

            Paint.Shader = null;
        }

        /// <summary>
        /// Reads the scalar field value at an index for the active content view.
        /// </summary>
        static private double ScalarAt
            (
            FieldSurfaceSampler Surface,
            FieldContentView View,
            int Idx
            )
        {
            // Route scalar lookup through the active content view channel.
            return View switch
            {
                FieldContentView.Proposed => Surface.Proposed[Idx],
                FieldContentView.CutFill => Surface.Existing[Idx] - Surface.Proposed[Idx],
                _ => Surface.Existing[Idx]
            };
        }

        /// <summary>
        /// Draws interior survey points as white dots in 2D.
        /// </summary>
        static private void DrawSurveyPoints2D
            (
            SKCanvas Canvas,
            SKRect Viewport,
            SurveyModel Survey,
            double CentreLat,
            double CentreLon,
            double MetresPerPixel
            )
        {
            using SKPaint Paint = new SKPaint { Color = SURVEY_POINT_COLOR, IsAntialias = true, Style = SKPaintStyle.Fill };
            // Plot each interior survey point at its geographic screen position.
            foreach (TopologyPoint Pt in Survey.InteriorPoints)
            {
                LatLonToScreen(Pt.Latitude, Pt.Longitude, CentreLat, CentreLon, MetresPerPixel, Viewport, out float X, out float Y);
                Canvas.DrawCircle(X, Y, SURVEY_POINT_RADIUS, Paint);
            }
        }

        /// <summary>
        /// Draws benchmark triangles and labels in 2D.
        /// </summary>
        /// <param name="LabelBackgroundSampler">
        /// Optional screen-pixel sampler for label contrast when benchmarks are drawn on a transparent overlay.
        /// </param>
        /// <param name="Surface">Optional surface sampler for topo colour under labels in the field viewer.</param>
        /// <param name="ContentView">Active content view when <paramref name="Surface"/> is supplied.</param>
        /// <param name="VMin">Scalar minimum for topo label sampling.</param>
        /// <param name="VMax">Scalar maximum for topo label sampling.</param>
        static private void DrawBenchmarks2D
            (
            SKCanvas Canvas,
            SKRect Viewport,
            SurveyModel Survey,
            double CentreLat,
            double CentreLon,
            double MetresPerPixel,
            Func<int, int, SKColor>? LabelBackgroundSampler = null,
            FieldSurfaceSampler? Surface = null,
            FieldContentView ContentView = default,
            double VMin = 0.0,
            double VMax = 0.0
            )
        {
            using SKPaint Fill = new SKPaint { Color = BENCHMARK_FILL_COLOR, IsAntialias = true, Style = SKPaintStyle.Fill };
            using SKPaint Stroke = new SKPaint
            {
                Color = BENCHMARK_STROKE_COLOR,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = BENCHMARK_TRIANGLE_STROKE_WIDTH
            };
            using SKFont LabelFont = new SKFont { Size = BENCHMARK_LABEL_FONT_SIZE };

            // Draw gold triangles with white outlines and contrast-aware labels for each benchmark.
            foreach (Benchmark Bm in Survey.Benchmarks)
            {
                LatLonToScreen(Bm.Location.Latitude, Bm.Location.Longitude, CentreLat, CentreLon, MetresPerPixel, Viewport, out float X, out float Y);
                using SKPath Tri = BuildBenchmarkTrianglePath(X, Y);
                DrawBenchmarkTriangle(Canvas, Tri, Fill, Stroke);

                float LabelX = X + BENCHMARK_LABEL_OFFSET_X;
                float LabelY = Y - BENCHMARK_LABEL_OFFSET_Y;
                SKColor LabelColor = ResolveBenchmarkLabelColor(
                    SampleBenchmarkLabelBackground(
                        LabelX,
                        LabelY,
                        CentreLat,
                        CentreLon,
                        MetresPerPixel,
                        Viewport,
                        LabelBackgroundSampler,
                        Surface,
                        ContentView,
                        VMin,
                        VMax));
                using SKPaint LabelPaint = new SKPaint { Color = LabelColor, IsAntialias = true };
                Canvas.DrawText(Bm.Name, LabelX, LabelY, SKTextAlign.Left, LabelFont, LabelPaint);
            }
        }

        /// <summary>
        /// Builds an upward-pointing equilateral benchmark triangle path centred at the screen anchor.
        /// </summary>
        /// <param name="X">Screen X of the triangle anchor.</param>
        /// <param name="Y">Screen Y of the triangle anchor.</param>
        /// <returns>Closed triangle path.</returns>
        static private SKPath BuildBenchmarkTrianglePath
            (
            float X,
            float Y
            )
        {
            SKPath Tri = new SKPath();
            Tri.MoveTo(X, Y - BENCHMARK_TRIANGLE_SIZE);
            Tri.LineTo(X - BENCHMARK_TRIANGLE_SIZE * EQUILATERAL_TRIANGLE_WIDTH_FACTOR, Y + BENCHMARK_TRIANGLE_SIZE * EQUILATERAL_TRIANGLE_HEIGHT_FACTOR);
            Tri.LineTo(X + BENCHMARK_TRIANGLE_SIZE * EQUILATERAL_TRIANGLE_WIDTH_FACTOR, Y + BENCHMARK_TRIANGLE_SIZE * EQUILATERAL_TRIANGLE_HEIGHT_FACTOR);
            Tri.Close();
            return Tri;
        }

        /// <summary>
        /// Draws a benchmark marker triangle with fill and outline strokes.
        /// </summary>
        /// <param name="Canvas">Target canvas.</param>
        /// <param name="TrianglePath">Closed triangle path.</param>
        /// <param name="FillPaint">Fill paint for the marker body.</param>
        /// <param name="StrokePaint">Stroke paint for the white outline.</param>
        static private void DrawBenchmarkTriangle
            (
            SKCanvas Canvas,
            SKPath TrianglePath,
            SKPaint FillPaint,
            SKPaint StrokePaint
            )
        {
            Canvas.DrawPath(TrianglePath, FillPaint);
            Canvas.DrawPath(TrianglePath, StrokePaint);
        }

        /// <summary>
        /// Chooses dark or white benchmark label text based on background luminance.
        /// </summary>
        /// <param name="BackgroundColor">Colour sampled under the label anchor.</param>
        /// <returns>Label text colour with sufficient contrast.</returns>
        static private SKColor ResolveBenchmarkLabelColor
            (
            SKColor BackgroundColor
            )
        {
            double Luminance = (0.2126 * BackgroundColor.Red + 0.7152 * BackgroundColor.Green + 0.0722 * BackgroundColor.Blue) / 255.0;
            return Luminance < BENCHMARK_LABEL_DARK_BACKGROUND_LUMINANCE_THRESHOLD
                ? BENCHMARK_LABEL_LIGHT_COLOR
                : ANNOTATION_TEXT_COLOR;
        }

        /// <summary>
        /// Samples the colour under a benchmark label anchor for contrast selection.
        /// </summary>
        /// <param name="LabelX">Label anchor X in screen pixels.</param>
        /// <param name="LabelY">Label anchor Y in screen pixels.</param>
        /// <param name="CentreLat">Map centre latitude.</param>
        /// <param name="CentreLon">Map centre longitude.</param>
        /// <param name="MetresPerPixel">Ground metres per screen pixel.</param>
        /// <param name="Viewport">Viewport rectangle in pixels.</param>
        /// <param name="BackgroundSampler">Optional overlay sampler when the canvas background is transparent.</param>
        /// <param name="Surface">Optional topo surface for field-viewer label sampling.</param>
        /// <param name="ContentView">Active content view when <paramref name="Surface"/> is supplied.</param>
        /// <param name="VMin">Scalar minimum for topo label sampling.</param>
        /// <param name="VMax">Scalar maximum for topo label sampling.</param>
        /// <returns>Background colour at the label position.</returns>
        static private SKColor SampleBenchmarkLabelBackground
            (
            float LabelX,
            float LabelY,
            double CentreLat,
            double CentreLon,
            double MetresPerPixel,
            SKRect Viewport,
            Func<int, int, SKColor>? BackgroundSampler,
            FieldSurfaceSampler? Surface,
            FieldContentView ContentView,
            double VMin,
            double VMax
            )
        {
            int PixelX = (int)MathF.Round(LabelX);
            int PixelY = (int)MathF.Round(LabelY);
            if (BackgroundSampler != null)
                return BackgroundSampler(PixelX, PixelY);

            if (Surface != null && Surface.SampleWidth > 0)
            {
                MapViewport MapViewport = new MapViewport { Width = (int)Viewport.Width, Height = (int)Viewport.Height };
                if (MapProjection.ScreenToLatLon(
                        PixelX,
                        PixelY,
                        CentreLat,
                        CentreLon,
                        MetresPerPixel,
                        MapViewport,
                        out double Lat,
                        out double Lon))
                {
                    return SampleBenchmarkLabelBackgroundAtLatLon(Surface, ContentView, Lat, Lon, VMin, VMax);
                }
            }

            return SKColors.White;
        }

        /// <summary>
        /// Resolves the topo display colour at a geographic point for benchmark label contrast.
        /// </summary>
        /// <param name="Surface">Surface sampler for the active view.</param>
        /// <param name="ContentView">Active content view.</param>
        /// <param name="Lat">Label anchor latitude.</param>
        /// <param name="Lon">Label anchor longitude.</param>
        /// <param name="VMin">Scalar minimum for colour mapping.</param>
        /// <param name="VMax">Scalar maximum for colour mapping.</param>
        /// <returns>Mapped topo colour at the label anchor.</returns>
        static private SKColor SampleBenchmarkLabelBackgroundAtLatLon
            (
            FieldSurfaceSampler Surface,
            FieldContentView ContentView,
            double Lat,
            double Lon,
            double VMin,
            double VMax
            )
        {
            bool FlatPlane = ContentView == FieldContentView.CutFill;
            double Value = FlatPlane ? Surface.SampleCutFill(Lat, Lon) : Surface.SampleScalar(ContentView, Lat, Lon);
            if (double.IsNaN(Value))
                return SKColors.White;

            return ArgbToSkia(NineBandColorScale.GetColor(Value, VMin, VMax));
        }

        /// <summary>
        /// Outlines the field perimeter with a solid black stroke in 2D (OptiSurface-style).
        /// </summary>
        static private void DrawFieldBorder2D
            (
            SKCanvas Canvas,
            SKRect Viewport,
            SurveyModel Survey,
            FieldSurfaceSampler Surface,
            double CentreLat,
            double CentreLon,
            double MetresPerPixel
            )
        {
            if (Survey.BoundaryPoints.Count >= GeoConstants.MIN_POLYGON_VERTICES)
            {
                DrawBoundaryRing2D(Canvas, Viewport, BuildOrderedBoundaryLatLon(Survey), CentreLat, CentreLon, MetresPerPixel);
                return;
            }

            DrawValidRegionBorder2D(Canvas, Viewport, Surface, CentreLat, CentreLon, MetresPerPixel);
        }

        /// <summary>
        /// Traces a lat/lon ring with a solid black stroke in 2D.
        /// </summary>
        static private void DrawBoundaryRing2D
            (
            SKCanvas Canvas,
            SKRect Viewport,
            IReadOnlyList<(double Lat, double Lon)> Ring,
            double CentreLat,
            double CentreLon,
            double MetresPerPixel
            )
        {
            if (Ring.Count < GeoConstants.MIN_POLYGON_VERTICES)
                return;

            using SKPaint Paint = new SKPaint
            {
                Color = SKColors.Black,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = FIELD_BORDER_STROKE_WIDTH_2D,
                IsAntialias = true,
                StrokeJoin = SKStrokeJoin.Miter,
                StrokeCap = SKStrokeCap.Butt
            };

            using SKPath Path = new SKPath();
            bool Started = false;
            foreach ((double Lat, double Lon) in Ring)
            {
                LatLonToScreen(Lat, Lon, CentreLat, CentreLon, MetresPerPixel, Viewport, out float X, out float Y);
                if (!Started)
                {
                    Path.MoveTo(X, Y);
                    Started = true;
                }
                else
                {
                    Path.LineTo(X, Y);
                }
            }

            Path.Close();
            Canvas.DrawPath(Path, Paint);
        }

        /// <summary>
        /// Draws a black outline along mesh edges where the valid region ends.
        /// </summary>
        static private void DrawValidRegionBorder2D
            (
            SKCanvas Canvas,
            SKRect Viewport,
            FieldSurfaceSampler Surface,
            double CentreLat,
            double CentreLon,
            double MetresPerPixel
            )
        {
            using SKPaint Paint = new SKPaint
            {
                Color = SKColors.Black,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = FIELD_BORDER_STROKE_WIDTH_2D,
                IsAntialias = true,
                StrokeJoin = SKStrokeJoin.Miter,
                StrokeCap = SKStrokeCap.Butt
            };

            foreach ((double Lat0, double Lon0, double Lat1, double Lon1) in EnumerateValidRegionBorderEdges(Surface))
            {
                LatLonToScreen(Lat0, Lon0, CentreLat, CentreLon, MetresPerPixel, Viewport, out float X0, out float Y0);
                LatLonToScreen(Lat1, Lon1, CentreLat, CentreLon, MetresPerPixel, Viewport, out float X1, out float Y1);
                Canvas.DrawLine(X0, Y0, X1, Y1, Paint);
            }
        }

        /// <summary>
        /// Outlines the field perimeter with a solid black stroke draped on the 3D surface.
        /// </summary>
        static private void DrawFieldBorder3D
            (
            SKCanvas Canvas,
            SKRect Viewport,
            SurveyModel Survey,
            FieldSurfaceSampler Surface,
            FieldContentView ContentView,
            FieldCamera3D Camera,
            bool FlatPlane,
            MeshDepthBuffer TopoDepth
            )
        {
            if (Survey.BoundaryPoints.Count >= GeoConstants.MIN_POLYGON_VERTICES)
            {
                List<(double Lat, double Lon)> Ring = BuildOrderedBoundaryLatLon(Survey);

                using SKPaint Paint = new SKPaint
                {
                    Color = SKColors.Black,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = FIELD_BORDER_STROKE_WIDTH_3D,
                    IsAntialias = true,
                    StrokeJoin = SKStrokeJoin.Miter,
                    StrokeCap = SKStrokeCap.Butt
                };

                List<(float X, float Y, double Depth)> ScreenRing = new List<(float X, float Y, double Depth)>(Ring.Count);
                foreach ((double Lat, double Lon) in Ring)
                {
                    double Elev = FlatPlane
                        ? Surface.MinElevation
                        : Surface.SampleScalar(ContentView, Lat, Lon);
                    if (double.IsNaN(Elev))
                    {
                        TopologyPoint? Match = Survey.BoundaryPoints.Find(P =>
                            Math.Abs(P.Latitude - Lat) < BOUNDARY_COORDINATE_EPSILON && Math.Abs(P.Longitude - Lon) < BOUNDARY_COORDINATE_EPSILON);
                        Elev = Match?.ExistingElevation ?? Surface.MinElevation;
                    }

                    Camera.Project(Lat, Lon, Elev, Viewport, FlatPlane, out float X, out float Y, out double Depth);
                    ScreenRing.Add((X, Y, Depth));
                }

                DrawDepthTestedPolyline3D(Canvas, ScreenRing, TopoDepth, Paint, Closed: true);
                return;
            }

            DrawValidRegionBorder3D(Canvas, Viewport, Surface, ContentView, Camera, FlatPlane, TopoDepth);
        }

        /// <summary>
        /// Draws a black outline along mesh edges where the valid region ends.
        /// </summary>
        static private void DrawValidRegionBorder3D
            (
            SKCanvas Canvas,
            SKRect Viewport,
            FieldSurfaceSampler Surface,
            FieldContentView ContentView,
            FieldCamera3D Camera,
            bool FlatPlane,
            MeshDepthBuffer TopoDepth
            )
        {
            using SKPaint Paint = new SKPaint
            {
                Color = SKColors.Black,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = FIELD_BORDER_STROKE_WIDTH_3D,
                IsAntialias = true,
                StrokeJoin = SKStrokeJoin.Miter,
                StrokeCap = SKStrokeCap.Butt
            };

            foreach ((double Lat0, double Lon0, double Lat1, double Lon1) in EnumerateValidRegionBorderEdges(Surface))
            {
                double E0 = FlatPlane ? Surface.MinElevation : Surface.SampleScalar(ContentView, Lat0, Lon0);
                double E1 = FlatPlane ? Surface.MinElevation : Surface.SampleScalar(ContentView, Lat1, Lon1);
                if (double.IsNaN(E0) || double.IsNaN(E1))
                    continue;

                Camera.Project(Lat0, Lon0, E0, Viewport, FlatPlane, out float X0, out float Y0, out double D0);
                Camera.Project(Lat1, Lon1, E1, Viewport, FlatPlane, out float X1, out float Y1, out double D1);
                DrawDepthTestedLine3D(Canvas, X0, Y0, D0, X1, Y1, D1, TopoDepth, Paint);
            }
        }

        /// <summary>
        /// Returns lat/lon segments along edges between valid and invalid mesh quads.
        /// </summary>
        static private IEnumerable<(double Lat0, double Lon0, double Lat1, double Lon1)> EnumerateValidRegionBorderEdges
            (
            FieldSurfaceSampler Surface
            )
        {
            int W = Surface.SampleWidth;
            int H = Surface.SampleHeight;
            if (W < MIN_GRID_DIMENSION || H < MIN_GRID_DIMENSION)
                yield break;

            double LatSpan = Surface.MaxLat - Surface.MinLat;
            double LonSpan = Surface.MaxLon - Surface.MinLon;
            if (LatSpan <= 0 || LonSpan <= 0)
                yield break;

            bool QuadValid(int Sx, int Sy)
            {
                if (Sx < 0 || Sy < 0 || Sx >= W - 1 || Sy >= H - 1)
                    return false;

                int Idx00 = Sy * W + Sx;
                int Idx10 = Sy * W + Sx + 1;
                int Idx11 = (Sy + 1) * W + Sx + 1;
                int Idx01 = (Sy + 1) * W + Sx;
                return Surface.Valid[Idx00] && Surface.Valid[Idx10] && Surface.Valid[Idx11] && Surface.Valid[Idx01];
            }

            (double Lat, double Lon) GridPoint(int Sx, int Sy)
            {
                double Lon = Surface.MinLon + LonSpan * Sx / Math.Max(1, W - 1);
                double Lat = Surface.MaxLat - LatSpan * Sy / Math.Max(1, H - 1);
                return (Lat, Lon);
            }

            for (int Sy = 0; Sy < H; Sy++)
            {
                for (int Sx = 0; Sx < W - 1; Sx++)
                {
                    bool Above = Sy > 0 && QuadValid(Sx, Sy - 1);
                    bool Below = Sy < H - 1 && QuadValid(Sx, Sy);
                    if (Above == Below)
                        continue;

                    (double Lat0, double Lon0) = GridPoint(Sx, Sy);
                    (double Lat1, double Lon1) = GridPoint(Sx + 1, Sy);
                    yield return (Lat0, Lon0, Lat1, Lon1);
                }
            }

            for (int Sx = 0; Sx < W; Sx++)
            {
                for (int Sy = 0; Sy < H - 1; Sy++)
                {
                    bool Left = Sx > 0 && QuadValid(Sx - 1, Sy);
                    bool Right = Sx < W - 1 && QuadValid(Sx, Sy);
                    if (Left == Right)
                        continue;

                    (double Lat0, double Lon0) = GridPoint(Sx, Sy);
                    (double Lat1, double Lon1) = GridPoint(Sx, Sy + 1);
                    yield return (Lat0, Lon0, Lat1, Lon1);
                }
            }
        }

        /// <summary>
        /// Builds a perimeter-ordered lat/lon ring from survey boundary points.
        /// </summary>
        static internal List<(double Lat, double Lon)> BuildOrderedBoundaryLatLon
            (
            SurveyModel Survey
            )
        {
            List<(double Lat, double Lon)> Unordered = new List<(double Lat, double Lon)>(Survey.BoundaryPoints.Count);
            foreach (TopologyPoint Pt in Survey.BoundaryPoints)
                Unordered.Add((Pt.Latitude, Pt.Longitude));

            return BoundaryMask.OrderBoundaryLatLon(Unordered);
        }

        /// <summary>Stroke width for contour lines in 2D view.</summary>
        private const float CONTOUR_STROKE_WIDTH_2D = 2.0f;
        /// <summary>Stroke width for contour lines in 3D view.</summary>
        private const float CONTOUR_STROKE_WIDTH_3D = 1.75f;
         // always use Step=1; Step=2 doubles grade-break steepness and creates boundary gaps

        /// <summary>
        /// Traces elevation contour polylines coloured by band in 2D.
        /// </summary>
        static private void DrawContours2D
            (
            SKCanvas Canvas,
            SKRect Viewport,
            FieldSurfaceSampler Surface,
            FieldContentView ContentView,
            double CentreLat,
            double CentreLon,
            double MetresPerPixel,
            double VMin,
            double VMax,
            IProgress<int>? Progress = null
            )
        {
            if (VMax <= VMin)
            {
                Progress?.Report(PROGRESS_COMPLETE_PERCENT);
                return;
            }

            // Offset contour colour slightly below the level so lines read above the fill.
            double BandOffset = (VMax - VMin) * RenderingConstants.CONTOUR_BAND_OFFSET_FRACTION;
            IReadOnlyList<(double Level, IReadOnlyList<(double Lat, double Lon)> Points)> Polylines =
                Surface.GetContourPolylinesByLevel(ContentView, VMin, VMax, ProgressMapper.Subrange(Progress, PROGRESS_START_PERCENT, PROGRESS_CONTOUR_POLYLINE_END_PERCENT));

            using SKPaint Paint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = CONTOUR_STROKE_WIDTH_2D,
                IsAntialias = true,
                StrokeJoin = SKStrokeJoin.Round,
                StrokeCap = SKStrokeCap.Round
            };

            foreach ((double Level, IReadOnlyList<(double Lat, double Lon)> Polyline) in Polylines)
            {
                if (Polyline.Count < GeoConstants.MIN_POLYLINE_VERTICES)
                    continue;

                Paint.Color = ArgbToSkia(NineBandColorScale.GetLightenedColor(Level - BandOffset, VMin, VMax));

                using SKPath Path = new SKPath();
                LatLonToScreen(Polyline[0].Lat, Polyline[0].Lon, CentreLat, CentreLon, MetresPerPixel, Viewport, out float X0, out float Y0);
                Path.MoveTo(X0, Y0);
                for (int I = 1; I < Polyline.Count; I++)
                {
                    LatLonToScreen(Polyline[I].Lat, Polyline[I].Lon, CentreLat, CentreLon, MetresPerPixel, Viewport, out float X, out float Y);
                    Path.LineTo(X, Y);
                }

                Canvas.DrawPath(Path, Paint);
            }

            Progress?.Report(PROGRESS_COMPLETE_PERCENT);
        }

        /// <summary>
        /// Returns the map clear colour for the active view mode and satellite toggle.
        /// </summary>
        static private SKColor ResolveMapBackgroundColor
            (
            bool ShowSatellite,
            bool UsePlanViewSatellite
            )
        {
            if (!ShowSatellite)
                return MAP_BACKGROUND_COLOR;

            return UsePlanViewSatellite
                ? SATELLITE_2D_BACKGROUND_COLOR
                : SATELLITE_GROUND_BACKGROUND_COLOR;
        }

        /// <summary>
        /// Draws the satellite basemap aligned to the 2D camera.
        /// </summary>
        static private int DrawSatellite2D
            (
            SKCanvas Canvas,
            SKRect Viewport,
            double CentreLat,
            double CentreLon,
            double MetresPerPixel,
            SatelliteTileService Satellite
            )
        {
            // Delegate basemap compositing to the core tile renderer aligned to the 2D camera.
            (_, int PlaceholdersDrawn, _) = SatelliteMapRenderer.DrawToCanvas(
                Canvas,
                CentreLat,
                CentreLon,
                Viewport.Width,
                Viewport.Height,
                MetresPerPixel,
                Satellite);
            return PlaceholdersDrawn;
        }

        /// <summary>
        /// Draws the satellite plinth: textured square top and black side faces.
        /// </summary>
        static private void DrawSatellite3DPlane
            (
            SKCanvas Canvas,
            SKRect Viewport,
            FieldSurfaceSampler Surface,
            double PlaneElevation,
            SatelliteTileService Satellite,
            FieldCamera3D Camera
            )
        {
            double FieldCentreLat = (Surface.MinLat + Surface.MaxLat) * FieldConstants.EXTENT_MIDPOINT_FRACTION;
            double FieldCentreLon = (Surface.MinLon + Surface.MaxLon) * FieldConstants.EXTENT_MIDPOINT_FRACTION;
            double HalfSideM = SATELLITE_PLINTH_HALF_SPAN_M;
            double PlinthDiagonalM = HalfSideM * PLINTH_DIAGONAL_SIDE_MULTIPLIER * Math.Sqrt(PLINTH_DIAGONAL_SIDE_MULTIPLIER);
            UTM.UTMCoordinate FieldCentreUtm = UTM.FromLatLon(FieldCentreLat, FieldCentreLon);
            double PlinthModelThickness = Math.Max(
                SATELLITE_PLINTH_MIN_MODEL_THICKNESS,
                Camera.HorizontalExtentM * SATELLITE_PLINTH_THICKNESS_FRACTION);

            (double EastOffset, double NorthOffset)[] CornerOffsets =
            {
                (-HalfSideM, -HalfSideM),
                (HalfSideM, -HalfSideM),
                (HalfSideM, HalfSideM),
                (-HalfSideM, HalfSideM)
            };

            ProjectPlinthCornerModel(Camera, Viewport, FieldCentreUtm, CornerOffsets[0], PLINTH_TOP_MODEL_Z,
                out float SwTx, out float SwTy, out double SwTd);
            ProjectPlinthCornerModel(Camera, Viewport, FieldCentreUtm, CornerOffsets[1], PLINTH_TOP_MODEL_Z,
                out float SeTx, out float SeTy, out double SeTd);
            ProjectPlinthCornerModel(Camera, Viewport, FieldCentreUtm, CornerOffsets[2], PLINTH_TOP_MODEL_Z,
                out float NeTx, out float NeTy, out double NeTd);
            ProjectPlinthCornerModel(Camera, Viewport, FieldCentreUtm, CornerOffsets[3], PLINTH_TOP_MODEL_Z,
                out float NwTx, out float NwTy, out double NwTd);

            ProjectPlinthCornerModel(Camera, Viewport, FieldCentreUtm, CornerOffsets[0], -PlinthModelThickness,
                out float SwBx, out float SwBy, out double SwBd);
            ProjectPlinthCornerModel(Camera, Viewport, FieldCentreUtm, CornerOffsets[1], -PlinthModelThickness,
                out float SeBx, out float SeBy, out double SeBd);
            ProjectPlinthCornerModel(Camera, Viewport, FieldCentreUtm, CornerOffsets[2], -PlinthModelThickness,
                out float NeBx, out float NeBy, out double NeBd);
            ProjectPlinthCornerModel(Camera, Viewport, FieldCentreUtm, CornerOffsets[3], -PlinthModelThickness,
                out float NwBx, out float NwBy, out double NwBd);

            List<PlinthFace> SideFaces =
            [
                new PlinthFace(SwTx, SwTy, SeTx, SeTy, SeBx, SeBy, SwBx, SwBy, AverageDepth(SwTd, SeTd, SeBd, SwBd), 0.0, -1.0, 0.0),
                new PlinthFace(SeTx, SeTy, NeTx, NeTy, NeBx, NeBy, SeBx, SeBy, AverageDepth(SeTd, NeTd, NeBd, SeBd), 1.0, 0.0, 0.0),
                new PlinthFace(NeTx, NeTy, NwTx, NwTy, NwBx, NwBy, NeBx, NeBy, AverageDepth(NeTd, NwTd, NwBd, NeBd), 0.0, 1.0, 0.0),
                new PlinthFace(NwTx, NwTy, SwTx, SwTy, SwBx, SwBy, NwBx, NwBy, AverageDepth(NwTd, SwTd, SwBd, NwBd), -1.0, 0.0, 0.0)
            ];
            SideFaces.Sort((A, B) => B.Depth.CompareTo(A.Depth));

            using SKPaint EdgePaint = new SKPaint
            {
                Color = PLINTH_EDGE_COLOR,
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };
            foreach (PlinthFace Face in SideFaces)
            {
                if (!IsReasonableScreenQuad(Face.P0x, Face.P0y, Face.P1x, Face.P1y, Face.P2x, Face.P2y, Face.P3x, Face.P3y, Viewport))
                    continue;

                DrawSolidQuad3D(Canvas, Face.P0x, Face.P0y, Face.P1x, Face.P1y, Face.P2x, Face.P2y, Face.P3x, Face.P3y, EdgePaint);
            }

            // Always draw a solid top first so partial tile availability does not expose sky through the plinth.
            using SKPaint TopFillPaint = new SKPaint
            {
                Color = PLINTH_TOP_FALLBACK_COLOR,
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };
            DrawSolidQuad3D(Canvas, SwTx, SwTy, SeTx, SeTy, NeTx, NeTy, NwTx, NwTy, TopFillPaint);

            // Clip textured tiles to the projected plinth top polygon.
            using SKPath PlinthTopPath = new SKPath();
            PlinthTopPath.MoveTo(SwTx, SwTy);
            PlinthTopPath.LineTo(SeTx, SeTy);
            PlinthTopPath.LineTo(NeTx, NeTy);
            PlinthTopPath.LineTo(NwTx, NwTy);
            PlinthTopPath.Close();

            // Slightly coarsen zoom for the large fixed plinth to keep interaction responsive.
            double MetresPerPixel = Camera.EstimateGroundMetresPerScreenPixel(Viewport) * RenderingConstants.PLINTH_TILE_ZOOM_FACTOR;
            (int Zoom, IReadOnlyList<(int TileX, int TileY)> TileIndices) = SatelliteMapLayout3D.ComputePlinthTiles(
                FieldCentreLat,
                FieldCentreLon,
                PlinthDiagonalM,
                MetresPerPixel);

            (double MinLat, double MaxLat, double MinLon, double MaxLon) PlinthBounds = SatelliteMapLayout3D.ComputePlinthSquareBounds(
                FieldCentreLat,
                FieldCentreLon,
                PlinthDiagonalM);

            int TilesPerAxis = 1 << Zoom;
            using SKPaint Paint = new SKPaint { IsAntialias = true, IsDither = true };
            List<(double Depth, int Tx, int Ty)> TileDraws = new List<(double Depth, int Tx, int Ty)>();

            foreach ((int Tcx, int Ty) in TileIndices)
            {
                if (Ty < 0 || Ty >= TilesPerAxis)
                    continue;

                WebMercator.TileLatLonBounds(Zoom, Tcx, Ty, out double RawMinLon, out double RawMinLat, out double RawMaxLon, out double RawMaxLat);
                double TileMinLat = Math.Min(RawMinLat, RawMaxLat);
                double TileMaxLat = Math.Max(RawMinLat, RawMaxLat);
                double TileMinLon = Math.Min(RawMinLon, RawMaxLon);
                double TileMaxLon = Math.Max(RawMinLon, RawMaxLon);
                if (!TileIntersectsPlinthBounds(TileMinLat, TileMaxLat, TileMinLon, TileMaxLon, PlinthBounds))
                    continue;

                double CentreLat = (TileMinLat + TileMaxLat) * FieldConstants.EXTENT_MIDPOINT_FRACTION;
                double CentreLon = (TileMinLon + TileMaxLon) * FieldConstants.EXTENT_MIDPOINT_FRACTION;
                Camera.Project(CentreLat, CentreLon, PlaneElevation, Viewport, FlatPlane: true, out _, out _, out double Depth);
                TileDraws.Add((Depth, Tcx, Ty));
            }

            TileDraws.Sort((A, B) => B.Depth.CompareTo(A.Depth));

            Canvas.Save();
            Canvas.ClipPath(PlinthTopPath, SKClipOperation.Intersect, true);

            foreach ((double _, int Tcx, int Ty) in TileDraws)
            {
                SKBitmap? Bitmap = Satellite.GetTileBitmap(Zoom, Tcx, Ty);
                if (Bitmap == null)
                    continue;

                WebMercator.TileLatLonBounds(Zoom, Tcx, Ty, out double RawMinLon, out double RawMinLat, out double RawMaxLon, out double RawMaxLat);
                double TileMinLat = Math.Min(RawMinLat, RawMaxLat);
                double TileMaxLat = Math.Max(RawMinLat, RawMaxLat);
                double TileMinLon = Math.Min(RawMinLon, RawMaxLon);
                double TileMaxLon = Math.Max(RawMinLon, RawMaxLon);

                DrawTexturedTileSubdivided3D(Canvas, Bitmap, Camera, TileMinLat, TileMinLon, TileMaxLat, TileMaxLon, PlaneElevation, Viewport, Paint);
            }

            Canvas.Restore();
        }

        /// <summary>
        /// Projects one plinth corner from UTM offsets to screen space.
        /// </summary>
        static private void ProjectPlinthCornerModel
            (
            FieldCamera3D Camera,
            SKRect Viewport,
            UTM.UTMCoordinate FieldCentreUtm,
            (double EastOffset, double NorthOffset) CornerOffset,
            double ModelZ,
            out float ScreenX,
            out float ScreenY,
            out double Depth
            )
        {
            double OffsetEastM = (FieldCentreUtm.Easting + CornerOffset.EastOffset) - Camera.CentreUtm.Easting;
            double OffsetNorthM = (FieldCentreUtm.Northing + CornerOffset.NorthOffset) - Camera.CentreUtm.Northing;
            Camera.ProjectModelOffset(OffsetEastM, OffsetNorthM, ModelZ, Viewport, out ScreenX, out ScreenY, out Depth);
        }

        /// <summary>
        /// Returns whether a Web Mercator tile overlaps the plinth square bounds.
        /// </summary>
        static private bool TileIntersectsPlinthBounds
            (
            double TileMinLat,
            double TileMaxLat,
            double TileMinLon,
            double TileMaxLon,
            (double MinLat, double MaxLat, double MinLon, double MaxLon) PlinthBounds
            )
        {
            if (TileMaxLat < PlinthBounds.MinLat || TileMinLat > PlinthBounds.MaxLat)
                return false;
            if (TileMaxLon < PlinthBounds.MinLon || TileMinLon > PlinthBounds.MaxLon)
                return false;
            return true;
        }

        /// <summary>
        /// Rejects degenerate or oversized projected tile quads before drawing.
        /// </summary>
        static private bool IsReasonableScreenQuad
            (
            float P0x,
            float P0y,
            float P1x,
            float P1y,
            float P2x,
            float P2y,
            float P3x,
            float P3y,
            SKRect Viewport
            )
        {
            if (!IsFinite(P0x, P0y) || !IsFinite(P1x, P1y) || !IsFinite(P2x, P2y) || !IsFinite(P3x, P3y))
                return false;

            float MinX = Math.Min(Math.Min(P0x, P1x), Math.Min(P2x, P3x));
            float MaxX = Math.Max(Math.Max(P0x, P1x), Math.Max(P2x, P3x));
            float MinY = Math.Min(Math.Min(P0y, P1y), Math.Min(P2y, P3y));
            float MaxY = Math.Max(Math.Max(P0y, P1y), Math.Max(P2y, P3y));
            float Width = MaxX - MinX;
            float Height = MaxY - MinY;
            if ((Width <= 0f) || (Height <= 0f))
                return false;

            double MaxArea = Viewport.Width * Viewport.Height * SCREEN_QUAD_MAX_AREA_MULTIPLIER;
            return (Width * Height) <= MaxArea;
        }

        /// <summary>
        /// Sortable plinth side face in screen space.
        /// </summary>
        private readonly struct PlinthFace
        {
            /// <summary>
            /// Creates a sortable plinth side face with screen corners and outward normal.
            /// </summary>
            public PlinthFace
                (
                float P0x,
                float P0y,
                float P1x,
                float P1y,
                float P2x,
                float P2y,
                float P3x,
                float P3y,
                double Depth,
                double NormalEast,
                double NormalNorth,
                double NormalUp
                )
            {
                this.P0x = P0x;
                this.P0y = P0y;
                this.P1x = P1x;
                this.P1y = P1y;
                this.P2x = P2x;
                this.P2y = P2y;
                this.P3x = P3x;
                this.P3y = P3y;
                this.Depth = Depth;
                this.NormalEast = NormalEast;
                this.NormalNorth = NormalNorth;
                this.NormalUp = NormalUp;
            }

            /// <summary>First corner screen X.</summary>
            public float P0x { get; }
            /// <summary>First corner screen Y.</summary>
            public float P0y { get; }
            /// <summary>Second corner screen X.</summary>
            public float P1x { get; }
            /// <summary>Second corner screen Y.</summary>
            public float P1y { get; }
            /// <summary>Third corner screen X.</summary>
            public float P2x { get; }
            /// <summary>Third corner screen Y.</summary>
            public float P2y { get; }
            /// <summary>Fourth corner screen X.</summary>
            public float P3x { get; }
            /// <summary>Fourth corner screen Y.</summary>
            public float P3y { get; }
            /// <summary>Average camera depth for painter ordering.</summary>
            public double Depth { get; }
            /// <summary>Outward normal east component in model space.</summary>
            public double NormalEast { get; }
            /// <summary>Outward normal north component in model space.</summary>
            public double NormalNorth { get; }
            /// <summary>Outward normal up component in model space.</summary>
            public double NormalUp { get; }
        }

        /// <summary>
        /// Averages four camera depths for plinth face painter ordering.
        /// </summary>
        static private double AverageDepth
            (
            double D0,
            double D1,
            double D2,
            double D3
            )
        {
            return (D0 + D1 + D2 + D3) * AVERAGE_DEPTH_FRACTION;
        }

        /// <summary>
        /// Draws a vertical sky gradient from light blue at the horizon to darker blue at the top.
        /// </summary>
        static private void DrawSky3D
            (
            SKCanvas Canvas,
            SKRect Viewport,
            FieldCamera3D Camera,
            double PlaneElevation,
            double HorizonLat,
            double HorizonLon
            )
        {
            float HorizonY = ComputeHorizonScreenY(Camera, Viewport, PlaneElevation, HorizonLat, HorizonLon);
            if (HorizonY <= Viewport.Top + SKY_HORIZON_VIEWPORT_INSET)
                return;

            float SkyBottom = Math.Min(HorizonY, Viewport.Bottom);
            SKRect SkyRect = SKRect.Create(Viewport.Left, Viewport.Top, Viewport.Width, SkyBottom - Viewport.Top);
            if (SkyRect.Height <= SKY_MIN_HEIGHT)
                return;

            using SKPaint SkyPaint = new SKPaint { IsAntialias = true };
            SkyPaint.Shader = SKShader.CreateLinearGradient(
                new SKPoint(Viewport.MidX, SkyBottom),
                new SKPoint(Viewport.MidX, Viewport.Top),
                new[] { SKY_HORIZON_COLOR, SKY_ZENITH_COLOR },
                SKShaderTileMode.Clamp);
            Canvas.DrawRect(SkyRect, SkyPaint);
        }

        /// <summary>
        /// Projects the field centre on the plinth top to find the horizon line in screen space.
        /// </summary>
        static private float ComputeHorizonScreenY
            (
            FieldCamera3D Camera,
            SKRect Viewport,
            double PlaneElevation,
            double HorizonLat,
            double HorizonLon
            )
        {
            Camera.Project(
                HorizonLat,
                HorizonLon,
                PlaneElevation,
                Viewport,
                FlatPlane: true,
                out _,
                out float HorizonY,
                out _);
            return HorizonY;
        }

        /// <summary>
        /// Rejects tile quads that collapse or fall entirely behind the camera at shallow pitch.
        /// </summary>
        static private bool IsUsableProjectedQuad
            (
            float P0x,
            float P0y,
            float P1x,
            float P1y,
            float P2x,
            float P2y,
            float P3x,
            float P3y,
            double D0,
            double D1,
            double D2,
            double D3,
            SKRect Viewport
            )
        {
            if (!IsFinite(P0x, P0y) || !IsFinite(P1x, P1y) || !IsFinite(P2x, P2y) || !IsFinite(P3x, P3y))
                return false;

            // Do not cull purely by camera-space depth sign here. In this projected renderer,
            // far-side quads can still be visible on screen even when all four depths are negative.

            float MinX = Math.Min(Math.Min(P0x, P1x), Math.Min(P2x, P3x));
            float MaxX = Math.Max(Math.Max(P0x, P1x), Math.Max(P2x, P3x));
            float MinY = Math.Min(Math.Min(P0y, P1y), Math.Min(P2y, P3y));
            float MaxY = Math.Max(Math.Max(P0y, P1y), Math.Max(P2y, P3y));

            float Margin = Math.Max(Viewport.Width, Viewport.Height);
            if ((MaxX < Viewport.Left - Margin) || (MinX > Viewport.Right + Margin))
                return false;
            if ((MaxY < Viewport.Top - Margin) || (MinY > Viewport.Bottom + Margin))
                return false;

            double Area = Math.Abs(((P1x - P0x) * (P2y - P0y)) - ((P2x - P0x) * (P1y - P0y)));
            Area += Math.Abs(((P1x - P2x) * (P3y - P2y)) - ((P3x - P2x) * (P1y - P2y)));
            if (Area < MIN_PROJECTED_AREA)
                return ((MaxX - MinX) >= MIN_SCREEN_SPAN) || ((MaxY - MinY) >= MIN_SCREEN_SPAN);

            return true;
        }

        /// <summary>
        /// Returns true when both screen coordinates are finite.
        /// </summary>
        static private bool IsFinite
            (
            float X,
            float Y
            )
        {
            return !(float.IsNaN(X) || float.IsNaN(Y) || float.IsInfinity(X) || float.IsInfinity(Y));
        }

        /// <summary>
        /// Draws a solid-colour quadrilateral in 3D screen space.
        /// </summary>
        static private void DrawSolidQuad3D
            (
            SKCanvas Canvas,
            float P0x,
            float P0y,
            float P1x,
            float P1y,
            float P2x,
            float P2y,
            float P3x,
            float P3y,
            SKPaint Paint
            )
        {
            using SKPath Path = new SKPath();
            Path.MoveTo(P0x, P0y);
            Path.LineTo(P1x, P1y);
            Path.LineTo(P2x, P2y);
            Path.LineTo(P3x, P3y);
            Path.Close();
            Canvas.DrawPath(Path, Paint);
        }

        /// <summary>
        /// Draws a bitmap mapped onto a quadrilateral via Skia vertex shader.
        /// </summary>
        // Number of sub-divisions per tile axis.  4×4 = 16 quads per tile which is
        // sufficient to eliminate perspective-UV error for zoom-15 tiles (~4 km each)
        // at typical 3D view angles without a measurable frame-rate cost.
        

        /// <summary>
        /// Draws a satellite tile as a grid of small projected quads so that
        /// perspective-correct UV mapping is approximated even in screen space.
        /// The tile bitmap has its top edge at MaxLat (north) and bottom at MinLat (south).
        /// </summary>
        static private void DrawTexturedTileSubdivided3D
            (
            SKCanvas Canvas,
            SKBitmap Bitmap,
            FieldCamera3D Camera,
            double TileMinLat,
            double TileMinLon,
            double TileMaxLat,
            double TileMaxLon,
            double Elevation,
            SKRect Viewport,
            SKPaint Paint
            )
        {
            Camera.Project(TileMinLat, TileMinLon, Elevation, Viewport, FlatPlane: true, out float SwX, out float SwY, out _);
            Camera.Project(TileMinLat, TileMaxLon, Elevation, Viewport, FlatPlane: true, out float SeX, out float SeY, out _);
            Camera.Project(TileMaxLat, TileMaxLon, Elevation, Viewport, FlatPlane: true, out float NeX, out float NeY, out _);
            Camera.Project(TileMaxLat, TileMinLon, Elevation, Viewport, FlatPlane: true, out float NwX, out float NwY, out _);

            float MinX = Math.Min(Math.Min(SwX, SeX), Math.Min(NeX, NwX));
            float MaxX = Math.Max(Math.Max(SwX, SeX), Math.Max(NeX, NwX));
            float MinY = Math.Min(Math.Min(SwY, SeY), Math.Min(NeY, NwY));
            float MaxY = Math.Max(Math.Max(SwY, SeY), Math.Max(NeY, NwY));
            float ScreenSpan = Math.Max(MaxX - MinX, MaxY - MinY);

            int N = ScreenSpan switch
            {
                <= TILE_SCREEN_SPAN_LOW_THRESHOLD => TILE_SCREEN_SPAN_LOW_SUBDIVISIONS,
                <= TILE_SCREEN_SPAN_MID_THRESHOLD => TILE_SCREEN_SPAN_MID_SUBDIVISIONS,
                _ => RenderingConstants.TILE_SUBDIVISIONS
            };
            float BmpW = Bitmap.Width;
            float BmpH = Bitmap.Height;

            // Pre-compute the (N+1)×(N+1) projected screen positions.
            // Row 0 = south (TileMinLat), Row N = north (TileMaxLat).
            // Col 0 = west  (TileMinLon), Col N = east  (TileMaxLon).
            float[,] ScrX = new float[N + 1, N + 1];
            float[,] ScrY = new float[N + 1, N + 1];

            for (int Row = 0; Row <= N; Row++)
            {
                double LatT = (double)Row / N;
                double Lat  = TileMinLat + LatT * (TileMaxLat - TileMinLat);
                for (int Col = 0; Col <= N; Col++)
                {
                    double LonT = (double)Col / N;
                    double Lon  = TileMinLon + LonT * (TileMaxLon - TileMinLon);
                    Camera.Project(Lat, Lon, Elevation, Viewport, FlatPlane: true, out float Sx, out float Sy, out double _);
                    ScrX[Row, Col] = Sx;
                    ScrY[Row, Col] = Sy;
                }
            }

            using SKShader Shader = SKShader.CreateBitmap(Bitmap, SKShaderTileMode.Clamp, SKShaderTileMode.Clamp);
            Paint.Shader = Shader;

            // Reusable arrays for each sub-quad (avoid per-quad allocation).
            SKPoint[] Positions  = new SKPoint[QUAD_VERTEX_COUNT];
            SKPoint[] TexCoords  = new SKPoint[QUAD_VERTEX_COUNT];
            ushort[]  Indices    = { 0, 1, 2, 0, 2, 3 };

            for (int Row = 0; Row < N; Row++)
            {
                // Bitmap Y: north (Row=N) → 0, south (Row=0) → BmpH.
                float TexYBot = BmpH - (float)Row       / N * BmpH;   // south edge of sub-row
                float TexYTop = BmpH - (float)(Row + 1) / N * BmpH;   // north edge of sub-row

                for (int Col = 0; Col < N; Col++)
                {
                    float TexXL = (float)Col       / N * BmpW;
                    float TexXR = (float)(Col + 1) / N * BmpW;

                    // SW, SE, NE, NW screen positions
                    Positions[0] = new SKPoint(ScrX[Row,     Col    ], ScrY[Row,     Col    ]); // SW
                    Positions[1] = new SKPoint(ScrX[Row,     Col + 1], ScrY[Row,     Col + 1]); // SE
                    Positions[2] = new SKPoint(ScrX[Row + 1, Col + 1], ScrY[Row + 1, Col + 1]); // NE
                    Positions[3] = new SKPoint(ScrX[Row + 1, Col    ], ScrY[Row + 1, Col    ]); // NW

                    // UV: SW→(L,Bot), SE→(R,Bot), NE→(R,Top), NW→(L,Top)
                    TexCoords[0] = new SKPoint(TexXL, TexYBot);
                    TexCoords[1] = new SKPoint(TexXR, TexYBot);
                    TexCoords[2] = new SKPoint(TexXR, TexYTop);
                    TexCoords[3] = new SKPoint(TexXL, TexYTop);

                    Canvas.DrawVertices(SKVertexMode.Triangles, Positions, TexCoords, null, Indices, Paint);
                }
            }

            Paint.Shader = null;
        }

        /// <summary>
        /// Renders cut/fill as a flat false-colour plane in 3D.
        /// </summary>
        static private MeshDepthBuffer DrawCutFillPlane3D
            (
            SKCanvas Canvas,
            SKRect Viewport,
            FieldSurfaceSampler Surface,
            double VMin,
            double VMax,
            FieldCamera3D Camera,
            bool BuildDepthBuffer,
            IProgress<int>? Progress = null
            )
        {
            // Reuse the mesh path with a flat plane so cut/fill reads as a horizontal sheet.
            return DrawMesh3D(Canvas, Viewport, Surface, FieldContentView.CutFill, VMin, VMax, Camera, ApplyHillshade: false, FlatPlane: true, BuildDepthBuffer, Progress: Progress);
        }

        /// <summary>Screen-space divisor for reduced-resolution depth buffer storage.</summary>
        private const int DEPTH_BUFFER_RESOLUTION_SCALE = 2;

        /// <summary>
        /// Per-pixel camera depth of the rendered topo mesh; larger values are closer to the camera.
        /// Stored at reduced resolution to keep orbit interaction responsive on large meshes.
        /// </summary>
        private sealed class MeshDepthBuffer
        {
            

            /// <summary>Singleton passthrough buffer that skips depth testing.</summary>
            static private readonly MeshDepthBuffer AlwaysVisibleInstance = new MeshDepthBuffer();

            /// <summary>Depth lookup that treats every overlay sample as visible.</summary>
            static public MeshDepthBuffer AlwaysVisible => AlwaysVisibleInstance;

            /// <summary>True when depth testing is disabled and all overlay samples are visible.</summary>
            public bool IsPassthrough => _Depth == null;

            /// <summary>Reduced-resolution depth values; null for passthrough instance.</summary>
            private readonly float[]? _Depth;
            /// <summary>Depth buffer width in reduced-resolution pixels.</summary>
            private readonly int _Width;
            /// <summary>Depth buffer height in reduced-resolution pixels.</summary>
            private readonly int _Height;

            /// <summary>Creates a passthrough depth buffer with no stored depths.</summary>
            private MeshDepthBuffer()
            {
            }

            /// <summary>
            /// Builds a depth buffer by rasterizing mesh triangles at reduced resolution.
            /// </summary>
            /// <param name="Viewport">Target viewport rectangle.</param>
            /// <param name="Triangles">Sorted mesh triangles with per-vertex depths.</param>
            /// <returns>Depth buffer for overlay visibility testing.</returns>
            static public MeshDepthBuffer Build(SKRect Viewport, IReadOnlyList<MeshTriangle> Triangles)
            {
                MeshDepthBuffer Buffer = new MeshDepthBuffer(Viewport);
                if (Triangles.Count == 0)
                    return Buffer;

                float[] Depth = Buffer._Depth!;
                foreach (MeshTriangle Triangle in Triangles)
                {
                    Buffer.RasterizeTriangleInto(
                        Depth,
                        Triangle.P0.X, Triangle.P0.Y, Triangle.D0,
                        Triangle.P1.X, Triangle.P1.Y, Triangle.D1,
                        Triangle.P2.X, Triangle.P2.Y, Triangle.D2);
                }

                return Buffer;
            }

            /// <summary>
            /// Allocates a reduced-resolution depth buffer for the given viewport.
            /// </summary>
            /// <param name="Viewport">Target viewport rectangle.</param>
            private MeshDepthBuffer(SKRect Viewport)
            {
                _Width = Math.Max(DEPTH_BUFFER_MIN_DIMENSION, (int)Math.Ceiling(Viewport.Width / DEPTH_BUFFER_RESOLUTION_SCALE));
                _Height = Math.Max(DEPTH_BUFFER_MIN_DIMENSION, (int)Math.Ceiling(Viewport.Height / DEPTH_BUFFER_RESOLUTION_SCALE));
                _Depth = new float[_Width * _Height];
                Array.Fill(_Depth, float.NegativeInfinity);
            }

            /// <summary>
            /// Rasterizes one triangle into the depth buffer using barycentric interpolation.
            /// </summary>
            private void RasterizeTriangleInto
                (
                float[] Depth,
                float X0,
                float Y0,
                double D0,
                float X1,
                float Y1,
                double D1,
                float X2,
                float Y2,
                double D2
                )
            {
                X0 /= DEPTH_BUFFER_RESOLUTION_SCALE;
                Y0 /= DEPTH_BUFFER_RESOLUTION_SCALE;
                X1 /= DEPTH_BUFFER_RESOLUTION_SCALE;
                Y1 /= DEPTH_BUFFER_RESOLUTION_SCALE;
                X2 /= DEPTH_BUFFER_RESOLUTION_SCALE;
                Y2 /= DEPTH_BUFFER_RESOLUTION_SCALE;

                double Area2 = TriangleEdge(X0, Y0, X1, Y1, X2, Y2);
                if (Math.Abs(Area2) < TRIANGLE_RASTERIZE_AREA_EPSILON)
                    return;

                int MinX = (int)Math.Floor(Math.Min(X0, Math.Min(X1, X2)));
                int MaxX = (int)Math.Ceiling(Math.Max(X0, Math.Max(X1, X2)));
                int MinY = (int)Math.Floor(Math.Min(Y0, Math.Min(Y1, Y2)));
                int MaxY = (int)Math.Ceiling(Math.Max(Y0, Math.Max(Y1, Y2)));
                if (MaxX < 0 || MinX >= _Width || MaxY < 0 || MinY >= _Height)
                    return;

                MinX = Math.Clamp(MinX, 0, _Width - 1);
                MaxX = Math.Clamp(MaxX, 0, _Width - 1);
                MinY = Math.Clamp(MinY, 0, _Height - 1);
                MaxY = Math.Clamp(MaxY, 0, _Height - 1);

                for (int Py = MinY; Py <= MaxY; Py++)
                {
                    int RowBase = Py * _Width;
                    for (int Px = MinX; Px <= MaxX; Px++)
                    {
                        double W0 = TriangleEdge(X1, Y1, X2, Y2, Px, Py) / Area2;
                        if (W0 < 0)
                            continue;

                        double W1 = TriangleEdge(X2, Y2, X0, Y0, Px, Py) / Area2;
                        if (W1 < 0)
                            continue;

                        double W2 = TriangleEdge(X0, Y0, X1, Y1, Px, Py) / Area2;
                        if (W2 < 0)
                            continue;

                        float TriangleDepth = (float)((W0 * D0) + (W1 * D1) + (W2 * D2));
                        int Idx = RowBase + Px;
                        float Stored = Depth[Idx];
                        if (TriangleDepth > Stored)
                            Depth[Idx] = TriangleDepth;
                    }
                }
            }

            /// <summary>
            /// Returns whether a screen point is visible at the given depth against the topo mesh.
            /// </summary>
            /// <param name="ScreenX">Screen X in pixels.</param>
            /// <param name="ScreenY">Screen Y in pixels.</param>
            /// <param name="Depth">Camera depth of the overlay sample.</param>
            /// <returns>True when the sample is not occluded by the mesh.</returns>
            public bool IsVisible(float ScreenX, float ScreenY, double Depth)
            {
                if (_Depth == null)
                    return true;

                int Px = (int)Math.Round(ScreenX / DEPTH_BUFFER_RESOLUTION_SCALE);
                int Py = (int)Math.Round(ScreenY / DEPTH_BUFFER_RESOLUTION_SCALE);
                if (Px < 0 || Py < 0 || Px >= _Width || Py >= _Height)
                    return false;

                float Stored = _Depth![(Py * _Width) + Px];
                if (float.IsNegativeInfinity(Stored))
                    return true;

                return Depth >= Stored - DEPTH_VISIBLE_EPSILON;
            }

            /// <summary>
            /// Signed doubled area of triangle ABC (positive = counter-clockwise).
            /// </summary>
            static private double TriangleEdge
                (
                double Ax,
                double Ay,
                double Bx,
                double By,
                double Cx,
                double Cy
                )
            {
                return ((Cx - Ax) * (By - Ay)) - ((Cy - Ay) * (Bx - Ax));
            }
        }

        /// <summary>
        /// Sortable triangle record for painter-ordered 3D mesh rendering.
        /// </summary>
        private sealed class MeshTriangle
        {
            /// <summary>First vertex screen position.</summary>
            public required SKPoint P0 { get; init; }

            /// <summary>Second vertex screen position.</summary>
            public required SKPoint P1 { get; init; }

            /// <summary>Third vertex screen position.</summary>
            public required SKPoint P2 { get; init; }

            /// <summary>Fill colour at the first vertex.</summary>
            public required SKColor C0 { get; init; }

            /// <summary>Fill colour at the second vertex.</summary>
            public required SKColor C1 { get; init; }

            /// <summary>Fill colour at the third vertex.</summary>
            public required SKColor C2 { get; init; }

            /// <summary>Camera depth at the first vertex.</summary>
            public required double D0 { get; init; }

            /// <summary>Camera depth at the second vertex.</summary>
            public required double D1 { get; init; }

            /// <summary>Camera depth at the third vertex.</summary>
            public required double D2 { get; init; }

            /// <summary>Camera-space depth used for painter sorting.</summary>
            public required double Depth { get; init; }
        }

        /// <summary>
        /// Tessellates the scalar surface into depth-sorted coloured triangles.
        /// </summary>
        static private MeshDepthBuffer DrawMesh3D
            (
            SKCanvas Canvas,
            SKRect Viewport,
            FieldSurfaceSampler Surface,
            FieldContentView ContentView,
            double VMin,
            double VMax,
            FieldCamera3D Camera,
            bool ApplyHillshade,
            bool FlatPlane,
            bool BuildDepthBuffer,
            bool DrawMeshColors = true,
            IProgress<int>? Progress = null
            )
        {
            Progress?.Report(PROGRESS_START_PERCENT);
            int Step = (Math.Max(Surface.SampleWidth, Surface.SampleHeight) > RenderingConstants.MESH_STEP_THRESHOLD) ? MESH_SAMPLING_COARSE_STEP : 1;
            if (ApplyHillshade)
                Surface.GetMeshHillshadeFactor(ContentView, 0);
            List<MeshTriangle> Triangles = new List<MeshTriangle>(
                Math.Max(MESH_INITIAL_CAPACITY, ((Surface.SampleHeight / Step) * (Surface.SampleWidth / Step)) * MESH_TRIANGLES_PER_QUAD));
            int RowCount = Math.Max(1, (Surface.SampleHeight - Step + Step - 1) / Step);

            // Tessellate the scalar grid into two triangles per cell for painter-ordered 3D fill.
            for (int Sy = 0; Sy < Surface.SampleHeight - Step; Sy += Step)
            {
                for (int Sx = 0; Sx < Surface.SampleWidth - Step; Sx += Step)
                {
                    int Idx00 = Sy * Surface.SampleWidth + Sx;
                    int Idx10 = Sy * Surface.SampleWidth + Sx + Step;
                    int Idx11 = (Sy + Step) * Surface.SampleWidth + Sx + Step;
                    int Idx01 = (Sy + Step) * Surface.SampleWidth + Sx;

                    if (!Surface.Valid[Idx00] || !Surface.Valid[Idx10] || !Surface.Valid[Idx11] || !Surface.Valid[Idx01])
                        continue;

                    double Lon0 = Surface.MinLon + (Surface.MaxLon - Surface.MinLon) * Sx / (Surface.SampleWidth - 1);
                    double Lat0 = Surface.MaxLat - (Surface.MaxLat - Surface.MinLat) * Sy / (Surface.SampleHeight - 1);
                    double Lon1 = Surface.MinLon + (Surface.MaxLon - Surface.MinLon) * (Sx + Step) / (Surface.SampleWidth - 1);
                    double Lat1 = Surface.MaxLat - (Surface.MaxLat - Surface.MinLat) * (Sy + Step) / (Surface.SampleHeight - 1);

                    double E00 = FlatPlane ? Surface.MinElevation : Surface.GetScalarAtIndex(ContentView, Idx00);
                    double E01 = FlatPlane ? Surface.MinElevation : Surface.GetScalarAtIndex(ContentView, Idx01);
                    double E11 = FlatPlane ? Surface.MinElevation : Surface.GetScalarAtIndex(ContentView, Idx11);
                    double E10 = FlatPlane ? Surface.MinElevation : Surface.GetScalarAtIndex(ContentView, Idx10);
                    if (double.IsNaN(E00) || double.IsNaN(E01) || double.IsNaN(E11) || double.IsNaN(E10))
                        continue;

                    Camera.Project(Lat0, Lon0, E00, Viewport, FlatPlane, out float P0x, out float P0y, out double D0);
                    Camera.Project(Lat0, Lon1, E10, Viewport, FlatPlane, out float P1x, out float P1y, out double D1);
                    Camera.Project(Lat1, Lon1, E11, Viewport, FlatPlane, out float P2x, out float P2y, out double D2);
                    Camera.Project(Lat1, Lon0, E01, Viewport, FlatPlane, out float P3x, out float P3y, out double D3);

                    SKColor C0 = ColorForVertex(Surface, ContentView, Lat0, Lon0, E00, VMin, VMax, FlatPlane, ApplyHillshade, Idx00);
                    SKColor C1 = ColorForVertex(Surface, ContentView, Lat0, Lon1, E10, VMin, VMax, FlatPlane, ApplyHillshade, Idx10);
                    SKColor C2 = ColorForVertex(Surface, ContentView, Lat1, Lon1, E11, VMin, VMax, FlatPlane, ApplyHillshade, Idx11);
                    SKColor C3 = ColorForVertex(Surface, ContentView, Lat1, Lon0, E01, VMin, VMax, FlatPlane, ApplyHillshade, Idx01);

                    Triangles.Add(new MeshTriangle
                    {
                        P0 = new SKPoint(P0x, P0y),
                        P1 = new SKPoint(P1x, P1y),
                        P2 = new SKPoint(P2x, P2y),
                        C0 = C0,
                        C1 = C1,
                        C2 = C2,
                        D0 = D0,
                        D1 = D1,
                        D2 = D2,
                        Depth = (D0 + D1 + D2) * TRIANGLE_DEPTH_AVERAGE_FRACTION
                    });

                    Triangles.Add(new MeshTriangle
                    {
                        P0 = new SKPoint(P0x, P0y),
                        P1 = new SKPoint(P2x, P2y),
                        P2 = new SKPoint(P3x, P3y),
                        C0 = C0,
                        C1 = C2,
                        C2 = C3,
                        D0 = D0,
                        D1 = D2,
                        D2 = D3,
                        Depth = (D0 + D2 + D3) * TRIANGLE_DEPTH_AVERAGE_FRACTION
                    });
                }

                ProgressMapper.ReportSegment(Progress, PROGRESS_START_PERCENT, PROGRESS_MESH_ROW_END_PERCENT, ((Sy / Step) + 1) * PROGRESS_COMPLETE_PERCENT / RowCount);
            }

            Triangles.Sort((A, B) => A.Depth.CompareTo(B.Depth));
            Progress?.Report(PROGRESS_MESH_SORTED_PERCENT);

            MeshDepthBuffer DepthBuffer = MeshDepthBuffer.AlwaysVisible;
            if (BuildDepthBuffer)
                DepthBuffer = MeshDepthBuffer.Build(Viewport, Triangles);

            SKPoint[] Positions = new SKPoint[Triangles.Count * TRIANGLE_VERTEX_COUNT];
            SKColor[] Colors = new SKColor[Triangles.Count * TRIANGLE_VERTEX_COUNT];
            for (int I = 0; I < Triangles.Count; I++)
            {
                MeshTriangle Triangle = Triangles[I];
                int Base = I * TRIANGLE_VERTEX_COUNT;
                Positions[Base] = Triangle.P0;
                Positions[Base + 1] = Triangle.P1;
                Positions[Base + 2] = Triangle.P2;
                Colors[Base] = Triangle.C0;
                Colors[Base + 1] = Triangle.C1;
                Colors[Base + 2] = Triangle.C2;
            }

            using SKPaint Paint = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
                Color = SKColors.White,
                BlendMode = SKBlendMode.SrcOver
            };
            if (DrawMeshColors && Positions.Length > 0)
                Canvas.DrawVertices(SKVertexMode.Triangles, Positions, null, Colors, Paint);
            Progress?.Report(PROGRESS_COMPLETE_PERCENT);
            return DepthBuffer;
        }

        /// <summary>
        /// Draws a 3D line only where it is not hidden behind closer mesh surface.
        /// </summary>
        static private void DrawDepthTestedLine3D
            (
            SKCanvas Canvas,
            float X0,
            float Y0,
            double D0,
            float X1,
            float Y1,
            double D1,
            MeshDepthBuffer TopoDepth,
            SKPaint Paint
            )
        {
            DrawDepthTestedPolyline3D(
                Canvas,
                new List<(float X, float Y, double Depth)> { (X0, Y0, D0), (X1, Y1, D1) },
                TopoDepth,
                Paint,
                Closed: false);
        }

        /// <summary>
        /// Draws screen-space polyline segments that pass the topo depth test.
        /// </summary>
        static private void DrawDepthTestedPolyline3D
            (
            SKCanvas Canvas,
            IReadOnlyList<(float X, float Y, double Depth)> Vertices,
            MeshDepthBuffer TopoDepth,
            SKPaint Paint,
            bool Closed
            )
        {
            if (Vertices.Count < GeoConstants.MIN_POLYLINE_VERTICES)
                return;

            if (TopoDepth.IsPassthrough)
            {
                using SKPath Path = new SKPath();
                Path.MoveTo(Vertices[0].X, Vertices[0].Y);
                for (int I = 1; I < Vertices.Count; I++)
                    Path.LineTo(Vertices[I].X, Vertices[I].Y);
                if (Closed)
                    Path.Close();
                Canvas.DrawPath(Path, Paint);
                return;
            }

            List<(float X, float Y, double Depth)> Samples = new List<(float X, float Y, double Depth)>();
            int SegmentCount = Closed ? Vertices.Count : Vertices.Count - 1;
            for (int I = 0; I < SegmentCount; I++)
            {
                (float X0, float Y0, double D0) = Vertices[I];
                (float X1, float Y1, double D1) = Vertices[(I + 1) % Vertices.Count];
                AppendDepthTestedSegmentSamples(Samples, X0, Y0, D0, X1, Y1, D1);
            }

            foreach (IReadOnlyList<(float X, float Y)> Run in BuildVisibleScreenRuns(Samples, TopoDepth))
            {
                if (Run.Count < GeoConstants.MIN_POLYLINE_VERTICES)
                    continue;

                using SKPath Path = new SKPath();
                Path.MoveTo(Run[0].X, Run[0].Y);
                for (int I = 1; I < Run.Count; I++)
                    Path.LineTo(Run[I].X, Run[I].Y);
                Canvas.DrawPath(Path, Paint);
            }
        }

        /// <summary>
        /// Subdivides a 3D line segment into depth samples for visibility testing.
        /// </summary>
        static private void AppendDepthTestedSegmentSamples
            (
            List<(float X, float Y, double Depth)> Samples,
            float X0,
            float Y0,
            double D0,
            float X1,
            float Y1,
            double D1
            )
        {
            double Length = Math.Sqrt(((X1 - X0) * (X1 - X0)) + ((Y1 - Y0) * (Y1 - Y0)));
            int Steps = Math.Clamp((int)Math.Ceiling(Length / DEPTH_SEGMENT_SAMPLE_SPACING), DEPTH_SEGMENT_MIN_STEPS, DEPTH_SEGMENT_MAX_STEPS);
            if (Samples.Count > 0)
            {
                (float LastX, float LastY, double LastD) = Samples[^1];
                if (Math.Abs(LastX - X0) < DEPTH_SAMPLE_JOIN_EPSILON && Math.Abs(LastY - Y0) < DEPTH_SAMPLE_JOIN_EPSILON)
                    Samples[^1] = (X0, Y0, D0);
                else
                    Samples.Add((X0, Y0, D0));
            }
            else
            {
                Samples.Add((X0, Y0, D0));
            }

            for (int Step = 1; Step < Steps; Step++)
            {
                double T = (double)Step / Steps;
                Samples.Add((
                    (float)(X0 + ((X1 - X0) * T)),
                    (float)(Y0 + ((Y1 - Y0) * T)),
                    D0 + ((D1 - D0) * T)));
            }

            Samples.Add((X1, Y1, D1));
        }

        /// <summary>
        /// Splits depth-tested screen samples into contiguous visible polyline runs.
        /// </summary>
        static private List<List<(float X, float Y)>> BuildVisibleScreenRuns
            (
            IReadOnlyList<(float X, float Y, double Depth)> Samples,
            MeshDepthBuffer TopoDepth
            )
        {
            List<List<(float X, float Y)>> Runs = new List<List<(float X, float Y)>>();
            List<(float X, float Y)>? Current = null;

            foreach ((float X, float Y, double Depth) in Samples)
            {
                if (TopoDepth.IsVisible(X, Y, Depth))
                {
                    Current ??= new List<(float X, float Y)>();
                    Current.Add((X, Y));
                    continue;
                }

                if (Current != null && Current.Count >= GeoConstants.MIN_POLYLINE_VERTICES)
                    Runs.Add(Current);
                Current = null;
            }

            if (Current != null && Current.Count >= GeoConstants.MIN_POLYLINE_VERTICES)
                Runs.Add(Current);

            return Runs;
        }

        /// <summary>
        /// Resolves display colour for one mesh vertex including optional hillshade.
        /// </summary>
        static private SKColor ColorForVertex
            (
            FieldSurfaceSampler Surface,
            FieldContentView ContentView,
            double Lat,
            double Lon,
            double Elevation,
            double VMin,
            double VMax,
            bool FlatPlane,
            bool ApplyHillshade,
            int GridIndex
            )
        {
            double Value = FlatPlane ? Surface.SampleCutFill(Lat, Lon) : Elevation;
            SKColor Color = ArgbToSkia(NineBandColorScale.GetColor(Value, VMin, VMax));
            if (!ApplyHillshade || FlatPlane)
                return Color;

            // Use precomputed vertex hillshade so 3D mesh drawing avoids per-frame gradient work.
            double Shade = Surface.GetMeshHillshadeFactor(ContentView, GridIndex);
            return ModulateHillshadeColor(Color, Shade);
        }

        /// <summary>
        /// Scales RGB channels by a hillshade factor while preserving alpha.
        /// </summary>
        static internal SKColor ModulateHillshadeColor
            (
            SKColor Base,
            double Shade
            )
        {
            if (double.IsNaN(Shade))
                return Base;

            // Blend hillshade factor toward mid-grey so flat areas stay readable.
            double Mix = HILLSHADE_BASE_MIX + HILLSHADE_RANGE_MIX * Shade;
            return new SKColor(
                (byte)Math.Clamp(Base.Red * Mix, 0, COLOR_CHANNEL_MAX_BYTE),
                (byte)Math.Clamp(Base.Green * Mix, 0, COLOR_CHANNEL_MAX_BYTE),
                (byte)Math.Clamp(Base.Blue * Mix, 0, COLOR_CHANNEL_MAX_BYTE),
                Base.Alpha);
        }

        /// <summary>
        /// Projects contour polylines onto the 3D mesh surface.
        /// </summary>
        static private void DrawContours3D
            (
            SKCanvas Canvas,
            SKRect Viewport,
            FieldSurfaceSampler Surface,
            FieldContentView ContentView,
            FieldCamera3D Camera,
            MeshDepthBuffer TopoDepth,
            double VMin,
            double VMax,
            IProgress<int>? Progress = null
            )
        {
            if (VMax <= VMin)
            {
                Progress?.Report(PROGRESS_COMPLETE_PERCENT);
                return;
            }

            // Match 2D contour colouring with a slight band offset for legibility on the mesh.
            double BandOffset = (VMax - VMin) * RenderingConstants.CONTOUR_BAND_OFFSET_FRACTION;
            double ZLift = (VMax - VMin) * RenderingConstants.CONTOUR_Z_LIFT_FRACTION;
            IReadOnlyList<(double Level, IReadOnlyList<(double Lat, double Lon)> Points)> Polylines =
                Surface.GetContourPolylinesByLevel(ContentView, VMin, VMax, ProgressMapper.Subrange(Progress, PROGRESS_START_PERCENT, PROGRESS_CONTOUR_POLYLINE_END_PERCENT));

            using SKPaint Paint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = CONTOUR_STROKE_WIDTH_3D,
                IsAntialias = true,
                StrokeJoin = SKStrokeJoin.Round,
                StrokeCap = SKStrokeCap.Round
            };

            foreach ((double Level, IReadOnlyList<(double Lat, double Lon)> Polyline) in Polylines)
            {
                if (Polyline.Count < GeoConstants.MIN_POLYLINE_VERTICES)
                    continue;

                Paint.Color = ArgbToSkia(NineBandColorScale.GetLightenedColor(Level - BandOffset, VMin, VMax));

                List<(float X, float Y, double Depth)> ScreenPolyline = new List<(float X, float Y, double Depth)>(Polyline.Count);
                foreach ((double Lat, double Lon) in Polyline)
                {
                    double Elev = Level + ZLift;
                    Camera.Project(Lat, Lon, Elev, Viewport, FlatPlane: false, out float X, out float Y, out double Depth);
                    ScreenPolyline.Add((X, Y, Depth));
                }

                DrawDepthTestedPolyline3D(Canvas, ScreenPolyline, TopoDepth, Paint, Closed: false);
            }

            Progress?.Report(PROGRESS_COMPLETE_PERCENT);
        }

        /// <summary>
        /// Draws interior survey points projected onto the 3D surface.
        /// </summary>
        static private void DrawSurveyPoints3D
            (
            SKCanvas Canvas,
            SKRect Viewport,
            SurveyModel Survey,
            FieldSurfaceSampler Surface,
            FieldCamera3D Camera
            )
        {
            using SKPaint Paint = new SKPaint { Color = SURVEY_POINT_COLOR, IsAntialias = true, Style = SKPaintStyle.Fill };

            // Project interior points onto the sampled surface elevation in 3D.
            foreach (TopologyPoint Pt in Survey.InteriorPoints)
            {
                double Elev = Surface.SampleExisting(Pt.Latitude, Pt.Longitude);
                if (double.IsNaN(Elev))
                    Elev = Pt.ExistingElevation;
                if (double.IsNaN(Elev))
                    continue;

                Camera.Project(Pt.Latitude, Pt.Longitude, Elev, Viewport, FlatPlane: false, out float X, out float Y, out _);
                Canvas.DrawCircle(X, Y, SURVEY_POINT_RADIUS, Paint);
            }
        }

        /// <summary>
        /// Draws benchmark markers projected onto the 3D surface.
        /// </summary>
        static private void DrawBenchmarks3D
            (
            SKCanvas Canvas,
            SKRect Viewport,
            SurveyModel Survey,
            FieldSurfaceSampler Surface,
            FieldContentView ContentView,
            FieldCamera3D Camera
            )
        {
            using SKPaint Fill = new SKPaint { Color = BENCHMARK_FILL_COLOR, IsAntialias = true, Style = SKPaintStyle.Fill };
            using SKPaint Stroke = new SKPaint
            {
                Color = BENCHMARK_STROKE_COLOR,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = BENCHMARK_TRIANGLE_STROKE_WIDTH
            };
            using SKFont LabelFont = new SKFont { Size = BENCHMARK_LABEL_FONT_SIZE };

            bool DrapeOnFlatOverlay = ContentView == FieldContentView.CutFill;
            Surface.ComputeScalarRange(ContentView, out double VMin, out double VMax);

            // Drape benchmark markers on the active topo surface so they stay on the mesh under vertical exaggeration.
            foreach (Benchmark Bm in Survey.Benchmarks)
            {
                float MarkerX;
                float MarkerY;
                if (DrapeOnFlatOverlay)
                {
                    UTM.UTMCoordinate Point = UTM.FromLatLon(Bm.Location.Latitude, Bm.Location.Longitude);
                    double OffsetEastM = Point.Easting - Camera.CentreUtm.Easting;
                    double OffsetNorthM = Point.Northing - Camera.CentreUtm.Northing;
                    double OverlayZ = Camera.GroundPlaneClearanceM > 0
                        ? Camera.GroundPlaneClearanceM
                        : Field3DGeometryBuilder.ComputeFlatOverlayZLift(Camera);
                    Camera.ProjectModelOffset(OffsetEastM, OffsetNorthM, OverlayZ, Viewport, out MarkerX, out MarkerY, out _);
                }
                else
                {
                    double Elev = Surface.SampleScalar(ContentView, Bm.Location.Latitude, Bm.Location.Longitude);
                    if (double.IsNaN(Elev))
                        Elev = Bm.Elevation;
                    Camera.Project(Bm.Location.Latitude, Bm.Location.Longitude, Elev, Viewport, FlatPlane: false, out MarkerX, out MarkerY, out _);
                }

                using SKPath Tri = BuildBenchmarkTrianglePath(MarkerX, MarkerY);
                DrawBenchmarkTriangle(Canvas, Tri, Fill, Stroke);

                float LabelX = MarkerX + BENCHMARK_LABEL_OFFSET_X;
                float LabelY = MarkerY - BENCHMARK_LABEL_OFFSET_Y;
                SKColor LabelColor = ResolveBenchmarkLabelColor(
                    SampleBenchmarkLabelBackgroundAtLatLon(
                        Surface,
                        ContentView,
                        Bm.Location.Latitude,
                        Bm.Location.Longitude,
                        VMin,
                        VMax));
                using SKPaint LabelPaint = new SKPaint { Color = LabelColor, IsAntialias = true };
                Canvas.DrawText(Bm.Name, LabelX, LabelY, SKTextAlign.Left, LabelFont, LabelPaint);
            }
        }

        /// <summary>
        /// Converts ARGB uint to Skia colour with byte channel order adjusted.
        /// </summary>
        static internal SKColor ArgbToSkia
            (
            uint Argb
            )
        {
            // Reorder ARGB bytes into Skia's RGBA channel layout.
            byte A = (byte)((Argb >> ARGB_ALPHA_SHIFT) & ARGB_CHANNEL_MASK);
            byte R = (byte)((Argb >> ARGB_RED_SHIFT) & ARGB_CHANNEL_MASK);
            byte G = (byte)((Argb >> ARGB_GREEN_SHIFT) & ARGB_CHANNEL_MASK);
            byte B = (byte)(Argb & ARGB_CHANNEL_MASK);
            return new SKColor(R, G, B, A);
        }

        /// <summary>
        /// Maps longitude to Web Mercator world X for the given world size.
        /// </summary>
        static private double LonToWorldX
            (
            double Lon,
            double WorldSize
            )
        {
            // Web Mercator normalizes longitude to [0, WorldSize).
            return (Lon + GeoConstants.DEGREES_IN_HALF_CIRCLE) / GeoConstants.DEGREES_IN_FULL_CIRCLE * WorldSize;
        }

        /// <summary>
        /// Maps latitude to Web Mercator world Y for the given world size.
        /// </summary>
        static private double LatToWorldY
            (
            double Lat,
            double WorldSize
            )
        {
            // Inverse Gudermannian maps latitude into Mercator Y.
            double SinLat = Math.Sin(Lat * Math.PI / GeoConstants.DEGREES_IN_HALF_CIRCLE);
            double Y = GeoConstants.VIEWPORT_CENTRE_FRACTION - Math.Log((1.0 + SinLat) / (1.0 - SinLat)) / (WEB_MERCATOR_PI_FACTOR * Math.PI);
            return Y * WorldSize;
        }

        /// <summary>
        /// Maps Web Mercator world X back to longitude.
        /// </summary>
        static private double WorldXToLon
            (
            double X,
            double WorldSize
            )
        {
            // Invert normalized Mercator X back to longitude degrees.
            return X / WorldSize * GeoConstants.DEGREES_IN_FULL_CIRCLE - GeoConstants.DEGREES_IN_HALF_CIRCLE;
        }

        /// <summary>
        /// Maps Web Mercator world Y back to latitude.
        /// </summary>
        static private double WorldYToLat
            (
            double Y,
            double WorldSize
            )
        {
            // Invert Mercator Y through the inverse Gudermannian.
            double N = Math.PI - WEB_MERCATOR_INVERSE_SCALE * Math.PI * Y / WorldSize;
            return GeoConstants.DEGREES_IN_HALF_CIRCLE / Math.PI * Math.Atan(GeoConstants.VIEWPORT_CENTRE_FRACTION * (Math.Exp(N) - Math.Exp(-N)));
        }

        /// <summary>
        /// Draws benchmark markers on a haul scrub frame overlay.
        /// </summary>
        /// <param name="LabelBackgroundSampler">
        /// Optional screen-pixel sampler for label contrast against the haul frame beneath the overlay.
        /// </param>
        public static void DrawHaulBenchmarks2D
            (
            SKCanvas Canvas,
            SKRect Viewport,
            SurveyModel Survey,
            double CentreLat,
            double CentreLon,
            double MetresPerPixel,
            Func<int, int, SKColor>? LabelBackgroundSampler = null
            )
        {
            DrawBenchmarks2D(Canvas, Viewport, Survey, CentreLat, CentreLon, MetresPerPixel, LabelBackgroundSampler);
        }

        /// <summary>
        /// Draws haul transport flow arrows when the display toggle and haul data are available.
        /// </summary>
        static private void TryDrawHaulFlowArrows2D
            (
            SKCanvas Canvas,
            SKRect Viewport,
            ProjectDocument Project,
            DisplaySettings Display,
            double CentreLat,
            double CentreLon,
            double MetresPerPixel
            )
        {
            if (!Display.HaulFlow || Project.HaulData == null)
                return;

            HaulSettings Settings = UserSettingsService.Instance.ActiveHaulSettings;
            HaulFlowFieldResolver.ResolvedHaulFlow? Resolved = HaulFlowFieldResolver.TryResolve(
                Project.HaulData,
                Project,
                Settings);
            if (Resolved == null)
                return;

            FieldBounds? ClipBounds = FieldBounds.TryFromProject(Project, out FieldBounds Bounds) ? Bounds : null;
            HaulFlowArrowRenderer.Draw2D(Canvas, Viewport, Resolved, CentreLat, CentreLon, MetresPerPixel, ClipBounds);
        }

        /// <summary>
        /// Draws haul transport flow arrows draped on the active 3D surface when enabled.
        /// </summary>
        static private void TryDrawHaulFlowArrows3D
            (
            SKCanvas Canvas,
            SKRect Viewport,
            ProjectDocument Project,
            DisplaySettings Display,
            FieldSurfaceSampler Surface,
            FieldContentView ContentView,
            FieldCamera3D Camera
            )
        {
            if (!Display.HaulFlow || Project.HaulData == null)
                return;

            HaulSettings Settings = UserSettingsService.Instance.ActiveHaulSettings;
            HaulFlowFieldResolver.ResolvedHaulFlow? Resolved = HaulFlowFieldResolver.TryResolve(
                Project.HaulData,
                Project,
                Settings);
            if (Resolved == null)
                return;

            FieldBounds? ClipBounds = FieldBounds.TryFromProject(Project, out FieldBounds Bounds) ? Bounds : null;
            HaulFlowArrowRenderer.Draw3D(Canvas, Viewport, Resolved, Surface, ContentView, Camera, ClipBounds);
        }
    }
}
