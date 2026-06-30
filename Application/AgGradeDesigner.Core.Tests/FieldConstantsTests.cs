using AgGradeDesigner.Core;

namespace AgGradeDesigner.Core.Tests
{
    /// <summary>
    /// Unit tests for <see cref="FieldConstants"/> compile-time design defaults.
    /// </summary>
    public sealed class FieldConstantsTests
    {
        /// <summary>
        /// Verifies the bin size constant matches the two-foot specification in metres.
        /// </summary>
        [Fact]
        public void BinSizeM_IsTwoFeetInMetres
            (
            )
        {
            // Assert against the canonical bin spacing used across grid math.
            Assert.Equal(0.6096, FieldConstants.BIN_SIZE_M, 4);
        }

        /// <summary>
        /// Verifies the default cut-fill ratio matches product specification.
        /// </summary>
        [Fact]
        public void CutFillRatio_DefaultMatchesSpec
            (
            )
        {
            // Confirm the shipped default ratio has not drifted from 1.2.
            Assert.Equal(1.2, FieldConstants.CUT_FILL_RATIO, 3);
        }

        /// <summary>
        /// Verifies the no-data sentinel matches AgGrade legacy values.
        /// </summary>
        [Fact]
        public void BinNoDataSentinel_MatchesAgGrade
            (
            )
        {
            // Sentinel must stay aligned with AgGrade grid interchange semantics.
            Assert.Equal(-1000, FieldConstants.BIN_NO_DATA_SENTINEL);
        }
    }
}
