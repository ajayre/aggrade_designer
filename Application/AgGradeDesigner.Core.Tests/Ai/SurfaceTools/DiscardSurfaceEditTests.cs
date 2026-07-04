using AgGradeDesigner.Core.Ai;
using AgGradeDesigner.Core.Ai.SurfaceTools;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Tests.Ai.Fixtures;

namespace AgGradeDesigner.Core.Tests.Ai.SurfaceTools
{
    /// <summary>
    /// Unit tests for the <c>discard_surface_edit</c> Phase 2 lifecycle tool.
    /// </summary>
    public sealed class DiscardSurfaceEditTests
    {
        /// <summary>Existing elevation used by the fixture grid.</summary>
        private const double EXISTING_ELEVATION_M = 100.0;

        /// <summary>Initial proposed elevation used by the fixture grid.</summary>
        private const double PROPOSED_ELEVATION_M = 100.0;

        /// <summary>Vertical offset applied during preview.</summary>
        private const double OFFSET_DELTA_M = 0.5;

        /// <summary>
        /// Verifies discard_surface_edit restores the original proposed elevations after a preview.
        /// </summary>
        [Fact]
        public void DiscardSurfaceEdit_AfterPreview_RestoresOriginalProposed
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

            SurfaceToolResult Result = Dispatcher.Execute(Project, "discard_surface_edit", "{}");
            Assert.True(Result.Success);
            Assert.False(Dispatcher.EditSession.PreviewActive);

            foreach (GridBinData Bin in Project.DesignGrid!.Bins)
            {
                if (!Bin.Valid)
                    continue;

                Assert.Equal(PROPOSED_ELEVATION_M, Bin.ProposedM, 6);
            }
        }
    }
}
