using AgGradeDesigner.Core;
using AgGradeDesigner.Core.Ai;
using AgGradeDesigner.Core.Ai.SurfaceTools;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Tests.Ai.Fixtures;

namespace AgGradeDesigner.Core.Tests.Ai.SurfaceTools
{
    /// <summary>
    /// Unit tests for the <c>apply_plane_grade</c> Phase 2 surface tool.
    /// </summary>
    public sealed class ApplyPlaneGradeTests
    {
        /// <summary>Existing elevation used by the fixture grid.</summary>
        private const double EXISTING_ELEVATION_M = 100.0;

        /// <summary>Initial proposed elevation used by the fixture grid.</summary>
        private const double PROPOSED_ELEVATION_M = 100.0;

        /// <summary>Plane anchor easting in metres.</summary>
        private const double ORIGIN_EASTING_M = 1000.0;

        /// <summary>Plane anchor northing in metres.</summary>
        private const double ORIGIN_NORTHING_M = 2000.0;

        /// <summary>Plane anchor elevation in metres.</summary>
        private const double ORIGIN_ELEVATION_M = 100.0;

        /// <summary>Easting grade applied during the test in m/m.</summary>
        private const double SLOPE_X = 0.001;

        /// <summary>Northing grade applied during the test in m/m.</summary>
        private const double SLOPE_Y = 0.0;

        /// <summary>Minimum UTM easting for the polygon-limited edit.</summary>
        private const double POLYGON_MIN_EASTING_M = 1000.0;

        /// <summary>Maximum UTM easting for the polygon-limited edit.</summary>
        private const double POLYGON_MAX_EASTING_M = 1000.0 + FieldConstants.BIN_SIZE_M;

        /// <summary>Minimum UTM northing for the polygon-limited edit.</summary>
        private const double POLYGON_MIN_NORTHING_M = 2000.0;

        /// <summary>Maximum UTM northing for the polygon-limited edit.</summary>
        private const double POLYGON_MAX_NORTHING_M = 2000.0 + (3 * FieldConstants.BIN_SIZE_M);

        /// <summary>
        /// Verifies apply_plane_grade changes proposed elevations inside a polygon only.
        /// </summary>
        [Fact]
        public void ApplyPlaneGrade_PolygonScope_DoesNotLeakOutside
            (
            )
        {
            ProjectDocument Project = SyntheticDesignGridFixture.CreateProject(
                SyntheticDesignGridFixture.CreateFlatGrid(EXISTING_ELEVATION_M, PROPOSED_ELEVATION_M));
            SurfaceToolDispatcher Dispatcher = new SurfaceToolDispatcher(AiDeliveryPhase.Phase2);

            string Arguments = $$"""
                {
                  "origin": { "x": {{ORIGIN_EASTING_M}}, "y": {{ORIGIN_NORTHING_M}}, "z": {{ORIGIN_ELEVATION_M}} },
                  "slope_x": {{SLOPE_X}},
                  "slope_y": {{SLOPE_Y}},
                  "polygon": {
                    "vertices": [
                      { "x": {{POLYGON_MIN_EASTING_M}}, "y": {{POLYGON_MIN_NORTHING_M}} },
                      { "x": {{POLYGON_MAX_EASTING_M}}, "y": {{POLYGON_MIN_NORTHING_M}} },
                      { "x": {{POLYGON_MAX_EASTING_M}}, "y": {{POLYGON_MAX_NORTHING_M}} },
                      { "x": {{POLYGON_MIN_EASTING_M}}, "y": {{POLYGON_MAX_NORTHING_M}} }
                    ]
                  }
                }
                """;

            SurfaceToolResult Result = Dispatcher.Execute(Project, "apply_plane_grade", Arguments);
            Assert.True(Result.Success);

            GridBinData InsideBin = Project.DesignGrid!.Bins.First(B => B.Valid && B.X == 0 && B.Y == 0);
            GridBinData OutsideBin = Project.DesignGrid.Bins.First(B => B.Valid && B.X == 2 && B.Y == 2);

            Assert.NotEqual(PROPOSED_ELEVATION_M, InsideBin.ProposedM);
            Assert.Equal(PROPOSED_ELEVATION_M, OutsideBin.ProposedM, 6);
        }
    }
}
