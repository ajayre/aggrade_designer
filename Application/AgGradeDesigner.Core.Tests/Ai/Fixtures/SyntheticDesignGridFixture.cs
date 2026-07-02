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
                Width = 3,
                Height = 3,
                BinSizeM = FieldConstants.BIN_SIZE_M,
                OriginEastingM = 1000.0,
                OriginNorthingM = 2000.0,
                MinX = 1000.0,
                MinY = 2000.0,
                MaxX = 1000.0 + 3 * FieldConstants.BIN_SIZE_M,
                MaxY = 2000.0 + 3 * FieldConstants.BIN_SIZE_M,
                PlaneA = 0.0,
                PlaneB = 0.0,
                PlaneC = ProposedM,
                CompositeSlopeHeadingDeg = 90.0
            };

            for (int Y = 0; Y < 3; Y++)
            {
                for (int X = 0; X < 3; X++)
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
            DesignGrid Grid = CreateFlatGrid(100.0, 100.0);
            foreach (GridBinData Bin in Grid.Bins)
                Bin.ProposedM = 100.0 + Bin.X * 0.1;

            Grid.PlaneA = 0.1 / FieldConstants.BIN_SIZE_M;
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
                    MainSlopeDirectionDeg = 90.0,
                    MainSlopePercent = 0.0,
                    CrossSlopePercent = 0.0,
                    CutFillRatio = 1.2
                },
                Survey = new SurveyModel()
            };
        }
    }
}
