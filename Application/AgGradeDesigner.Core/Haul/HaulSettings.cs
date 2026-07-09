namespace AgGradeDesigner.Core.Haul
{
    /// <summary>
    /// Haul-planning parameters stored per project and seeded from user defaults for new projects (HAUL-SPEC §7.6).
    /// </summary>
    public sealed class HaulSettings
    {
        /// <summary>Scraper capacity in loose cubic yards (<see cref="HaulBuilder.HaulBuilderOptions.ScraperCapacityLcy"/>).</summary>
        public double ScraperCapacityLcy { get; set; } = HaulBuilder.Constants.DEFAULT_SCRAPER_CAPACITY_LCY;

        /// <summary>Swell factor for LCY/BCY conversion.</summary>
        public double SwellFactor { get; set; } = HaulBuilder.Constants.DEFAULT_SWELL_FACTOR;

        /// <summary>Blade width in millimetres.</summary>
        public double BladeWidthMm { get; set; } = HaulBuilder.Constants.DEFAULT_BLADE_WIDTH_MM;

        /// <summary>Maximum cut/fill depth per pass in millimetres.</summary>
        public double MaxDepthMm { get; set; } = HaulBuilder.Constants.DEFAULT_MAX_DEPTH_MM;

        /// <summary>Finishing tolerance in millimetres.</summary>
        public double ToleranceMm { get; set; } = HaulBuilder.Constants.DEFAULT_TOLERANCE_MM;

        /// <summary>Maximum multi-drop hop distance in feet.</summary>
        public double MaxDropHopFt { get; set; } = HaulBuilder.Constants.DEFAULT_MAX_DROP_HOP_FT;

        /// <summary>Minimum load fill percentage before stopping extra hops.</summary>
        public double MinLoadFillPct { get; set; } = 50.0;

        /// <summary>Equipment width in feet for obstacle buffering.</summary>
        public double EquipmentWidthFt { get; set; } = 12.0;

        /// <summary>Lane overlap fraction.</summary>
        public double LaneOverlap { get; set; } = 0.5;

        /// <summary>Field efficiency fraction in [0, 1].</summary>
        public double FieldEfficiency { get; set; } = HaulBuilder.Constants.DEFAULT_FIELD_EFFICIENCY;

        /// <summary>Transport mode: <c>poisson</c> or <c>mincostflow</c>.</summary>
        public string TransportMode { get; set; } = "poisson";

        /// <summary>Balance mode: <c>check</c> or <c>adjust</c>.</summary>
        public string BalanceMode { get; set; } = "check";

        /// <summary>Cut zone count for sequencing.</summary>
        public int Zones { get; set; } = 1;

        /// <summary>When true, heading comes from the open project design main slope direction.</summary>
        public bool UseDesignMainSlopeDirection { get; set; } = true;

        /// <summary>User heading in degrees when <see cref="UseDesignMainSlopeDirection"/> is false.</summary>
        public double HeadingDeg { get; set; } = 0.0;

        /// <summary>Loading speed in miles per hour.</summary>
        public double LoadingSpeedMph { get; set; } = HaulBuilder.Constants.DEFAULT_LOADING_SPEED_MPH;

        /// <summary>Haul speed in miles per hour.</summary>
        public double HaulSpeedMph { get; set; } = HaulBuilder.Constants.DEFAULT_HAUL_SPEED_MPH;

        /// <summary>Dumping speed in miles per hour.</summary>
        public double DumpingSpeedMph { get; set; } = HaulBuilder.Constants.DEFAULT_DUMPING_SPEED_MPH;

        /// <summary>Return speed in miles per hour.</summary>
        public double ReturnSpeedMph { get; set; } = HaulBuilder.Constants.DEFAULT_RETURN_SPEED_MPH;

        /// <summary>Turn speed in miles per hour.</summary>
        public double TurnSpeedMph { get; set; } = HaulBuilder.Constants.DEFAULT_TURN_SPEED_MPH;

        /// <summary>Grade/compact speed in miles per hour.</summary>
        public double GradeSpeedMph { get; set; } = HaulBuilder.Constants.DEFAULT_GRADE_SPEED_MPH;

        /// <summary>Acceleration phase duration in seconds.</summary>
        public double AccelTimeS { get; set; } = HaulBuilder.Constants.DEFAULT_ACCEL_TIME_S;

        /// <summary>Deceleration phase duration in seconds.</summary>
        public double DecelTimeS { get; set; } = HaulBuilder.Constants.DEFAULT_DECEL_TIME_S;

        /// <summary>Turning circle diameter in feet.</summary>
        public double TurningCircleFt { get; set; } = HaulBuilder.Constants.DEFAULT_TURNING_CIRCLE_FT;

        /// <summary>Heading-change threshold treated as a turn, in degrees.</summary>
        public double TurnThresholdDeg { get; set; } = HaulBuilder.Constants.DEFAULT_TURN_THRESHOLD_DEG;

        /// <summary>Whether center-pivot analysis is enabled.</summary>
        public bool Pivot { get; set; }

        /// <summary>Pivot centre latitude in decimal degrees (WGS-84).</summary>
        public double PivotLatitudeDeg { get; set; }

        /// <summary>Pivot centre longitude in decimal degrees (WGS-84).</summary>
        public double PivotLongitudeDeg { get; set; }

        /// <summary>Pivot arm wheelbase in feet.</summary>
        public double PivotWheelbaseFt { get; set; } = HaulBuilder.Constants.DEFAULT_PIVOT_WHEELBASE_FT;

        /// <summary>Extra pivot arm clearance per side in feet.</summary>
        public double PivotClearanceFt { get; set; } = HaulBuilder.Constants.DEFAULT_PIVOT_CLEARANCE_FT;

        /// <summary>Pivot move cost in detour-equivalent passes.</summary>
        public double PivotMoveCost { get; set; } = 1.0;

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
                MaxDepthMm = MaxDepthMm,
                ToleranceMm = ToleranceMm,
                MaxDropHopFt = MaxDropHopFt,
                MinLoadFillPct = MinLoadFillPct,
                EquipmentWidthFt = EquipmentWidthFt,
                LaneOverlap = LaneOverlap,
                FieldEfficiency = FieldEfficiency,
                TransportMode = TransportMode,
                BalanceMode = BalanceMode,
                Zones = Zones,
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
            MaxDepthMm = Source.MaxDepthMm;
            ToleranceMm = Source.ToleranceMm;
            MaxDropHopFt = Source.MaxDropHopFt;
            MinLoadFillPct = Source.MinLoadFillPct;
            EquipmentWidthFt = Source.EquipmentWidthFt;
            LaneOverlap = Source.LaneOverlap;
            FieldEfficiency = Source.FieldEfficiency;
            TransportMode = Source.TransportMode;
            BalanceMode = Source.BalanceMode;
            Zones = Source.Zones;
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
            Pivot = Source.Pivot;
            PivotLatitudeDeg = Source.PivotLatitudeDeg;
            PivotLongitudeDeg = Source.PivotLongitudeDeg;
            PivotWheelbaseFt = Source.PivotWheelbaseFt;
            PivotClearanceFt = Source.PivotClearanceFt;
            PivotMoveCost = Source.PivotMoveCost;
        }
    }
}
