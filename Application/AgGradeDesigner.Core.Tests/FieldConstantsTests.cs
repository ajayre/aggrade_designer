using AgGradeDesigner.Core;

namespace AgGradeDesigner.Core.Tests
{
    /// <summary>
    /// Unit tests for <see cref="FieldConstants"/> compile-time design defaults.
    /// </summary>
    public sealed class FieldConstantsTests
    {
        /// <summary>
        /// Expected value used when asserting bin size m.
        /// </summary>

        private const double EXPECTED_BIN_SIZE_M = 0.6096;
        /// <summary>
        /// Bin size precision used by this component.
        /// </summary>

        private const int BIN_SIZE_PRECISION = 4;
        /// <summary>
        /// Expected value used when asserting cut fill ratio.
        /// </summary>

        private const double EXPECTED_CUT_FILL_RATIO = 1.2;
        /// <summary>
        /// Cut fill ratio precision used by this component.
        /// </summary>

        private const int CUT_FILL_RATIO_PRECISION = 3;
        /// <summary>
        /// Expected value used when asserting bin no data sentinel.
        /// </summary>

        private const int EXPECTED_BIN_NO_DATA_SENTINEL = -1000;

        /// <summary>
        /// Verifies the bin size constant matches the two-foot specification in metres.
        /// </summary>
        [Fact]
        public void BinSizeM_IsTwoFeetInMetres
            (
            )
        {
            // Assert against the canonical bin spacing used across grid math.
            Assert.Equal(EXPECTED_BIN_SIZE_M, FieldConstants.BIN_SIZE_M, BIN_SIZE_PRECISION);
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
            Assert.Equal(EXPECTED_CUT_FILL_RATIO, FieldConstants.CUT_FILL_RATIO, CUT_FILL_RATIO_PRECISION);
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
            Assert.Equal(EXPECTED_BIN_NO_DATA_SENTINEL, FieldConstants.BIN_NO_DATA_SENTINEL);
        }
    }
}
