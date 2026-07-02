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
        /// Existing elevation m used by this component.
        /// </summary>

        private const double EXISTING_ELEVATION_M = 99.0;
        /// <summary>
        /// Proposed elevation m used by this component.
        /// </summary>

        private const double PROPOSED_ELEVATION_M = 101.5;
        /// <summary>
        /// Bin centre offset used by this component.
        /// </summary>

        private const double BIN_CENTRE_OFFSET = 1.5;
        /// <summary>
        /// Outside easting m used by this component.
        /// </summary>

        private const double OUTSIDE_EASTING_M = 0.0;
        /// <summary>
        /// Outside northing m used by this component.
        /// </summary>

        private const double OUTSIDE_NORTHING_M = 0.0;
        /// <summary>
        /// Elevation precision used by this component.
        /// </summary>

        private const int ELEVATION_PRECISION = 3;

        /// <summary>
        /// Verifies bilinear sampling returns the flat proposed elevation at a bin centre.
        /// </summary>
        [Fact]
        public void TrySampleElevation_ReturnsFlatProposedElevation
            (
            )
        {
            DesignGrid Grid = SyntheticDesignGridFixture.CreateFlatGrid(EXISTING_ELEVATION_M, PROPOSED_ELEVATION_M);
            double Easting = Grid.OriginEastingM + BIN_CENTRE_OFFSET * Grid.BinSizeM;
            double Northing = Grid.OriginNorthingM + BIN_CENTRE_OFFSET * Grid.BinSizeM;

            bool Found = SurfaceGridSampler.TrySampleElevation(
                Grid,
                SurfaceLayerKind.Proposed,
                Easting,
                Northing,
                out double ElevationM);

            Assert.True(Found);
            Assert.Equal(PROPOSED_ELEVATION_M, ElevationM, ELEVATION_PRECISION);
        }

        /// <summary>
        /// Verifies sampling outside the grid returns false.
        /// </summary>
        [Fact]
        public void TrySampleElevation_ReturnsFalseOutsideGrid
            (
            )
        {
            DesignGrid Grid = SyntheticDesignGridFixture.CreateFlatGrid(EXISTING_ELEVATION_M, PROPOSED_ELEVATION_M);

            bool Found = SurfaceGridSampler.TrySampleElevation(
                Grid,
                SurfaceLayerKind.Proposed,
                OUTSIDE_EASTING_M,
                OUTSIDE_NORTHING_M,
                out double ElevationM);

            Assert.False(Found);
            Assert.Equal(FieldConstants.BIN_NO_DATA_SENTINEL, ElevationM);
        }
    }
}
