using NetTopologySuite.Geometries;

namespace AgGradeDesigner.Core.Ai.SurfaceTools
{
    /// <summary>
    /// Removes high-frequency snap artifacts from road centerlines so perpendicular
    /// distance and side-slope daylighting vary smoothly along the corridor.
    /// </summary>
    static internal class RoadCenterlineSmoothing
    {
        /// <summary>Minimum vertex count required before smoothing is applied.</summary>
        private const int MIN_SMOOTH_VERTEX_COUNT = 3;

        /// <summary>
        /// Maximum deviation from a chord line, in bin-size units, before a polyline is
        /// treated as curved rather than straight.
        /// </summary>
        private const double STRAIGHT_LINE_MAX_DEVIATION_BIN_FRACTION = 2.5;

        /// <summary>Minimum moving-average radius in vertices.</summary>
        private const int MIN_MOVING_AVERAGE_RADIUS = 2;

        /// <summary>Maximum moving-average radius in vertices.</summary>
        private const int MAX_MOVING_AVERAGE_RADIUS = 12;

        /// <summary>Target smoothing window width in metres for curved centerlines.</summary>
        private const double MOVING_AVERAGE_WINDOW_M = 2.0;

        /// <summary>Shared geometry factory for UTM line strings.</summary>
        private static readonly GeometryFactory GeometryFactory = new GeometryFactory();

        /// <summary>
        /// Builds a measurement centerline with snap jitter removed while preserving endpoints
        /// and long-radius curvature.
        /// </summary>
        /// <param name="Centerline">Snapped or aligned centerline.</param>
        /// <param name="BinSizeM">Design grid bin size in metres.</param>
        /// <returns>Smoothed centerline for corridor distance calculations.</returns>
        static internal LineString CreateMeasurementLine
            (
            LineString Centerline,
            double BinSizeM
            )
        {
            Coordinate[] Points = Centerline.Coordinates;
            if (Points.Length < MIN_SMOOTH_VERTEX_COUNT || BinSizeM <= 0.0)
                return Centerline;

            if (TryCreateStraightLine(Points, BinSizeM, out LineString StraightLine))
                return StraightLine;

            return CreateMovingAverageLine(Centerline, BinSizeM);
        }

        /// <summary>
        /// Returns a straight chord when every interior vertex lies near the end-to-end segment.
        /// </summary>
        /// <param name="Points">Ordered centerline vertices.</param>
        /// <param name="BinSizeM">Design grid bin size in metres.</param>
        /// <param name="StraightLine">Straight measurement line when created.</param>
        /// <returns>True when the input is straight enough to collapse to a chord.</returns>
        static private bool TryCreateStraightLine
            (
            Coordinate[] Points,
            double BinSizeM,
            out LineString StraightLine
            )
        {
            StraightLine = CenterlinePlaceholder(Points);
            Coordinate Start = Points[0];
            Coordinate End = Points[^1];
            double MaxDeviationM = BinSizeM * STRAIGHT_LINE_MAX_DEVIATION_BIN_FRACTION;

            for (int Index = 1; Index < Points.Length - 1; Index++)
            {
                if (DistancePointToSegmentM(Points[Index], Start, End) > MaxDeviationM)
                    return false;
            }

            StraightLine = GeometryFactory.CreateLineString(new[] { Start, End });
            return true;
        }

        /// <summary>
        /// Applies a chainage-domain moving average to remove bin snap noise from curved roads.
        /// </summary>
        /// <param name="Centerline">Input centerline.</param>
        /// <param name="BinSizeM">Design grid bin size in metres.</param>
        /// <returns>Smoothed centerline.</returns>
        static private LineString CreateMovingAverageLine
            (
            LineString Centerline,
            double BinSizeM
            )
        {
            Coordinate[] Points = Centerline.Coordinates;
            double AverageSpacingM = Centerline.Length / Math.Max(1, Points.Length - 1);
            int Radius = (int)Math.Ceiling(MOVING_AVERAGE_WINDOW_M / Math.Max(AverageSpacingM, BinSizeM * 0.25));
            Radius = Math.Clamp(Radius, MIN_MOVING_AVERAGE_RADIUS, MAX_MOVING_AVERAGE_RADIUS);

            List<Coordinate> Smoothed = new List<Coordinate>(Points.Length);
            for (int Index = 0; Index < Points.Length; Index++)
            {
                int StartIndex = Math.Max(0, Index - Radius);
                int EndIndex = Math.Min(Points.Length - 1, Index + Radius);
                double SumX = 0.0;
                double SumY = 0.0;
                int Count = 0;
                for (int SampleIndex = StartIndex; SampleIndex <= EndIndex; SampleIndex++)
                {
                    SumX += Points[SampleIndex].X;
                    SumY += Points[SampleIndex].Y;
                    Count++;
                }

                Smoothed.Add(new Coordinate(SumX / Count, SumY / Count));
            }

            List<Coordinate> Reduced = CollapseDuplicateVertices(Smoothed);
            if (Reduced.Count < MIN_SMOOTH_VERTEX_COUNT)
                return Centerline;

            return GeometryFactory.CreateLineString(Reduced.ToArray());
        }

        /// <summary>
        /// Computes the shortest distance from a point to a line segment in metres.
        /// </summary>
        /// <param name="Point">Point to measure.</param>
        /// <param name="Start">Segment start.</param>
        /// <param name="End">Segment end.</param>
        /// <returns>Perpendicular distance in metres.</returns>
        static private double DistancePointToSegmentM
            (
            Coordinate Point,
            Coordinate Start,
            Coordinate End
            )
        {
            double SegmentDeltaX = End.X - Start.X;
            double SegmentDeltaY = End.Y - Start.Y;
            double SegmentLengthSq = (SegmentDeltaX * SegmentDeltaX) + (SegmentDeltaY * SegmentDeltaY);
            if (SegmentLengthSq <= 1e-12)
                return Point.Distance(Start);

            double Projection = (((Point.X - Start.X) * SegmentDeltaX) + ((Point.Y - Start.Y) * SegmentDeltaY)) / SegmentLengthSq;
            Projection = Math.Clamp(Projection, 0.0, 1.0);
            double ClosestX = Start.X + (Projection * SegmentDeltaX);
            double ClosestY = Start.Y + (Projection * SegmentDeltaY);
            double DeltaX = Point.X - ClosestX;
            double DeltaY = Point.Y - ClosestY;
            return Math.Sqrt((DeltaX * DeltaX) + (DeltaY * DeltaY));
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

        /// <summary>
        /// Returns the input line unchanged for failed straight-line detection fallbacks.
        /// </summary>
        /// <param name="Points">Input vertices.</param>
        /// <returns>Placeholder line string.</returns>
        static private LineString CenterlinePlaceholder
            (
            Coordinate[] Points
            )
        {
            return GeometryFactory.CreateLineString(Points);
        }
    }
}
