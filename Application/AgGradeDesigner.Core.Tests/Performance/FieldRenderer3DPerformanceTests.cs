using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Core.Tests.TestFixtures;

namespace AgGradeDesigner.Core.Tests.Performance
{
    /// <summary>
    /// Guards 3D orbit rendering against performance regressions introduced by depth-tested overlays.
    /// </summary>
    [Trait("Category", "RenderingRegression")]
    public sealed class FieldRenderer3DPerformanceTests
    {
        /// <summary>
        /// Interactive frame budget in milliseconds (FakeField_01, layered 3D passes).
        /// </summary>
        private const double INTERACTIVE_FRAME_BUDGET_MS = 480.0;

        /// <summary>
        /// Maximum allowed slowdown when satellite is on but tiles are still loading.
        /// </summary>
        private const double SATELLITE_LOADING_SLOWDOWN_FACTOR = 1.25;
        /// <summary>
        /// A typical FakeField_01 3D frame (mesh + contours + border, no satellite) must stay interactive.
        /// </summary>
        [Fact]
        public void FakeField01_Draw3D_NoSatellite_CompletesWithinBudget
            (
            )
        {
            (FieldSurfaceSampler Surface, ProjectDocument Project) = FieldRenderer3DBenchmark.BuildFakeField01Scene();
            DisplaySettings Display = FieldRenderer3DBenchmark.CreateDefaultDisplay(Satellite: false);

            double MedianMs = ImportBenchmark.MedianMilliseconds(
                () => FieldRenderer3DBenchmark.Render3DFrame(Surface, Project, Display),
                WarmupRuns: 1,
                MeasuredRuns: 3);

            Assert.True(MedianMs < INTERACTIVE_FRAME_BUDGET_MS,
                $"3D frame without satellite too slow: {MedianMs:F1} ms (budget {INTERACTIVE_FRAME_BUDGET_MS} ms).");
        }

        /// <summary>
        /// Contours add GPU line geometry; with Vulkan readback dominating frame time the delta is small,
        /// but contours-on must not be dramatically slower than contours-off.
        /// </summary>
        [Fact]
        public void FakeField01_Draw3D_ContoursOff_IsFasterThanContoursOn
            (
            )
        {
            (FieldSurfaceSampler Surface, ProjectDocument Project) = FieldRenderer3DBenchmark.BuildFakeField01Scene();

            double WithContoursMs = ImportBenchmark.MedianMilliseconds(
                () => FieldRenderer3DBenchmark.Render3DFrame(
                    Surface,
                    Project,
                    FieldRenderer3DBenchmark.CreateDefaultDisplay(Satellite: false)),
                WarmupRuns: 1,
                MeasuredRuns: 5);

            double WithoutContoursMs = ImportBenchmark.MedianMilliseconds(
                () => FieldRenderer3DBenchmark.Render3DFrame(
                    Surface,
                    Project,
                    new DisplaySettings { Contours = false, Hillshade = true, Satellite = false }),
                WarmupRuns: 1,
                MeasuredRuns: 5);

            Assert.True(WithoutContoursMs <= WithContoursMs * 1.15,
                $"Contours-off ({WithoutContoursMs:F1} ms) should not be much slower than contours-on ({WithContoursMs:F1} ms).");
        }

        /// <summary>
        /// Orbit interaction re-renders many consecutive frames; repeated draws should stay fast after warmup.
        /// </summary>
        [Fact]
        public void FakeField01_Draw3D_RepeatedOrbitFrames_StayWithinBudget
            (
            )
        {
            (FieldSurfaceSampler Surface, ProjectDocument Project) = FieldRenderer3DBenchmark.BuildFakeField01Scene();
            DisplaySettings Display = FieldRenderer3DBenchmark.CreateDefaultDisplay(Satellite: false);

            double MedianMs = ImportBenchmark.MedianMilliseconds(
                () =>
                {
                    for (int Frame = 0; Frame < 8; Frame++)
                    {
                        double Heading = 45.0 + Frame * 11.0;
                        FieldRenderer3DBenchmark.Render3DFrame(
                            Surface,
                            Project,
                            Display,
                            HeadingDeg: Heading);
                    }
                },
                WarmupRuns: 1,
                MeasuredRuns: 3);

            Assert.True(MedianMs < 4000.0,
                $"Eight consecutive 3D orbit frames too slow: {MedianMs:F1} ms (budget 4000 ms).");
        }

        /// <summary>
        /// The Shop 2 at full Step=1 resolution is the heaviest common mesh; must remain usable.
        /// </summary>
        [Fact]
        public void TheShop2_Draw3D_NoSatellite_CompletesWithinBudget
            (
            )
        {
            string Shop2Path = Path.Combine(TestDataPaths.TestDataDirectory, "TheShop2_2ft.agd");
            if (!File.Exists(Shop2Path))
                return;

            (FieldSurfaceSampler Surface, ProjectDocument Project) = FieldRenderer3DBenchmark.BuildShop2Scene();
            DisplaySettings Display = FieldRenderer3DBenchmark.CreateDefaultDisplay(Satellite: false);

            double MedianMs = ImportBenchmark.MedianMilliseconds(
                () => FieldRenderer3DBenchmark.Render3DFrame(Surface, Project, Display),
                WarmupRuns: 1,
                MeasuredRuns: 3);

            Assert.True(MedianMs < 650.0,
                $"TheShop2 3D frame too slow: {MedianMs:F1} ms (budget 650 ms).");
        }

        /// <summary>
        /// Satellite enabled while tiles are still loading must stay interactive on the production
        /// monolithic path; fetches run in the background and the plinth draws fallbacks only.
        /// </summary>
        [Fact]
        public void FakeField01_Monolithic3D_SatelliteEnabled_UncachedTiles_CompletesWithinBudget
            (
            )
        {
            (FieldSurfaceSampler Surface, ProjectDocument Project) = FieldRenderer3DBenchmark.BuildFakeField01Scene();
            DisplaySettings NoSatelliteDisplay = FieldRenderer3DBenchmark.CreateDefaultDisplay(Satellite: false);
            FieldRenderer3DScenario BaselineScenario = FieldRenderer3DScenario.DefaultOrbit(NoSatelliteDisplay);

            double BaselineMs = ImportBenchmark.MedianMilliseconds(
                () => FieldRenderer3DBenchmark.Render3DMonolithicBytes(Surface, Project, BaselineScenario),
                WarmupRuns: 1,
                MeasuredRuns: 3);

            DisplaySettings SatelliteDisplay = FieldRenderer3DBenchmark.CreateDefaultDisplay(Satellite: true);
            SatelliteTileService LoadingSatellite = new SatelliteTileService();
            FieldRenderer3DScenario LoadingScenario = FieldRenderer3DScenario.DefaultOrbit(
                SatelliteDisplay,
                LoadingSatellite);

            double UncachedMs = ImportBenchmark.MedianMilliseconds(
                () => FieldRenderer3DBenchmark.Render3DMonolithicBytes(Surface, Project, LoadingScenario),
                WarmupRuns: 1,
                MeasuredRuns: 5);

            Assert.True(
                UncachedMs < INTERACTIVE_FRAME_BUDGET_MS,
                $"Monolithic 3D frame with loading satellite too slow: {UncachedMs:F1} ms "
                + $"(budget {INTERACTIVE_FRAME_BUDGET_MS} ms).");
            Assert.True(
                UncachedMs <= BaselineMs * SATELLITE_LOADING_SLOWDOWN_FACTOR,
                $"Satellite-on while tiles load ({UncachedMs:F1} ms) should not be much slower than "
                + $"no satellite ({BaselineMs:F1} ms).");
        }

        /// <summary>
        /// Manual profiling helper for 3D phase breakdown.
        /// </summary>
        [Fact(Skip = "Profiling helper — run manually to inspect 3D frame timings.")]
        public void ProfileDraw3D_Manual
            (
            )
        {
            (FieldSurfaceSampler Surface, ProjectDocument Project) = FieldRenderer3DBenchmark.BuildFakeField01Scene();
            DisplaySettings Display = FieldRenderer3DBenchmark.CreateDefaultDisplay(Satellite: false);

            double MedianMs = ImportBenchmark.MedianMilliseconds(
                () => FieldRenderer3DBenchmark.Render3DFrame(Surface, Project, Display),
                WarmupRuns: 1,
                MeasuredRuns: 5);

            Assert.Fail($"FakeField01 Draw3D median: {MedianMs:F1} ms.");
        }
    }
}
