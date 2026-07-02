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
        /// Verifies summary metrics on a sloped synthetic grid.
        /// </summary>
        [Fact]
        public void BuildSurfaceSummary_ReturnsExpectedExtentsAndElevations
            (
            )
        {
            SurfaceSummary Summary = SurfaceAnalysis.BuildSurfaceSummary(SyntheticDesignGridFixture.CreateSlopedGrid());

            Assert.Equal(FieldConstants.BIN_SIZE_M, Summary.CellSizeM, 6);
            Assert.Equal(100.0, Summary.MinElevationM, 3);
            Assert.Equal(100.2, Summary.MaxElevationM, 3);
            Assert.True(Summary.MaxSlopeMm > 0.0);
            Assert.Equal(1000.0 + 0.5 * FieldConstants.BIN_SIZE_M, Summary.LowPointEastingM, 3);
        }
    }
}
