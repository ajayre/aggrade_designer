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
        /// Verifies Phase 2 tool names return a clear not-available error.
        /// </summary>
        [Fact]
        public void Execute_UnknownPhase2Tool_ReturnsNotAvailableError
            (
            )
        {
            SurfaceToolDispatcher Dispatcher = new SurfaceToolDispatcher();
            SurfaceToolResult Result = Dispatcher.Execute(
                SyntheticDesignGridFixture.CreateProject(SyntheticDesignGridFixture.CreateFlatGrid(100.0, 100.0)),
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
                SyntheticDesignGridFixture.CreateProject(SyntheticDesignGridFixture.CreateFlatGrid(100.0, 100.2)),
                "calculate_cut_fill",
                "{}");

            Assert.True(Result.Success);
            Assert.Contains("fill_m3", Result.Json, StringComparison.Ordinal);
        }
    }
}
