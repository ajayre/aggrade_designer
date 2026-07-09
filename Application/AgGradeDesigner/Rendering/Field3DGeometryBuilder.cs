using System.Diagnostics;
using System.Threading;

using AgGradeDesigner.Core;
using AgGradeDesigner.Core.Color;
using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Core.Rendering.Vulkan;
using AgGradeDesigner.Core.Survey;
using AgGradeDesigner.Diagnostics;

using SkiaSharp;

namespace AgGradeDesigner.Rendering
{
    /// <summary>
    /// Builds GPU vertex buffers for the 3D field view using the same topology as the Skia renderer.
    /// </summary>
    static internal partial class Field3DGeometryBuilder
    {
        /// <summary>Minimum model-space Z lift for flat overlays above the satellite ground plane.</summary>
        private const double FLAT_OVERLAY_Z_LIFT_MIN = 0.5;
        /// <summary>No Z lift applied when flat overlays are not raised.</summary>
        private const double FLAT_OVERLAY_Z_LIFT_NONE = 0.0;
        /// <summary>Fraction of horizontal extent used to compute flat overlay Z lift.</summary>
        private const double FLAT_OVERLAY_Z_LIFT_FRACTION = 0.00015;
        /// <summary>Initial list capacity for satellite plinth background vertices.</summary>
        private const int BACKGROUND_MESH_INITIAL_CAPACITY = 4096;
        /// <summary>Initial list capacity for textured tile meshes.</summary>
        private const int TEXTURE_MESH_INITIAL_CAPACITY = 64;
        /// <summary>Initial list capacity for contour and decoration line vertices.</summary>
        private const int LINE_VERTEX_INITIAL_CAPACITY = 4096;
        /// <summary>Initial list capacity for field border line vertices.</summary>
        private const int BORDER_LINE_INITIAL_CAPACITY = 512;
        /// <summary>Mesh sampling step when grid dimension exceeds the coarse threshold.</summary>
        private const int MESH_SAMPLING_COARSE_STEP = 2;
        /// <summary>Number of triangles emitted per sampled grid quad.</summary>
        private const int TRIANGLES_PER_MESH_QUAD = 2;
        /// <summary>Divisor converting opacity percent to 0–1 unit range.</summary>
        private const double OPACITY_PERCENT_TO_UNIT = 100.0;
        /// <summary>Minimum surface overlay opacity in unit range.</summary>
        private const double MIN_OPACITY_UNIT = 0.0;
        /// <summary>Maximum surface overlay opacity in unit range.</summary>
        private const double MAX_OPACITY_UNIT = 1.0;
        /// <summary>Fraction used when averaging elevations at border segment endpoints.</summary>
        private const double BORDER_ELEVATION_AVERAGE_FRACTION = 0.5;
        /// <summary>Coordinate tolerance when matching boundary point elevations.</summary>
        private const double BOUNDARY_COORDINATE_EPSILON = 1e-8;
        /// <summary>Divisor mapping byte colour channels to GPU float range.</summary>
        private const float COLOR_CHANNEL_MAX = 255f;
        /// <summary>Opaque alpha for non-transparent clear colours.</summary>
        private const float CLEAR_COLOR_OPAQUE = 1f;
        /// <summary>Transparent alpha for overlay frame clear colours.</summary>
        private const float CLEAR_COLOR_TRANSPARENT = 0f;
        /// <summary>Fraction of depth span added as padding to the GPU depth range.</summary>
        private const double DEPTH_RANGE_PAD_FRACTION = 0.01;
        /// <summary>Minimum absolute depth range padding in model units.</summary>
        private const double DEPTH_RANGE_MIN_PAD = 1e-3;
        /// <summary>Fallback minimum camera depth when geometry is empty.</summary>
        private const double FALLBACK_DEPTH_MIN = 0;
        /// <summary>Fallback maximum camera depth when geometry is empty.</summary>
        private const double FALLBACK_DEPTH_MAX = 1;
        /// <summary>Progress percent reported when coloured mesh build completes.</summary>
        private const int PROGRESS_MESH_END_PERCENT = 60;
        /// <summary>Progress percent reported when contour line build completes.</summary>
        private const int PROGRESS_CONTOUR_END_PERCENT = 85;
        /// <summary>Progress percent indicating geometry build is complete.</summary>
        private const int PROGRESS_COMPLETE_PERCENT = 100;
        /// <summary>Minimum point count required to emit a contour polyline segment.</summary>
        private const int MIN_POLYLINE_POINTS = 2;
        /// <summary>Minimum boundary points required to draw a closed border ring.</summary>
        private const int MIN_BOUNDARY_POINTS = 3;
        /// <summary>Vertices emitted per subdivided texture tile (two triangles).</summary>
        private const int VERTICES_PER_TEXTURE_TRIANGLE = 6;
        /// <summary>Base V coordinate when flipping Web Mercator tile V for GPU textures.</summary>
        private const float TEXTURE_V_FLIP_BASE = 1.0f;

        /// <summary>Fraction of lat/lon span used when resolving the fixed field-centre UTM anchor.</summary>
        private const double FIELD_CENTRE_FRACTION = 0.5;

        /// <summary>Contour levels processed per parallel worker to avoid tiny-task scheduling overhead.</summary>
        private const int CONTOUR_LEVEL_PARALLEL_CHUNK = 128;

        /// <summary>Bump to invalidate stale static geometry when satellite layout rules change.</summary>
        private const int SATELLITE_STATIC_LAYOUT_VERSION = 2;

        /// <summary>Bump to invalidate stale static geometry when true-scale relief storage rules change.</summary>
        private const int TRUE_SCALE_RELIEF_STORAGE_VERSION = 1;

        /// <summary>Background clear colour matching the satellite ground plane.</summary>
        private static readonly SKColor SatelliteGroundBackgroundColor = new SKColor(
            RenderingConstants.SATELLITE_GROUND_BACKGROUND_RED,
            RenderingConstants.SATELLITE_GROUND_BACKGROUND_GREEN,
            RenderingConstants.SATELLITE_GROUND_BACKGROUND_BLUE);

        /// <summary>
        /// Small model-space lift so topo and flat overlays sit above the satellite ground plane.
        /// </summary>
        static internal double ComputeFlatOverlayZLift
            (
            FieldCamera3D Camera
            )
        {
            return ComputeFlatOverlayZLift(Camera.HorizontalExtentM);
        }

        /// <summary>
        /// Small model-space lift from field horizontal extent alone.
        /// </summary>
        static internal double ComputeFlatOverlayZLift
            (
            FieldSurfaceSampler Surface
            )
        {
            UTM.UTMCoordinate SouthWest = UTM.FromLatLon(Surface.MinLat, Surface.MinLon);
            UTM.UTMCoordinate NorthEast = UTM.FromLatLon(Surface.MaxLat, Surface.MaxLon);
            double HorizontalExtentM = Math.Max(
                Math.Abs(NorthEast.Easting - SouthWest.Easting),
                Math.Abs(NorthEast.Northing - SouthWest.Northing));
            return ComputeFlatOverlayZLift(HorizontalExtentM);
        }

        /// <summary>
        /// Computes flat-overlay Z lift from horizontal field extent alone.
        /// </summary>
        /// <param name="HorizontalExtentM">Larger horizontal span of the field in metres.</param>
        /// <returns>Model-space lift applied above the satellite ground plane.</returns>
        static private double ComputeFlatOverlayZLift
            (
            double HorizontalExtentM
            )
        {
            return Math.Max(FLAT_OVERLAY_Z_LIFT_MIN, HorizontalExtentM * FLAT_OVERLAY_Z_LIFT_FRACTION);
        }

        /// <summary>
        /// Builds mesh and line geometry for one 3D orbit frame.
        /// </summary>
        /// <param name="haulRouteScrubIndex">Pinned haul route scrub index for haul 3D, or -1 when not applicable.</param>
        static public GpuFieldFrame BuildFrame
            (
            FieldSurfaceSampler Surface,
            ProjectDocument Project,
            FieldContentView ContentView,
            DisplaySettings Display,
            FieldCamera3D Camera,
            SKRect Viewport,
            SatelliteTileService? Satellite = null,
            bool IncludeColoredMesh = true,
            bool IncludeLineDecorations = true,
            bool UsePlanViewSatellite = false,
            IProgress<int>? Progress = null,
            int haulRouteScrubIndex = -1
            )
        {
            Progress?.Report(0);
            bool ShowSatellite = Display.Satellite && Satellite != null;
            bool FlatPlane = ContentView == FieldContentView.CutFill;
            Field3DGeometryCacheKey CacheKey = new Field3DGeometryCacheKey
            {
                Project = Project,
                ContentView = ContentView,
                Hillshade = Display.Hillshade,
                HaulRouteScrubIndex = haulRouteScrubIndex,
                TrueScaleReliefStorageVersion = TRUE_SCALE_RELIEF_STORAGE_VERSION,
            };

            UTM.UTMCoordinate FieldCentreUtm = ResolveFieldCentreUtm(Surface);
            FieldCamera3D StorageCamera = CreateTrueScaleReliefStorageCamera(Surface, ContentView);
            GpuFieldStaticGeometry StaticGeometry = Field3DGeometryCache.GetOrBuild(
                CacheKey,
                () => BuildStaticGeometry(
                    Surface,
                    Project,
                    ContentView,
                    Display,
                    StorageCamera,
                    Progress,
                    haulRouteScrubIndex));

            // intent: exaggeration is applied after cache lookup so slider drags reuse mesh topology
            StaticGeometry = ApplyVerticalExaggerationToStaticGeometry(StaticGeometry, Camera, FlatPlane);

            if (ShowSatellite && Satellite != null)
            {
                StaticGeometry = UsePlanViewSatellite
                    ? BuildPlanViewSatelliteGeometry(
                        StaticGeometry,
                        Satellite,
                        Camera,
                        FieldCentreUtm,
                        Viewport,
                        Surface.MinElevation)
                    : BuildOrbitSatelliteGeometry(
                        StaticGeometry,
                        Surface,
                        Satellite,
                        Camera,
                        FieldCentreUtm,
                        Surface.MinElevation);
            }

            Progress?.Report(PROGRESS_COMPLETE_PERCENT);
            float SurfaceOverlayOpacity = (float)Math.Clamp(Display.TopoCutFillOpacityPercent / OPACITY_PERCENT_TO_UNIT, MIN_OPACITY_UNIT, MAX_OPACITY_UNIT);
            return AssembleFrame(
                StaticGeometry,
                Project,
                ContentView,
                FieldCentreUtm,
                Camera,
                Viewport,
                Display,
                IncludeColoredMesh,
                IncludeLineDecorations,
                ShowSatellite,
                ShowSatellite ? Satellite!.CacheGeneration : 0,
                SurfaceOverlayOpacity,
                haulRouteScrubIndex);
        }

        /// <summary>
        /// Builds a transparent overlay frame containing only coloured mesh and line decorations.
        /// </summary>
        /// <param name="haulRouteScrubIndex">Pinned haul route scrub index for haul 3D, or -1 when not applicable.</param>
        static public GpuFieldFrame BuildOverlayFrame
            (
            FieldSurfaceSampler Surface,
            ProjectDocument Project,
            FieldContentView ContentView,
            DisplaySettings Display,
            FieldCamera3D Camera,
            SKRect Viewport,
            IProgress<int>? Progress = null,
            int haulRouteScrubIndex = -1
            )
        {
            GpuFieldFrame Frame = BuildFrame(
                Surface,
                Project,
                ContentView,
                Display,
                Camera,
                Viewport,
                Satellite: null,
                IncludeColoredMesh: true,
                IncludeLineDecorations: true,
                UsePlanViewSatellite: false,
                Progress,
                haulRouteScrubIndex);

            return new GpuFieldFrame
            {
                BackgroundMeshVertices = Array.Empty<GpuVertex>(),
                TextureMeshes = Array.Empty<GpuTextureMesh>(),
                CombinedTextureVertices = Array.Empty<GpuTexturedVertex>(),
                TextureDraws = Array.Empty<GpuTextureDrawSpec>(),
                MeshVertices = Frame.MeshVertices,
                LineVertices = Frame.LineVertices,
                BorderLineVertices = Array.Empty<GpuVertex>(),
                Camera = Frame.Camera,
                SatelliteCacheGeneration = 0,
                ClearR = CLEAR_COLOR_TRANSPARENT,
                ClearG = CLEAR_COLOR_TRANSPARENT,
                ClearB = CLEAR_COLOR_TRANSPARENT,
                ClearA = CLEAR_COLOR_TRANSPARENT
            };
        }

        /// <summary>
        /// Builds model-space geometry that does not depend on orbit heading or pitch.
        /// </summary>
        /// <param name="haulRouteScrubIndex">Pinned haul route scrub index for haul 3D diagnostics, or -1 when not applicable.</param>
        static private GpuFieldStaticGeometry BuildStaticGeometry
            (
            FieldSurfaceSampler Surface,
            ProjectDocument Project,
            FieldContentView ContentView,
            DisplaySettings Display,
            FieldCamera3D Camera,
            IProgress<int>? Progress,
            int haulRouteScrubIndex = -1
            )
        {
            Surface.ComputeScalarRange(ContentView, out double VMin, out double VMax);
            bool FlatPlane = ContentView == FieldContentView.CutFill;
            bool ApplyHillshade = Display.Hillshade && !FlatPlane;
            FieldLocalMetresProjection LocalProjection = FieldLocalMetresProjection.FromSurface(Surface);
            long HillshadeMs = 0;
            if (ApplyHillshade)
            {
                Stopwatch HillshadeWatch = Stopwatch.StartNew();
                Surface.GetMeshHillshadeFactor(ContentView, 0);
                HillshadeWatch.Stop();
                HillshadeMs = HillshadeWatch.ElapsedMilliseconds;
            }

            List<GpuVertex> BackgroundMesh = new List<GpuVertex>(BACKGROUND_MESH_INITIAL_CAPACITY);
            List<GpuVertex> MeshVertices = new List<GpuVertex>(Surface.SampleWidth * Surface.SampleHeight * TRIANGLES_PER_MESH_QUAD);
            List<GpuVertex> LineVertices = new List<GpuVertex>(LINE_VERTEX_INITIAL_CAPACITY);
            List<GpuVertex> BorderLineVertices = new List<GpuVertex>(BORDER_LINE_INITIAL_CAPACITY);
            double DepthMin = double.PositiveInfinity;
            double DepthMax = double.NegativeInfinity;

            double FlatOverlayZLift = FlatPlane && Display.Satellite ? ComputeFlatOverlayZLift(Surface) : FLAT_OVERLAY_Z_LIFT_NONE;
            // intent: plinth sides are cheap; satellite on/off is applied when assembling the frame
            AppendPlinthSides(
                BackgroundMesh,
                ref DepthMin,
                ref DepthMax,
                Camera);

            int Step = Math.Max(Surface.SampleWidth, Surface.SampleHeight) > RenderingConstants.MESH_STEP_THRESHOLD ? MESH_SAMPLING_COARSE_STEP : 1;
            int RowCount = Math.Max(1, (Surface.SampleHeight - Step + Step - 1) / Step);
            Stopwatch MeshWatch = Stopwatch.StartNew();
            BuildMeshVerticesParallel(
                MeshVertices,
                ref DepthMin,
                ref DepthMax,
                Surface,
                ContentView,
                LocalProjection,
                Camera,
                FlatPlane,
                FlatOverlayZLift,
                VMin,
                VMax,
                ApplyHillshade,
                Step,
                RowCount,
                Progress);
            MeshWatch.Stop();

            // intent: contour polylines are cached once; display toggles hide them during frame assembly
            int ContourLevelCount = 0;
            int ContourPolylinePointCount = 0;
            long ContourFetchMs = 0;
            long ContourGpuMs = 0;
            if (!FlatPlane && VMax > VMin)
            {
                double BandOffset = (VMax - VMin) * RenderingConstants.CONTOUR_BAND_OFFSET_FRACTION;
                double ZLift = (VMax - VMin) * RenderingConstants.CONTOUR_Z_LIFT_FRACTION;
                Stopwatch ContourFetchWatch = Stopwatch.StartNew();
                IReadOnlyList<(double Level, IReadOnlyList<(double Lat, double Lon)> Points)> Polylines =
                    Surface.GetContourPolylinesByLevel(ContentView, VMin, VMax, ProgressMapper.Subrange(Progress, PROGRESS_MESH_END_PERCENT, PROGRESS_CONTOUR_END_PERCENT));
                ContourFetchWatch.Stop();
                ContourFetchMs = ContourFetchWatch.ElapsedMilliseconds;
                ContourLevelCount = Polylines.Count;
                Stopwatch ContourGpuWatch = Stopwatch.StartNew();
                ContourPolylinePointCount = BuildContourLineVerticesParallel(
                    LineVertices,
                    ref DepthMin,
                    ref DepthMax,
                    Polylines,
                    LocalProjection,
                    Camera,
                    BandOffset,
                    ZLift,
                    VMin,
                    VMax);
                ContourGpuWatch.Stop();
                ContourGpuMs = ContourGpuWatch.ElapsedMilliseconds;
            }
            else
            {
                ProgressMapper.ReportSegment(Progress, PROGRESS_MESH_END_PERCENT, PROGRESS_CONTOUR_END_PERCENT, PROGRESS_COMPLETE_PERCENT);
            }

            if (Project.Survey != null)
                AppendFieldBorder(BorderLineVertices, ref DepthMin, ref DepthMax, LocalProjection, Camera, Surface, Project.Survey, ContentView, FlatPlane, FlatOverlayZLift);

            float ClearR = CLEAR_COLOR_OPAQUE;
            float ClearG = CLEAR_COLOR_OPAQUE;
            float ClearB = CLEAR_COLOR_OPAQUE;
            float ClearA = CLEAR_COLOR_OPAQUE;

            if (ContentView == FieldContentView.Haul)
            {
                HaulThreeDInstrumentation.LogStaticGeometryBuild(
                    Surface,
                    VMin,
                    VMax,
                    MeshVertices.Count,
                    LineVertices.Count,
                    ContourLevelCount,
                    ContourPolylinePointCount,
                    haulRouteScrubIndex,
                    HillshadeMs,
                    MeshWatch.ElapsedMilliseconds,
                    ContourFetchMs,
                    ContourGpuMs);
            }

            return new GpuFieldStaticGeometry
            {
                BackgroundMeshVertices = BackgroundMesh.ToArray(),
                TextureMeshes = Array.Empty<GpuTextureMesh>(),
                CombinedTextureVertices = Array.Empty<GpuTexturedVertex>(),
                TextureDraws = Array.Empty<GpuTextureDrawSpec>(),
                MeshVertices = MeshVertices.ToArray(),
                LineVertices = LineVertices.ToArray(),
                BorderLineVertices = BorderLineVertices.ToArray(),
                ClearR = ClearR,
                ClearG = ClearG,
                ClearB = ClearB,
                ClearA = ClearA
            };
        }

        /// <summary>
        /// Replaces cached plinth satellite geometry with viewport-aligned tiles for plan view.
        /// </summary>
        static private GpuFieldStaticGeometry BuildPlanViewSatelliteGeometry
            (
            GpuFieldStaticGeometry StaticGeometry,
            SatelliteTileService Satellite,
            FieldCamera3D Camera,
            UTM.UTMCoordinate FieldCentreUtm,
            SKRect Viewport,
            double PlaneElevation
            )
        {
            List<GpuTextureMesh> TextureMeshes = new List<GpuTextureMesh>(TEXTURE_MESH_INITIAL_CAPACITY);
            double DepthMin = double.PositiveInfinity;
            double DepthMax = double.NegativeInfinity;
            AppendPlanViewSatelliteBackground(
                TextureMeshes,
                ref DepthMin,
                ref DepthMax,
                Satellite,
                Camera,
                FieldCentreUtm,
                Viewport,
                PlaneElevation);

            return MergeSatelliteTextures(StaticGeometry, TextureMeshes, includePlinthSides: false, usePlanViewClearColor: true);
        }

        /// <summary>
        /// Adds current plinth tile imagery to cached plinth-side geometry for orbit view.
        /// </summary>
        static private GpuFieldStaticGeometry BuildOrbitSatelliteGeometry
            (
            GpuFieldStaticGeometry StaticGeometry,
            FieldSurfaceSampler Surface,
            SatelliteTileService Satellite,
            FieldCamera3D Camera,
            UTM.UTMCoordinate FieldCentreUtm,
            double PlaneElevation
            )
        {
            List<GpuTextureMesh> TextureMeshes = new List<GpuTextureMesh>(TEXTURE_MESH_INITIAL_CAPACITY);
            double DepthMin = double.PositiveInfinity;
            double DepthMax = double.NegativeInfinity;
            AppendOrbitSatelliteTiles(
                TextureMeshes,
                ref DepthMin,
                ref DepthMax,
                Surface,
                Satellite,
                Camera,
                FieldCentreUtm,
                PlaneElevation);

            return MergeSatelliteTextures(StaticGeometry, TextureMeshes, includePlinthSides: true);
        }

        /// <summary>
        /// Combines cached mesh geometry with freshly resolved satellite tile textures.
        /// </summary>
        static private GpuFieldStaticGeometry MergeSatelliteTextures
            (
            GpuFieldStaticGeometry StaticGeometry,
            List<GpuTextureMesh> TextureMeshes,
            bool includePlinthSides,
            bool usePlanViewClearColor = false
            )
        {
            float ClearR = StaticGeometry.ClearR;
            float ClearG = StaticGeometry.ClearG;
            float ClearB = StaticGeometry.ClearB;
            if (usePlanViewClearColor)
            {
                ClearR = RenderingConstants.SATELLITE_2D_BACKGROUND_RED / COLOR_CHANNEL_MAX;
                ClearG = RenderingConstants.SATELLITE_2D_BACKGROUND_GREEN / COLOR_CHANNEL_MAX;
                ClearB = RenderingConstants.SATELLITE_2D_BACKGROUND_BLUE / COLOR_CHANNEL_MAX;
            }

            return new GpuFieldStaticGeometry
            {
                BackgroundMeshVertices = includePlinthSides ? StaticGeometry.BackgroundMeshVertices : Array.Empty<GpuVertex>(),
                TextureMeshes = TextureMeshes.ToArray(),
                CombinedTextureVertices = BuildCombinedTextureVertices(TextureMeshes, out GpuTextureDrawSpec[] TextureDraws),
                TextureDraws = TextureDraws,
                MeshVertices = StaticGeometry.MeshVertices,
                LineVertices = StaticGeometry.LineVertices,
                BorderLineVertices = StaticGeometry.BorderLineVertices,
                ClearR = ClearR,
                ClearG = ClearG,
                ClearB = ClearB,
                ClearA = StaticGeometry.ClearA
            };
        }

        /// <summary>
        /// Concatenates per-tile textured vertices and builds draw-range specs for GPU upload.
        /// </summary>
        /// <param name="TextureMeshes">Individual tile meshes with local vertex arrays.</param>
        /// <param name="DrawSpecs">Output draw ranges indexing the combined buffer.</param>
        /// <returns>Single concatenated textured vertex array.</returns>
        static private GpuTexturedVertex[] BuildCombinedTextureVertices
            (
            List<GpuTextureMesh> TextureMeshes,
            out GpuTextureDrawSpec[] DrawSpecs
            )
        {
            if (TextureMeshes.Count == 0)
            {
                DrawSpecs = Array.Empty<GpuTextureDrawSpec>();
                return Array.Empty<GpuTexturedVertex>();
            }

            List<GpuTexturedVertex> Combined = new List<GpuTexturedVertex>();
            List<GpuTextureDrawSpec> Specs = new List<GpuTextureDrawSpec>(TextureMeshes.Count);
            for (int MeshIndex = 0; MeshIndex < TextureMeshes.Count; MeshIndex++)
            {
                GpuTextureMesh Mesh = TextureMeshes[MeshIndex];
                if (Mesh.Vertices.Length == 0)
                    continue;

                Specs.Add(new GpuTextureDrawSpec
                {
                    MeshIndex = MeshIndex,
                    FirstVertex = (uint)Combined.Count,
                    VertexCount = (uint)Mesh.Vertices.Length
                });
                Combined.AddRange(Mesh.Vertices);
            }

            DrawSpecs = Specs.ToArray();
            return Combined.ToArray();
        }

        /// <summary>
        /// Applies the active camera to cached model-space geometry.
        /// </summary>
        /// <param name="Project">Active project used for contour visibility rules.</param>
        /// <param name="ContentView">Scalar channel used for contour visibility rules.</param>
        /// <param name="Display">Active display toggles controlling contour visibility and clear colour.</param>
        /// <param name="ShowSatellite">True when satellite imagery is drawn behind the mesh.</param>
        /// <param name="haulRouteScrubIndex">Pinned haul route scrub index for haul 3D diagnostics, or -1 when not applicable.</param>
        static private GpuFieldFrame AssembleFrame
            (
            GpuFieldStaticGeometry StaticGeometry,
            ProjectDocument Project,
            FieldContentView ContentView,
            UTM.UTMCoordinate FieldCentreUtm,
            FieldCamera3D Camera,
            SKRect Viewport,
            DisplaySettings Display,
            bool IncludeColoredMesh,
            bool IncludeLineDecorations,
            bool ShowSatellite,
            int SatelliteCacheGeneration,
            float SurfaceOverlayOpacity = 1f,
            int haulRouteScrubIndex = -1
            )
        {
            (double DepthMin, double DepthMax) = ComputeDepthRange(Camera, FieldCentreUtm, StaticGeometry);
            bool DrawContours = IncludeLineDecorations &&
                                ContourVisibility.ShouldDrawContours(Display, Project, ContentView);

            float ClearR = CLEAR_COLOR_OPAQUE;
            float ClearG = CLEAR_COLOR_OPAQUE;
            float ClearB = CLEAR_COLOR_OPAQUE;
            float ClearA = CLEAR_COLOR_OPAQUE;
            if (ShowSatellite)
            {
                ClearR = SatelliteGroundBackgroundColor.Red / COLOR_CHANNEL_MAX;
                ClearG = SatelliteGroundBackgroundColor.Green / COLOR_CHANNEL_MAX;
                ClearB = SatelliteGroundBackgroundColor.Blue / COLOR_CHANNEL_MAX;
            }

            GpuVertex[] FrameLineVertices = DrawContours ? StaticGeometry.LineVertices : Array.Empty<GpuVertex>();
            if (ContentView == FieldContentView.Haul)
            {
                HaulThreeDInstrumentation.LogFrameAssembly(
                    Display,
                    IncludeColoredMesh,
                    IncludeLineDecorations,
                    DrawContours,
                    StaticGeometry.LineVertices.Length,
                    FrameLineVertices.Length,
                    haulRouteScrubIndex);
            }

            return new GpuFieldFrame
            {
                BackgroundMeshVertices = ShowSatellite ? StaticGeometry.BackgroundMeshVertices : Array.Empty<GpuVertex>(),
                TextureMeshes = StaticGeometry.TextureMeshes,
                CombinedTextureVertices = StaticGeometry.CombinedTextureVertices,
                TextureDraws = StaticGeometry.TextureDraws,
                MeshVertices = IncludeColoredMesh ? StaticGeometry.MeshVertices : Array.Empty<GpuVertex>(),
                LineVertices = FrameLineVertices,
                BorderLineVertices = IncludeLineDecorations ? StaticGeometry.BorderLineVertices : Array.Empty<GpuVertex>(),
                Camera = Camera.ToGpuUniforms(Viewport, DepthMin, DepthMax, FieldCentreUtm),
                SatelliteCacheGeneration = SatelliteCacheGeneration,
                ClearR = ClearR,
                ClearG = ClearG,
                ClearB = ClearB,
                ClearA = ClearA,
                SurfaceOverlayOpacity = SurfaceOverlayOpacity
            };
        }

        /// <summary>
        /// Scans all static geometry and returns padded min/max camera depth for GPU uniforms.
        /// </summary>
        /// <param name="Camera">Active orbit camera for depth projection.</param>
        /// <param name="StaticGeometry">Model-space geometry to measure.</param>
        /// <returns>Depth range with padding, or fallback when empty.</returns>
        static private (double DepthMin, double DepthMax) ComputeDepthRange
            (
            FieldCamera3D Camera,
            UTM.UTMCoordinate FieldCentreUtm,
            GpuFieldStaticGeometry StaticGeometry
            )
        {
            double DepthMin = double.PositiveInfinity;
            double DepthMax = double.NegativeInfinity;
            double PivotEastM = FieldCentreUtm.Easting - Camera.CentreUtm.Easting;
            double PivotNorthM = FieldCentreUtm.Northing - Camera.CentreUtm.Northing;

            foreach (GpuVertex Vertex in StaticGeometry.BackgroundMeshVertices)
                TrackDepth(Camera, Vertex.EastM + PivotEastM, Vertex.NorthM + PivotNorthM, Vertex.ModelZ, ref DepthMin, ref DepthMax);
            foreach (GpuVertex Vertex in StaticGeometry.MeshVertices)
                TrackDepth(Camera, Vertex.EastM + PivotEastM, Vertex.NorthM + PivotNorthM, Vertex.ModelZ, ref DepthMin, ref DepthMax);
            foreach (GpuVertex Vertex in StaticGeometry.LineVertices)
                TrackDepth(Camera, Vertex.EastM + PivotEastM, Vertex.NorthM + PivotNorthM, Vertex.ModelZ, ref DepthMin, ref DepthMax);
            foreach (GpuVertex Vertex in StaticGeometry.BorderLineVertices)
                TrackDepth(Camera, Vertex.EastM + PivotEastM, Vertex.NorthM + PivotNorthM, Vertex.ModelZ, ref DepthMin, ref DepthMax);
            foreach (GpuTextureMesh TextureMesh in StaticGeometry.TextureMeshes)
            {
                foreach (GpuTexturedVertex Vertex in TextureMesh.Vertices)
                    TrackDepth(Camera, Vertex.EastM + PivotEastM, Vertex.NorthM + PivotNorthM, Vertex.ModelZ, ref DepthMin, ref DepthMax);
            }

            if (double.IsPositiveInfinity(DepthMin) || double.IsNegativeInfinity(DepthMax))
                return (FALLBACK_DEPTH_MIN, FALLBACK_DEPTH_MAX);

            double Pad = Math.Max((DepthMax - DepthMin) * DEPTH_RANGE_PAD_FRACTION, DEPTH_RANGE_MIN_PAD);
            return (DepthMin - Pad, DepthMax + Pad);
        }

        /// <summary>
        /// Fast field-relative metres projection for static geometry over small working areas.
        /// </summary>
        private readonly struct FieldLocalMetresProjection
        {
            /// <summary>Field-centre latitude in decimal degrees.</summary>
            public double CentreLat { get; init; }

            /// <summary>Field-centre longitude in decimal degrees.</summary>
            public double CentreLon { get; init; }

            /// <summary>Metres per degree of latitude at the field centre.</summary>
            public double MetresPerDegreeLat { get; init; }

            /// <summary>Metres per degree of longitude at the field centre.</summary>
            public double MetresPerDegreeLon { get; init; }

            /// <summary>
            /// Builds a local metres projection anchored at the sampled field centre.
            /// </summary>
            /// <param name="Surface">Sampled field defining geographic extents.</param>
            /// <returns>Projection constants for field-relative east/north conversion.</returns>
            public static FieldLocalMetresProjection FromSurface
                (
                FieldSurfaceSampler Surface
                )
            {
                double CentreLat = (Surface.MinLat + Surface.MaxLat) * FIELD_CENTRE_FRACTION;
                double CentreLon = (Surface.MinLon + Surface.MaxLon) * FIELD_CENTRE_FRACTION;
                double MetresPerDegreeLat = FieldConstants.METRES_PER_DEGREE_LAT;
                double MetresPerDegreeLon = MetresPerDegreeLat * Math.Cos(
                    CentreLat * Math.PI / FieldConstants.DEGREES_PER_RADIAN);
                return new FieldLocalMetresProjection
                {
                    CentreLat = CentreLat,
                    CentreLon = CentreLon,
                    MetresPerDegreeLat = MetresPerDegreeLat,
                    MetresPerDegreeLon = MetresPerDegreeLon
                };
            }

            /// <summary>
            /// Converts WGS-84 coordinates to field-centre-relative east/north metres.
            /// </summary>
            /// <param name="Lat">Point latitude in decimal degrees.</param>
            /// <param name="Lon">Point longitude in decimal degrees.</param>
            /// <returns>East and north offsets in metres from the field centre.</returns>
            public (double EastM, double NorthM) LatLonToFieldMetres
                (
                double Lat,
                double Lon
                )
            {
                // intent: linear local metres are sub-metre accurate over AgGrade field extents
                double EastM = (Lon - CentreLon) * MetresPerDegreeLon;
                double NorthM = (Lat - CentreLat) * MetresPerDegreeLat;
                return (EastM, NorthM);
            }
        }

        /// <summary>Vertices and depth span produced while building one sampled mesh row.</summary>
        private readonly struct MeshRowBuildResult
        {
            /// <summary>Coloured mesh vertices for this row of grid quads.</summary>
            public List<GpuVertex> Vertices { get; init; }

            /// <summary>Minimum camera depth observed while building this row.</summary>
            public double DepthMin { get; init; }

            /// <summary>Maximum camera depth observed while building this row.</summary>
            public double DepthMax { get; init; }
        }

        /// <summary>Line vertices and depth span produced while converting a chunk of contour levels.</summary>
        private readonly struct ContourChunkLineBuildResult
        {
            /// <summary>GPU line vertices for this contour chunk.</summary>
            public List<GpuVertex> Vertices { get; init; }

            /// <summary>Minimum camera depth observed while building this chunk.</summary>
            public double DepthMin { get; init; }

            /// <summary>Maximum camera depth observed while building this chunk.</summary>
            public double DepthMax { get; init; }

            /// <summary>Source polyline point count across all levels in this chunk.</summary>
            public int PolylinePointCount { get; init; }
        }

        /// <summary>
        /// Builds coloured mesh vertices in parallel, one worker per sampled grid row.
        /// </summary>
        /// <param name="MeshVertices">Output mesh vertex list; existing contents are preserved.</param>
        /// <param name="DepthMin">Running minimum camera depth updated with each row result.</param>
        /// <param name="DepthMax">Running maximum camera depth updated with each row result.</param>
        /// <param name="Surface">Sampled scalar field defining mesh topology.</param>
        /// <param name="ContentView">Active scalar channel used for elevations and colours.</param>
        /// <param name="LocalProjection">Fast field-relative metres projection for vertex placement.</param>
        /// <param name="Camera">True-scale storage camera used while caching geometry.</param>
        /// <param name="FlatPlane">When true, mesh vertices lie on the cut/fill comparison plane.</param>
        /// <param name="FlatOverlayZLift">Optional Z lift applied to flat overlays above satellite.</param>
        /// <param name="VMin">Minimum scalar value in the active display range.</param>
        /// <param name="VMax">Maximum scalar value in the active display range.</param>
        /// <param name="ApplyHillshade">When true, hillshade modulation is baked into vertex colours.</param>
        /// <param name="Step">Mesh sampling stride across the sample grid.</param>
        /// <param name="RowCount">Number of sampled rows to build.</param>
        /// <param name="Progress">Optional progress reporter for the mesh build segment.</param>
        static private void BuildMeshVerticesParallel
            (
            List<GpuVertex> MeshVertices,
            ref double DepthMin,
            ref double DepthMax,
            FieldSurfaceSampler Surface,
            FieldContentView ContentView,
            FieldLocalMetresProjection LocalProjection,
            FieldCamera3D Camera,
            bool FlatPlane,
            double FlatOverlayZLift,
            double VMin,
            double VMax,
            bool ApplyHillshade,
            int Step,
            int RowCount,
            IProgress<int>? Progress
            )
        {
            MeshRowBuildResult[] RowResults = new MeshRowBuildResult[RowCount];
            int CompletedRows = 0;

            Parallel.For(0, RowCount, RowIndex =>
            {
                int Sy = RowIndex * Step;
                RowResults[RowIndex] = BuildMeshRow(
                    Surface,
                    ContentView,
                    LocalProjection,
                    Camera,
                    FlatPlane,
                    FlatOverlayZLift,
                    VMin,
                    VMax,
                    ApplyHillshade,
                    Sy,
                    Step);

                int Done = Interlocked.Increment(ref CompletedRows);
                ProgressMapper.ReportSegment(Progress, 0, PROGRESS_MESH_END_PERCENT, Done * PROGRESS_COMPLETE_PERCENT / RowCount);
            });

            int TotalVertexCount = 0;
            for (int RowIndex = 0; RowIndex < RowCount; RowIndex++)
                TotalVertexCount += RowResults[RowIndex].Vertices.Count;

            MeshVertices.Capacity = MeshVertices.Count + TotalVertexCount;
            for (int RowIndex = 0; RowIndex < RowCount; RowIndex++)
            {
                MeshRowBuildResult Row = RowResults[RowIndex];
                MeshVertices.AddRange(Row.Vertices);
                MergeDepthRange(ref DepthMin, ref DepthMax, Row.DepthMin, Row.DepthMax);
            }
        }

        /// <summary>
        /// Builds coloured mesh vertices for one sampled row of surface grid quads.
        /// </summary>
        /// <param name="Surface">Sampled scalar field defining mesh topology.</param>
        /// <param name="ContentView">Active scalar channel used for elevations and colours.</param>
        /// <param name="LocalProjection">Fast field-relative metres projection for vertex placement.</param>
        /// <param name="Camera">True-scale storage camera used while caching geometry.</param>
        /// <param name="FlatPlane">When true, mesh vertices lie on the cut/fill comparison plane.</param>
        /// <param name="FlatOverlayZLift">Optional Z lift applied to flat overlays above satellite.</param>
        /// <param name="VMin">Minimum scalar value in the active display range.</param>
        /// <param name="VMax">Maximum scalar value in the active display range.</param>
        /// <param name="ApplyHillshade">When true, hillshade modulation is baked into vertex colours.</param>
        /// <param name="Sy">Starting sample-grid row index for this build slice.</param>
        /// <param name="Step">Mesh sampling stride across the sample grid.</param>
        /// <returns>Vertices and local depth span for the row.</returns>
        static private MeshRowBuildResult BuildMeshRow
            (
            FieldSurfaceSampler Surface,
            FieldContentView ContentView,
            FieldLocalMetresProjection LocalProjection,
            FieldCamera3D Camera,
            bool FlatPlane,
            double FlatOverlayZLift,
            double VMin,
            double VMax,
            bool ApplyHillshade,
            int Sy,
            int Step
            )
        {
            List<GpuVertex> RowVertices = new List<GpuVertex>();
            double RowDepthMin = double.PositiveInfinity;
            double RowDepthMax = double.NegativeInfinity;

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

                double E00 = FlatPlane ? Surface.ResolveMinElevation(ContentView) : Surface.GetScalarAtIndex(ContentView, Idx00);
                double E01 = FlatPlane ? Surface.ResolveMinElevation(ContentView) : Surface.GetScalarAtIndex(ContentView, Idx01);
                double E11 = FlatPlane ? Surface.ResolveMinElevation(ContentView) : Surface.GetScalarAtIndex(ContentView, Idx11);
                double E10 = FlatPlane ? Surface.ResolveMinElevation(ContentView) : Surface.GetScalarAtIndex(ContentView, Idx10);
                if (double.IsNaN(E00) || double.IsNaN(E01) || double.IsNaN(E11) || double.IsNaN(E10))
                    continue;

                AddMeshTriangle(
                    RowVertices,
                    ref RowDepthMin,
                    ref RowDepthMax,
                    LocalProjection,
                    Camera,
                    FlatPlane,
                    FlatOverlayZLift,
                    Surface,
                    ContentView,
                    VMin,
                    VMax,
                    ApplyHillshade,
                    Lat0,
                    Lon0,
                    E00,
                    Idx00,
                    Lat0,
                    Lon1,
                    E10,
                    Idx10,
                    Lat1,
                    Lon1,
                    E11,
                    Idx11);
                AddMeshTriangle(
                    RowVertices,
                    ref RowDepthMin,
                    ref RowDepthMax,
                    LocalProjection,
                    Camera,
                    FlatPlane,
                    FlatOverlayZLift,
                    Surface,
                    ContentView,
                    VMin,
                    VMax,
                    ApplyHillshade,
                    Lat0,
                    Lon0,
                    E00,
                    Idx00,
                    Lat1,
                    Lon1,
                    E11,
                    Idx11,
                    Lat1,
                    Lon0,
                    E01,
                    Idx01);
            }

            return new MeshRowBuildResult
            {
                Vertices = RowVertices,
                DepthMin = RowDepthMin,
                DepthMax = RowDepthMax
            };
        }

        /// <summary>
        /// Converts cached contour polylines into GPU line vertices in parallel, chunking levels per worker.
        /// </summary>
        /// <param name="LineVertices">Output line vertex list; existing contents are preserved.</param>
        /// <param name="DepthMin">Running minimum camera depth updated with each chunk result.</param>
        /// <param name="DepthMax">Running maximum camera depth updated with each chunk result.</param>
        /// <param name="Polylines">Cached contour level and polyline pairs.</param>
        /// <param name="LocalProjection">Fast field-relative metres projection for vertex placement.</param>
        /// <param name="Camera">True-scale storage camera used while caching geometry.</param>
        /// <param name="BandOffset">Colour offset applied below each contour level.</param>
        /// <param name="ZLift">Model-space Z lift applied to contour lines above the mesh.</param>
        /// <param name="VMin">Minimum scalar value in the active display range.</param>
        /// <param name="VMax">Maximum scalar value in the active display range.</param>
        /// <returns>Total source polyline point count across all levels.</returns>
        static private int BuildContourLineVerticesParallel
            (
            List<GpuVertex> LineVertices,
            ref double DepthMin,
            ref double DepthMax,
            IReadOnlyList<(double Level, IReadOnlyList<(double Lat, double Lon)> Points)> Polylines,
            FieldLocalMetresProjection LocalProjection,
            FieldCamera3D Camera,
            double BandOffset,
            double ZLift,
            double VMin,
            double VMax
            )
        {
            int LevelCount = Polylines.Count;
            if (LevelCount == 0)
                return 0;

            int ChunkCount = (LevelCount + CONTOUR_LEVEL_PARALLEL_CHUNK - 1) / CONTOUR_LEVEL_PARALLEL_CHUNK;
            ContourChunkLineBuildResult[] ChunkResults = new ContourChunkLineBuildResult[ChunkCount];
            Parallel.For(0, ChunkCount, ChunkIndex =>
            {
                int StartLevel = ChunkIndex * CONTOUR_LEVEL_PARALLEL_CHUNK;
                int EndLevel = Math.Min(StartLevel + CONTOUR_LEVEL_PARALLEL_CHUNK, LevelCount);
                ChunkResults[ChunkIndex] = BuildContourLevelChunkLineVertices(
                    Polylines,
                    StartLevel,
                    EndLevel,
                    LocalProjection,
                    Camera,
                    BandOffset,
                    ZLift,
                    VMin,
                    VMax);
            });

            int PolylinePointCount = 0;
            int TotalVertexCount = 0;
            for (int ChunkIndex = 0; ChunkIndex < ChunkCount; ChunkIndex++)
            {
                PolylinePointCount += ChunkResults[ChunkIndex].PolylinePointCount;
                TotalVertexCount += ChunkResults[ChunkIndex].Vertices.Count;
            }

            LineVertices.Capacity = LineVertices.Count + TotalVertexCount;
            for (int ChunkIndex = 0; ChunkIndex < ChunkCount; ChunkIndex++)
            {
                ContourChunkLineBuildResult Chunk = ChunkResults[ChunkIndex];
                LineVertices.AddRange(Chunk.Vertices);
                MergeDepthRange(ref DepthMin, ref DepthMax, Chunk.DepthMin, Chunk.DepthMax);
            }

            return PolylinePointCount;
        }

        /// <summary>
        /// Converts a contiguous slice of cached contour levels into GPU line vertices.
        /// </summary>
        /// <param name="Polylines">Cached contour level and polyline pairs.</param>
        /// <param name="StartLevel">Inclusive start level index.</param>
        /// <param name="EndLevel">Exclusive end level index.</param>
        /// <param name="LocalProjection">Fast field-relative metres projection for vertex placement.</param>
        /// <param name="Camera">True-scale storage camera used while caching geometry.</param>
        /// <param name="BandOffset">Colour offset applied below each contour level.</param>
        /// <param name="ZLift">Model-space Z lift applied above the contour level.</param>
        /// <param name="VMin">Minimum scalar value in the active display range.</param>
        /// <param name="VMax">Maximum scalar value in the active display range.</param>
        /// <returns>Line vertices and local depth span for the level chunk.</returns>
        static private ContourChunkLineBuildResult BuildContourLevelChunkLineVertices
            (
            IReadOnlyList<(double Level, IReadOnlyList<(double Lat, double Lon)> Points)> Polylines,
            int StartLevel,
            int EndLevel,
            FieldLocalMetresProjection LocalProjection,
            FieldCamera3D Camera,
            double BandOffset,
            double ZLift,
            double VMin,
            double VMax
            )
        {
            List<GpuVertex> ChunkVertices = new List<GpuVertex>();
            double ChunkDepthMin = double.PositiveInfinity;
            double ChunkDepthMax = double.NegativeInfinity;
            int PolylinePointCount = 0;

            for (int LevelIndex = StartLevel; LevelIndex < EndLevel; LevelIndex++)
            {
                (double Level, IReadOnlyList<(double Lat, double Lon)> Polyline) = Polylines[LevelIndex];
                PolylinePointCount += Polyline.Count;
                if (Polyline.Count < MIN_POLYLINE_POINTS)
                    continue;

                uint Color = NineBandColorScale.GetLightenedColor(Level - BandOffset, VMin, VMax);
                AppendPolyline(
                    ChunkVertices,
                    ref ChunkDepthMin,
                    ref ChunkDepthMax,
                    LocalProjection,
                    Camera,
                    Polyline,
                    Level + ZLift,
                    FlatPlane: false,
                    Color);
            }

            return new ContourChunkLineBuildResult
            {
                Vertices = ChunkVertices,
                DepthMin = ChunkDepthMin,
                DepthMax = ChunkDepthMax,
                PolylinePointCount = PolylinePointCount
            };
        }

        /// <summary>
        /// Expands a running depth range with another local min/max pair.
        /// </summary>
        /// <param name="DepthMin">Running minimum camera depth.</param>
        /// <param name="DepthMax">Running maximum camera depth.</param>
        /// <param name="LocalDepthMin">Candidate minimum depth from a parallel build slice.</param>
        /// <param name="LocalDepthMax">Candidate maximum depth from a parallel build slice.</param>
        static private void MergeDepthRange
            (
            ref double DepthMin,
            ref double DepthMax,
            double LocalDepthMin,
            double LocalDepthMax
            )
        {
            if (LocalDepthMin < DepthMin)
                DepthMin = LocalDepthMin;
            if (LocalDepthMax > DepthMax)
                DepthMax = LocalDepthMax;
        }

        /// <summary>
        /// Appends one coloured mesh triangle from three grid sample locations.
        /// </summary>
        static private void AddMeshTriangle
            (
            List<GpuVertex> MeshVertices,
            ref double DepthMin,
            ref double DepthMax,
            FieldLocalMetresProjection LocalProjection,
            FieldCamera3D Camera,
            bool FlatPlane,
            double FlatOverlayZLift,
            FieldSurfaceSampler Surface,
            FieldContentView ContentView,
            double VMin,
            double VMax,
            bool ApplyHillshade,
            double Lat0,
            double Lon0,
            double E0,
            int Idx0,
            double Lat1,
            double Lon1,
            double E1,
            int Idx1,
            double Lat2,
            double Lon2,
            double E2,
            int Idx2
            )
        {
            MeshVertices.Add(CreateMeshVertex(LocalProjection, Camera, FlatPlane, FlatOverlayZLift, Surface, ContentView, VMin, VMax, ApplyHillshade, Lat0, Lon0, E0, Idx0, ref DepthMin, ref DepthMax));
            MeshVertices.Add(CreateMeshVertex(LocalProjection, Camera, FlatPlane, FlatOverlayZLift, Surface, ContentView, VMin, VMax, ApplyHillshade, Lat1, Lon1, E1, Idx1, ref DepthMin, ref DepthMax));
            MeshVertices.Add(CreateMeshVertex(LocalProjection, Camera, FlatPlane, FlatOverlayZLift, Surface, ContentView, VMin, VMax, ApplyHillshade, Lat2, Lon2, E2, Idx2, ref DepthMin, ref DepthMax));
        }

        /// <summary>
        /// Creates one coloured GPU vertex from a grid sample with hillshade and depth tracking.
        /// </summary>
        static private GpuVertex CreateMeshVertex
            (
            FieldLocalMetresProjection LocalProjection,
            FieldCamera3D Camera,
            bool FlatPlane,
            double FlatOverlayZLift,
            FieldSurfaceSampler Surface,
            FieldContentView ContentView,
            double VMin,
            double VMax,
            bool ApplyHillshade,
            double Lat,
            double Lon,
            double Elevation,
            int GridIndex,
            ref double DepthMin,
            ref double DepthMax
            )
        {
            (double EastM, double NorthM, double ModelZ) = ToFieldModelSpace(LocalProjection, Camera, Lat, Lon, Elevation, FlatPlane);
            if (FlatPlane && FlatOverlayZLift > 0)
                ModelZ += FlatOverlayZLift;
            TrackDepth(Camera, EastM, NorthM, ModelZ, ref DepthMin, ref DepthMax);
            SKColor Color = ColorForVertex(Surface, ContentView, Lat, Lon, Elevation, VMin, VMax, FlatPlane, ApplyHillshade, GridIndex);
            return ToGpuVertex(EastM, NorthM, ModelZ, Color);
        }

        /// <summary>
        /// Converts a contour polyline into paired line-segment GPU vertices.
        /// </summary>
        static private void AppendPolyline
            (
            List<GpuVertex> LineVertices,
            ref double DepthMin,
            ref double DepthMax,
            FieldLocalMetresProjection LocalProjection,
            FieldCamera3D Camera,
            IReadOnlyList<(double Lat, double Lon)> Points,
            double Elevation,
            bool FlatPlane,
            uint ArgbColor
            )
        {
            SKColor Color = FieldRenderer.ArgbToSkia(ArgbColor);
            for (int I = 0; I < Points.Count - 1; I++)
            {
                (double Lat0, double Lon0) = Points[I];
                (double Lat1, double Lon1) = Points[I + 1];
                AppendLineSegment(LineVertices, ref DepthMin, ref DepthMax, LocalProjection, Camera, Lat0, Lon0, Lat1, Lon1, Elevation, FlatPlane, FLAT_OVERLAY_Z_LIFT_NONE, Color);
            }
        }

        /// <summary>
        /// Appends closed field boundary segments as GPU line vertices.
        /// </summary>
        static private void AppendFieldBorder
            (
            List<GpuVertex> LineVertices,
            ref double DepthMin,
            ref double DepthMax,
            FieldLocalMetresProjection LocalProjection,
            FieldCamera3D Camera,
            FieldSurfaceSampler Surface,
            SurveyModel Survey,
            FieldContentView ContentView,
            bool FlatPlane,
            double FlatOverlayZLift
            )
        {
            if (Survey.BoundaryPoints.Count < MIN_BOUNDARY_POINTS)
                return;

            List<(double Lat, double Lon)> Ring = FieldRenderer.BuildOrderedBoundaryLatLon(Survey);
            SKColor Color = SKColors.Black;
            for (int I = 0; I < Ring.Count; I++)
            {
                (double Lat0, double Lon0) = Ring[I];
                (double Lat1, double Lon1) = Ring[(I + 1) % Ring.Count];
                double Elev0 = SampleBorderElevation(Surface, Survey, ContentView, FlatPlane, Lat0, Lon0);
                double Elev1 = SampleBorderElevation(Surface, Survey, ContentView, FlatPlane, Lat1, Lon1);
                AppendLineSegment(LineVertices, ref DepthMin, ref DepthMax, LocalProjection, Camera, Lat0, Lon0, Lat1, Lon1, (Elev0 + Elev1) * BORDER_ELEVATION_AVERAGE_FRACTION, FlatPlane, FlatOverlayZLift, Color);
            }
        }

        /// <summary>
        /// Resolves elevation at a boundary vertex for border line placement.
        /// </summary>
        static private double SampleBorderElevation
            (
            FieldSurfaceSampler Surface,
            SurveyModel Survey,
            FieldContentView ContentView,
            bool FlatPlane,
            double Lat,
            double Lon
            )
        {
            if (FlatPlane)
                return Surface.ResolveMinElevation(ContentView);

            double Elev = Surface.SampleScalar(ContentView, Lat, Lon);
            if (!double.IsNaN(Elev))
                return Elev;

            TopologyPoint? Match = Survey.BoundaryPoints.Find(P =>
                Math.Abs(P.Latitude - Lat) < BOUNDARY_COORDINATE_EPSILON && Math.Abs(P.Longitude - Lon) < BOUNDARY_COORDINATE_EPSILON);
            return Match?.ExistingElevation ?? Surface.ResolveMinElevation(ContentView);
        }

        /// <summary>
        /// Appends one line segment as two GPU vertices with depth tracking.
        /// </summary>
        static private void AppendLineSegment
            (
            List<GpuVertex> LineVertices,
            ref double DepthMin,
            ref double DepthMax,
            FieldLocalMetresProjection LocalProjection,
            FieldCamera3D Camera,
            double Lat0,
            double Lon0,
            double Lat1,
            double Lon1,
            double Elevation,
            bool FlatPlane,
            double FlatOverlayZLift,
            SKColor Color
            )
        {
            (double East0, double North0, double Z0) = ToFieldModelSpace(LocalProjection, Camera, Lat0, Lon0, Elevation, FlatPlane);
            (double East1, double North1, double Z1) = ToFieldModelSpace(LocalProjection, Camera, Lat1, Lon1, Elevation, FlatPlane);
            if (FlatPlane && FlatOverlayZLift > 0)
            {
                Z0 += FlatOverlayZLift;
                Z1 += FlatOverlayZLift;
            }
            TrackDepth(Camera, East0, North0, Z0, ref DepthMin, ref DepthMax);
            TrackDepth(Camera, East1, North1, Z1, ref DepthMin, ref DepthMax);
            LineVertices.Add(ToGpuVertex(East0, North0, Z0, Color));
            LineVertices.Add(ToGpuVertex(East1, North1, Z1, Color));
        }

        /// <summary>
        /// Updates running min/max camera depth for a model-space point.
        /// </summary>
        static private void TrackDepth
            (
            FieldCamera3D Camera,
            double EastM,
            double NorthM,
            double ModelZ,
            ref double DepthMin,
            ref double DepthMax
            )
        {
            double Depth = Camera.ComputeCameraDepth(EastM, NorthM, ModelZ);
            if (Depth < DepthMin)
                DepthMin = Depth;
            if (Depth > DepthMax)
                DepthMax = Depth;
        }

        /// <summary>
        /// Converts model-space coordinates and an Skia colour to a GPU vertex.
        /// </summary>
        static private GpuVertex ToGpuVertex
            (
            double EastM,
            double NorthM,
            double ModelZ,
            SKColor Color
            )
        {
            return new GpuVertex
            {
                EastM = (float)EastM,
                NorthM = (float)NorthM,
                ModelZ = (float)ModelZ,
                R = Color.Red / COLOR_CHANNEL_MAX,
                G = Color.Green / COLOR_CHANNEL_MAX,
                B = Color.Blue / COLOR_CHANNEL_MAX,
                A = Color.Alpha / COLOR_CHANNEL_MAX
            };
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
            SKColor Color = FieldRenderer.ArgbToSkia(NineBandColorScale.GetColor(Value, VMin, VMax));
            if (!ApplyHillshade || FlatPlane)
                return Color;

            double Shade = Surface.GetMeshHillshadeFactor(ContentView, GridIndex);
            return FieldRenderer.ModulateHillshadeColor(Color, Shade);
        }

        /// <summary>
        /// Resolves the fixed field-centre UTM anchor used for orbit-invariant geometry.
        /// </summary>
        static private UTM.UTMCoordinate ResolveFieldCentreUtm
            (
            FieldSurfaceSampler Surface
            )
        {
            double CentreLat = (Surface.MinLat + Surface.MaxLat) * FIELD_CENTRE_FRACTION;
            double CentreLon = (Surface.MinLon + Surface.MaxLon) * FIELD_CENTRE_FRACTION;
            return UTM.FromLatLon(CentreLat, CentreLon);
        }

        /// <summary>
        /// Converts a geographic point to field-centre-relative east/north with camera relief Z.
        /// </summary>
        static private (double EastM, double NorthM, double ModelZ) ToFieldModelSpace
            (
            FieldLocalMetresProjection LocalProjection,
            FieldCamera3D Camera,
            double Lat,
            double Lon,
            double ElevationM,
            bool FlatPlane
            )
        {
            (double EastM, double NorthM) = LocalProjection.LatLonToFieldMetres(Lat, Lon);
            (_, _, double ModelZ) = Camera.ToModelSpace(Lat, Lon, ElevationM, FlatPlane);
            return (EastM, NorthM, ModelZ);
        }

        /// <summary>
        /// Converts a geographic point to field-centre-relative east/north with camera relief Z.
        /// </summary>
        static private (double EastM, double NorthM, double ModelZ) ToFieldModelSpace
            (
            UTM.UTMCoordinate FieldCentreUtm,
            FieldCamera3D Camera,
            double Lat,
            double Lon,
            double ElevationM,
            bool FlatPlane
            )
        {
            UTM.UTMCoordinate Point = UTM.FromLatLon(Lat, Lon);
            double EastM = Point.Easting - FieldCentreUtm.Easting;
            double NorthM = Point.Northing - FieldCentreUtm.Northing;
            (_, _, double ModelZ) = Camera.ToModelSpace(Lat, Lon, ElevationM, FlatPlane);
            return (EastM, NorthM, ModelZ);
        }

        /// <summary>Orbit heading used only while building exaggeration-independent cached geometry.</summary>
        private const double TRUE_SCALE_STORAGE_CAMERA_HEADING_DEG = 0.0;

        /// <summary>Orbit pitch used only while building exaggeration-independent cached geometry.</summary>
        private const double TRUE_SCALE_STORAGE_CAMERA_PITCH_DEG = 0.0;

        /// <summary>
        /// Builds a camera whose relief Z stores true elevation delta above the active view minimum.
        /// </summary>
        /// <param name="Surface">Sampled field surface defining relief extents.</param>
        /// <param name="ContentView">Active scalar view used for relief anchoring.</param>
        /// <returns>Camera with zero exaggeration and no ground-plane clearance baked into stored Z.</returns>
        static private FieldCamera3D CreateTrueScaleReliefStorageCamera
            (
            FieldSurfaceSampler Surface,
            FieldContentView ContentView
            )
        {
            return new FieldCamera3D(
                Surface,
                TRUE_SCALE_STORAGE_CAMERA_HEADING_DEG,
                TRUE_SCALE_STORAGE_CAMERA_PITCH_DEG,
                RenderingConstants.MIN_ORBIT_DISTANCE_M,
                VerticalExaggeration: MIN_VERTICAL_EXAGGERATION,
                ReliefView: ContentView,
                GroundPlaneClearanceM: FLAT_OVERLAY_Z_LIFT_NONE);
        }

        /// <summary>
        /// Applies the active camera's vertical exaggeration to cached true-scale relief vertices.
        /// </summary>
        /// <param name="Source">Cached model-space geometry stored at true-scale relief.</param>
        /// <param name="Camera">Active orbit camera carrying exaggeration and ground clearance.</param>
        /// <param name="FlatPlane">When true, cached Z already represents flat overlay lift only.</param>
        /// <returns>Geometry with relief vertices remapped for the active exaggeration setting.</returns>
        static private GpuFieldStaticGeometry ApplyVerticalExaggerationToStaticGeometry
            (
            GpuFieldStaticGeometry Source,
            FieldCamera3D Camera,
            bool FlatPlane
            )
        {
            if (FlatPlane)
                return Source;

            return new GpuFieldStaticGeometry
            {
                BackgroundMeshVertices = Source.BackgroundMeshVertices,
                TextureMeshes = Source.TextureMeshes,
                CombinedTextureVertices = Source.CombinedTextureVertices,
                TextureDraws = Source.TextureDraws,
                MeshVertices = RemapReliefVertexZ(Source.MeshVertices, Camera),
                LineVertices = RemapReliefVertexZ(Source.LineVertices, Camera),
                BorderLineVertices = RemapReliefVertexZ(Source.BorderLineVertices, Camera),
                ClearR = Source.ClearR,
                ClearG = Source.ClearG,
                ClearB = Source.ClearB,
                ClearA = Source.ClearA
            };
        }

        /// <summary>
        /// Remaps cached true-scale relief Z values through the active exaggeration formula.
        /// </summary>
        /// <param name="Vertices">Vertices whose <see cref="GpuVertex.ModelZ"/> stores true-scale delta.</param>
        /// <param name="Camera">Active orbit camera carrying exaggeration and ground clearance.</param>
        /// <returns>Vertices with display exaggeration applied to relief Z.</returns>
        static private GpuVertex[] RemapReliefVertexZ
            (
            GpuVertex[] Vertices,
            FieldCamera3D Camera
            )
        {
            if (Vertices.Length == 0)
                return Vertices;

            GpuVertex[] Result = new GpuVertex[Vertices.Length];
            for (int Index = 0; Index < Vertices.Length; Index++)
            {
                GpuVertex Vertex = Vertices[Index];
                double TrueScaleDelta = Vertex.ModelZ;
                double ModelZ = FieldCamera3D.ComputeReliefModelZ(
                    Camera.MinElevation + TrueScaleDelta,
                    Camera.MinElevation,
                    Camera.VerticalExaggeration,
                    Camera.ReliefScale) + Camera.GroundPlaneClearanceM;
                Result[Index] = new GpuVertex
                {
                    EastM = Vertex.EastM,
                    NorthM = Vertex.NorthM,
                    ModelZ = (float)ModelZ,
                    R = Vertex.R,
                    G = Vertex.G,
                    B = Vertex.B,
                    A = Vertex.A
                };
            }

            return Result;
        }

        /// <summary>Vertical exaggeration at or below this value stores true 1:1 metre relief delta.</summary>
        private const double MIN_VERTICAL_EXAGGERATION = 0.0;
    }
}
