using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Survey;

using NetTopologySuite.Geometries;

using NtsCoordinate = NetTopologySuite.Geometries.Coordinate;

namespace AgGradeDesigner.Core.Ai.SurfaceTools
{
    /// <summary>
    /// Resolves field boundary rings used for geometry planning and road edge alignment.
    /// </summary>
    static internal class FieldBoundaryGeometry
    {
        /// <summary>Minimum boundary vertices required for a survey perimeter ring.</summary>
        private const int MIN_BOUNDARY_VERTEX_COUNT = 3;

        /// <summary>Shared geometry factory for UTM boundary operations.</summary>
        private static readonly GeometryFactory GeometryFactory = new GeometryFactory();

        /// <summary>
        /// Resolves the field boundary ring and interior reference point for road alignment.
        /// </summary>
        /// <param name="Project">Optional project document for survey boundaries.</param>
        /// <param name="Grid">Design grid.</param>
        /// <param name="BoundaryRing">Closed boundary ring when available.</param>
        /// <param name="FieldCentroid">Interior reference point in UTM metres.</param>
        /// <returns>True when a usable boundary ring was resolved.</returns>
        static internal bool TryResolveBoundaryRing
            (
            ProjectDocument? Project,
            DesignGrid Grid,
            out LinearRing? BoundaryRing,
            out NtsCoordinate FieldCentroid
            )
        {
            BoundaryRing = null;
            FieldCentroid = ComputeFieldCentroid(Grid);

            List<(double X, double Y)> Vertices = ResolveBoundaryVertices(Project, Grid);
            if (Vertices.Count < MIN_BOUNDARY_VERTEX_COUNT)
                return false;

            FieldCentroid = ComputeBoundaryCentroid(Vertices, FieldCentroid);
            BoundaryRing = BuildClosedRing(Vertices);
            return BoundaryRing != null;
        }

        /// <summary>
        /// Resolves ordered UTM boundary vertices for geometry payloads and planning.
        /// </summary>
        /// <param name="Project">Active project document.</param>
        /// <param name="Grid">Design grid.</param>
        /// <param name="BoundarySource">Boundary provenance token.</param>
        /// <returns>Ordered UTM boundary vertices.</returns>
        static internal List<(double X, double Y)> ResolveBoundaryVertices
            (
            ProjectDocument Project,
            DesignGrid Grid,
            out string BoundarySource
            )
        {
            SurveyModel? Survey = Project.Survey;
            if (Survey != null && Survey.BoundaryPoints.Count >= MIN_BOUNDARY_VERTEX_COUNT)
            {
                BoundarySource = "survey";
                return BuildSurveyBoundaryVertices(Survey);
            }

            BoundarySource = "valid_bin_outer_extents";
            return BuildValidBinOuterExtentRectangle(Grid);
        }

        /// <summary>
        /// Computes axis-aligned extents from valid bin outer edges.
        /// </summary>
        /// <param name="Grid">Design grid.</param>
        /// <param name="MinEastingM">Minimum outer easting.</param>
        /// <param name="MaxEastingM">Maximum outer easting.</param>
        /// <param name="MinNorthingM">Minimum outer northing.</param>
        /// <param name="MaxNorthingM">Maximum outer northing.</param>
        /// <returns>True when at least one valid bin exists.</returns>
        static internal bool TryComputeValidBinOuterExtents
            (
            DesignGrid Grid,
            out double MinEastingM,
            out double MaxEastingM,
            out double MinNorthingM,
            out double MaxNorthingM
            )
        {
            MinEastingM = double.MaxValue;
            MaxEastingM = double.MinValue;
            MinNorthingM = double.MaxValue;
            MaxNorthingM = double.MinValue;
            bool HasValidBin = false;

            foreach (GridBinData Bin in Grid.Bins)
            {
                if (!Bin.Valid)
                    continue;

                HasValidBin = true;
                double MinE = Grid.OriginEastingM + Bin.X * Grid.BinSizeM;
                double MaxE = MinE + Grid.BinSizeM;
                double MinN = Grid.OriginNorthingM + Bin.Y * Grid.BinSizeM;
                double MaxN = MinN + Grid.BinSizeM;
                MinEastingM = Math.Min(MinEastingM, MinE);
                MaxEastingM = Math.Max(MaxEastingM, MaxE);
                MinNorthingM = Math.Min(MinNorthingM, MinN);
                MaxNorthingM = Math.Max(MaxNorthingM, MaxN);
            }

            return HasValidBin;
        }

        /// <summary>
        /// Computes axis-aligned extents from valid bin centroids.
        /// </summary>
        /// <param name="Grid">Design grid.</param>
        /// <param name="MinEastingM">Minimum centroid easting.</param>
        /// <param name="MaxEastingM">Maximum centroid easting.</param>
        /// <param name="MinNorthingM">Minimum centroid northing.</param>
        /// <param name="MaxNorthingM">Maximum centroid northing.</param>
        /// <returns>True when at least one valid bin exists.</returns>
        static internal bool TryComputeValidBinCentroidExtents
            (
            DesignGrid Grid,
            out double MinEastingM,
            out double MaxEastingM,
            out double MinNorthingM,
            out double MaxNorthingM
            )
        {
            MinEastingM = double.MaxValue;
            MaxEastingM = double.MinValue;
            MinNorthingM = double.MaxValue;
            MaxNorthingM = double.MinValue;
            bool HasValidBin = false;

            foreach (GridBinData Bin in Grid.Bins)
            {
                if (!Bin.Valid)
                    continue;

                HasValidBin = true;
                double EastingM = Grid.OriginEastingM + (Bin.X + 0.5) * Grid.BinSizeM;
                double NorthingM = Grid.OriginNorthingM + (Bin.Y + 0.5) * Grid.BinSizeM;
                MinEastingM = Math.Min(MinEastingM, EastingM);
                MaxEastingM = Math.Max(MaxEastingM, EastingM);
                MinNorthingM = Math.Min(MinNorthingM, NorthingM);
                MaxNorthingM = Math.Max(MaxNorthingM, NorthingM);
            }

            return HasValidBin;
        }

        /// <summary>
        /// Resolves boundary vertices without a project, using valid-bin outer extents only.
        /// </summary>
        /// <param name="Grid">Design grid.</param>
        /// <returns>Ordered UTM boundary vertices.</returns>
        static private List<(double X, double Y)> ResolveBoundaryVertices
            (
            ProjectDocument? Project,
            DesignGrid Grid
            )
        {
            if (Project != null)
            {
                SurveyModel? Survey = Project.Survey;
                if (Survey != null && Survey.BoundaryPoints.Count >= MIN_BOUNDARY_VERTEX_COUNT)
                    return BuildSurveyBoundaryVertices(Survey);
            }

            return BuildValidBinOuterExtentRectangle(Grid);
        }

        /// <summary>
        /// Builds a rectangle from valid bin outer edges.
        /// </summary>
        /// <param name="Grid">Design grid.</param>
        /// <returns>Clockwise UTM rectangle vertices.</returns>
        static private List<(double X, double Y)> BuildValidBinOuterExtentRectangle
            (
            DesignGrid Grid
            )
        {
            if (!TryComputeValidBinOuterExtents(Grid, out double MinE, out double MaxE, out double MinN, out double MaxN))
            {
                return new List<(double X, double Y)>
                {
                    (Grid.MinX, Grid.MinY),
                    (Grid.MaxX, Grid.MinY),
                    (Grid.MaxX, Grid.MaxY),
                    (Grid.MinX, Grid.MaxY)
                };
            }

            return new List<(double X, double Y)>
            {
                (MinE, MinN),
                (MaxE, MinN),
                (MaxE, MaxN),
                (MinE, MaxN)
            };
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
        /// Builds a closed linear ring from ordered UTM vertices.
        /// </summary>
        /// <param name="Vertices">Ordered boundary vertices.</param>
        /// <returns>Closed ring when valid.</returns>
        static private LinearRing? BuildClosedRing
            (
            IReadOnlyList<(double X, double Y)> Vertices
            )
        {
            if (Vertices.Count < MIN_BOUNDARY_VERTEX_COUNT)
                return null;

            NtsCoordinate[] Coordinates = new NtsCoordinate[Vertices.Count + 1];
            for (int Index = 0; Index < Vertices.Count; Index++)
                Coordinates[Index] = new NtsCoordinate(Vertices[Index].X, Vertices[Index].Y);

            Coordinates[^1] = new NtsCoordinate(Vertices[0].X, Vertices[0].Y);
            return GeometryFactory.CreateLinearRing(Coordinates);
        }

        /// <summary>
        /// Computes a coarse field centroid from valid bin centroids.
        /// </summary>
        /// <param name="Grid">Design grid.</param>
        /// <returns>Centroid coordinate in UTM metres.</returns>
        static private NtsCoordinate ComputeFieldCentroid
            (
            DesignGrid Grid
            )
        {
            if (!TryComputeValidBinCentroidExtents(Grid, out double MinE, out double MaxE, out double MinN, out double MaxN))
            {
                return new NtsCoordinate(
                    (Grid.MinX + Grid.MaxX) * 0.5,
                    (Grid.MinY + Grid.MaxY) * 0.5);
            }

            return new NtsCoordinate((MinE + MaxE) * 0.5, (MinN + MaxN) * 0.5);
        }

        /// <summary>
        /// Computes the centroid of a boundary ring, falling back to the supplied point.
        /// </summary>
        /// <param name="Vertices">Boundary vertices in UTM metres.</param>
        /// <param name="Fallback">Fallback centroid when the ring is empty.</param>
        /// <returns>Centroid easting and northing in metres.</returns>
        static private NtsCoordinate ComputeBoundaryCentroid
            (
            IReadOnlyList<(double X, double Y)> Vertices,
            NtsCoordinate Fallback
            )
        {
            if (Vertices.Count == 0)
                return Fallback;

            double SumX = 0.0;
            double SumY = 0.0;
            foreach ((double X, double Y) Vertex in Vertices)
            {
                SumX += Vertex.X;
                SumY += Vertex.Y;
            }

            return new NtsCoordinate(SumX / Vertices.Count, SumY / Vertices.Count);
        }
    }
}
