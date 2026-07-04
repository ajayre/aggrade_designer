using AgGradeDesigner.Core.Ai;
using AgGradeDesigner.Core.Ai.SurfaceTools;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Tests.Ai.Fixtures;

namespace AgGradeDesigner.Core.Tests.Ai.SurfaceTools
{
    /// <summary>
    /// Unit tests for the <c>calculate_edit_delta</c> Phase 2 surface tool.
    /// </summary>
    public sealed class CalculateEditDeltaTests
    {
        /// <summary>Existing elevation used by the fixture grid.</summary>
        private const double EXISTING_ELEVATION_M = 100.0;

        /// <summary>Initial proposed elevation used by the fixture grid.</summary>
        private const double PROPOSED_ELEVATION_M = 100.0;

        /// <summary>Vertical offset applied before calculating edit delta.</summary>
        private const double OFFSET_DELTA_M = 0.4;

        /// <summary>
        /// Verifies calculate_edit_delta reports fill when preview proposed is raised above committed proposed.
        /// </summary>
        [Fact]
        public void CalculateEditDelta_AfterPreview_ReportsFill
            (
            )
        {
            ProjectDocument Project = SyntheticDesignGridFixture.CreateProject(
                SyntheticDesignGridFixture.CreateFlatGrid(EXISTING_ELEVATION_M, PROPOSED_ELEVATION_M));
            SurfaceToolDispatcher Dispatcher = new SurfaceToolDispatcher(AiDeliveryPhase.Phase2);

            Assert.True(Dispatcher.Execute(
                Project,
                "offset_surface",
                $$"""{"delta_z": {{OFFSET_DELTA_M}}}""").Success);

            SurfaceToolResult Result = Dispatcher.Execute(
                Project,
                "calculate_edit_delta",
                """{"before_surface":"proposed","after_surface":"proposed"}""");

            Assert.True(Result.Success);
            Assert.Contains("fill_m3", Result.Json, StringComparison.Ordinal);
        }
    }
}
