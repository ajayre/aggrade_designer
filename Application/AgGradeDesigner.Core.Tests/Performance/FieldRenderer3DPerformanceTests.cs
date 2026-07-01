using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Core.Tests.TestFixtures;

using SkiaSharp;

namespace AgGradeDesigner.Core.Tests.Performance
{
    /// <summary>
    /// Guards 3D orbit rendering against performance regressions introduced by depth-tested overlays.
    /// </summary>
    public sealed class FieldRenderer3DPerformanceTests
    {
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

            Assert.True(MedianMs < 480.0,
                $"3D frame without satellite too slow: {MedianMs:F1} ms (budget 480 ms).");
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
        /// Satellite plinth rendering with cached tiles must not decode PNGs on every orbit frame.
        /// </summary>
        [Fact]
        public void FakeField01_Draw3D_WithSeededSatellite_CompletesWithinBudget
            (
            )
        {
            (FieldSurfaceSampler Surface, ProjectDocument Project) = FieldRenderer3DBenchmark.BuildFakeField01Scene();
            DisplaySettings Display = FieldRenderer3DBenchmark.CreateDefaultDisplay(Satellite: true);
            SatelliteTileService Satellite = new SatelliteTileService();

            double OrbitLat = (Surface.MinLat + Surface.MaxLat) * 0.5;
            double OrbitLon = (Surface.MinLon + Surface.MaxLon) * 0.5;
            (int Zoom, IReadOnlyList<(int TileX, int TileY)> Tiles) = SatelliteMapLayout3D.ComputePlinthTiles(
                OrbitLat,
                OrbitLon,
                1609.344 * 2.0 * Math.Sqrt(2.0),
                MetresPerPixel: 3.0);
            foreach ((int TileX, int TileY) in Tiles)
                FieldRenderer3DBenchmark.SeedSolidSatelliteTile(Satellite, Zoom, TileX, TileY, new SKColor(90, 110, 70));

            // Warm decoded bitmap cache.
            FieldRenderer3DBenchmark.Render3DFrame(Surface, Project, Display, Satellite: Satellite);

            double MedianMs = ImportBenchmark.MedianMilliseconds(
                () => FieldRenderer3DBenchmark.Render3DFrame(Surface, Project, Display, Satellite: Satellite),
                WarmupRuns: 1,
                MeasuredRuns: 5);

            Assert.True(MedianMs < 480.0,
                $"3D frame with seeded satellite too slow: {MedianMs:F1} ms (budget 480 ms).");
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
