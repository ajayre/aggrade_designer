using AgGradeDesigner.Core.Design;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Tests.Ai.Fixtures;
using AgGradeDesigner.Core.Water;

namespace AgGradeDesigner.Core.Tests.Water
{
    /// <summary>
    /// Verifies water elevation grids match AgGrade bin orientation (north at row 0).
    /// </summary>
    public sealed class WaterGridOrientationTests
    {
        /// <summary>
        /// Verifies the northernmost east bin maps to row 0, easternmost column in the DEM.
        /// </summary>
        [Fact]
        public void DesignGridBuild_PlacesNorthEastBinAtRowZeroEastColumn
            (
            )
        {
            DesignGrid Grid = SyntheticDesignGridFixture.CreateRectGrid(3, 4, 100.0, 100.0);
            GridBinData NorthEastBin = Grid.Bins.First(Bin => Bin.X == 2 && Bin.Y == 3);
            NorthEastBin.ExistingM = 42.0;

            ProjectDocument Project = CreateProjectWithGrid(Grid);
            WaterElevationGridBuilder.ElevationGrid? ElevationGrid = WaterElevationGridBuilder.TryBuild(
                Project,
                WaterBasisSurfaceKind.Existing,
                RouteScrubIndex: 0);
            Assert.NotNull(ElevationGrid);

            Assert.Equal(42.0f, ElevationGrid!.Dem[0, 2]);
            Assert.True(ElevationGrid.MaxLat > ElevationGrid.MinLat);
            Assert.True(ElevationGrid.MaxLon > ElevationGrid.MinLon);
        }

        /// <summary>
        /// Verifies the southernmost west bin maps to the last row, column zero in the DEM.
        /// </summary>
        [Fact]
        public void DesignGridBuild_PlacesSouthWestBinAtLastRowWestColumn
            (
            )
        {
            DesignGrid Grid = SyntheticDesignGridFixture.CreateRectGrid(3, 4, 100.0, 100.0);
            GridBinData SouthWestBin = Grid.Bins.First(Bin => Bin.X == 0 && Bin.Y == 0);
            SouthWestBin.ExistingM = 17.0;

            ProjectDocument Project = CreateProjectWithGrid(Grid);
            WaterElevationGridBuilder.ElevationGrid? ElevationGrid = WaterElevationGridBuilder.TryBuild(
                Project,
                WaterBasisSurfaceKind.Existing,
                RouteScrubIndex: 0);
            Assert.NotNull(ElevationGrid);

            Assert.Equal(17.0f, ElevationGrid!.Dem[3, 0]);
        }

        /// <summary>
        /// Verifies leveled haul grids are flipped so the northern row maps to DEM row 0.
        /// </summary>
        [Fact]
        public void HaulGridBuild_PlacesNorthernRowAtDemRowZero
            (
            )
        {
            const int GridWidth = 3;
            const int GridHeight = 4;
            const float SouthElevationM = 10f;
            const float NorthElevationM = 99f;

            HaulBuilder.Grid LeveledGrid = CreateLeveledHaulGrid(GridWidth, GridHeight);
            LeveledGrid.Existing[0, 1] = SouthElevationM;
            LeveledGrid.Existing[GridHeight - 1, 1] = NorthElevationM;

            WaterElevationGridBuilder.ElevationGrid? ElevationGrid =
                WaterElevationGridBuilder.TryBuildFromLeveledHaulGrid(LeveledGrid);
            Assert.NotNull(ElevationGrid);

            Assert.Equal(NorthElevationM, ElevationGrid!.Dem[0, 1]);
            Assert.Equal(SouthElevationM, ElevationGrid.Dem[GridHeight - 1, 1]);
        }

        /// <summary>
        /// Creates a fully valid leveled haul grid for orientation tests.
        /// </summary>
        /// <param name="Width">Grid width in cells.</param>
        /// <param name="Height">Grid height in cells.</param>
        /// <returns>Leveled haul grid with south at row 0.</returns>
        private static HaulBuilder.Grid CreateLeveledHaulGrid
            (
            int Width,
            int Height
            )
        {
            bool[,] Valid = new bool[Height, Width];
            double[,] Existing = new double[Height, Width];
            double[,] Proposed = new double[Height, Width];
            for (Int32 Row = 0; Row < Height; Row++)
            {
                for (Int32 Col = 0; Col < Width; Col++)
                {
                    Valid[Row, Col] = true;
                    Existing[Row, Col] = 50.0;
                    Proposed[Row, Col] = 50.0;
                }
            }

            return new HaulBuilder.Grid(
                2.0,
                Width,
                Height,
                500000.0,
                4000000.0,
                15,
                true,
                36.0,
                -90.0,
                Existing,
                Proposed,
                Valid,
                1.0,
                1.0,
                50.0,
                new List<List<(double X, double Y)>>());
        }

        /// <summary>
        /// Builds a project document that satisfies <see cref="ProjectDocument.HasDesign"/>.
        /// </summary>
        /// <param name="Grid">Design grid to attach.</param>
        /// <returns>Project with design parameters and grid.</returns>
        private static ProjectDocument CreateProjectWithGrid
            (
            DesignGrid Grid
            )
        {
            Grid.UtmZone = 10;
            Grid.UtmNorth = true;
            return new ProjectDocument
            {
                DesignGrid = Grid,
                DesignParameters = new DesignParameters(),
            };
        }
    }
}
