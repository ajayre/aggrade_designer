using AgGradeDesigner.Controls;
using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Core.Tests.Import;

namespace AgGradeDesigner.Core.Tests.TestFixtures
{
    /// <summary>
    /// Drives headless <see cref="FieldViewerControl"/> through 2D pan/zoom for integration regression tests.
    /// </summary>
    internal static class FieldViewer2DRegressionHarness
    {
        /// <summary>
        /// Longitude offset in decimal degrees between consecutive pan regression frames.
        /// </summary>
        internal const double PAN_LON_STEP_DEG = 0.0003;

        /// <summary>
        /// Number of pan steps in a full regression sweep.
        /// </summary>
        internal const int PAN_FRAME_COUNT = 12;

        /// <summary>
        /// Zoom factor multiplier applied during zoom-in regression.
        /// </summary>
        internal const double ZOOM_IN_MULTIPLIER = 2.0;

        /// <summary>
        /// Creates a 2D viewer with fitted camera and warm surface ready for frame builds.
        /// </summary>
        /// <param name="Project">Project with survey data.</param>
        /// <param name="Display">Display settings for rendering.</param>
        /// <returns>Configured headless viewer in 2D mode.</returns>
        internal static FieldViewerControl Create2DViewer
            (
            ProjectDocument Project,
            DisplaySettings Display
            )
        {
            FieldViewerControl Viewer = ImportHandoffHarness.CreateViewer(Project);
            Viewer.ViewMode = ViewMode.TwoD;
            if (Project.ViewerState != null)
            {
                Project.ViewerState.ViewMode = ViewMode.TwoD;
                Project.ViewerState.FieldContentView = FieldContentView.Existing;
            }

            Viewer.SuppressSynchronousPrepare = false;
            Viewer.DisplaySettings = Display;
            ImportHandoffProjectFactory.ApplyImportCameraFit(
                Project,
                Viewer,
                ImportHandoffHarness.DefaultViewportWidth,
                ImportHandoffHarness.DefaultViewportHeight);
            Viewer.EnsureSurfaceBuiltForFit();
            return Viewer;
        }

        /// <summary>
        /// Builds and caches the monolithic 2D frame for the current viewer camera.
        /// </summary>
        /// <param name="Viewer">Headless viewer in 2D mode.</param>
        internal static void BuildCurrent2DFrame
            (
            FieldViewerControl Viewer
            )
        {
            Assert.True(Viewer.TestBuildAndCache2DFrame(), "2D frame build did not commit a monolithic packet.");
        }

        /// <summary>
        /// Returns pixel bytes for the cached 2D monolithic frame.
        /// </summary>
        /// <param name="Viewer">Headless viewer with a cached 2D frame.</param>
        /// <returns>BGRA pixel buffer or null when no cache exists.</returns>
        internal static byte[]? CopyCurrent2DFrameBytes
            (
            FieldViewerControl Viewer
            )
        {
            FieldContentView ContentView = Viewer.Project?.ViewerState?.FieldContentView ?? FieldContentView.Existing;
            return Viewer.TestCopyMonolithicPacketBytes(ContentView);
        }

        /// <summary>
        /// Renders a sequence of eastward pans and returns metrics for each frame.
        /// </summary>
        /// <param name="Viewer">Headless viewer in 2D mode.</param>
        /// <returns>Metrics collected after each pan step.</returns>
        internal static IReadOnlyList<RenderFrameAnalyzer.FrameMetrics> RenderPanSequence
            (
            FieldViewerControl Viewer
            )
        {
            double StartLon = Viewer.CentreLongitude;
            List<RenderFrameAnalyzer.FrameMetrics> Metrics = new List<RenderFrameAnalyzer.FrameMetrics>(PAN_FRAME_COUNT);
            for (int Step = 0; Step < PAN_FRAME_COUNT; Step++)
            {
                Viewer.CentreLongitude = StartLon + Step * PAN_LON_STEP_DEG;
                BuildCurrent2DFrame(Viewer);
                byte[]? Pixels = CopyCurrent2DFrameBytes(Viewer);
                Assert.NotNull(Pixels);
                Metrics.Add(RenderFrameAnalyzer.AnalyzeBgraBytes(
                    Pixels!,
                    ImportHandoffHarness.DefaultViewportWidth,
                    ImportHandoffHarness.DefaultViewportHeight));
            }

            return Metrics;
        }

        /// <summary>
        /// Seeds visible flat-map satellite tiles for the current 2D camera.
        /// </summary>
        /// <param name="Viewer">Viewer whose satellite service will receive tiles.</param>
        internal static void SeedViewerSatelliteMap
            (
            FieldViewerControl Viewer
            )
        {
            Viewer.TestSeedSatelliteMapTiles(
                Viewer.CentreLatitude,
                Viewer.CentreLongitude,
                Viewer.ZoomFactor,
                FieldViewer3DRegressionHarness.SeededSatelliteColor);
        }
    }
}
