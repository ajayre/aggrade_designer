using AgGradeDesigner.Core;
using AgGradeDesigner.Core.Features;

namespace AgGradeDesigner.Core.Tests.Features
{
    /// <summary>
    /// Tests for snapping a road segment to 15° increments.
    /// </summary>
    public sealed class RoadSegmentSnapTests
    {
        /// <summary>Last vertex used as the snap origin, in pixels.</summary>
        private const double ORIGIN_X = 100.0;

        /// <summary>Last vertex used as the snap origin, in pixels.</summary>
        private const double ORIGIN_Y = 200.0;

        /// <summary>Length of the test segment, in pixels.</summary>
        private const double SEGMENT_LENGTH_PX = 80.0;

        /// <summary>Decimal places required of a snapped coordinate.</summary>
        private const int PIXEL_PRECISION = 4;

        /// <summary>
        /// Verifies a segment that is already on a 15° step stays there, including due east and 45°.
        /// </summary>
        [Fact]
        public void Snap_KeepsAnExactIncrement
            (
            )
        {
            AssertSnapsTo(0.0);
            AssertSnapsTo(45.0);
            AssertSnapsTo(-90.0);
        }

        /// <summary>
        /// Verifies 10° snaps to 15° and 7° snaps back to 0°.
        /// </summary>
        [Fact]
        public void Snap_ChoosesTheNearestIncrement
            (
            )
        {
            AssertSnapsTo(10.0, 15.0);
            AssertSnapsTo(7.0, 0.0);
            AssertSnapsTo(-10.0, -15.0);
        }

        /// <summary>
        /// Verifies an angle just past halfway takes the next increment.
        /// </summary>
        [Fact]
        public void Snap_JustPastHalfwayTakesTheNextIncrement
            (
            )
        {
            AssertSnapsTo(8.0, 15.0);
            AssertSnapsTo(-8.0, -15.0);
        }

        /// <summary>
        /// Verifies a pointer sitting on the last vertex is left alone.
        /// </summary>
        [Fact]
        public void Snap_IgnoresAPointerOnTheLastVertex
            (
            )
        {
            bool Snapped = RoadSegmentSnap.TrySnapScreenPoint(
                ORIGIN_X,
                ORIGIN_Y,
                ORIGIN_X,
                ORIGIN_Y,
                out double SnappedX,
                out double SnappedY);

            Assert.False(Snapped);
            Assert.Equal(ORIGIN_X, SnappedX);
            Assert.Equal(ORIGIN_Y, SnappedY);
        }

        /// <summary>
        /// Snaps a pointer at <paramref name="FromDegrees"/> and expects <paramref name="ExpectedDegrees"/>.
        /// </summary>
        /// <param name="FromDegrees">Pointer direction from the last vertex, in degrees. Screen Y grows downward.</param>
        /// <param name="ExpectedDegrees">Direction the segment should take, in degrees.</param>
        private void AssertSnapsTo
            (
            double FromDegrees,
            double? ExpectedDegrees = null
            )
        {
            double Expected = ExpectedDegrees ?? FromDegrees;
            double FromRadians = FromDegrees * Math.PI / FieldConstants.DEGREES_PER_RADIAN;
            double PointerX = ORIGIN_X + (SEGMENT_LENGTH_PX * Math.Cos(FromRadians));
            double PointerY = ORIGIN_Y + (SEGMENT_LENGTH_PX * Math.Sin(FromRadians));

            bool Snapped = RoadSegmentSnap.TrySnapScreenPoint(
                ORIGIN_X,
                ORIGIN_Y,
                PointerX,
                PointerY,
                out double SnappedX,
                out double SnappedY);

            double ExpectedRadians = Expected * Math.PI / FieldConstants.DEGREES_PER_RADIAN;
            double ExpectedX = ORIGIN_X + (SEGMENT_LENGTH_PX * Math.Cos(ExpectedRadians));
            double ExpectedY = ORIGIN_Y + (SEGMENT_LENGTH_PX * Math.Sin(ExpectedRadians));
            Assert.True(Snapped);
            Assert.Equal(ExpectedX, SnappedX, PIXEL_PRECISION);
            Assert.Equal(ExpectedY, SnappedY, PIXEL_PRECISION);
        }
    }
}
