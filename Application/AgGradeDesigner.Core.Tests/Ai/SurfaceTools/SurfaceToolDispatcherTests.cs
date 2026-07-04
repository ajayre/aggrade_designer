using AgGradeDesigner.Core.Ai;
using AgGradeDesigner.Core.Ai.SurfaceTools;
using AgGradeDesigner.Core.MapTools;
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

        /// <summary>
        /// Verifies get_surface_summary exposes mean local slope for general slope questions.
        /// </summary>
        [Fact]
        public void Execute_GetSurfaceSummary_IncludesMeanAndFieldSlopePercent
            (
            )
        {
            SurfaceToolDispatcher Dispatcher = new SurfaceToolDispatcher();
            SurfaceToolResult Result = Dispatcher.Execute(
                SyntheticDesignGridFixture.CreateProject(SyntheticDesignGridFixture.CreateSlopedGrid()),
                "get_surface_summary",
                "{}");

            Assert.True(Result.Success);
            Assert.Contains("mean_slope_percent", Result.Json, StringComparison.Ordinal);
            Assert.Contains("field_slope_percent", Result.Json, StringComparison.Ordinal);
        }

        /// <summary>
        /// Verifies get_tool_data executes through the dispatcher with an empty session.
        /// </summary>
        [Fact]
        public void Execute_GetToolData_ReturnsEmptyPayload
            (
            )
        {
            ToolDataSession Session = new ToolDataSession();
            SurfaceToolDispatcher Dispatcher = new SurfaceToolDispatcher(() => Session);
            SurfaceToolResult Result = Dispatcher.Execute(
                SyntheticDesignGridFixture.CreateProject(
                    SyntheticDesignGridFixture.CreateFlatGrid(EXISTING_ELEVATION_M, FLAT_PROPOSED_ELEVATION_M)),
                "get_tool_data",
                "{}");

            Assert.True(Result.Success);
            Assert.Contains("elevations_frozen_at_creation", Result.Json, StringComparison.Ordinal);
        }
    }
}
