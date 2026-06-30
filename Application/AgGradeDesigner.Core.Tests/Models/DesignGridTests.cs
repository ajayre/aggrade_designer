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
                    new GridBinData { X = 0, Y = 0, ExistingM = 100.5, Valid = true }
                }
            };

            // Populated coordinates return stored elevations; absent bins use the sentinel.
            Assert.Equal(100.5, Grid.GetExisting(0, 0), 3);
            Assert.Equal(FieldConstants.BIN_NO_DATA_SENTINEL, Grid.GetExisting(1, 0));
        }
    }
}
