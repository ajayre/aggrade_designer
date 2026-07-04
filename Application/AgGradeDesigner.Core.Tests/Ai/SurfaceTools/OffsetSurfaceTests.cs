using AgGradeDesigner.Core.Ai;
using AgGradeDesigner.Core.Ai.SurfaceTools;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Tests.Ai.Fixtures;

namespace AgGradeDesigner.Core.Tests.Ai.SurfaceTools
{
    /// <summary>
    /// Unit tests for the <c>offset_surface</c> Phase 2 surface tool.
    /// </summary>
    public sealed class OffsetSurfaceTests
    {
        /// <summary>Existing elevation used by the fixture grid.</summary>
        private const double EXISTING_ELEVATION_M = 100.0;

        /// <summary>Initial proposed elevation used by the fixture grid.</summary>
        private const double PROPOSED_ELEVATION_M = 100.0;

        /// <summary>Vertical offset applied during tests in metres.</summary>
        private const double OFFSET_DELTA_M = 0.5;

        /// <summary>
        /// Verifies offset_surface raises every valid proposed bin in the preview.
        /// </summary>
        [Fact]
        public void OffsetSurface_RaisesAllValidBins
            (
            )
        {
            ProjectDocument Project = SyntheticDesignGridFixture.CreateProject(
                SyntheticDesignGridFixture.CreateFlatGrid(EXISTING_ELEVATION_M, PROPOSED_ELEVATION_M));
            SurfaceToolDispatcher Dispatcher = new SurfaceToolDispatcher(AiDeliveryPhase.Phase2);

            SurfaceToolResult Result = Dispatcher.Execute(
                Project,
                "offset_surface",
                $$"""{"delta_z": {{OFFSET_DELTA_M}}}""");

            Assert.True(Result.Success);
            Assert.Contains("\"status\":\"preview\"", Result.Json, StringComparison.Ordinal);

            foreach (GridBinData Bin in Project.DesignGrid!.Bins)
            {
                if (!Bin.Valid)
                    continue;

                Assert.Equal(PROPOSED_ELEVATION_M + OFFSET_DELTA_M, Bin.ProposedM, 6);
            }
        }
    }
}
