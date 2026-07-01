using System.Diagnostics;

using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;

using SkiaSharp;

namespace AgGradeDesigner.Core.Tests.TestFixtures
{
    /// <summary>
    /// Profiles proposed → existing field-content switches after AGD import.
    /// </summary>
    static internal class ContentViewSwitchBenchmark
    {
        private const int ViewportWidth = 1280;
        private const int ViewportHeight = 720;

        /// <summary>
        /// Timings for each phase of a content-view switch (milliseconds).
        /// </summary>
        internal sealed class PhaseTimings
        {
            public double ProposedPrewarmMs { get; init; }
            public double ProposedFrameMs { get; init; }
            public double ExistingPrewarmMs { get; init; }
            public double ExistingFrameMs { get; init; }
            public double ExistingSwitchTotalMs { get; init; }
            public int ProgressSamplesBefore10Percent { get; init; }
        }

        /// <summary>
        /// Breaks down existing-view display prewarm into colour-map and contour phases.
        /// </summary>
        static public (double ColorMapMs, double ContourMs) MeasureExistingPrewarmPhases
            (
            FieldSurfaceSampler Surface,
            DisplaySettings Display
            )
        {
            Surface.PrewarmDisplayCaches(FieldContentView.Proposed, Display);
            Surface.ComputeScalarRange(FieldContentView.Existing, out double VMin, out double VMax);

            Stopwatch Watch = Stopwatch.StartNew();
            Surface.GetPlanViewColorMap(FieldContentView.Existing, VMin, VMax, Display.Hillshade);
            double ColorMapMs = Watch.Elapsed.TotalMilliseconds;

            Watch.Restart();
            if (Display.Contours)
                Surface.GetContourPolylinesByLevel(FieldContentView.Existing, VMin, VMax);
            double ContourMs = Watch.Elapsed.TotalMilliseconds;

            return (ColorMapMs, ContourMs);
        }

        /// <summary>
        /// Simulates post-import Proposed render then a cold switch to Existing.
        /// </summary>
        static public PhaseTimings MeasureColdProposedToExistingSwitch
            (
            FieldSurfaceSampler Surface,
            DisplaySettings Display,
            bool WarmProposedFrame = true
            )
        {
            Stopwatch Watch = Stopwatch.StartNew();

            Surface.PrewarmDisplayCaches(FieldContentView.Proposed, Display);
            double ProposedPrewarmMs = Watch.Elapsed.TotalMilliseconds;

            Watch.Restart();
            if (WarmProposedFrame)
                RenderContentViewFrame(Surface, FieldContentView.Proposed, Display);
            double ProposedFrameMs = Watch.Elapsed.TotalMilliseconds;

            List<int> ProgressSamples = new List<int>();
            IProgress<int> SwitchProgress = new Progress<int>(Value => ProgressSamples.Add(Value));

            Watch.Restart();
            IProgress<int> PrewarmProgress = ProgressMapper.Subrange(SwitchProgress, 5, 25);
            Surface.PrewarmDisplayCaches(FieldContentView.Existing, Display, PrewarmProgress);
            double ExistingPrewarmMs = Watch.Elapsed.TotalMilliseconds;

            Watch.Restart();
            IProgress<int> FrameProgress = ProgressMapper.Subrange(SwitchProgress, 25, 99);
            RenderContentViewFrame(Surface, FieldContentView.Existing, Display, FrameProgress);
            double ExistingFrameMs = Watch.Elapsed.TotalMilliseconds;

            return new PhaseTimings
            {
                ProposedPrewarmMs = ProposedPrewarmMs,
                ProposedFrameMs = ProposedFrameMs,
                ExistingPrewarmMs = ExistingPrewarmMs,
                ExistingFrameMs = ExistingFrameMs,
                ExistingSwitchTotalMs = ExistingPrewarmMs + ExistingFrameMs,
                ProgressSamplesBefore10Percent = ProgressSamples.TakeWhile(Value => Value < 10).Count()
            };
        }

        /// <summary>
        /// Simulates switching to Existing when display caches were background-warmed after Proposed.
        /// </summary>
        static public double MeasureWarmDisplayCacheExistingSwitch
            (
            FieldSurfaceSampler Surface,
            DisplaySettings Display
            )
        {
            Surface.PrewarmDisplayCaches(FieldContentView.Proposed, Display);
            Surface.PrewarmDisplayCaches(FieldContentView.Existing, Display);

            return ImportBenchmark.MedianMilliseconds(
                () => RenderContentViewFrame(Surface, FieldContentView.Existing, Display),
                WarmupRuns: 1,
                MeasuredRuns: 3);
        }

        /// <summary>
        /// Renders a 2D content-view frame using the same core path as the viewer prepare step.
        /// </summary>
        static public void RenderContentViewFrame
            (
            FieldSurfaceSampler Surface,
            FieldContentView ContentView,
            DisplaySettings Display,
            IProgress<int>? Progress = null
            )
        {
            Progress?.Report(0);
            using SKBitmap FrameBitmap = new SKBitmap(ViewportWidth, ViewportHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
            using SKCanvas Canvas = new SKCanvas(FrameBitmap);
            SKRect Viewport = SKRect.Create(0, 0, ViewportWidth, ViewportHeight);
            Canvas.Clear(SKColors.White);

            Surface.ComputeScalarRange(ContentView, out double VMin, out double VMax);
            Progress?.Report(15);

            bool ApplyHillshade = Display.Hillshade && ContentView != FieldContentView.CutFill;
            SKBitmap ColorMap = Surface.GetPlanViewColorMap(
                ContentView,
                VMin,
                VMax,
                ApplyHillshade,
                ProgressMapper.Subrange(Progress, 15, 45));

            LatLonToScreenFootprint(Surface, out float X0, out float Y0, out float X1, out float Y1);
            SKRect Dest = SKRect.Create(
                Math.Min(X0, X1),
                Math.Min(Y0, Y1),
                Math.Abs(X1 - X0),
                Math.Abs(Y1 - Y0));
            Canvas.DrawBitmap(ColorMap, Dest, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));
            Progress?.Report(45);

            if (Display.Contours && ContentView != FieldContentView.CutFill)
            {
                IReadOnlyList<(double Level, IReadOnlyList<(double Lat, double Lon)> Points)> Polylines =
                    Surface.GetContourPolylinesByLevel(
                        ContentView,
                        VMin,
                        VMax,
                        ProgressMapper.Subrange(Progress, 45, 85));
                DrawContourPolylines(Canvas, Surface, Polylines);
            }
            else
            {
                ProgressMapper.ReportSegment(Progress, 45, 85, 100);
            }

            Progress?.Report(100);
            Canvas.Flush();
        }

        static private void LatLonToScreenFootprint
            (
            FieldSurfaceSampler Surface,
            out float X0,
            out float Y0,
            out float X1,
            out float Y1
            )
        {
            double CentreLat = (Surface.MinLat + Surface.MaxLat) * 0.5;
            double CentreLon = (Surface.MinLon + Surface.MaxLon) * 0.5;
            double MetresPerPixel = 1.0;
            MapViewport Viewport = new MapViewport { Width = ViewportWidth, Height = ViewportHeight };

            MapProjection.LatLonToScreen(
                Surface.MaxLat, Surface.MinLon, CentreLat, CentreLon, MetresPerPixel, Viewport, out X0, out Y0);
            MapProjection.LatLonToScreen(
                Surface.MinLat, Surface.MaxLon, CentreLat, CentreLon, MetresPerPixel, Viewport, out X1, out Y1);
        }

        static private void DrawContourPolylines
            (
            SKCanvas Canvas,
            FieldSurfaceSampler Surface,
            IReadOnlyList<(double Level, IReadOnlyList<(double Lat, double Lon)> Points)> Polylines
            )
        {
            double CentreLat = (Surface.MinLat + Surface.MaxLat) * 0.5;
            double CentreLon = (Surface.MinLon + Surface.MaxLon) * 0.5;
            double MetresPerPixel = 1.0;
            MapViewport Viewport = new MapViewport { Width = ViewportWidth, Height = ViewportHeight };

            using SKPaint Paint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2f,
                IsAntialias = true,
                Color = SKColors.White
            };

            foreach ((double _, IReadOnlyList<(double Lat, double Lon)> Points) in Polylines)
            {
                if (Points.Count < 2)
                    continue;

                using SKPath Path = new SKPath();
                MapProjection.LatLonToScreen(
                    Points[0].Lat, Points[0].Lon, CentreLat, CentreLon, MetresPerPixel, Viewport, out float X0, out float Y0);
                Path.MoveTo(X0, Y0);
                for (int I = 1; I < Points.Count; I++)
                {
                    MapProjection.LatLonToScreen(
                        Points[I].Lat, Points[I].Lon, CentreLat, CentreLon, MetresPerPixel, Viewport, out float X, out float Y);
                    Path.LineTo(X, Y);
                }

                Canvas.DrawPath(Path, Paint);
            }
        }
    }
}
