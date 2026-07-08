using System.Diagnostics;

using AgGradeDesigner.Controls;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Core.Tests.Import;
using AgGradeDesigner.Core.Tests.Integration;
using AgGradeDesigner.Core.Tests.TestFixtures;

namespace AgGradeDesigner.Core.Tests.Rendering
{
    /// <summary>
    /// Headless acceptance tests for warm 2D to cold 3D view-mode handoff through synchronous prepare.
    /// Run after view-mode changes: dotnet test --filter "ViewModeSwitchHandoff=true"
    /// </summary>
    [Collection("ImportHandoff")]
    [Trait("Category", "RenderingRegression")]
    [Trait("ViewModeSwitchHandoff", "true")]
    public sealed class ViewModeSwitchHandoffTests
    {
        /// <summary>
        /// Synchronous 3D prepare after a warm 2D frame must yield an orbit-ready, non-black composite.
        /// </summary>
        [Fact]
        public async Task FakeField01_WarmTwoD_ToThreeD_SyncPrepare_IsOrbitReady
            (
            )
        {
            await HeadlessAvaloniaBootstrap.RunOnUiThreadAsync(async () =>
            {
                ProjectDocument Project = ImportHandoffProjectFactory.CreateFakeField01Project();
                DisplaySettings Display = FieldRenderer3DBenchmark.CreateDefaultDisplay(Satellite: false);
                FieldViewerControl Viewer = FieldViewer3DRegressionHarness.CreateWarmTwoDViewer(Project, Display);

                Stopwatch PrepareWatch = Stopwatch.StartNew();
                await FieldViewer3DRegressionHarness.SwitchWarmTwoDToThreeDAsync(Viewer);
                PrepareWatch.Stop();

                Assert.True(
                    PrepareWatch.ElapsedMilliseconds <= FieldViewer3DRegressionHarness.MaxColdThreeDPrepareMs,
                    $"Cold 3D prepare exceeded budget: {PrepareWatch.ElapsedMilliseconds} ms.");

                byte[]? BeforeOrbitPixels = FieldViewer3DRegressionHarness.CopyCurrent3DFrameBytes(Viewer);
                Assert.NotNull(BeforeOrbitPixels);

                RenderFrameAnalyzer.FrameMetrics BeforeOrbitMetrics = RenderFrameAnalyzer.AnalyzeBgraBytes(
                    BeforeOrbitPixels!,
                    ImportHandoffHarness.DefaultViewportWidth,
                    ImportHandoffHarness.DefaultViewportHeight);
                Assert.False(
                    RenderFrameAnalyzer.IsMostlyBlack(BeforeOrbitMetrics),
                    RenderFrameAnalyzer.Describe(BeforeOrbitMetrics, "3D after mode switch"));
                Assert.True(
                    RenderFrameAnalyzer.HasMinimumVisibleContent(BeforeOrbitMetrics),
                    RenderFrameAnalyzer.Describe(BeforeOrbitMetrics, "3D after mode switch"));

                double InitialHeadingDeg = Viewer.OrbitHeadingDeg;
                Viewer.OrbitHeadingDeg = InitialHeadingDeg + FieldViewer3DRegressionHarness.ORBIT_HEADING_STEP_DEG;
                FieldViewer3DRegressionHarness.BuildCurrent3DFrame(Viewer);

                byte[]? AfterOrbitPixels = FieldViewer3DRegressionHarness.CopyCurrent3DFrameBytes(Viewer);
                Assert.NotNull(AfterOrbitPixels);
                Assert.False(
                    BeforeOrbitPixels!.AsSpan().SequenceEqual(AfterOrbitPixels!),
                    "Orbit rebuild produced identical pixels; camera change did not update the frame.");

                RenderFrameAnalyzer.FrameMetrics AfterOrbitMetrics = RenderFrameAnalyzer.AnalyzeBgraBytes(
                    AfterOrbitPixels!,
                    ImportHandoffHarness.DefaultViewportWidth,
                    ImportHandoffHarness.DefaultViewportHeight);
                Assert.False(
                    RenderFrameAnalyzer.IsMostlyBlack(AfterOrbitMetrics),
                    RenderFrameAnalyzer.Describe(AfterOrbitMetrics, "3D after orbit"));
                return true;
            });
        }

        /// <summary>
        /// Toggling satellite on a warm 3D mesh must not re-tessellate the full surface grid.
        /// </summary>
        [Fact]
        public async Task FakeField01_WarmThreeD_SatelliteToggle_RebuildWithinBudget
            (
            )
        {
            await HeadlessAvaloniaBootstrap.RunOnUiThreadAsync(async () =>
            {
                ProjectDocument Project = ImportHandoffProjectFactory.CreateFakeField01Project();
                DisplaySettings DisplayOff = FieldRenderer3DBenchmark.CreateDefaultDisplay(Satellite: false);
                FieldViewerControl Viewer = FieldViewer3DRegressionHarness.Create3DViewer(Project, DisplayOff);
                FieldViewer3DRegressionHarness.BuildCurrent3DFrame(Viewer);

                DisplaySettings DisplayOn = FieldRenderer3DBenchmark.CreateDefaultDisplay(Satellite: true);
                Viewer.DisplaySettings = DisplayOn;
                if (Project.ViewerState != null)
                    Project.ViewerState.Display.Satellite = true;

                Stopwatch ToggleWatch = Stopwatch.StartNew();
                FieldViewer3DRegressionHarness.BuildCurrent3DFrame(Viewer);
                ToggleWatch.Stop();

                Assert.True(
                    ToggleWatch.ElapsedMilliseconds <= FieldViewer3DRegressionHarness.MaxSatelliteToggleRebuildMs,
                    $"Satellite toggle rebuild exceeded budget: {ToggleWatch.ElapsedMilliseconds} ms.");

                byte[]? Pixels = FieldViewer3DRegressionHarness.CopyCurrent3DFrameBytes(Viewer);
                Assert.NotNull(Pixels);
                RenderFrameAnalyzer.FrameMetrics Metrics = RenderFrameAnalyzer.AnalyzeBgraBytes(
                    Pixels!,
                    ImportHandoffHarness.DefaultViewportWidth,
                    ImportHandoffHarness.DefaultViewportHeight);
                Assert.False(RenderFrameAnalyzer.IsMostlyBlack(Metrics), RenderFrameAnalyzer.Describe(Metrics, "3D after satellite toggle"));
                return true;
            });
        }
    }
}
