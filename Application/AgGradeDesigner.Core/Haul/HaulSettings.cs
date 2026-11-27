namespace AgGradeDesigner.Core.Haul
{
    /// <summary>
    /// Haul-planning parameters stored per project and seeded from user defaults for new projects.
    /// Values follow the current <see cref="HaulBuilder.HaulBuilderOptions"/> surface.
    /// Retired matcher switches (hop distance, load-fill percent, lane overlap, transport mode,
    /// balance mode, flow-aligned strips, lane-axis heading, and optional grade return) are not stored.
    /// </summary>
    public sealed class HaulSettings
    {
        /// <summary>Default option values from <see cref="HaulBuilder.HaulBuilderOptions"/>.</summary>
        private static readonly HaulBuilder.HaulBuilderOptions EngineDefaults = new HaulBuilder.HaulBuilderOptions();

        /// <summary>Default blade width in feet (independent of haul grid spacing).</summary>
        private const double DEFAULT_BLADE_WIDTH_FT = 8.5;

        /// <summary>Default haul processing grid cell edge length in feet.</summary>
        private const double DEFAULT_GRID_SIZE_FT = 8.5;

        /// <summary>Millimetres per inch for blade-width default conversion.</summary>
        private const double MM_PER_INCH = 25.4;

        /// <summary>Scraper capacity in loose cubic yards (<see cref="HaulBuilder.HaulBuilderOptions.ScraperCapacityLcy"/>).</summary>
        public double ScraperCapacityLcy { get; set; } = EngineDefaults.ScraperCapacityLcy;

        /// <summary>Swell factor for LCY/BCY conversion.</summary>
        public double SwellFactor { get; set; } = EngineDefaults.SwellFactor;

        /// <summary>Blade width in millimetres (default 8.5 ft).</summary>
        public double BladeWidthMm { get; set; } = DEFAULT_BLADE_WIDTH_FT * 12.0 * MM_PER_INCH;

        /// <summary>Haul processing grid cell edge length in feet (default 8.5; separate from blade width).</summary>
        public double GridSizeFt { get; set; } = DEFAULT_GRID_SIZE_FT;

        /// <summary>Maximum cut/fill depth per pass in millimetres.</summary>
        public double MaxDepthMm { get; set; } = EngineDefaults.MaxDepthMm;

        /// <summary>Finishing tolerance in millimetres.</summary>
        public double ToleranceMm { get; set; } = EngineDefaults.ToleranceMm;

        /// <summary>Equipment width in feet for obstacle buffering.</summary>
        public double EquipmentWidthFt { get; set; } = EngineDefaults.EquipmentWidthFt;

        /// <summary>Tractor and scraper overall length in feet for straight lineup before and after cut and fill.</summary>
        public double EquipmentLengthFt { get; set; } = EngineDefaults.EquipmentLengthFt;

        /// <summary>
        /// Minimum blade-down cut or fill run length in feet.
        /// Zero means half of <see cref="EquipmentLengthFt"/>. Ignored when <see cref="EnableMinimumWorkLength"/> is false.
        /// </summary>
        public double MinimumWorkLengthFt { get; set; } = EngineDefaults.MinimumWorkLengthFt;

        /// <summary>Whether a minimum blade-down run length is applied.</summary>
        public bool EnableMinimumWorkLength { get; set; } = EngineDefaults.EnableMinimumWorkLength;

        /// <summary>Field efficiency fraction in [0, 1].</summary>
        public double FieldEfficiency { get; set; } = EngineDefaults.FieldEfficiency;

        /// <summary>Cut zone count for sequencing (<see cref="HaulBuilder.HaulBuilderOptions.ZoneCount"/>).</summary>
        public int Zones { get; set; } = EngineDefaults.ZoneCount;

        /// <summary>Loading/cut speed in miles per hour.</summary>
        public double LoadingSpeedMph { get; set; } = EngineDefaults.CutSpeedMph;

        /// <summary>Loaded travel speed in miles per hour.</summary>
        public double HaulSpeedMph { get; set; } = EngineDefaults.LoadedTravelSpeedMph;

        /// <summary>Fill/dump speed in miles per hour.</summary>
        public double DumpingSpeedMph { get; set; } = EngineDefaults.FillSpeedMph;

        /// <summary>Unloaded return speed in miles per hour.</summary>
        public double ReturnSpeedMph { get; set; } = EngineDefaults.UnloadedTravelSpeedMph;

        /// <summary>Turn speed in miles per hour for route order and the production guidance planner.</summary>
        public double TurnSpeedMph { get; set; } = EngineDefaults.TurnSpeedMph;

        /// <summary>Grade-return speed in miles per hour for the production guidance planner.</summary>
        public double GradeSpeedMph { get; set; } = EngineDefaults.GradeSpeedMph;

        /// <summary>Acceleration phase duration in seconds.</summary>
        public double AccelTimeS { get; set; } = EngineDefaults.AccelerationTimeS;

        /// <summary>Deceleration phase duration in seconds.</summary>
        public double DecelTimeS { get; set; } = EngineDefaults.DecelerationTimeS;

        /// <summary>Minimum tractor turning diameter in feet.</summary>
        public double TurningCircleFt { get; set; } = EngineDefaults.MinimumTurningDiameterFt;

        /// <summary>Heading-change threshold treated as a turn, in degrees.</summary>
        public double TurnThresholdDeg { get; set; } = EngineDefaults.WaypointTurnThresholdDeg;

        /// <summary>Dubins trip-guidance rules.</summary>
        public HaulGuidanceSettings Guidance { get; set; } = new HaulGuidanceSettings();

        /// <summary>Expert optimizer tuning.</summary>
        public HaulOptimizationSettings Optimization { get; set; } = new HaulOptimizationSettings();

        /// <summary>Optional ponding audit and reorder settings.</summary>
        public HaulPondingSettings Ponding { get; set; } = new HaulPondingSettings();

        /// <summary>Whether center-pivot analysis is enabled.</summary>
        public bool Pivot { get; set; } = EngineDefaults.EnablePivot;

        /// <summary>Pivot centre latitude in decimal degrees (WGS-84).</summary>
        public double PivotLatitudeDeg { get; set; }

        /// <summary>Pivot centre longitude in decimal degrees (WGS-84).</summary>
        public double PivotLongitudeDeg { get; set; }

        /// <summary>Pivot arm wheelbase in feet.</summary>
        public double PivotWheelbaseFt { get; set; } = EngineDefaults.PivotWheelbaseFt;

        /// <summary>Extra pivot arm clearance per side in feet.</summary>
        public double PivotClearanceFt { get; set; } = EngineDefaults.PivotClearanceFt;

        /// <summary>Pivot move cost after guidance conflicts have been minimized.</summary>
        public double PivotMoveCost { get; set; } = EngineDefaults.PivotMoveCost;

        /// <summary>
        /// Creates a deep copy suitable for dialog session editing.
        /// </summary>
        /// <returns>Independent settings snapshot.</returns>
        public HaulSettings Clone
            (
            )
        {
            return new HaulSettings
            {
                ScraperCapacityLcy = ScraperCapacityLcy,
                SwellFactor = SwellFactor,
                BladeWidthMm = BladeWidthMm,
                GridSizeFt = GridSizeFt,
                MaxDepthMm = MaxDepthMm,
                ToleranceMm = ToleranceMm,
                EquipmentWidthFt = EquipmentWidthFt,
                EquipmentLengthFt = EquipmentLengthFt,
                MinimumWorkLengthFt = MinimumWorkLengthFt,
                EnableMinimumWorkLength = EnableMinimumWorkLength,
                FieldEfficiency = FieldEfficiency,
                Zones = Zones,
                LoadingSpeedMph = LoadingSpeedMph,
                HaulSpeedMph = HaulSpeedMph,
                DumpingSpeedMph = DumpingSpeedMph,
                ReturnSpeedMph = ReturnSpeedMph,
                TurnSpeedMph = TurnSpeedMph,
                GradeSpeedMph = GradeSpeedMph,
                AccelTimeS = AccelTimeS,
                DecelTimeS = DecelTimeS,
                TurningCircleFt = TurningCircleFt,
                TurnThresholdDeg = TurnThresholdDeg,
                Guidance = Guidance.Clone(),
                Optimization = Optimization.Clone(),
                Ponding = Ponding.Clone(),
                Pivot = Pivot,
                PivotLatitudeDeg = PivotLatitudeDeg,
                PivotLongitudeDeg = PivotLongitudeDeg,
                PivotWheelbaseFt = PivotWheelbaseFt,
                PivotClearanceFt = PivotClearanceFt,
                PivotMoveCost = PivotMoveCost
            };
        }

        /// <summary>
        /// Copies values from another settings instance into this one.
        /// </summary>
        /// <param name="Source">Settings to copy.</param>
        public void CopyFrom
            (
            HaulSettings Source
            )
        {
            ScraperCapacityLcy = Source.ScraperCapacityLcy;
            SwellFactor = Source.SwellFactor;
            BladeWidthMm = Source.BladeWidthMm;
            GridSizeFt = Source.GridSizeFt;
            MaxDepthMm = Source.MaxDepthMm;
            ToleranceMm = Source.ToleranceMm;
            EquipmentWidthFt = Source.EquipmentWidthFt;
            EquipmentLengthFt = Source.EquipmentLengthFt;
            MinimumWorkLengthFt = Source.MinimumWorkLengthFt;
            EnableMinimumWorkLength = Source.EnableMinimumWorkLength;
            FieldEfficiency = Source.FieldEfficiency;
            Zones = Source.Zones;
            LoadingSpeedMph = Source.LoadingSpeedMph;
            HaulSpeedMph = Source.HaulSpeedMph;
            DumpingSpeedMph = Source.DumpingSpeedMph;
            ReturnSpeedMph = Source.ReturnSpeedMph;
            TurnSpeedMph = Source.TurnSpeedMph;
            GradeSpeedMph = Source.GradeSpeedMph;
            AccelTimeS = Source.AccelTimeS;
            DecelTimeS = Source.DecelTimeS;
            TurningCircleFt = Source.TurningCircleFt;
            TurnThresholdDeg = Source.TurnThresholdDeg;
            Guidance.CopyFrom(Source.Guidance);
            Optimization.CopyFrom(Source.Optimization);
            Ponding.CopyFrom(Source.Ponding);
            Pivot = Source.Pivot;
            PivotLatitudeDeg = Source.PivotLatitudeDeg;
            PivotLongitudeDeg = Source.PivotLongitudeDeg;
            PivotWheelbaseFt = Source.PivotWheelbaseFt;
            PivotClearanceFt = Source.PivotClearanceFt;
            PivotMoveCost = Source.PivotMoveCost;
        }

        /// <summary>
        /// Copies the equipment and pass-limit fields from another settings instance.
        /// Travel, guidance, and pivot values are left unchanged.
        /// </summary>
        /// <param name="Source">Settings whose equipment values are copied.</param>
        public void CopyEquipmentFrom
            (
            HaulSettings Source
            )
        {
            ScraperCapacityLcy = Source.ScraperCapacityLcy;
            BladeWidthMm = Source.BladeWidthMm;
            MaxDepthMm = Source.MaxDepthMm;
            ToleranceMm = Source.ToleranceMm;
            EquipmentWidthFt = Source.EquipmentWidthFt;
            EquipmentLengthFt = Source.EquipmentLengthFt;
            MinimumWorkLengthFt = Source.MinimumWorkLengthFt;
            EnableMinimumWorkLength = Source.EnableMinimumWorkLength;
            TurningCircleFt = Source.TurningCircleFt;
        }

        /// <summary>
        /// True when the equipment and pass-limit fields match another settings instance.
        /// </summary>
        /// <param name="Other">Settings to compare.</param>
        /// <returns>True when scraper, blade, depth, size, turning diameter, and minimum work length match.</returns>
        public bool HasSameEquipment
            (
            HaulSettings Other
            )
        {
            return (ScraperCapacityLcy == Other.ScraperCapacityLcy) &&
                (BladeWidthMm == Other.BladeWidthMm) &&
                (MaxDepthMm == Other.MaxDepthMm) &&
                (ToleranceMm == Other.ToleranceMm) &&
                (EquipmentWidthFt == Other.EquipmentWidthFt) &&
                (EquipmentLengthFt == Other.EquipmentLengthFt) &&
                (MinimumWorkLengthFt == Other.MinimumWorkLengthFt) &&
                (EnableMinimumWorkLength == Other.EnableMinimumWorkLength) &&
                (TurningCircleFt == Other.TurningCircleFt);
        }
    }
}
