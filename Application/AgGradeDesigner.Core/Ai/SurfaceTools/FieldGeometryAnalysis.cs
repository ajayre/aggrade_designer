using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Survey;

namespace AgGradeDesigner.Core.Ai.SurfaceTools
{
    /// <summary>
    /// Builds field geometry payloads for <c>get_field_geometry</c>.
    /// </summary>
    static public class FieldGeometryAnalysis
    {
        /// <summary>Minimum boundary vertices required for a survey perimeter ring.</summary>
        private const int MIN_BOUNDARY_VERTEX_COUNT = 3;

        /// <summary>Offset from bin index to bin centre in cell units.</summary>
        private const double BIN_CELL_CENTRE_OFFSET = 0.5;

        /// <summary>
        /// Builds edge-following centerline polylines through valid bin centroids.
        /// </summary>
        /// <param name="Grid">Design grid.</param>
        /// <param name="EdgeName">Edge name: north, south, east, or west.</param>
        /// <returns>Ordered UTM vertices along the requested edge.</returns>
        static public List<(double X, double Y)> BuildValidBinEdgeCenterline
            (
            DesignGrid Grid,
            string EdgeName
            )
        {
            List<GridBinData> ValidBins = Grid.Bins.Where(B => B.Valid).ToList();
            if (ValidBins.Count == 0)
                return new List<(double X, double Y)>();

            IEnumerable<GridBinData> EdgeBins;
            string Normalized = EdgeName.Trim().ToLowerInvariant();
            switch (Normalized)
            {
                case "north":
                    int MaxY = ValidBins.Max(B => B.Y);
                    EdgeBins = ValidBins.Where(B => B.Y == MaxY).OrderBy(B => B.X);
                    break;
                case "south":
                    int MinY = ValidBins.Min(B => B.Y);
                    EdgeBins = ValidBins.Where(B => B.Y == MinY).OrderBy(B => B.X);
                    break;
                case "east":
                    int MaxX = ValidBins.Max(B => B.X);
                    EdgeBins = ValidBins.Where(B => B.X == MaxX).OrderBy(B => B.Y);
                    break;
                case "west":
                    int MinX = ValidBins.Min(B => B.X);
                    EdgeBins = ValidBins.Where(B => B.X == MinX).OrderBy(B => B.Y);
                    break;
                default:
                    return new List<(double X, double Y)>();
            }

            return EdgeBins
                .Select(B => (
                    Grid.OriginEastingM + (B.X + BIN_CELL_CENTRE_OFFSET) * Grid.BinSizeM,
                    Grid.OriginNorthingM + (B.Y + BIN_CELL_CENTRE_OFFSET) * Grid.BinSizeM))
                .ToList();
        }

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
            List<(double X, double Y)> BoundaryVertices = ResolveBoundaryVertices(Project, Summary, out string BoundarySource);
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
                edge_centerlines = new
                {
                    north = SerializeVertices(BuildValidBinEdgeCenterline(Grid, "north")),
                    south = SerializeVertices(BuildValidBinEdgeCenterline(Grid, "south")),
                    east = SerializeVertices(BuildValidBinEdgeCenterline(Grid, "east")),
                    west = SerializeVertices(BuildValidBinEdgeCenterline(Grid, "west"))
                },
                edge_centerlines_note =
                    "Use edge_centerlines for full-width roads along a field edge. These follow valid bin centroids, not the outer grid bounding box.",
                valid_bin_count = Grid.Bins.Count(B => B.Valid)
            };
        }

        /// <summary>
        /// Resolves the field boundary ring used for geometry planning.
        /// </summary>
        /// <param name="Project">Active project document.</param>
        /// <param name="Summary">Field summary metrics.</param>
        /// <param name="BoundarySource">Boundary provenance token.</param>
        /// <returns>Ordered UTM boundary vertices.</returns>
        static private List<(double X, double Y)> ResolveBoundaryVertices
            (
            ProjectDocument Project,
            SurfaceSummary Summary,
            out string BoundarySource
            )
        {
            SurveyModel? Survey = Project.Survey;
            if (Survey != null && Survey.BoundaryPoints.Count >= MIN_BOUNDARY_VERTEX_COUNT)
            {
                BoundarySource = "survey";
                return BuildSurveyBoundaryVertices(Survey);
            }

            BoundarySource = "grid_extent";
            return BuildGridExtentRectangle(Summary);
        }

        /// <summary>
        /// Converts ordered survey boundary points to a UTM vertex ring.
        /// </summary>
        /// <param name="Survey">Survey model containing boundary points.</param>
        /// <returns>Ordered UTM boundary vertices.</returns>
        static private List<(double X, double Y)> BuildSurveyBoundaryVertices
            (
            SurveyModel Survey
            )
        {
            List<(double Lat, double Lon)> LatLonRing = Survey.BoundaryPoints
                .Select(Point => (Point.Latitude, Point.Longitude))
                .ToList();
            List<(double Lat, double Lon)> OrderedLatLon = BoundaryMask.OrderBoundaryLatLon(LatLonRing);
            return BoundaryMask.BuildUtmRing(OrderedLatLon);
        }

        /// <summary>
        /// Builds a rectangular boundary from the design grid extents.
        /// </summary>
        /// <param name="Summary">Field summary metrics.</param>
        /// <returns>Clockwise UTM rectangle vertices.</returns>
        static private List<(double X, double Y)> BuildGridExtentRectangle
            (
            SurfaceSummary Summary
            )
        {
            return new List<(double X, double Y)>
            {
                (Summary.MinEastingM, Summary.MinNorthingM),
                (Summary.MaxEastingM, Summary.MinNorthingM),
                (Summary.MaxEastingM, Summary.MaxNorthingM),
                (Summary.MinEastingM, Summary.MaxNorthingM)
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
