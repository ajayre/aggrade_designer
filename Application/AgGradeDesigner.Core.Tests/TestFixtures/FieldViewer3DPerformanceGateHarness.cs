using System.Diagnostics;

using AgGradeDesigner.Controls;
using AgGradeDesigner.Core.Haul;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Core.Tests.Import;
using AgGradeDesigner.Rendering;

namespace AgGradeDesigner.Core.Tests.TestFixtures
{
    /// <summary>
    /// Metrics collected while driving orbit frames through <see cref="FieldViewerControl"/>.
    /// </summary>
    internal sealed class FieldViewer3DOrbitMetrics
    {
        /// <summary>Median frame build time in milliseconds across measured orbit steps.</summary>
        internal double MedianFrameMs { get; init; }

        /// <summary>Maximum frame build time in milliseconds across measured orbit steps.</summary>
        internal double MaxFrameMs { get; init; }

        /// <summary>Total wall time in milliseconds for all measured orbit steps.</summary>
        internal double TotalMs { get; init; }

        /// <summary>Geometry cache rebuild count observed during measured orbit steps.</summary>
        internal int GeometryCacheBuildCount { get; init; }

        /// <summary>Haul 3D overlay stamp count observed during measured orbit steps.</summary>
        internal int HaulOverlayStampCount { get; init; }

        /// <summary>Committed haul 3D shared-surface reuse count during measured orbit steps.</summary>
        internal int HaulCommittedSurfaceReuseCount { get; init; }

        /// <summary>Number of orbit steps included in the timing sample.</summary>
        internal int StepCount { get; init; }
    }

    /// <summary>
    /// Headless performance gate for the real viewer-control 3D frame-build path.
    /// </summary>
    internal static class FieldViewer3DPerformanceGateHarness
    {
        /// <summary>Maximum median orbit frame time for FakeField_01 existing 3D.</summary>
        internal const double FakeField01OrbitMedianBudgetMs = 5000.0;

        /// <summary>Maximum single orbit frame time for FakeField_01 existing 3D.</summary>
        internal const double FakeField01OrbitMaxFrameBudgetMs = 5300.0;

        /// <summary>Maximum total wall time for a 16-step FakeField_01 orbit sweep.</summary>
        internal const double FakeField01OrbitTotalBudgetMs = 80_000.0;

        /// <summary>Maximum geometry cache rebuilds allowed during an existing-view orbit sweep.</summary>
        internal const int ExistingOrbitMaxGeometryRebuilds = 0;

        /// <summary>Maximum median orbit frame time for TheShop2 haul 3D after the initial pin.</summary>
        internal const double TheShop2HaulOrbitMedianBudgetMs = 1200.0;

        /// <summary>Maximum single orbit frame time for TheShop2 haul 3D after the initial pin.</summary>
        internal const double TheShop2HaulOrbitMaxFrameBudgetMs = 2500.0;

        /// <summary>Maximum haul overlay stamps allowed after the initial 3D pin frame.</summary>
        internal const int HaulOrbitMaxOverlayStamps = 0;

        /// <summary>Minimum committed shared-surface reuses expected during haul 3D orbit.</summary>
        internal const int HaulOrbitMinCommittedSurfaceReuses = 8;

        /// <summary>Maximum frame rebuild time after a vertical-exaggeration slider change.</summary>
        internal const double ExaggerationChangeFrameBudgetMs = 8000.0;

        /// <summary>Maximum geometry cache rebuilds allowed after a vertical-exaggeration slider change.</summary>
        internal const int ExaggerationChangeMaxGeometryRebuilds = 0;

        /// <summary>Maximum allowed cold 3D prepare time after a warm 2D frame in milliseconds.</summary>
        internal const int MaxColdThreeDPrepareMs = 45_000;

        /// <summary>Maximum allowed 3D frame rebuild after toggling satellite on a warm mesh cache.</summary>
        internal const int MaxSatelliteToggleRebuildMs = 5000;

        /// <summary>
        /// Creates a warm existing-view 3D viewer ready for frame builds.
        /// </summary>
        /// <param name="Project">Project with survey data.</param>
        /// <param name="Display">Display settings for rendering.</param>
        /// <returns>Configured headless viewer in 3D mode.</returns>
        internal static FieldViewerControl CreateExistingThreeDViewer
            (
            ProjectDocument Project,
            DisplaySettings Display
            )
        {
            Project.ViewerState.FieldContentView = FieldContentView.Existing;
            FieldViewerControl Viewer = FieldViewer3DRegressionHarness.Create3DViewer(Project, Display);
            Viewer.ContentView = FieldContentView.Existing;
            return Viewer;
        }

        /// <summary>
        /// Creates a haul 3D viewer with a pinned route scrub index.
        /// </summary>
        /// <param name="Project">Project with survey, design, and haul data.</param>
        /// <param name="Display">Display settings for rendering.</param>
        /// <param name="RouteScrubIndex">Pinned haul route scrub index.</param>
        /// <returns>Configured headless viewer in haul 3D mode.</returns>
        internal static FieldViewerControl CreateHaulThreeDViewer
            (
            ProjectDocument Project,
            DisplaySettings Display,
            int RouteScrubIndex
            )
        {
            Project.ViewerState.FieldContentView = FieldContentView.Haul;
            Project.ViewerState.ViewMode = ViewMode.ThreeD;
            FieldViewerControl Viewer = ImportHandoffHarness.CreateViewer(Project);
            Viewer.ContentView = FieldContentView.Haul;
            Viewer.ViewMode = ViewMode.ThreeD;
            Viewer.HaulThreeDRouteScrubIndex = RouteScrubIndex;
            Viewer.SuppressSynchronousPrepare = false;
            Viewer.DisplaySettings = Display;
            Viewer.EnsureSurfaceBuiltForFit();
            return Viewer;
        }

        /// <summary>
        /// Builds the initial haul 3D pin frame, then measures subsequent orbit frame builds.
        /// </summary>
        /// <param name="Viewer">Headless haul 3D viewer.</param>
        /// <param name="StartHeadingDeg">First measured orbit heading in degrees.</param>
        /// <param name="StepCount">Number of timed orbit steps after the pin frame.</param>
        /// <returns>Timing and invariant metrics for the measured orbit steps.</returns>
        internal static FieldViewer3DOrbitMetrics MeasureHaulThreeDOrbitAfterPin
            (
            FieldViewerControl Viewer,
            double StartHeadingDeg = 45.0,
            int StepCount = FieldViewer3DRegressionHarness.ORBIT_FRAME_COUNT
            )
        {
            Field3DGeometryCache.TestResetGeometryBuildCount();
            Viewer.TestResetPerformanceCounters();

            Assert.True(
                Viewer.TestBuildAndCache3DFrame(),
                "Initial haul 3D pin frame did not commit a monolithic packet.");
            Assert.Equal(1, Viewer.TestHaulThreeDOverlayStampCount);

            Field3DGeometryCache.TestResetGeometryBuildCount();
            Viewer.TestResetPerformanceCounters();
            return MeasureOrbitBuildSequence(Viewer, StartHeadingDeg, StepCount);
        }

        /// <summary>
        /// Warms one 3D frame, then measures orbit frame builds through the viewer control path.
        /// </summary>
        /// <param name="Viewer">Headless viewer in 3D mode.</param>
        /// <param name="StartHeadingDeg">First measured orbit heading in degrees.</param>
        /// <param name="StepCount">Number of timed orbit steps after warmup.</param>
        /// <returns>Timing and invariant metrics for the measured orbit steps.</returns>
        internal static FieldViewer3DOrbitMetrics MeasureOrbitBuildSequence
            (
            FieldViewerControl Viewer,
            double StartHeadingDeg = 45.0,
            int StepCount = FieldViewer3DRegressionHarness.ORBIT_FRAME_COUNT
            )
        {
            Field3DGeometryCache.TestResetGeometryBuildCount();
            Viewer.TestResetPerformanceCounters();

            Viewer.OrbitHeadingDeg = StartHeadingDeg;
            Assert.True(
                Viewer.TestBuildAndCache3DFrame(),
                "Warmup 3D frame did not commit a monolithic packet.");

            Field3DGeometryCache.TestResetGeometryBuildCount();
            Viewer.TestResetPerformanceCounters();

            List<double> FrameTimesMs = new List<double>(StepCount);
            Stopwatch TotalWatch = Stopwatch.StartNew();
            for (int Step = 0; Step < StepCount; Step++)
            {
                Viewer.OrbitHeadingDeg = StartHeadingDeg + Step * FieldViewer3DRegressionHarness.ORBIT_HEADING_STEP_DEG;
                Stopwatch FrameWatch = Stopwatch.StartNew();
                Assert.True(
                    Viewer.TestBuildAndCache3DFrame(),
                    $"3D frame build failed at orbit step {Step}.");
                FrameWatch.Stop();
                FrameTimesMs.Add(FrameWatch.Elapsed.TotalMilliseconds);
            }

            TotalWatch.Stop();
            FrameTimesMs.Sort();
            return new FieldViewer3DOrbitMetrics
            {
                MedianFrameMs = FrameTimesMs[FrameTimesMs.Count / 2],
                MaxFrameMs = FrameTimesMs[^1],
                TotalMs = TotalWatch.Elapsed.TotalMilliseconds,
                GeometryCacheBuildCount = Field3DGeometryCache.TestGeometryCacheBuildCount,
                HaulOverlayStampCount = Viewer.TestHaulThreeDOverlayStampCount,
                HaulCommittedSurfaceReuseCount = Viewer.TestHaulThreeDCommittedSurfaceReuseCount,
                StepCount = StepCount
            };
        }

        /// <summary>
        /// Applies a vertical-exaggeration slider change and measures the next 3D frame rebuild.
        /// </summary>
        /// <param name="Viewer">Warm 3D viewer.</param>
        /// <param name="NewExaggeration">Updated vertical exaggeration multiplier.</param>
        /// <returns>Frame rebuild time and geometry-cache rebuild count.</returns>
        internal static (double FrameMs, int GeometryCacheBuildCount) MeasureExaggerationChangeRebuild
            (
            FieldViewerControl Viewer,
            double NewExaggeration
            )
        {
            Field3DGeometryCache.TestResetGeometryBuildCount();
            DisplaySettings Updated = Viewer.DisplaySettings ?? new DisplaySettings();
            Updated.VerticalExaggeration = NewExaggeration;
            Viewer.DisplaySettings = Updated;
            if (Viewer.Project?.ViewerState != null)
                Viewer.Project.ViewerState.Display.VerticalExaggeration = NewExaggeration;

            Viewer.ScheduleInteractiveRenderParameterRefresh();

            Stopwatch FrameWatch = Stopwatch.StartNew();
            Assert.True(
                Viewer.TestBuildAndCache3DFrame(),
                "3D frame rebuild after exaggeration change did not commit a monolithic packet.");
            FrameWatch.Stop();

            return (FrameWatch.Elapsed.TotalMilliseconds, Field3DGeometryCache.TestGeometryCacheBuildCount);
        }

        /// <summary>
        /// Formats orbit metrics for assertion failure messages.
        /// </summary>
        /// <param name="Metrics">Collected orbit metrics.</param>
        /// <param name="Label">Scenario label.</param>
        /// <returns>Human-readable summary.</returns>
        internal static string DescribeOrbitMetrics
            (
            FieldViewer3DOrbitMetrics Metrics,
            string Label
            )
        {
            return $"{Label}: median={Metrics.MedianFrameMs:F1} ms max={Metrics.MaxFrameMs:F1} ms total={Metrics.TotalMs:F1} ms "
                + $"geomRebuilds={Metrics.GeometryCacheBuildCount} haulStamps={Metrics.HaulOverlayStampCount} "
                + $"haulReuse={Metrics.HaulCommittedSurfaceReuseCount} steps={Metrics.StepCount}";
        }
    }
}
