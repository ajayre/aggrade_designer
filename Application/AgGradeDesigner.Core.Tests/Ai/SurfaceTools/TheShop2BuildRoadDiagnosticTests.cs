using System.Text.Json;

using AgGradeDesigner.Core.Ai;
using AgGradeDesigner.Core.Ai.SurfaceTools;
using AgGradeDesigner.Core.Design;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Survey;
using AgGradeDesigner.Core.Tests.Import;
using AgGradeDesigner.Core.Tests.TestFixtures;

namespace AgGradeDesigner.Core.Tests.Ai.SurfaceTools
{
    /// <summary>
    /// Diagnostic tests for road building on the TheShop2 AGD fixture.
    /// </summary>
    public sealed class TheShop2BuildRoadDiagnosticTests
    {
        /// <summary>12 ft crown width in metres.</summary>
        private const double CROWN_WIDTH_M = 3.6576;

        /// <summary>1.5 ft raise in metres.</summary>
        private const double RAISE_ABOVE_PROPOSED_M = 0.4572;

        /// <summary>Maximum plausible road-only fill on TheShop2 in cubic metres.</summary>
        private const double MAX_ROAD_FILL_M3 = 5000.0;

        /// <summary>
        /// Verifies build_road reports road-operation fill, not field-wide earthwork totals.
        /// </summary>
        [Fact]
        public void BuildRoad_TheShop2_NorthEdgeCenterline_ReportsRoadOnlyVolumes
            (
            )
        {
            ProjectDocument Project = ImportHandoffProjectFactory.CreateTheShop2Project();
            DesignGrid Grid = Project.DesignGrid!;
            List<(double X, double Y)> NorthEdge = BuildNorthEdgeCenterlineFromExtents(Grid);
            Assert.True(NorthEdge.Count >= 2);
            Assert.True(
                MeasurePolylineLengthM(NorthEdge) > 100.0,
                $"Expected a full-width north edge centerline, got {NorthEdge.Count} vertices.");

            SurfaceToolDispatcher Dispatcher = new SurfaceToolDispatcher(AiDeliveryPhase.Phase3);
            string Arguments = BuildRoadArguments(NorthEdge);

            SurfaceToolResult Result = Dispatcher.Execute(Project, "build_road", Arguments);
            Assert.True(Result.Success, Result.ErrorMessage);

            using JsonDocument Document = JsonDocument.Parse(Result.Json);
            double FillM3 = Document.RootElement.GetProperty("fill_m3").GetDouble();
            string VolumeScope = Document.RootElement.GetProperty("volume_scope").GetString() ?? string.Empty;
            int AffectedBins = Document.RootElement.GetProperty("affected_bin_count").GetInt32();

            Assert.Equal("road_operation_delta", VolumeScope);
            Assert.True(AffectedBins > 0, Result.Json);
            Assert.True(FillM3 > 0.0);
            Assert.True(FillM3 < MAX_ROAD_FILL_M3, $"Expected road-only fill, got {FillM3} m³. Result: {Result.Json}");
        }

        /// <summary>
        /// Verifies get_field_geometry exposes boundary and grid context on TheShop2.
        /// </summary>
        [Fact]
        public void GetFieldGeometry_TheShop2_IncludesPlanningContext
            (
            )
        {
            ProjectDocument Project = ImportHandoffProjectFactory.CreateTheShop2Project();
            SurfaceToolDispatcher Dispatcher = new SurfaceToolDispatcher(AiDeliveryPhase.Phase3);

            SurfaceToolResult Result = Dispatcher.Execute(Project, "get_field_geometry", "{}");
            Assert.True(Result.Success, Result.ErrorMessage);
            Assert.Contains("boundary_vertices", Result.Json, StringComparison.Ordinal);
            Assert.Contains("\"grid\"", Result.Json, StringComparison.Ordinal);
            Assert.Contains("geometry_planning_note", Result.Json, StringComparison.Ordinal);
            Assert.DoesNotContain("edge_centerlines", Result.Json, StringComparison.Ordinal);
        }

        /// <summary>
        /// Builds a north-edge centerline the way the AI should: from field extents, not a preset polyline.
        /// </summary>
        /// <param name="Grid">Design grid.</param>
        /// <returns>West-to-east centerline along the northern extent.</returns>
        static private List<(double X, double Y)> BuildNorthEdgeCenterlineFromExtents
            (
            DesignGrid Grid
            )
        {
            SurfaceSummary Summary = SurfaceAnalysis.BuildSurfaceSummary(Grid);
            return new List<(double X, double Y)>
            {
                (Summary.MinEastingM, Summary.MaxNorthingM),
                (Summary.MaxEastingM, Summary.MaxNorthingM)
            };
        }

        /// <summary>
        /// Returns the total length of a UTM polyline in metres.
        /// </summary>
        /// <param name="Vertices">Ordered polyline vertices.</param>
        /// <returns>Polyline length in metres.</returns>
        static private double MeasurePolylineLengthM
            (
            IReadOnlyList<(double X, double Y)> Vertices
            )
        {
            double LengthM = 0.0;
            for (int Index = 1; Index < Vertices.Count; Index++)
            {
                double DeltaX = Vertices[Index].X - Vertices[Index - 1].X;
                double DeltaY = Vertices[Index].Y - Vertices[Index - 1].Y;
                LengthM += Math.Sqrt((DeltaX * DeltaX) + (DeltaY * DeltaY));
            }

            return LengthM;
        }

        /// <summary>
        /// Builds JSON arguments for a north-edge road on TheShop2.
        /// </summary>
        /// <param name="NorthEdge">North edge centerline vertices.</param>
        /// <returns>Tool arguments JSON.</returns>
        static private string BuildRoadArguments
            (
            IReadOnlyList<(double X, double Y)> NorthEdge
            )
        {
            string Start = $$"""{ "x": {{NorthEdge[0].X}}, "y": {{NorthEdge[0].Y}} }""";
            string End = $$"""{ "x": {{NorthEdge[^1].X}}, "y": {{NorthEdge[^1].Y}} }""";
            return $$"""
                {
                  "centerline": [ {{Start}}, {{End}} ],
                  "crown_width_m": {{CROWN_WIDTH_M}},
                  "raise_above_proposed_m": {{RAISE_ABOVE_PROPOSED_M}}
                }
                """;
        }
    }
}
