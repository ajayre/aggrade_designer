using AgGradeDesigner.Controls;
using AgGradeDesigner.Core.Haul;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Core.Tests.Import;
using AgGradeDesigner.Core.Tests.Integration;
using AgGradeDesigner.Core.Tests.TestFixtures;

namespace AgGradeDesigner.Core.Tests.Performance
{
    /// <summary>
    /// Performance and orchestration invariants for the real <see cref="FieldViewerControl"/> 3D path.
    /// Run on viewer/rendering changes: dotnet test --filter "Category=Viewer3DPerformanceGate"
    /// </summary>
    [Collection("ImportHandoff")]
    [Trait("Category", "RenderingRegression")]
    [Trait("Category", "Viewer3DPerformanceGate")]
    public sealed class FieldViewer3DPerformanceGateTests
    {
        /// <summary>
        /// Existing-view orbit through FieldViewerControl must stay interactive and reuse geometry.
        /// </summary>
        [Fact]
        public async Task FakeField01_ExistingThreeD_OrbitSequence_StaysWithinBudgetWithoutGeometryRebuilds
            (
            )
        {
            await HeadlessAvaloniaBootstrap.RunOnUiThreadAsync(async () =>
            {
                ProjectDocument Project = ImportHandoffProjectFactory.CreateFakeField01Project();
                DisplaySettings Display = FieldRenderer3DBenchmark.CreateDefaultDisplay(Satellite: false);
                FieldViewerControl Viewer = FieldViewer3DPerformanceGateHarness.CreateExistingThreeDViewer(Project, Display);

                FieldViewer3DOrbitMetrics Metrics =
                    FieldViewer3DPerformanceGateHarness.MeasureOrbitBuildSequence(Viewer);

                Assert.True(
                    Metrics.GeometryCacheBuildCount <= FieldViewer3DPerformanceGateHarness.ExistingOrbitMaxGeometryRebuilds,
                    FieldViewer3DPerformanceGateHarness.DescribeOrbitMetrics(Metrics, "geometry rebuild regression"));
                Assert.True(
                    Metrics.MedianFrameMs <= FieldViewer3DPerformanceGateHarness.FakeField01OrbitMedianBudgetMs,
                    FieldViewer3DPerformanceGateHarness.DescribeOrbitMetrics(Metrics, "median frame regression"));
                Assert.True(
                    Metrics.MaxFrameMs <= FieldViewer3DPerformanceGateHarness.FakeField01OrbitMaxFrameBudgetMs,
                    FieldViewer3DPerformanceGateHarness.DescribeOrbitMetrics(Metrics, "max frame regression"));
                Assert.True(
                    Metrics.TotalMs <= FieldViewer3DPerformanceGateHarness.FakeField01OrbitTotalBudgetMs,
                    FieldViewer3DPerformanceGateHarness.DescribeOrbitMetrics(Metrics, "total orbit regression"));
                return true;
            });
        }

        /// <summary>
        /// Haul 3D must pin once, reuse the committed surface during orbit, and stay interactive.
        /// </summary>
        [Fact]
        public async Task TheShop2_HaulThreeD_OrbitAfterPin_ReusesCommittedSurfaceWithoutRestamping
            (
            )
        {
            string Shop2Path = Path.Combine(TestDataPaths.TestDataDirectory, "TheShop2_2ft.agd");
            if (!File.Exists(Shop2Path))
                return;

            await HeadlessAvaloniaBootstrap.RunOnUiThreadAsync(async () =>
            {
                ProjectDocument Project = ImportHandoffProjectFactory.CreateTheShop2Project();
                HaulData Data = HaulEngineService.Generate(Project, new HaulSettings());
                if (Data.RouteStepCount < 1)
                    return true;

                Project.HaulData = Data;
                DisplaySettings Display = FieldRenderer3DBenchmark.CreateDefaultDisplay(Satellite: false);
                FieldViewerControl Viewer = FieldViewer3DPerformanceGateHarness.CreateHaulThreeDViewer(
                    Project,
                    Display,
                    RouteScrubIndex: 0);

                FieldViewer3DOrbitMetrics Metrics =
                    FieldViewer3DPerformanceGateHarness.MeasureHaulThreeDOrbitAfterPin(Viewer);

                Assert.True(
                    Metrics.HaulOverlayStampCount <= FieldViewer3DPerformanceGateHarness.HaulOrbitMaxOverlayStamps,
                    FieldViewer3DPerformanceGateHarness.DescribeOrbitMetrics(Metrics, "haul overlay restamp regression"));
                Assert.True(
                    Metrics.HaulCommittedSurfaceReuseCount >= FieldViewer3DPerformanceGateHarness.HaulOrbitMinCommittedSurfaceReuses,
                    FieldViewer3DPerformanceGateHarness.DescribeOrbitMetrics(Metrics, "committed haul surface reuse regression"));
                Assert.True(
                    Metrics.GeometryCacheBuildCount <= FieldViewer3DPerformanceGateHarness.ExistingOrbitMaxGeometryRebuilds,
                    FieldViewer3DPerformanceGateHarness.DescribeOrbitMetrics(Metrics, "haul geometry rebuild regression"));
                Assert.True(
                    Metrics.MedianFrameMs <= FieldViewer3DPerformanceGateHarness.TheShop2HaulOrbitMedianBudgetMs,
                    FieldViewer3DPerformanceGateHarness.DescribeOrbitMetrics(Metrics, "haul median frame regression"));
                Assert.True(
                    Metrics.MaxFrameMs <= FieldViewer3DPerformanceGateHarness.TheShop2HaulOrbitMaxFrameBudgetMs,
                    FieldViewer3DPerformanceGateHarness.DescribeOrbitMetrics(Metrics, "haul max frame regression"));
                return true;
            });
        }

        /// <summary>
        /// Vertical-exaggeration slider changes must not force a full geometry rebuild or black out the frame.
        /// </summary>
        [Fact]
        public async Task FakeField01_ExistingThreeD_ExaggerationChange_RebuildsWithoutGeometryInvalidation
            (
            )
        {
            await HeadlessAvaloniaBootstrap.RunOnUiThreadAsync(async () =>
            {
                ProjectDocument Project = ImportHandoffProjectFactory.CreateFakeField01Project();
                DisplaySettings Display = FieldRenderer3DBenchmark.CreateDefaultDisplay(Satellite: false);
                FieldViewerControl Viewer = FieldViewer3DPerformanceGateHarness.CreateExistingThreeDViewer(Project, Display);
                Assert.True(Viewer.TestBuildAndCache3DFrame(), "Warmup 3D frame did not commit.");

                double InitialExaggeration = Display.VerticalExaggeration;
                (double FrameMs, int GeometryRebuilds) = FieldViewer3DPerformanceGateHarness.MeasureExaggerationChangeRebuild(
                    Viewer,
                    InitialExaggeration * 1.15);

                byte[]? Pixels = FieldViewer3DRegressionHarness.CopyCurrent3DFrameBytes(Viewer);
                Assert.NotNull(Pixels);
                RenderFrameAnalyzer.FrameMetrics Metrics = RenderFrameAnalyzer.AnalyzeBgraBytes(
                    Pixels!,
                    ImportHandoffHarness.DefaultViewportWidth,
                    ImportHandoffHarness.DefaultViewportHeight);

                Assert.False(
                    RenderFrameAnalyzer.IsMostlyBlack(Metrics),
                    RenderFrameAnalyzer.Describe(Metrics, "3D after exaggeration change"));
                Assert.True(
                    GeometryRebuilds <= FieldViewer3DPerformanceGateHarness.ExaggerationChangeMaxGeometryRebuilds,
                    $"Exaggeration change triggered {GeometryRebuilds} geometry rebuild(s).");
                Assert.True(
                    FrameMs <= FieldViewer3DPerformanceGateHarness.ExaggerationChangeFrameBudgetMs,
                    $"Exaggeration-change rebuild too slow: {FrameMs:F1} ms.");
                return true;
            });
        }
    }
}
