using AgGradeDesigner.Core.Models;

namespace AgGradeDesigner.Core.Tests.Models
{
    /// <summary>
    /// Unit tests for <see cref="DesignParameters"/> cloning semantics.
    /// </summary>
    public sealed class DesignParametersTests
    {
        /// <summary>
        /// Main slope direction deg used by this component.
        /// </summary>

        private const double MAIN_SLOPE_DIRECTION_DEG = 45;
        /// <summary>
        /// Main slope percent used by this component.
        /// </summary>

        private const double MAIN_SLOPE_PERCENT = 0.2;
        /// <summary>
        /// Cross slope percent used by this component.
        /// </summary>

        private const double CROSS_SLOPE_PERCENT = 0.1;
        /// <summary>
        /// Cut fill ratio used by this component.
        /// </summary>

        private const double CUT_FILL_RATIO = 1.5;
        /// <summary>
        /// Import to field cy used by this component.
        /// </summary>

        private const int IMPORT_TO_FIELD_CY = 100;
        /// <summary>
        /// Export from field cy used by this component.
        /// </summary>

        private const int EXPORT_FROM_FIELD_CY = 50;
        /// <summary>
        /// Slope precision used by this component.
        /// </summary>

        private const int SLOPE_PRECISION = 3;

        /// <summary>
        /// Verifies <see cref="DesignParameters.Clone"/> produces an independent copy.
        /// </summary>
        [Fact]
        public void Clone_CreatesIndependentCopy
            (
            )
        {
            DesignParameters Original = new DesignParameters
            {
                MainSlopeDirectionDeg = MAIN_SLOPE_DIRECTION_DEG,
                MainSlopePercent = MAIN_SLOPE_PERCENT,
                CrossSlopePercent = CROSS_SLOPE_PERCENT,
                CutFillRatio = CUT_FILL_RATIO,
                ImportToFieldCy = IMPORT_TO_FIELD_CY,
                ExportFromFieldCy = EXPORT_FROM_FIELD_CY
            };

            DesignParameters Copy = Original.Clone();
            Copy.MainSlopePercent = 0.0;

            // Mutating the clone must not alter the source parameter bag.
            Assert.Equal(MAIN_SLOPE_PERCENT, Original.MainSlopePercent, SLOPE_PRECISION);
        }
    }
}
