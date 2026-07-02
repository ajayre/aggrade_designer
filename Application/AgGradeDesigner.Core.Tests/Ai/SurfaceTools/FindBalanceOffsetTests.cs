using AgGradeDesigner.Core.Ai.SurfaceTools;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Tests.Ai.Fixtures;

namespace AgGradeDesigner.Core.Tests.Ai.SurfaceTools
{
    /// <summary>
    /// Unit tests for the <c>find_balance_offset</c> read-only surface tool.
    /// </summary>

    public sealed class FindBalanceOffsetTests
    {
        /// <summary>
        /// Existing elevation m used by this component.
        /// </summary>

        private const double EXISTING_ELEVATION_M = 100.0;
        /// <summary>
        /// Proposed elevation m used by this component.
        /// </summary>

        private const double PROPOSED_ELEVATION_M = 99.0;
        /// <summary>
        /// Cut fill ratio used by this component.
        /// </summary>

        private const double CUT_FILL_RATIO = 1.0;

        /// <summary>
        /// Verifies a positive offset is returned when proposed is uniformly lower than existing.
        /// </summary>
        [Fact]
        public void FindBalanceOffset_LowerProposed_ReturnsPositiveOffset
            (
            )
        {
            DesignGrid Grid = SyntheticDesignGridFixture.CreateFlatGrid(EXISTING_ELEVATION_M, PROPOSED_ELEVATION_M);
            double OffsetM = SurfaceVolumeCalculator.FindBalanceOffset(Grid, CUT_FILL_RATIO);

            Assert.True(OffsetM > 0.0);
        }
    }
}
