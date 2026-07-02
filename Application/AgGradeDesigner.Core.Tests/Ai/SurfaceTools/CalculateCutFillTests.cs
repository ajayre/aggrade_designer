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
        /// Existing elevation m used by this component.
        /// </summary>

        private const double EXISTING_ELEVATION_M = 100.0;
        /// <summary>
        /// Proposed elevation m used by this component.
        /// </summary>

        private const double PROPOSED_ELEVATION_M = 100.5;
        /// <summary>
        /// Fill height delta m used by this component.
        /// </summary>

        private const double FILL_HEIGHT_DELTA_M = 0.5;
        /// <summary>
        /// Valid bin count used by this component.
        /// </summary>

        private const int VALID_BIN_COUNT = 9;
        /// <summary>
        /// Volume precision used by this component.
        /// </summary>

        private const int VOLUME_PRECISION = 6;
        /// <summary>
        /// Fill volume precision used by this component.
        /// </summary>

        private const int FILL_VOLUME_PRECISION = 3;

        /// <summary>
        /// Verifies cut and fill volumes on a uniformly raised proposed surface.
        /// </summary>
        [Fact]
        public void CalculateCutFill_FlatRaise_ProducesFillOnly
            (
            )
        {
            DesignGrid Grid = SyntheticDesignGridFixture.CreateFlatGrid(EXISTING_ELEVATION_M, PROPOSED_ELEVATION_M);
            double BinArea = Grid.BinSizeM * Grid.BinSizeM;
            SurfaceCutFillVolumes Volumes = SurfaceVolumeCalculator.CalculateCutFill(
                Grid,
                SurfaceLayerKind.Existing,
                SurfaceLayerKind.Proposed);

            Assert.Equal(0.0, Volumes.CutM3, VOLUME_PRECISION);
            Assert.Equal(VALID_BIN_COUNT * FILL_HEIGHT_DELTA_M * BinArea, Volumes.FillM3, FILL_VOLUME_PRECISION);
        }
    }
}
