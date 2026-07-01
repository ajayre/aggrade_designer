using System.Globalization;

using AgGradeDesigner.Core.Color;
using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Core.Survey;

using SkiaSharp;

namespace AgGradeDesigner.Rendering
{
    /// <summary>
    /// Skia drawing routines for the field viewer (§2).
    /// </summary>
    static public class FieldRenderer
    {
        private const float BENCHMARK_TRIANGLE_SIZE = 8f;
        private const float FIELD_BORDER_STROKE_WIDTH_2D = 3.0f;
        private const float FIELD_BORDER_STROKE_WIDTH_3D = 2.5f;
        private const double METRES_PER_MILE = 1609.344;
        private const double SATELLITE_PLINTH_HALF_SPAN_M = METRES_PER_MILE;
        private const double SATELLITE_PLINTH_THICKNESS_FRACTION = 0.012;
        private const double SATELLITE_PLINTH_MIN_MODEL_THICKNESS = 1.5;
        private static readonly SKColor MAP_BACKGROUND_COLOR = SKColors.White;
        private static readonly SKColor SATELLITE_GROUND_BACKGROUND_COLOR = new SKColor(212, 214, 218);
        private static readonly SKColor SKY_HORIZON_COLOR = new SKColor(186, 214, 244);
        private static readonly SKColor SKY_ZENITH_COLOR = new SKColor(106, 158, 210);
        private static readonly SKColor PLINTH_EDGE_COLOR = SKColors.Black;
        private static readonly SKColor PLINTH_TOP_FALLBACK_COLOR = new SKColor(118, 132, 72);
        private static readonly SKColor SURVEY_POINT_COLOR = new SKColor(30, 30, 30);
        private static readonly SKColor ANNOTATION_TEXT_COLOR = new SKColor(30, 30, 30);

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
            IProgress<int>? Progress = null
            )
        {
            Progress?.Report(0);
            Canvas.Clear(MAP_BACKGROUND_COLOR);
            Progress?.Report(2);

            // Draw layers back-to-front: basemap, surface, contours, border, overlays, then annotations.
            if (Display.Satellite && Satellite != null)
            {
                DrawSatellite2D(Canvas, Viewport, CentreLat, CentreLon, MetresPerPixel, Satellite);
                Progress?.Report(12);
            }

            if (Project != null && Project.HasSurvey && Surface.SampleWidth > 0)
            {
                Surface.ComputeScalarRange(ContentView, out double VMin, out double VMax);
                Progress?.Report(15);
                DrawSurface2D(Canvas, Viewport, Surface, ContentView, Display, CentreLat, CentreLon, MetresPerPixel, VMin, VMax, ProgressMapper.Subrange(Progress, 15, 45));

                if (Display.Contours)
                    DrawContours2D(Canvas, Viewport, Surface, ContentView, CentreLat, CentreLon, MetresPerPixel, VMin, VMax, ProgressMapper.Subrange(Progress, 45, 85));
                else
                    ProgressMapper.ReportSegment(Progress, 45, 85, 100);

                if (Project.Survey != null)
                    DrawFieldBorder2D(Canvas, Viewport, Project.Survey, CentreLat, CentreLon, MetresPerPixel);

                Progress?.Report(88);

                if (Display.SurveyPoints && ContentView != FieldContentView.Proposed && Project.Survey != null)
                    DrawSurveyPoints2D(Canvas, Viewport, Project.Survey, CentreLat, CentreLon, MetresPerPixel);

                if (Display.Benchmarks && Project.Survey != null)
                    DrawBenchmarks2D(Canvas, Viewport, Project.Survey, CentreLat, CentreLon, MetresPerPixel);
            }

            Progress?.Report(100);
            ViewCompassRenderer.Draw(Canvas, Viewport, ViewMode.TwoD);
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
            IProgress<int>? Progress = null
            )
        {
            Progress?.Report(0);

            if (Project == null || !Project.HasSurvey || Surface.SampleWidth == 0)
            {
                Canvas.Clear(MAP_BACKGROUND_COLOR);
                Progress?.Report(100);
                return;
            }

            // Resolve colour range once, then paint mesh, contours, border, and point overlays in depth order.
            Surface.ComputeScalarRange(ContentView, out double VMin, out double VMax);
            double MinExisting = Surface.MinElevation;
            bool FlatPlane = ContentView == FieldContentView.CutFill;
            Progress?.Report(2);

            FieldCamera3D MeshCamera = new FieldCamera3D(
                Surface, HeadingDeg, PitchDeg, DistanceM, VerticalExaggeration, OrbitCentreLat, OrbitCentreLon, ContentView);

            bool ShowSatellite = Display.Satellite && Satellite != null;
            Canvas.Clear(ShowSatellite ? SATELLITE_GROUND_BACKGROUND_COLOR : MAP_BACKGROUND_COLOR);
            if (ShowSatellite)
            {
                double FieldCentreLat = (Surface.MinLat + Surface.MaxLat) * 0.5;
                double FieldCentreLon = (Surface.MinLon + Surface.MaxLon) * 0.5;
                DrawSky3D(Canvas, Viewport, MeshCamera, MinExisting, FieldCentreLat, FieldCentreLon);
            }

            Progress?.Report(5);

            if (ShowSatellite)
            {
                DrawSatellite3DPlane(Canvas, Viewport, Surface, MinExisting, Satellite!, MeshCamera);
                Progress?.Report(15);
            }

            if (FlatPlane)
                DrawCutFillPlane3D(Canvas, Viewport, Surface, VMin, VMax, MeshCamera, ProgressMapper.Subrange(Progress, 15, 75));
            else
                DrawMesh3D(Canvas, Viewport, Surface, ContentView, VMin, VMax, MeshCamera, Display.Hillshade, FlatPlane: false, ProgressMapper.Subrange(Progress, 15, 75));

            if (Display.Contours && !FlatPlane)
                DrawContours3D(Canvas, Viewport, Surface, ContentView, MeshCamera, VMin, VMax, ProgressMapper.Subrange(Progress, 75, 90));
            else
                ProgressMapper.ReportSegment(Progress, 75, 90, 100);

            if (Project.Survey != null)
                DrawFieldBorder3D(Canvas, Viewport, Project.Survey, Surface, ContentView, MeshCamera, FlatPlane);

            Progress?.Report(92);

            if (Display.SurveyPoints && ContentView != FieldContentView.Proposed && Project.Survey != null)
                DrawSurveyPoints3D(Canvas, Viewport, Project.Survey, Surface, MeshCamera);

            if (Display.Benchmarks && Project.Survey != null)
                DrawBenchmarks3D(Canvas, Viewport, Project.Survey, Surface, ContentView, MeshCamera);

            Progress?.Report(100);
            ViewCompassRenderer.Draw(Canvas, Viewport, ViewMode.ThreeD, MeshCamera);
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
            // Stretch the precomputed colour map bitmap to the field's screen footprint.
            bool ApplyHillshade = Display.Hillshade && ContentView != FieldContentView.CutFill;
            SKBitmap ColorMap = Surface.GetPlanViewColorMap(ContentView, VMin, VMax, ApplyHillshade, Progress);

            LatLonToScreen(Surface.MaxLat, Surface.MinLon, CentreLat, CentreLon, MetresPerPixel, Viewport, out float X0, out float Y0);
            LatLonToScreen(Surface.MinLat, Surface.MaxLon, CentreLat, CentreLon, MetresPerPixel, Viewport, out float X1, out float Y1);

            SKRect Dest = SKRect.Create(
                Math.Min(X0, X1),
                Math.Min(Y0, Y1),
                Math.Abs(X1 - X0),
                Math.Abs(Y1 - Y0));

            using SKPaint Paint = new SKPaint { IsAntialias = true };
            Canvas.DrawBitmap(ColorMap, Dest, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None), Paint);
            Progress?.Report(100);
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
                Canvas.DrawCircle(X, Y, 2f, Paint);
            }
        }

        /// <summary>
        /// Draws benchmark triangles and labels in 2D.
        /// </summary>
        static private void DrawBenchmarks2D
            (
            SKCanvas Canvas,
            SKRect Viewport,
            SurveyModel Survey,
            double CentreLat,
            double CentreLon,
            double MetresPerPixel
            )
        {
            using SKPaint Fill = new SKPaint { Color = new SKColor(212, 160, 23), IsAntialias = true, Style = SKPaintStyle.Fill };
            using SKFont LabelFont = new SKFont { Size = 11f };
            using SKPaint LabelPaint = new SKPaint { Color = ANNOTATION_TEXT_COLOR, IsAntialias = true };

            // Draw gold triangles with offset labels for each benchmark.
            foreach (Benchmark Bm in Survey.Benchmarks)
            {
                LatLonToScreen(Bm.Location.Latitude, Bm.Location.Longitude, CentreLat, CentreLon, MetresPerPixel, Viewport, out float X, out float Y);
                using SKPath Tri = new SKPath();
                Tri.MoveTo(X, Y - BENCHMARK_TRIANGLE_SIZE);
                Tri.LineTo(X - BENCHMARK_TRIANGLE_SIZE * 0.866f, Y + BENCHMARK_TRIANGLE_SIZE * 0.5f);
                Tri.LineTo(X + BENCHMARK_TRIANGLE_SIZE * 0.866f, Y + BENCHMARK_TRIANGLE_SIZE * 0.5f);
                Tri.Close();
                Canvas.DrawPath(Tri, Fill);
                Canvas.DrawText(Bm.Name, X + 6, Y - 4, SKTextAlign.Left, LabelFont, LabelPaint);
            }
        }

        /// <summary>
        /// Outlines the field perimeter with a solid black stroke in 2D (OptiSurface-style).
        /// </summary>
        static private void DrawFieldBorder2D
            (
            SKCanvas Canvas,
            SKRect Viewport,
            SurveyModel Survey,
            double CentreLat,
            double CentreLon,
            double MetresPerPixel
            )
        {
            if (Survey.BoundaryPoints.Count < 3)
                return;

            List<(double Lat, double Lon)> Ring = BuildOrderedBoundaryLatLon(Survey);

            using SKPaint Paint = new SKPaint
            {
                Color = SKColors.Black,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = FIELD_BORDER_STROKE_WIDTH_2D,
                IsAntialias = true,
                StrokeJoin = SKStrokeJoin.Miter,
                StrokeCap = SKStrokeCap.Butt
            };

            // Trace the survey boundary ring and close it so the outline matches the clipped field edge.
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
            bool FlatPlane
            )
        {
            if (Survey.BoundaryPoints.Count < 3)
                return;

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

            // Project each boundary vertex at surface elevation so the outline hugs the mesh edge.
            using SKPath Path = new SKPath();
            bool Started = false;
            foreach ((double Lat, double Lon) in Ring)
            {
                double Elev = FlatPlane
                    ? Surface.MinElevation
                    : Surface.SampleScalar(ContentView, Lat, Lon);
                if (double.IsNaN(Elev))
                {
                    TopologyPoint? Match = Survey.BoundaryPoints.Find(P =>
                        Math.Abs(P.Latitude - Lat) < 1e-8 && Math.Abs(P.Longitude - Lon) < 1e-8);
                    Elev = Match?.ExistingElevation ?? Surface.MinElevation;
                }

                Camera.Project(Lat, Lon, Elev, Viewport, FlatPlane, out float X, out float Y, out _);
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
        /// Builds a perimeter-ordered lat/lon ring from survey boundary points.
        /// </summary>
        static private List<(double Lat, double Lon)> BuildOrderedBoundaryLatLon
            (
            SurveyModel Survey
            )
        {
            List<(double Lat, double Lon)> Unordered = new List<(double Lat, double Lon)>(Survey.BoundaryPoints.Count);
            foreach (TopologyPoint Pt in Survey.BoundaryPoints)
                Unordered.Add((Pt.Latitude, Pt.Longitude));

            return BoundaryMask.OrderBoundaryLatLon(Unordered);
        }

        private const float CONTOUR_STROKE_WIDTH_2D = 2.0f;
        private const float CONTOUR_STROKE_WIDTH_3D = 1.75f;
        private const double CONTOUR_Z_LIFT_FRACTION = 0.0005;
        private const int MESH_STEP_THRESHOLD = 128;

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
                Progress?.Report(100);
                return;
            }

            // Offset contour colour slightly below the level so lines read above the fill.
            double BandOffset = (VMax - VMin) * 0.002;
            IReadOnlyList<(double Level, IReadOnlyList<(double Lat, double Lon)> Points)> Polylines =
                Surface.GetContourPolylinesByLevel(ContentView, VMin, VMax, ProgressMapper.Subrange(Progress, 0, 80));

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
                if (Polyline.Count < 2)
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

            Progress?.Report(100);
        }

        /// <summary>
        /// Draws the satellite basemap aligned to the 2D camera.
        /// </summary>
        static private void DrawSatellite2D
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
            SatelliteMapRenderer.DrawToCanvas(
                Canvas,
                CentreLat,
                CentreLon,
                Viewport.Width,
                Viewport.Height,
                MetresPerPixel,
                Satellite);
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
            double FieldCentreLat = (Surface.MinLat + Surface.MaxLat) * 0.5;
            double FieldCentreLon = (Surface.MinLon + Surface.MaxLon) * 0.5;
            double HalfSideM = SATELLITE_PLINTH_HALF_SPAN_M;
            double PlinthDiagonalM = HalfSideM * 2.0 * Math.Sqrt(2.0);
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

            ProjectPlinthCornerModel(Camera, Viewport, FieldCentreUtm, CornerOffsets[0], 0.0,
                out float SwTx, out float SwTy, out double SwTd);
            ProjectPlinthCornerModel(Camera, Viewport, FieldCentreUtm, CornerOffsets[1], 0.0,
                out float SeTx, out float SeTy, out double SeTd);
            ProjectPlinthCornerModel(Camera, Viewport, FieldCentreUtm, CornerOffsets[2], 0.0,
                out float NeTx, out float NeTy, out double NeTd);
            ProjectPlinthCornerModel(Camera, Viewport, FieldCentreUtm, CornerOffsets[3], 0.0,
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
            double MetresPerPixel = Camera.EstimateGroundMetresPerScreenPixel(Viewport) * 1.35;
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

                double CentreLat = (TileMinLat + TileMaxLat) * 0.5;
                double CentreLon = (TileMinLon + TileMaxLon) * 0.5;
                Camera.Project(CentreLat, CentreLon, PlaneElevation, Viewport, FlatPlane: true, out _, out _, out double Depth);
                TileDraws.Add((Depth, Tcx, Ty));
            }

            TileDraws.Sort((A, B) => B.Depth.CompareTo(A.Depth));

            List<SKBitmap> LoadedBitmaps = new List<SKBitmap>();
            Dictionary<(int Tx, int Ty), SKBitmap> DecodedTiles = new Dictionary<(int Tx, int Ty), SKBitmap>();
            foreach ((double _, int Tcx, int Ty) in TileDraws)
            {
                byte[]? Data = Satellite.GetTile(Zoom, Tcx, Ty);
                if (Data == null)
                    continue;

                SKBitmap? Bitmap = SKBitmap.Decode(Data);
                if (Bitmap == null)
                    continue;

                DecodedTiles[(Tcx, Ty)] = Bitmap;
                LoadedBitmaps.Add(Bitmap);
            }

            try
            {
                Canvas.Save();
                Canvas.ClipPath(PlinthTopPath, SKClipOperation.Intersect, true);

                foreach ((double _, int Tcx, int Ty) in TileDraws)
                {
                    if (!DecodedTiles.TryGetValue((Tcx, Ty), out SKBitmap? Bitmap))
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
            finally
            {
                foreach (SKBitmap Bitmap in LoadedBitmaps)
                    Bitmap.Dispose();
            }
        }

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

            double MaxArea = Viewport.Width * Viewport.Height * 2.0;
            return (Width * Height) <= MaxArea;
        }

        /// <summary>
        /// Sortable plinth side face in screen space.
        /// </summary>
        private readonly struct PlinthFace
        {
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

            public float P0x { get; }
            public float P0y { get; }
            public float P1x { get; }
            public float P1y { get; }
            public float P2x { get; }
            public float P2y { get; }
            public float P3x { get; }
            public float P3y { get; }
            public double Depth { get; }
            public double NormalEast { get; }
            public double NormalNorth { get; }
            public double NormalUp { get; }
        }

        static private double AverageDepth
            (
            double D0,
            double D1,
            double D2,
            double D3
            )
        {
            return (D0 + D1 + D2 + D3) * 0.25;
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
            if (HorizonY <= Viewport.Top + 1f)
                return;

            float SkyBottom = Math.Min(HorizonY, Viewport.Bottom);
            SKRect SkyRect = SKRect.Create(Viewport.Left, Viewport.Top, Viewport.Width, SkyBottom - Viewport.Top);
            if (SkyRect.Height <= 1f)
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
            if (Area < 1.0)
                return ((MaxX - MinX) >= 1f) || ((MaxY - MinY) >= 1f);

            return true;
        }

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
        private const int TILE_SUBDIVISIONS = 4;

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
            int N = TILE_SUBDIVISIONS;
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
            SKPoint[] Positions  = new SKPoint[4];
            SKPoint[] TexCoords  = new SKPoint[4];
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
        static private void DrawCutFillPlane3D
            (
            SKCanvas Canvas,
            SKRect Viewport,
            FieldSurfaceSampler Surface,
            double VMin,
            double VMax,
            FieldCamera3D Camera,
            IProgress<int>? Progress = null
            )
        {
            // Reuse the mesh path with a flat plane so cut/fill reads as a horizontal sheet.
            DrawMesh3D(Canvas, Viewport, Surface, FieldContentView.CutFill, VMin, VMax, Camera, ApplyHillshade: false, FlatPlane: true, Progress);
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

            /// <summary>Camera-space depth used for painter sorting.</summary>
            public required double Depth { get; init; }
        }

        /// <summary>
        /// Tessellates the scalar surface into depth-sorted coloured triangles.
        /// </summary>
        static private void DrawMesh3D
            (
            SKCanvas Canvas,
            SKRect Viewport,
            FieldSurfaceSampler Surface,
            FieldContentView ContentView,
            double VMin,
            double VMax,
            FieldCamera3D Camera,
            bool ApplyHillshade,
            bool FlatPlane = false,
            IProgress<int>? Progress = null
            )
        {
            Progress?.Report(0);
            int Step = (Math.Max(Surface.SampleWidth, Surface.SampleHeight) > MESH_STEP_THRESHOLD) ? 2 : 1;
            if (ApplyHillshade)
                Surface.GetMeshHillshadeFactor(ContentView, 0);
            List<MeshTriangle> Triangles = new List<MeshTriangle>();
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

                    double E00 = FlatPlane ? Surface.MinElevation : Surface.SampleScalar(ContentView, Lat0, Lon0);
                    double E01 = FlatPlane ? Surface.MinElevation : Surface.SampleScalar(ContentView, Lat0, Lon1);
                    double E11 = FlatPlane ? Surface.MinElevation : Surface.SampleScalar(ContentView, Lat1, Lon1);
                    double E10 = FlatPlane ? Surface.MinElevation : Surface.SampleScalar(ContentView, Lat1, Lon0);
                    if (double.IsNaN(E00) || double.IsNaN(E01) || double.IsNaN(E11) || double.IsNaN(E10))
                        continue;

                    Camera.Project(Lat0, Lon0, E00, Viewport, FlatPlane, out float P0x, out float P0y, out double D0);
                    Camera.Project(Lat0, Lon1, E01, Viewport, FlatPlane, out float P1x, out float P1y, out double D1);
                    Camera.Project(Lat1, Lon1, E11, Viewport, FlatPlane, out float P2x, out float P2y, out double D2);
                    Camera.Project(Lat1, Lon0, E10, Viewport, FlatPlane, out float P3x, out float P3y, out double D3);

                    SKColor C0 = ColorForVertex(Surface, ContentView, Lat0, Lon0, E00, VMin, VMax, FlatPlane, ApplyHillshade, Idx00);
                    SKColor C1 = ColorForVertex(Surface, ContentView, Lat0, Lon1, E01, VMin, VMax, FlatPlane, ApplyHillshade, Idx01);
                    SKColor C2 = ColorForVertex(Surface, ContentView, Lat1, Lon1, E11, VMin, VMax, FlatPlane, ApplyHillshade, Idx11);
                    SKColor C3 = ColorForVertex(Surface, ContentView, Lat1, Lon0, E10, VMin, VMax, FlatPlane, ApplyHillshade, Idx10);

                    Triangles.Add(new MeshTriangle
                    {
                        P0 = new SKPoint(P0x, P0y),
                        P1 = new SKPoint(P1x, P1y),
                        P2 = new SKPoint(P2x, P2y),
                        C0 = C0,
                        C1 = C1,
                        C2 = C2,
                        Depth = (D0 + D1 + D2) * (1.0 / 3.0)
                    });

                    Triangles.Add(new MeshTriangle
                    {
                        P0 = new SKPoint(P0x, P0y),
                        P1 = new SKPoint(P2x, P2y),
                        P2 = new SKPoint(P3x, P3y),
                        C0 = C0,
                        C1 = C2,
                        C2 = C3,
                        Depth = (D0 + D2 + D3) * (1.0 / 3.0)
                    });
                }

                ProgressMapper.ReportSegment(Progress, 0, 70, ((Sy / Step) + 1) * 100 / RowCount);
            }

            Triangles.Sort((A, B) => A.Depth.CompareTo(B.Depth));
            Progress?.Report(80);

            SKPoint[] Positions = new SKPoint[Triangles.Count * 3];
            SKColor[] Colors = new SKColor[Triangles.Count * 3];
            for (int I = 0; I < Triangles.Count; I++)
            {
                MeshTriangle Triangle = Triangles[I];
                int Base = I * 3;
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
                Color = SKColors.White
            };
            if (Positions.Length > 0)
                Canvas.DrawVertices(SKVertexMode.Triangles, Positions, null, Colors, Paint);
            Progress?.Report(100);
        }
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
        static private SKColor ModulateHillshadeColor
            (
            SKColor Base,
            double Shade
            )
        {
            if (double.IsNaN(Shade))
                return Base;

            // Blend hillshade factor toward mid-grey so flat areas stay readable.
            double Mix = 0.32 + 0.68 * Shade;
            return new SKColor(
                (byte)Math.Clamp(Base.Red * Mix, 0, 255),
                (byte)Math.Clamp(Base.Green * Mix, 0, 255),
                (byte)Math.Clamp(Base.Blue * Mix, 0, 255),
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
            double VMin,
            double VMax,
            IProgress<int>? Progress = null
            )
        {
            if (VMax <= VMin)
            {
                Progress?.Report(100);
                return;
            }

            // Match 2D contour colouring with a slight band offset for legibility on the mesh.
            double BandOffset = (VMax - VMin) * 0.002;
            double ZLift = (VMax - VMin) * CONTOUR_Z_LIFT_FRACTION;
            IReadOnlyList<(double Level, IReadOnlyList<(double Lat, double Lon)> Points)> Polylines =
                Surface.GetContourPolylinesByLevel(ContentView, VMin, VMax, ProgressMapper.Subrange(Progress, 0, 80));

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
                if (Polyline.Count < 2)
                    continue;

                Paint.Color = ArgbToSkia(NineBandColorScale.GetLightenedColor(Level - BandOffset, VMin, VMax));

                using SKPath Path = new SKPath();
                // Drape each contour at its extracted elevation (not re-sampled) with a tiny lift to avoid z-fighting.
                for (int I = 0; I < Polyline.Count; I++)
                {
                    (double Lat, double Lon) = Polyline[I];
                    double Elev = Level + ZLift;
                    Camera.Project(Lat, Lon, Elev, Viewport, FlatPlane: false, out float X, out float Y, out _);
                    if (I == 0)
                        Path.MoveTo(X, Y);
                    else
                        Path.LineTo(X, Y);
                }

                Canvas.DrawPath(Path, Paint);
            }

            Progress?.Report(100);
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
                Canvas.DrawCircle(X, Y, 2f, Paint);
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
            using SKPaint Fill = new SKPaint { Color = new SKColor(212, 160, 23), IsAntialias = true, Style = SKPaintStyle.Fill };
            using SKFont LabelFont = new SKFont { Size = 11f };
            using SKPaint LabelPaint = new SKPaint { Color = ANNOTATION_TEXT_COLOR, IsAntialias = true };

            // Drape benchmark markers on the active topo surface so they stay on the mesh under vertical exaggeration.
            foreach (Benchmark Bm in Survey.Benchmarks)
            {
                double Elev = ContentView == FieldContentView.CutFill
                    ? Surface.MinElevation
                    : Surface.SampleScalar(ContentView, Bm.Location.Latitude, Bm.Location.Longitude);
                if (double.IsNaN(Elev))
                    Elev = Bm.Elevation;
                Camera.Project(Bm.Location.Latitude, Bm.Location.Longitude, Elev, Viewport, FlatPlane: false, out float X, out float Y, out _);
                using SKPath Tri = new SKPath();
                Tri.MoveTo(X, Y - BENCHMARK_TRIANGLE_SIZE);
                Tri.LineTo(X - BENCHMARK_TRIANGLE_SIZE * 0.866f, Y + BENCHMARK_TRIANGLE_SIZE * 0.5f);
                Tri.LineTo(X + BENCHMARK_TRIANGLE_SIZE * 0.866f, Y + BENCHMARK_TRIANGLE_SIZE * 0.5f);
                Tri.Close();
                Canvas.DrawPath(Tri, Fill);
                Canvas.DrawText(Bm.Name, X + 6, Y - 4, SKTextAlign.Left, LabelFont, LabelPaint);
            }
        }

        /// <summary>
        /// Converts ARGB uint to Skia colour with byte channel order adjusted.
        /// </summary>
        static private SKColor ArgbToSkia
            (
            uint Argb
            )
        {
            // Reorder ARGB bytes into Skia's RGBA channel layout.
            byte A = (byte)((Argb >> 24) & 0xFF);
            byte R = (byte)((Argb >> 16) & 0xFF);
            byte G = (byte)((Argb >> 8) & 0xFF);
            byte B = (byte)(Argb & 0xFF);
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
            return (Lon + 180.0) / 360.0 * WorldSize;
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
            double SinLat = Math.Sin(Lat * Math.PI / 180.0);
            double Y = 0.5 - Math.Log((1.0 + SinLat) / (1.0 - SinLat)) / (4.0 * Math.PI);
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
            return X / WorldSize * 360.0 - 180.0;
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
            double N = Math.PI - 2.0 * Math.PI * Y / WorldSize;
            return 180.0 / Math.PI * Math.Atan(0.5 * (Math.Exp(N) - Math.Exp(-N)));
        }
    }
}
