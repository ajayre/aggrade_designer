using AgGradeDesigner.Core;
using AgGradeDesigner.Core.Ai.SurfaceTools;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Survey;
using AgGradeDesigner.Core.Tests.Ai.Fixtures;

namespace AgGradeDesigner.Core.Tests.Ai.SurfaceTools
{
    /// <summary>
    /// Unit tests for the <c>get_field_geometry</c> read-only surface tool.
    /// </summary>
    public sealed class GetFieldGeometryTests
    {
        /// <summary>Grid width used for geometry tests.</summary>
        private const int GRID_WIDTH = 4;

        /// <summary>Grid height used for geometry tests.</summary>
        private const int GRID_HEIGHT = 3;

        /// <summary>
        /// Verifies grid-only projects fall back to a rectangular boundary ring.
        /// </summary>
        [Fact]
        public void GetFieldGeometry_WithoutSurveyBoundary_UsesGridExtent
            (
            )
        {
            ProjectDocument Project = CreateProjectWithoutBoundary();
            SurfaceToolDispatcher Dispatcher = new SurfaceToolDispatcher();
            SurfaceToolResult Result = Dispatcher.Execute(Project, "get_field_geometry", "{}");

            Assert.True(Result.Success, Result.ErrorMessage);
            Assert.Contains("boundary_vertices", Result.Json, StringComparison.Ordinal);
            Assert.Contains("\"grid\"", Result.Json, StringComparison.Ordinal);
            Assert.Contains("geometry_planning_note", Result.Json, StringComparison.Ordinal);
            Assert.DoesNotContain("edge_centerlines", Result.Json, StringComparison.Ordinal);
        }

        /// <summary>
        /// Verifies survey boundary points are returned as an ordered UTM ring.
        /// </summary>
        [Fact]
        public void GetFieldGeometry_WithSurveyBoundary_ReturnsSurveyRing
            (
            )
        {
            ProjectDocument Project = CreateProjectWithSurveyBoundary();
            SurfaceToolDispatcher Dispatcher = new SurfaceToolDispatcher();
            SurfaceToolResult Result = Dispatcher.Execute(Project, "get_field_geometry", "{}");

            Assert.True(Result.Success, Result.ErrorMessage);
            Assert.Contains("\"boundary_source\":\"survey\"", Result.Json.Replace(" ", string.Empty));
            Assert.Contains("\"boundary_vertex_count\":4", Result.Json.Replace(" ", string.Empty));
            Assert.Contains("boundary_vertices", Result.Json, StringComparison.Ordinal);
        }

        /// <summary>
        /// Creates a rectangular project without survey boundary points.
        /// </summary>
        /// <returns>Project document.</returns>
        static private ProjectDocument CreateProjectWithoutBoundary
            (
            )
        {
            DesignGrid Grid = SyntheticDesignGridFixture.CreateRectGrid(
                GRID_WIDTH,
                GRID_HEIGHT,
                100.0,
                100.0);
            return SyntheticDesignGridFixture.CreateProject(Grid);
        }

        /// <summary>
        /// Creates a project with a simple rectangular survey boundary.
        /// </summary>
        /// <returns>Project document.</returns>
        static private ProjectDocument CreateProjectWithSurveyBoundary
            (
            )
        {
            ProjectDocument Project = CreateProjectWithoutBoundary();
            Project.Survey = new SurveyModel
            {
                BoundaryPoints =
                {
                    new TopologyPoint { Latitude = 18.0100, Longitude = -77.5100 },
                    new TopologyPoint { Latitude = 18.0100, Longitude = -77.5090 },
                    new TopologyPoint { Latitude = 18.0090, Longitude = -77.5090 },
                    new TopologyPoint { Latitude = 18.0090, Longitude = -77.5100 }
                }
            };
            return Project;
        }
    }
}
