using AgGradeDesigner.Core;
using AgGradeDesigner.Core.Ai.SurfaceTools;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Survey;
using AgGradeDesigner.Core.Tests.Ai.Fixtures;

namespace AgGradeDesigner.Core.Tests.Ai.SurfaceTools
{
    /// <summary>
    /// Unit tests for the <c>calculate_cut_fill</c> read-only surface tool.
    /// </summary>
    public sealed class CalculateCutFillTests
    {
        /// <summary>
        /// Verifies cut and fill volumes on a uniformly raised proposed surface.
        /// </summary>
        [Fact]
        public void CalculateCutFill_FlatRaise_ProducesFillOnly
            (
            )
        {
            DesignGrid Grid = SyntheticDesignGridFixture.CreateFlatGrid(100.0, 100.5);
            double BinArea = Grid.BinSizeM * Grid.BinSizeM;
            SurfaceCutFillVolumes Volumes = SurfaceVolumeCalculator.CalculateCutFill(
                Grid,
                SurfaceLayerKind.Existing,
                SurfaceLayerKind.Proposed);

            Assert.Equal(0.0, Volumes.CutM3, 6);
            Assert.Equal(9 * 0.5 * BinArea, Volumes.FillM3, 3);
        }
    }
}
