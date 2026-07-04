using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Core.Tests.TestFixtures;

using SkiaSharp;

namespace AgGradeDesigner.Core.Tests.Rendering
{
    /// <summary>
    /// Pixel-level regression tests for 3D orbit rendering (black frames, satellite, camera motion, opacity).
    /// Run after rendering changes: dotnet test --filter "Category=RenderingRegression"
    /// </summary>
    [Trait("Category", "RenderingRegression")]
    public sealed class FieldRenderer3DRegressionTests
    {
        /// <summary>
        /// Default orbit heading for baseline scenarios.
        /// </summary>
        private const double DEFAULT_HEADING_DEG = 45.0;

        /// <summary>
        /// Vertical exaggeration multiplier for exaggerated terrain tests.
        /// </summary>
        private const double EXAGGERATED_VERTICAL_MULTIPLIER = 2.0;

        /// <summary>
        /// True-scale vertical exaggeration (slider at 0×).
        /// </summary>
        private const double TRUE_SCALE_VERTICAL_EXAGGERATION = 0.0;

        /// <summary>
        /// A default FakeField_01 monolithic 3D frame must contain visible terrain, not a black screen.
        /// </summary>
        [Fact]
        public void FakeField01_Monolithic3D_DefaultView_HasVisibleContent
            (
            )
        {
            (FieldSurfaceSampler Surface, ProjectDocument Project) = FieldRenderer3DBenchmark.BuildFakeField01Scene();
            DisplaySettings Display = FieldRenderer3DBenchmark.CreateDefaultDisplay(Satellite: false);
            FieldRenderer3DScenario Scenario = FieldRenderer3DScenario.DefaultOrbit(Display);

            byte[] Pixels = FieldRenderer3DBenchmark.Render3DMonolithicBytes(Surface, Project, Scenario);
            RenderFrameAnalyzer.FrameMetrics Metrics = RenderFrameAnalyzer.AnalyzeBgraBytes(
                Pixels,
                FieldRenderer3DBenchmark.ViewportWidth,
                FieldRenderer3DBenchmark.ViewportHeight);

            Assert.False(RenderFrameAnalyzer.IsMostlyBlack(Metrics), RenderFrameAnalyzer.Describe(Metrics, "default view"));
            Assert.True(RenderFrameAnalyzer.HasMinimumVisibleContent(Metrics), RenderFrameAnalyzer.Describe(Metrics, "default view"));
        }

        /// <summary>
        /// Sixteen consecutive orbit headings must each produce visible content with no black-flash drops.
        /// </summary>
        [Fact]
        public void FakeField01_Monolithic3D_OrbitSequence_NoBlackFramesOrFlashes
            (
            )
        {
            (FieldSurfaceSampler Surface, ProjectDocument Project) = FieldRenderer3DBenchmark.BuildFakeField01Scene();
            DisplaySettings Display = FieldRenderer3DBenchmark.CreateDefaultDisplay(Satellite: false);

            RenderFrameAnalyzer.FrameMetrics? Prior = null;
            for (int Step = 0; Step < FieldViewer3DRegressionHarness.ORBIT_FRAME_COUNT; Step++)
            {
                double Heading = DEFAULT_HEADING_DEG + Step * FieldViewer3DRegressionHarness.ORBIT_HEADING_STEP_DEG;
                FieldRenderer3DScenario Scenario = FieldRenderer3DScenario.DefaultOrbit(Display) with { HeadingDeg = Heading };
                byte[] Pixels = FieldRenderer3DBenchmark.Render3DMonolithicBytes(Surface, Project, Scenario);
                RenderFrameAnalyzer.FrameMetrics Metrics = RenderFrameAnalyzer.AnalyzeBgraBytes(
                    Pixels,
                    FieldRenderer3DBenchmark.ViewportWidth,
                    FieldRenderer3DBenchmark.ViewportHeight);

                Assert.False(
                    RenderFrameAnalyzer.IsMostlyBlack(Metrics),
                    $"Heading {Heading:F1}°: {RenderFrameAnalyzer.Describe(Metrics)}");

                if (Prior.HasValue)
                {
                    Assert.False(
                        RenderFrameAnalyzer.DetectBlackFlash(Prior.Value, Metrics),
                        $"Black flash at heading {Heading:F1}°: before={Prior.Value.VisibleFraction:P1}, after={Metrics.VisibleFraction:P1}");
                }

                Prior = Metrics;
            }
        }

        /// <summary>
        /// Zoomed-in and zoomed-out cameras must both render visible terrain.
        /// </summary>
        [Fact]
        public void FakeField01_Monolithic3D_ZoomInAndOut_BothHaveVisibleContent
            (
            )
        {
            (FieldSurfaceSampler Surface, ProjectDocument Project) = FieldRenderer3DBenchmark.BuildFakeField01Scene();
            DisplaySettings Display = FieldRenderer3DBenchmark.CreateDefaultDisplay(Satellite: false);

            foreach (FieldRenderer3DScenario Scenario in new[]
                     {
                         FieldRenderer3DScenario.ZoomedIn(Display),
                         FieldRenderer3DScenario.ZoomedOut(Display)
                     })
            {
                byte[] Pixels = FieldRenderer3DBenchmark.Render3DMonolithicBytes(Surface, Project, Scenario);
                RenderFrameAnalyzer.FrameMetrics Metrics = RenderFrameAnalyzer.AnalyzeBgraBytes(
                    Pixels,
                    FieldRenderer3DBenchmark.ViewportWidth,
                    FieldRenderer3DBenchmark.ViewportHeight);

                Assert.True(
                    RenderFrameAnalyzer.HasMinimumVisibleContent(Metrics),
                    $"Distance {Scenario.DistanceM:F0} m: {RenderFrameAnalyzer.Describe(Metrics)}");
            }
        }

        /// <summary>
        /// Rotating the camera must change the frame while keeping visible content.
        /// </summary>
        [Fact]
        public void FakeField01_Monolithic3D_Rotation_ChangesPixelsAndStaysVisible
            (
            )
        {
            (FieldSurfaceSampler Surface, ProjectDocument Project) = FieldRenderer3DBenchmark.BuildFakeField01Scene();
            DisplaySettings Display = FieldRenderer3DBenchmark.CreateDefaultDisplay(Satellite: false);

            FieldRenderer3DScenario First = FieldRenderer3DScenario.DefaultOrbit(Display) with { HeadingDeg = 30.0 };
            FieldRenderer3DScenario Second = FieldRenderer3DScenario.DefaultOrbit(Display) with { HeadingDeg = 120.0 };

            byte[] FirstPixels = FieldRenderer3DBenchmark.Render3DMonolithicBytes(Surface, Project, First);
            byte[] SecondPixels = FieldRenderer3DBenchmark.Render3DMonolithicBytes(Surface, Project, Second);

            double Diff = RenderFrameAnalyzer.ComputeDiffFraction(
                FirstPixels,
                SecondPixels,
                FieldRenderer3DBenchmark.ViewportWidth,
                FieldRenderer3DBenchmark.ViewportHeight);

            RenderFrameAnalyzer.FrameMetrics SecondMetrics = RenderFrameAnalyzer.AnalyzeBgraBytes(
                SecondPixels,
                FieldRenderer3DBenchmark.ViewportWidth,
                FieldRenderer3DBenchmark.ViewportHeight);

            Assert.True(Diff >= RenderFrameAnalyzer.MIN_FRAME_DIFF_FRACTION,
                $"Rotation produced insufficient pixel change: {Diff:P2}.");
            Assert.True(RenderFrameAnalyzer.HasMinimumVisibleContent(SecondMetrics),
                RenderFrameAnalyzer.Describe(SecondMetrics, "rotated view"));
        }

        /// <summary>
        /// Layered compositing at 50% topo opacity must still show visible terrain.
        /// </summary>
        [Fact]
        public void FakeField01_Layered3D_HalfOpacity_HasVisibleContent
            (
            )
        {
            (FieldSurfaceSampler Surface, ProjectDocument Project) = FieldRenderer3DBenchmark.BuildFakeField01Scene();
            DisplaySettings Display = FieldRenderer3DBenchmark.CreateDefaultDisplay(Satellite: false);
            FieldRenderer3DScenario Scenario = FieldRenderer3DScenario.LowOpacity(Display);

            byte[] Pixels = FieldRenderer3DBenchmark.Render3DLayeredCompositeBytes(Surface, Project, Scenario);
            RenderFrameAnalyzer.FrameMetrics Metrics = RenderFrameAnalyzer.AnalyzeBgraBytes(
                Pixels,
                FieldRenderer3DBenchmark.ViewportWidth,
                FieldRenderer3DBenchmark.ViewportHeight);

            Assert.False(RenderFrameAnalyzer.IsMostlyBlack(Metrics), RenderFrameAnalyzer.Describe(Metrics, "50% opacity"));
            Assert.True(RenderFrameAnalyzer.HasMinimumVisibleContent(Metrics), RenderFrameAnalyzer.Describe(Metrics, "50% opacity"));
        }

        /// <summary>
        /// Seeded satellite tiles must appear as visible plinth colour in the monolithic frame.
        /// </summary>
        [Fact]
        public void FakeField01_Monolithic3D_WithSeededSatellite_ShowsPlinthColour
            (
            )
        {
            (FieldSurfaceSampler Surface, ProjectDocument Project) = FieldRenderer3DBenchmark.BuildFakeField01Scene();
            DisplaySettings Display = FieldRenderer3DBenchmark.CreateDefaultDisplay(Satellite: true);
            SatelliteTileService Satellite = FieldRenderer3DBenchmark.CreateSeededSatelliteService(
                Surface,
                FieldViewer3DRegressionHarness.SeededSatelliteColor);
            FieldRenderer3DScenario Scenario = FieldRenderer3DScenario.DefaultOrbit(Display, Satellite);

            byte[] Pixels = FieldRenderer3DBenchmark.Render3DMonolithicBytes(Surface, Project, Scenario);
            double SatelliteFraction = RenderFrameAnalyzer.ComputeSatelliteColorFraction(
                Pixels,
                FieldRenderer3DBenchmark.ViewportWidth,
                FieldRenderer3DBenchmark.ViewportHeight,
                FieldViewer3DRegressionHarness.SeededSatelliteColor);

            Assert.True(
                SatelliteFraction >= RenderFrameAnalyzer.MIN_SATELLITE_PIXEL_FRACTION,
                $"Seeded satellite colour absent: matched {SatelliteFraction:P2} of pixels.");
        }

        /// <summary>
        /// True-scale and 2× vertical exaggeration must both produce visible frames.
        /// </summary>
        [Fact]
        public void FakeField01_Monolithic3D_VerticalExaggeration0And2x_BothVisible
            (
            )
        {
            (FieldSurfaceSampler Surface, ProjectDocument Project) = FieldRenderer3DBenchmark.BuildFakeField01Scene();

            foreach (double Exaggeration in new[] { TRUE_SCALE_VERTICAL_EXAGGERATION, EXAGGERATED_VERTICAL_MULTIPLIER })
            {
                DisplaySettings Display = FieldRenderer3DBenchmark.CreateDefaultDisplay(Satellite: false);
                Display.VerticalExaggeration = Exaggeration;
                FieldRenderer3DScenario Scenario = FieldRenderer3DScenario.DefaultOrbit(Display);

                byte[] Pixels = FieldRenderer3DBenchmark.Render3DMonolithicBytes(Surface, Project, Scenario);
                RenderFrameAnalyzer.FrameMetrics Metrics = RenderFrameAnalyzer.AnalyzeBgraBytes(
                    Pixels,
                    FieldRenderer3DBenchmark.ViewportWidth,
                    FieldRenderer3DBenchmark.ViewportHeight);

                Assert.True(
                    RenderFrameAnalyzer.HasMinimumVisibleContent(Metrics),
                    $"Exaggeration {Exaggeration}×: {RenderFrameAnalyzer.Describe(Metrics)}");
            }
        }

        /// <summary>
        /// Panning the orbit centre must keep visible content in frame.
        /// </summary>
        [Fact]
        public void FakeField01_Monolithic3D_PannedOrbitCentre_HasVisibleContent
            (
            )
        {
            (FieldSurfaceSampler Surface, ProjectDocument Project) = FieldRenderer3DBenchmark.BuildFakeField01Scene();
            DisplaySettings Display = FieldRenderer3DBenchmark.CreateDefaultDisplay(Satellite: false);

            double CentreLat = (Surface.MinLat + Surface.MaxLat) * 0.5;
            double CentreLon = (Surface.MinLon + Surface.MaxLon) * 0.5;
            FieldRenderer3DScenario Scenario = FieldRenderer3DScenario.DefaultOrbit(Display) with
            {
                OrbitCentreLat = CentreLat,
                OrbitCentreLon = CentreLon + FieldViewer3DRegressionHarness.PAN_LON_OFFSET_DEG
            };

            byte[] Pixels = FieldRenderer3DBenchmark.Render3DMonolithicBytes(Surface, Project, Scenario);
            RenderFrameAnalyzer.FrameMetrics Metrics = RenderFrameAnalyzer.AnalyzeBgraBytes(
                Pixels,
                FieldRenderer3DBenchmark.ViewportWidth,
                FieldRenderer3DBenchmark.ViewportHeight);

            Assert.True(RenderFrameAnalyzer.HasMinimumVisibleContent(Metrics), RenderFrameAnalyzer.Describe(Metrics, "panned"));
        }
    }
}
