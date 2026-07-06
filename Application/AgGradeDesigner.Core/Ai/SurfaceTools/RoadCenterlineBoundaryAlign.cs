using AgGradeDesigner.Core.Models;

using NetTopologySuite.Geometries;
using NetTopologySuite.LinearReferencing;
using NetTopologySuite.Operation.Distance;

namespace AgGradeDesigner.Core.Ai.SurfaceTools
{
    /// <summary>
    /// Aligns road centerlines so the outer crown edge sits on the field boundary while
    /// following curved or straight boundary segments.
    /// </summary>
    static internal class RoadCenterlineBoundaryAlign
    {
        /// <summary>Chainage step as a fraction of bin size when densifying centerlines.</summary>
        private const double CHAINAGE_STEP_BIN_FRACTION = 0.5;

        /// <summary>Minimum distinct vertices required in an aligned centerline.</summary>
        private const int MIN_ALIGNED_VERTEX_COUNT = 2;

        /// <summary>Shared geometry factory for UTM line strings.</summary>
        private static readonly GeometryFactory GeometryFactory = new GeometryFactory();

        /// <summary>
        /// Offsets a centerline inward from the field boundary so the outer crown edge is flush
        /// with the boundary while preserving curved edge geometry.
        /// </summary>
        /// <param name="Grid">Design grid.</param>
        /// <param name="BoundaryRing">Closed field boundary ring.</param>
        /// <param name="FieldCentroid">Interior reference point in UTM metres.</param>
        /// <param name="Centerline">Requested centerline.</param>
        /// <param name="CrownWidthM">Full flat crown width in metres.</param>
        /// <param name="Enabled">False to return the original centerline unchanged.</param>
        /// <returns>Boundary-aligned centerline.</returns>
        static internal LineString AlignCrownToFieldEdge
            (
            DesignGrid Grid,
            LinearRing BoundaryRing,
            Coordinate FieldCentroid,
            LineString Centerline,
            double CrownWidthM,
            bool Enabled
            )
        {
            if (!Enabled || Centerline.Coordinates.Length < MIN_ALIGNED_VERTEX_COUNT || CrownWidthM <= 0.0)
                return Centerline;

            Polygon FieldPolygon = GeometryFactory.CreatePolygon(BoundaryRing);
            double HalfCrownM = CrownWidthM * 0.5;
            double InsideReachM = HalfCrownM + Grid.BinSizeM;
            double OutsideReachM = Math.Max(CrownWidthM + Grid.BinSizeM * 4.0, Grid.BinSizeM * 8.0);
            LengthIndexedLine IndexedBoundary = new LengthIndexedLine(BoundaryRing);

            List<Coordinate> RequestedPoints = CollectRequestedPoints(Centerline, Grid);
            List<Coordinate> Aligned = new List<Coordinate>(RequestedPoints.Count);

            foreach (Coordinate Point in RequestedPoints)
            {
                if (TryAlignPoint(
                        Point,
                        FieldPolygon,
                        BoundaryRing,
                        IndexedBoundary,
                        FieldCentroid,
                        HalfCrownM,
                        InsideReachM,
                        OutsideReachM,
                        out Coordinate AlignedPoint))
                {
                    Aligned.Add(AlignedPoint);
                }
                else
                {
                    Aligned.Add(new Coordinate(Point.X, Point.Y));
                }
            }

            List<Coordinate> Reduced = CollapseDuplicateVertices(Aligned);
            if (Reduced.Count < MIN_ALIGNED_VERTEX_COUNT)
                return Centerline;

            return GeometryFactory.CreateLineString(Reduced.ToArray());
        }

        /// <summary>
        /// Collects requested centerline vertices plus densified samples along each span.
        /// </summary>
        /// <param name="Centerline">Requested centerline.</param>
        /// <param name="Grid">Design grid.</param>
        /// <returns>Ordered sample points.</returns>
        static private List<Coordinate> CollectRequestedPoints
            (
            LineString Centerline,
            DesignGrid Grid
            )
        {
            List<Coordinate> Points = new List<Coordinate>();
            Coordinate[] Vertices = Centerline.Coordinates;
            Points.Add(new Coordinate(Vertices[0].X, Vertices[0].Y));

            for (int Index = 1; Index < Vertices.Length; Index++)
            {
                Coordinate Start = Vertices[Index - 1];
                Coordinate End = Vertices[Index];
                double SpanM = Start.Distance(End);
                double StepM = Math.Max(Grid.BinSizeM * CHAINAGE_STEP_BIN_FRACTION, 0.1);
                int StepCount = Math.Max(1, (int)Math.Ceiling(SpanM / StepM));
                for (int Step = 1; Step <= StepCount; Step++)
                {
                    double Fraction = Step / (double)StepCount;
                    Points.Add(new Coordinate(
                        Start.X + ((End.X - Start.X) * Fraction),
                        Start.Y + ((End.Y - Start.Y) * Fraction)));
                }
            }

            return Points;
        }

        /// <summary>
        /// Aligns one centerline sample to an inward offset from the nearest boundary point.
        /// </summary>
        /// <param name="Point">Requested sample point.</param>
        /// <param name="FieldPolygon">Field boundary polygon.</param>
        /// <param name="BoundaryRing">Closed field boundary ring.</param>
        /// <param name="IndexedBoundary">Length-indexed boundary ring.</param>
        /// <param name="FieldCentroid">Interior reference point.</param>
        /// <param name="HalfCrownM">Half of the crown width in metres.</param>
        /// <param name="InsideReachM">Maximum inward distance for edge-intent samples.</param>
        /// <param name="OutsideReachM">Maximum outward distance for edge-intent samples.</param>
        /// <param name="AlignedPoint">Aligned centerline point.</param>
        /// <returns>True when the sample was aligned to the boundary.</returns>
        static private bool TryAlignPoint
            (
            Coordinate Point,
            Polygon FieldPolygon,
            LinearRing BoundaryRing,
            LengthIndexedLine IndexedBoundary,
            Coordinate FieldCentroid,
            double HalfCrownM,
            double InsideReachM,
            double OutsideReachM,
            out Coordinate AlignedPoint
            )
        {
            AlignedPoint = new Coordinate(Point.X, Point.Y);
            Point SamplePoint = GeometryFactory.CreatePoint(Point);
            double DistanceToBoundaryM = BoundaryRing.Distance(SamplePoint);
            bool IsInsideField = FieldPolygon.Contains(SamplePoint);

            if (IsInsideField)
            {
                if (DistanceToBoundaryM > InsideReachM)
                    return false;
            }
            else if (DistanceToBoundaryM > OutsideReachM)
            {
                return false;
            }

            Coordinate[] Nearest = DistanceOp.NearestPoints(BoundaryRing, SamplePoint);
            Coordinate BoundaryPoint = Nearest[0];
            if (!TryComputeInwardNormal(BoundaryRing, IndexedBoundary, BoundaryPoint, FieldCentroid, out Coordinate InwardNormal))
                return false;

            AlignedPoint = new Coordinate(
                BoundaryPoint.X + (InwardNormal.X * HalfCrownM),
                BoundaryPoint.Y + (InwardNormal.Y * HalfCrownM));
            return true;
        }

        /// <summary>
        /// Computes a unit normal at a boundary point that points toward the field interior.
        /// </summary>
        /// <param name="BoundaryRing">Closed field boundary ring.</param>
        /// <param name="IndexedBoundary">Length-indexed boundary ring.</param>
        /// <param name="BoundaryPoint">Point on the boundary ring.</param>
        /// <param name="FieldCentroid">Interior reference point.</param>
        /// <param name="InwardNormal">Unit normal pointing into the field.</param>
        /// <returns>True when a usable inward normal was computed.</returns>
        static private bool TryComputeInwardNormal
            (
            LinearRing BoundaryRing,
            LengthIndexedLine IndexedBoundary,
            Coordinate BoundaryPoint,
            Coordinate FieldCentroid,
            out Coordinate InwardNormal
            )
        {
            InwardNormal = new Coordinate(0.0, 0.0);
            double LengthM = BoundaryRing.Length;
            if (LengthM <= 0.0)
                return false;

            double Location = IndexedBoundary.Project(BoundaryPoint);
            double DeltaM = Math.Max(0.1, LengthM * 0.01);
            Coordinate Before = IndexedBoundary.ExtractPoint(Math.Max(0.0, Location - DeltaM));
            Coordinate After = IndexedBoundary.ExtractPoint(Math.Min(LengthM, Location + DeltaM));
            double TangentX = After.X - Before.X;
            double TangentY = After.Y - Before.Y;
            double TangentLength = Math.Sqrt((TangentX * TangentX) + (TangentY * TangentY));
            if (TangentLength <= 1e-9)
                return false;

            TangentX /= TangentLength;
            TangentY /= TangentLength;

            Coordinate LeftNormal = new Coordinate(-TangentY, TangentX);
            Coordinate RightNormal = new Coordinate(TangentY, -TangentX);
            double ToCentroidX = FieldCentroid.X - BoundaryPoint.X;
            double ToCentroidY = FieldCentroid.Y - BoundaryPoint.Y;
            double LeftDot = (LeftNormal.X * ToCentroidX) + (LeftNormal.Y * ToCentroidY);
            InwardNormal = LeftDot >= 0.0 ? LeftNormal : RightNormal;
            return true;
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
