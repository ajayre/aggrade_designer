using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Haul;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Core.Tests.Import;
using AgGradeDesigner.Rendering;

using SkiaSharp;

namespace AgGradeDesigner.Core.Tests.Haul
{
    /// <summary>
    /// Regression tests for 3D haul-flow arrow draping behavior.
    /// </summary>
    public sealed class HaulFlowArrowRenderer3DTests
    {
        /// <summary>Latitude south of The Shop 2 survey coverage.</summary>
        private const double SOUTH_EXTERIOR_LAT = 35.0;

        /// <summary>Longitude inside The Shop 2 survey coverage.</summary>
        private const double INTERIOR_LON = -90.72;

        /// <summary>Viewport width used when rasterizing 3D flow arrows in tests.</summary>
        private const int VIEWPORT_WIDTH = 1200;

        /// <summary>Viewport height used when rasterizing 3D flow arrows in tests.</summary>
        private const int VIEWPORT_HEIGHT = 900;

        /// <summary>Orbit heading used for 3D projection tests.</summary>
        private const double ORBIT_HEADING_DEG = 45.0;

        /// <summary>Orbit pitch used for 3D projection tests.</summary>
        private const double ORBIT_PITCH_DEG = 45.0;

        /// <summary>Orbit distance used for 3D projection tests.</summary>
        private const double ORBIT_DISTANCE_M = 500.0;

        /// <summary>Maximum allowed length of any drawable arrow segment in screen pixels.</summary>
        private const float MAX_SEGMENT_LENGTH_PX = 80f;

        /// <summary>
        /// Verifies exterior lat/lon samples return NaN so 3D arrows skip invalid drape points.
        /// </summary>
        [Fact]
        public void SampleScalar_ReturnsNanSouthOfSurvey_ForExistingView
            (
            )
        {
            ProjectDocument Project = ImportHandoffProjectFactory.CreateTheShop2Project();
            FieldSurfaceSampler Surface = new FieldSurfaceSampler();
            Surface.BuildFromProject(Project);

            double Exterior = Surface.SampleScalar(FieldContentView.Existing, SOUTH_EXTERIOR_LAT, INTERIOR_LON);
            Assert.True(double.IsNaN(Exterior), "Expected exterior southern samples to be NaN for 3D drape rejection.");
        }

        /// <summary>
        /// Verifies 3D arrow segments stay bounded when draped with a shared centre elevation.
        /// </summary>
        [Fact]
        public void Draw3D_GeneratedHaulData_SegmentLengthsStayBounded
            (
            )
        {
            HaulFlowFieldResolver.ClearCache();
            ProjectDocument Project = ImportHandoffProjectFactory.CreateTheShop2Project();
            HaulSettings Settings = new HaulSettings();
            HaulData Data = HaulEngineService.Generate(Project, Settings);
            HaulFlowFieldResolver.ResolvedHaulFlow? Resolved = HaulFlowFieldResolver.TryResolve(Data, Project, Settings)
                ?? throw new InvalidOperationException("Expected resolved haul flow data.");

            FieldSurfaceSampler Surface = new FieldSurfaceSampler();
            Surface.BuildFromProject(Project);
            Assert.True(FieldBounds.TryFromProject(Project, out FieldBounds Bounds));
            MapViewFitResult Fit = MapViewFitter.Fit(Bounds, VIEWPORT_WIDTH, VIEWPORT_HEIGHT);

            FieldCamera3D Camera = new FieldCamera3D(
                Surface,
                ORBIT_HEADING_DEG,
                ORBIT_PITCH_DEG,
                ORBIT_DISTANCE_M,
                DisplaySettings.DEFAULT_VERTICAL_EXAGGERATION,
                Fit.CentreLat,
                Fit.CentreLon);

            float MaxSegment = MeasureMaxDrawableSegmentLengthPx(
                Resolved,
                Surface,
                FieldContentView.Existing,
                Camera,
                new SKRect(0, 0, VIEWPORT_WIDTH, VIEWPORT_HEIGHT),
                Bounds);

            Assert.True(
                MaxSegment <= MAX_SEGMENT_LENGTH_PX,
                $"Expected bounded 3D arrow segments, observed max={MaxSegment:0.##}px.");
        }

        /// <summary>
        /// Verifies a full-screen occluding depth buffer suppresses painted haul-flow arrow strokes.
        /// </summary>
        [Fact]
        public void Draw3D_FullScreenOccluder_SuppressesArrowStrokes
            (
            )
        {
            HaulFlowFieldResolver.ClearCache();
            ProjectDocument Project = ImportHandoffProjectFactory.CreateTheShop2Project();
            HaulSettings Settings = new HaulSettings();
            HaulData Data = HaulEngineService.Generate(Project, Settings);
            HaulFlowFieldResolver.ResolvedHaulFlow? Resolved = HaulFlowFieldResolver.TryResolve(Data, Project, Settings)
                ?? throw new InvalidOperationException("Expected resolved haul flow data.");

            FieldSurfaceSampler Surface = new FieldSurfaceSampler();
            Surface.BuildFromProject(Project);
            Assert.True(FieldBounds.TryFromProject(Project, out FieldBounds Bounds));
            MapViewFitResult Fit = MapViewFitter.Fit(Bounds, VIEWPORT_WIDTH, VIEWPORT_HEIGHT);

            FieldCamera3D Camera = new FieldCamera3D(
                Surface,
                ORBIT_HEADING_DEG,
                ORBIT_PITCH_DEG,
                ORBIT_DISTANCE_M,
                DisplaySettings.DEFAULT_VERTICAL_EXAGGERATION,
                Fit.CentreLat,
                Fit.CentreLon);

            using SKBitmap UnoccludedBitmap = RenderHaulFlowArrows3D(
                Resolved,
                Surface,
                FieldContentView.Existing,
                Camera,
                Bounds,
                MeshDepthBuffer.AlwaysVisible);
            using SKBitmap OccludedBitmap = RenderHaulFlowArrows3D(
                Resolved,
                Surface,
                FieldContentView.Existing,
                Camera,
                Bounds,
                BuildFullScreenOccluder());

            int UnoccludedPixels = CountNearBlackStrokePixels(UnoccludedBitmap);
            int OccludedPixels = CountNearBlackStrokePixels(OccludedBitmap);
            Assert.True(UnoccludedPixels > 0, "Expected visible arrows without occlusion.");
            Assert.True(OccludedPixels < UnoccludedPixels, "Expected fewer arrow pixels behind a full-screen occluder.");
        }

        /// <summary>
        /// Renders haul-flow arrows to a bitmap for pixel-level assertions.
        /// </summary>
        private static SKBitmap RenderHaulFlowArrows3D
            (
            HaulFlowFieldResolver.ResolvedHaulFlow Resolved,
            FieldSurfaceSampler Surface,
            FieldContentView ContentView,
            FieldCamera3D Camera,
            FieldBounds Bounds,
            MeshDepthBuffer TopoDepth
            )
        {
            SKBitmap Bitmap = new SKBitmap(VIEWPORT_WIDTH, VIEWPORT_HEIGHT, SKColorType.Rgba8888, SKAlphaType.Premul);
            using SKCanvas Canvas = new SKCanvas(Bitmap);
            Canvas.Clear(SKColors.White);
            HaulFlowArrowRenderer.Draw3D(
                Canvas,
                new SKRect(0, 0, VIEWPORT_WIDTH, VIEWPORT_HEIGHT),
                Resolved,
                Surface,
                ContentView,
                Camera,
                Bounds,
                TopoDepth);
            return Bitmap;
        }

        /// <summary>
        /// Builds a depth buffer with one screen-filling triangle close to the camera.
        /// </summary>
        private static MeshDepthBuffer BuildFullScreenOccluder()
        {
            const double OccluderDepth = 1000.0;
            SKRect Viewport = SKRect.Create(0, 0, VIEWPORT_WIDTH, VIEWPORT_HEIGHT);
            MeshTriangle Occluder = new MeshTriangle
            {
                P0 = new SKPoint(0, 0),
                P1 = new SKPoint(VIEWPORT_WIDTH, 0),
                P2 = new SKPoint(VIEWPORT_WIDTH * 0.5f, VIEWPORT_HEIGHT),
                C0 = SKColors.White,
                C1 = SKColors.White,
                C2 = SKColors.White,
                D0 = OccluderDepth,
                D1 = OccluderDepth,
                D2 = OccluderDepth,
                Depth = OccluderDepth
            };

            return MeshDepthBuffer.Build(Viewport, new[] { Occluder });
        }

        /// <summary>
        /// Counts near-black opaque pixels produced by hollow arrow outlines.
        /// </summary>
        private static int CountNearBlackStrokePixels(SKBitmap Bitmap)
        {
            const byte NearBlackChannelMax = 40;
            const byte MinArrowAlpha = 200;
            int Count = 0;
            for (int Y = 0; Y < Bitmap.Height; Y++)
            {
                for (int X = 0; X < Bitmap.Width; X++)
                {
                    SKColor Color = Bitmap.GetPixel(X, Y);
                    if (Color.Alpha < MinArrowAlpha)
                        continue;

                    if (Color.Red <= NearBlackChannelMax &&
                        Color.Green <= NearBlackChannelMax &&
                        Color.Blue <= NearBlackChannelMax)
                    {
                        Count++;
                    }
                }
            }

            return Count;
        }

        /// <summary>
        /// Measures the longest drawable 3D arrow segment using centre-elevation draping rules.
        /// </summary>
        private static float MeasureMaxDrawableSegmentLengthPx
            (
            HaulFlowFieldResolver.ResolvedHaulFlow Resolved,
            FieldSurfaceSampler Surface,
            FieldContentView ContentView,
            FieldCamera3D Camera,
            SKRect Viewport,
            FieldBounds ClipBounds
            )
        {
            float MaxLength = 0f;
            HaulBuilder.Grid Grid = Resolved.Grid;
            HaulBuilder.FlowField Flow = Resolved.Flow;

            for (int By = 0; By < Grid.Height; By += Resolved.ArrowStride)
            {
                for (int Bx = 0; Bx < Grid.Width; Bx += Resolved.ArrowStride)
                {
                    if (!HaulFlowArrowGeometry.TryBuildDrawableTriangle(Grid, Flow, Bx, By, out HaulFlowArrowGeometry.ArrowTriangleUtm Triangle))
                        continue;

                    if (!HaulFlowArrowGeometry.TryGetCellCenterLatLon(Grid, Bx, By, out double CenterLat, out double CenterLon))
                        continue;

                    if (!AreAllTriangleVerticesInsideClipForTest(ClipBounds, Grid, Triangle))
                        continue;

                    double CenterElev = Surface.SampleScalar(ContentView, CenterLat, CenterLon);
                    if (double.IsNaN(CenterElev))
                        continue;

                    if (!TryProjectVertex(Triangle.TipE, Triangle.TipN, Grid, CenterElev, Camera, Viewport, out float TipX, out float TipY) ||
                        !TryProjectVertex(Triangle.LeftE, Triangle.LeftN, Grid, CenterElev, Camera, Viewport, out float LeftX, out float LeftY) ||
                        !TryProjectVertex(Triangle.RightE, Triangle.RightN, Grid, CenterElev, Camera, Viewport, out float RightX, out float RightY))
                    {
                        continue;
                    }

                    MaxLength = Math.Max(MaxLength, SegmentLength(TipX, TipY, LeftX, LeftY));
                    MaxLength = Math.Max(MaxLength, SegmentLength(TipX, TipY, RightX, RightY));
                    MaxLength = Math.Max(MaxLength, SegmentLength(LeftX, LeftY, RightX, RightY));
                }
            }

            return MaxLength;
        }

        /// <summary>
        /// Returns true when every triangle vertex lies inside clip bounds for test measurement.
        /// </summary>
        private static bool AreAllTriangleVerticesInsideClipForTest
            (
            FieldBounds Bounds,
            HaulBuilder.Grid Grid,
            HaulFlowArrowGeometry.ArrowTriangleUtm Triangle
            )
        {
            return IsInsideClipForTest(Bounds, Grid, Triangle.TipE, Triangle.TipN) &&
                IsInsideClipForTest(Bounds, Grid, Triangle.LeftE, Triangle.LeftN) &&
                IsInsideClipForTest(Bounds, Grid, Triangle.RightE, Triangle.RightN);
        }

        /// <summary>
        /// Returns true when one haul-grid vertex lies inside clip bounds for test measurement.
        /// </summary>
        private static bool IsInsideClipForTest
            (
            FieldBounds Bounds,
            HaulBuilder.Grid Grid,
            double Easting,
            double Northing
            )
        {
            if (!HaulFlowArrowGeometry.TryUtmToLatLon(Grid, Easting, Northing, out double Lat, out double Lon))
                return false;

            return Lat >= Bounds.MinLat &&
                Lat <= Bounds.MaxLat &&
                Lon >= Bounds.MinLon &&
                Lon <= Bounds.MaxLon;
        }

        /// <summary>
        /// Projects one haul-grid vertex using a shared centre elevation.
        /// </summary>
        private static bool TryProjectVertex
            (
            double Easting,
            double Northing,
            HaulBuilder.Grid Grid,
            double CenterElev,
            FieldCamera3D Camera,
            SKRect Viewport,
            out float ScreenX,
            out float ScreenY
            )
        {
            if (!HaulFlowArrowGeometry.TryUtmToLatLon(Grid, Easting, Northing, out double Lat, out double Lon))
            {
                ScreenX = 0;
                ScreenY = 0;
                return false;
            }

            Camera.Project(Lat, Lon, CenterElev, Viewport, FlatPlane: false, out ScreenX, out ScreenY, out _);
            return true;
        }

        /// <summary>
        /// Returns the Euclidean length of one screen segment.
        /// </summary>
        private static float SegmentLength
            (
            float X0,
            float Y0,
            float X1,
            float Y1
            )
        {
            float Dx = X1 - X0;
            float Dy = Y1 - Y0;
            return MathF.Sqrt((Dx * Dx) + (Dy * Dy));
        }
    }
}
