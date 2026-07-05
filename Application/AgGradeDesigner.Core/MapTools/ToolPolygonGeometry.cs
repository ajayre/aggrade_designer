using AgGradeDesigner.Core.Geo;

namespace AgGradeDesigner.Core.MapTools
{
    /// <summary>
    /// Plan-view polygon helpers for close proximity and self-intersection checks (TOOLS-SPEC-ADDENDUM1 §3.4.6, §3.4.13).
    /// </summary>
    static public class ToolPolygonGeometry
    {
        /// <summary>
        /// Returns whether a screen pointer lies within close proximity of a vertex in viewport pixels.
        /// </summary>
        /// <param name="PointerX">Pointer horizontal screen coordinate in pixels.</param>
        /// <param name="PointerY">Pointer vertical screen coordinate in pixels.</param>
        /// <param name="VertexX">Vertex horizontal screen coordinate in pixels.</param>
        /// <param name="VertexY">Vertex vertical screen coordinate in pixels.</param>
        /// <param name="ThresholdPx">Maximum screen distance treated as close proximity.</param>
        /// <returns>True when the screen distance is within the close-proximity threshold.</returns>
        static public bool IsNearVertexScreen
            (
            float PointerX,
            float PointerY,
            float VertexX,
            float VertexY,
            float ThresholdPx = ToolDataConstants.POLYGON_CLOSE_PROXIMITY_PX
            )
        {
            float DeltaX = PointerX - VertexX;
            float DeltaY = PointerY - VertexY;
            float ThresholdSq = ThresholdPx * ThresholdPx;
            return ((DeltaX * DeltaX) + (DeltaY * DeltaY)) <= ThresholdSq;
        }

        /// <summary>
        /// Returns whether a closed vertex ring would self-intersect (§3.4.13, §3.4.15).
        /// </summary>
        /// <param name="Vertices">Ordered boundary vertices; the ring closes from last back to first.</param>
        /// <returns>True when two non-adjacent boundary segments cross.</returns>
        static public bool WouldClosedRingSelfIntersect
            (
            IReadOnlyList<ToolPolylineVertexRecord> Vertices
            )
        {
            int VertexCount = Vertices.Count;
            if (VertexCount < GeoConstants.MIN_POLYGON_VERTICES)
                return false;

            int SegmentCount = VertexCount;
            for (Int32 SegmentIndexA = 0; SegmentIndexA < SegmentCount; SegmentIndexA++)
            {
                for (Int32 SegmentIndexB = SegmentIndexA + 1; SegmentIndexB < SegmentCount; SegmentIndexB++)
                {
                    if (AreAdjacentSegments(SegmentIndexA, SegmentIndexB, SegmentCount))
                        continue;

                    if (SegmentsCross(
                            Vertices,
                            SegmentIndexA,
                            SegmentIndexB,
                            VertexCount))
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns whether two ring segment indices share an endpoint and are therefore adjacent.
        /// </summary>
        /// <param name="SegmentIndexA">First segment index.</param>
        /// <param name="SegmentIndexB">Second segment index.</param>
        /// <param name="SegmentCount">Total segment count around the closed ring.</param>
        /// <returns>True when the segments are adjacent.</returns>
        static private bool AreAdjacentSegments
            (
            int SegmentIndexA,
            int SegmentIndexB,
            int SegmentCount
            )
        {
            int Difference = Math.Abs(SegmentIndexA - SegmentIndexB);
            if (Difference <= ToolDataConstants.POLYGON_ADJACENT_SEGMENT_INDEX_DELTA)
                return true;

            if ((SegmentIndexA == ToolDataConstants.POLYGON_RING_FIRST_SEGMENT_INDEX) &&
                (SegmentIndexB == (SegmentCount - ToolDataConstants.POLYGON_RING_FIRST_SEGMENT_INDEX)))
                return true;

            if ((SegmentIndexB == ToolDataConstants.POLYGON_RING_FIRST_SEGMENT_INDEX) &&
                (SegmentIndexA == (SegmentCount - ToolDataConstants.POLYGON_RING_FIRST_SEGMENT_INDEX)))
                return true;

            return false;
        }

        /// <summary>
        /// Returns whether two ring segments intersect at a point other than a shared endpoint.
        /// </summary>
        /// <param name="Vertices">Ring vertices.</param>
        /// <param name="SegmentIndexA">First segment index.</param>
        /// <param name="SegmentIndexB">Second segment index.</param>
        /// <param name="VertexCount">Number of vertices in the ring.</param>
        /// <returns>True when the segments properly cross.</returns>
        static private bool SegmentsCross
            (
            IReadOnlyList<ToolPolylineVertexRecord> Vertices,
            int SegmentIndexA,
            int SegmentIndexB,
            int VertexCount
            )
        {
            (double Ax, double Ay) = ToPlanPoint(Vertices[SegmentIndexA]);
            (double Bx, double By) = ToPlanPoint(Vertices[(SegmentIndexA + 1) % VertexCount]);
            (double Cx, double Cy) = ToPlanPoint(Vertices[SegmentIndexB]);
            (double Dx, double Dy) = ToPlanPoint(Vertices[(SegmentIndexB + 1) % VertexCount]);

            double Orientation1 = Orientation(Ax, Ay, Bx, By, Cx, Cy);
            double Orientation2 = Orientation(Ax, Ay, Bx, By, Dx, Dy);
            double Orientation3 = Orientation(Cx, Cy, Dx, Dy, Ax, Ay);
            double Orientation4 = Orientation(Cx, Cy, Dx, Dy, Bx, By);

            if (((Orientation1 > ToolDataConstants.SEGMENT_INTERSECTION_EPSILON) &&
                    (Orientation2 < -ToolDataConstants.SEGMENT_INTERSECTION_EPSILON)) ||
                ((Orientation2 > ToolDataConstants.SEGMENT_INTERSECTION_EPSILON) &&
                    (Orientation1 < -ToolDataConstants.SEGMENT_INTERSECTION_EPSILON)))
            {
                if (((Orientation3 > ToolDataConstants.SEGMENT_INTERSECTION_EPSILON) &&
                        (Orientation4 < -ToolDataConstants.SEGMENT_INTERSECTION_EPSILON)) ||
                    ((Orientation4 > ToolDataConstants.SEGMENT_INTERSECTION_EPSILON) &&
                        (Orientation3 < -ToolDataConstants.SEGMENT_INTERSECTION_EPSILON)))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Converts one geographic vertex to a UTM plan coordinate.
        /// </summary>
        /// <param name="Vertex">Geographic vertex.</param>
        /// <returns>Easting and northing in metres.</returns>
        static private (double Easting, double Northing) ToPlanPoint
            (
            ToolPolylineVertexRecord Vertex
            )
        {
            UTM.UTMCoordinate Utm = UTM.FromLatLon(Vertex.Latitude, Vertex.Longitude);
            return (Utm.Easting, Utm.Northing);
        }

        /// <summary>
        /// Computes the signed orientation of ordered triplet (A, B, C) in the plan plane.
        /// </summary>
        /// <param name="Ax">First point easting.</param>
        /// <param name="Ay">First point northing.</param>
        /// <param name="Bx">Second point easting.</param>
        /// <param name="By">Second point northing.</param>
        /// <param name="Cx">Third point easting.</param>
        /// <param name="Cy">Third point northing.</param>
        /// <returns>Signed cross product magnitude.</returns>
        static private double Orientation
            (
            double Ax,
            double Ay,
            double Bx,
            double By,
            double Cx,
            double Cy
            )
        {
            return ((Bx - Ax) * (Cy - Ay)) - ((By - Ay) * (Cx - Ax));
        }
    }
}
