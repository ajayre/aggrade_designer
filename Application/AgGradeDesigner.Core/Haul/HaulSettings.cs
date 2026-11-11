namespace AgGradeDesigner.Core.Haul
{
    /// <summary>
    /// Haul-planning parameters stored per project and seeded from user defaults for new projects (HAUL-SPEC §7.6).
    /// </summary>
    public sealed class HaulSettings
    {
        /// <summary>Default option values from <see cref="HaulBuilder.HaulBuilderOptions"/>.</summary>
        private static readonly HaulBuilder.HaulBuilderOptions EngineDefaults = new HaulBuilder.HaulBuilderOptions();

        /// <summary>Default blade width in feet (independent of haul grid spacing).</summary>
        private const double DEFAULT_BLADE_WIDTH_FT = 8.5;

        /// <summary>Default haul processing grid cell edge length in feet.</summary>
        private const double DEFAULT_GRID_SIZE_FT = 8.5;

        /// <summary>Default tractor+scraper overall length in feet.</summary>
        private const double DEFAULT_EQUIPMENT_LENGTH_FT = 43.0;

        /// <summary>Default turning circle diameter in feet.</summary>
        private const double DEFAULT_TURNING_CIRCLE_FT = 50.0;

        /// <summary>Default maximum multi-drop hop distance in feet (retired engine option).</summary>
        private const double DEFAULT_MAX_DROP_HOP_FT = 50.0;

        /// <summary>Default minimum load fill percentage (retired engine option).</summary>
        private const double DEFAULT_MIN_LOAD_FILL_PCT = 90.0;

        /// <summary>Default lane overlap fraction (retired engine option).</summary>
        private const double DEFAULT_LANE_OVERLAP = 0.1;

        /// <summary>Default transport mode for flow-field recomputation (retired engine option).</summary>
        private const string DEFAULT_TRANSPORT_MODE = "poisson";

        /// <summary>Default balance mode label (retired engine option).</summary>
        private const string DEFAULT_BALANCE_MODE = "check";

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

        /// <summary>Maximum multi-drop hop distance in feet (UI-only; retired from the engine).</summary>
        public double MaxDropHopFt { get; set; } = DEFAULT_MAX_DROP_HOP_FT;

        /// <summary>Minimum load fill percentage before stopping extra hops (UI-only; retired from the engine).</summary>
        public double MinLoadFillPct { get; set; } = DEFAULT_MIN_LOAD_FILL_PCT;

        /// <summary>Equipment width in feet for obstacle buffering.</summary>
        public double EquipmentWidthFt { get; set; } = EngineDefaults.EquipmentWidthFt;

        /// <summary>Tractor+scraper overall length in feet for straight lineup before/after CUT/FILL.</summary>
        public double EquipmentLengthFt { get; set; } = DEFAULT_EQUIPMENT_LENGTH_FT;

        /// <summary>Lane overlap fraction (UI-only; retired from the engine).</summary>
        public double LaneOverlap { get; set; } = DEFAULT_LANE_OVERLAP;

        /// <summary>Field efficiency fraction in [0, 1].</summary>
        public double FieldEfficiency { get; set; } = EngineDefaults.FieldEfficiency;

        /// <summary>Transport mode for flow-field recomputation: <c>poisson</c> or <c>mincostflow</c>.</summary>
        public string TransportMode { get; set; } = DEFAULT_TRANSPORT_MODE;

        /// <summary>Balance mode label retained for project compatibility (retired from the engine).</summary>
        public string BalanceMode { get; set; } = DEFAULT_BALANCE_MODE;

        /// <summary>Cut zone count for sequencing (<see cref="HaulBuilder.HaulBuilderOptions.ZoneCount"/>).</summary>
        public int Zones { get; set; } = EngineDefaults.ZoneCount;

        /// <summary>When true, flow-aligned strip growth is disabled (UI-only; retired from the engine).</summary>
        public bool NoFlowAligned { get; set; }

        /// <summary>When true, heading comes from the open project design main slope direction.</summary>
        public bool UseDesignMainSlopeDirection { get; set; } = true;

        /// <summary>User heading in degrees when <see cref="UseDesignMainSlopeDirection"/> is false.</summary>
        public double HeadingDeg { get; set; }

        /// <summary>Loading/cut speed in miles per hour.</summary>
        public double LoadingSpeedMph { get; set; } = EngineDefaults.CutSpeedMph;

        /// <summary>Loaded travel speed in miles per hour.</summary>
        public double HaulSpeedMph { get; set; } = EngineDefaults.LoadedTravelSpeedMph;

        /// <summary>Fill/dump speed in miles per hour.</summary>
        public double DumpingSpeedMph { get; set; } = EngineDefaults.FillSpeedMph;

        /// <summary>Unloaded return speed in miles per hour.</summary>
        public double ReturnSpeedMph { get; set; } = EngineDefaults.UnloadedTravelSpeedMph;

        /// <summary>Turn speed in miles per hour.</summary>
        public double TurnSpeedMph { get; set; } = EngineDefaults.TurnSpeedMph;

        /// <summary>Grade/compact speed in miles per hour.</summary>
        public double GradeSpeedMph { get; set; } = EngineDefaults.GradeSpeedMph;

        /// <summary>Acceleration phase duration in seconds.</summary>
        public double AccelTimeS { get; set; } = EngineDefaults.AccelerationTimeS;

        /// <summary>Deceleration phase duration in seconds.</summary>
        public double DecelTimeS { get; set; } = EngineDefaults.DecelerationTimeS;

        /// <summary>Turning circle diameter in feet.</summary>
        public double TurningCircleFt { get; set; } = DEFAULT_TURNING_CIRCLE_FT;

        /// <summary>Heading-change threshold treated as a turn, in degrees.</summary>
        public double TurnThresholdDeg { get; set; } = EngineDefaults.WaypointTurnThresholdDeg;

        /// <summary>When true, grade-return passes are disabled (UI-only; retired from the engine).</summary>
        public bool NoGradeReturn { get; set; }

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

        /// <summary>Pivot move cost in detour-equivalent passes.</summary>
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
                MaxDropHopFt = MaxDropHopFt,
                MinLoadFillPct = MinLoadFillPct,
                EquipmentWidthFt = EquipmentWidthFt,
                EquipmentLengthFt = EquipmentLengthFt,
                LaneOverlap = LaneOverlap,
                FieldEfficiency = FieldEfficiency,
                TransportMode = TransportMode,
                BalanceMode = BalanceMode,
                Zones = Zones,
                NoFlowAligned = NoFlowAligned,
                UseDesignMainSlopeDirection = UseDesignMainSlopeDirection,
                HeadingDeg = HeadingDeg,
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
                NoGradeReturn = NoGradeReturn,
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
            MaxDropHopFt = Source.MaxDropHopFt;
            MinLoadFillPct = Source.MinLoadFillPct;
            EquipmentWidthFt = Source.EquipmentWidthFt;
            EquipmentLengthFt = Source.EquipmentLengthFt;
            LaneOverlap = Source.LaneOverlap;
            FieldEfficiency = Source.FieldEfficiency;
            TransportMode = Source.TransportMode;
            BalanceMode = Source.BalanceMode;
            Zones = Source.Zones;
            NoFlowAligned = Source.NoFlowAligned;
            UseDesignMainSlopeDirection = Source.UseDesignMainSlopeDirection;
            HeadingDeg = Source.HeadingDeg;
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
            NoGradeReturn = Source.NoGradeReturn;
            Pivot = Source.Pivot;
            PivotLatitudeDeg = Source.PivotLatitudeDeg;
            PivotLongitudeDeg = Source.PivotLongitudeDeg;
            PivotWheelbaseFt = Source.PivotWheelbaseFt;
            PivotClearanceFt = Source.PivotClearanceFt;
            PivotMoveCost = Source.PivotMoveCost;
        }
    }
}
