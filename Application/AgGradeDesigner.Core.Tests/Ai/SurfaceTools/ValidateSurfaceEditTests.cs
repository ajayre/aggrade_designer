using AgGradeDesigner.Core.Ai;
using AgGradeDesigner.Core.Ai.SurfaceTools;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Tests.Ai.Fixtures;

namespace AgGradeDesigner.Core.Tests.Ai.SurfaceTools
{
    /// <summary>
    /// Unit tests for the <c>validate_surface_edit</c> Phase 2 surface tool.
    /// </summary>
    public sealed class ValidateSurfaceEditTests
    {
        /// <summary>Existing elevation used by the fixture grid.</summary>
        private const double EXISTING_ELEVATION_M = 100.0;

        /// <summary>Initial proposed elevation used by the fixture grid.</summary>
        private const double PROPOSED_ELEVATION_M = 100.0;

        /// <summary>Maximum elevation constraint violated by the preview offset.</summary>
        private const double MAX_ELEVATION_CONSTRAINT_M = 100.1;

        /// <summary>Vertical offset large enough to violate the elevation constraint.</summary>
        private const double OFFSET_DELTA_M = 0.5;

        /// <summary>
        /// Verifies validate_surface_edit returns a violation for an exceeded max elevation constraint.
        /// </summary>
        [Fact]
        public void ValidateSurfaceEdit_MaxElevationExceeded_ReturnsViolation
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
                "validate_surface_edit",
                "{\"constraints\":{\"max_elevation_m\":" + MAX_ELEVATION_CONSTRAINT_M.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}}");

            Assert.True(Result.Success);
            Assert.Contains("\"valid\":false", Result.Json, StringComparison.Ordinal);
            Assert.Contains("max_elevation_exceeded", Result.Json, StringComparison.Ordinal);
        }
    }
}
