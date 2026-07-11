namespace AgGradeDesigner.Core.Export
{
    /// <summary>
    /// SQLite DDL for the <c>.agcontrol</c> export schema (EXPORT-SPEC Appendix A).
    /// </summary>
    static public class AgControlSchema
    {
        /// <summary>
        /// Ordered CREATE TABLE / INDEX statements applied to a new export database.
        /// </summary>
        static public readonly string[] CreateStatements =
        {
            """
            CREATE TABLE FieldState (
              BinID INTEGER PRIMARY KEY AUTOINCREMENT,
              X INTEGER NOT NULL,
              Y INTEGER NOT NULL,
              InitialHeightM REAL NOT NULL,
              CurrentHeightM REAL NOT NULL,
              TargetHeightM REAL NOT NULL,
              CentroidLat REAL NOT NULL,
              CentroidLon REAL NOT NULL,
              HaulPath INTEGER NOT NULL DEFAULT 0
            );
            """,
            "CREATE UNIQUE INDEX idx_fieldstate_xy ON FieldState (X, Y);",
            """
            CREATE TABLE Data (
              DataID INTEGER PRIMARY KEY AUTOINCREMENT,
              Name TEXT NOT NULL,
              Value REAL NOT NULL
            );
            """,
            "CREATE UNIQUE INDEX idx_data_name ON Data (Name);",
            """
            CREATE TABLE HaulArrows (
              ArrowID INTEGER PRIMARY KEY AUTOINCREMENT,
              Latitude REAL NOT NULL,
              Longitude REAL NOT NULL,
              Heading REAL NOT NULL
            );
            """,
            """
            CREATE TABLE Benchmarks (
              BenchMarkID INTEGER PRIMARY KEY AUTOINCREMENT,
              Latitude REAL NOT NULL,
              Longitude REAL NOT NULL,
              Name TEXT NOT NULL,
              ElevationM REAL NOT NULL
            );
            """,
            """
            CREATE TABLE Events (
              EventID INTEGER PRIMARY KEY AUTOINCREMENT,
              Type TEXT,
              Value REAL,
              Timestamp INTEGER
            );
            """,
            """
            CREATE TABLE LevelingOperation (
              OperationId INTEGER PRIMARY KEY AUTOINCREMENT,
              OperationType TEXT NOT NULL CHECK (OperationType IN ('CUT', 'FILL')),
              StartedAtMs INTEGER NOT NULL,
              CompletedAtMs INTEGER NOT NULL
            );
            """,
            """
            CREATE TABLE LevelingOperationBin (
              OperationBinId INTEGER PRIMARY KEY AUTOINCREMENT,
              OperationId INTEGER NOT NULL,
              X INTEGER NOT NULL,
              Y INTEGER NOT NULL,
              DeltaHeightM REAL NOT NULL,
              FOREIGN KEY (OperationId) REFERENCES LevelingOperation(OperationId) ON DELETE CASCADE
            );
            """,
            "CREATE INDEX idx_opbin_operation ON LevelingOperationBin (OperationId);",
            "CREATE INDEX idx_opbin_xy ON LevelingOperationBin (X, Y);",
            """
            CREATE TABLE HaulPaths (
              HaulPathID INTEGER PRIMARY KEY AUTOINCREMENT,
              HaulPath INTEGER NOT NULL,
              PointNumber INTEGER NOT NULL,
              Latitude REAL NOT NULL,
              Longitude REAL NOT NULL,
              Seq INTEGER NOT NULL,
              Action TEXT NOT NULL,
              Blade TEXT NOT NULL,
              EastingM REAL NOT NULL,
              NorthingM REAL NOT NULL,
              DepthMm REAL NOT NULL,
              HeadingDeg REAL NOT NULL
            );
            """,
            "CREATE UNIQUE INDEX idx_haulpaths_haul_point ON HaulPaths (HaulPath, PointNumber);",
            "CREATE INDEX idx_haulpaths_seq ON HaulPaths (Seq);",
            """
            CREATE TABLE HaulRoutes (
              HaulRouteID INTEGER PRIMARY KEY AUTOINCREMENT,
              HaulPath INTEGER NOT NULL UNIQUE,
              BladeDepthMm REAL NOT NULL,
              CutStartBinX INTEGER NOT NULL,
              CutStartBinY INTEGER NOT NULL,
              CutStartEastingM REAL NOT NULL,
              CutStartNorthingM REAL NOT NULL,
              CutEndEastingM REAL NOT NULL,
              CutEndNorthingM REAL NOT NULL,
              FillStartEastingM REAL NOT NULL,
              FillStartNorthingM REAL NOT NULL,
              FillEndEastingM REAL NOT NULL,
              FillEndNorthingM REAL NOT NULL,
              LoadBcy REAL NOT NULL,
              HaulDistanceM REAL NOT NULL,
              RouteTimeSeconds REAL NOT NULL,
              TotalCutBcyBefore REAL NOT NULL,
              TotalFillBcyBefore REAL NOT NULL
            );
            """,
            "CREATE UNIQUE INDEX idx_haulroutes_path ON HaulRoutes (HaulPath);",
            """
            CREATE TABLE PivotConfig (
              PivotConfigID INTEGER PRIMARY KEY CHECK (PivotConfigID = 1),
              CenterLatitude REAL NOT NULL,
              CenterLongitude REAL NOT NULL,
              RadiusM REAL NOT NULL
            );
            """,
            """
            CREATE TABLE PivotMoves (
              PivotMoveID INTEGER PRIMARY KEY AUTOINCREMENT,
              MoveIndex INTEGER NOT NULL UNIQUE,
              FirstRoute INTEGER NOT NULL,
              LastRoute INTEGER NOT NULL,
              BearingDeg REAL NOT NULL,
              MoveDeg REAL NOT NULL
            );
            """,
            "CREATE UNIQUE INDEX idx_pivotmoves_index ON PivotMoves (MoveIndex);",
            """
            CREATE TABLE PivotPassBearings (
              HaulPath INTEGER NOT NULL PRIMARY KEY,
              BearingDeg REAL NOT NULL
            );
            """
        };
    }
}
