using AgGradeDesigner.Core;
using AgGradeDesigner.Core.Models;

namespace AgGradeDesigner.Core.Tests.Models
{
    /// <summary>
    /// Unit tests for <see cref="DesignGrid"/> bin lookup behaviour.
    /// </summary>
    public sealed class DesignGridTests
    {
        /// <summary>
        /// Existing elevation m used by this component.
        /// </summary>

        private const double EXISTING_ELEVATION_M = 100.5;
        /// <summary>
        /// Elevation precision used by this component.
        /// </summary>

        private const int ELEVATION_PRECISION = 3;
        /// <summary>
        /// Other bin x used by this component.
        /// </summary>

        private const int OTHER_BIN_X = 1;

        /// <summary>
        /// Verifies missing bins return the no-data sentinel instead of zero.
        /// </summary>
        [Fact]
        public void GetExisting_ReturnsSentinelForMissingBin
            (
            )
        {
            DesignGrid Grid = new DesignGrid
            {
                Bins = new List<GridBinData>
                {
                    new GridBinData { X = 0, Y = 0, ExistingM = EXISTING_ELEVATION_M, Valid = true }
                }
            };

            // Populated coordinates return stored elevations; absent bins use the sentinel.
            Assert.Equal(EXISTING_ELEVATION_M, Grid.GetExisting(0, 0), ELEVATION_PRECISION);
            Assert.Equal(FieldConstants.BIN_NO_DATA_SENTINEL, Grid.GetExisting(OTHER_BIN_X, 0));
        }
    }
}
