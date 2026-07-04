using AgGradeDesigner.Core.Ai;
using AgGradeDesigner.Core.Ai.SurfaceTools;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Tests.Ai.Fixtures;

namespace AgGradeDesigner.Core.Tests.Ai.SurfaceTools
{
    /// <summary>
    /// Unit tests for the <c>apply_edit_plan</c> Phase 2 planning tool.
    /// </summary>
    public sealed class ApplyEditPlanTests
    {
        /// <summary>Existing elevation used by the fixture grid.</summary>
        private const double EXISTING_ELEVATION_M = 100.0;

        /// <summary>Initial proposed elevation used by the fixture grid.</summary>
        private const double PROPOSED_ELEVATION_M = 100.0;

        /// <summary>Vertical offset applied by the edit plan in metres.</summary>
        private const double OFFSET_DELTA_M = 0.15;

        /// <summary>
        /// Verifies apply_edit_plan previews an edit plan without committing it.
        /// </summary>
        [Fact]
        public void ApplyEditPlan_DoesNotCommitUntilExplicitCommit
            (
            )
        {
            ProjectDocument Project = SyntheticDesignGridFixture.CreateProject(
                SyntheticDesignGridFixture.CreateFlatGrid(EXISTING_ELEVATION_M, PROPOSED_ELEVATION_M));
            SurfaceToolDispatcher Dispatcher = new SurfaceToolDispatcher(AiDeliveryPhase.Phase2);

            SurfaceToolResult Result = Dispatcher.Execute(
                Project,
                "apply_edit_plan",
                $$"""
                {
                  "edit_plan": {
                    "description": "Small raise",
                    "steps": [ { "operation": "offset_surface", "delta_z": {{OFFSET_DELTA_M}} } ]
                  }
                }
                """);

            Assert.True(Result.Success);
            Assert.True(Dispatcher.EditSession.PreviewActive);
            Assert.False(Project.IsModified);
        }
    }
}
