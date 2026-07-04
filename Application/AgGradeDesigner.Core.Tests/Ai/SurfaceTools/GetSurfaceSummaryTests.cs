using AgGradeDesigner.Core;
using AgGradeDesigner.Core.Ai.SurfaceTools;
using AgGradeDesigner.Core.Tests.Ai.Fixtures;

namespace AgGradeDesigner.Core.Tests.Ai.SurfaceTools
{
    /// <summary>
    /// Unit tests for the <c>get_surface_summary</c> read-only surface tool.
    /// </summary>

    public sealed class GetSurfaceSummaryTests
    {
        /// <summary>
        /// Origin easting m used by this component.
        /// </summary>

        private const double ORIGIN_EASTING_M = 1000.0;
        /// <summary>
        /// Bin centre offset used by this component.
        /// </summary>

        private const double BIN_CENTRE_OFFSET = 0.5;
        /// <summary>
        /// Min elevation m used by this component.
        /// </summary>

        private const double MIN_ELEVATION_M = 100.0;
        /// <summary>
        /// Max elevation m used by this component.
        /// </summary>

        private const double MAX_ELEVATION_M = 100.2;
        /// <summary>
        /// Elevation precision used by this component.
        /// </summary>

        private const int ELEVATION_PRECISION = 3;
        /// <summary>
        /// Coord precision used by this component.
        /// </summary>

        private const int COORD_PRECISION = 3;
        /// <summary>
        /// Cell size precision used by this component.
        /// </summary>

        private const int CELL_SIZE_PRECISION = 6;

        /// <summary>
        /// Verifies summary metrics on a sloped synthetic grid.
        /// </summary>
        [Fact]
        public void BuildSurfaceSummary_ReturnsExpectedExtentsAndElevations
            (
            )
        {
            SurfaceSummary Summary = SurfaceAnalysis.BuildSurfaceSummary(SyntheticDesignGridFixture.CreateSlopedGrid());

            Assert.Equal(FieldConstants.BIN_SIZE_M, Summary.CellSizeM, CELL_SIZE_PRECISION);
            Assert.Equal(MIN_ELEVATION_M, Summary.MinElevationM, ELEVATION_PRECISION);
            Assert.Equal(MAX_ELEVATION_M, Summary.MaxElevationM, ELEVATION_PRECISION);
            Assert.True(Summary.MaxSlopeMm > 0.0);
            Assert.True(Summary.MeanSlopeMm > 0.0);
            Assert.True(Summary.MeanSlopeMm <= Summary.MaxSlopeMm);
            Assert.Equal(ORIGIN_EASTING_M + BIN_CENTRE_OFFSET * FieldConstants.BIN_SIZE_M, Summary.LowPointEastingM, COORD_PRECISION);
        }
    }
}
