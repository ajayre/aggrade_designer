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

            Assert.Equal(9, Stats.ValidBinCount);
            Assert.Equal(100.1, Stats.MeanElevationM, 2);
            Assert.True(Stats.MaxSlopeMm >= Stats.MinSlopeMm);
            Assert.True(Stats.VolumeAboveReferenceM3 > 0.0);
            Assert.True(Stats.VolumeBelowReferenceM3 > 0.0);
        }
    }
}
