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

        /// <summary>
        /// Verifies that closing a valid polygon stores one completed feature (§3.4.16).
        /// </summary>
        [Fact]
        public void TryCloseCurrentPolygon_WithValidTriangle_StoresCompletedPolygon
            (
            )
        {
            ToolDataSession Session = new ToolDataSession();
            Session.AppendPolygonVertex(51.0, -114.0, 100.0);
            Session.AppendPolygonVertex(51.0, -113.999, 100.0);
            Session.AppendPolygonVertex(51.001, -113.9995, 100.0);

            Assert.True(Session.TryCloseCurrentPolygon());

            Assert.Single(Session.Polygons);
            Assert.Equal(3, Session.Polygons[0].Vertices.Count);
            Assert.Empty(Session.CurrentPolygonVertices);
        }

        /// <summary>
        /// Verifies that a self-intersecting close attempt leaves the in-progress polygon unchanged (§3.4.15).
        /// </summary>
        [Fact]
        public void TryCloseCurrentPolygon_WithSelfIntersection_DoesNotClose
            (
            )
        {
            ToolDataSession Session = new ToolDataSession();
            Session.AppendPolygonVertex(51.0, -114.0, 100.0);
            Session.AppendPolygonVertex(51.001, -113.999, 100.0);
            Session.AppendPolygonVertex(51.0, -113.999, 100.0);
            Session.AppendPolygonVertex(51.001, -114.0, 100.0);

            Assert.False(Session.TryCloseCurrentPolygon());

            Assert.Empty(Session.Polygons);
            Assert.Equal(4, Session.CurrentPolygonVertices.Count);
        }

        /// <summary>
        /// Verifies that deactivating with fewer than three vertices discards the in-progress polygon (§3.4.19).
        /// </summary>
        [Fact]
        public void CompleteCurrentPolygon_WithTwoVertices_DiscardsInProgress
            (
            )
        {
            ToolDataSession Session = new ToolDataSession();
            Session.AppendPolygonVertex(51.0, -114.0, 100.0);
            Session.AppendPolygonVertex(51.001, -114.0, 100.0);

            Session.CompleteCurrentPolygon();

            Assert.Empty(Session.Polygons);
            Assert.Empty(Session.CurrentPolygonVertices);
        }
    }
}
