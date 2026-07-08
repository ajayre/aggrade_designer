using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Haul;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Tests.Import;
using AgGradeDesigner.Rendering;

using SkiaSharp;

namespace AgGradeDesigner.Core.Tests.Haul
{
    /// <summary>
    /// Unit tests for <see cref="HaulFlowArrowRenderer"/>.
    /// </summary>
    public sealed class HaulFlowArrowRendererTests
    {
        /// <summary>Viewport width used when rasterizing flow arrows in tests.</summary>
        private const int VIEWPORT_WIDTH = 1200;

        /// <summary>Viewport height used when rasterizing flow arrows in tests.</summary>
        private const int VIEWPORT_HEIGHT = 900;

        /// <summary>Minimum black stroke pixels expected after drawing flow arrows.</summary>
        private const int MIN_ARROW_STROKE_PIXEL_COUNT = 20;

        /// <summary>Maximum channel value treated as a near-black arrow stroke pixel.</summary>
        private const byte NEAR_BLACK_CHANNEL_MAX = 40;

        /// <summary>Minimum alpha required for a pixel to count as a drawn arrow stroke.</summary>
        private const byte MIN_ARROW_ALPHA = 200;

        /// <summary>Margin below the field bottom edge when scanning for stray arrow pixels.</summary>
        private const float FIELD_BOTTOM_MARGIN_PX = 8f;

        /// <summary>Maximum allowed length of any painted arrow segment in screen pixels.</summary>
        private const float MAX_SEGMENT_LENGTH_PX = 80f;

        /// <summary>
        /// Verifies 2D flow-arrow rendering produces visible hollow-triangle strokes.
        /// </summary>
        [Fact]
        public void Draw2D_GeneratedHaulData_ProducesVisibleArrowStrokes
            (
            )
        {
            HaulFlowFieldResolver.ClearCache();
            ProjectDocument Project = ImportHandoffProjectFactory.CreateTheShop2Project();
            HaulSettings Settings = new HaulSettings();
            HaulData Data = HaulEngineService.Generate(Project, Settings);
            HaulFlowFieldResolver.ResolvedHaulFlow? Resolved = HaulFlowFieldResolver.TryResolve(Data, Project, Settings)
                ?? throw new InvalidOperationException("Expected resolved haul flow data.");

            Assert.True(FieldBounds.TryFromProject(Project, out FieldBounds Bounds));
            MapViewFitResult Fit = MapViewFitter.Fit(Bounds, VIEWPORT_WIDTH, VIEWPORT_HEIGHT);
            double MetresPerPixel = FieldRenderer.EstimateMetresPerPixel(Fit.CentreLat, Fit.ZoomFactor);

            using SKBitmap Bitmap = new SKBitmap(VIEWPORT_WIDTH, VIEWPORT_HEIGHT, SKColorType.Rgba8888, SKAlphaType.Premul);
            using SKCanvas Canvas = new SKCanvas(Bitmap);
            Canvas.Clear(SKColors.White);

            HaulFlowArrowRenderer.Draw2D(
                Canvas,
                new SKRect(0, 0, VIEWPORT_WIDTH, VIEWPORT_HEIGHT),
                Resolved,
                Fit.CentreLat,
                Fit.CentreLon,
                MetresPerPixel,
                Bounds);

            int BlackPixels = CountNearBlackStrokePixels(Bitmap);
            Assert.True(
                BlackPixels >= MIN_ARROW_STROKE_PIXEL_COUNT,
                $"Expected visible flow-arrow strokes, got {BlackPixels} near-black pixels.");
        }

        /// <summary>
        /// Verifies 2D rendering does not paint arrow strokes below the southern field boundary.
        /// </summary>
        [Fact]
        public void Draw2D_GeneratedHaulData_DoesNotPaintBelowSouthernFieldEdge
            (
            )
        {
            HaulFlowFieldResolver.ClearCache();
            ProjectDocument Project = ImportHandoffProjectFactory.CreateTheShop2Project();
            HaulSettings Settings = new HaulSettings();
            HaulData Data = HaulEngineService.Generate(Project, Settings);
            HaulFlowFieldResolver.ResolvedHaulFlow? Resolved = HaulFlowFieldResolver.TryResolve(Data, Project, Settings)
                ?? throw new InvalidOperationException("Expected resolved haul flow data.");

            Assert.True(FieldBounds.TryFromProject(Project, out FieldBounds Bounds));
            MapViewFitResult Fit = MapViewFitter.Fit(Bounds, VIEWPORT_WIDTH, VIEWPORT_HEIGHT);
            double MetresPerPixel = FieldRenderer.EstimateMetresPerPixel(Fit.CentreLat, Fit.ZoomFactor);
            MapViewport MapViewport = new MapViewport { Width = VIEWPORT_WIDTH, Height = VIEWPORT_HEIGHT };

            MapProjection.LatLonToScreen(
                Bounds.MinLat,
                Bounds.MinLon,
                Fit.CentreLat,
                Fit.CentreLon,
                MetresPerPixel,
                MapViewport,
                out _,
                out float SouthWestY);
            MapProjection.LatLonToScreen(
                Bounds.MinLat,
                Bounds.MaxLon,
                Fit.CentreLat,
                Fit.CentreLon,
                MetresPerPixel,
                MapViewport,
                out _,
                out float SouthEastY);
            float FieldBottomY = Math.Max(SouthWestY, SouthEastY);

            using SKBitmap Bitmap = new SKBitmap(VIEWPORT_WIDTH, VIEWPORT_HEIGHT, SKColorType.Rgba8888, SKAlphaType.Premul);
            using SKCanvas Canvas = new SKCanvas(Bitmap);
            Canvas.Clear(SKColors.White);

            HaulFlowArrowRenderer.Draw2D(
                Canvas,
                new SKRect(0, 0, VIEWPORT_WIDTH, VIEWPORT_HEIGHT),
                Resolved,
                Fit.CentreLat,
                Fit.CentreLon,
                MetresPerPixel,
                Bounds);

            int BelowEdgePixels = CountNearBlackStrokePixelsBelowY(Bitmap, FieldBottomY + FIELD_BOTTOM_MARGIN_PX);
            Assert.Equal(
                0,
                BelowEdgePixels);
        }

        /// <summary>
        /// Verifies painted arrow segments stay within a reasonable screen-space length.
        /// </summary>
        [Fact]
        public void Draw2D_GeneratedHaulData_SegmentLengthsStayBounded
            (
            )
        {
            HaulFlowFieldResolver.ClearCache();
            ProjectDocument Project = ImportHandoffProjectFactory.CreateTheShop2Project();
            HaulSettings Settings = new HaulSettings();
            HaulData Data = HaulEngineService.Generate(Project, Settings);
            HaulFlowFieldResolver.ResolvedHaulFlow? Resolved = HaulFlowFieldResolver.TryResolve(Data, Project, Settings)
                ?? throw new InvalidOperationException("Expected resolved haul flow data.");

            Assert.True(FieldBounds.TryFromProject(Project, out FieldBounds Bounds));
            MapViewFitResult Fit = MapViewFitter.Fit(Bounds, VIEWPORT_WIDTH, VIEWPORT_HEIGHT);
            double MetresPerPixel = FieldRenderer.EstimateMetresPerPixel(Fit.CentreLat, Fit.ZoomFactor);
            MapViewport MapViewport = new MapViewport { Width = VIEWPORT_WIDTH, Height = VIEWPORT_HEIGHT };

            float MaxObserved = MeasureMaxDrawableSegmentLengthPx(
                Resolved,
                Fit.CentreLat,
                Fit.CentreLon,
                MetresPerPixel,
                MapViewport,
                Bounds);

            Assert.True(
                MaxObserved <= MAX_SEGMENT_LENGTH_PX,
                $"Expected bounded arrow segments, observed max={MaxObserved:0.##}px.");
        }

        /// <summary>
        /// Verifies drawable southern-edge arrows are removed when any vertex falls outside field bounds.
        /// </summary>
        [Fact]
        public void Draw2D_WithClipBounds_OmitsTrianglesExtendingOutsideField
            (
            )
        {
            HaulFlowFieldResolver.ClearCache();
            ProjectDocument Project = ImportHandoffProjectFactory.CreateTheShop2Project();
            HaulSettings Settings = new HaulSettings();
            HaulData Data = HaulEngineService.Generate(Project, Settings);
            HaulFlowFieldResolver.ResolvedHaulFlow? Resolved = HaulFlowFieldResolver.TryResolve(Data, Project, Settings)
                ?? throw new InvalidOperationException("Expected resolved haul flow data.");

            Assert.True(FieldBounds.TryFromProject(Project, out FieldBounds Bounds));
            MapViewFitResult Fit = MapViewFitter.Fit(Bounds, VIEWPORT_WIDTH, VIEWPORT_HEIGHT);
            double MetresPerPixel = FieldRenderer.EstimateMetresPerPixel(Fit.CentreLat, Fit.ZoomFactor);

            int WithoutClip = CountDrawableTriangles(Resolved, Bounds, ClipVertices: false);
            int WithClip = CountDrawableTriangles(Resolved, Bounds, ClipVertices: true);
            Assert.True(WithoutClip > 0);
            Assert.True(WithClip < WithoutClip, "Expected field-bounds clipping to remove edge triangles.");
        }

        /// <summary>
        /// Verifies flow-arrow rendering is a no-op when the resolved field has no visible arrows.
        /// </summary>
        [Fact]
        public void Draw2D_WithoutVisibleArrows_LeavesCanvasUnchanged
            (
            )
        {
            HaulFlowFieldResolver.ResolvedHaulFlow Empty = new HaulFlowFieldResolver.ResolvedHaulFlow
            {
                Grid = new HaulBuilder.Grid(
                    1.0,
                    1,
                    1,
                    0.0,
                    0.0,
                    15,
                    true,
                    36.0,
                    -90.0,
                    new double[1, 1],
                    new double[1, 1],
                    new bool[1, 1] { { true } },
                    1.0,
                    1.0,
                    50.0,
                    new List<List<(double X, double Y)>>()),
                Flow = new HaulBuilder.FlowField(
                    new double[1, 1],
                    new double[1, 1],
                    new double[1, 1],
                    new double[1, 1],
                    new bool[1, 1] { { false } },
                    "poisson"),
                ArrowStride = HaulFlowFieldResolver.DEFAULT_ARROW_STRIDE,
                HasVisibleArrows = false
            };

            using SKBitmap Bitmap = new SKBitmap(VIEWPORT_WIDTH, VIEWPORT_HEIGHT, SKColorType.Rgba8888, SKAlphaType.Premul);
            using SKCanvas Canvas = new SKCanvas(Bitmap);
            Canvas.Clear(SKColors.White);

            HaulFlowArrowRenderer.Draw2D(
                Canvas,
                new SKRect(0, 0, VIEWPORT_WIDTH, VIEWPORT_HEIGHT),
                Empty,
                36.0,
                -90.0,
                1.0);

            Assert.Equal(0, CountNearBlackStrokePixels(Bitmap));
        }

        /// <summary>
        /// Counts near-black opaque pixels produced by hollow arrow outlines.
        /// </summary>
        private static int CountNearBlackStrokePixels
            (
            SKBitmap Bitmap
            )
        {
            int Count = 0;
            for (int Y = 0; Y < Bitmap.Height; Y++)
            {
                for (int X = 0; X < Bitmap.Width; X++)
                {
                    SKColor Color = Bitmap.GetPixel(X, Y);
                    if (Color.Alpha < MIN_ARROW_ALPHA)
                        continue;

                    if (Color.Red <= NEAR_BLACK_CHANNEL_MAX &&
                        Color.Green <= NEAR_BLACK_CHANNEL_MAX &&
                        Color.Blue <= NEAR_BLACK_CHANNEL_MAX)
                    {
                        Count++;
                    }
                }
            }

            return Count;
        }

        /// <summary>
        /// Counts near-black stroke pixels strictly below a screen-Y threshold.
        /// </summary>
        private static int CountNearBlackStrokePixelsBelowY
            (
            SKBitmap Bitmap,
            float ThresholdY
            )
        {
            int Count = 0;
            int StartY = (int)Math.Ceiling(ThresholdY);
            if (StartY < 0)
                StartY = 0;

            for (int Y = StartY; Y < Bitmap.Height; Y++)
            {
                for (int X = 0; X < Bitmap.Width; X++)
                {
                    SKColor Color = Bitmap.GetPixel(X, Y);
                    if (Color.Alpha < MIN_ARROW_ALPHA)
                        continue;

                    if (Color.Red <= NEAR_BLACK_CHANNEL_MAX &&
                        Color.Green <= NEAR_BLACK_CHANNEL_MAX &&
                        Color.Blue <= NEAR_BLACK_CHANNEL_MAX)
                    {
                        Count++;
                    }
                }
            }

            return Count;
        }

        /// <summary>
        /// Measures the longest drawable arrow segment using the same geometry rules as the renderer.
        /// </summary>
        private static float MeasureMaxDrawableSegmentLengthPx
            (
            HaulFlowFieldResolver.ResolvedHaulFlow Resolved,
            double CentreLat,
            double CentreLon,
            double MetresPerPixel,
            MapViewport MapViewport,
            FieldBounds? ClipBounds
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

                    if (ClipBounds != null && !AreAllTriangleVerticesInsideClipForTest(ClipBounds.Value, Grid, Triangle))
                        continue;

                    if (!TryProjectGridUtm(CentreLat, CentreLon, MetresPerPixel, MapViewport, Grid, Triangle.TipE, Triangle.TipN, out float TipX, out float TipY) ||
                        !TryProjectGridUtm(CentreLat, CentreLon, MetresPerPixel, MapViewport, Grid, Triangle.LeftE, Triangle.LeftN, out float LeftX, out float LeftY) ||
                        !TryProjectGridUtm(CentreLat, CentreLon, MetresPerPixel, MapViewport, Grid, Triangle.RightE, Triangle.RightN, out float RightX, out float RightY))
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
        /// Counts drawable triangles with optional field-bounds clipping.
        /// </summary>
        private static int CountDrawableTriangles
            (
            HaulFlowFieldResolver.ResolvedHaulFlow Resolved,
            FieldBounds Bounds,
            bool ClipVertices
            )
        {
            int Count = 0;
            HaulBuilder.Grid Grid = Resolved.Grid;
            HaulBuilder.FlowField Flow = Resolved.Flow;

            for (int By = 0; By < Grid.Height; By += Resolved.ArrowStride)
            {
                for (int Bx = 0; Bx < Grid.Width; Bx += Resolved.ArrowStride)
                {
                    if (!HaulFlowArrowGeometry.TryBuildDrawableTriangle(Grid, Flow, Bx, By, out HaulFlowArrowGeometry.ArrowTriangleUtm Triangle))
                        continue;

                    if (ClipVertices && !AreAllTriangleVerticesInsideClipForTest(Bounds, Grid, Triangle))
                        continue;

                    Count++;
                }
            }

            return Count;
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
        /// Renders flow arrows to a bitmap for pixel-level assertions.
        /// </summary>
        private static SKBitmap RenderFlowArrows
            (
            HaulFlowFieldResolver.ResolvedHaulFlow Resolved,
            MapViewFitResult Fit,
            double MetresPerPixel,
            FieldBounds? ClipBounds
            )
        {
            SKBitmap Bitmap = new SKBitmap(VIEWPORT_WIDTH, VIEWPORT_HEIGHT, SKColorType.Rgba8888, SKAlphaType.Premul);
            using SKCanvas Canvas = new SKCanvas(Bitmap);
            Canvas.Clear(SKColors.White);
            HaulFlowArrowRenderer.Draw2D(
                Canvas,
                new SKRect(0, 0, VIEWPORT_WIDTH, VIEWPORT_HEIGHT),
                Resolved,
                Fit.CentreLat,
                Fit.CentreLon,
                MetresPerPixel,
                ClipBounds);
            return Bitmap;
        }

        /// <summary>
        /// Projects one haul-grid UTM coordinate into screen pixels for test measurement.
        /// </summary>
        private static bool TryProjectGridUtm
            (
            double CentreLat,
            double CentreLon,
            double MetresPerPixel,
            MapViewport MapViewport,
            HaulBuilder.Grid Grid,
            double Easting,
            double Northing,
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

            return MapProjection.LatLonToScreen(
                Lat,
                Lon,
                CentreLat,
                CentreLon,
                MetresPerPixel,
                MapViewport,
                out ScreenX,
                out ScreenY);
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
