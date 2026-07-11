namespace AgGradeDesigner.Core.Export
{
    /// <summary>
    /// Named constants for the <c>.agcontrol</c> SQLite export format (EXPORT-SPEC §5, Appendix A).
    /// </summary>
    static public class AgControlConstants
    {
        /// <summary>File extension including the leading dot.</summary>
        public const string FILE_EXTENSION = ".agcontrol";

        /// <summary>Default suggested file name for an unsaved export.</summary>
        public const string DEFAULT_FILE_NAME = "field.agcontrol";

        /// <summary>Progress percent reported when export validation completes.</summary>
        public const int PROGRESS_VALIDATED_PERCENT = 5;

        /// <summary>Progress percent reported when the SQLite schema is created.</summary>
        public const int PROGRESS_SCHEMA_PERCENT = 15;

        /// <summary>Progress percent reported when FieldState rows are written.</summary>
        public const int PROGRESS_FIELD_STATE_PERCENT = 45;

        /// <summary>Progress percent reported when Data and Benchmarks are written.</summary>
        public const int PROGRESS_DATA_PERCENT = 60;

        /// <summary>Progress percent reported when haul routes and paths are written.</summary>
        public const int PROGRESS_HAUL_PERCENT = 80;

        /// <summary>Progress percent reported when flow arrows and pivot tables are written.</summary>
        public const int PROGRESS_OVERLAYS_PERCENT = 92;

        /// <summary>Progress percent reported when the export file is fully written.</summary>
        public const int PROGRESS_COMPLETE_PERCENT = 100;

        /// <summary>
        /// SQLite <c>PRAGMA user_version</c> for Designer→Control export databases
        /// (Control legacy field DBs use 1).
        /// </summary>
        public const int USER_VERSION = 2;

        /// <summary>Elevation sentinel for bins without survey/design data (metres).</summary>
        public const double NODATA_HEIGHT_M = FieldConstants.BIN_NO_DATA_SENTINEL;

        /// <summary>Designer guidance exports leave every <c>FieldState.HaulPath</c> at zero.</summary>
        public const int FIELD_STATE_HAUL_PATH_UNUSED = 0;

        /// <summary>Millimetres per metre for blade-depth conversion from cut-cell metres.</summary>
        public const double MM_PER_METRE = 1000.0;

        /// <summary>False / zero flag written to <c>Data.Calibrated</c> at export.</summary>
        public const double DATA_CALIBRATED_FALSE = 0.0;

        /// <summary>Zero completed cut volume (CY) at export time.</summary>
        public const double DATA_COMPLETED_CUT_CY = 0.0;

        /// <summary>Zero completed fill volume (CY) at export time.</summary>
        public const double DATA_COMPLETED_FILL_CY = 0.0;

        /// <summary>Zero height calibration offset (metres) at export.</summary>
        public const double DATA_HEIGHT_OFFSET_M = 0.0;

        /// <summary>Zero easting calibration offset (metres) at export.</summary>
        public const double DATA_EASTING_OFFSET_M = 0.0;

        /// <summary>Zero northing calibration offset (metres) at export.</summary>
        public const double DATA_NORTHING_OFFSET_M = 0.0;

        /// <summary>Provenance marker value identifying AgGrade Designer as the producer.</summary>
        public const double DATA_PRODUCER_DESIGNER = 1.0;

        /// <summary>Single-row primary key enforced on <c>PivotConfig</c>.</summary>
        public const int PIVOT_CONFIG_SINGLE_ROW_ID = 1;

        /// <summary>Guidance action token used when resolving pair blade depth from waypoints.</summary>
        public const string GUIDANCE_ACTION_CUT = "CUT";

        /// <summary><c>Data.Name</c> key for design-grid column count.</summary>
        public const string DATA_NAME_GRID_WIDTH = "GridWidth";

        /// <summary><c>Data.Name</c> key for design-grid row count.</summary>
        public const string DATA_NAME_GRID_HEIGHT = "GridHeight";

        /// <summary><c>Data.Name</c> key for mean latitude of valid bins.</summary>
        public const string DATA_NAME_MEAN_LAT = "MeanLat";

        /// <summary><c>Data.Name</c> key for mean longitude of valid bins.</summary>
        public const string DATA_NAME_MEAN_LON = "MeanLon";

        /// <summary><c>Data.Name</c> key for minimum UTM easting (metres).</summary>
        public const string DATA_NAME_MIN_X = "MinX";

        /// <summary><c>Data.Name</c> key for minimum UTM northing (metres).</summary>
        public const string DATA_NAME_MIN_Y = "MinY";

        /// <summary><c>Data.Name</c> key for maximum UTM easting (metres).</summary>
        public const string DATA_NAME_MAX_X = "MaxX";

        /// <summary><c>Data.Name</c> key for maximum UTM northing (metres).</summary>
        public const string DATA_NAME_MAX_Y = "MaxY";

        /// <summary><c>Data.Name</c> key for minimum latitude of valid bins.</summary>
        public const string DATA_NAME_MIN_LAT = "MinLat";

        /// <summary><c>Data.Name</c> key for minimum longitude of valid bins.</summary>
        public const string DATA_NAME_MIN_LON = "MinLon";

        /// <summary><c>Data.Name</c> key for maximum latitude of valid bins.</summary>
        public const string DATA_NAME_MAX_LAT = "MaxLat";

        /// <summary><c>Data.Name</c> key for maximum longitude of valid bins.</summary>
        public const string DATA_NAME_MAX_LON = "MaxLon";

        /// <summary><c>Data.Name</c> key for design total cut volume (cubic yards).</summary>
        public const string DATA_NAME_TOTAL_CUT_CY = "TotalCutCY";

        /// <summary><c>Data.Name</c> key for design total fill volume (cubic yards).</summary>
        public const string DATA_NAME_TOTAL_FILL_CY = "TotalFillCY";

        /// <summary><c>Data.Name</c> key for completed cut volume (cubic yards).</summary>
        public const string DATA_NAME_COMPLETED_CUT_CY = "CompletedCutCY";

        /// <summary><c>Data.Name</c> key for completed fill volume (cubic yards).</summary>
        public const string DATA_NAME_COMPLETED_FILL_CY = "CompletedFillCY";

        /// <summary><c>Data.Name</c> key for calibration flag.</summary>
        public const string DATA_NAME_CALIBRATED = "Calibrated";

        /// <summary><c>Data.Name</c> key for height calibration offset (metres).</summary>
        public const string DATA_NAME_HEIGHT_OFFSET_M = "HeightOffsetM";

        /// <summary><c>Data.Name</c> key for easting calibration offset (metres).</summary>
        public const string DATA_NAME_EASTING_OFFSET_M = "EastingOffsetM";

        /// <summary><c>Data.Name</c> key for northing calibration offset (metres).</summary>
        public const string DATA_NAME_NORTHING_OFFSET_M = "NorthingOffsetM";

        /// <summary><c>Data.Name</c> key marking Designer as the file producer.</summary>
        public const string DATA_NAME_PRODUCER_DESIGNER = "ProducerDesigner";
    }
}
