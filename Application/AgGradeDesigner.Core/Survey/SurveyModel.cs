using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;

using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.IO;
using AgGradeDesigner.Core.Models;

namespace AgGradeDesigner.Core.Survey
{
    /// <summary>
    /// File format used when a survey was loaded or last saved via <see cref="Survey.Load"/> / <see cref="Survey.Save"/>.
    /// </summary>
    public enum SurveyFileFormats
    {
        /// <summary>No supported format recorded (survey not loaded from .txt or .ags).</summary>
        None = 0,
        /// <summary>Trimble multiplane text format (.txt).</summary>
        MultiplaneTxt = 1,
        /// <summary>AGS comma-separated format (.ags).</summary>
        Ags = 2
    }

    /// <summary>
    /// In-memory survey topology: interior and boundary points, benchmarks, and file-format persistence
    /// for Trimble multiplane (.txt) and AGS (.ags) interchange.
    /// </summary>
    public class SurveyModel
    {
        /// <summary>
        /// Master benchmark elevation used in Trimble multiplane files: 30.48 m (100 ft).
        /// All stored survey elevations are relative to this value once the MB GPS anchor is set.
        /// </summary>
        private const double MultiplaneMasterElevationM = 30.48;

        /// <summary>
        /// When loading legacy multiplane files, point elevations more than this many meters
        /// above <see cref="MultiplaneMasterElevationM"/> are treated as mistakenly written
        /// absolute GPS values rather than multiplane-relative elevations.
        /// </summary>
        private const double MaxPlausibleMultiplaneRelativeOffsetM = 150.0;

        /// <summary>Path of the file this survey was loaded from or last saved to.</summary>
        private string FileName;

        /// <summary>
        /// Format of the file this survey was loaded from (or set by <see cref="LoadFromMultiplane"/> / <see cref="LoadFromAgs"/>).
        /// </summary>
        public SurveyFileFormats LoadedFileFormat { get; private set; }

        /// <summary>
        /// Absolute GPS elevation in meters at the master benchmark (MB), captured when MB is first
        /// placed during live surveying. Persisted in multiplane header column 6 so a survey can be
        /// reloaded after a crash and new GPS samples converted into the same relative elevation frame.
        /// Null until MB is set from GPS or loaded from a multiplane file that includes the anchor.
        /// </summary>
        public double? MasterAbsoluteElevationM { get; private set; }

        /// <summary>
        /// Records the absolute GPS elevation at the master benchmark for multiplane-relative storage.
        /// </summary>
        /// <param name="Value">Absolute GPS elevation in meters at MB.</param>
        public void SetMasterAbsoluteElevationM
            (
            double Value
            )
        {
            // Persist anchor so subsequent GPS samples share the same relative frame.
            MasterAbsoluteElevationM = Value;
        }

        /// <summary>
        /// Creates a deep copy of interior points, boundary points, benchmarks, and format metadata.
        /// </summary>
        /// <returns>Independent snapshot of this survey.</returns>
        public SurveyModel CloneSnapshot
            (
            )
        {
            SurveyModel Copy = new SurveyModel();
            Copy.MasterAbsoluteElevationM = MasterAbsoluteElevationM;
            Copy.LoadedFileFormat = LoadedFileFormat;
            foreach (TopologyPoint Pt in InteriorPoints)
            {
                Copy.InteriorPoints.Add(new TopologyPoint
                {
                    Latitude = Pt.Latitude,
                    Longitude = Pt.Longitude,
                    ExistingElevation = Pt.ExistingElevation,
                    Code = Pt.Code
                });
            }
            foreach (TopologyPoint Pt in BoundaryPoints)
            {
                Copy.BoundaryPoints.Add(new TopologyPoint
                {
                    Latitude = Pt.Latitude,
                    Longitude = Pt.Longitude,
                    ExistingElevation = Pt.ExistingElevation,
                    Code = Pt.Code
                });
            }
            foreach (Benchmark Bm in Benchmarks)
            {
                Copy.Benchmarks.Add(new Benchmark(Bm.Location.Latitude, Bm.Location.Longitude, Bm.Name, Bm.Elevation));
            }
            return Copy;
        }

        /// <summary>
        /// Appends all points and benchmarks from another survey onto this instance.
        /// </summary>
        /// <param name="Other">Survey whose collections are merged in place.</param>
        public void MergeFrom
            (
            SurveyModel Other
            )
        {
            // Concatenate collections without deduplication; caller owns merge policy.
            InteriorPoints.AddRange(Other.InteriorPoints);
            BoundaryPoints.AddRange(Other.BoundaryPoints);
            Benchmarks.AddRange(Other.Benchmarks);
        }

        /// <summary>
        /// Returns true when the first line of a text file matches the Trimble multiplane header pattern.
        /// </summary>
        /// <param name="FilePath">Path to the candidate survey file.</param>
        /// <returns>True when column 5 of the first line begins with MB.</returns>
        static public bool IsMultiplaneContent
            (
            string FilePath
            )
        {
            using StreamReader Reader = new StreamReader(FilePath);
            string? Line = Reader.ReadLine();
            if (string.IsNullOrWhiteSpace(Line))
                return false;
            string[] Parts = Line.Split('\t');
            // Multiplane headers always place the MB location token in the fifth tab column.
            return Parts.Length >= 5 && Parts[4].TrimStart().StartsWith("MB", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Loads a survey by inferring format from extension and, when needed, header content.
        /// </summary>
        /// <param name="FilePath">Path to a .txt, .ags, or content-detectable survey file.</param>
        public void LoadAuto
            (
            string FilePath
            )
        {
            string Ext = Path.GetExtension(FilePath);
            if (string.Equals(Ext, ".ags", StringComparison.OrdinalIgnoreCase))
                LoadFromAgs(FilePath);
            else if (string.Equals(Ext, ".txt", StringComparison.OrdinalIgnoreCase) || IsMultiplaneContent(FilePath))
                LoadFromMultiplane(FilePath);
            else if (string.Equals(Ext, ".agd", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException("Use AgdService to import .agd files.");
            else if (IsMultiplaneContent(FilePath))
                LoadFromMultiplane(FilePath);
            else
                LoadFromAgs(FilePath);
        }

        /// <summary>
        /// Only Latitude, Longitude and ExistingElevation are used
        /// </summary>
        public List<TopologyPoint> InteriorPoints = new List<TopologyPoint>();

        /// <summary>
        /// Only Latitude, Longitude and ExistingElevation are used
        /// </summary>
        public List<TopologyPoint> BoundaryPoints = new List<TopologyPoint>();

        /// <summary>Benchmark rows including MB and any BM* control points.</summary>
        public List<Benchmark> Benchmarks = new List<Benchmark>();

        /// <summary>Display name derived from the loaded or saved file path without extension.</summary>
        public string Name
        {
            get
            {
                return Path.GetFileNameWithoutExtension(FileName);
            }
        }

        /// <summary>
        /// Adds a new benchmark to the survey from an absolute GPS elevation reading.
        /// If this is the first benchmark it is named MB, records <see cref="MasterAbsoluteElevationM"/>,
        /// and stores MB at <see cref="MultiplaneMasterElevationM"/> (100 ft). Additional benchmarks are
        /// named BMx and stored as multiplane-relative elevations using the same GPS anchor.
        /// </summary>
        /// <param name="Location">Benchmark location as latitude/longitude.</param>
        /// <param name="AbsoluteGpsElevationM">Absolute GPS elevation in meters from the receiver.</param>
        /// <returns>The newly created benchmark instance.</returns>
        public Benchmark AddBenchmark
            (
            Coordinate Location,
            double AbsoluteGpsElevationM
            )
        {
            if (Location == null)
                throw new ArgumentNullException(nameof(Location));

            string Name;
            double StoredElevationM;

            if (Benchmarks.Count == 0)
            {
                // First benchmark establishes the multiplane datum and GPS anchor.
                Name = "MB";
                MasterAbsoluteElevationM = AbsoluteGpsElevationM;
                StoredElevationM = MultiplaneMasterElevationM;
            }
            else
            {
                int MaxSequence = 0;
                foreach (Benchmark Existing in Benchmarks)
                {
                    if (Existing == null || string.IsNullOrWhiteSpace(Existing.Name)) continue;

                    Match Match = Regex.Match(Existing.Name.Trim(), @"^BM(\d+)$", RegexOptions.IgnoreCase);
                    if (!Match.Success) continue;

                    if (int.TryParse(Match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int Sequence))
                    {
                        if (Sequence > MaxSequence) MaxSequence = Sequence;
                    }
                }

                Name = $"BM{MaxSequence + 1}";
                StoredElevationM = ToMultiplaneRelativeElevationM(AbsoluteGpsElevationM);
            }

            Benchmark Benchmark = new Benchmark(Location, Name, StoredElevationM);
            Benchmarks.Add(Benchmark);
            return Benchmark;
        }

        /// <summary>
        /// Converts an absolute GPS elevation in meters into the multiplane-relative elevation
        /// stored internally and written to .txt survey files (MB datum = 100 ft / 30.48 m).
        /// </summary>
        /// <param name="AbsoluteGpsElevationM">Absolute GPS elevation in meters from the receiver.</param>
        /// <returns>Elevation in meters relative to the multiplane MB datum.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when <see cref="MasterAbsoluteElevationM"/> has not been set (MB benchmark missing).
        /// </exception>
        public double ToMultiplaneRelativeElevationM
            (
            double AbsoluteGpsElevationM
            )
        {
            if (!MasterAbsoluteElevationM.HasValue)
            {
                throw new InvalidOperationException(
                    "Cannot convert GPS elevation to multiplane-relative storage: master benchmark absolute elevation is not set. Add the MB benchmark first.");
            }

            // Shift GPS absolute into the stored MB-relative frame anchored at 100 ft.
            return AbsoluteGpsElevationM - MasterAbsoluteElevationM.Value + MultiplaneMasterElevationM;
        }

        /// <summary>
        /// Loads a survey from disk by extension: <c>.txt</c> uses multiplane format, <c>.ags</c> uses AGS format.
        /// Sets <see cref="LoadedFileFormat"/> accordingly.
        /// </summary>
        /// <param name="FileName">Path and name of the file to load</param>
        public void Load
            (
            string FileName
            )
        {
            if (string.IsNullOrWhiteSpace(FileName))
                throw new ArgumentException("File name is required.", nameof(FileName));

            if (!File.Exists(FileName))
                throw new FileNotFoundException("Survey file was not found.", FileName);

            string Extension = Path.GetExtension(FileName);
            if (string.Equals(Extension, ".ags", StringComparison.OrdinalIgnoreCase))
                LoadFromAgs(FileName);
            else if (string.Equals(Extension, ".txt", StringComparison.OrdinalIgnoreCase))
                LoadFromMultiplane(FileName);
            else
                throw new NotSupportedException($"Unsupported survey file extension '{Extension}'. Expected .txt or .ags.");
        }

        /// <summary>
        /// Saves the survey to the current file path using the format recorded in <see cref="LoadedFileFormat"/>.
        /// </summary>
        public void Save
            (
            )
        {
            if (FileName == null)
                throw new Exception("Survey does not have a file name.");

            switch (LoadedFileFormat)
            {
                case SurveyFileFormats.MultiplaneTxt:
                    SaveToMultiplane();
                    break;
                case SurveyFileFormats.Ags:
                    SaveToAgs();
                    break;
                default:
                    throw new InvalidOperationException(
                        "Cannot save: survey format is not set. Load from a .txt or .ags file first.");
            }
        }

        /// <summary>
        /// Loads a trimble multiplane file
        /// </summary>
        /// <param name="FileName">Path and name of the file to load</param>
        public void LoadFromMultiplane
            (
            string FileName
            )
        {
            if (string.IsNullOrWhiteSpace(FileName))
                throw new ArgumentException("File name is required.", nameof(FileName));

            if (!File.Exists(FileName))
                throw new FileNotFoundException("Multiplane file was not found.", FileName);

            this.FileName = FileName;
            LoadedFileFormat = SurveyFileFormats.MultiplaneTxt;

            // Filename suffix _m selects meter units; otherwise assume feet in the file body.
            bool Meters = false;
            if (Path.GetFileNameWithoutExtension(FileName).ToLower().Contains("_m")) Meters = true;

            string[] Lines = File.ReadAllLines(FileName);
            InteriorPoints.Clear();
            BoundaryPoints.Clear();
            Benchmarks.Clear();
            MasterAbsoluteElevationM = null;

            // Support an empty/blank file as a valid new survey.
            if (Lines.Length == 0 || Lines.All(string.IsNullOrWhiteSpace))
                return;

            string[] HeaderParts = Lines[0].Split('\t');
            if (HeaderParts.Length < 5)
                throw new Exception("Invalid multiplane header. Expected at least 5 tab-separated columns.");

            double BaseOffsetXFt = ParseDouble(HeaderParts[1], "header offset X");
            double BaseOffsetYFt = ParseDouble(HeaderParts[2], "header offset Y");
            double BaseHeightFt = ParseDouble(HeaderParts[3], "header elevation");

            Coordinate BaseMasterLocation = ParseHeaderLocation(HeaderParts[4]);
            Coordinate MasterLocation = UTM.OffsetLocation(
                BaseMasterLocation,
                Meters ? BaseOffsetXFt : FeetToMeters(BaseOffsetXFt),
                Meters ? BaseOffsetYFt : FeetToMeters(BaseOffsetYFt));

            UTM.UTMCoordinate MasterUtm = UTM.FromLatLon(MasterLocation.Latitude, MasterLocation.Longitude);

            // Multiplane elevation values in the file are relative to the MB datum (100 ft / 30.48 m).
            double MasterElevationMeters = Meters ? BaseHeightFt : FeetToMeters(BaseHeightFt);

            Benchmarks.Add(new Benchmark(MasterLocation, "MB", MasterElevationMeters));

            // Column 6 stores the absolute GPS elevation (meters) at MB for crash recovery.
            MasterAbsoluteElevationM = TryReadMultiplaneHeaderAnchorM(HeaderParts);

            for (int LineIndex = 1; LineIndex < Lines.Length; LineIndex++)
            {
                string Line = Lines[LineIndex];
                if (string.IsNullOrWhiteSpace(Line))
                    continue;

                string[] Parts = Line.Split('\t');
                if (Parts.Length < 4)
                    throw new Exception($"Unable to parse line {LineIndex + 1}. Expected at least 4 tab-separated columns.");

                double XFt = ParseDouble(Parts[1], $"line {LineIndex + 1} offset X");
                double YFt = ParseDouble(Parts[2], $"line {LineIndex + 1} offset Y");
                double ElevationFt = ParseDouble(Parts[3], $"line {LineIndex + 1} elevation");

                string Code = Parts.Length > 4 ? Parts[4].Trim() : string.Empty;
                double ElevationM = Meters ? ElevationFt : FeetToMeters(ElevationFt);
                Coordinate PointLocation = OffsetFromMasterUtm(
                    MasterUtm,
                    Meters ? XFt : FeetToMeters(XFt),
                    Meters ? YFt : FeetToMeters(YFt));

                if (IsBenchmarkCode(Code))
                {
                    Benchmarks.Add(new Benchmark(PointLocation, Code, ElevationM));
                    continue;
                }

                TopologyPoint Point = new TopologyPoint
                {
                    Latitude = PointLocation.Latitude,
                    Longitude = PointLocation.Longitude,
                    ExistingElevation = ElevationM
                };

                if (string.Equals(Code, "B", StringComparison.OrdinalIgnoreCase))
                    BoundaryPoints.Add(Point);
                else
                    InteriorPoints.Add(Point);
            }

            // Older AgGrade builds wrote absolute GPS feet for survey points while MB was normalized to 100 ft.
            TryRepairLegacyAbsoluteMultiplaneElevations();
        }

        /// <summary>
        /// Reads the absolute MB GPS anchor from multiplane header column 6 (meters).
        /// Column 6 is optional; zero or missing means the anchor was not persisted (legacy file).
        /// </summary>
        /// <param name="HeaderParts">Tab-separated columns from the first line of a multiplane file.</param>
        /// <returns>Absolute GPS elevation in meters at MB, or null when not stored in the file.</returns>
        static private double? TryReadMultiplaneHeaderAnchorM
            (
            string[] HeaderParts
            )
        {
            if (HeaderParts == null || HeaderParts.Length < 6)
                return null;

            string AnchorText = HeaderParts[5]?.Trim();
            if (string.IsNullOrWhiteSpace(AnchorText))
                return null;

            if (!double.TryParse(AnchorText, NumberStyles.Float, CultureInfo.InvariantCulture, out double AnchorM))
                return null;

            // Zero is reserved to mean "not set" for backward compatibility with files that used 0.000.
            if (Math.Abs(AnchorM) < 1e-9)
                return null;

            return AnchorM;
        }

        /// <summary>
        /// Repairs multiplane surveys saved by older AgGrade builds that normalized MB to 100 ft in the
        /// header but wrote absolute GPS elevations (in feet) for boundary and interior points. Detects
        /// that condition when no header anchor is present and stored point elevations are implausibly
        /// high relative to MB, then infers <see cref="MasterAbsoluteElevationM"/> and rewrites all
        /// stored elevations into the correct multiplane-relative frame.
        /// </summary>
        private void TryRepairLegacyAbsoluteMultiplaneElevations
            (
            )
        {
            if (MasterAbsoluteElevationM.HasValue)
                return;

            if (!HasImplausiblyHighMultiplaneRelativeElevations())
                return;

            double InferredAnchorM = InferLegacyAbsoluteElevationAnchorM();
            MasterAbsoluteElevationM = InferredAnchorM;

            foreach (Benchmark Benchmark in Benchmarks)
            {
                if (Benchmark == null) continue;
                if (string.Equals(Benchmark.Name?.Trim(), "MB", StringComparison.OrdinalIgnoreCase))
                {
                    Benchmark.Elevation = MultiplaneMasterElevationM;
                    continue;
                }

                Benchmark.Elevation = ToMultiplaneRelativeElevationM(Benchmark.Elevation);
            }

            foreach (TopologyPoint Point in BoundaryPoints)
            {
                if (Point == null) continue;
                Point.ExistingElevation = ToMultiplaneRelativeElevationM(Point.ExistingElevation);
            }

            foreach (TopologyPoint Point in InteriorPoints)
            {
                if (Point == null) continue;
                Point.ExistingElevation = ToMultiplaneRelativeElevationM(Point.ExistingElevation);
            }
        }

        /// <summary>
        /// Returns true when any stored benchmark (other than MB) or topology point elevation is far
        /// above the multiplane MB datum, indicating absolute GPS values were stored instead of relative.
        /// </summary>
        /// <returns>True when legacy absolute-elevation corruption is suspected.</returns>
        private bool HasImplausiblyHighMultiplaneRelativeElevations
            (
            )
        {
            double ThresholdM = MultiplaneMasterElevationM + MaxPlausibleMultiplaneRelativeOffsetM;

            foreach (Benchmark Benchmark in Benchmarks)
            {
                if (Benchmark == null) continue;
                if (string.Equals(Benchmark.Name?.Trim(), "MB", StringComparison.OrdinalIgnoreCase)) continue;
                if (Benchmark.Elevation > ThresholdM)
                    return true;
            }

            foreach (TopologyPoint Point in BoundaryPoints)
            {
                if (Point == null) continue;
                if (Point.ExistingElevation > ThresholdM)
                    return true;
            }

            foreach (TopologyPoint Point in InteriorPoints)
            {
                if (Point == null) continue;
                if (Point.ExistingElevation > ThresholdM)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Estimates the absolute GPS elevation at MB for a legacy file by using the first available
        /// survey point. Those files mistakenly stored absolute GPS as point elevations, so the first
        /// boundary (or interior) point is typically near the MB absolute elevation at survey start.
        /// </summary>
        /// <returns>Inferred absolute GPS elevation in meters for the master benchmark anchor.</returns>
        private double InferLegacyAbsoluteElevationAnchorM
            (
            )
        {
            if (BoundaryPoints.Count > 0 && BoundaryPoints[0] != null)
                return BoundaryPoints[0].ExistingElevation;

            if (InteriorPoints.Count > 0 && InteriorPoints[0] != null)
                return InteriorPoints[0].ExistingElevation;

            foreach (Benchmark Benchmark in Benchmarks)
            {
                if (Benchmark == null) continue;
                if (string.Equals(Benchmark.Name?.Trim(), "MB", StringComparison.OrdinalIgnoreCase)) continue;
                return Benchmark.Elevation;
            }

            throw new Exception(
                "Cannot repair legacy multiplane elevations: no survey points were found to infer the master GPS anchor.");
        }

        /// <summary>
        /// Returns the master benchmark (MB) or throws when it is missing.
        /// </summary>
        /// <returns>The master benchmark instance.</returns>
        private Benchmark GetMasterBenchmark
            (
            )
        {
            return Benchmarks.FirstOrDefault(Bm =>
                Bm != null &&
                !string.IsNullOrWhiteSpace(Bm.Name) &&
                string.Equals(Bm.Name.Trim(), "MB", StringComparison.OrdinalIgnoreCase))
                ?? throw new Exception("No master benchmark (MB) exists.");
        }

        /// <summary>
        /// Ensures the stored MB elevation is exactly <see cref="MultiplaneMasterElevationM"/> when
        /// a GPS anchor is known. Does not modify boundary or interior points; those are expected to
        /// already be stored as multiplane-relative elevations.
        /// </summary>
        private void EnsureMultiplaneMasterBenchmarkElevation
            (
            )
        {
            if (!MasterAbsoluteElevationM.HasValue)
                return;

            GetMasterBenchmark().Elevation = MultiplaneMasterElevationM;
        }

        /// <summary>
        /// Ensures the master benchmark is stored at the multiplane datum (30.48 m / 100 ft).
        /// Boundary and interior points are not modified; use <see cref="ToMultiplaneRelativeElevationM"/>
        /// when recording GPS samples so all elevations share the same relative frame.
        /// </summary>
        public void NormalizeElevationsForMultiplane
            (
            )
        {
            EnsureMultiplaneMasterBenchmarkElevation();
        }

        /// <summary>
        /// Saves to current file name
        /// </summary>
        public void SaveToMultiplane
            (
            )
        {
            if (FileName == null) throw new Exception("Survey does not have a file name");

            SaveToMultiplane(FileName);
        }

        /// <summary>
        /// Saves the survey to a trimble multiplane file.
        /// Elevations are written as multiplane-relative values (MB = 100 ft) without mutating stored
        /// data. Header column 6 persists <see cref="MasterAbsoluteElevationM"/> for crash recovery.
        /// If the file exists then it is overwritten without warning.
        /// </summary>
        /// <param name="FileName">Path and name of the file to save to</param>
        public void SaveToMultiplane
            (
            string FileName
            )
        {
            if (string.IsNullOrWhiteSpace(FileName))
                throw new ArgumentException("File name is required.", nameof(FileName));

            // Filename suffix _m selects meter units on write, matching load behavior.
            bool Meters = false;
            if (Path.GetFileNameWithoutExtension(FileName).ToLower().Contains("_m")) Meters = true;

            EnsureMultiplaneMasterBenchmarkElevation();

            Benchmark MasterBenchmark = GetMasterBenchmark();

            Coordinate MasterLocation = MasterBenchmark.Location
                ?? throw new Exception("Cannot save multiplane file: master benchmark location is null.");

            double MasterHeightFt = Meters ? MasterBenchmark.Elevation : MetersToFeet(MasterBenchmark.Elevation);

            // Column 6 always stores the GPS anchor in meters regardless of file unit suffix.
            double HeaderAnchorM = MasterAbsoluteElevationM ?? 0.0;
            string HeaderAnchorText = HeaderAnchorM.ToString("G17", CultureInfo.InvariantCulture);

            List<string> Lines = new List<string>();
            Lines.Add(string.Format(
                CultureInfo.InvariantCulture,
                "0001\t0.000\t0.000\t{0:0.000}\tMB {1} / {2}\t{3}",
                MasterHeightFt,
                FormatLatitude(MasterLocation.Latitude),
                FormatLongitude(MasterLocation.Longitude),
                HeaderAnchorText));

            UTM.UTMCoordinate MasterUtm = UTM.FromLatLon(MasterLocation.Latitude, MasterLocation.Longitude);
            int NextId = 2;

            foreach (Benchmark Benchmark in Benchmarks)
            {
                if (Benchmark == null) continue;
                if (string.Equals(Benchmark.Name?.Trim(), "MB", StringComparison.OrdinalIgnoreCase)) continue;

                Lines.Add(FormatPointLine(NextId++, MasterUtm, Benchmark.Location, Benchmark.Elevation, Benchmark.Name, Meters));
            }

            foreach (TopologyPoint Point in BoundaryPoints)
            {
                if (Point == null) continue;
                Lines.Add(FormatPointLine(NextId++, MasterUtm, new Coordinate(Point.Latitude, Point.Longitude), Point.ExistingElevation, "B", Meters));
            }

            foreach (TopologyPoint Point in InteriorPoints)
            {
                if (Point == null) continue;
                Lines.Add(FormatPointLine(NextId++, MasterUtm, new Coordinate(Point.Latitude, Point.Longitude), Point.ExistingElevation, string.Empty, Meters));
            }

            File.WriteAllLines(FileName, Lines);
        }

        /// <summary>
        /// Loads an AGS survey file (comma-separated values: latitude, longitude, elevation in meters, optional code).
        /// Master benchmark rows use a code containing <c>mb</c> (for example 0mb_4g); other benchmarks use a code containing <c>bm</c> (for example BM1).
        /// Boundary rows use code 2PER; interior rows use 3GRD.
        /// </summary>
        /// <param name="FileName">Path and name of the file to load</param>
        public void LoadFromAgs
            (
            string FileName
            )
        {
            if (string.IsNullOrWhiteSpace(FileName))
                throw new ArgumentException("File name is required.", nameof(FileName));

            if (!File.Exists(FileName))
                throw new FileNotFoundException("AGS file was not found.", FileName);

            this.FileName = FileName;
            LoadedFileFormat = SurveyFileFormats.Ags;

            int EstimatedRows = DelimitedTextParser.EstimateRowCapacity(FileName, AverageBytesPerRow: 36);
            InteriorPoints.Clear();
            BoundaryPoints.Clear();
            Benchmarks.Clear();
            MasterAbsoluteElevationM = null;
            InteriorPoints.Capacity = Math.Max(InteriorPoints.Capacity, EstimatedRows);
            BoundaryPoints.Capacity = Math.Max(BoundaryPoints.Capacity, EstimatedRows / 8);

            bool SkippedHeaderRow = false;
            using StreamReader Reader = new StreamReader(FileName);
            string? Line;
            int LineNumber = 0;
            while ((Line = Reader.ReadLine()) != null)
            {
                LineNumber++;
                if (string.IsNullOrWhiteSpace(Line))
                    continue;

                ReadOnlySpan<char> Row = Line.AsSpan().Trim();
                if (!DelimitedTextParser.TryParseLeadingDoubles(
                        Row,
                        out double Latitude,
                        out double Longitude,
                        out double ElevationM,
                        out int CodeStart,
                        out int CodeLength))
                {
                    if (!SkippedHeaderRow)
                    {
                        SkippedHeaderRow = true;
                        continue;
                    }

                    throw new Exception($"Invalid numeric value(s) on AGS line {LineNumber}. Expected numeric latitude, longitude, and elevation.");
                }

                ReadOnlySpan<char> Code = CodeLength > 0 ? Row.Slice(CodeStart, CodeLength).Trim() : ReadOnlySpan<char>.Empty;
                if (IsAgsMasterBenchmarkCode(Code))
                {
                    Benchmarks.Add(new Benchmark(new Coordinate(Latitude, Longitude), Code.ToString(), ElevationM));
                    continue;
                }

                if (IsAgsRegularBenchmarkCode(Code))
                {
                    Benchmarks.Add(new Benchmark(new Coordinate(Latitude, Longitude), Code.ToString(), ElevationM));
                    continue;
                }

                TopologyPoint Point = new TopologyPoint
                {
                    Latitude = Latitude,
                    Longitude = Longitude,
                    ExistingElevation = ElevationM,
                    Code = Code.IsEmpty ? null : Code.ToString()
                };

                if (DelimitedTextParser.CodeEquals(Code, "2PER"))
                    BoundaryPoints.Add(Point);
                else
                    InteriorPoints.Add(Point);
            }
        }

        /// <summary>
        /// Converts a UTM offset from the master benchmark into a geographic coordinate.
        /// </summary>
        static private Coordinate OffsetFromMasterUtm
            (
            UTM.UTMCoordinate MasterUtm,
            double EastingM,
            double NorthingM
            )
        {
            if ((EastingM == 0) && (NorthingM == 0))
            {
                UTM.ToLatLon(
                    MasterUtm.Zone,
                    MasterUtm.IsNorthernHemisphere,
                    MasterUtm.Easting,
                    MasterUtm.Northing,
                    out double Latitude,
                    out double Longitude);
                return new Coordinate(Latitude, Longitude);
            }

            UTM.ToLatLon(
                MasterUtm.Zone,
                MasterUtm.IsNorthernHemisphere,
                MasterUtm.Easting + EastingM,
                MasterUtm.Northing + NorthingM,
                out double OffsetLatitude,
                out double OffsetLongitude);
            OffsetLongitude = ((OffsetLongitude + 180.0) % 360.0 + 360.0) % 360.0 - 180.0;
            return new Coordinate(OffsetLatitude, OffsetLongitude);
        }

        /// <summary>
        /// Saves to current file name
        /// </summary>
        public void SaveToAgs
            (
            )
        {
            if (FileName == null) throw new Exception("Survey does not have a file name");

            SaveToAgs(FileName);
        }

        /// <summary>
        /// Saves the survey to an AGS file (comma-separated values: latitude, longitude, elevation in meters, code).
        /// Writes the master row first using its stored benchmark name (must contain <c>mb</c>, for example 0mb_4g or MB),
        /// then other benchmarks (names containing <c>bm</c>, for example BM1), then boundary points as 2PER and interior points as 3GRD.
        /// If the file exists then it is overwritten without warning.
        /// </summary>
        /// <param name="FileName">Path and name of the file to save to</param>
        public void SaveToAgs
            (
            string FileName
            )
        {
            if (string.IsNullOrWhiteSpace(FileName))
                throw new ArgumentException("File name is required.", nameof(FileName));

            Benchmark MasterBenchmark = Benchmarks.FirstOrDefault(Bm =>
                Bm != null &&
                !string.IsNullOrWhiteSpace(Bm.Name) &&
                IsAgsMasterBenchmarkCode(Bm.Name))
                ?? throw new Exception("Cannot save AGS file: no master benchmark (name must contain mb, for example MB or 0mb_4g).");

            Coordinate MasterLocation = MasterBenchmark.Location
                ?? throw new Exception("Cannot save AGS file: master benchmark location is null.");

            string MasterCode = MasterBenchmark.Name.Trim();

            List<string> Lines = new List<string>();
            Lines.Add(FormatAgsLine(MasterLocation.Latitude, MasterLocation.Longitude, MasterBenchmark.Elevation, MasterCode));

            foreach (Benchmark Benchmark in Benchmarks)
            {
                if (Benchmark == null) continue;
                if (ReferenceEquals(Benchmark, MasterBenchmark)) continue;

                Coordinate Loc = Benchmark.Location
                    ?? throw new Exception($"Cannot save AGS file: benchmark '{Benchmark.Name}' has a null location.");
                Lines.Add(FormatAgsLine(Loc.Latitude, Loc.Longitude, Benchmark.Elevation, Benchmark.Name.Trim()));
            }

            foreach (TopologyPoint Point in BoundaryPoints)
            {
                if (Point == null) continue;
                Lines.Add(FormatAgsLine(Point.Latitude, Point.Longitude, Point.ExistingElevation, "2PER"));
            }

            foreach (TopologyPoint Point in InteriorPoints)
            {
                if (Point == null) continue;
                Lines.Add(FormatAgsLine(Point.Latitude, Point.Longitude, Point.ExistingElevation, "3GRD"));
            }

            File.WriteAllLines(FileName, Lines);
        }

        /// <summary>
        /// Formats one AGS data row: latitude, longitude, elevation (meters), optional code.
        /// </summary>
        /// <param name="Latitude">Latitude in decimal degrees.</param>
        /// <param name="Longitude">Longitude in decimal degrees.</param>
        /// <param name="ElevationM">Elevation in meters.</param>
        /// <param name="Code">Optional survey code appended as the fourth column.</param>
        /// <returns>One comma-separated AGS line.</returns>
        static private string FormatAgsLine
            (
            double Latitude,
            double Longitude,
            double ElevationM,
            string Code
            )
        {
            string Lat = Latitude.ToString("G17", CultureInfo.InvariantCulture);
            string Lon = Longitude.ToString("G17", CultureInfo.InvariantCulture);
            string Elev = ElevationM.ToString("G17", CultureInfo.InvariantCulture);
            if (string.IsNullOrEmpty(Code))
                return string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},", Lat, Lon, Elev);
            return string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3}", Lat, Lon, Elev, Code);
        }

        /// <summary>
        /// Formats a non-header multiplane point row from a geodetic location and relative elevation.
        /// </summary>
        /// <param name="PointId">Sequential point identifier to write in column 1.</param>
        /// <param name="MasterUtm">UTM coordinate of the master benchmark used as the offset origin.</param>
        /// <param name="Location">Point latitude/longitude location.</param>
        /// <param name="ElevationM">Point elevation in meters relative to the master normalization.</param>
        /// <param name="Code">Point code such as BM1, B, or other survey code.</param>
        /// <param name="Meters">true to write units in meters, false to write in feet</param>
        /// <returns>One tab-delimited multiplane point line.</returns>
        static private string FormatPointLine
            (
            int PointId,
            UTM.UTMCoordinate MasterUtm,
            Coordinate Location,
            double ElevationM,
            string Code,
            bool Meters
            )
        {
            if (Location == null)
                throw new Exception($"Point {PointId} has a null location.");

            UTM.UTMCoordinate Utm = UTM.FromLatLon(Location.Latitude, Location.Longitude);
            if (Utm.Zone != MasterUtm.Zone || Utm.IsNorthernHemisphere != MasterUtm.IsNorthernHemisphere)
                throw new Exception($"Point {PointId} is not in the same UTM zone/hemisphere as the master benchmark.");

            double RelativeXM = Utm.Easting - MasterUtm.Easting;
            double RelativeYM = Utm.Northing - MasterUtm.Northing;
            double RelativeXFt = Meters ? RelativeXM : MetersToFeet(RelativeXM);
            double RelativeYFt = Meters ? RelativeYM : MetersToFeet(RelativeYM);

            double ElevationFt = Meters ? ElevationM : MetersToFeet(ElevationM);

            return string.Format(
                CultureInfo.InvariantCulture,
                "{0}\t{1:0.00}\t{2:0.00}\t{3:0.000000}\t{4}",
                PointId,
                RelativeXFt,
                RelativeYFt,
                ElevationFt,
                string.IsNullOrWhiteSpace(Code) ? string.Empty : Code.Trim());
        }

        /// <summary>
        /// Parses the header location segment in the format "MB N.. / W..".
        /// </summary>
        /// <param name="HeaderLocationText">Raw header location text from column 5.</param>
        /// <returns>Parsed master benchmark latitude/longitude.</returns>
        static private Coordinate ParseHeaderLocation
            (
            string HeaderLocationText
            )
        {
            if (string.IsNullOrWhiteSpace(HeaderLocationText))
                throw new Exception("Invalid multiplane header location: value is empty.");

            int SlashIndex = HeaderLocationText.IndexOf('/');
            if (SlashIndex < 0)
                throw new Exception($"Invalid multiplane header location: '{HeaderLocationText}'.");

            string Left = HeaderLocationText.Substring(0, SlashIndex).Trim();
            string Right = HeaderLocationText.Substring(SlashIndex + 1).Trim();

            if (!Left.StartsWith("MB ", StringComparison.OrdinalIgnoreCase))
                throw new Exception($"Invalid multiplane header location prefix: '{HeaderLocationText}'.");

            string LatText = Left.Substring(3).Trim();
            double Latitude = ParseDms(LatText, 'N', 'S', "latitude");
            double Longitude = ParseDms(Right, 'E', 'W', "longitude");

            return new Coordinate(Latitude, Longitude);
        }

        /// <summary>
        /// Determines whether a code should be treated as a benchmark code.
        /// </summary>
        /// <param name="Code">Point code text to inspect.</param>
        /// <returns>True when the code represents MB or BM*.</returns>
        static private bool IsBenchmarkCode
            (
            string Code
            )
        {
            if (string.IsNullOrWhiteSpace(Code)) return false;
            string Normalized = Code.Trim().ToUpperInvariant();
            return Normalized == "MB" || Normalized.StartsWith("BM");
        }

        /// <summary>
        /// AGS master benchmark: code contains the substring <c>mb</c> (case-insensitive), for example 0mb_4g or MB.
        /// </summary>
        /// <param name="Code">AGS code column to inspect.</param>
        /// <returns>True when the code identifies a master benchmark row.</returns>
        static private bool IsAgsMasterBenchmarkCode
            (
            string Code
            )
        {
            return IsAgsMasterBenchmarkCode(Code.AsSpan());
        }

        /// <summary>
        /// AGS master benchmark: code contains the substring <c>mb</c> (case-insensitive), for example 0mb_4g or MB.
        /// </summary>
        /// <param name="Code">AGS code column to inspect.</param>
        /// <returns>True when the code identifies a master benchmark row.</returns>
        static private bool IsAgsMasterBenchmarkCode
            (
            ReadOnlySpan<char> Code
            )
        {
            if (Code.IsEmpty) return false;
            return Code.Contains("mb".AsSpan(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// AGS non-master benchmark: code contains the substring <c>bm</c> (case-insensitive), for example BM1.
        /// Checked after <see cref="IsAgsMasterBenchmarkCode"/> so codes that match both resolve as master.
        /// </summary>
        /// <param name="Code">AGS code column to inspect.</param>
        /// <returns>True when the code identifies a non-master benchmark row.</returns>
        static private bool IsAgsRegularBenchmarkCode
            (
            ReadOnlySpan<char> Code
            )
        {
            if (Code.IsEmpty) return false;
            return Code.Contains("bm".AsSpan(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Parses a single DMS value with hemisphere prefix (for example N36:26:51.704).
        /// </summary>
        /// <param name="Text">Text containing hemisphere and D:M:S components.</param>
        /// <param name="PositiveHemisphere">Hemisphere letter treated as positive (for example N or E).</param>
        /// <param name="NegativeHemisphere">Hemisphere letter treated as negative (for example S or W).</param>
        /// <param name="ValueName">Human-readable value name used in exception messages.</param>
        /// <returns>Signed decimal-degree value.</returns>
        static private double ParseDms
            (
            string Text,
            char PositiveHemisphere,
            char NegativeHemisphere,
            string ValueName
            )
        {
            if (string.IsNullOrWhiteSpace(Text))
                throw new Exception($"Invalid {ValueName}: value is empty.");

            string Trimmed = Text.Trim();
            char Hemisphere = char.ToUpperInvariant(Trimmed[0]);

            if (Hemisphere != char.ToUpperInvariant(PositiveHemisphere) &&
                Hemisphere != char.ToUpperInvariant(NegativeHemisphere))
            {
                throw new Exception($"Invalid {ValueName} hemisphere in '{Text}'.");
            }

            string Dms = Trimmed.Substring(1);
            string[] Parts = Dms.Split(':');
            if (Parts.Length != 3)
                throw new Exception($"Invalid {ValueName} format '{Text}'. Expected D:M:S.");

            double Degrees = ParseDouble(Parts[0], $"{ValueName} degrees");
            double Minutes = ParseDouble(Parts[1], $"{ValueName} minutes");
            double Seconds = ParseDouble(Parts[2], $"{ValueName} seconds");

            if (Minutes < 0 || Minutes >= 60 || Seconds < 0 || Seconds >= 60)
                throw new Exception($"Invalid {ValueName} DMS range in '{Text}'.");

            double Value = Degrees + (Minutes / 60.0) + (Seconds / 3600.0);
            if (Hemisphere == char.ToUpperInvariant(NegativeHemisphere))
                Value = -Value;

            return Value;
        }

        /// <summary>
        /// Formats latitude in multiplane DMS notation.
        /// </summary>
        /// <param name="Latitude">Latitude in decimal degrees.</param>
        /// <returns>Latitude formatted like N36:26:51.704.</returns>
        static private string FormatLatitude
            (
            double Latitude
            )
        {
            return FormatDms(Latitude, 'N', 'S');
        }

        /// <summary>
        /// Formats longitude in multiplane DMS notation.
        /// </summary>
        /// <param name="Longitude">Longitude in decimal degrees.</param>
        /// <returns>Longitude formatted like W090:43:42.432.</returns>
        static private string FormatLongitude
            (
            double Longitude
            )
        {
            return FormatDms(Longitude, 'E', 'W');
        }

        /// <summary>
        /// Converts a signed decimal-degree value to DMS text with hemisphere.
        /// </summary>
        /// <param name="Value">Signed decimal-degree value.</param>
        /// <param name="PositiveHemisphere">Hemisphere letter to use for non-negative values.</param>
        /// <param name="NegativeHemisphere">Hemisphere letter to use for negative values.</param>
        /// <returns>DMS-formatted coordinate text.</returns>
        static private string FormatDms
            (
            double Value,
            char PositiveHemisphere,
            char NegativeHemisphere
            )
        {
            char Hemisphere = Value >= 0 ? PositiveHemisphere : NegativeHemisphere;
            double Abs = Math.Abs(Value);
            int Degrees = (int)Math.Floor(Abs);
            double RemainingMinutes = (Abs - Degrees) * 60.0;
            int Minutes = (int)Math.Floor(RemainingMinutes);
            double Seconds = (RemainingMinutes - Minutes) * 60.0;

            // Correct rounding carry-over.
            if (Seconds >= 59.9995)
            {
                Seconds = 0;
                Minutes += 1;
            }

            if (Minutes >= 60)
            {
                Minutes = 0;
                Degrees += 1;
            }

            return string.Format(CultureInfo.InvariantCulture, "{0}{1:00}:{2:00}:{3:00.000}", Hemisphere, Degrees, Minutes, Seconds);
        }

        /// <summary>
        /// Parses a floating-point value using invariant culture.
        /// </summary>
        /// <param name="Text">Raw text containing a numeric value.</param>
        /// <param name="ValueName">Human-readable field name used in exception messages.</param>
        /// <returns>Parsed double value.</returns>
        static private double ParseDouble
            (
            string Text,
            string ValueName
            )
        {
            if (!double.TryParse(Text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double Value))
                throw new Exception($"Invalid numeric value for {ValueName}: '{Text}'.");
            return Value;
        }

        /// <summary>
        /// Converts feet to meters.
        /// </summary>
        /// <param name="Feet">Distance in feet.</param>
        /// <returns>Distance in meters.</returns>
        static private double FeetToMeters
            (
            double Feet
            )
        {
            return Feet * 0.3048;
        }

        /// <summary>
        /// Converts meters to feet.
        /// </summary>
        /// <param name="Meters">Distance in meters.</param>
        /// <returns>Distance in feet.</returns>
        static private double MetersToFeet
            (
            double Meters
            )
        {
            return Meters / 0.3048;
        }
    }
}
