using AgGradeDesigner.Core;
using AgGradeDesigner.Core.Ai;
using AgGradeDesigner.Core.Ai.SurfaceTools;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Tests.Ai.Fixtures;

namespace AgGradeDesigner.Core.Tests.Ai.SurfaceTools
{
    /// <summary>
    /// Unit tests for Phase 3 road surface tools.
    /// </summary>
    public sealed class BuildRoadTests
    {
        /// <summary>Existing elevation used by the fixture grid.</summary>
        private const double EXISTING_ELEVATION_M = 100.0;

        /// <summary>Initial proposed elevation used by the fixture grid.</summary>
        private const double PROPOSED_ELEVATION_M = 100.0;

        /// <summary>Vertical raise applied to the road crown in metres.</summary>
        private const double RAISE_ABOVE_PROPOSED_M = 0.5;

        /// <summary>Flat crown width in metres.</summary>
        private const double CROWN_WIDTH_M = 1.0;

        /// <summary>Grid width for road tests.</summary>
        private const int GRID_WIDTH = 10;

        /// <summary>Grid height for road tests.</summary>
        private const int GRID_HEIGHT = 5;

        /// <summary>
        /// Verifies build_road raises bins along a centerline polyline.
        /// </summary>
        [Fact]
        public void BuildRoad_Centerline_RaisesRoadBins
            (
            )
        {
            ProjectDocument Project = CreateRoadProject();
            SurfaceToolDispatcher Dispatcher = new SurfaceToolDispatcher(AiDeliveryPhase.Phase3);
            string Arguments = BuildRoadArgumentsJson(IncludeSideSlope: false);

            SurfaceToolResult Result = Dispatcher.Execute(Project, "build_road", Arguments);
            Assert.True(Result.Success, Result.ErrorMessage);

            GridBinData CenterBin = Project.DesignGrid!.Bins.First(B => B.Valid && B.X == 5 && B.Y == 2);
            GridBinData FarBin = Project.DesignGrid.Bins.First(B => B.Valid && B.X == 5 && B.Y == 0);

            Assert.Equal(EXISTING_ELEVATION_M + RAISE_ABOVE_PROPOSED_M, CenterBin.ProposedM, 3);
            Assert.Equal(PROPOSED_ELEVATION_M, FarBin.ProposedM, 3);
        }

        /// <summary>
        /// Verifies build_road raises bins along a centerline derived from field geometry.
        /// </summary>
        [Fact]
        public void BuildRoad_CenterlineFromFieldGeometry_RaisesRoadBins
            (
            )
        {
            ProjectDocument Project = CreateRoadProject();
            SurfaceToolDispatcher Dispatcher = new SurfaceToolDispatcher(AiDeliveryPhase.Phase3);

            SurfaceToolResult GeometryResult = Dispatcher.Execute(Project, "get_field_geometry", "{}");
            Assert.True(GeometryResult.Success, GeometryResult.ErrorMessage);
            Assert.Contains("bbox_corners", GeometryResult.Json, StringComparison.Ordinal);

            double StartEasting = 1000.0 + 0.5 * FieldConstants.BIN_SIZE_M;
            double EndEasting = 1000.0 + (GRID_WIDTH - 0.5) * FieldConstants.BIN_SIZE_M;
            double Northing = 2000.0 + (GRID_HEIGHT - 0.5) * FieldConstants.BIN_SIZE_M;
            string Arguments = $$"""
                {
                  "centerline": [
                    { "x": {{StartEasting}}, "y": {{Northing}} },
                    { "x": {{EndEasting}}, "y": {{Northing}} }
                  ],
                  "crown_width_m": {{CROWN_WIDTH_M}},
                  "raise_above_proposed_m": {{RAISE_ABOVE_PROPOSED_M}}
                }
                """;

            SurfaceToolResult Result = Dispatcher.Execute(Project, "build_road", Arguments);
            Assert.True(Result.Success, Result.ErrorMessage);

            GridBinData NorthBin = Project.DesignGrid!.Bins.First(B => B.Valid && B.X == 5 && B.Y == GRID_HEIGHT - 1);
            GridBinData SouthBin = Project.DesignGrid.Bins.First(B => B.Valid && B.X == 5 && B.Y == 0);

            Assert.True(NorthBin.ProposedM > EXISTING_ELEVATION_M);
            Assert.Equal(PROPOSED_ELEVATION_M, SouthBin.ProposedM, 3);
        }

        /// <summary>
        /// Verifies side slopes extend the road influence beyond the flat crown.
        /// </summary>
        [Fact]
        public void BuildRoad_WithSideSlope_AffectsBinsOutsideCrown
            (
            )
        {
            ProjectDocument Project = CreateRoadProject();
            SurfaceToolDispatcher Dispatcher = new SurfaceToolDispatcher(AiDeliveryPhase.Phase3);
            string FlatArguments = BuildRoadArgumentsJson(IncludeSideSlope: false);
            string SlopedArguments = BuildRoadArgumentsJson(IncludeSideSlope: true);

            Dispatcher.Execute(Project, "build_road", FlatArguments);
            int FlatAffected = Project.DesignGrid!.Bins.Count(B => B.Valid && B.ProposedM > EXISTING_ELEVATION_M + 1e-3);

            Project.DesignGrid = SyntheticDesignGridFixture.CreateRectGrid(
                GRID_WIDTH,
                GRID_HEIGHT,
                EXISTING_ELEVATION_M,
                PROPOSED_ELEVATION_M);
            Project = SyntheticDesignGridFixture.CreateProject(Project.DesignGrid);
            Dispatcher = new SurfaceToolDispatcher(AiDeliveryPhase.Phase3);

            Dispatcher.Execute(Project, "build_road", SlopedArguments);
            int SlopedAffected = Project.DesignGrid.Bins.Count(B => B.Valid && B.ProposedM > EXISTING_ELEVATION_M + 1e-3);

            Assert.True(SlopedAffected > FlatAffected);
        }

        /// <summary>
        /// Verifies create_road followed by build_road using road_id works.
        /// </summary>
        [Fact]
        public void BuildRoad_WithRoadId_UsesRegisteredCorridor
            (
            )
        {
            ProjectDocument Project = CreateRoadProject();
            SurfaceToolDispatcher Dispatcher = new SurfaceToolDispatcher(AiDeliveryPhase.Phase3);
            string CreateArguments = BuildCreateRoadArgumentsJson();

            SurfaceToolResult CreateResult = Dispatcher.Execute(Project, "create_road", CreateArguments);
            Assert.True(CreateResult.Success, CreateResult.ErrorMessage);
            Assert.Contains("rd_1", CreateResult.Json);

            string BuildArguments = $$"""
                {
                  "road_id": "rd_1",
                  "raise_above_proposed_m": {{RAISE_ABOVE_PROPOSED_M}}
                }
                """;

            SurfaceToolResult BuildResult = Dispatcher.Execute(Project, "build_road", BuildArguments);
            Assert.True(BuildResult.Success, BuildResult.ErrorMessage);

            GridBinData CenterBin = Project.DesignGrid!.Bins.First(B => B.Valid && B.X == 5 && B.Y == 2);
            Assert.Equal(EXISTING_ELEVATION_M + RAISE_ABOVE_PROPOSED_M, CenterBin.ProposedM, 3);
        }

        /// <summary>
        /// Verifies calculate_road_volumes reports fill within the road corridor.
        /// </summary>
        [Fact]
        public void CalculateRoadVolumes_AfterBuild_ReturnsFill
            (
            )
        {
            ProjectDocument Project = CreateRoadProject();
            SurfaceToolDispatcher Dispatcher = new SurfaceToolDispatcher(AiDeliveryPhase.Phase3);
            Dispatcher.Execute(Project, "build_road", BuildRoadArgumentsJson(IncludeSideSlope: false));

            SurfaceToolResult VolumeResult = Dispatcher.Execute(
                Project,
                "calculate_road_volumes",
                """{ "road_id": "rd_1" }""");

            Assert.True(VolumeResult.Success, VolumeResult.ErrorMessage);
            Assert.Contains("\"fill_m3\"", VolumeResult.Json);
            Assert.DoesNotContain("\"fill_m3\":0", VolumeResult.Json.Replace(" ", string.Empty));
        }

        /// <summary>
        /// Verifies build_road can run as a step inside preview_edit.
        /// </summary>
        [Fact]
        public void PreviewEdit_BuildRoadStep_RaisesRoadBins
            (
            )
        {
            ProjectDocument Project = CreateRoadProject();
            SurfaceToolDispatcher Dispatcher = new SurfaceToolDispatcher(AiDeliveryPhase.Phase3);
            double StartEasting = 1000.0 + 0.5 * FieldConstants.BIN_SIZE_M;
            double EndEasting = 1000.0 + (GRID_WIDTH - 0.5) * FieldConstants.BIN_SIZE_M;
            double Northing = 2000.0 + (2 + 0.5) * FieldConstants.BIN_SIZE_M;

            SurfaceToolResult Result = Dispatcher.Execute(
                Project,
                "preview_edit",
                $$"""
                {
                  "edit_plan": {
                    "description": "Add access road",
                    "steps": [
                      {
                        "operation": "build_road",
                        "centerline": [
                          { "x": {{StartEasting}}, "y": {{Northing}} },
                          { "x": {{EndEasting}}, "y": {{Northing}} }
                        ],
                        "crown_width_m": {{CROWN_WIDTH_M}},
                        "raise_above_proposed_m": {{RAISE_ABOVE_PROPOSED_M}}
                      }
                    ]
                  }
                }
                """);

            Assert.True(Result.Success, Result.ErrorMessage);
            Assert.Contains("build_road", Result.Json, StringComparison.Ordinal);
            Assert.True(Dispatcher.EditSession.PreviewActive);

            GridBinData CenterBin = Project.DesignGrid!.Bins.First(B => B.Valid && B.X == 5 && B.Y == 2);
            Assert.Equal(EXISTING_ELEVATION_M + RAISE_ABOVE_PROPOSED_M, CenterBin.ProposedM, 3);
        }

        /// <summary>
        /// Verifies preview_edit can offset the surface then build a road in one plan.
        /// </summary>
        [Fact]
        public void PreviewEdit_OffsetThenBuildRoad_AppliesBothSteps
            (
            )
        {
            const double OFFSET_M = 0.1;
            ProjectDocument Project = CreateRoadProject();
            SurfaceToolDispatcher Dispatcher = new SurfaceToolDispatcher(AiDeliveryPhase.Phase3);
            double StartEasting = 1000.0 + 0.5 * FieldConstants.BIN_SIZE_M;
            double EndEasting = 1000.0 + (GRID_WIDTH - 0.5) * FieldConstants.BIN_SIZE_M;
            double Northing = 2000.0 + (2 + 0.5) * FieldConstants.BIN_SIZE_M;

            SurfaceToolResult Result = Dispatcher.Execute(
                Project,
                "preview_edit",
                $$"""
                {
                  "edit_plan": {
                    "description": "Raise field then add road",
                    "steps": [
                      { "operation": "offset_surface", "delta_z": {{OFFSET_M}} },
                      {
                        "operation": "build_road",
                        "centerline": [
                          { "x": {{StartEasting}}, "y": {{Northing}} },
                          { "x": {{EndEasting}}, "y": {{Northing}} }
                        ],
                        "crown_width_m": {{CROWN_WIDTH_M}},
                        "raise_above_proposed_m": {{RAISE_ABOVE_PROPOSED_M}}
                      }
                    ]
                  }
                }
                """);

            Assert.True(Result.Success, Result.ErrorMessage);
            Assert.Contains("step_count", Result.Json, StringComparison.Ordinal);

            GridBinData CenterBin = Project.DesignGrid!.Bins.First(B => B.Valid && B.X == 5 && B.Y == 2);
            GridBinData FarBin = Project.DesignGrid.Bins.First(B => B.Valid && B.X == 5 && B.Y == 0);

            Assert.Equal(PROPOSED_ELEVATION_M + OFFSET_M + RAISE_ABOVE_PROPOSED_M, CenterBin.ProposedM, 3);
            Assert.Equal(PROPOSED_ELEVATION_M + OFFSET_M, FarBin.ProposedM, 3);
        }

        /// <summary>
        /// Verifies preview_edit build_road can reference a road registered by create_road.
        /// </summary>
        [Fact]
        public void PreviewEdit_BuildRoadWithRoadId_UsesRegisteredCorridor
            (
            )
        {
            ProjectDocument Project = CreateRoadProject();
            SurfaceToolDispatcher Dispatcher = new SurfaceToolDispatcher(AiDeliveryPhase.Phase3);

            SurfaceToolResult CreateResult = Dispatcher.Execute(Project, "create_road", BuildCreateRoadArgumentsJson());
            Assert.True(CreateResult.Success, CreateResult.ErrorMessage);

            SurfaceToolResult Result = Dispatcher.Execute(
                Project,
                "preview_edit",
                $$"""
                {
                  "edit_plan": {
                    "description": "Build registered road",
                    "steps": [
                      {
                        "operation": "build_road",
                        "road_id": "rd_1",
                        "raise_above_proposed_m": {{RAISE_ABOVE_PROPOSED_M}}
                      }
                    ]
                  }
                }
                """);

            Assert.True(Result.Success, Result.ErrorMessage);

            GridBinData CenterBin = Project.DesignGrid!.Bins.First(B => B.Valid && B.X == 5 && B.Y == 2);
            Assert.Equal(EXISTING_ELEVATION_M + RAISE_ABOVE_PROPOSED_M, CenterBin.ProposedM, 3);
        }

        /// <summary>
        /// Creates a project with a wide flat grid for road tests.
        /// </summary>
        /// <returns>Project document.</returns>
        static private ProjectDocument CreateRoadProject
            (
            )
        {
            DesignGrid Grid = SyntheticDesignGridFixture.CreateRectGrid(
                GRID_WIDTH,
                GRID_HEIGHT,
                EXISTING_ELEVATION_M,
                PROPOSED_ELEVATION_M);
            return SyntheticDesignGridFixture.CreateProject(Grid);
        }

        /// <summary>
        /// Builds JSON arguments for build_road along the middle grid row.
        /// </summary>
        /// <param name="IncludeSideSlope">True to include a 2:1 side slope.</param>
        /// <returns>Tool arguments JSON.</returns>
        static private string BuildRoadArgumentsJson
            (
            bool IncludeSideSlope
            )
        {
            double StartEasting = 1000.0 + 0.5 * FieldConstants.BIN_SIZE_M;
            double EndEasting = 1000.0 + (GRID_WIDTH - 0.5) * FieldConstants.BIN_SIZE_M;
            double Northing = 2000.0 + (2 + 0.5) * FieldConstants.BIN_SIZE_M;
            string SideSlopeJson = IncludeSideSlope ? """, "side_slope_hv": 2.0 """ : string.Empty;

            return $$"""
                {
                  "centerline": [
                    { "x": {{StartEasting}}, "y": {{Northing}} },
                    { "x": {{EndEasting}}, "y": {{Northing}} }
                  ],
                  "crown_width_m": {{CROWN_WIDTH_M}},
                  "raise_above_proposed_m": {{RAISE_ABOVE_PROPOSED_M}}{{SideSlopeJson}}
                }
                """;
        }

        /// <summary>
        /// Builds JSON arguments for create_road along the middle grid row.
        /// </summary>
        /// <returns>Tool arguments JSON.</returns>
        static private string BuildCreateRoadArgumentsJson
            (
            )
        {
            double StartEasting = 1000.0 + 0.5 * FieldConstants.BIN_SIZE_M;
            double EndEasting = 1000.0 + (GRID_WIDTH - 0.5) * FieldConstants.BIN_SIZE_M;
            double Northing = 2000.0 + (2 + 0.5) * FieldConstants.BIN_SIZE_M;

            return $$"""
                {
                  "centerline": [
                    { "x": {{StartEasting}}, "y": {{Northing}} },
                    { "x": {{EndEasting}}, "y": {{Northing}} }
                  ],
                  "width_m": {{CROWN_WIDTH_M}},
                  "name": "Access road"
                }
                """;
        }
    }
}
