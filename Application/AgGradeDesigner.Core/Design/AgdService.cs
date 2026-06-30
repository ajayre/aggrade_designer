using System.Globalization;
using System.Text;

using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.IO;
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
            if (!File.Exists(FilePath))
                throw new FileNotFoundException("AGD file was not found.", FilePath);

            int EstimatedRows = DelimitedTextParser.EstimateRowCapacity(FilePath, AverageBytesPerRow: 48);
            AgdImportResult Result = new AgdImportResult();
            List<(double Lat, double Lon, double Existing, double Proposed, string Code)> DataRows =
                new List<(double Lat, double Lon, double Existing, double Proposed, string Code)>(EstimatedRows);

            bool SawDataRow = false;
            using (StreamReader Reader = new StreamReader(FilePath))
            {
                string? Line = Reader.ReadLine();
                if (Line == null)
                    throw new InvalidDataException("AGD file is empty.");

                while ((Line = Reader.ReadLine()) != null)
                {
                    if (Line.Length == 0)
                        continue;

                    ReadOnlySpan<char> Row = Line.AsSpan().Trim();
                    if (Row.IsEmpty)
                        continue;

                    if (!TryParseAgdRow(Row, out double Lat, out double Lon, out double ExistingM, out double ProposedM, out ReadOnlySpan<char> Code))
                        continue;

                    SawDataRow = true;
                    if (DelimitedTextParser.IsBenchmarkCode(Code))
                    {
                        Result.Survey.Benchmarks.Add(new Benchmark(Lat, Lon, Code.ToString(), ExistingM));
                        continue;
                    }

                    if (double.IsNaN(ExistingM) || double.IsNaN(ProposedM))
                        continue;

                    TopologyPoint Point = new TopologyPoint
                    {
                        Latitude = Lat,
                        Longitude = Lon,
                        ExistingElevation = ExistingM,
                        ProposedElevation = ProposedM,
                        CutFill = ProposedM - ExistingM,
                        Code = Code.IsEmpty ? null : Code.ToString()
                    };

                    if (DelimitedTextParser.CodeEquals(Code, "2PER"))
                        Result.Survey.BoundaryPoints.Add(Point);
                    else
                    {
                        Result.Survey.InteriorPoints.Add(Point);
                        DataRows.Add((Lat, Lon, ExistingM, ProposedM, Point.Code ?? string.Empty));
                    }
                }
            }

            if (!SawDataRow)
                throw new InvalidDataException("AGD file is empty.");

            BuildGridFromAgdRows(Result, DataRows);
            return Result;
        }

        /// <summary>
        /// Parses one AGD data row into geographic coordinates, elevations, and code text.
        /// </summary>
        static private bool TryParseAgdRow
            (
            ReadOnlySpan<char> Row,
            out double Lat,
            out double Lon,
            out double ExistingM,
            out double ProposedM,
            out ReadOnlySpan<char> Code
            )
        {
            Lat = 0;
            Lon = 0;
            ExistingM = 0;
            ProposedM = 0;
            Code = ReadOnlySpan<char>.Empty;

            int FieldIndex = 0;
            int Index = 0;
            ReadOnlySpan<char> ExistingText = ReadOnlySpan<char>.Empty;
            ReadOnlySpan<char> ProposedText = ReadOnlySpan<char>.Empty;
            ReadOnlySpan<char> CodeText = ReadOnlySpan<char>.Empty;

            while (Index <= Row.Length)
            {
                int FieldStart = Index;
                while (Index < Row.Length && Row[Index] != ',')
                    Index++;

                ReadOnlySpan<char> Field = Row[FieldStart..Index].Trim();
                switch (FieldIndex)
                {
                    case 0:
                        if (Field.IsEmpty || !double.TryParse(Field, NumberStyles.Float, CultureInfo.InvariantCulture, out Lat))
                            return false;
                        break;
                    case 1:
                        if (Field.IsEmpty || !double.TryParse(Field, NumberStyles.Float, CultureInfo.InvariantCulture, out Lon))
                            return false;
                        break;
                    case 2:
                        ExistingText = Field;
                        break;
                    case 3:
                        ProposedText = Field;
                        break;
                    case 5:
                        CodeText = Field;
                        break;
                }

                FieldIndex++;
                if (Index >= Row.Length)
                    break;
                Index++;
            }

            if (FieldIndex < 4)
                return false;

            if (!DelimitedTextParser.TryParseOptionalDouble(ExistingText, out ExistingM))
            {
                ExistingM = double.NaN;
                ProposedM = double.NaN;
                Code = CodeText;
                return !Code.IsEmpty;
            }

            if (!DelimitedTextParser.TryParseOptionalDouble(ProposedText, out ProposedM))
                ProposedM = double.NaN;

            Code = CodeText;
            return true;
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
            List<(double Existing, double Proposed, double E, double N, double Lat, double Lon)> UtmRows =
                new List<(double Existing, double Proposed, double E, double N, double Lat, double Lon)>(Rows.Count);

            foreach ((double Lat, double Lon, double Existing, double Proposed, string Code) Row in Rows)
            {
                UTM.UTMCoordinate C = UTM.FromLatLon(Row.Lat, Row.Lon);
                if ((C.Zone != Zone) || (C.IsNorthernHemisphere != North))
                    throw new InvalidDataException("Field crosses UTM zone boundaries.");

                MinX = Math.Min(MinX, C.Easting);
                MinY = Math.Min(MinY, C.Northing);
                MaxX = Math.Max(MaxX, C.Easting);
                MaxY = Math.Max(MaxY, C.Northing);
                UtmRows.Add((Row.Existing, Row.Proposed, C.Easting, C.Northing, Row.Lat, Row.Lon));
            }

            // Match FieldCreator padding so bin indices align with the grid origin.
            MinX -= FieldConstants.BIN_SIZE_M;
            MinY -= FieldConstants.BIN_SIZE_M;
            MaxX += FieldConstants.BIN_SIZE_M;
            MaxY += FieldConstants.BIN_SIZE_M;

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

            Dictionary<(int Bx, int By), (double SumExisting, double SumProposed, double SumLat, double SumLon, int Count)> Grouped =
                new Dictionary<(int Bx, int By), (double SumExisting, double SumProposed, double SumLat, double SumLon, int Count)>(Rows.Count);

            foreach ((double Existing, double Proposed, double E, double N, double Lat, double Lon) Row in UtmRows)
            {
                int Bx = (int)Math.Floor((Row.E - MinX) / FieldConstants.BIN_SIZE_M);
                int By = (int)Math.Floor((Row.N - MinY) / FieldConstants.BIN_SIZE_M);
                if ((Bx < 0) || (Bx >= Width) || (By < 0) || (By >= Height))
                    continue;

                (int Bx, int By) Key = (Bx, By);
                if (Grouped.TryGetValue(Key, out (double SumExisting, double SumProposed, double SumLat, double SumLon, int Count) Agg))
                    Grouped[Key] = (Agg.SumExisting + Row.Existing, Agg.SumProposed + Row.Proposed, Agg.SumLat + Row.Lat, Agg.SumLon + Row.Lon, Agg.Count + 1);
                else
                    Grouped[Key] = (Row.Existing, Row.Proposed, Row.Lat, Row.Lon, 1);
            }

            foreach (KeyValuePair<(int Bx, int By), (double SumExisting, double SumProposed, double SumLat, double SumLon, int Count)> Entry in Grouped)
            {
                (int Bx, int By) = Entry.Key;
                (double SumExisting, double SumProposed, double SumLat, double SumLon, int Count) = Entry.Value;
                Grid.Bins.Add(new GridBinData
                {
                    X = Bx,
                    Y = By,
                    ExistingM = SumExisting / Count,
                    ProposedM = SumProposed / Count,
                    CentroidLat = SumLat / Count,
                    CentroidLon = SumLon / Count,
                    Valid = true
                });
            }

            CompactGridToPopulatedBins(Grid);
            Result.Grid = Grid;
            Result.Parameters = new DesignParameters();
        }

        /// <summary>
        /// Reindexes a sparse AGD grid to a tight width/height around populated bins only.
        /// </summary>
        static private void CompactGridToPopulatedBins
            (
            DesignGrid Grid
            )
        {
            if (Grid.Bins.Count == 0)
            {
                Grid.Width = 0;
                Grid.Height = 0;
                return;
            }

            int MinBx = Grid.Bins.Min(B => B.X);
            int MinBy = Grid.Bins.Min(B => B.Y);
            int MaxBx = Grid.Bins.Max(B => B.X);
            int MaxBy = Grid.Bins.Max(B => B.Y);

            // Keep one empty bin margin so bilinear interpolation has corner neighbours when possible.
            int OriginBx = Math.Max(0, MinBx - 1);
            int OriginBy = Math.Max(0, MinBy - 1);
            int CompactWidth = (MaxBx - OriginBx) + 2;
            int CompactHeight = (MaxBy - OriginBy) + 2;

            double OriginEastingM = Grid.OriginEastingM + OriginBx * FieldConstants.BIN_SIZE_M;
            double OriginNorthingM = Grid.OriginNorthingM + OriginBy * FieldConstants.BIN_SIZE_M;
            double MaxEastingM = OriginEastingM + CompactWidth * FieldConstants.BIN_SIZE_M;
            double MaxNorthingM = OriginNorthingM + CompactHeight * FieldConstants.BIN_SIZE_M;

            foreach (GridBinData Bin in Grid.Bins)
            {
                Bin.X -= OriginBx;
                Bin.Y -= OriginBy;
            }

            Grid.Width = CompactWidth;
            Grid.Height = CompactHeight;
            Grid.OriginEastingM = OriginEastingM;
            Grid.OriginNorthingM = OriginNorthingM;
            Grid.MinX = OriginEastingM;
            Grid.MinY = OriginNorthingM;
            Grid.MaxX = MaxEastingM;
            Grid.MaxY = MaxNorthingM;
        }
    }
}
