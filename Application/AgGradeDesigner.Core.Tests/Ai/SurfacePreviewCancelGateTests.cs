using AgGradeDesigner.Core.Ai;

namespace AgGradeDesigner.Core.Tests.Ai
{
    /// <summary>
    /// Unit tests for <see cref="SurfacePreviewCancelGate"/>.
    /// </summary>
    public sealed class SurfacePreviewCancelGateTests
    {
        /// <summary>
        /// Verifies explicit decline phrases are detected.
        /// </summary>
        /// <param name="Message">User chat message.</param>
        [Theory]
        [InlineData("don't commit")]
        [InlineData("No")]
        [InlineData("cancel")]
        [InlineData("never mind")]
        public void IsExplicitPreviewCancel_DeclinePhrases_ReturnsTrue
            (
            string Message
            )
        {
            Assert.True(SurfacePreviewCancelGate.IsExplicitPreviewCancel(Message));
        }

        /// <summary>
        /// Verifies unrelated field questions are not treated as preview cancel.
        /// </summary>
        [Fact]
        public void IsExplicitPreviewCancel_UnrelatedQuestion_ReturnsFalse
            (
            )
        {
            Assert.False(SurfacePreviewCancelGate.IsExplicitPreviewCancel("What is the field slope?"));
        }
    }
}
