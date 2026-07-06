using System.Text.Json;

using AgGradeDesigner.Core.Ai.SurfaceTools;

namespace AgGradeDesigner.Core.Tests.Ai.SurfaceTools
{
    /// <summary>
    /// Unit tests for road side-slope argument parsing.
    /// </summary>
    public sealed class RoadSideSlopeSpecTests
    {
        /// <summary>
        /// Verifies side_slope_side selects one side of the centerline.
        /// </summary>
        [Fact]
        public void TryParseOptional_SideSlopeSide_ParsesLeftRightBoth
            (
            )
        {
            using JsonDocument LeftDocument = JsonDocument.Parse("""{"side_slope_hv":2,"side_slope_side":"left"}""");
            Assert.True(RoadSideSlopeSpec.TryParseOptional(
                LeftDocument.RootElement,
                out RoadSideSlopeSpec? LeftSlope,
                out string? LeftError));
            Assert.Null(LeftError);
            Assert.NotNull(LeftSlope);
            Assert.Equal(RoadSideSlopeSide.Left, LeftSlope.Value.Side);
            Assert.True(LeftSlope.Value.AppliesToSignedOffset(1.0));
            Assert.False(LeftSlope.Value.AppliesToSignedOffset(-1.0));

            using JsonDocument RightDocument = JsonDocument.Parse("""{"side_slope_percent":50,"side_slope_side":"right"}""");
            Assert.True(RoadSideSlopeSpec.TryParseOptional(
                RightDocument.RootElement,
                out RoadSideSlopeSpec? RightSlope,
                out string? RightError));
            Assert.Null(RightError);
            Assert.NotNull(RightSlope);
            Assert.Equal(RoadSideSlopeSide.Right, RightSlope.Value.Side);
            Assert.False(RightSlope.Value.AppliesToSignedOffset(1.0));
            Assert.True(RightSlope.Value.AppliesToSignedOffset(-1.0));
        }

        /// <summary>
        /// Verifies negative vertical_offset_m builds a ditch.
        /// </summary>
        [Fact]
        public void TryParseOptional_DitchSideSlope_RisesBackToGrade
            (
            )
        {
            RoadSideSlopeSpec Slope = new RoadSideSlopeSpec { HorizontalToVertical = 2.0 };
            double BottomEdgeM = 100.0;
            double AtOneMetreOutM = Slope.ElevationBeyondFlatEdge(BottomEdgeM, 1.0, IsCut: true);
            Assert.Equal(100.5, AtOneMetreOutM, 3);
        }

        /// <summary>
        /// Verifies omitted side_slope_side defaults to both sides.
        /// </summary>
        [Fact]
        public void TryParseOptional_WithoutSide_DefaultsToBoth
            (
            )
        {
            using JsonDocument Document = JsonDocument.Parse("""{"side_slope_hv":2}""");
            Assert.True(RoadSideSlopeSpec.TryParseOptional(
                Document.RootElement,
                out RoadSideSlopeSpec? SideSlope,
                out string? Error));
            Assert.Null(Error);
            Assert.NotNull(SideSlope);
            Assert.Equal(RoadSideSlopeSide.Both, SideSlope.Value.Side);
        }
    }
}
