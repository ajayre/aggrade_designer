using AgGradeDesigner.Core.MapTools;

namespace AgGradeDesigner.Core.Tests.MapTools
{
    /// <summary>
    /// Unit tests for erase rectangle normalization (TOOLS-SPEC §3.3).
    /// </summary>
    public sealed class ToolScreenRectTests
    {
        /// <summary>
        /// Verifies a pure vertical drag produces a rectangle with minimum width.
        /// </summary>
        [Fact]
        public void FromDrag_VerticalDrag_EnforcesMinimumWidth
            (
            )
        {
            ToolScreenRect Rect = ToolScreenRect.FromDrag(50f, 80f, 50f, 20f);

            Assert.True((Rect.Right - Rect.Left) >= ToolDataConstants.ERASE_RECT_MIN_DIMENSION_PX);
            Assert.True(Rect.ContainsScreenPoint(50f, 50f));
        }

        /// <summary>
        /// Verifies a pure horizontal drag produces a rectangle with minimum height.
        /// </summary>
        [Fact]
        public void FromDrag_HorizontalDrag_EnforcesMinimumHeight
            (
            )
        {
            ToolScreenRect Rect = ToolScreenRect.FromDrag(10f, 50f, 90f, 50f);

            Assert.True((Rect.Bottom - Rect.Top) >= ToolDataConstants.ERASE_RECT_MIN_DIMENSION_PX);
            Assert.True(Rect.ContainsScreenPoint(50f, 50f));
        }
    }
}
