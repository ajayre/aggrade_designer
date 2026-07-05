using AgGradeDesigner.Core.Ai.SurfaceTools;
using AgGradeDesigner.Core.MapTools;

namespace AgGradeDesigner.Core.Tests.MapTools
{
    /// <summary>
    /// Unit tests for <see cref="ToolDataSession"/> and <c>get_tool_data</c> payload shape.
    /// </summary>
    public sealed class GetToolDataTests
    {
        /// <summary>
        /// Proposed elevation m used by fixtures in this class.
        /// </summary>
        private const double PROPOSED_ELEVATION_M = 312.45;

        /// <summary>
        /// Verifies an empty session returns the normative empty payload.
        /// </summary>
        [Fact]
        public void BuildGetToolDataPayload_EmptySession_ReturnsEmptyArrays
            (
            )
        {
            ToolDataSession Session = new ToolDataSession();
            SurfaceToolDispatcher Dispatcher = new SurfaceToolDispatcher(ToolDataProvider: () => Session);
            SurfaceToolResult Result = Dispatcher.Execute(
                new AgGradeDesigner.Core.Models.ProjectDocument(),
                "get_tool_data",
                "{}");

            Assert.True(Result.Success);
            Assert.Contains("\"point_count\":0", Result.Json, StringComparison.Ordinal);
            Assert.Contains("\"polyline_count\":0", Result.Json, StringComparison.Ordinal);
            Assert.Contains("\"polygon_count\":0", Result.Json, StringComparison.Ordinal);
            Assert.Contains("\"polygons\":[]", Result.Json, StringComparison.Ordinal);
            Assert.Contains("\"coordinate_frame\":\"utm_metres\"", Result.Json, StringComparison.Ordinal);
            Assert.Contains("\"elevations_frozen_at_creation\":true", Result.Json, StringComparison.Ordinal);
        }

        /// <summary>
        /// Verifies added points and polylines serialize with stable identifiers.
        /// </summary>
        [Fact]
        public void BuildGetToolDataPayload_WithFeatures_ReturnsStableIds
            (
            )
        {
            ToolDataSession Session = new ToolDataSession();
            Session.AddPoint(40.0, -105.0, PROPOSED_ELEVATION_M);
            Session.AppendPolylineVertex(40.001, -105.0, PROPOSED_ELEVATION_M);
            Session.AppendPolylineVertex(40.002, -105.001, PROPOSED_ELEVATION_M);
            Session.CompleteCurrentPolyline();

            SurfaceToolDispatcher Dispatcher = new SurfaceToolDispatcher(ToolDataProvider: () => Session);
            SurfaceToolResult Result = Dispatcher.Execute(
                new AgGradeDesigner.Core.Models.ProjectDocument(),
                "get_tool_data",
                "{}");

            Assert.True(Result.Success);
            Assert.Contains("\"tp_1\"", Result.Json, StringComparison.Ordinal);
            Assert.Contains("\"pl_1\"", Result.Json, StringComparison.Ordinal);
            Assert.Contains("\"vertex_count\":2", Result.Json, StringComparison.Ordinal);
        }

        /// <summary>
        /// Verifies completed polygons serialize with stable identifiers and closed flag (§4.6.1).
        /// </summary>
        [Fact]
        public void BuildGetToolDataPayload_WithPolygon_ReturnsClosedPolygon
            (
            )
        {
            ToolDataSession Session = new ToolDataSession();
            Session.AppendPolygonVertex(40.0, -105.0, PROPOSED_ELEVATION_M);
            Session.AppendPolygonVertex(40.001, -105.0, PROPOSED_ELEVATION_M);
            Session.AppendPolygonVertex(40.0005, -105.001, PROPOSED_ELEVATION_M);
            Session.TryCloseCurrentPolygon();

            SurfaceToolDispatcher Dispatcher = new SurfaceToolDispatcher(ToolDataProvider: () => Session);
            SurfaceToolResult Result = Dispatcher.Execute(
                new AgGradeDesigner.Core.Models.ProjectDocument(),
                "get_tool_data",
                "{}");

            Assert.True(Result.Success);
            Assert.Contains("\"pg_1\"", Result.Json, StringComparison.Ordinal);
            Assert.Contains("\"polygon_count\":1", Result.Json, StringComparison.Ordinal);
            Assert.Contains("\"closed\":true", Result.Json, StringComparison.Ordinal);
            Assert.Contains("\"vertex_count\":3", Result.Json, StringComparison.Ordinal);
        }
    }
}
