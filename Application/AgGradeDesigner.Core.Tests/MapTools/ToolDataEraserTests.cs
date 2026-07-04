using AgGradeDesigner.Core.MapTools;

namespace AgGradeDesigner.Core.Tests.MapTools
{
    /// <summary>
    /// Unit tests for erase rectangle selection rules (TOOLS-SPEC §3.3.7–§3.3.8).
    /// </summary>
    public sealed class ToolDataEraserTests
    {
        /// <summary>
        /// Screen X coordinate for vertices outside the erase rectangle.
        /// </summary>
        private const float OUTSIDE_X = 10f;

        /// <summary>
        /// Screen Y coordinate for vertices outside the erase rectangle.
        /// </summary>
        private const float OUTSIDE_Y = 10f;

        /// <summary>
        /// Screen X coordinate for vertices inside the erase rectangle.
        /// </summary>
        private const float INSIDE_X = 50f;

        /// <summary>
        /// Screen Y coordinate for vertices inside the erase rectangle.
        /// </summary>
        private const float INSIDE_Y = 50f;

        /// <summary>
        /// Latitude assigned to inside test vertices.
        /// </summary>
        private const double INSIDE_LAT = 40.0;

        /// <summary>
        /// Longitude assigned to inside test vertices.
        /// </summary>
        private const double INSIDE_LON = -105.0;

        /// <summary>
        /// Stored elevation for test vertices.
        /// </summary>
        private const double ELEVATION_M = 100.0;

        /// <summary>
        /// Verifies completely-inside mode deletes all segments with both endpoints in the rectangle.
        /// </summary>
        [Fact]
        public void Apply_CompletelyInsideMode_RemovesAllInsideSegments
            (
            )
        {
            ToolDataSession Session = new ToolDataSession();
            Session.AppendPolylineVertex(INSIDE_LAT, INSIDE_LON, ELEVATION_M);
            Session.AppendPolylineVertex(INSIDE_LAT + 0.001, INSIDE_LON, ELEVATION_M);
            Session.AppendPolylineVertex(INSIDE_LAT + 0.002, INSIDE_LON, ELEVATION_M);
            Session.CompleteCurrentPolyline();

            ToolScreenRect Rect = new ToolScreenRect
            {
                Left = 40f,
                Top = 40f,
                Right = 60f,
                Bottom = 60f,
            };

            ToolDataEraser.Apply(
                Session,
                Rect,
                EraseSelectionMode.CompletelyInside,
                ProjectToScreen);

            Assert.Empty(Session.Polylines);
        }

        /// <summary>
        /// Verifies enter-and-inside mode retains a middle segment fully surrounded by inside vertices.
        /// </summary>
        [Fact]
        public void Apply_EnterAndInsideMode_KeepsInteriorRunSegment
            (
            )
        {
            ToolDataSession Session = new ToolDataSession();
            Session.AppendPolylineVertex(INSIDE_LAT, INSIDE_LON, ELEVATION_M);
            Session.AppendPolylineVertex(INSIDE_LAT + 0.001, INSIDE_LON, ELEVATION_M);
            Session.AppendPolylineVertex(INSIDE_LAT + 0.002, INSIDE_LON, ELEVATION_M);
            Session.AppendPolylineVertex(INSIDE_LAT + 0.003, INSIDE_LON, ELEVATION_M);
            Session.CompleteCurrentPolyline();

            ToolScreenRect Rect = new ToolScreenRect
            {
                Left = 40f,
                Top = 40f,
                Right = 60f,
                Bottom = 60f,
            };

            ToolDataEraser.Apply(
                Session,
                Rect,
                EraseSelectionMode.EnterAndCompletelyInside,
                ProjectToScreen);

            Assert.Single(Session.Polylines);
            Assert.Equal(2, Session.Polylines[0].Vertices.Count);
        }

        /// <summary>
        /// Verifies completely-inside mode removes a middle segment and splits the polyline in two.
        /// </summary>
        [Fact]
        public void Apply_CompletelyInsideMode_SplitsPolylineAtMiddleSegment
            (
            )
        {
            ToolDataSession Session = new ToolDataSession();
            Session.AppendPolylineVertex(INSIDE_LAT, INSIDE_LON, ELEVATION_M);
            Session.AppendPolylineVertex(INSIDE_LAT + 0.001, INSIDE_LON, ELEVATION_M);
            Session.AppendPolylineVertex(INSIDE_LAT + 0.002, INSIDE_LON, ELEVATION_M);
            Session.AppendPolylineVertex(INSIDE_LAT + 0.003, INSIDE_LON, ELEVATION_M);
            Session.CompleteCurrentPolyline();

            ToolScreenRect Rect = new ToolScreenRect
            {
                Left = 40f,
                Top = 40f,
                Right = 60f,
                Bottom = 60f,
            };

            ToolDataEraser.Apply(
                Session,
                Rect,
                EraseSelectionMode.CompletelyInside,
                ProjectMiddleSegmentToScreen);

            Assert.Equal(2, Session.Polylines.Count);
            Assert.Equal(2, Session.Polylines[0].Vertices.Count);
            Assert.Equal(2, Session.Polylines[1].Vertices.Count);
        }

        /// <summary>
        /// Projects only the middle-segment vertices to the inside test screen location.
        /// </summary>
        /// <param name="Latitude">Point latitude.</param>
        /// <param name="Longitude">Point longitude.</param>
        /// <param name="ScreenX">Output screen X.</param>
        /// <param name="ScreenY">Output screen Y.</param>
        /// <returns>True when projection succeeds.</returns>
        private bool ProjectMiddleSegmentToScreen
            (
            double Latitude,
            double Longitude,
            out float ScreenX,
            out float ScreenY
            )
        {
            if ((Math.Abs(Latitude - (INSIDE_LAT + 0.001)) < 0.0001) ||
                (Math.Abs(Latitude - (INSIDE_LAT + 0.002)) < 0.0001))
            {
                ScreenX = INSIDE_X;
                ScreenY = INSIDE_Y;
                return true;
            }

            ScreenX = OUTSIDE_X;
            ScreenY = OUTSIDE_Y;
            return true;
        }

        /// <summary>
        /// Projects inside/outside test coordinates to fixed screen locations.
        /// </summary>
        /// <param name="Latitude">Point latitude.</param>
        /// <param name="Longitude">Point longitude.</param>
        /// <param name="ScreenX">Output screen X.</param>
        /// <param name="ScreenY">Output screen Y.</param>
        /// <returns>True when projection succeeds.</returns>
        private bool ProjectToScreen
            (
            double Latitude,
            double Longitude,
            out float ScreenX,
            out float ScreenY
            )
        {
            if ((Math.Abs(Latitude - INSIDE_LAT) < 0.01) &&
                (Math.Abs(Longitude - INSIDE_LON) < 0.01))
            {
                ScreenX = INSIDE_X;
                ScreenY = INSIDE_Y;
                return true;
            }

            ScreenX = OUTSIDE_X;
            ScreenY = OUTSIDE_Y;
            return true;
        }
    }
}
