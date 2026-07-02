using AgGradeDesigner.Core.Ai.SurfaceTools;
using AgGradeDesigner.Core.Tests.Ai.Fixtures;

namespace AgGradeDesigner.Core.Tests.Ai.SurfaceTools
{
    /// <summary>
    /// Unit tests for the <c>get_region_stats</c> read-only surface tool.
    /// </summary>

    public sealed class GetRegionStatsTests
    {
        /// <summary>
        /// Valid bin count used by this component.
        /// </summary>

        private const int VALID_BIN_COUNT = 9;
        /// <summary>
        /// Mean elevation m used by this component.
        /// </summary>

        private const double MEAN_ELEVATION_M = 100.1;
        /// <summary>
        /// Mean elevation precision used by this component.
        /// </summary>

        private const int MEAN_ELEVATION_PRECISION = 2;

        /// <summary>
        /// Verifies field-wide statistics on a sloped synthetic grid.
        /// </summary>
        [Fact]
        public void BuildRegionStats_ReturnsExpectedMeanAndCounts
            (
            )
        {
            SurfaceRegionStats Stats = SurfaceAnalysis.BuildRegionStats(
                SyntheticDesignGridFixture.CreateSlopedGrid(),
                SurfaceLayerKind.Proposed);

            Assert.Equal(VALID_BIN_COUNT, Stats.ValidBinCount);
            Assert.Equal(MEAN_ELEVATION_M, Stats.MeanElevationM, MEAN_ELEVATION_PRECISION);
            Assert.True(Stats.MaxSlopeMm >= Stats.MinSlopeMm);
            Assert.True(Stats.VolumeAboveReferenceM3 > 0.0);
            Assert.True(Stats.VolumeBelowReferenceM3 > 0.0);
        }
    }
}
