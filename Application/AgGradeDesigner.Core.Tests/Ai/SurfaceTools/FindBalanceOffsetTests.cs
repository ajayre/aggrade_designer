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
        /// Verifies a positive offset is returned when proposed is uniformly lower than existing.
        /// </summary>
        [Fact]
        public void FindBalanceOffset_LowerProposed_ReturnsPositiveOffset
            (
            )
        {
            DesignGrid Grid = SyntheticDesignGridFixture.CreateFlatGrid(100.0, 99.0);
            double OffsetM = SurfaceVolumeCalculator.FindBalanceOffset(Grid, 1.0);

            Assert.True(OffsetM > 0.0);
        }
    }
}
