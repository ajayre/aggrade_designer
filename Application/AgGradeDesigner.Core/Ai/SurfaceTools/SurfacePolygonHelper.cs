using System.Text.Json;

using AgGradeDesigner.Core.Models;

using NetTopologySuite.Geometries;

namespace AgGradeDesigner.Core.Ai.SurfaceTools
{
    /// <summary>
    /// Parses polygon tool arguments and tests bin centroids against polygon geometry.
    /// </summary>
    static public class SurfacePolygonHelper
    {
        /// <summary>Minimum vertex count for a closed polygon ring.</summary>
        private const int MIN_POLYGON_VERTEX_COUNT = 3;

        /// <summary>Offset from bin index to bin centre in cell units.</summary>
        private const double BIN_CELL_CENTRE_OFFSET = 0.5;

        /// <summary>Shared geometry factory for UTM polygon operations.</summary>
        private static readonly GeometryFactory GeometryFactory = new GeometryFactory();

        /// <summary>
        /// Parses a polygon argument object into a NetTopologySuite polygon.
        /// </summary>
        /// <param name="PolygonElement">JSON polygon object with a vertices array.</param>
        /// <param name="Polygon">Parsed polygon geometry.</param>
        /// <param name="Error">Error message when parsing fails.</param>
        /// <returns>True when parsing succeeds.</returns>
        static public bool TryParsePolygon
            (
            JsonElement PolygonElement,
            out Polygon? Polygon,
            out string? Error
            )
        {
            Polygon = null;
            Error = null;

            if (PolygonElement.ValueKind != JsonValueKind.Object)
            {
                Error = "Argument 'polygon' must be an object with a vertices array.";
                return false;
            }

            if (!PolygonElement.TryGetProperty("vertices", out JsonElement VerticesElement) ||
                VerticesElement.ValueKind != JsonValueKind.Array)
            {
                Error = "Argument 'polygon.vertices' must be an array with at least three points.";
                return false;
            }

            if (VerticesElement.GetArrayLength() < MIN_POLYGON_VERTEX_COUNT)
            {
                Error = "Argument 'polygon.vertices' must contain at least three points.";
                return false;
            }

            List<Coordinate> Coordinates = new List<Coordinate>();
            foreach (JsonElement VertexElement in VerticesElement.EnumerateArray())
            {
                if (!TryReadPoint(VertexElement, out double X, out double Y, out string? PointError))
                {
                    Error = PointError;
                    return false;
                }

                Coordinates.Add(new Coordinate(X, Y));
            }

            // intent: close the ring explicitly so NTS treats the boundary as a filled area
            Coordinate First = Coordinates[0];
            Coordinate Last = Coordinates[Coordinates.Count - 1];
            if (First.X != Last.X || First.Y != Last.Y)
                Coordinates.Add(new Coordinate(First.X, First.Y));

            LinearRing Ring = GeometryFactory.CreateLinearRing(Coordinates.ToArray());
            Polygon = GeometryFactory.CreatePolygon(Ring);
            return true;
        }

        /// <summary>
        /// Parses a point with optional elevation from tool JSON.
        /// </summary>
        /// <param name="PointElement">JSON point object.</param>
        /// <param name="X">UTM easting in metres.</param>
        /// <param name="Y">UTM northing in metres.</param>
        /// <param name="Z">Elevation in metres when present.</param>
        /// <param name="HasZ">True when elevation was supplied.</param>
        /// <param name="Error">Error message when parsing fails.</param>
        /// <returns>True when x and y are present.</returns>
        static public bool TryReadPointXyz
            (
            JsonElement PointElement,
            out double X,
            out double Y,
            out double Z,
            out bool HasZ,
            out string? Error
            )
        {
            X = 0.0;
            Y = 0.0;
            Z = 0.0;
            HasZ = false;
            Error = null;

            if (!TryReadPoint(PointElement, out X, out Y, out Error))
                return false;

            if (PointElement.TryGetProperty("z", out JsonElement ZElement) ||
                PointElement.TryGetProperty("elevation_m", out ZElement))
            {
                if (!ZElement.TryGetDouble(out Z))
                {
                    Error = "Point elevation must be numeric.";
                    return false;
                }

                HasZ = true;
            }

            return true;
        }

        /// <summary>
        /// Returns true when the bin centroid lies inside the polygon, or when no polygon filter is supplied.
        /// </summary>
        /// <param name="Grid">Design grid.</param>
        /// <param name="Bin">Bin to test.</param>
        /// <param name="Polygon">Optional polygon filter.</param>
        /// <returns>True when the bin should be included in a polygon-scoped edit.</returns>
        static public bool IsBinIncluded
            (
            DesignGrid Grid,
            GridBinData Bin,
            Polygon? Polygon
            )
        {
            if (Polygon == null)
                return true;

            double EastingM = Grid.OriginEastingM + (Bin.X + BIN_CELL_CENTRE_OFFSET) * Grid.BinSizeM;
            double NorthingM = Grid.OriginNorthingM + (Bin.Y + BIN_CELL_CENTRE_OFFSET) * Grid.BinSizeM;
            Point Point = GeometryFactory.CreatePoint(new Coordinate(EastingM, NorthingM));
            return Polygon.Contains(Point);
        }

        /// <summary>
        /// Reads x and y from a JSON point object.
        /// </summary>
        /// <param name="PointElement">JSON point object.</param>
        /// <param name="X">UTM easting in metres.</param>
        /// <param name="Y">UTM northing in metres.</param>
        /// <param name="Error">Error message when parsing fails.</param>
        /// <returns>True when x and y are numeric.</returns>
        static private bool TryReadPoint
            (
            JsonElement PointElement,
            out double X,
            out double Y,
            out string? Error
            )
        {
            X = 0.0;
            Y = 0.0;
            Error = null;

            if (PointElement.ValueKind != JsonValueKind.Object)
            {
                Error = "Expected a point object with x and y.";
                return false;
            }

            if (!PointElement.TryGetProperty("x", out JsonElement XElement) || !XElement.TryGetDouble(out X))
            {
                Error = "Point argument requires numeric 'x'.";
                return false;
            }

            if (!PointElement.TryGetProperty("y", out JsonElement YElement) || !YElement.TryGetDouble(out Y))
            {
                Error = "Point argument requires numeric 'y'.";
                return false;
            }

            return true;
        }
    }
}
