using AgGradeDesigner.Core.Color;
using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Core.Rendering.Vulkan;
using AgGradeDesigner.Core.Survey;

using SkiaSharp;

namespace AgGradeDesigner.Rendering
{
    /// <summary>
    /// Builds GPU vertex buffers for the 3D field view using the same topology as the Skia renderer.
    /// </summary>
    static internal partial class Field3DGeometryBuilder
    {
        private const int MESH_STEP_THRESHOLD = 65536;
        private const double CONTOUR_Z_LIFT_FRACTION = 0.0005;
        private const double FLAT_OVERLAY_Z_LIFT_MIN = 0.5;
        private const double FLAT_OVERLAY_Z_LIFT_FRACTION = 0.00015;

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
        static public GpuFieldFrame BuildFrame
            (
            FieldSurfaceSampler Surface,
            ProjectDocument Project,
            FieldContentView ContentView,
            DisplaySettings Display,
            FieldCamera3D Camera,
            SKRect Viewport,
            SatelliteTileService? Satellite = null,
            IProgress<int>? Progress = null
            )
        {
            Progress?.Report(0);
            bool ShowSatellite = Display.Satellite && Satellite != null;
            Field3DGeometryCacheKey CacheKey = new Field3DGeometryCacheKey
            {
                Surface = Surface,
                Project = Project,
                ContentView = ContentView,
                ShowSatellite = ShowSatellite,
                Contours = Display.Contours,
                Hillshade = Display.Hillshade,
                VerticalExaggeration = Display.VerticalExaggeration,
                OrbitCentreLat = Camera.CentreLat,
                OrbitCentreLon = Camera.CentreLon,
                SatelliteCacheGeneration = ShowSatellite ? Satellite!.CacheGeneration : 0
            };

            GpuFieldStaticGeometry StaticGeometry = Field3DGeometryCache.GetOrBuild(
                CacheKey,
                () => BuildStaticGeometry(Surface, Project, ContentView, Display, Camera, Satellite, Progress));

            Progress?.Report(100);
            return AssembleFrame(StaticGeometry, Camera, Viewport, ShowSatellite ? Satellite!.CacheGeneration : 0);
        }

        /// <summary>
        /// Builds model-space geometry that does not depend on orbit heading or pitch.
        /// </summary>
        static private GpuFieldStaticGeometry BuildStaticGeometry
            (
            FieldSurfaceSampler Surface,
            ProjectDocument Project,
            FieldContentView ContentView,
            DisplaySettings Display,
            FieldCamera3D Camera,
            SatelliteTileService? Satellite,
            IProgress<int>? Progress
            )
        {
            Surface.ComputeScalarRange(ContentView, out double VMin, out double VMax);
            bool FlatPlane = ContentView == FieldContentView.CutFill;
            bool ApplyHillshade = Display.Hillshade && !FlatPlane;
            if (ApplyHillshade)
                Surface.GetMeshHillshadeFactor(ContentView, 0);

            List<GpuVertex> BackgroundMesh = new List<GpuVertex>(4096);
            List<GpuTextureMesh> TextureMeshes = new List<GpuTextureMesh>(64);
            List<GpuVertex> MeshVertices = new List<GpuVertex>(Surface.SampleWidth * Surface.SampleHeight * 2);
            List<GpuVertex> LineVertices = new List<GpuVertex>(4096);
            double DepthMin = double.PositiveInfinity;
            double DepthMax = double.NegativeInfinity;

            bool ShowSatellite = Display.Satellite && Satellite != null;
            double FlatOverlayZLift = FlatPlane && ShowSatellite ? ComputeFlatOverlayZLift(Surface) : 0.0;
            if (ShowSatellite)
            {
                AppendSatelliteBackground(
                    BackgroundMesh,
                    TextureMeshes,
                    ref DepthMin,
                    ref DepthMax,
                    Surface,
                    Satellite!,
                    Camera,
                    Surface.MinElevation);
            }

            int Step = Math.Max(Surface.SampleWidth, Surface.SampleHeight) > MESH_STEP_THRESHOLD ? 2 : 1;
            int RowCount = Math.Max(1, (Surface.SampleHeight - Step + Step - 1) / Step);

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

                    AddMeshTriangle(
                        MeshVertices,
                        ref DepthMin,
                        ref DepthMax,
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
                        MeshVertices,
                        ref DepthMin,
                        ref DepthMax,
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

                ProgressMapper.ReportSegment(Progress, 0, 60, ((Sy / Step) + 1) * 100 / RowCount);
            }

            if (Display.Contours && !FlatPlane && VMax > VMin)
            {
                double BandOffset = (VMax - VMin) * 0.002;
                double ZLift = (VMax - VMin) * CONTOUR_Z_LIFT_FRACTION;
                IReadOnlyList<(double Level, IReadOnlyList<(double Lat, double Lon)> Points)> Polylines =
                    Surface.GetContourPolylinesByLevel(ContentView, VMin, VMax, ProgressMapper.Subrange(Progress, 60, 85));

                foreach ((double Level, IReadOnlyList<(double Lat, double Lon)> Polyline) in Polylines)
                {
                    if (Polyline.Count < 2)
                        continue;

                    uint Color = NineBandColorScale.GetLightenedColor(Level - BandOffset, VMin, VMax);
                    AppendPolyline(LineVertices, ref DepthMin, ref DepthMax, Camera, Polyline, Level + ZLift, FlatPlane: false, Color);
                }
            }
            else
            {
                ProgressMapper.ReportSegment(Progress, 60, 85, 100);
            }

            if (Project.Survey != null)
                AppendFieldBorder(LineVertices, ref DepthMin, ref DepthMax, Camera, Surface, Project.Survey, ContentView, FlatPlane, FlatOverlayZLift);

            float ClearR = 1f;
            float ClearG = 1f;
            float ClearB = 1f;
            float ClearA = 1f;
            if (ShowSatellite)
            {
                ClearR = 212f / 255f;
                ClearG = 214f / 255f;
                ClearB = 218f / 255f;
                ClearA = 1f;
            }

            return new GpuFieldStaticGeometry
            {
                BackgroundMeshVertices = BackgroundMesh.ToArray(),
                TextureMeshes = TextureMeshes.ToArray(),
                CombinedTextureVertices = BuildCombinedTextureVertices(TextureMeshes, out GpuTextureDrawSpec[] TextureDraws),
                TextureDraws = TextureDraws,
                MeshVertices = MeshVertices.ToArray(),
                LineVertices = LineVertices.ToArray(),
                ClearR = ClearR,
                ClearG = ClearG,
                ClearB = ClearB,
                ClearA = ClearA
            };
        }

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
        static private GpuFieldFrame AssembleFrame
            (
            GpuFieldStaticGeometry StaticGeometry,
            FieldCamera3D Camera,
            SKRect Viewport,
            int SatelliteCacheGeneration
            )
        {
            (double DepthMin, double DepthMax) = ComputeDepthRange(Camera, StaticGeometry);

            return new GpuFieldFrame
            {
                BackgroundMeshVertices = StaticGeometry.BackgroundMeshVertices,
                TextureMeshes = StaticGeometry.TextureMeshes,
                CombinedTextureVertices = StaticGeometry.CombinedTextureVertices,
                TextureDraws = StaticGeometry.TextureDraws,
                MeshVertices = StaticGeometry.MeshVertices,
                LineVertices = StaticGeometry.LineVertices,
                Camera = Camera.ToGpuUniforms(Viewport, DepthMin, DepthMax),
                SatelliteCacheGeneration = SatelliteCacheGeneration,
                ClearR = StaticGeometry.ClearR,
                ClearG = StaticGeometry.ClearG,
                ClearB = StaticGeometry.ClearB,
                ClearA = StaticGeometry.ClearA
            };
        }

        static private (double DepthMin, double DepthMax) ComputeDepthRange
            (
            FieldCamera3D Camera,
            GpuFieldStaticGeometry StaticGeometry
            )
        {
            double DepthMin = double.PositiveInfinity;
            double DepthMax = double.NegativeInfinity;

            foreach (GpuVertex Vertex in StaticGeometry.BackgroundMeshVertices)
                TrackDepth(Camera, Vertex.EastM, Vertex.NorthM, Vertex.ModelZ, ref DepthMin, ref DepthMax);
            foreach (GpuVertex Vertex in StaticGeometry.MeshVertices)
                TrackDepth(Camera, Vertex.EastM, Vertex.NorthM, Vertex.ModelZ, ref DepthMin, ref DepthMax);
            foreach (GpuVertex Vertex in StaticGeometry.LineVertices)
                TrackDepth(Camera, Vertex.EastM, Vertex.NorthM, Vertex.ModelZ, ref DepthMin, ref DepthMax);
            foreach (GpuTextureMesh TextureMesh in StaticGeometry.TextureMeshes)
            {
                foreach (GpuTexturedVertex Vertex in TextureMesh.Vertices)
                    TrackDepth(Camera, Vertex.EastM, Vertex.NorthM, Vertex.ModelZ, ref DepthMin, ref DepthMax);
            }

            if (double.IsPositiveInfinity(DepthMin) || double.IsNegativeInfinity(DepthMax))
                return (0, 1);

            double Pad = Math.Max((DepthMax - DepthMin) * 0.01, 1e-3);
            return (DepthMin - Pad, DepthMax + Pad);
        }

        static private void AddMeshTriangle
            (
            List<GpuVertex> MeshVertices,
            ref double DepthMin,
            ref double DepthMax,
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
            MeshVertices.Add(CreateMeshVertex(Camera, FlatPlane, FlatOverlayZLift, Surface, ContentView, VMin, VMax, ApplyHillshade, Lat0, Lon0, E0, Idx0, ref DepthMin, ref DepthMax));
            MeshVertices.Add(CreateMeshVertex(Camera, FlatPlane, FlatOverlayZLift, Surface, ContentView, VMin, VMax, ApplyHillshade, Lat1, Lon1, E1, Idx1, ref DepthMin, ref DepthMax));
            MeshVertices.Add(CreateMeshVertex(Camera, FlatPlane, FlatOverlayZLift, Surface, ContentView, VMin, VMax, ApplyHillshade, Lat2, Lon2, E2, Idx2, ref DepthMin, ref DepthMax));
        }

        static private GpuVertex CreateMeshVertex
            (
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
            (double EastM, double NorthM, double ModelZ) = Camera.ToModelSpace(Lat, Lon, Elevation, FlatPlane);
            if (FlatPlane && FlatOverlayZLift > 0)
                ModelZ += FlatOverlayZLift;
            TrackDepth(Camera, EastM, NorthM, ModelZ, ref DepthMin, ref DepthMax);
            SKColor Color = ColorForVertex(Surface, ContentView, Lat, Lon, Elevation, VMin, VMax, FlatPlane, ApplyHillshade, GridIndex);
            return ToGpuVertex(EastM, NorthM, ModelZ, Color);
        }

        static private void AppendPolyline
            (
            List<GpuVertex> LineVertices,
            ref double DepthMin,
            ref double DepthMax,
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
                AppendLineSegment(LineVertices, ref DepthMin, ref DepthMax, Camera, Lat0, Lon0, Lat1, Lon1, Elevation, FlatPlane, 0.0, Color);
            }
        }

        static private void AppendFieldBorder
            (
            List<GpuVertex> LineVertices,
            ref double DepthMin,
            ref double DepthMax,
            FieldCamera3D Camera,
            FieldSurfaceSampler Surface,
            SurveyModel Survey,
            FieldContentView ContentView,
            bool FlatPlane,
            double FlatOverlayZLift
            )
        {
            if (Survey.BoundaryPoints.Count < 3)
                return;

            List<(double Lat, double Lon)> Ring = FieldRenderer.BuildOrderedBoundaryLatLon(Survey);
            SKColor Color = SKColors.Black;
            for (int I = 0; I < Ring.Count; I++)
            {
                (double Lat0, double Lon0) = Ring[I];
                (double Lat1, double Lon1) = Ring[(I + 1) % Ring.Count];
                double Elev0 = SampleBorderElevation(Surface, Survey, ContentView, FlatPlane, Lat0, Lon0);
                double Elev1 = SampleBorderElevation(Surface, Survey, ContentView, FlatPlane, Lat1, Lon1);
                AppendLineSegment(LineVertices, ref DepthMin, ref DepthMax, Camera, Lat0, Lon0, Lat1, Lon1, (Elev0 + Elev1) * 0.5, FlatPlane, FlatOverlayZLift, Color);
            }
        }

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
                return Surface.MinElevation;

            double Elev = Surface.SampleScalar(ContentView, Lat, Lon);
            if (!double.IsNaN(Elev))
                return Elev;

            TopologyPoint? Match = Survey.BoundaryPoints.Find(P =>
                Math.Abs(P.Latitude - Lat) < 1e-8 && Math.Abs(P.Longitude - Lon) < 1e-8);
            return Match?.ExistingElevation ?? Surface.MinElevation;
        }

        static private void AppendLineSegment
            (
            List<GpuVertex> LineVertices,
            ref double DepthMin,
            ref double DepthMax,
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
            (double East0, double North0, double Z0) = Camera.ToModelSpace(Lat0, Lon0, Elevation, FlatPlane);
            (double East1, double North1, double Z1) = Camera.ToModelSpace(Lat1, Lon1, Elevation, FlatPlane);
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
                R = Color.Red / 255f,
                G = Color.Green / 255f,
                B = Color.Blue / 255f,
                A = Color.Alpha / 255f
            };
        }

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
    }
}
