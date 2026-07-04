using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Core.Tests.TestFixtures;

namespace AgGradeDesigner.Core.Tests.Rendering
{
    /// <summary>
    /// Pixel-level regression tests for 2D plan-view rendering (pan, zoom, opacity, satellite basemap).
    /// Run after rendering changes: dotnet test --filter "Category=RenderingRegression"
    /// </summary>
    [Trait("Category", "RenderingRegression")]
    public sealed class FieldRenderer2DRegressionTests
    {
        /// <summary>
        /// Vertical exaggeration multiplier for exaggerated terrain tests.
        /// </summary>
        private const double EXAGGERATED_VERTICAL_MULTIPLIER = 2.0;

        /// <summary>
        /// True-scale vertical exaggeration (slider at 0×).
        /// </summary>
        private const double TRUE_SCALE_VERTICAL_EXAGGERATION = 0.0;

        /// <summary>
        /// A fitted FakeField_01 2D monolithic frame must contain visible terrain, not a black screen.
        /// </summary>
        [Fact]
        public void FakeField01_Monolithic2D_FittedView_HasVisibleContent
            (
            )
        {
            (FieldSurfaceSampler Surface, ProjectDocument Project) = FieldRenderer3DBenchmark.BuildFakeField01Scene();
            DisplaySettings Display = FieldRenderer3DBenchmark.CreateDefaultDisplay(Satellite: false);
            FieldRenderer2DScenario Scenario = FieldRenderer2DScenario.FitToField(Surface, Project, Display);

            byte[] Pixels = FieldRenderer2DBenchmark.Render2DMonolithicBytes(Surface, Project, Scenario);
            RenderFrameAnalyzer.FrameMetrics Metrics = RenderFrameAnalyzer.AnalyzeBgraBytes(
                Pixels,
                FieldRenderer2DBenchmark.ViewportWidth,
                FieldRenderer2DBenchmark.ViewportHeight);

            Assert.False(RenderFrameAnalyzer.IsMostlyBlack(Metrics), RenderFrameAnalyzer.Describe(Metrics, "fitted 2D"));
            Assert.True(RenderFrameAnalyzer.HasMinimumVisibleContent(Metrics), RenderFrameAnalyzer.Describe(Metrics, "fitted 2D"));
        }

        /// <summary>
        /// Twelve consecutive eastward pans must each produce visible content with no black-flash drops.
        /// </summary>
        [Fact]
        public void FakeField01_Monolithic2D_PanSequence_NoBlackFramesOrFlashes
            (
            )
        {
            (FieldSurfaceSampler Surface, ProjectDocument Project) = FieldRenderer3DBenchmark.BuildFakeField01Scene();
            DisplaySettings Display = FieldRenderer3DBenchmark.CreateDefaultDisplay(Satellite: false);
            FieldRenderer2DScenario Base = FieldRenderer2DScenario.FitToField(Surface, Project, Display);

            RenderFrameAnalyzer.FrameMetrics? Prior = null;
            for (int Step = 0; Step < FieldViewer2DRegressionHarness.PAN_FRAME_COUNT; Step++)
            {
                FieldRenderer2DScenario Scenario = Base with
                {
                    CentreLon = Base.CentreLon + Step * FieldViewer2DRegressionHarness.PAN_LON_STEP_DEG
                };
                byte[] Pixels = FieldRenderer2DBenchmark.Render2DMonolithicBytes(Surface, Project, Scenario);
                RenderFrameAnalyzer.FrameMetrics Metrics = RenderFrameAnalyzer.AnalyzeBgraBytes(
                    Pixels,
                    FieldRenderer2DBenchmark.ViewportWidth,
                    FieldRenderer2DBenchmark.ViewportHeight);

                Assert.False(
                    RenderFrameAnalyzer.IsMostlyBlack(Metrics),
                    $"Pan step {Step}: {RenderFrameAnalyzer.Describe(Metrics)}");

                if (Prior.HasValue)
                {
                    Assert.False(
                        RenderFrameAnalyzer.DetectBlackFlash(Prior.Value, Metrics),
                        $"Black flash at pan step {Step}: before={Prior.Value.VisibleFraction:P1}, after={Metrics.VisibleFraction:P1}");
                }

                Prior = Metrics;
            }
        }

        /// <summary>
        /// Zoomed-in and zoomed-out 2D views must both render visible terrain.
        /// </summary>
        [Fact]
        public void FakeField01_Monolithic2D_ZoomInAndOut_BothHaveVisibleContent
            (
            )
        {
            (FieldSurfaceSampler Surface, ProjectDocument Project) = FieldRenderer3DBenchmark.BuildFakeField01Scene();
            DisplaySettings Display = FieldRenderer3DBenchmark.CreateDefaultDisplay(Satellite: false);
            FieldRenderer2DScenario Base = FieldRenderer2DScenario.FitToField(Surface, Project, Display);

            foreach (FieldRenderer2DScenario Scenario in new[]
                     {
                         FieldRenderer2DScenario.ZoomedIn(Base),
                         FieldRenderer2DScenario.ZoomedOut(Base)
                     })
            {
                byte[] Pixels = FieldRenderer2DBenchmark.Render2DMonolithicBytes(Surface, Project, Scenario);
                RenderFrameAnalyzer.FrameMetrics Metrics = RenderFrameAnalyzer.AnalyzeBgraBytes(
                    Pixels,
                    FieldRenderer2DBenchmark.ViewportWidth,
                    FieldRenderer2DBenchmark.ViewportHeight);

                Assert.True(
                    RenderFrameAnalyzer.HasMinimumVisibleContent(Metrics),
                    $"Zoom {Scenario.ZoomFactor:F2}×: {RenderFrameAnalyzer.Describe(Metrics)}");
            }
        }

        /// <summary>
        /// Panning east must change pixels while keeping visible content.
        /// </summary>
        [Fact]
        public void FakeField01_Monolithic2D_Pan_ChangesPixelsAndStaysVisible
            (
            )
        {
            (FieldSurfaceSampler Surface, ProjectDocument Project) = FieldRenderer3DBenchmark.BuildFakeField01Scene();
            DisplaySettings Display = FieldRenderer3DBenchmark.CreateDefaultDisplay(Satellite: false);
            FieldRenderer2DScenario Base = FieldRenderer2DScenario.FitToField(Surface, Project, Display);
            FieldRenderer2DScenario Panned = Base with
            {
                CentreLon = Base.CentreLon + FieldViewer2DRegressionHarness.PAN_LON_STEP_DEG * 4
            };

            byte[] BeforePixels = FieldRenderer2DBenchmark.Render2DMonolithicBytes(Surface, Project, Base);
            byte[] AfterPixels = FieldRenderer2DBenchmark.Render2DMonolithicBytes(Surface, Project, Panned);

            double Diff = RenderFrameAnalyzer.ComputeDiffFraction(
                BeforePixels,
                AfterPixels,
                FieldRenderer2DBenchmark.ViewportWidth,
                FieldRenderer2DBenchmark.ViewportHeight);

            RenderFrameAnalyzer.FrameMetrics AfterMetrics = RenderFrameAnalyzer.AnalyzeBgraBytes(
                AfterPixels,
                FieldRenderer2DBenchmark.ViewportWidth,
                FieldRenderer2DBenchmark.ViewportHeight);

            Assert.True(Diff >= RenderFrameAnalyzer.MIN_FRAME_DIFF_FRACTION,
                $"Pan produced insufficient pixel change: {Diff:P2}.");
            Assert.True(RenderFrameAnalyzer.HasMinimumVisibleContent(AfterMetrics),
                RenderFrameAnalyzer.Describe(AfterMetrics, "panned 2D"));
        }

        /// <summary>
        /// Layered compositing at 50% topo opacity must still show visible terrain in 2D.
        /// </summary>
        [Fact]
        public void FakeField01_Layered2D_HalfOpacity_HasVisibleContent
            (
            )
        {
            (FieldSurfaceSampler Surface, ProjectDocument Project) = FieldRenderer3DBenchmark.BuildFakeField01Scene();
            DisplaySettings Display = FieldRenderer3DBenchmark.CreateDefaultDisplay(Satellite: false);
            FieldRenderer2DScenario Scenario = FieldRenderer2DScenario.LowOpacity(
                FieldRenderer2DScenario.FitToField(Surface, Project, Display));

            byte[] Pixels = FieldRenderer2DBenchmark.Render2DLayeredCompositeBytes(Surface, Project, Scenario);
            RenderFrameAnalyzer.FrameMetrics Metrics = RenderFrameAnalyzer.AnalyzeBgraBytes(
                Pixels,
                FieldRenderer2DBenchmark.ViewportWidth,
                FieldRenderer2DBenchmark.ViewportHeight);

            Assert.False(RenderFrameAnalyzer.IsMostlyBlack(Metrics), RenderFrameAnalyzer.Describe(Metrics, "50% opacity 2D"));
            Assert.True(RenderFrameAnalyzer.HasMinimumVisibleContent(Metrics), RenderFrameAnalyzer.Describe(Metrics, "50% opacity 2D"));
        }

        /// <summary>
        /// Seeded flat-map satellite tiles must appear in the monolithic 2D frame.
        /// </summary>
        [Fact]
        public void FakeField01_Monolithic2D_WithSeededSatellite_ShowsMapColour
            (
            )
        {
            (FieldSurfaceSampler Surface, ProjectDocument Project) = FieldRenderer3DBenchmark.BuildFakeField01Scene();
            DisplaySettings Display = FieldRenderer3DBenchmark.CreateDefaultDisplay(Satellite: true);
            FieldRenderer2DScenario Base = FieldRenderer2DScenario.FitToField(Surface, Project, Display);
            SatelliteTileService Satellite = FieldRenderer2DBenchmark.CreateSeededSatelliteMapService(
                Base,
                FieldViewer3DRegressionHarness.SeededSatelliteColor);
            FieldRenderer2DScenario Scenario = Base with { Satellite = Satellite };

            byte[] Pixels = FieldRenderer2DBenchmark.Render2DMonolithicBytes(Surface, Project, Scenario);
            double SatelliteFraction = RenderFrameAnalyzer.ComputeSatelliteColorFraction(
                Pixels,
                FieldRenderer2DBenchmark.ViewportWidth,
                FieldRenderer2DBenchmark.ViewportHeight,
                FieldViewer3DRegressionHarness.SeededSatelliteColor);

            Assert.True(
                SatelliteFraction >= RenderFrameAnalyzer.MIN_SATELLITE_PIXEL_FRACTION,
                $"Seeded 2D satellite colour absent: matched {SatelliteFraction:P2} of pixels.");
        }

        /// <summary>
        /// True-scale and 2× vertical exaggeration must both produce visible 2D frames.
        /// </summary>
        [Fact]
        public void FakeField01_Monolithic2D_VerticalExaggeration0And2x_BothVisible
            (
            )
        {
            (FieldSurfaceSampler Surface, ProjectDocument Project) = FieldRenderer3DBenchmark.BuildFakeField01Scene();

            foreach (double Exaggeration in new[] { TRUE_SCALE_VERTICAL_EXAGGERATION, EXAGGERATED_VERTICAL_MULTIPLIER })
            {
                DisplaySettings Display = FieldRenderer3DBenchmark.CreateDefaultDisplay(Satellite: false);
                Display.VerticalExaggeration = Exaggeration;
                FieldRenderer2DScenario Scenario = FieldRenderer2DScenario.FitToField(Surface, Project, Display);

                byte[] Pixels = FieldRenderer2DBenchmark.Render2DMonolithicBytes(Surface, Project, Scenario);
                RenderFrameAnalyzer.FrameMetrics Metrics = RenderFrameAnalyzer.AnalyzeBgraBytes(
                    Pixels,
                    FieldRenderer2DBenchmark.ViewportWidth,
                    FieldRenderer2DBenchmark.ViewportHeight);

                Assert.True(
                    RenderFrameAnalyzer.HasMinimumVisibleContent(Metrics),
                    $"2D exaggeration {Exaggeration}×: {RenderFrameAnalyzer.Describe(Metrics)}");
            }
        }
    }
}
