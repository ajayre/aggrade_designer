using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Survey;

namespace AgGradeDesigner.Core.Ai.SurfaceTools
{
    /// <summary>
    /// Builds field geometry payloads for <c>get_field_geometry</c>.
    /// </summary>
    static public class FieldGeometryAnalysis
    {
        /// <summary>
        /// Builds the JSON payload for <c>get_field_geometry</c>.
        /// </summary>
        /// <param name="Project">Active project document.</param>
        /// <param name="Grid">Design grid.</param>
        /// <returns>Anonymous object ready for JSON serialization.</returns>
        static public object BuildPayload
            (
            ProjectDocument Project,
            DesignGrid Grid
            )
        {
            SurfaceSummary Summary = SurfaceAnalysis.BuildSurfaceSummary(Grid);
            List<(double X, double Y)> BoundaryVertices = FieldBoundaryGeometry.ResolveBoundaryVertices(
                Project,
                Grid,
                out string BoundarySource);
            object[] BoundaryPayload = SerializeVertices(BoundaryVertices);
            (double CentroidX, double CentroidY) = ComputeCentroid(BoundaryVertices, Summary);

            return new
            {
                coordinate_frame = "utm_metres",
                cell_size_m = Summary.CellSizeM,
                min_easting_m = Summary.MinEastingM,
                max_easting_m = Summary.MaxEastingM,
                min_northing_m = Summary.MinNorthingM,
                max_northing_m = Summary.MaxNorthingM,
                centroid = new { x = CentroidX, y = CentroidY },
                boundary_source = BoundarySource,
                boundary_vertex_count = BoundaryPayload.Length,
                boundary_vertices = BoundaryPayload,
                bbox_corners = BuildBboxCorners(Summary),
                bbox_corners_are_axis_aligned = true,
                bbox_corners_note =
                    "NE/NW/SE/SW corners are axis-aligned bounding-box corners. On irregular fields they may lie outside the true survey boundary; prefer boundary_vertices for edge-following geometry.",
                grid = new
                {
                    width_bins = Grid.Width,
                    height_bins = Grid.Height,
                    origin_easting_m = Grid.OriginEastingM,
                    origin_northing_m = Grid.OriginNorthingM,
                    bin_size_m = Grid.BinSizeM,
                    utm_zone = Grid.UtmZone,
                    utm_north = Grid.UtmNorth
                },
                valid_bin_extents = BuildValidBinCentroidExtents(Grid),
                valid_bin_outer_extents = BuildValidBinOuterExtents(Grid),
                valid_bin_count = Grid.Bins.Count(B => B.Valid),
                geometry_planning_note =
                    "Relative phrases (north edge, NE corner, into the center) are not tool parameters. " +
                    "Interpret the user's intent from boundary_vertices, valid_bin_outer_extents, centroid, and grid metadata, " +
                    "then supply UTM centerline vertices to road tools. Trace boundary_vertices with enough points to follow curved edges. " +
                    "build_road aligns the outer crown edge to the field boundary by default (align_crown_to_field_edge); " +
                    "place the centerline on or just outside the intended edge path. On irregular fields, min/max easting/northing are " +
                    "grid envelope padding; prefer boundary_vertices and valid_bin_outer_extents over bbox_corners."
            };
        }

        /// <summary>
        /// Builds axis-aligned extents from valid bin centroids.
        /// </summary>
        /// <param name="Grid">Design grid.</param>
        /// <returns>Valid-bin centroid extent metrics in UTM metres.</returns>
        static private object BuildValidBinCentroidExtents
            (
            DesignGrid Grid
            )
        {
            if (!FieldBoundaryGeometry.TryComputeValidBinCentroidExtents(Grid, out double MinE, out double MaxE, out double MinN, out double MaxN))
            {
                return new
                {
                    min_easting_m = Grid.MinX,
                    max_easting_m = Grid.MaxX,
                    min_northing_m = Grid.MinY,
                    max_northing_m = Grid.MaxY
                };
            }

            return new
            {
                min_easting_m = MinE,
                max_easting_m = MaxE,
                min_northing_m = MinN,
                max_northing_m = MaxN
            };
        }

        /// <summary>
        /// Builds axis-aligned extents from valid bin outer edges.
        /// </summary>
        /// <param name="Grid">Design grid.</param>
        /// <returns>Valid-bin outer extent metrics in UTM metres.</returns>
        static private object BuildValidBinOuterExtents
            (
            DesignGrid Grid
            )
        {
            if (!FieldBoundaryGeometry.TryComputeValidBinOuterExtents(Grid, out double MinE, out double MaxE, out double MinN, out double MaxN))
            {
                return new
                {
                    min_easting_m = Grid.MinX,
                    max_easting_m = Grid.MaxX,
                    min_northing_m = Grid.MinY,
                    max_northing_m = Grid.MaxY
                };
            }

            return new
            {
                min_easting_m = MinE,
                max_easting_m = MaxE,
                min_northing_m = MinN,
                max_northing_m = MaxN
            };
        }

        /// <summary>
        /// Computes the centroid of the boundary ring, falling back to the grid centre.
        /// </summary>
        /// <param name="BoundaryVertices">Boundary vertices in UTM metres.</param>
        /// <param name="Summary">Field summary metrics.</param>
        /// <returns>Centroid easting and northing in metres.</returns>
        static private (double X, double Y) ComputeCentroid
            (
            IReadOnlyList<(double X, double Y)> BoundaryVertices,
            SurfaceSummary Summary
            )
        {
            if (BoundaryVertices.Count == 0)
            {
                return (
                    (Summary.MinEastingM + Summary.MaxEastingM) * 0.5,
                    (Summary.MinNorthingM + Summary.MaxNorthingM) * 0.5);
            }

            double SumX = 0.0;
            double SumY = 0.0;
            foreach ((double X, double Y) Vertex in BoundaryVertices)
            {
                SumX += Vertex.X;
                SumY += Vertex.Y;
            }

            return (SumX / BoundaryVertices.Count, SumY / BoundaryVertices.Count);
        }

        /// <summary>
        /// Builds axis-aligned bounding-box corner labels for quick reference.
        /// </summary>
        /// <param name="Summary">Field summary metrics.</param>
        /// <returns>Named bbox corner coordinates.</returns>
        static private object BuildBboxCorners
            (
            SurfaceSummary Summary
            )
        {
            return new
            {
                sw = new { x = Summary.MinEastingM, y = Summary.MinNorthingM },
                se = new { x = Summary.MaxEastingM, y = Summary.MinNorthingM },
                ne = new { x = Summary.MaxEastingM, y = Summary.MaxNorthingM },
                nw = new { x = Summary.MinEastingM, y = Summary.MaxNorthingM }
            };
        }

        /// <summary>
        /// Serializes UTM vertices for JSON output.
        /// </summary>
        /// <param name="Vertices">Boundary vertices in UTM metres.</param>
        /// <returns>Vertex payload objects.</returns>
        static private object[] SerializeVertices
            (
            IReadOnlyList<(double X, double Y)> Vertices
            )
        {
            object[] Payload = new object[Vertices.Count];
            for (int Index = 0; Index < Vertices.Count; Index++)
            {
                Payload[Index] = new { x = Vertices[Index].X, y = Vertices[Index].Y };
            }

            return Payload;
        }
    }
}
