using AgGradeDesigner.Controls;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Core.Tests.Import;
using AgGradeDesigner.Core.Tests.Integration;
using AgGradeDesigner.Core.Tests.TestFixtures;

namespace AgGradeDesigner.Core.Tests.Rendering
{
    /// <summary>
    /// Headless viewer integration regression tests for 2D frame cache and plan-view compositing.
    /// Run after rendering changes: dotnet test --filter "Category=RenderingRegression"
    /// </summary>
    [Collection("ImportHandoff")]
    [Trait("Category", "RenderingRegression")]
    public sealed class FieldViewer2DRegressionTests
    {
        /// <summary>
        /// Building 2D frames through FieldViewerControl must not produce mostly-black cached pixels.
        /// </summary>
        [Fact]
        public async Task FakeField01_Viewer2D_FittedFrame_IsNotBlack
            (
            )
        {
            await HeadlessAvaloniaBootstrap.RunOnUiThreadAsync(async () =>
            {
                ProjectDocument Project = ImportHandoffProjectFactory.CreateFakeField01Project();
                DisplaySettings Display = FieldRenderer3DBenchmark.CreateDefaultDisplay(Satellite: false);
                FieldViewerControl Viewer = FieldViewer2DRegressionHarness.Create2DViewer(Project, Display);

                FieldViewer2DRegressionHarness.BuildCurrent2DFrame(Viewer);
                byte[]? Pixels = FieldViewer2DRegressionHarness.CopyCurrent2DFrameBytes(Viewer);
                Assert.NotNull(Pixels);

                RenderFrameAnalyzer.FrameMetrics Metrics = RenderFrameAnalyzer.AnalyzeBgraBytes(
                    Pixels!,
                    ImportHandoffHarness.DefaultViewportWidth,
                    ImportHandoffHarness.DefaultViewportHeight);

                Assert.False(RenderFrameAnalyzer.IsMostlyBlack(Metrics), RenderFrameAnalyzer.Describe(Metrics, "viewer 2D"));
                Assert.True(RenderFrameAnalyzer.HasMinimumVisibleContent(Metrics), RenderFrameAnalyzer.Describe(Metrics, "viewer 2D"));
                return true;
            });
        }

        /// <summary>
        /// Pan interaction through the viewer control must not flash black between consecutive frames.
        /// </summary>
        [Fact]
        public async Task FakeField01_Viewer2D_PanSequence_NoBlackFlashes
            (
            )
        {
            await HeadlessAvaloniaBootstrap.RunOnUiThreadAsync(async () =>
            {
                ProjectDocument Project = ImportHandoffProjectFactory.CreateFakeField01Project();
                DisplaySettings Display = FieldRenderer3DBenchmark.CreateDefaultDisplay(Satellite: false);
                FieldViewerControl Viewer = FieldViewer2DRegressionHarness.Create2DViewer(Project, Display);

                IReadOnlyList<RenderFrameAnalyzer.FrameMetrics> Metrics =
                    FieldViewer2DRegressionHarness.RenderPanSequence(Viewer);

                for (int Index = 1; Index < Metrics.Count; Index++)
                {
                    Assert.False(
                        RenderFrameAnalyzer.DetectBlackFlash(Metrics[Index - 1], Metrics[Index]),
                        $"Black flash at pan step {Index}: before={Metrics[Index - 1].VisibleFraction:P1}, after={Metrics[Index].VisibleFraction:P1}");
                }

                return true;
            });
        }

        /// <summary>
        /// Zooming in via zoom factor must keep visible content in the cached 2D frame.
        /// </summary>
        [Fact]
        public async Task FakeField01_Viewer2D_ZoomIn_HasVisibleContent
            (
            )
        {
            await HeadlessAvaloniaBootstrap.RunOnUiThreadAsync(async () =>
            {
                ProjectDocument Project = ImportHandoffProjectFactory.CreateFakeField01Project();
                DisplaySettings Display = FieldRenderer3DBenchmark.CreateDefaultDisplay(Satellite: false);
                FieldViewerControl Viewer = FieldViewer2DRegressionHarness.Create2DViewer(Project, Display);

                Viewer.ZoomFactor *= FieldViewer2DRegressionHarness.ZOOM_IN_MULTIPLIER;
                FieldViewer2DRegressionHarness.BuildCurrent2DFrame(Viewer);
                byte[]? Pixels = FieldViewer2DRegressionHarness.CopyCurrent2DFrameBytes(Viewer);
                Assert.NotNull(Pixels);

                RenderFrameAnalyzer.FrameMetrics Metrics = RenderFrameAnalyzer.AnalyzeBgraBytes(
                    Pixels!,
                    ImportHandoffHarness.DefaultViewportWidth,
                    ImportHandoffHarness.DefaultViewportHeight);

                Assert.True(RenderFrameAnalyzer.HasMinimumVisibleContent(Metrics), RenderFrameAnalyzer.Describe(Metrics, "zoomed 2D"));
                return true;
            });
        }

        /// <summary>
        /// Seeded flat-map satellite tiles must appear when rendering through the viewer control path.
        /// </summary>
        [Fact]
        public async Task FakeField01_Viewer2D_WithSeededSatellite_ShowsMapColour
            (
            )
        {
            await HeadlessAvaloniaBootstrap.RunOnUiThreadAsync(async () =>
            {
                ProjectDocument Project = ImportHandoffProjectFactory.CreateFakeField01Project();
                DisplaySettings Display = FieldRenderer3DBenchmark.CreateDefaultDisplay(Satellite: true);
                FieldViewerControl Viewer = FieldViewer2DRegressionHarness.Create2DViewer(Project, Display);
                FieldViewer2DRegressionHarness.SeedViewerSatelliteMap(Viewer);

                FieldViewer2DRegressionHarness.BuildCurrent2DFrame(Viewer);
                byte[]? Pixels = FieldViewer2DRegressionHarness.CopyCurrent2DFrameBytes(Viewer);
                Assert.NotNull(Pixels);

                double SatelliteFraction = RenderFrameAnalyzer.ComputeSatelliteColorFraction(
                    Pixels!,
                    ImportHandoffHarness.DefaultViewportWidth,
                    ImportHandoffHarness.DefaultViewportHeight,
                    FieldViewer3DRegressionHarness.SeededSatelliteColor);

                Assert.True(
                    SatelliteFraction >= RenderFrameAnalyzer.MIN_SATELLITE_PIXEL_FRACTION,
                    $"Viewer 2D satellite colour absent: matched {SatelliteFraction:P2} of pixels.");
                return true;
            });
        }
    }
}
