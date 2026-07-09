using AgGradeDesigner.Controls;
using AgGradeDesigner.Core.Haul;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Tests.Import;
using AgGradeDesigner.Core.Tests.Integration;
using AgGradeDesigner.Core.Tests.TestFixtures;

namespace AgGradeDesigner.Core.Tests.Haul
{
    /// <summary>
    /// Verifies haul 3D keeps leveled elevations when display toggles rebuild GPU mesh geometry.
    /// </summary>
    [Trait("Category", "HaulRegression")]
    public sealed class HaulThreeDDisplayToggleTests
    {
        /// <summary>
        /// Satellite and other display toggles must re-stamp the haul overlay on the committed 3D surface.
        /// </summary>
        [Fact]
        public async Task SatelliteToggle_RestampsHaulThreeDOverlay
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
                Assert.Equal(1, Viewer.TestHaulThreeDOverlayStampCount);

                Display.Satellite = true;
                Viewer.DisplaySettings = Display;
                Viewer.TestRequestHaulThreeDOverlayRestampForDisplayChange();
                Assert.True(Viewer.TestBuildAndCache3DFrame(), "Satellite-on haul 3D frame did not commit.");
                Assert.Equal(2, Viewer.TestHaulThreeDOverlayStampCount);

                Display.Satellite = false;
                Viewer.DisplaySettings = Display;
                Viewer.TestRequestHaulThreeDOverlayRestampForDisplayChange();
                Assert.True(Viewer.TestBuildAndCache3DFrame(), "Satellite-off haul 3D frame did not commit.");
                Assert.Equal(3, Viewer.TestHaulThreeDOverlayStampCount);
                return true;
            });
        }
    }
}
