using AgGradeDesigner.Core;
using AgGradeDesigner.Core.Ai.SurfaceTools;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Tests.Ai.Fixtures;

namespace AgGradeDesigner.Core.Tests.Ai.SurfaceTools
{
    /// <summary>
    /// Unit tests for the <c>get_elevation_at</c> read-only surface tool.
    /// </summary>
    public sealed class GetElevationAtTests
    {
        /// <summary>
        /// Verifies bilinear sampling returns the flat proposed elevation at a bin centre.
        /// </summary>
        [Fact]
        public void TrySampleElevation_ReturnsFlatProposedElevation
            (
            )
        {
            DesignGrid Grid = SyntheticDesignGridFixture.CreateFlatGrid(99.0, 101.5);
            double Easting = Grid.OriginEastingM + 1.5 * Grid.BinSizeM;
            double Northing = Grid.OriginNorthingM + 1.5 * Grid.BinSizeM;

            bool Found = SurfaceGridSampler.TrySampleElevation(
                Grid,
                SurfaceLayerKind.Proposed,
                Easting,
                Northing,
                out double ElevationM);

            Assert.True(Found);
            Assert.Equal(101.5, ElevationM, 3);
        }

        /// <summary>
        /// Verifies sampling outside the grid returns false.
        /// </summary>
        [Fact]
        public void TrySampleElevation_ReturnsFalseOutsideGrid
            (
            )
        {
            DesignGrid Grid = SyntheticDesignGridFixture.CreateFlatGrid(99.0, 101.5);

            bool Found = SurfaceGridSampler.TrySampleElevation(
                Grid,
                SurfaceLayerKind.Proposed,
                0.0,
                0.0,
                out double ElevationM);

            Assert.False(Found);
            Assert.Equal(FieldConstants.BIN_NO_DATA_SENTINEL, ElevationM);
        }
    }
}
