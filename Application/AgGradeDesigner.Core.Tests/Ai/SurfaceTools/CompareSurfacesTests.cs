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
        /// Verifies compare statistics when proposed is uniformly higher than existing.
        /// </summary>
        [Fact]
        public void CompareSurfaces_RaisedProposed_CountsRaisedBins
            (
            )
        {
            DesignGrid Grid = SyntheticDesignGridFixture.CreateFlatGrid(100.0, 100.4);
            SurfaceCompareStats Stats = SurfaceVolumeCalculator.CompareSurfaces(
                Grid,
                SurfaceLayerKind.Existing,
                SurfaceLayerKind.Proposed);

            Assert.Equal(0.4, Stats.MinDeltaM, 3);
            Assert.Equal(0.4, Stats.MaxDeltaM, 3);
            Assert.Equal(9, Stats.RaisedBinCount);
            Assert.Equal(0, Stats.LoweredBinCount);
        }
    }
}
