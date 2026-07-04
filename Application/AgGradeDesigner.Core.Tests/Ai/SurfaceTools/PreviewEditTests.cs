using AgGradeDesigner.Core.Ai;
using AgGradeDesigner.Core.Ai.SurfaceTools;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Tests.Ai.Fixtures;

namespace AgGradeDesigner.Core.Tests.Ai.SurfaceTools
{
    /// <summary>
    /// Unit tests for the <c>preview_edit</c> and <c>apply_edit_plan</c> planning tools.
    /// </summary>
    public sealed class PreviewEditTests
    {
        /// <summary>Existing elevation used by the fixture grid.</summary>
        private const double EXISTING_ELEVATION_M = 100.0;

        /// <summary>Initial proposed elevation used by the fixture grid.</summary>
        private const double PROPOSED_ELEVATION_M = 100.0;

        /// <summary>First offset applied by the edit plan in metres.</summary>
        private const double FIRST_OFFSET_M = 0.2;

        /// <summary>Second offset applied by the edit plan in metres.</summary>
        private const double SECOND_OFFSET_M = 0.1;

        /// <summary>
        /// Verifies preview_edit executes multiple steps and leaves a preview active.
        /// </summary>
        [Fact]
        public void PreviewEdit_MultiStepPlan_ExecutesAllSteps
            (
            )
        {
            ProjectDocument Project = SyntheticDesignGridFixture.CreateProject(
                SyntheticDesignGridFixture.CreateFlatGrid(EXISTING_ELEVATION_M, PROPOSED_ELEVATION_M));
            SurfaceToolDispatcher Dispatcher = new SurfaceToolDispatcher(AiDeliveryPhase.Phase2);

            SurfaceToolResult Result = Dispatcher.Execute(
                Project,
                "preview_edit",
                $$"""
                {
                  "edit_plan": {
                    "description": "Raise then raise again",
                    "steps": [
                      { "operation": "offset_surface", "delta_z": {{FIRST_OFFSET_M}} },
                      { "operation": "offset_surface", "delta_z": {{SECOND_OFFSET_M}} }
                    ]
                  }
                }
                """);

            Assert.True(Result.Success);
            Assert.Contains("step_count", Result.Json, StringComparison.Ordinal);
            Assert.True(Dispatcher.EditSession.PreviewActive);

            GridBinData Bin = Project.DesignGrid!.Bins.First(B => B.Valid);
            Assert.Equal(PROPOSED_ELEVATION_M + FIRST_OFFSET_M + SECOND_OFFSET_M, Bin.ProposedM, 6);
        }
    }
}
