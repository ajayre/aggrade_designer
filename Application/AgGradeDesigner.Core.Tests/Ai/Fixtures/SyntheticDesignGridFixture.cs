using AgGradeDesigner.Core;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Survey;

namespace AgGradeDesigner.Core.Tests.Ai.Fixtures
{
    /// <summary>
    /// Builds small deterministic design grids for surface tool unit tests.
    /// </summary>

    static public class SyntheticDesignGridFixture
    {
        /// <summary>
        /// Grid width used by this component.
        /// </summary>

        private const int GRID_WIDTH = 3;
        /// <summary>
        /// Grid height used by this component.
        /// </summary>

        private const int GRID_HEIGHT = 3;
        /// <summary>
        /// Origin easting m used by this component.
        /// </summary>

        private const double ORIGIN_EASTING_M = 1000.0;
        /// <summary>
        /// Origin northing m used by this component.
        /// </summary>

        private const double ORIGIN_NORTHING_M = 2000.0;
        /// <summary>
        /// Flat existing elevation m used by this component.
        /// </summary>

        private const double FLAT_EXISTING_ELEVATION_M = 100.0;
        /// <summary>
        /// Slope increment m per bin used by this component.
        /// </summary>

        private const double SLOPE_INCREMENT_M_PER_BIN = 0.1;
        /// <summary>
        /// Main slope direction deg used by this component.
        /// </summary>

        private const double MAIN_SLOPE_DIRECTION_DEG = 90.0;
        /// <summary>
        /// Zero slope percent used by this component.
        /// </summary>

        private const double ZERO_SLOPE_PERCENT = 0.0;
        /// <summary>
        /// Default cut fill ratio used by this component.
        /// </summary>

        private const double DEFAULT_CUT_FILL_RATIO = FieldConstants.CUT_FILL_RATIO;

        /// <summary>
        /// Creates a flat 3×3 valid grid with configurable existing and proposed elevations.
        /// </summary>
        /// <param name="ExistingM">Existing elevation for every valid bin.</param>
        /// <param name="ProposedM">Proposed elevation for every valid bin.</param>
        /// <returns>Design grid with UTM origin at (1000, 2000).</returns>
        static public DesignGrid CreateFlatGrid
            (
            double ExistingM,
            double ProposedM
            )
        {
            DesignGrid Grid = new DesignGrid
            {
                Width = GRID_WIDTH,
                Height = GRID_HEIGHT,
                BinSizeM = FieldConstants.BIN_SIZE_M,
                OriginEastingM = ORIGIN_EASTING_M,
                OriginNorthingM = ORIGIN_NORTHING_M,
                MinX = ORIGIN_EASTING_M,
                MinY = ORIGIN_NORTHING_M,
                MaxX = ORIGIN_EASTING_M + GRID_WIDTH * FieldConstants.BIN_SIZE_M,
                MaxY = ORIGIN_NORTHING_M + GRID_HEIGHT * FieldConstants.BIN_SIZE_M,
                PlaneA = 0.0,
                PlaneB = 0.0,
                PlaneC = ProposedM,
                CompositeSlopeHeadingDeg = MAIN_SLOPE_DIRECTION_DEG
            };

            for (int Y = 0; Y < GRID_HEIGHT; Y++)
            {
                for (int X = 0; X < GRID_WIDTH; X++)
                {
                    Grid.Bins.Add(new GridBinData
                    {
                        X = X,
                        Y = Y,
                        ExistingM = ExistingM,
                        ProposedM = ProposedM,
                        Valid = true
                    });
                }
            }

            return Grid;
        }

        /// <summary>
        /// Creates a sloped proposed surface rising 0.1 m per bin column.
        /// </summary>
        /// <returns>Design grid for slope and summary tests.</returns>
        static public DesignGrid CreateSlopedGrid
            (
            )
        {
            DesignGrid Grid = CreateFlatGrid(FLAT_EXISTING_ELEVATION_M, FLAT_EXISTING_ELEVATION_M);
            foreach (GridBinData Bin in Grid.Bins)
                Bin.ProposedM = FLAT_EXISTING_ELEVATION_M + Bin.X * SLOPE_INCREMENT_M_PER_BIN;

            Grid.PlaneA = SLOPE_INCREMENT_M_PER_BIN / FieldConstants.BIN_SIZE_M;
            return Grid;
        }

        /// <summary>
        /// Wraps a design grid in a project document with design parameters.
        /// </summary>
        /// <param name="Grid">Design grid.</param>
        /// <returns>Project document marked as having design data.</returns>
        static public ProjectDocument CreateProject
            (
            DesignGrid Grid
            )
        {
            return new ProjectDocument
            {
                DesignGrid = Grid,
                DesignParameters = new DesignParameters
                {
                    MainSlopeDirectionDeg = MAIN_SLOPE_DIRECTION_DEG,
                    MainSlopePercent = ZERO_SLOPE_PERCENT,
                    CrossSlopePercent = ZERO_SLOPE_PERCENT,
                    CutFillRatio = DEFAULT_CUT_FILL_RATIO
                },
                Survey = new SurveyModel()
            };
        }
    }
}
