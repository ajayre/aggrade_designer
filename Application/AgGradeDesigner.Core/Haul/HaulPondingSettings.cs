namespace AgGradeDesigner.Core.Haul
{
    /// <summary>
    /// Ponding audit and reorder settings stored with a project.
    /// Values match <see cref="HaulBuilder.PondingOptions"/>. Audit and optimize stay off
    /// until the user turns them on.
    /// </summary>
    public sealed class HaulPondingSettings
    {
        /// <summary>Specification defaults from a new <see cref="HaulBuilder.PondingOptions"/>.</summary>
        private static readonly HaulBuilder.PondingOptions EngineDefaults = new HaulBuilder.PondingOptions();

        /// <summary>Whether drainage-aware pass reordering runs.</summary>
        public bool Optimize { get; set; } = EngineDefaults.Optimize;

        /// <summary>Ponding penalty the reorder search minimizes.</summary>
        public HaulBuilder.PondingObjectiveMode ObjectiveMode { get; set; } = EngineDefaults.ObjectiveMode;

        /// <summary>Whether ponding metrics are collected without reordering.</summary>
        public bool Audit { get; set; } = EngineDefaults.Audit;

        /// <summary>Whether optimisation skips the implicit pre-audit pass.</summary>
        public bool NoImplicitAudit { get; set; } = EngineDefaults.NoImplicitAudit;

        /// <summary>Whether target-spill cells are marked as outlets.</summary>
        public bool TargetSpillOutlets { get; set; } = EngineDefaults.TargetSpillOutlets;

        /// <summary>Target-spill outlet tolerance in feet.</summary>
        public double SpillToleranceFt { get; set; } = EngineDefaults.SpillToleranceFt;

        /// <summary>Seconds applied per unit of the selected ponding penalty.</summary>
        public double Weight { get; set; } = EngineDefaults.Weight;

        /// <summary>Maximum pass gap between optimisation checkpoints. Null lets the engine choose.</summary>
        public int? CheckpointInterval { get; set; } = EngineDefaults.CheckpointInterval;

        /// <summary>WhiteBoxTools executable or install directory. Empty uses the in-process analyser.</summary>
        public string WhiteBoxToolsPath { get; set; } = EngineDefaults.WhiteBoxToolsPath ?? string.Empty;

        /// <summary>Scratch directory for external ponding rasters.</summary>
        public string WhiteBoxToolsWorkDirectory { get; set; } = EngineDefaults.WhiteBoxToolsWorkDirectory ?? string.Empty;

        /// <summary>Warning threshold for maximum pond depth in feet.</summary>
        public double WarnDepthFt { get; set; } = EngineDefaults.WarnDepthFt;

        /// <summary>Warning threshold for trapped volume in bank cubic yards.</summary>
        public double WarnVolumeBcy { get; set; } = EngineDefaults.WarnVolumeBcy;

        /// <summary>Design rainfall depth in inches used for automatic volume warnings.</summary>
        public double DesignRainfallIn { get; set; } = EngineDefaults.DesignRainfallIn;

        /// <summary>Whether a ponding warning fails the run.</summary>
        public bool FailOnWarn { get; set; } = EngineDefaults.FailOnWarn;

        /// <summary>Whether a worst-checkpoint ponding map is requested from the engine.</summary>
        public bool IncludeMap { get; set; } = EngineDefaults.IncludeMap;

        /// <summary>Raster scale for the ponding map.</summary>
        public int MapPixelsPerCell { get; set; } = EngineDefaults.MapPixelsPerCell;

        /// <summary>Manual culvert outlet points, each <c>lat,lon</c>.</summary>
        public List<string> OutletSpecs { get; set; } = new List<string>();

        /// <summary>Manual culvert outlet polygons, each <c>lat,lon;lat,lon;...</c>.</summary>
        public List<string> OutletPolygonSpecs { get; set; } = new List<string>();

        /// <summary>
        /// Creates an independent copy, including outlet lists.
        /// </summary>
        /// <returns>Settings with the same values and no shared lists.</returns>
        public HaulPondingSettings Clone
            (
            )
        {
            return new HaulPondingSettings
            {
                Optimize = Optimize,
                ObjectiveMode = ObjectiveMode,
                Audit = Audit,
                NoImplicitAudit = NoImplicitAudit,
                TargetSpillOutlets = TargetSpillOutlets,
                SpillToleranceFt = SpillToleranceFt,
                Weight = Weight,
                CheckpointInterval = CheckpointInterval,
                WhiteBoxToolsPath = WhiteBoxToolsPath,
                WhiteBoxToolsWorkDirectory = WhiteBoxToolsWorkDirectory,
                WarnDepthFt = WarnDepthFt,
                WarnVolumeBcy = WarnVolumeBcy,
                DesignRainfallIn = DesignRainfallIn,
                FailOnWarn = FailOnWarn,
                IncludeMap = IncludeMap,
                MapPixelsPerCell = MapPixelsPerCell,
                OutletSpecs = new List<string>(OutletSpecs),
                OutletPolygonSpecs = new List<string>(OutletPolygonSpecs)
            };
        }

        /// <summary>
        /// Copies values from another ponding settings instance.
        /// </summary>
        /// <param name="Source">Settings to copy.</param>
        public void CopyFrom
            (
            HaulPondingSettings Source
            )
        {
            Optimize = Source.Optimize;
            ObjectiveMode = Source.ObjectiveMode;
            Audit = Source.Audit;
            NoImplicitAudit = Source.NoImplicitAudit;
            TargetSpillOutlets = Source.TargetSpillOutlets;
            SpillToleranceFt = Source.SpillToleranceFt;
            Weight = Source.Weight;
            CheckpointInterval = Source.CheckpointInterval;
            WhiteBoxToolsPath = Source.WhiteBoxToolsPath;
            WhiteBoxToolsWorkDirectory = Source.WhiteBoxToolsWorkDirectory;
            WarnDepthFt = Source.WarnDepthFt;
            WarnVolumeBcy = Source.WarnVolumeBcy;
            DesignRainfallIn = Source.DesignRainfallIn;
            FailOnWarn = Source.FailOnWarn;
            IncludeMap = Source.IncludeMap;
            MapPixelsPerCell = Source.MapPixelsPerCell;
            OutletSpecs = new List<string>(Source.OutletSpecs);
            OutletPolygonSpecs = new List<string>(Source.OutletPolygonSpecs);
        }
    }
}
