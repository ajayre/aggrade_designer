using AgGradeDesigner.Core.Ai;
using AgGradeDesigner.Core.Ai.SurfaceTools;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Tests.Ai.Fixtures;

namespace AgGradeDesigner.Core.Tests.Ai.SurfaceTools
{
    /// <summary>
    /// Unit tests for the <c>rebalance_surface</c> Phase 2 surface tool.
    /// </summary>
    public sealed class RebalanceSurfaceTests
    {
        /// <summary>Existing elevation used by the fixture grid.</summary>
        private const double EXISTING_ELEVATION_M = 100.0;

        /// <summary>Lower proposed elevation used to require a positive rebalance offset.</summary>
        private const double LOWER_PROPOSED_ELEVATION_M = 99.0;

        /// <summary>Target cut/fill ratio used during rebalance.</summary>
        private const double TARGET_CUT_FILL_RATIO = 1.0;

        /// <summary>
        /// Verifies rebalance_surface returns a preview and changes proposed elevations.
        /// </summary>
        [Fact]
        public void RebalanceSurface_LowerProposed_ReturnsPreview
            (
            )
        {
            ProjectDocument Project = SyntheticDesignGridFixture.CreateProject(
                SyntheticDesignGridFixture.CreateFlatGrid(EXISTING_ELEVATION_M, LOWER_PROPOSED_ELEVATION_M));
            SurfaceToolDispatcher Dispatcher = new SurfaceToolDispatcher(AiDeliveryPhase.Phase2);

            SurfaceToolResult Result = Dispatcher.Execute(
                Project,
                "rebalance_surface",
                $$"""{"target_cut_fill_ratio": {{TARGET_CUT_FILL_RATIO}}}""");

            Assert.True(Result.Success);
            Assert.Contains("applied_offset_m", Result.Json, StringComparison.Ordinal);
            Assert.True(Project.DesignGrid!.Bins.All(B => !B.Valid || B.ProposedM > LOWER_PROPOSED_ELEVATION_M));
        }
    }
}
