namespace AgGradeDesigner.Core.Water
{
    /// <summary>
    /// Canonical defaults and limits for the Water feature (WATER-SPEC §4.3, §7.1.8–§7.1.9).
    /// </summary>
    public static class WaterConstants
    {
        /// <summary>Default design-storm ponding rainfall stored in millimetres.</summary>
        public const uint DEFAULT_PONDING_RAINFALL_MM = 50;

        /// <summary>Default SCS curve number for ponding analysis.</summary>
        public const uint DEFAULT_PONDING_CURVE_NUMBER = 85;

        /// <summary>Minimum allowed ponding rainfall in millimetres.</summary>
        public const uint MIN_PONDING_RAINFALL_MM = 0;

        /// <summary>Minimum allowed ponding curve number.</summary>
        public const uint MIN_PONDING_CURVE_NUMBER = 0;

        /// <summary>Maximum allowed SCS curve number for runoff computation.</summary>
        public const double MAX_SCS_CURVE_NUMBER = 100.0;

        /// <summary>Minimum allowed SCS curve number for runoff computation.</summary>
        public const double MIN_SCS_CURVE_NUMBER = 1.0;

        /// <summary>SCS initial abstraction factor applied to potential maximum retention.</summary>
        public const double SCS_INITIAL_ABSTRACTION_FACTOR = 0.2;

        /// <summary>SCS potential maximum retention numerator constant in millimetres.</summary>
        public const double SCS_RETENTION_NUMERATOR_MM = 25400.0;

        /// <summary>SCS potential maximum retention denominator offset in millimetres.</summary>
        public const double SCS_RETENTION_DENOMINATOR_OFFSET_MM = 254.0;

        /// <summary>Millimetres per inch for display-unit conversion.</summary>
        public const double MM_PER_INCH = 25.4;

        /// <summary>Default imperial display value for <see cref="DEFAULT_PONDING_RAINFALL_MM"/> (nearest whole inch).</summary>
        public const uint DEFAULT_PONDING_RAINFALL_IMPERIAL_IN = 2;

        /// <summary>Grayscale exponent applied when mapping pond depth to overlay intensity.</summary>
        public const double POND_DEPTH_GRAYSCALE_EXPONENT = 0.25;

        /// <summary>Water overlay blue channel component (0–255).</summary>
        public const byte WATER_OVERLAY_BLUE = 180;

        /// <summary>Water overlay green channel component (0–255).</summary>
        public const byte WATER_OVERLAY_GREEN = 120;

        /// <summary>Water overlay red channel component (0–255).</summary>
        public const byte WATER_OVERLAY_RED = 40;

        /// <summary>Maximum overlay alpha for deepest ponding cells.</summary>
        public const byte WATER_OVERLAY_MAX_ALPHA = 200;

        /// <summary>Binary blob format magic bytes identifying a water layer payload.</summary>
        public const uint BLOB_MAGIC = 0x4C574741;

        /// <summary>Binary blob format version written after the magic header.</summary>
        public const uint BLOB_VERSION = 2;

        /// <summary>Legacy binary blob format version without geographic bounds in the header.</summary>
        public const uint BLOB_VERSION_LEGACY = 1;

        /// <summary>Binary header size in bytes for <see cref="BLOB_VERSION"/> (magic, version, width, height, cell size, bounds).</summary>
        public const int BLOB_HEADER_SIZE_BYTES = 56;

        /// <summary>Legacy binary header size in bytes for <see cref="BLOB_VERSION_LEGACY"/>.</summary>
        public const int BLOB_LEGACY_HEADER_SIZE_BYTES = 24;

        /// <summary>Size of one float depth sample in the persisted blob body.</summary>
        public const int BLOB_DEPTH_BYTES_PER_CELL = 4;

        /// <summary>Status message shown while surface water layer calculation runs.</summary>
        public const string CALCULATING_WATER_LAYER_MESSAGE = "Calculating water layer";

        /// <summary>Maximum iterations for volume-to-depth binary search in ponding fill.</summary>
        public const int VOLUME_TO_DEPTH_MAX_ITERATIONS = 60;

        /// <summary>Progress percent reported when ponding analysis completes.</summary>
        public const int PROGRESS_COMPLETE_PERCENT = 100;

        /// <summary>Progress percent reported when ponding analysis starts.</summary>
        public const int PROGRESS_START_PERCENT = 0;

        /// <summary>Progress percent reported after DEM preparation.</summary>
        public const int PROGRESS_DEM_READY_PERCENT = 20;

        /// <summary>Progress percent reported after flow routing completes.</summary>
        public const int PROGRESS_ROUTING_COMPLETE_PERCENT = 70;

        /// <summary>Progress percent reported before overlay rasterization.</summary>
        public const int PROGRESS_OVERLAY_START_PERCENT = 85;
    }
}
