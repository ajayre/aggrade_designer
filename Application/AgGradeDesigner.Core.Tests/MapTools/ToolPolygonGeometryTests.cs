using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.MapTools;

namespace AgGradeDesigner.Core.Tests.MapTools
{
    /// <summary>
    /// Unit tests for <see cref="ToolPolygonGeometry"/> (TOOLS-SPEC §3.4.6, §3.4.13).
    /// </summary>
    public sealed class ToolPolygonGeometryTests
    {
        /// <summary>
        /// Base latitude for geometry fixtures in this class.
        /// </summary>
        private const double BASE_LAT = 51.0;

        /// <summary>
        /// Base longitude for geometry fixtures in this class.
        /// </summary>
        private const double BASE_LON = -114.0;

        /// <summary>
        /// Stored elevation assigned to test vertices.
        /// </summary>
        private const double ELEVATION_M = 100.0;

        /// <summary>
        /// Screen X coordinate shared by pointer and vertex in near-proximity tests.
        /// </summary>
        private const float NEAR_SCREEN_X = 100f;

        /// <summary>
        /// Screen Y coordinate shared by pointer and vertex in near-proximity tests.
        /// </summary>
        private const float NEAR_SCREEN_Y = 200f;

        /// <summary>
        /// Screen offset in pixels that exceeds the close-proximity threshold.
        /// </summary>
        private const float FAR_SCREEN_OFFSET_PX = 60f;

        /// <summary>
        /// Verifies close proximity uses the screen-space pixel threshold.
        /// </summary>
        [Fact]
        public void IsNearVertexScreen_WithinThreshold_ReturnsTrue
            (
            )
        {
            bool Near = ToolPolygonGeometry.IsNearVertexScreen(
                NEAR_SCREEN_X,
                NEAR_SCREEN_Y,
                NEAR_SCREEN_X + 10f,
                NEAR_SCREEN_Y + 10f);

            Assert.True(Near);
        }

        /// <summary>
        /// Verifies locations outside the close-proximity threshold are not near.
        /// </summary>
        [Fact]
        public void IsNearVertexScreen_BeyondThreshold_ReturnsFalse
            (
            )
        {
            bool Near = ToolPolygonGeometry.IsNearVertexScreen(
                NEAR_SCREEN_X,
                NEAR_SCREEN_Y,
                NEAR_SCREEN_X + FAR_SCREEN_OFFSET_PX,
                NEAR_SCREEN_Y);

            Assert.False(Near);
        }

        /// <summary>
        /// Verifies a simple convex ring is not self-intersecting.
        /// </summary>
        [Fact]
        public void WouldClosedRingSelfIntersect_ConvexSquare_ReturnsFalse
            (
            )
        {
            ToolPolylineVertexRecord[] Vertices =
            {
                Vertex(BASE_LAT, BASE_LON),
                Vertex(BASE_LAT, BASE_LON + 0.001),
                Vertex(BASE_LAT + 0.001, BASE_LON + 0.001),
                Vertex(BASE_LAT + 0.001, BASE_LON),
            };

            Assert.False(ToolPolygonGeometry.WouldClosedRingSelfIntersect(Vertices));
        }

        /// <summary>
        /// Verifies a bow-tie ring is rejected as self-intersecting (§3.4.13).
        /// </summary>
        [Fact]
        public void WouldClosedRingSelfIntersect_BowTie_ReturnsTrue
            (
            )
        {
            ToolPolylineVertexRecord[] Vertices =
            {
                Vertex(BASE_LAT, BASE_LON),
                Vertex(BASE_LAT + 0.001, BASE_LON + 0.001),
                Vertex(BASE_LAT, BASE_LON + 0.001),
                Vertex(BASE_LAT + 0.001, BASE_LON),
            };

            Assert.True(ToolPolygonGeometry.WouldClosedRingSelfIntersect(Vertices));
        }

        /// <summary>
        /// Creates one test vertex at a geographic location.
        /// </summary>
        /// <param name="Latitude">Latitude in decimal degrees.</param>
        /// <param name="Longitude">Longitude in decimal degrees.</param>
        /// <returns>Vertex record.</returns>
        private static ToolPolylineVertexRecord Vertex
            (
            double Latitude,
            double Longitude
            )
        {
            return new ToolPolylineVertexRecord(Latitude, Longitude, ELEVATION_M);
        }
    }
}
