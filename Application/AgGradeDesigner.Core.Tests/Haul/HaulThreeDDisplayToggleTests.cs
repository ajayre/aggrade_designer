using AgGradeDesigner.Controls;
using AgGradeDesigner.Core.Haul;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Tests.Import;
using AgGradeDesigner.Core.Tests.Integration;
using AgGradeDesigner.Core.Tests.TestFixtures;

namespace AgGradeDesigner.Core.Tests.Haul
{
    /// <summary>
    /// Verifies haul 3D keeps the one-time haul-leveled surface when display toggles rebuild GPU mesh geometry.
    /// </summary>
    [Trait("Category", "HaulRegression")]
    public sealed class HaulThreeDDisplayToggleTests
    {
        /// <summary>
        /// Satellite and other display toggles must reuse the committed haul surface without rebuilding elevations.
        /// </summary>
        [Fact]
        public async Task SatelliteToggle_ReusesCommittedHaulThreeDSurface
            (
            )
        {
            await HeadlessAvaloniaBootstrap.RunOnUiThreadAsync(async () =>
            {
                ProjectDocument Project = ImportHandoffProjectFactory.CreateTheShop2Project();
                HaulData Data = HaulEngineService.Generate(Project, new HaulSettings());
                if (Data.RouteStepCount < 1)
                    return true;

                Project.HaulData = Data;
                DisplaySettings Display = new DisplaySettings
                {
                    Satellite = false,
                    Contours = true,
                    Hillshade = true,
                };
                FieldViewerControl Viewer = FieldViewer3DPerformanceGateHarness.CreateHaulThreeDViewer(
                    Project,
                    Display,
                    RouteScrubIndex: 0);
                Viewer.TestResetPerformanceCounters();

                Assert.True(Viewer.TestBuildAndCache3DFrame(), "Initial haul 3D pin frame did not commit.");
                Assert.Equal(1, Viewer.TestHaulThreeDBuildCount);

                Display.Satellite = true;
                Viewer.DisplaySettings = Display;
                Assert.True(Viewer.TestBuildAndCache3DFrame(), "Satellite-on haul 3D frame did not commit.");
                Assert.Equal(1, Viewer.TestHaulThreeDBuildCount);

                Display.Satellite = false;
                Viewer.DisplaySettings = Display;
                Assert.True(Viewer.TestBuildAndCache3DFrame(), "Satellite-off haul 3D frame did not commit.");
                Assert.Equal(1, Viewer.TestHaulThreeDBuildCount);
                return true;
            });
        }

        /// <summary>
        /// Contour toggles must reuse the committed haul surface and invalidate stale survey contour caches.
        /// </summary>
        [Fact]
        public async Task ContoursToggle_ReusesCommittedHaulThreeDSurface
            (
            )
        {
            await HeadlessAvaloniaBootstrap.RunOnUiThreadAsync(async () =>
            {
                ProjectDocument Project = ImportHandoffProjectFactory.CreateTheShop2Project();
                HaulData Data = HaulEngineService.Generate(Project, new HaulSettings());
                if (Data.RouteStepCount < 1)
                    return true;

                Project.HaulData = Data;
                DisplaySettings Display = new DisplaySettings
                {
                    Satellite = true,
                    Contours = true,
                    Hillshade = true,
                };
                FieldViewerControl Viewer = FieldViewer3DPerformanceGateHarness.CreateHaulThreeDViewer(
                    Project,
                    Display,
                    RouteScrubIndex: 0);
                Viewer.TestResetPerformanceCounters();

                Assert.True(Viewer.TestBuildAndCache3DFrame(), "Initial haul 3D pin frame did not commit.");
                Assert.Equal(1, Viewer.TestHaulThreeDBuildCount);

                Display.Contours = false;
                Viewer.DisplaySettings = Display;
                Assert.True(Viewer.TestBuildAndCache3DFrame(), "Contours-off haul 3D frame did not commit.");
                Assert.Equal(1, Viewer.TestHaulThreeDBuildCount);

                Display.Contours = true;
                Viewer.DisplaySettings = Display;
                Assert.True(Viewer.TestBuildAndCache3DFrame(), "Contours-on haul 3D frame did not commit.");
                Assert.Equal(1, Viewer.TestHaulThreeDBuildCount);
                return true;
            });
        }

        /// <summary>
        /// Brief prepare-lock contention during display toggles must not rebuild the haul-leveled channel.
        /// </summary>
        [Fact]
        public async Task DisplayToggleDuringPrepareLockContention_ReusesCommittedHaulSurface
            (
            )
        {
            await HeadlessAvaloniaBootstrap.RunOnUiThreadAsync(async () =>
            {
                ProjectDocument Project = ImportHandoffProjectFactory.CreateTheShop2Project();
                HaulData Data = HaulEngineService.Generate(Project, new HaulSettings());
                if (Data.RouteStepCount < 1)
                    return true;

                Project.HaulData = Data;
                DisplaySettings Display = new DisplaySettings
                {
                    Satellite = false,
                    Contours = true,
                    Hillshade = true,
                    Water = false,
                    HaulFlow = false,
                };
                FieldViewerControl Viewer = FieldViewer3DPerformanceGateHarness.CreateHaulThreeDViewer(
                    Project,
                    Display,
                    RouteScrubIndex: 0);
                Viewer.TestResetPerformanceCounters();

                Assert.True(Viewer.TestBuildAndCache3DFrame(), "Initial haul 3D pin frame did not commit.");
                Assert.Equal(1, Viewer.TestHaulThreeDBuildCount);

                Task HoldTask = Viewer.TestHoldPrepareLockAsync(1500);
                await Task.Delay(100);

                Display.Water = true;
                Display.HaulFlow = true;
                Viewer.DisplaySettings = Display;
                Assert.True(
                    Viewer.TestBuildAndCache3DFrame(),
                    "Water/haul-flow toggle frame did not commit during prepare-lock contention.");
                Assert.Equal(1, Viewer.TestHaulThreeDBuildCount);

                await HoldTask;
                return true;
            });
        }
    }
}
