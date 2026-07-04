using AgGradeDesigner.Controls;
using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Core.Tests.Import;

using SkiaSharp;

namespace AgGradeDesigner.Core.Tests.TestFixtures
{
    /// <summary>
    /// Drives headless <see cref="FieldViewerControl"/> through 3D camera changes for integration regression tests.
    /// </summary>
    internal static class FieldViewer3DRegressionHarness
    {
        /// <summary>
        /// Heading step in degrees between orbit regression frames.
        /// </summary>
        internal const double ORBIT_HEADING_STEP_DEG = 22.5;

        /// <summary>
        /// Number of orbit steps in a full regression sweep.
        /// </summary>
        internal const int ORBIT_FRAME_COUNT = 16;

        /// <summary>
        /// Pan offset in decimal degrees applied during pan regression.
        /// </summary>
        internal const double PAN_LON_OFFSET_DEG = 0.0005;

        /// <summary>
        /// Seeded satellite tile colour for offline plinth tests.
        /// </summary>
        internal static readonly SKColor SeededSatelliteColor = new SKColor(90, 110, 70);

        /// <summary>
        /// Creates a 3D viewer with a warm surface ready for frame builds.
        /// </summary>
        /// <param name="Project">Project with survey data.</param>
        /// <param name="Display">Display settings for rendering.</param>
        /// <returns>Configured headless viewer in 3D mode.</returns>
        internal static FieldViewerControl Create3DViewer
            (
            ProjectDocument Project,
            DisplaySettings Display
            )
        {
            FieldViewerControl Viewer = ImportHandoffHarness.CreateViewer(Project);
            Viewer.ViewMode = ViewMode.ThreeD;
            if (Project.ViewerState != null)
            {
                Project.ViewerState.ViewMode = ViewMode.ThreeD;
                Project.ViewerState.FieldContentView = FieldContentView.Existing;
            }
            Viewer.SuppressSynchronousPrepare = false;
            Viewer.DisplaySettings = Display;
            Viewer.EnsureSurfaceBuiltForFit();
            return Viewer;
        }

        /// <summary>
        /// Builds and caches the monolithic 3D frame for the current viewer camera.
        /// </summary>
        /// <param name="Viewer">Headless viewer in 3D mode.</param>
        internal static void BuildCurrent3DFrame
            (
            FieldViewerControl Viewer
            )
        {
            Assert.True(Viewer.TestBuildAndCache3DFrame(), "3D frame build did not commit a monolithic packet.");
        }

        /// <summary>
        /// Returns pixel bytes for the cached 3D monolithic frame.
        /// </summary>
        /// <param name="Viewer">Headless viewer with a cached 3D frame.</param>
        /// <returns>BGRA pixel buffer or null when no cache exists.</returns>
        internal static byte[]? CopyCurrent3DFrameBytes
            (
            FieldViewerControl Viewer
            )
        {
            FieldContentView ContentView = Viewer.Project?.ViewerState?.FieldContentView ?? FieldContentView.Existing;
            return Viewer.TestCopy3DMonolithicPacketBytes(ContentView);
        }

        /// <summary>
        /// Renders a sequence of orbit headings and returns metrics for each frame.
        /// </summary>
        /// <param name="Viewer">Headless viewer in 3D mode.</param>
        /// <param name="StartHeadingDeg">First heading in degrees.</param>
        /// <returns>Metrics collected after each orbit step.</returns>
        internal static IReadOnlyList<RenderFrameAnalyzer.FrameMetrics> RenderOrbitSequence
            (
            FieldViewerControl Viewer,
            double StartHeadingDeg = 45.0
            )
        {
            List<RenderFrameAnalyzer.FrameMetrics> Metrics = new List<RenderFrameAnalyzer.FrameMetrics>(ORBIT_FRAME_COUNT);
            for (int Step = 0; Step < ORBIT_FRAME_COUNT; Step++)
            {
                Viewer.OrbitHeadingDeg = StartHeadingDeg + Step * ORBIT_HEADING_STEP_DEG;
                BuildCurrent3DFrame(Viewer);
                byte[]? Pixels = CopyCurrent3DFrameBytes(Viewer);
                Assert.NotNull(Pixels);
                Metrics.Add(RenderFrameAnalyzer.AnalyzeBgraBytes(
                    Pixels!,
                    ImportHandoffHarness.DefaultViewportWidth,
                    ImportHandoffHarness.DefaultViewportHeight));
            }

            return Metrics;
        }

        /// <summary>
        /// Seeds the viewer satellite service with solid plinth tiles.
        /// </summary>
        /// <param name="Viewer">Viewer whose satellite service will receive tiles.</param>
        /// <param name="Surface">Built surface defining plinth layout.</param>
        internal static void SeedViewerSatellite
            (
            FieldViewerControl Viewer,
            FieldSurfaceSampler Surface
            )
        {
            Viewer.TestSeedSatellitePlinthTiles(Surface, SeededSatelliteColor);
        }
    }
}
