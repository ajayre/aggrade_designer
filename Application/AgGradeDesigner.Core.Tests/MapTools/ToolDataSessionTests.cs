using AgGradeDesigner.Core.MapTools;

namespace AgGradeDesigner.Core.Tests.MapTools
{
    /// <summary>
    /// Unit tests for <see cref="ToolDataSession"/> lifecycle rules (TOOLS-SPEC §3.2.14).
    /// </summary>
    public sealed class ToolDataSessionTests
    {
        /// <summary>
        /// Verifies that completing an empty in-progress polyline stores nothing (TOOLS-SPEC §3.2.14).
        /// </summary>
        [Fact]
        public void CompleteCurrentPolyline_WithZeroVertices_DoesNotStorePolyline
            (
            )
        {
            ToolDataSession Session = new ToolDataSession();

            Session.CompleteCurrentPolyline();

            Assert.False(Session.HasData);
            Assert.Empty(Session.Polylines);
            Assert.Empty(Session.CurrentPolylineVertices);
        }

        /// <summary>
        /// Verifies that completing a polyline with vertices retains one completed polyline.
        /// </summary>
        [Fact]
        public void CompleteCurrentPolyline_WithVertices_StoresCompletedPolyline
            (
            )
        {
            ToolDataSession Session = new ToolDataSession();
            Session.AppendPolylineVertex(51.0, -114.0, 100.0);
            Session.AppendPolylineVertex(51.1, -114.1, 101.0);

            Session.CompleteCurrentPolyline();

            Assert.Single(Session.Polylines);
            Assert.Equal(2, Session.Polylines[0].Vertices.Count);
            Assert.Empty(Session.CurrentPolylineVertices);
        }
    }
}
