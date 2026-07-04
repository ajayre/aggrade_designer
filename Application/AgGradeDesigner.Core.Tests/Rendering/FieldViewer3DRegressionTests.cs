using AgGradeDesigner.Controls;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Core.Tests.Import;
using AgGradeDesigner.Core.Tests.Integration;
using AgGradeDesigner.Core.Tests.TestFixtures;

namespace AgGradeDesigner.Core.Tests.Rendering
{
    /// <summary>
    /// Headless viewer integration regression tests for 3D frame cache and compositing paths.
    /// Run after rendering changes: dotnet test --filter "Category=RenderingRegression"
    /// </summary>
    [Collection("ImportHandoff")]
    [Trait("Category", "RenderingRegression")]
    public sealed class FieldViewer3DRegressionTests
    {
        /// <summary>
        /// Building 3D frames through FieldViewerControl must not produce mostly-black cached pixels.
        /// </summary>
        [Fact]
        public async Task FakeField01_Viewer3D_DefaultFrame_IsNotBlack
            (
            )
        {
            await HeadlessAvaloniaBootstrap.RunOnUiThreadAsync(async () =>
            {
                ProjectDocument Project = ImportHandoffProjectFactory.CreateFakeField01Project();
                DisplaySettings Display = FieldRenderer3DBenchmark.CreateDefaultDisplay(Satellite: false);
                FieldViewerControl Viewer = FieldViewer3DRegressionHarness.Create3DViewer(Project, Display);
                FieldViewer3DRegressionHarness.BuildCurrent3DFrame(Viewer);
                byte[]? Pixels = FieldViewer3DRegressionHarness.CopyCurrent3DFrameBytes(Viewer);
                Assert.NotNull(Pixels);

                RenderFrameAnalyzer.FrameMetrics Metrics = RenderFrameAnalyzer.AnalyzeBgraBytes(
                    Pixels!,
                    ImportHandoffHarness.DefaultViewportWidth,
                    ImportHandoffHarness.DefaultViewportHeight);

                Assert.False(RenderFrameAnalyzer.IsMostlyBlack(Metrics), RenderFrameAnalyzer.Describe(Metrics, "viewer 3D"));
                Assert.True(RenderFrameAnalyzer.HasMinimumVisibleContent(Metrics), RenderFrameAnalyzer.Describe(Metrics, "viewer 3D"));
                return true;
            });
        }

        /// <summary>
        /// Orbit interaction through the viewer control must not flash black between consecutive frames.
        /// </summary>
        [Fact]
        public async Task FakeField01_Viewer3D_OrbitSequence_NoBlackFlashes
            (
            )
        {
            await HeadlessAvaloniaBootstrap.RunOnUiThreadAsync(async () =>
            {
                ProjectDocument Project = ImportHandoffProjectFactory.CreateFakeField01Project();
                DisplaySettings Display = FieldRenderer3DBenchmark.CreateDefaultDisplay(Satellite: false);
                FieldViewerControl Viewer = FieldViewer3DRegressionHarness.Create3DViewer(Project, Display);

                IReadOnlyList<RenderFrameAnalyzer.FrameMetrics> Metrics =
                    FieldViewer3DRegressionHarness.RenderOrbitSequence(Viewer);

                for (int Index = 1; Index < Metrics.Count; Index++)
                {
                    Assert.False(
                        RenderFrameAnalyzer.DetectBlackFlash(Metrics[Index - 1], Metrics[Index]),
                        $"Black flash at orbit step {Index}: before={Metrics[Index - 1].VisibleFraction:P1}, after={Metrics[Index].VisibleFraction:P1}");
                }

                return true;
            });
        }

        /// <summary>
        /// Seeded satellite tiles must appear when rendering through the viewer control path.
        /// </summary>
        [Fact]
        public async Task FakeField01_Viewer3D_WithSeededSatellite_ShowsPlinthColour
            (
            )
        {
            await HeadlessAvaloniaBootstrap.RunOnUiThreadAsync(async () =>
            {
                ProjectDocument Project = ImportHandoffProjectFactory.CreateFakeField01Project();
                DisplaySettings Display = FieldRenderer3DBenchmark.CreateDefaultDisplay(Satellite: true);
                FieldViewerControl Viewer = FieldViewer3DRegressionHarness.Create3DViewer(Project, Display);
                FieldViewer3DRegressionHarness.SeedViewerSatellite(Viewer, Viewer.FieldSurface);

                FieldViewer3DRegressionHarness.BuildCurrent3DFrame(Viewer);
                byte[]? Pixels = FieldViewer3DRegressionHarness.CopyCurrent3DFrameBytes(Viewer);
                Assert.NotNull(Pixels);

                double SatelliteFraction = RenderFrameAnalyzer.ComputeSatelliteColorFraction(
                    Pixels!,
                    ImportHandoffHarness.DefaultViewportWidth,
                    ImportHandoffHarness.DefaultViewportHeight,
                    FieldViewer3DRegressionHarness.SeededSatelliteColor);

                Assert.True(
                    SatelliteFraction >= RenderFrameAnalyzer.MIN_SATELLITE_PIXEL_FRACTION,
                    $"Viewer satellite colour absent: matched {SatelliteFraction:P2} of pixels.");
                return true;
            });
        }
    }
}
