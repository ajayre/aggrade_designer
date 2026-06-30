using AgGradeDesigner.Core.Models;

namespace AgGradeDesigner.Core.Tests.Models
{
    /// <summary>
    /// Unit tests for <see cref="DesignParameters"/> cloning semantics.
    /// </summary>
    public sealed class DesignParametersTests
    {
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
                MainSlopeDirectionDeg = 45,
                MainSlopePercent = 0.2,
                CrossSlopePercent = 0.1,
                CutFillRatio = 1.5,
                ImportToFieldCy = 100,
                ExportFromFieldCy = 50
            };

            DesignParameters Copy = Original.Clone();
            Copy.MainSlopePercent = 0.0;

            // Mutating the clone must not alter the source parameter bag.
            Assert.Equal(0.2, Original.MainSlopePercent, 3);
        }
    }
}
