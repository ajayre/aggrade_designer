using System.Globalization;

using Microsoft.Data.Sqlite;

using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Haul;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Survey;

namespace AgGradeDesigner.Core.Export
{
    /// <summary>
    /// Writes an AgGrade Control <c>.agcontrol</c> SQLite database from a Designer project
    /// (EXPORT-SPEC §5–§7, Appendix A).
    /// </summary>
    static public class AgControlExporter
    {
        /// <summary>
        /// Returns whether <c>.agcontrol</c> export is allowed for the project
        /// (requires a proposed surface / design grid — EXPORT-SPEC §7.1).
        /// </summary>
        /// <param name="Project">Active project document.</param>
        /// <returns>True when a proposed surface is present.</returns>
        static public bool CanExport
            (
            ProjectDocument Project
            )
        {
            return Project.HasDesign;
        }

        /// <summary>
        /// Builds the default suggested file name for an export save dialog (§7.1.5).
        /// </summary>
        /// <param name="Project">Active project document.</param>
        /// <returns>File name including the <c>.agcontrol</c> extension.</returns>
        static public string SuggestFileName
            (
            ProjectDocument Project
            )
        {
            if (Project == null)
                return AgControlConstants.DEFAULT_FILE_NAME;

            string BaseName = Project.DisplayName;
            if (string.IsNullOrWhiteSpace(BaseName) ||
                string.Equals(BaseName, "Untitled", StringComparison.OrdinalIgnoreCase))
            {
                return AgControlConstants.DEFAULT_FILE_NAME;
            }

            return BaseName + AgControlConstants.FILE_EXTENSION;
        }

        /// <summary>
        /// Ensures <paramref name="FilePath"/> ends with the <c>.agcontrol</c> extension.
        /// </summary>
        /// <param name="FilePath">User-selected or constructed path.</param>
        /// <returns>Path with the correct extension.</returns>
        static public string EnsureExtension
            (
            string FilePath
            )
        {
            if (string.IsNullOrWhiteSpace(FilePath))
                throw new ArgumentException("Export path is required.", nameof(FilePath));

            if (FilePath.EndsWith(AgControlConstants.FILE_EXTENSION, StringComparison.OrdinalIgnoreCase))
                return FilePath;

            return FilePath + AgControlConstants.FILE_EXTENSION;
        }

        /// <summary>
        /// Exports the project to a new <c>.agcontrol</c> SQLite file at <paramref name="FilePath"/>.
        /// </summary>
        /// <param name="FilePath">Destination path (typically ending in <c>.agcontrol</c>).</param>
        /// <param name="Project">Project supplying design, optional haul, and survey benchmarks.</param>
        /// <param name="Progress">Optional 0–100 progress reporter.</param>
        /// <returns>Validation result that was applied (always valid when this method returns).</returns>
        /// <exception cref="ArgumentNullException">Project is null.</exception>
        /// <exception cref="InvalidOperationException">Validation failed or the project cannot be exported.</exception>
        static public AgControlExportValidationResult Export
            (
            string FilePath,
            ProjectDocument Project,
            IProgress<int>? Progress = null
            )
        {
            if (Project == null)
                throw new ArgumentNullException(nameof(Project));

            AgControlExportValidationResult Validation = AgControlExportValidator.Validate(Project);
            if (!Validation.IsValid)
            {
                throw new InvalidOperationException(Validation.FormatErrors());
            }

            ReportProgress(Progress, AgControlConstants.PROGRESS_VALIDATED_PERCENT);

            string ResolvedPath = EnsureExtension(FilePath);
            DesignGrid Grid = Project.DesignGrid!;
            Grid.EnsureCentroidsFromUtm();

            // intent: replace any prior file so schema and rows match this export exactly (§7.1.5 overwrite)
            if (File.Exists(ResolvedPath))
                File.Delete(ResolvedPath);

            SqliteConnectionStringBuilder Builder = new SqliteConnectionStringBuilder
            {
                DataSource = ResolvedPath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                // intent: avoid pooled handles keeping the .agcontrol file locked after export
                Pooling = false
            };

            using (SqliteConnection Connection = new SqliteConnection(Builder.ConnectionString))
            {
                Connection.Open();
                ApplyPragmas(Connection);
                CreateSchema(Connection);
                ReportProgress(Progress, AgControlConstants.PROGRESS_SCHEMA_PERCENT);

                using (SqliteTransaction Transaction = Connection.BeginTransaction())
                {
                    WriteFieldState(Connection, Transaction, Grid);
                    ReportProgress(Progress, AgControlConstants.PROGRESS_FIELD_STATE_PERCENT);

                    WriteData(Connection, Transaction, Grid);
                    WriteBenchmarks(Connection, Transaction, Project.Survey);
                    ReportProgress(Progress, AgControlConstants.PROGRESS_DATA_PERCENT);

                    if (Project.HasHaulData && (Project.HaulData != null))
                    {
                        WriteHaulPlan(Connection, Transaction, Grid, Project.HaulData);
                        ReportProgress(Progress, AgControlConstants.PROGRESS_HAUL_PERCENT);

                        WriteFlowArrows(Connection, Transaction, Grid, Project);
                        WritePivotTables(Connection, Transaction, Project);
                        ReportProgress(Progress, AgControlConstants.PROGRESS_OVERLAYS_PERCENT);
                    }

                    Transaction.Commit();
                }
            }

            ReportProgress(Progress, AgControlConstants.PROGRESS_COMPLETE_PERCENT);
            return Validation;
        }

        /// <summary>
        /// Reports a progress percent when a reporter is supplied.
        /// </summary>
        /// <param name="Progress">Optional progress sink.</param>
        /// <param name="Percent">Progress percent in 0–100.</param>
        static private void ReportProgress
            (
            IProgress<int>? Progress,
            int Percent
            )
        {
            Progress?.Report(Percent);
        }

        /// <summary>
        /// Applies foreign-key enforcement and the export <c>user_version</c>.
        /// </summary>
        /// <param name="Connection">Open SQLite connection.</param>
        static private void ApplyPragmas
            (
            SqliteConnection Connection
            )
        {
            ExecuteNonQuery(Connection, null, "PRAGMA foreign_keys = ON;");
            ExecuteNonQuery(
                Connection,
                null,
                "PRAGMA user_version = " + AgControlConstants.USER_VERSION.ToString(CultureInfo.InvariantCulture) + ";");
        }

        /// <summary>
        /// Creates all Appendix A tables and indexes on an empty database.
        /// </summary>
        /// <param name="Connection">Open SQLite connection.</param>
        static private void CreateSchema
            (
            SqliteConnection Connection
            )
        {
            foreach (string Statement in AgControlSchema.CreateStatements)
                ExecuteNonQuery(Connection, null, Statement);
        }

        /// <summary>
        /// Inserts one <c>FieldState</c> row per valid design bin (EXPORT-SPEC §6.5–§6.6).
        /// </summary>
        /// <param name="Connection">Open SQLite connection.</param>
        /// <param name="Transaction">Active write transaction.</param>
        /// <param name="Grid">Design grid with proposed elevations.</param>
        static private void WriteFieldState
            (
            SqliteConnection Connection,
            SqliteTransaction Transaction,
            DesignGrid Grid
            )
        {
            const string Sql =
                "INSERT INTO FieldState (X, Y, InitialHeightM, CurrentHeightM, TargetHeightM, CentroidLat, CentroidLon, HaulPath) " +
                "VALUES ($X, $Y, $Initial, $Current, $Target, $Lat, $Lon, $HaulPath);";

            using (SqliteCommand Command = Connection.CreateCommand())
            {
                Command.Transaction = Transaction;
                Command.CommandText = Sql;
                SqliteParameter ParamX = Command.Parameters.Add("$X", SqliteType.Integer);
                SqliteParameter ParamY = Command.Parameters.Add("$Y", SqliteType.Integer);
                SqliteParameter ParamInitial = Command.Parameters.Add("$Initial", SqliteType.Real);
                SqliteParameter ParamCurrent = Command.Parameters.Add("$Current", SqliteType.Real);
                SqliteParameter ParamTarget = Command.Parameters.Add("$Target", SqliteType.Real);
                SqliteParameter ParamLat = Command.Parameters.Add("$Lat", SqliteType.Real);
                SqliteParameter ParamLon = Command.Parameters.Add("$Lon", SqliteType.Real);
                SqliteParameter ParamHaulPath = Command.Parameters.Add("$HaulPath", SqliteType.Integer);

                foreach (GridBinData Bin in Grid.Bins)
                {
                    if (!Bin.Valid)
                        continue;

                    // intent: Designer guidance uses HaulRoutes/HaulPaths — never stamp bin→path ownership
                    ParamX.Value = Bin.X;
                    ParamY.Value = Bin.Y;
                    ParamInitial.Value = Bin.ExistingM;
                    ParamCurrent.Value = Bin.ExistingM;
                    ParamTarget.Value = Bin.ProposedM;
                    ParamLat.Value = Bin.CentroidLat;
                    ParamLon.Value = Bin.CentroidLon;
                    ParamHaulPath.Value = AgControlConstants.FIELD_STATE_HAUL_PATH_UNUSED;
                    Command.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Writes Control-familiar <c>Data</c> keys plus Designer provenance (EXPORT-SPEC §6.3, §6.5.3).
        /// </summary>
        /// <param name="Connection">Open SQLite connection.</param>
        /// <param name="Transaction">Active write transaction.</param>
        /// <param name="Grid">Design grid supplying extents and design cut/fill totals.</param>
        static private void WriteData
            (
            SqliteConnection Connection,
            SqliteTransaction Transaction,
            DesignGrid Grid
            )
        {
            ComputeLatLonExtents(Grid, out double MinLat, out double MinLon, out double MaxLat, out double MaxLon);

            InsertData(Connection, Transaction, AgControlConstants.DATA_NAME_GRID_WIDTH, Grid.Width);
            InsertData(Connection, Transaction, AgControlConstants.DATA_NAME_GRID_HEIGHT, Grid.Height);
            InsertData(Connection, Transaction, AgControlConstants.DATA_NAME_MEAN_LAT, Grid.MeanLat);
            InsertData(Connection, Transaction, AgControlConstants.DATA_NAME_MEAN_LON, Grid.MeanLon);
            InsertData(Connection, Transaction, AgControlConstants.DATA_NAME_MIN_X, Grid.MinX);
            InsertData(Connection, Transaction, AgControlConstants.DATA_NAME_MIN_Y, Grid.MinY);
            InsertData(Connection, Transaction, AgControlConstants.DATA_NAME_MAX_X, Grid.MaxX);
            InsertData(Connection, Transaction, AgControlConstants.DATA_NAME_MAX_Y, Grid.MaxY);
            InsertData(Connection, Transaction, AgControlConstants.DATA_NAME_MIN_LAT, MinLat);
            InsertData(Connection, Transaction, AgControlConstants.DATA_NAME_MIN_LON, MinLon);
            InsertData(Connection, Transaction, AgControlConstants.DATA_NAME_MAX_LAT, MaxLat);
            InsertData(Connection, Transaction, AgControlConstants.DATA_NAME_MAX_LON, MaxLon);
            // intent: always design totals — haul BCY lives on HaulRoutes, not Data
            InsertData(Connection, Transaction, AgControlConstants.DATA_NAME_TOTAL_CUT_CY, Grid.TotalCutCy);
            InsertData(Connection, Transaction, AgControlConstants.DATA_NAME_TOTAL_FILL_CY, Grid.TotalFillCy);
            InsertData(Connection, Transaction, AgControlConstants.DATA_NAME_COMPLETED_CUT_CY, AgControlConstants.DATA_COMPLETED_CUT_CY);
            InsertData(Connection, Transaction, AgControlConstants.DATA_NAME_COMPLETED_FILL_CY, AgControlConstants.DATA_COMPLETED_FILL_CY);
            InsertData(Connection, Transaction, AgControlConstants.DATA_NAME_CALIBRATED, AgControlConstants.DATA_CALIBRATED_FALSE);
            InsertData(Connection, Transaction, AgControlConstants.DATA_NAME_HEIGHT_OFFSET_M, AgControlConstants.DATA_HEIGHT_OFFSET_M);
            InsertData(Connection, Transaction, AgControlConstants.DATA_NAME_EASTING_OFFSET_M, AgControlConstants.DATA_EASTING_OFFSET_M);
            InsertData(Connection, Transaction, AgControlConstants.DATA_NAME_NORTHING_OFFSET_M, AgControlConstants.DATA_NORTHING_OFFSET_M);
            InsertData(Connection, Transaction, AgControlConstants.DATA_NAME_PRODUCER_DESIGNER, AgControlConstants.DATA_PRODUCER_DESIGNER);
        }

        /// <summary>
        /// Inserts one named <c>Data</c> key/value row.
        /// </summary>
        /// <param name="Connection">Open SQLite connection.</param>
        /// <param name="Transaction">Active write transaction.</param>
        /// <param name="Name">Data key name.</param>
        /// <param name="Value">Numeric value.</param>
        static private void InsertData
            (
            SqliteConnection Connection,
            SqliteTransaction Transaction,
            string Name,
            double Value
            )
        {
            using (SqliteCommand Command = Connection.CreateCommand())
            {
                Command.Transaction = Transaction;
                Command.CommandText = "INSERT INTO Data (Name, Value) VALUES ($Name, $Value);";
                Command.Parameters.AddWithValue("$Name", Name);
                Command.Parameters.AddWithValue("$Value", Value);
                Command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Computes WGS-84 extents from valid bin centroids.
        /// </summary>
        /// <param name="Grid">Design grid.</param>
        /// <param name="MinLat">Minimum latitude.</param>
        /// <param name="MinLon">Minimum longitude.</param>
        /// <param name="MaxLat">Maximum latitude.</param>
        /// <param name="MaxLon">Maximum longitude.</param>
        static private void ComputeLatLonExtents
            (
            DesignGrid Grid,
            out double MinLat,
            out double MinLon,
            out double MaxLat,
            out double MaxLon
            )
        {
            MinLat = double.PositiveInfinity;
            MinLon = double.PositiveInfinity;
            MaxLat = double.NegativeInfinity;
            MaxLon = double.NegativeInfinity;
            bool SawValid = false;

            foreach (GridBinData Bin in Grid.Bins)
            {
                if (!Bin.Valid)
                    continue;

                SawValid = true;
                if (Bin.CentroidLat < MinLat) MinLat = Bin.CentroidLat;
                if (Bin.CentroidLon < MinLon) MinLon = Bin.CentroidLon;
                if (Bin.CentroidLat > MaxLat) MaxLat = Bin.CentroidLat;
                if (Bin.CentroidLon > MaxLon) MaxLon = Bin.CentroidLon;
            }

            if (!SawValid)
            {
                MinLat = Grid.MeanLat;
                MinLon = Grid.MeanLon;
                MaxLat = Grid.MeanLat;
                MaxLon = Grid.MeanLon;
            }
        }

        /// <summary>
        /// Inserts survey benchmarks when present (EXPORT-SPEC §6.7).
        /// </summary>
        /// <param name="Connection">Open SQLite connection.</param>
        /// <param name="Transaction">Active write transaction.</param>
        /// <param name="Survey">Survey model, or null when absent.</param>
        static private void WriteBenchmarks
            (
            SqliteConnection Connection,
            SqliteTransaction Transaction,
            SurveyModel? Survey
            )
        {
            if ((Survey == null) || (Survey.Benchmarks.Count == 0))
                return;

            const string Sql =
                "INSERT INTO Benchmarks (Latitude, Longitude, Name, ElevationM) VALUES ($Lat, $Lon, $Name, $Elev);";

            using (SqliteCommand Command = Connection.CreateCommand())
            {
                Command.Transaction = Transaction;
                Command.CommandText = Sql;
                SqliteParameter ParamLat = Command.Parameters.Add("$Lat", SqliteType.Real);
                SqliteParameter ParamLon = Command.Parameters.Add("$Lon", SqliteType.Real);
                SqliteParameter ParamName = Command.Parameters.Add("$Name", SqliteType.Text);
                SqliteParameter ParamElev = Command.Parameters.Add("$Elev", SqliteType.Real);

                foreach (Benchmark Bench in Survey.Benchmarks)
                {
                    ParamLat.Value = Bench.Location.Latitude;
                    ParamLon.Value = Bench.Location.Longitude;
                    ParamName.Value = Bench.Name ?? string.Empty;
                    ParamElev.Value = Bench.Elevation;
                    Command.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Writes ordered cut-fill pairs and guidance points when haul data is present (EXPORT-SPEC §6.1).
        /// </summary>
        /// <param name="Connection">Open SQLite connection.</param>
        /// <param name="Transaction">Active write transaction.</param>
        /// <param name="Grid">Design grid for cut-start bin indices.</param>
        /// <param name="Haul">Valid haul payload.</param>
        static private void WriteHaulPlan
            (
            SqliteConnection Connection,
            SqliteTransaction Transaction,
            DesignGrid Grid,
            HaulData Haul
            )
        {
            IReadOnlyList<HaulPassDto> Passes = Haul.Payload.OrderedPasses;
            IReadOnlyList<HaulCutFillPairDto> Pairs = Haul.ResolveCutFillPairs();
            IReadOnlyList<HaulGuidancePointDto> Guidance = Haul.Payload.GuidancePoints;

            for (int RouteIndex = 0; RouteIndex < Passes.Count; RouteIndex++)
            {
                int HaulPath = RouteIndex + 1;
                HaulPassDto Pass = Passes[RouteIndex];
                HaulCutFillPairDto? Pair = FindPair(Pairs, HaulPath);

                ResolveCutStartBin(Grid, Pass.CutStartX, Pass.CutStartY, out int CutStartBinX, out int CutStartBinY);
                double BladeDepthMm = ResolveBladeDepthMm(HaulPath, Pass, Guidance);

                InsertHaulRoute(
                    Connection,
                    Transaction,
                    HaulPath,
                    BladeDepthMm,
                    CutStartBinX,
                    CutStartBinY,
                    Pass,
                    Pair);
            }

            WriteHaulPaths(Connection, Transaction, Guidance);
        }

        /// <summary>
        /// Locates the cut/fill pair summary for a 1-based route id.
        /// </summary>
        /// <param name="Pairs">Route-ordered pair summaries.</param>
        /// <param name="HaulPath">1-based route / pass id.</param>
        /// <returns>Matching pair, or null when missing.</returns>
        static private HaulCutFillPairDto? FindPair
            (
            IReadOnlyList<HaulCutFillPairDto> Pairs,
            int HaulPath
            )
        {
            foreach (HaulCutFillPairDto Pair in Pairs)
            {
                if (Pair.Order == HaulPath)
                    return Pair;
            }

            int ZeroBased = HaulPath - 1;
            if ((ZeroBased >= 0) && (ZeroBased < Pairs.Count))
                return Pairs[ZeroBased];

            return null;
        }

        /// <summary>
        /// Maps cut-start UTM to the enclosing 2 ft design-grid bin indices.
        /// </summary>
        /// <param name="Grid">Design grid.</param>
        /// <param name="EastingM">Cut-start easting (blade centre) in metres.</param>
        /// <param name="NorthingM">Cut-start northing (blade centre) in metres.</param>
        /// <param name="BinX">Output column index.</param>
        /// <param name="BinY">Output row index.</param>
        static private void ResolveCutStartBin
            (
            DesignGrid Grid,
            double EastingM,
            double NorthingM,
            out int BinX,
            out int BinY
            )
        {
            double BinSizeM = Grid.BinSizeM > 0 ? Grid.BinSizeM : FieldConstants.BIN_SIZE_M;
            BinX = (int)Math.Floor((EastingM - Grid.OriginEastingM) / BinSizeM);
            BinY = (int)Math.Floor((NorthingM - Grid.OriginNorthingM) / BinSizeM);
        }

        /// <summary>
        /// Resolves the pair blade depth from CUT guidance, else max cut-cell depth in mm.
        /// </summary>
        /// <param name="HaulPath">1-based route id.</param>
        /// <param name="Pass">Pass geometry and cut cells.</param>
        /// <param name="Guidance">All guidance waypoints.</param>
        /// <returns>Blade depth in millimetres.</returns>
        static private double ResolveBladeDepthMm
            (
            int HaulPath,
            HaulPassDto Pass,
            IReadOnlyList<HaulGuidancePointDto> Guidance
            )
        {
            foreach (HaulGuidancePointDto Point in Guidance)
            {
                if (Point.PassNo != HaulPath)
                    continue;

                if (!string.Equals(Point.Action, AgControlConstants.GUIDANCE_ACTION_CUT, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (Point.DepthMm > 0)
                    return Point.DepthMm;
            }

            double MaxDepthM = 0.0;
            foreach (HaulPassCellDto Cell in Pass.CutCells)
            {
                if (Cell.Depth > MaxDepthM)
                    MaxDepthM = Cell.Depth;
            }

            return MaxDepthM * AgControlConstants.MM_PER_METRE;
        }

        /// <summary>
        /// Inserts one <c>HaulRoutes</c> row for a cut-fill pair.
        /// </summary>
        /// <param name="Connection">Open SQLite connection.</param>
        /// <param name="Transaction">Active write transaction.</param>
        /// <param name="HaulPath">1-based route id.</param>
        /// <param name="BladeDepthMm">Pair cutting depth in millimetres.</param>
        /// <param name="CutStartBinX">Design-bin column under blade centre at cut start.</param>
        /// <param name="CutStartBinY">Design-bin row under blade centre at cut start.</param>
        /// <param name="Pass">Pass sweep endpoints and load.</param>
        /// <param name="Pair">Optional pair timing/cumulative stats.</param>
        static private void InsertHaulRoute
            (
            SqliteConnection Connection,
            SqliteTransaction Transaction,
            int HaulPath,
            double BladeDepthMm,
            int CutStartBinX,
            int CutStartBinY,
            HaulPassDto Pass,
            HaulCutFillPairDto? Pair
            )
        {
            const string Sql =
                "INSERT INTO HaulRoutes (" +
                "HaulPath, BladeDepthMm, CutStartBinX, CutStartBinY, " +
                "CutStartEastingM, CutStartNorthingM, CutEndEastingM, CutEndNorthingM, " +
                "FillStartEastingM, FillStartNorthingM, FillEndEastingM, FillEndNorthingM, " +
                "LoadBcy, HaulDistanceM, RouteTimeSeconds, TotalCutBcyBefore, TotalFillBcyBefore) " +
                "VALUES (" +
                "$HaulPath, $BladeDepthMm, $CutStartBinX, $CutStartBinY, " +
                "$CutStartE, $CutStartN, $CutEndE, $CutEndN, " +
                "$FillStartE, $FillStartN, $FillEndE, $FillEndN, " +
                "$LoadBcy, $HaulDistanceM, $RouteTimeSeconds, $TotalCutBcyBefore, $TotalFillBcyBefore);";

            using (SqliteCommand Command = Connection.CreateCommand())
            {
                Command.Transaction = Transaction;
                Command.CommandText = Sql;
                Command.Parameters.AddWithValue("$HaulPath", HaulPath);
                Command.Parameters.AddWithValue("$BladeDepthMm", BladeDepthMm);
                Command.Parameters.AddWithValue("$CutStartBinX", CutStartBinX);
                Command.Parameters.AddWithValue("$CutStartBinY", CutStartBinY);
                Command.Parameters.AddWithValue("$CutStartE", Pass.CutStartX);
                Command.Parameters.AddWithValue("$CutStartN", Pass.CutStartY);
                Command.Parameters.AddWithValue("$CutEndE", Pass.CutEndX);
                Command.Parameters.AddWithValue("$CutEndN", Pass.CutEndY);
                Command.Parameters.AddWithValue("$FillStartE", Pass.FillStartX);
                Command.Parameters.AddWithValue("$FillStartN", Pass.FillStartY);
                Command.Parameters.AddWithValue("$FillEndE", Pass.FillEndX);
                Command.Parameters.AddWithValue("$FillEndN", Pass.FillEndY);
                Command.Parameters.AddWithValue("$LoadBcy", Pair?.LoadBcy ?? Pass.LoadBcy);
                Command.Parameters.AddWithValue("$HaulDistanceM", Pair?.HaulDistanceM ?? Pass.HaulDistanceM);
                Command.Parameters.AddWithValue("$RouteTimeSeconds", Pair?.RouteTimeSeconds ?? 0.0);
                Command.Parameters.AddWithValue("$TotalCutBcyBefore", Pair?.TotalCutBcyBefore ?? 0.0);
                Command.Parameters.AddWithValue("$TotalFillBcyBefore", Pair?.TotalFillBcyBefore ?? 0.0);
                Command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Inserts expanded <c>HaulPaths</c> rows from guidance waypoints (EXPORT-SPEC §5.3, §6.1.4).
        /// </summary>
        /// <param name="Connection">Open SQLite connection.</param>
        /// <param name="Transaction">Active write transaction.</param>
        /// <param name="Guidance">Guidance waypoints.</param>
        static private void WriteHaulPaths
            (
            SqliteConnection Connection,
            SqliteTransaction Transaction,
            IReadOnlyList<HaulGuidancePointDto> Guidance
            )
        {
            if (Guidance.Count == 0)
                return;

            const string Sql =
                "INSERT INTO HaulPaths (" +
                "HaulPath, PointNumber, Latitude, Longitude, Seq, Action, Blade, " +
                "EastingM, NorthingM, DepthMm, HeadingDeg) " +
                "VALUES (" +
                "$HaulPath, $PointNumber, $Lat, $Lon, $Seq, $Action, $Blade, " +
                "$EastingM, $NorthingM, $DepthMm, $HeadingDeg);";

            Dictionary<int, int> PointNumberByPath = new Dictionary<int, int>();

            using (SqliteCommand Command = Connection.CreateCommand())
            {
                Command.Transaction = Transaction;
                Command.CommandText = Sql;
                SqliteParameter ParamHaulPath = Command.Parameters.Add("$HaulPath", SqliteType.Integer);
                SqliteParameter ParamPointNumber = Command.Parameters.Add("$PointNumber", SqliteType.Integer);
                SqliteParameter ParamLat = Command.Parameters.Add("$Lat", SqliteType.Real);
                SqliteParameter ParamLon = Command.Parameters.Add("$Lon", SqliteType.Real);
                SqliteParameter ParamSeq = Command.Parameters.Add("$Seq", SqliteType.Integer);
                SqliteParameter ParamAction = Command.Parameters.Add("$Action", SqliteType.Text);
                SqliteParameter ParamBlade = Command.Parameters.Add("$Blade", SqliteType.Text);
                SqliteParameter ParamEasting = Command.Parameters.Add("$EastingM", SqliteType.Real);
                SqliteParameter ParamNorthing = Command.Parameters.Add("$NorthingM", SqliteType.Real);
                SqliteParameter ParamDepth = Command.Parameters.Add("$DepthMm", SqliteType.Real);
                SqliteParameter ParamHeading = Command.Parameters.Add("$HeadingDeg", SqliteType.Real);

                foreach (HaulGuidancePointDto Point in Guidance)
                {
                    int HaulPath = Point.PassNo;
                    if (!PointNumberByPath.TryGetValue(HaulPath, out int PointNumber))
                        PointNumber = 0;

                    PointNumber++;
                    PointNumberByPath[HaulPath] = PointNumber;

                    ParamHaulPath.Value = HaulPath;
                    ParamPointNumber.Value = PointNumber;
                    ParamLat.Value = Point.Lat;
                    ParamLon.Value = Point.Lon;
                    ParamSeq.Value = Point.Seq;
                    ParamAction.Value = Point.Action ?? string.Empty;
                    ParamBlade.Value = Point.Blade ?? string.Empty;
                    ParamEasting.Value = Point.X;
                    ParamNorthing.Value = Point.Y;
                    ParamDepth.Value = Point.DepthMm;
                    ParamHeading.Value = Point.HeadingDeg;
                    Command.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Writes flow-arrow samples honouring <c>ArrowStride</c> (EXPORT-SPEC §6.4).
        /// </summary>
        /// <param name="Connection">Open SQLite connection.</param>
        /// <param name="Transaction">Active write transaction.</param>
        /// <param name="DesignGrid">Design grid used to omit arrows outside coverage.</param>
        /// <param name="Project">Project supplying haul flow and settings.</param>
        static private void WriteFlowArrows
            (
            SqliteConnection Connection,
            SqliteTransaction Transaction,
            DesignGrid DesignGrid,
            ProjectDocument Project
            )
        {
            HaulFlowFieldResolver.ResolvedHaulFlow? Resolved = HaulFlowFieldResolver.TryResolve(
                Project.HaulData,
                Project,
                Project.HaulSettings);

            if ((Resolved == null) || !Resolved.HasVisibleArrows)
                return;

            HaulBuilder.Grid HaulGrid = Resolved.Grid;
            HaulBuilder.FlowField Flow = Resolved.Flow;
            int ArrowStride = Math.Max(1, Resolved.ArrowStride);

            const string Sql =
                "INSERT INTO HaulArrows (Latitude, Longitude, Heading) VALUES ($Lat, $Lon, $Heading);";

            using (SqliteCommand Command = Connection.CreateCommand())
            {
                Command.Transaction = Transaction;
                Command.CommandText = Sql;
                SqliteParameter ParamLat = Command.Parameters.Add("$Lat", SqliteType.Real);
                SqliteParameter ParamLon = Command.Parameters.Add("$Lon", SqliteType.Real);
                SqliteParameter ParamHeading = Command.Parameters.Add("$Heading", SqliteType.Real);

                for (int By = 0; By < HaulGrid.Height; By += ArrowStride)
                {
                    for (int Bx = 0; Bx < HaulGrid.Width; Bx += ArrowStride)
                    {
                        if (!HaulGrid.Valid[By, Bx] || !Flow.HasArrow[By, Bx])
                            continue;

                        if (!HaulFlowArrowGeometry.TryGetCellCenterLatLon(HaulGrid, Bx, By, out double Lat, out double Lon))
                            continue;

                        (double EastingM, double NorthingM) = HaulGrid.CellCenterXy(Bx, By);
                        // intent: omit arrows whose centres fall outside the design surface
                        if (!IsInsideDesignCoverage(DesignGrid, EastingM, NorthingM))
                            continue;

                        double HeadingDeg = NormalizeHeadingDeg(Flow.DirectionDeg(Bx, By));
                        ParamLat.Value = Lat;
                        ParamLon.Value = Lon;
                        ParamHeading.Value = HeadingDeg;
                        Command.ExecuteNonQuery();
                    }
                }
            }
        }

        /// <summary>
        /// Returns whether a UTM point lies in a valid design-grid bin.
        /// </summary>
        /// <param name="Grid">Design grid.</param>
        /// <param name="EastingM">Easting in metres.</param>
        /// <param name="NorthingM">Northing in metres.</param>
        /// <returns>True when the enclosing bin is valid.</returns>
        static private bool IsInsideDesignCoverage
            (
            DesignGrid Grid,
            double EastingM,
            double NorthingM
            )
        {
            ResolveCutStartBin(Grid, EastingM, NorthingM, out int BinX, out int BinY);
            return Grid.TryGetValidBin(BinX, BinY, out _);
        }

        /// <summary>
        /// Normalizes a heading to <c>[0, 360)</c> degrees clockwise from north.
        /// </summary>
        /// <param name="HeadingDeg">Raw heading in degrees.</param>
        /// <returns>Normalized heading.</returns>
        static private double NormalizeHeadingDeg
            (
            double HeadingDeg
            )
        {
            double Normalized = HeadingDeg % FieldConstants.DEGREES_FULL_CIRCLE;
            if (Normalized < 0)
                Normalized += FieldConstants.DEGREES_FULL_CIRCLE;

            return Normalized;
        }

        /// <summary>
        /// Writes pivot config, moves, and per-pass bearings when pivot overlay data exists (EXPORT-SPEC §6.2).
        /// </summary>
        /// <param name="Connection">Open SQLite connection.</param>
        /// <param name="Transaction">Active write transaction.</param>
        /// <param name="Project">Project supplying haul overlay and pivot settings.</param>
        static private void WritePivotTables
            (
            SqliteConnection Connection,
            SqliteTransaction Transaction,
            ProjectDocument Project
            )
        {
            HaulPivotOverlayDto? Overlay = Project.HaulData?.ResolvePivotOverlay();
            if ((Overlay == null) || (Overlay.RadiusM <= 0.0))
                return;

            HaulSettings Settings = Project.HaulSettings;
            InsertPivotConfig(
                Connection,
                Transaction,
                Settings.PivotLatitudeDeg,
                Settings.PivotLongitudeDeg,
                Overlay.RadiusM);

            foreach (HaulPivotMoveDto Move in Overlay.Moves)
            {
                InsertPivotMove(Connection, Transaction, Move);
            }

            for (int Index = 0; Index < Overlay.BearingByPassDeg.Length; Index++)
            {
                int HaulPath = Index + 1;
                InsertPivotPassBearing(Connection, Transaction, HaulPath, Overlay.BearingByPassDeg[Index]);
            }
        }

        /// <summary>
        /// Inserts the single <c>PivotConfig</c> row.
        /// </summary>
        /// <param name="Connection">Open SQLite connection.</param>
        /// <param name="Transaction">Active write transaction.</param>
        /// <param name="CenterLatitude">Pivot centre latitude in degrees.</param>
        /// <param name="CenterLongitude">Pivot centre longitude in degrees.</param>
        /// <param name="RadiusM">Pivot arm radius in metres.</param>
        static private void InsertPivotConfig
            (
            SqliteConnection Connection,
            SqliteTransaction Transaction,
            double CenterLatitude,
            double CenterLongitude,
            double RadiusM
            )
        {
            using (SqliteCommand Command = Connection.CreateCommand())
            {
                Command.Transaction = Transaction;
                Command.CommandText =
                    "INSERT INTO PivotConfig (PivotConfigID, CenterLatitude, CenterLongitude, RadiusM) " +
                    "VALUES ($Id, $Lat, $Lon, $Radius);";
                Command.Parameters.AddWithValue("$Id", AgControlConstants.PIVOT_CONFIG_SINGLE_ROW_ID);
                Command.Parameters.AddWithValue("$Lat", CenterLatitude);
                Command.Parameters.AddWithValue("$Lon", CenterLongitude);
                Command.Parameters.AddWithValue("$Radius", RadiusM);
                Command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Inserts one <c>PivotMoves</c> schedule row.
        /// </summary>
        /// <param name="Connection">Open SQLite connection.</param>
        /// <param name="Transaction">Active write transaction.</param>
        /// <param name="Move">Pivot move DTO.</param>
        static private void InsertPivotMove
            (
            SqliteConnection Connection,
            SqliteTransaction Transaction,
            HaulPivotMoveDto Move
            )
        {
            using (SqliteCommand Command = Connection.CreateCommand())
            {
                Command.Transaction = Transaction;
                Command.CommandText =
                    "INSERT INTO PivotMoves (MoveIndex, FirstRoute, LastRoute, BearingDeg, MoveDeg) " +
                    "VALUES ($MoveIndex, $FirstRoute, $LastRoute, $BearingDeg, $MoveDeg);";
                Command.Parameters.AddWithValue("$MoveIndex", Move.Index);
                Command.Parameters.AddWithValue("$FirstRoute", Move.FirstRoute);
                Command.Parameters.AddWithValue("$LastRoute", Move.LastRoute);
                Command.Parameters.AddWithValue("$BearingDeg", Move.BearingDeg);
                Command.Parameters.AddWithValue("$MoveDeg", Move.MoveDeg);
                Command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Inserts one <c>PivotPassBearings</c> row for a haul path.
        /// </summary>
        /// <param name="Connection">Open SQLite connection.</param>
        /// <param name="Transaction">Active write transaction.</param>
        /// <param name="HaulPath">1-based route id.</param>
        /// <param name="BearingDeg">Held bearing in degrees.</param>
        static private void InsertPivotPassBearing
            (
            SqliteConnection Connection,
            SqliteTransaction Transaction,
            int HaulPath,
            double BearingDeg
            )
        {
            using (SqliteCommand Command = Connection.CreateCommand())
            {
                Command.Transaction = Transaction;
                Command.CommandText =
                    "INSERT INTO PivotPassBearings (HaulPath, BearingDeg) VALUES ($HaulPath, $BearingDeg);";
                Command.Parameters.AddWithValue("$HaulPath", HaulPath);
                Command.Parameters.AddWithValue("$BearingDeg", BearingDeg);
                Command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Executes a non-query SQL statement optionally inside a transaction.
        /// </summary>
        /// <param name="Connection">Open SQLite connection.</param>
        /// <param name="Transaction">Optional transaction, or null.</param>
        /// <param name="Sql">SQL text to execute.</param>
        static private void ExecuteNonQuery
            (
            SqliteConnection Connection,
            SqliteTransaction? Transaction,
            string Sql
            )
        {
            using (SqliteCommand Command = Connection.CreateCommand())
            {
                Command.Transaction = Transaction;
                Command.CommandText = Sql;
                Command.ExecuteNonQuery();
            }
        }
    }
}
