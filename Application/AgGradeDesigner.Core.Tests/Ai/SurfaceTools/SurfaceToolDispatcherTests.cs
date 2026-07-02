using AgGradeDesigner.Core.Ai;
using AgGradeDesigner.Core.Ai.SurfaceTools;
using AgGradeDesigner.Core.Tests.Ai.Fixtures;

namespace AgGradeDesigner.Core.Tests.Ai.SurfaceTools
{
    /// <summary>
    /// Unit tests for <see cref="SurfaceToolDispatcher"/>.
    /// </summary>
    public sealed class SurfaceToolDispatcherTests
    {
        /// <summary>
        /// Existing elevation m used by this component.
        /// </summary>

        private const double EXISTING_ELEVATION_M = 100.0;
        /// <summary>
        /// Flat proposed elevation m used by this component.
        /// </summary>

        private const double FLAT_PROPOSED_ELEVATION_M = 100.0;
        /// <summary>
        /// Raised proposed elevation m used by this component.
        /// </summary>

        private const double RAISED_PROPOSED_ELEVATION_M = 100.2;

        /// <summary>
        /// Verifies Phase 2 tool names return a clear not-available error.
        /// </summary>
        [Fact]
        public void Execute_UnknownPhase2Tool_ReturnsNotAvailableError
            (
            )
        {
            SurfaceToolDispatcher Dispatcher = new SurfaceToolDispatcher();
            SurfaceToolResult Result = Dispatcher.Execute(
                SyntheticDesignGridFixture.CreateProject(
                    SyntheticDesignGridFixture.CreateFlatGrid(EXISTING_ELEVATION_M, FLAT_PROPOSED_ELEVATION_M)),
                "offset_surface",
                "{}");

            Assert.False(Result.Success);
            Assert.Contains("not available", Result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Verifies calculate_cut_fill executes through the dispatcher on a loaded project.
        /// </summary>
        [Fact]
        public void Execute_CalculateCutFill_ReturnsVolumes
            (
            )
        {
            SurfaceToolDispatcher Dispatcher = new SurfaceToolDispatcher();
            SurfaceToolResult Result = Dispatcher.Execute(
                SyntheticDesignGridFixture.CreateProject(
                    SyntheticDesignGridFixture.CreateFlatGrid(EXISTING_ELEVATION_M, RAISED_PROPOSED_ELEVATION_M)),
                "calculate_cut_fill",
                "{}");

            Assert.True(Result.Success);
            Assert.Contains("fill_m3", Result.Json, StringComparison.Ordinal);
        }
    }
}
