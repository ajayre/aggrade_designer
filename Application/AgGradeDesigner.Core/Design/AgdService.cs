using System.Globalization;
using System.Text;

using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Survey;

namespace AgGradeDesigner.Core.Design
{
    /// <summary>
    /// Survey, design parameters, and grid produced by importing an AGD comma-separated file.
    /// </summary>
    public sealed class AgdImportResult
    {
        /// <summary>Benchmarks and topology points read from the AGD file.</summary>
        public SurveyModel Survey { get; set; } = new SurveyModel();

        /// <summary>Design parameters; populated with defaults when inferred from AGD grid rows.</summary>
        public DesignParameters Parameters { get; set; } = new DesignParameters();

        /// <summary>Bin grid reconstructed from AGD elevation rows in UTM coordinates.</summary>
        public DesignGrid Grid { get; set; } = new DesignGrid();
    }

    /// <summary>
    /// Imports and exports Trimble-style AGD comma-separated field design files.
    /// </summary>
    static public class AgdService
    {
        /// <summary>Expected CSV header line for AGD export.</summary>
        private const string HEADER_LINE =
            "Latitude (deg), Longitude (deg), Elevation Existing(m), Elevation Proposed(m), CutFill(m), Code, Comments";

        /// <summary>
        /// Reads an AGD file into survey points, benchmarks, and a reconstructed design grid.
        /// </summary>
        /// <param name="FilePath">Path to the AGD comma-separated file.</param>
        /// <returns>Imported survey, parameters, and grid.</returns>
        /// <exception cref="InvalidDataException">File is empty or field crosses UTM zone boundaries.</exception>
        static public AgdImportResult Import
            (
            string FilePath
            )
        {
            string[] Lines = File.ReadAllLines(FilePath);
            if (Lines.Length < 2)
                throw new InvalidDataException("AGD file is empty.");

            AgdImportResult Result = new AgdImportResult();
            List<(double Lat, double Lon, double Existing, double Proposed, string Code)> DataRows =
                new List<(double Lat, double Lon, double Existing, double Proposed, string Code)>();

            // Skip row zero (header); process each data line independently.
            for (Int32 LineIndex = 1; LineIndex < Lines.Length; LineIndex++)
            {
                string Line = Lines[LineIndex].Trim();
                if (Line.Length == 0)
                    continue;

                string[] Fields = Line.Split(',');
                for (Int32 FieldIndex = 0; FieldIndex < Fields.Length; FieldIndex++)
                    Fields[FieldIndex] = Fields[FieldIndex].Trim();

                if (Fields.Length < 4)
                    continue;

                double Lat = ParseDouble(Fields[0]);
                double Lon = ParseDouble(Fields[1]);
                string Code = Fields.Length > 5 ? Fields[5] : "";

                if (IsBenchmarkCode(Code))
                {
                    double Existing = ParseOptionalDouble(Fields, 2);
                    Result.Survey.Benchmarks.Add(new Benchmark(Lat, Lon, Code, Existing));
                    continue;
                }

                if (!TryParseOptionalDouble(Fields, 2, out double ExistingM) ||
                    !TryParseOptionalDouble(Fields, 3, out double ProposedM))
                    continue;

                DataRows.Add((Lat, Lon, ExistingM, ProposedM, Code));
            }

            foreach ((double Lat, double Lon, double Existing, double Proposed, string Code) Row in DataRows)
            {
                TopologyPoint Pt = new TopologyPoint
                {
                    Latitude = Row.Lat,
                    Longitude = Row.Lon,
                    ExistingElevation = Row.Existing,
                    ProposedElevation = Row.Proposed,
                    CutFill = Row.Proposed - Row.Existing,
                    Code = Row.Code
                };

                // Boundary perimeter uses code 2PER; all other topology rows are interior.
                if (string.Equals(Row.Code, "2PER", StringComparison.OrdinalIgnoreCase))
                    Result.Survey.BoundaryPoints.Add(Pt);
                else
                    Result.Survey.InteriorPoints.Add(Pt);
            }

            BuildGridFromAgdRows(Result, DataRows);
            return Result;
        }

        /// <summary>
        /// Writes survey benchmarks and valid grid bins to an AGD comma-separated file.
        /// </summary>
        /// <param name="FilePath">Destination file path.</param>
        /// <param name="Survey">Survey containing benchmarks to export first.</param>
        /// <param name="Grid">Design grid whose valid bins become data rows.</param>
        /// <param name="Parameters">Design parameters (reserved for future AGD fields).</param>
        static public void Export
            (
            string FilePath,
            SurveyModel Survey,
            DesignGrid Grid,
            DesignParameters Parameters
            )
        {
            StringBuilder Sb = new StringBuilder();
            Sb.AppendLine(HEADER_LINE);

            // List main benchmark rows before secondary benchmarks for AGD reader compatibility.
            foreach (Benchmark Bm in Survey.Benchmarks.OrderBy(Benchmark =>
                Benchmark.Name.Contains("mb", StringComparison.OrdinalIgnoreCase) ? 0 : 1))
            {
                Sb.AppendLine(string.Join(",",
                    Bm.Location.Latitude.ToString("F8", CultureInfo.InvariantCulture),
                    Bm.Location.Longitude.ToString("F8", CultureInfo.InvariantCulture),
                    Bm.Elevation.ToString("F3", CultureInfo.InvariantCulture),
                    "",
                    "",
                    Bm.Name,
                    ""));
            }

            IEnumerable<GridBinData> ValidBins = Grid.Bins
                .Where(Bin => Bin.Valid && Bin.ExistingM != FieldConstants.BIN_NO_DATA_SENTINEL)
                .OrderBy(Bin => Bin.Y * Grid.Width + Bin.X);

            foreach (GridBinData Bin in ValidBins)
            {
                double CutFill = Bin.ProposedM - Bin.ExistingM;
                Sb.AppendLine(string.Join(",",
                    Bin.CentroidLat.ToString("F8", CultureInfo.InvariantCulture),
                    Bin.CentroidLon.ToString("F8", CultureInfo.InvariantCulture),
                    Bin.ExistingM.ToString("F3", CultureInfo.InvariantCulture),
                    Bin.ProposedM.ToString("F3", CultureInfo.InvariantCulture),
                    CutFill.ToString("F3", CultureInfo.InvariantCulture),
                    "3GRD",
                    ""));
            }

            File.WriteAllText(FilePath, Sb.ToString(), Encoding.UTF8);
        }

        /// <summary>
        /// Builds a UTM-aligned design grid from parsed AGD data rows and attaches it to the import result.
        /// </summary>
        /// <param name="Result">Import result receiving grid and default parameters.</param>
        /// <param name="Rows">Latitude/longitude rows with existing and proposed elevations.</param>
        /// <exception cref="InvalidDataException">Rows span more than one UTM zone or hemisphere.</exception>
        static private void BuildGridFromAgdRows
            (
            AgdImportResult Result,
            List<(double Lat, double Lon, double Existing, double Proposed, string Code)> Rows
            )
        {
            if (Rows.Count == 0)
                return;

            double MinLat = Rows.Min(Row => Row.Lat);
            double MinLon = Rows.Min(Row => Row.Lon);
            UTM.UTMCoordinate Sw = UTM.FromLatLon(MinLat, MinLon);
            int Zone = Sw.Zone;
            bool North = Sw.IsNorthernHemisphere;

            double MinX = double.MaxValue, MinY = double.MaxValue, MaxX = double.MinValue, MaxY = double.MinValue;
            List<(int Bx, int By, double Existing, double Proposed, double Lat, double Lon)> UtmRows =
                new List<(int Bx, int By, double Existing, double Proposed, double Lat, double Lon)>();

            foreach ((double Lat, double Lon, double Existing, double Proposed, string Code) Row in Rows)
            {
                UTM.UTMCoordinate C = UTM.FromLatLon(Row.Lat, Row.Lon);
                if ((C.Zone != Zone) || (C.IsNorthernHemisphere != North))
                    throw new InvalidDataException("Field crosses UTM zone boundaries.");

                MinX = Math.Min(MinX, C.Easting);
                MinY = Math.Min(MinY, C.Northing);
                MaxX = Math.Max(MaxX, C.Easting);
                MaxY = Math.Max(MaxY, C.Northing);

                int Bx = (int)Math.Floor((C.Easting - MinX) / FieldConstants.BIN_SIZE_M);
                int By = (int)Math.Floor((C.Northing - MinY) / FieldConstants.BIN_SIZE_M);
                UtmRows.Add((Bx, By, Row.Existing, Row.Proposed, Row.Lat, Row.Lon));
            }

            // Pad grid extent by one bin on the minimum edge so centroids stay inside bounds.
            MinX -= FieldConstants.BIN_SIZE_M;
            MinY -= FieldConstants.BIN_SIZE_M;
            int Width = (int)Math.Ceiling((MaxX - MinX) / FieldConstants.BIN_SIZE_M) + 1;
            int Height = (int)Math.Ceiling((MaxY - MinY) / FieldConstants.BIN_SIZE_M) + 1;
            MaxX = MinX + Width * FieldConstants.BIN_SIZE_M;
            MaxY = MinY + Height * FieldConstants.BIN_SIZE_M;

            DesignGrid Grid = new DesignGrid
            {
                Width = Width,
                Height = Height,
                UtmZone = Zone,
                UtmNorth = North,
                OriginEastingM = MinX,
                OriginNorthingM = MinY,
                MinX = MinX,
                MinY = MinY,
                MaxX = MaxX,
                MaxY = MaxY,
                PlaneA = 0,
                PlaneB = 0,
                PlaneC = 0
            };

            foreach ((int Bx, int By, double Existing, double Proposed, double Lat, double Lon) Row in UtmRows)
            {
                double Cx = MinX + (Row.Bx + 0.5) * FieldConstants.BIN_SIZE_M;
                double Cy = MinY + (Row.By + 0.5) * FieldConstants.BIN_SIZE_M;
                UTM.ToLatLon(Zone, North, Cx, Cy, out double CentroidLat, out double CentroidLon);
                Grid.Bins.Add(new GridBinData
                {
                    X = Row.Bx,
                    Y = Row.By,
                    ExistingM = Row.Existing,
                    ProposedM = Row.Proposed,
                    CentroidLat = CentroidLat,
                    CentroidLon = CentroidLon,
                    Valid = true
                });
            }

            Result.Grid = Grid;
            Result.Parameters = new DesignParameters();
        }

        /// <summary>
        /// Returns whether a point code denotes a survey benchmark rather than a grid row.
        /// </summary>
        /// <param name="Code">AGD point code column value.</param>
        /// <returns>True when the code contains MB or BM (case-insensitive).</returns>
        static private bool IsBenchmarkCode
            (
            string Code
            )
        {
            if (string.IsNullOrWhiteSpace(Code))
                return false;
            string UpperCode = Code.ToUpperInvariant();
            return UpperCode.Contains("MB") || UpperCode.Contains("BM");
        }

        /// <summary>
        /// Parses a required floating-point field from CSV text.
        /// </summary>
        /// <param name="Text">Field text to parse.</param>
        /// <returns>Parsed double value.</returns>
        static private double ParseDouble
            (
            string Text
            )
        {
            return double.Parse(Text, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Parses an optional floating-point CSV field, returning zero when absent or blank.
        /// </summary>
        /// <param name="Fields">Split CSV columns for one row.</param>
        /// <param name="Index">Zero-based column index.</param>
        /// <returns>Parsed value or zero.</returns>
        static private double ParseOptionalDouble
            (
            string[] Fields,
            int Index
            )
        {
            return TryParseOptionalDouble(Fields, Index, out double Parsed) ? Parsed : 0;
        }

        /// <summary>
        /// Attempts to parse an optional floating-point CSV field.
        /// </summary>
        /// <param name="Fields">Split CSV columns for one row.</param>
        /// <param name="Index">Zero-based column index.</param>
        /// <param name="Value">Parsed value when the attempt succeeds.</param>
        /// <returns>True when the column exists, is non-blank, and parses as a double.</returns>
        static private bool TryParseOptionalDouble
            (
            string[] Fields,
            int Index,
            out double Value
            )
        {
            Value = 0;
            if ((Index >= Fields.Length) || string.IsNullOrWhiteSpace(Fields[Index]))
                return false;
            return double.TryParse(Fields[Index], NumberStyles.Float, CultureInfo.InvariantCulture, out Value);
        }
    }
}
