using AgGradeDesigner.Core.Ai;
using AgGradeDesigner.Core.Ai.SurfaceTools;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Tests.Ai.Fixtures;

namespace AgGradeDesigner.Core.Tests.Ai.SurfaceTools
{
    /// <summary>
    /// Integration-style tests for preview and commit lifecycle tools.
    /// </summary>
    public sealed class CommitSurfaceEditTests
    {
        /// <summary>Existing elevation used by the fixture grid.</summary>
        private const double EXISTING_ELEVATION_M = 100.0;

        /// <summary>Initial proposed elevation used by the fixture grid.</summary>
        private const double PROPOSED_ELEVATION_M = 100.0;

        /// <summary>Vertical offset applied before commit.</summary>
        private const double OFFSET_DELTA_M = 0.25;

        /// <summary>
        /// Verifies preview does not mark the project modified until commit_surface_edit runs.
        /// </summary>
        [Fact]
        public void PreviewEdit_DoesNotMarkProjectModifiedUntilCommit
            (
            )
        {
            ProjectDocument Project = SyntheticDesignGridFixture.CreateProject(
                SyntheticDesignGridFixture.CreateFlatGrid(EXISTING_ELEVATION_M, PROPOSED_ELEVATION_M));
            SurfaceToolDispatcher Dispatcher = new SurfaceToolDispatcher(AiDeliveryPhase.Phase2);

            Assert.True(Dispatcher.Execute(
                Project,
                "preview_edit",
                $$"""
                {
                  "edit_plan": {
                    "description": "Raise field",
                    "steps": [ { "operation": "offset_surface", "delta_z": {{OFFSET_DELTA_M}} } ]
                  }
                }
                """).Success);
            Assert.False(Project.IsModified);

            SurfaceToolResult CommitResult = Dispatcher.Execute(
                Project,
                "commit_surface_edit",
                """{"description":"Approved in chat"}""");
            Assert.True(CommitResult.Success);
            Assert.True(Project.IsModified);
            Assert.False(Dispatcher.EditSession.PreviewActive);
        }

        /// <summary>
        /// Verifies commit_surface_edit errors when no preview is active.
        /// </summary>
        [Fact]
        public void CommitSurfaceEdit_WithoutPreview_ReturnsError
            (
            )
        {
            ProjectDocument Project = SyntheticDesignGridFixture.CreateProject(
                SyntheticDesignGridFixture.CreateFlatGrid(EXISTING_ELEVATION_M, PROPOSED_ELEVATION_M));
            SurfaceToolDispatcher Dispatcher = new SurfaceToolDispatcher(AiDeliveryPhase.Phase2);

            SurfaceToolResult Result = Dispatcher.Execute(Project, "commit_surface_edit", "{}");
            Assert.False(Result.Success);
            Assert.Contains("No preview active", Result.ErrorMessage, StringComparison.Ordinal);
        }
    }
}
