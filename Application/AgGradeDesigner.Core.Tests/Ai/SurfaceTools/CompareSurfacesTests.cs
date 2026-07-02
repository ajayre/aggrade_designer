using AgGradeDesigner.Core.Ai.SurfaceTools;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Tests.Ai.Fixtures;

namespace AgGradeDesigner.Core.Tests.Ai.SurfaceTools
{
    /// <summary>
    /// Unit tests for the <c>compare_surfaces</c> read-only surface tool.
    /// </summary>

    public sealed class CompareSurfacesTests
    {
        /// <summary>
        /// Existing elevation m used by this component.
        /// </summary>

        private const double EXISTING_ELEVATION_M = 100.0;
        /// <summary>
        /// Proposed elevation m used by this component.
        /// </summary>

        private const double PROPOSED_ELEVATION_M = 100.4;
        /// <summary>
        /// Surface delta m used by this component.
        /// </summary>

        private const double SURFACE_DELTA_M = 0.4;
        /// <summary>
        /// Valid bin count used by this component.
        /// </summary>

        private const int VALID_BIN_COUNT = 9;
        /// <summary>
        /// Delta precision used by this component.
        /// </summary>

        private const int DELTA_PRECISION = 3;

        /// <summary>
        /// Verifies compare statistics when proposed is uniformly higher than existing.
        /// </summary>
        [Fact]
        public void CompareSurfaces_RaisedProposed_CountsRaisedBins
            (
            )
        {
            DesignGrid Grid = SyntheticDesignGridFixture.CreateFlatGrid(EXISTING_ELEVATION_M, PROPOSED_ELEVATION_M);
            SurfaceCompareStats Stats = SurfaceVolumeCalculator.CompareSurfaces(
                Grid,
                SurfaceLayerKind.Existing,
                SurfaceLayerKind.Proposed);

            Assert.Equal(SURFACE_DELTA_M, Stats.MinDeltaM, DELTA_PRECISION);
            Assert.Equal(SURFACE_DELTA_M, Stats.MaxDeltaM, DELTA_PRECISION);
            Assert.Equal(VALID_BIN_COUNT, Stats.RaisedBinCount);
            Assert.Equal(0, Stats.LoweredBinCount);
        }
    }
}
