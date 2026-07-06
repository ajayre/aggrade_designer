using AgGradeDesigner.Core.Models;

using NetTopologySuite.Geometries;
using NetTopologySuite.LinearReferencing;

namespace AgGradeDesigner.Core.Ai.SurfaceTools
{
    /// <summary>
    /// Snaps requested road centerlines onto valid field bins when geometry lies on the grid
    /// envelope but outside the actual valid bin footprint.
    /// </summary>
    static internal class RoadCenterlineFieldSnap
    {
        /// <summary>Offset from bin index to bin centre in cell units.</summary>
        private const double BIN_CELL_CENTRE_OFFSET = 0.5;

        /// <summary>Chainage step as a fraction of bin size when densifying centerlines.</summary>
        private const double CHAINAGE_STEP_BIN_FRACTION = 0.5;

        /// <summary>Minimum distinct vertices required in a snapped centerline.</summary>
        private const int MIN_SNAPPED_VERTEX_COUNT = 2;

        /// <summary>Shared geometry factory for UTM line strings.</summary>
        private static readonly GeometryFactory GeometryFactory = new GeometryFactory();

        /// <summary>
        /// Projects a centerline onto nearby valid bin centroids so corridor tools affect the field
        /// rather than empty grid envelope padding.
        /// </summary>
        /// <param name="Grid">Design grid.</param>
        /// <param name="Centerline">Requested centerline from the tool caller.</param>
        /// <param name="MaxSnapDistanceM">Maximum snap distance in metres.</param>
        /// <returns>Centerline adjusted to follow valid bin coverage where needed.</returns>
        static internal LineString SnapToValidField
            (
            DesignGrid Grid,
            LineString Centerline,
            double MaxSnapDistanceM
            )
        {
            if (Centerline.Coordinates.Length < MIN_SNAPPED_VERTEX_COUNT || MaxSnapDistanceM <= 0.0)
                return Centerline;

            LengthIndexedLine Indexed = new LengthIndexedLine(Centerline);
            double LengthM = Centerline.Length;
            if (LengthM <= 0.0)
                return Centerline;

            double StepM = Math.Max(Grid.BinSizeM * CHAINAGE_STEP_BIN_FRACTION, 0.1);
            int SampleCount = Math.Max(MIN_SNAPPED_VERTEX_COUNT, (int)Math.Ceiling(LengthM / StepM) + 1);
            List<Coordinate> Snapped = new List<Coordinate>(SampleCount);

            for (int Index = 0; Index < SampleCount; Index++)
            {
                double ChainageM = Math.Min(Index * StepM, LengthM);
                Coordinate Point = Indexed.ExtractPoint(ChainageM);
                if (TrySnapToNearestValidBinCentroid(Grid, Point.X, Point.Y, MaxSnapDistanceM, out Coordinate SnappedPoint))
                    Snapped.Add(SnappedPoint);
                else
                    Snapped.Add(new Coordinate(Point.X, Point.Y));
            }

            List<Coordinate> Reduced = CollapseDuplicateVertices(Snapped);
            if (Reduced.Count < MIN_SNAPPED_VERTEX_COUNT)
                return Centerline;

            return GeometryFactory.CreateLineString(Reduced.ToArray());
        }

        /// <summary>
        /// Finds the nearest valid bin centroid within the snap radius of a UTM point.
        /// </summary>
        /// <param name="Grid">Design grid.</param>
        /// <param name="EastingM">Sample easting in metres.</param>
        /// <param name="NorthingM">Sample northing in metres.</param>
        /// <param name="MaxSnapDistanceM">Maximum snap distance in metres.</param>
        /// <param name="SnappedPoint">Nearest valid bin centroid when found.</param>
        /// <returns>True when a valid bin centroid was found within range.</returns>
        static private bool TrySnapToNearestValidBinCentroid
            (
            DesignGrid Grid,
            double EastingM,
            double NorthingM,
            double MaxSnapDistanceM,
            out Coordinate SnappedPoint
            )
        {
            SnappedPoint = new Coordinate(EastingM, NorthingM);
            int CenterX = (int)Math.Floor((EastingM - Grid.OriginEastingM) / Grid.BinSizeM - BIN_CELL_CENTRE_OFFSET);
            int CenterY = (int)Math.Floor((NorthingM - Grid.OriginNorthingM) / Grid.BinSizeM - BIN_CELL_CENTRE_OFFSET);
            int SearchRadiusBins = (int)Math.Ceiling(MaxSnapDistanceM / Grid.BinSizeM) + 1;

            double BestDistanceM = double.MaxValue;
            bool Found = false;

            for (int DeltaY = -SearchRadiusBins; DeltaY <= SearchRadiusBins; DeltaY++)
            {
                for (int DeltaX = -SearchRadiusBins; DeltaX <= SearchRadiusBins; DeltaX++)
                {
                    if (!Grid.TryGetValidBin(CenterX + DeltaX, CenterY + DeltaY, out GridBinData Bin))
                        continue;

                    double BinEastingM = Grid.OriginEastingM + (Bin.X + BIN_CELL_CENTRE_OFFSET) * Grid.BinSizeM;
                    double BinNorthingM = Grid.OriginNorthingM + (Bin.Y + BIN_CELL_CENTRE_OFFSET) * Grid.BinSizeM;
                    double DeltaEastingM = EastingM - BinEastingM;
                    double DeltaNorthingM = NorthingM - BinNorthingM;
                    double DistanceM = Math.Sqrt((DeltaEastingM * DeltaEastingM) + (DeltaNorthingM * DeltaNorthingM));
                    if (DistanceM > MaxSnapDistanceM || DistanceM >= BestDistanceM)
                        continue;

                    BestDistanceM = DistanceM;
                    SnappedPoint = new Coordinate(BinEastingM, BinNorthingM);
                    Found = true;
                }
            }

            return Found;
        }

        /// <summary>
        /// Removes consecutive duplicate vertices from a polyline.
        /// </summary>
        /// <param name="Vertices">Ordered vertices.</param>
        /// <returns>Deduplicated vertex list.</returns>
        static private List<Coordinate> CollapseDuplicateVertices
            (
            IReadOnlyList<Coordinate> Vertices
            )
        {
            List<Coordinate> Reduced = new List<Coordinate>(Vertices.Count);
            foreach (Coordinate Vertex in Vertices)
            {
                if (Reduced.Count > 0 &&
                    Math.Abs(Reduced[^1].X - Vertex.X) < 1e-6 &&
                    Math.Abs(Reduced[^1].Y - Vertex.Y) < 1e-6)
                    continue;

                Reduced.Add(Vertex);
            }

            return Reduced;
        }
    }
}
