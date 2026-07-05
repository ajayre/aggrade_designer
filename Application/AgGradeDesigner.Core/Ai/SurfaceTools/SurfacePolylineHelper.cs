using System.Text.Json;

using AgGradeDesigner.Core.Models;

using NetTopologySuite.Geometries;
using NetTopologySuite.Operation.Buffer;
using NetTopologySuite.LinearReferencing;

namespace AgGradeDesigner.Core.Ai.SurfaceTools
{
    /// <summary>
    /// Parses centerline location arguments and computes distances along polylines.
    /// </summary>
    static public class SurfacePolylineHelper
    {
        /// <summary>Minimum vertex count for a polyline.</summary>
        private const int MIN_POLYLINE_VERTEX_COUNT = 2;

        /// <summary>Offset from bin index to bin centre in cell units.</summary>
        private const double BIN_CELL_CENTRE_OFFSET = 0.5;

        /// <summary>Shared geometry factory for UTM operations.</summary>
        private static readonly GeometryFactory GeometryFactory = new GeometryFactory();

        /// <summary>
        /// Parses a road or corridor centerline from tool JSON arguments.
        /// </summary>
        /// <param name="Args">Tool arguments object.</param>
        /// <param name="Centerline">Parsed centerline geometry.</param>
        /// <param name="Error">Error message when parsing fails.</param>
        /// <returns>True when a centerline was resolved.</returns>
        static public bool TryParseCenterline
            (
            JsonElement Args,
            out LineString? Centerline,
            out string? Error
            )
        {
            Centerline = null;
            Error = null;

            if (Args.TryGetProperty("centerline", out JsonElement CenterlineElement))
                return TryParsePolyline(CenterlineElement, out Centerline, out Error);

            if (Args.TryGetProperty("start_point", out JsonElement StartElement) &&
                Args.TryGetProperty("end_point", out JsonElement EndElement))
            {
                if (!SurfacePolygonHelper.TryReadPoint(StartElement, out double StartX, out double StartY, out Error))
                    return false;

                if (!SurfacePolygonHelper.TryReadPoint(EndElement, out double EndX, out double EndY, out Error))
                    return false;

                Centerline = GeometryFactory.CreateLineString(new[]
                {
                    new Coordinate(StartX, StartY),
                    new Coordinate(EndX, EndY)
                });
                return true;
            }

            Error = "Provide 'centerline' as an ordered UTM vertex array, or 'start_point' and 'end_point' for a straight segment.";
            return false;
        }

        /// <summary>
        /// Parses a polyline JSON array into a line string.
        /// </summary>
        /// <param name="PolylineElement">JSON array of point objects.</param>
        /// <param name="Centerline">Parsed centerline.</param>
        /// <param name="Error">Error message when parsing fails.</param>
        /// <returns>True when parsing succeeds.</returns>
        static public bool TryParsePolyline
            (
            JsonElement PolylineElement,
            out LineString? Centerline,
            out string? Error
            )
        {
            Centerline = null;
            Error = null;

            if (PolylineElement.ValueKind != JsonValueKind.Array ||
                PolylineElement.GetArrayLength() < MIN_POLYLINE_VERTEX_COUNT)
            {
                Error = "Argument 'centerline' must be an array with at least two points.";
                return false;
            }

            List<Coordinate> Coordinates = new List<Coordinate>();
            foreach (JsonElement VertexElement in PolylineElement.EnumerateArray())
            {
                if (!SurfacePolygonHelper.TryReadPoint(VertexElement, out double X, out double Y, out Error))
                    return false;

                Coordinates.Add(new Coordinate(X, Y));
            }

            Centerline = GeometryFactory.CreateLineString(Coordinates.ToArray());
            return true;
        }

        /// <summary>
        /// Returns the perpendicular distance from a point to a centerline and the chainage to the closest point.
        /// </summary>
        /// <param name="Centerline">Road centerline.</param>
        /// <param name="EastingM">Point easting in metres.</param>
        /// <param name="NorthingM">Point northing in metres.</param>
        /// <param name="ChainageM">Distance along the centerline to the closest point.</param>
        /// <param name="PerpendicularDistanceM">Perpendicular distance from the point to the centerline.</param>
        /// <param name="SignedOffsetM">Signed perpendicular offset; positive is left of the line direction.</param>
        static public void MeasureRelativeToCenterline
            (
            LineString Centerline,
            double EastingM,
            double NorthingM,
            out double ChainageM,
            out double PerpendicularDistanceM,
            out double SignedOffsetM
            )
        {
            Point Point = GeometryFactory.CreatePoint(new Coordinate(EastingM, NorthingM));
            LengthIndexedLine IndexedLine = new LengthIndexedLine(Centerline);
            double Index = IndexedLine.Project(Point.Coordinate);
            Coordinate Closest = IndexedLine.ExtractPoint(Index);
            ChainageM = Index;
            PerpendicularDistanceM = Point.Distance(GeometryFactory.CreatePoint(Closest));

            double LineHeadingRad = ComputeSegmentHeadingRad(Centerline, Index);
            double DeltaE = EastingM - Closest.X;
            double DeltaN = NorthingM - Closest.Y;
            double Cross = Math.Cos(LineHeadingRad) * DeltaN - Math.Sin(LineHeadingRad) * DeltaE;
            SignedOffsetM = Math.Sign(Cross) * PerpendicularDistanceM;
        }

        /// <summary>
        /// Samples the existing surface elevation at the closest point on the centerline.
        /// </summary>
        /// <param name="Grid">Design grid.</param>
        /// <param name="Centerline">Road centerline.</param>
        /// <param name="ChainageM">Chainage along the centerline in metres.</param>
        /// <returns>Existing elevation in metres at the sampled location.</returns>
        static public double SampleExistingAtChainage
            (
            DesignGrid Grid,
            LineString Centerline,
            double ChainageM
            )
        {
            LengthIndexedLine IndexedLine = new LengthIndexedLine(Centerline);
            Coordinate Coordinate = IndexedLine.ExtractPoint(ChainageM);
            return SampleExistingAtPoint(Grid, Coordinate.X, Coordinate.Y);
        }

        /// <summary>
        /// Samples the proposed surface elevation at the closest point on the centerline.
        /// </summary>
        /// <param name="Grid">Design grid.</param>
        /// <param name="Centerline">Road centerline.</param>
        /// <param name="ChainageM">Chainage along the centerline in metres.</param>
        /// <returns>Proposed elevation in metres at the sampled location.</returns>
        static public double SampleProposedAtChainage
            (
            DesignGrid Grid,
            LineString Centerline,
            double ChainageM
            )
        {
            LengthIndexedLine IndexedLine = new LengthIndexedLine(Centerline);
            Coordinate Coordinate = IndexedLine.ExtractPoint(ChainageM);
            return SampleProposedAtPoint(Grid, Coordinate.X, Coordinate.Y);
        }

        /// <summary>
        /// Samples a baseline proposed elevation at the closest point on the centerline.
        /// </summary>
        /// <param name="Grid">Design grid.</param>
        /// <param name="Centerline">Road centerline.</param>
        /// <param name="ChainageM">Chainage along the centerline in metres.</param>
        /// <param name="BaselineProposedM">Proposed elevations captured before road build.</param>
        /// <returns>Baseline proposed elevation in metres at the sampled location.</returns>
        static public double SampleBaselineProposedAtChainage
            (
            DesignGrid Grid,
            LineString Centerline,
            double ChainageM,
            IReadOnlyDictionary<(int X, int Y), double> BaselineProposedM
            )
        {
            LengthIndexedLine IndexedLine = new LengthIndexedLine(Centerline);
            Coordinate Coordinate = IndexedLine.ExtractPoint(ChainageM);
            return SampleBaselineProposedAtPoint(Grid, Coordinate.X, Coordinate.Y, BaselineProposedM);
        }

        /// <summary>
        /// Samples the proposed surface elevation at a UTM point using the nearest valid bin.
        /// </summary>
        /// <param name="Grid">Design grid.</param>
        /// <param name="EastingM">UTM easting in metres.</param>
        /// <param name="NorthingM">UTM northing in metres.</param>
        /// <returns>Proposed elevation in metres.</returns>
        static public double SampleProposedAtPoint
            (
            DesignGrid Grid,
            double EastingM,
            double NorthingM
            )
        {
            if (!TryFindNearestValidBin(Grid, EastingM, NorthingM, out GridBinData Bin))
                return 0.0;

            return Bin.ProposedM;
        }

        /// <summary>
        /// Samples a baseline proposed elevation at a UTM point using the nearest valid bin.
        /// </summary>
        /// <param name="Grid">Design grid.</param>
        /// <param name="EastingM">UTM easting in metres.</param>
        /// <param name="NorthingM">UTM northing in metres.</param>
        /// <param name="BaselineProposedM">Proposed elevations captured before road build.</param>
        /// <returns>Baseline proposed elevation in metres.</returns>
        static public double SampleBaselineProposedAtPoint
            (
            DesignGrid Grid,
            double EastingM,
            double NorthingM,
            IReadOnlyDictionary<(int X, int Y), double> BaselineProposedM
            )
        {
            if (!TryFindNearestValidBin(Grid, EastingM, NorthingM, out GridBinData Bin))
                return 0.0;

            return BaselineProposedM.TryGetValue((Bin.X, Bin.Y), out double ElevationM)
                ? ElevationM
                : Bin.ProposedM;
        }

        /// <summary>
        /// Samples the existing surface elevation at a UTM point using the nearest valid bin.
        /// </summary>
        /// <param name="Grid">Design grid.</param>
        /// <param name="EastingM">UTM easting in metres.</param>
        /// <param name="NorthingM">UTM northing in metres.</param>
        /// <returns>Existing elevation in metres.</returns>
        static public double SampleExistingAtPoint
            (
            DesignGrid Grid,
            double EastingM,
            double NorthingM
            )
        {
            if (!TryFindNearestValidBin(Grid, EastingM, NorthingM, out GridBinData Bin))
                return 0.0;

            return Bin.ExistingM;
        }

        /// <summary>
        /// Builds a corridor polygon by buffering the centerline by half the total width.
        /// </summary>
        /// <param name="Centerline">Road centerline.</param>
        /// <param name="TotalWidthM">Full corridor width in metres.</param>
        /// <returns>Buffered corridor polygon.</returns>
        static public Polygon BuildCorridorPolygon
            (
            LineString Centerline,
            double TotalWidthM
            )
        {
            Geometry Buffer = Centerline.Buffer(TotalWidthM * 0.5, EndCapStyle.Flat);
            if (Buffer is Polygon Polygon)
                return Polygon;

            if (Buffer is GeometryCollection Collection && Collection.NumGeometries > 0 &&
                Collection.GetGeometryN(0) is Polygon FirstPolygon)
            {
                return FirstPolygon;
            }

            return GeometryFactory.CreatePolygon();
        }

        /// <summary>
        /// Serializes a polygon to the wire-format vertices array used by surface tools.
        /// </summary>
        /// <param name="Polygon">Polygon geometry.</param>
        /// <returns>Vertices suitable for JSON serialization.</returns>
        static public object[] SerializePolygonVertices
            (
            Polygon Polygon
            )
        {
            Coordinate[] Coordinates = Polygon.ExteriorRing.Coordinates;
            List<object> Vertices = new List<object>();
            int Limit = Coordinates.Length;
            if (Limit > 1 &&
                Coordinates[0].X == Coordinates[Limit - 1].X &&
                Coordinates[0].Y == Coordinates[Limit - 1].Y)
            {
                Limit--;
            }

            for (int Index = 0; Index < Limit; Index++)
            {
                Vertices.Add(new { x = Coordinates[Index].X, y = Coordinates[Index].Y });
            }

            return Vertices.ToArray();
        }

        /// <summary>
        /// Returns the heading in radians of the centerline segment containing the supplied chainage.
        /// </summary>
        /// <param name="Centerline">Road centerline.</param>
        /// <param name="ChainageM">Chainage in metres.</param>
        /// <returns>Heading in radians, zero along +X when degenerate.</returns>
        static private double ComputeSegmentHeadingRad
            (
            LineString Centerline,
            double ChainageM
            )
        {
            Coordinate[] Coordinates = Centerline.Coordinates;
            double Accumulated = 0.0;
            for (int Index = 0; Index < Coordinates.Length - 1; Index++)
            {
                Coordinate Start = Coordinates[Index];
                Coordinate End = Coordinates[Index + 1];
                double SegmentLength = Start.Distance(End);
                if (ChainageM <= Accumulated + SegmentLength || Index == Coordinates.Length - 2)
                    return Math.Atan2(End.Y - Start.Y, End.X - Start.X);

                Accumulated += SegmentLength;
            }

            return 0.0;
        }

        /// <summary>
        /// Finds the nearest valid bin centroid to a UTM point.
        /// </summary>
        /// <param name="Grid">Design grid.</param>
        /// <param name="EastingM">UTM easting in metres.</param>
        /// <param name="NorthingM">UTM northing in metres.</param>
        /// <param name="Bin">Nearest valid bin.</param>
        /// <returns>True when a valid bin exists.</returns>
        static private bool TryFindNearestValidBin
            (
            DesignGrid Grid,
            double EastingM,
            double NorthingM,
            out GridBinData Bin
            )
        {
            Bin = default!;
            double BestDistanceSq = double.MaxValue;
            bool Found = false;

            foreach (GridBinData Candidate in Grid.Bins)
            {
                if (!Candidate.Valid)
                    continue;

                double CandidateEasting = Grid.OriginEastingM + (Candidate.X + BIN_CELL_CENTRE_OFFSET) * Grid.BinSizeM;
                double CandidateNorthing = Grid.OriginNorthingM + (Candidate.Y + BIN_CELL_CENTRE_OFFSET) * Grid.BinSizeM;
                double DeltaE = CandidateEasting - EastingM;
                double DeltaN = CandidateNorthing - NorthingM;
                double DistanceSq = DeltaE * DeltaE + DeltaN * DeltaN;
                if (DistanceSq >= BestDistanceSq)
                    continue;

                BestDistanceSq = DistanceSq;
                Bin = Candidate;
                Found = true;
            }

            return Found;
        }
    }
}
