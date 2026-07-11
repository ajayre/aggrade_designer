using Microsoft.Data.Sqlite;

using AgGradeDesigner.Core;
using AgGradeDesigner.Core.Design;
using AgGradeDesigner.Core.Export;
using AgGradeDesigner.Core.Haul;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Survey;
using AgGradeDesigner.Core.Tests.TestFixtures;

namespace AgGradeDesigner.Core.Tests.Export
{
    /// <summary>
    /// Unit tests for <see cref="AgControlExporter"/> <c>.agcontrol</c> SQLite export (EXPORT-SPEC Phase 1–2).
    /// </summary>
    public sealed class AgControlExporterTests
    {
        /// <summary>Main slope direction used when building a design fixture.</summary>
        private const double MAIN_SLOPE_DIRECTION_DEG = 90.0;

        /// <summary>Zero slope percent for a flat design fixture.</summary>
        private const double ZERO_SLOPE_PERCENT = 0.0;

        /// <summary>Synthetic guidance depth in millimetres for haul-plan tests.</summary>
        private const double GUIDANCE_DEPTH_MM = 50.0;

        /// <summary>Synthetic cut-cell depth in metres for haul-plan tests.</summary>
        private const double CUT_CELL_DEPTH_M = 0.05;

        /// <summary>Synthetic load in bank cubic yards for haul-plan tests.</summary>
        private const double LOAD_BCY = 12.5;

        /// <summary>Synthetic haul distance in metres for haul-plan tests.</summary>
        private const double HAUL_DISTANCE_M = 40.0;

        /// <summary>Synthetic pivot radius in metres.</summary>
        private const double PIVOT_RADIUS_M = 120.0;

        /// <summary>Synthetic pivot centre latitude in degrees.</summary>
        private const double PIVOT_LAT_DEG = 40.1;

        /// <summary>Synthetic pivot centre longitude in degrees.</summary>
        private const double PIVOT_LON_DEG = -100.2;

        /// <summary>Synthetic held pivot bearing in degrees.</summary>
        private const double PIVOT_BEARING_DEG = 45.0;

        /// <summary>
        /// Appendix A table names that must exist after export.
        /// </summary>
        static private readonly string[] RequiredTables =
        {
            "FieldState",
            "Data",
            "HaulArrows",
            "Benchmarks",
            "Events",
            "LevelingOperation",
            "LevelingOperationBin",
            "HaulPaths",
            "HaulRoutes",
            "PivotConfig",
            "PivotMoves",
            "PivotPassBearings"
        };

        /// <summary>
        /// Verifies export is disabled when the project has no proposed surface.
        /// </summary>
        [Fact]
        public void CanExport_WithoutDesign_ReturnsFalse
            (
            )
        {
            ProjectDocument Project = new ProjectDocument();
            Assert.False(AgControlExporter.CanExport(Project));
        }

        /// <summary>
        /// Verifies topology-only export writes schema, FieldState, Data, and empty haul tables.
        /// </summary>
        [Fact]
        public void Export_WithoutHaul_WritesTopologyAndEmptyHaulTables
            (
            )
        {
            ProjectDocument Project = BuildProjectWithDesign();
            string Path = TempFile.Write(AgControlConstants.FILE_EXTENSION, string.Empty);
            try
            {
                AgControlExporter.Export(Path, Project);

                using (SqliteConnection Connection = OpenReadOnly(Path))
                {
                    Assert.Equal(AgControlConstants.USER_VERSION, ReadUserVersion(Connection));
                    Assert.True(CountRows(Connection, "FieldState") > 0);
                    Assert.Equal(0, CountRows(Connection, "HaulPaths"));
                    Assert.Equal(0, CountRows(Connection, "HaulRoutes"));
                    Assert.Equal(0, CountRows(Connection, "HaulArrows"));
                    Assert.Equal(0, CountRows(Connection, "PivotMoves"));
                    Assert.Equal(0, CountRows(Connection, "Events"));
                    Assert.Equal(0, CountRows(Connection, "LevelingOperation"));

                    Assert.Equal(0, ReadAllFieldStateHaulPaths(Connection));
                    Assert.Equal(
                        Project.DesignGrid!.TotalCutCy,
                        ReadDataValue(Connection, AgControlConstants.DATA_NAME_TOTAL_CUT_CY),
                        precision: 6);
                    Assert.Equal(
                        Project.DesignGrid.TotalFillCy,
                        ReadDataValue(Connection, AgControlConstants.DATA_NAME_TOTAL_FILL_CY),
                        precision: 6);
                    Assert.Equal(
                        AgControlConstants.DATA_COMPLETED_CUT_CY,
                        ReadDataValue(Connection, AgControlConstants.DATA_NAME_COMPLETED_CUT_CY));
                    Assert.Equal(
                        AgControlConstants.DATA_PRODUCER_DESIGNER,
                        ReadDataValue(Connection, AgControlConstants.DATA_NAME_PRODUCER_DESIGNER));
                    Assert.True(Project.Survey!.Benchmarks.Count == 0 || CountRows(Connection, "Benchmarks") >= 0);
                }
            }
            finally
            {
                TempFile.DeleteIfExists(Path);
            }
        }

        /// <summary>
        /// Verifies haul guidance and routes are written when a haul plan is present.
        /// </summary>
        [Fact]
        public void Export_WithHaulPlan_WritesRoutesAndPaths
            (
            )
        {
            ProjectDocument Project = BuildProjectWithDesign();
            Project.HaulData = BuildSyntheticHaulData(Project.DesignGrid!);
            string Path = TempFile.Write(AgControlConstants.FILE_EXTENSION, string.Empty);
            try
            {
                AgControlExporter.Export(Path, Project);

                using (SqliteConnection Connection = OpenReadOnly(Path))
                {
                    Assert.Equal(1, CountRows(Connection, "HaulRoutes"));
                    Assert.Equal(2, CountRows(Connection, "HaulPaths"));
                    Assert.Equal(0, ReadAllFieldStateHaulPaths(Connection));

                    using (SqliteCommand Command = Connection.CreateCommand())
                    {
                        Command.CommandText = "SELECT HaulPath, BladeDepthMm, LoadBcy FROM HaulRoutes LIMIT 1;";
                        using (SqliteDataReader Reader = Command.ExecuteReader())
                        {
                            Assert.True(Reader.Read());
                            Assert.Equal(1, Reader.GetInt32(0));
                            Assert.Equal(GUIDANCE_DEPTH_MM, Reader.GetDouble(1));
                            Assert.Equal(LOAD_BCY, Reader.GetDouble(2));
                        }
                    }
                }
            }
            finally
            {
                TempFile.DeleteIfExists(Path);
            }
        }

        /// <summary>
        /// Verifies export throws when called without a proposed surface.
        /// </summary>
        [Fact]
        public void Export_WithoutDesign_Throws
            (
            )
        {
            ProjectDocument Project = new ProjectDocument();
            string Path = TempFile.Write(AgControlConstants.FILE_EXTENSION, string.Empty);
            try
            {
                Assert.Throws<InvalidOperationException>(() => AgControlExporter.Export(Path, Project));
            }
            finally
            {
                TempFile.DeleteIfExists(Path);
            }
        }

        /// <summary>
        /// Verifies Appendix A tables are all present after a topology-only export.
        /// </summary>
        [Fact]
        public void Export_CreatesAllRequiredTables
            (
            )
        {
            ProjectDocument Project = BuildProjectWithDesign();
            string Path = TempFile.Write(AgControlConstants.FILE_EXTENSION, string.Empty);
            try
            {
                AgControlExporter.Export(Path, Project);

                using (SqliteConnection Connection = OpenReadOnly(Path))
                {
                    HashSet<string> Tables = ReadTableNames(Connection);
                    foreach (string TableName in RequiredTables)
                        Assert.Contains(TableName, Tables);
                }
            }
            finally
            {
                TempFile.DeleteIfExists(Path);
            }
        }

        /// <summary>
        /// Verifies FieldState elevations map existing→initial/current and proposed→target.
        /// </summary>
        [Fact]
        public void Export_FieldState_MapsExistingAndProposedHeights
            (
            )
        {
            ProjectDocument Project = BuildProjectWithDesign();
            GridBinData SeedBin = Project.DesignGrid!.Bins.First(B => B.Valid);
            string Path = TempFile.Write(AgControlConstants.FILE_EXTENSION, string.Empty);
            try
            {
                AgControlExporter.Export(Path, Project);

                using (SqliteConnection Connection = OpenReadOnly(Path))
                using (SqliteCommand Command = Connection.CreateCommand())
                {
                    Command.CommandText =
                        "SELECT InitialHeightM, CurrentHeightM, TargetHeightM, HaulPath FROM FieldState WHERE X = $X AND Y = $Y;";
                    Command.Parameters.AddWithValue("$X", SeedBin.X);
                    Command.Parameters.AddWithValue("$Y", SeedBin.Y);
                    using (SqliteDataReader Reader = Command.ExecuteReader())
                    {
                        Assert.True(Reader.Read());
                        Assert.Equal(SeedBin.ExistingM, Reader.GetDouble(0), precision: 6);
                        Assert.Equal(SeedBin.ExistingM, Reader.GetDouble(1), precision: 6);
                        Assert.Equal(SeedBin.ProposedM, Reader.GetDouble(2), precision: 6);
                        Assert.Equal(AgControlConstants.FIELD_STATE_HAUL_PATH_UNUSED, Reader.GetInt32(3));
                    }
                }
            }
            finally
            {
                TempFile.DeleteIfExists(Path);
            }
        }

        /// <summary>
        /// Verifies survey benchmarks are written to the Benchmarks table.
        /// </summary>
        [Fact]
        public void Export_WritesBenchmarks
            (
            )
        {
            ProjectDocument Project = BuildProjectWithDesign();
            Assert.True(Project.Survey!.Benchmarks.Count > 0);
            Benchmark Expected = Project.Survey.Benchmarks[0];
            string Path = TempFile.Write(AgControlConstants.FILE_EXTENSION, string.Empty);
            try
            {
                AgControlExporter.Export(Path, Project);

                using (SqliteConnection Connection = OpenReadOnly(Path))
                {
                    Assert.Equal(Project.Survey.Benchmarks.Count, CountRows(Connection, "Benchmarks"));
                    using (SqliteCommand Command = Connection.CreateCommand())
                    {
                        Command.CommandText =
                            "SELECT Latitude, Longitude, Name, ElevationM FROM Benchmarks WHERE Name = $Name LIMIT 1;";
                        Command.Parameters.AddWithValue("$Name", Expected.Name);
                        using (SqliteDataReader Reader = Command.ExecuteReader())
                        {
                            Assert.True(Reader.Read());
                            Assert.Equal(Expected.Location.Latitude, Reader.GetDouble(0), precision: 6);
                            Assert.Equal(Expected.Location.Longitude, Reader.GetDouble(1), precision: 6);
                            Assert.Equal(Expected.Name, Reader.GetString(2));
                            Assert.Equal(Expected.Elevation, Reader.GetDouble(3), precision: 6);
                        }
                    }
                }
            }
            finally
            {
                TempFile.DeleteIfExists(Path);
            }
        }

        /// <summary>
        /// Verifies pivot overlay data is written to PivotConfig, PivotMoves, and PivotPassBearings.
        /// </summary>
        [Fact]
        public void Export_WithPivot_WritesPivotTables
            (
            )
        {
            ProjectDocument Project = BuildProjectWithDesign();
            Project.HaulSettings.PivotLatitudeDeg = PIVOT_LAT_DEG;
            Project.HaulSettings.PivotLongitudeDeg = PIVOT_LON_DEG;
            HaulData Haul = BuildSyntheticHaulData(Project.DesignGrid!);
            Haul.Payload.PivotOverlay = new HaulPivotOverlayDto
            {
                RadiusM = PIVOT_RADIUS_M,
                BearingByPassDeg = new[] { PIVOT_BEARING_DEG },
                Moves =
                {
                    new HaulPivotMoveDto
                    {
                        Index = 1,
                        FirstRoute = 1,
                        LastRoute = 1,
                        BearingDeg = PIVOT_BEARING_DEG,
                        MoveDeg = 0.0
                    }
                }
            };
            Project.HaulData = Haul;

            string Path = TempFile.Write(AgControlConstants.FILE_EXTENSION, string.Empty);
            try
            {
                AgControlExporter.Export(Path, Project);

                using (SqliteConnection Connection = OpenReadOnly(Path))
                {
                    Assert.Equal(1, CountRows(Connection, "PivotConfig"));
                    Assert.Equal(1, CountRows(Connection, "PivotMoves"));
                    Assert.Equal(1, CountRows(Connection, "PivotPassBearings"));

                    using (SqliteCommand Command = Connection.CreateCommand())
                    {
                        Command.CommandText =
                            "SELECT CenterLatitude, CenterLongitude, RadiusM FROM PivotConfig WHERE PivotConfigID = 1;";
                        using (SqliteDataReader Reader = Command.ExecuteReader())
                        {
                            Assert.True(Reader.Read());
                            Assert.Equal(PIVOT_LAT_DEG, Reader.GetDouble(0));
                            Assert.Equal(PIVOT_LON_DEG, Reader.GetDouble(1));
                            Assert.Equal(PIVOT_RADIUS_M, Reader.GetDouble(2));
                        }
                    }
                }
            }
            finally
            {
                TempFile.DeleteIfExists(Path);
            }
        }

        /// <summary>
        /// Verifies export progress reports reach completion.
        /// </summary>
        [Fact]
        public void Export_ReportsProgressToCompletion
            (
            )
        {
            ProjectDocument Project = BuildProjectWithDesign();
            string Path = TempFile.Write(AgControlConstants.FILE_EXTENSION, string.Empty);
            List<int> Reports = new List<int>();
            try
            {
                AgControlExporter.Export(Path, Project, new Progress<int>(Reports.Add));

                Assert.Contains(AgControlConstants.PROGRESS_VALIDATED_PERCENT, Reports);
                Assert.Contains(AgControlConstants.PROGRESS_SCHEMA_PERCENT, Reports);
                Assert.Contains(AgControlConstants.PROGRESS_COMPLETE_PERCENT, Reports);
                Assert.Equal(AgControlConstants.PROGRESS_COMPLETE_PERCENT, Reports[^1]);
            }
            finally
            {
                TempFile.DeleteIfExists(Path);
            }
        }

        /// <summary>
        /// Verifies suggested file names use the project display name when available.
        /// </summary>
        [Fact]
        public void SuggestFileName_UsesProjectDisplayName
            (
            )
        {
            ProjectDocument Untitled = new ProjectDocument();
            Assert.Equal(AgControlConstants.DEFAULT_FILE_NAME, AgControlExporter.SuggestFileName(Untitled));

            ProjectDocument Named = new ProjectDocument { FilePath = @"C:\fields\NorthEdge.agproject" };
            Assert.Equal("NorthEdge" + AgControlConstants.FILE_EXTENSION, AgControlExporter.SuggestFileName(Named));
        }

        /// <summary>
        /// Verifies missing extensions are appended and existing ones are preserved.
        /// </summary>
        [Fact]
        public void EnsureExtension_AppendsWhenMissing
            (
            )
        {
            Assert.Equal(
                "field" + AgControlConstants.FILE_EXTENSION,
                AgControlExporter.EnsureExtension("field"));
            Assert.Equal(
                "field" + AgControlConstants.FILE_EXTENSION,
                AgControlExporter.EnsureExtension("field" + AgControlConstants.FILE_EXTENSION));
        }

        /// <summary>
        /// Builds a project with survey and design grid from the minimal AGS fixture.
        /// </summary>
        /// <returns>Project with a proposed surface.</returns>
        static private ProjectDocument BuildProjectWithDesign
            (
            )
        {
            string AgsPath = TempFile.Write(".ags", SurveySamples.MINIMAL_AGS);
            try
            {
                SurveyModel Survey = new SurveyModel();
                Survey.LoadFromAgs(AgsPath);
                DesignParameters Parameters = new DesignParameters
                {
                    MainSlopeDirectionDeg = MAIN_SLOPE_DIRECTION_DEG,
                    MainSlopePercent = ZERO_SLOPE_PERCENT,
                    CrossSlopePercent = ZERO_SLOPE_PERCENT,
                    CutFillRatio = FieldConstants.CUT_FILL_RATIO
                };

                FieldCreator Creator = new FieldCreator();
                (DesignGrid Grid, _) = Creator.CreateDesign(Survey, Parameters);

                return new ProjectDocument
                {
                    Survey = Survey,
                    DesignParameters = Parameters,
                    DesignGrid = Grid
                };
            }
            finally
            {
                TempFile.DeleteIfExists(AgsPath);
            }
        }

        /// <summary>
        /// Builds a minimal one-route haul payload for export mapping tests.
        /// </summary>
        /// <param name="Grid">Design grid supplying cut-start UTM near a valid bin.</param>
        /// <returns>Haul data with one pass and two guidance points.</returns>
        static private HaulData BuildSyntheticHaulData
            (
            DesignGrid Grid
            )
        {
            GridBinData? SeedBin = Grid.Bins.Find(B => B.Valid);
            Assert.NotNull(SeedBin);

            double EastingM = Grid.OriginEastingM + (SeedBin!.X + FieldConstants.BIN_CENTER_OFFSET) * Grid.BinSizeM;
            double NorthingM = Grid.OriginNorthingM + (SeedBin.Y + FieldConstants.BIN_CENTER_OFFSET) * Grid.BinSizeM;

            HaulPassDto Pass = new HaulPassDto
            {
                LoadBcy = LOAD_BCY,
                HaulDistanceM = HAUL_DISTANCE_M,
                CutStartX = EastingM,
                CutStartY = NorthingM,
                CutEndX = EastingM + Grid.BinSizeM,
                CutEndY = NorthingM,
                FillStartX = EastingM + (2 * Grid.BinSizeM),
                FillStartY = NorthingM,
                FillEndX = EastingM + (3 * Grid.BinSizeM),
                FillEndY = NorthingM
            };
            Pass.CutCells.Add(new HaulPassCellDto
            {
                X = SeedBin.X,
                Y = SeedBin.Y,
                Depth = CUT_CELL_DEPTH_M
            });

            HaulData Data = new HaulData();
            Data.Payload.OrderedPasses.Add(Pass);
            Data.Payload.CutFillPairs.Add(new HaulCutFillPairDto
            {
                Order = 1,
                LoadBcy = LOAD_BCY,
                HaulDistanceM = HAUL_DISTANCE_M,
                RouteTimeSeconds = 0.0,
                TotalCutBcyBefore = 0.0,
                TotalFillBcyBefore = 0.0
            });
            Data.Payload.GuidancePoints.Add(new HaulGuidancePointDto
            {
                Seq = 1,
                PassNo = 1,
                Action = AgControlConstants.GUIDANCE_ACTION_CUT,
                Blade = "DOWN",
                X = EastingM,
                Y = NorthingM,
                Lat = SeedBin.CentroidLat,
                Lon = SeedBin.CentroidLon,
                DepthMm = GUIDANCE_DEPTH_MM,
                HeadingDeg = 90.0
            });
            Data.Payload.GuidancePoints.Add(new HaulGuidancePointDto
            {
                Seq = 2,
                PassNo = 1,
                Action = "FILL",
                Blade = "UP",
                X = EastingM + (2 * Grid.BinSizeM),
                Y = NorthingM,
                Lat = SeedBin.CentroidLat,
                Lon = SeedBin.CentroidLon,
                DepthMm = 0.0,
                HeadingDeg = 90.0
            });

            return Data;
        }

        /// <summary>
        /// Opens a read-only connection to an exported <c>.agcontrol</c> file.
        /// </summary>
        /// <param name="Path">Database path.</param>
        /// <returns>Open connection.</returns>
        static private SqliteConnection OpenReadOnly
            (
            string Path
            )
        {
            SqliteConnectionStringBuilder Builder = new SqliteConnectionStringBuilder
            {
                DataSource = Path,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            };
            SqliteConnection Connection = new SqliteConnection(Builder.ConnectionString);
            Connection.Open();
            return Connection;
        }

        /// <summary>
        /// Reads table names from <c>sqlite_master</c>.
        /// </summary>
        /// <param name="Connection">Open connection.</param>
        /// <returns>Set of table names.</returns>
        static private HashSet<string> ReadTableNames
            (
            SqliteConnection Connection
            )
        {
            HashSet<string> Tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (SqliteCommand Command = Connection.CreateCommand())
            {
                Command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table';";
                using (SqliteDataReader Reader = Command.ExecuteReader())
                {
                    while (Reader.Read())
                        Tables.Add(Reader.GetString(0));
                }
            }

            return Tables;
        }

        /// <summary>
        /// Reads <c>PRAGMA user_version</c> from the database.
        /// </summary>
        /// <param name="Connection">Open connection.</param>
        /// <returns>User version integer.</returns>
        static private int ReadUserVersion
            (
            SqliteConnection Connection
            )
        {
            using (SqliteCommand Command = Connection.CreateCommand())
            {
                Command.CommandText = "PRAGMA user_version;";
                return Convert.ToInt32(Command.ExecuteScalar());
            }
        }

        /// <summary>
        /// Counts rows in a named table.
        /// </summary>
        /// <param name="Connection">Open connection.</param>
        /// <param name="TableName">Table name.</param>
        /// <returns>Row count.</returns>
        static private int CountRows
            (
            SqliteConnection Connection,
            string TableName
            )
        {
            using (SqliteCommand Command = Connection.CreateCommand())
            {
                Command.CommandText = "SELECT COUNT(*) FROM " + TableName + ";";
                return Convert.ToInt32(Command.ExecuteScalar());
            }
        }

        /// <summary>
        /// Returns the sum of all <c>FieldState.HaulPath</c> values (must be zero for Designer exports).
        /// </summary>
        /// <param name="Connection">Open connection.</param>
        /// <returns>Sum of haul-path stamps.</returns>
        static private long ReadAllFieldStateHaulPaths
            (
            SqliteConnection Connection
            )
        {
            using (SqliteCommand Command = Connection.CreateCommand())
            {
                Command.CommandText = "SELECT COALESCE(SUM(HaulPath), 0) FROM FieldState;";
                return Convert.ToInt64(Command.ExecuteScalar());
            }
        }

        /// <summary>
        /// Reads one <c>Data</c> value by name.
        /// </summary>
        /// <param name="Connection">Open connection.</param>
        /// <param name="Name">Data key.</param>
        /// <returns>Stored value.</returns>
        static private double ReadDataValue
            (
            SqliteConnection Connection,
            string Name
            )
        {
            using (SqliteCommand Command = Connection.CreateCommand())
            {
                Command.CommandText = "SELECT Value FROM Data WHERE Name = $Name;";
                Command.Parameters.AddWithValue("$Name", Name);
                object? Value = Command.ExecuteScalar();
                Assert.NotNull(Value);
                return Convert.ToDouble(Value);
            }
        }
    }
}
