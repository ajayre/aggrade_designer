namespace AgGradeDesigner.Core.Haul
{
    /// <summary>
    /// Haul-planning parameters stored per project and seeded from user defaults for new projects (HAUL-SPEC §7.6).
    /// </summary>
    public sealed class HaulSettings
    {
        /// <summary>Default option values from <see cref="HaulBuilder.HaulBuilderOptions"/>.</summary>
        private static readonly HaulBuilder.HaulBuilderOptions EngineDefaults = new HaulBuilder.HaulBuilderOptions();

        /// <summary>Scraper capacity in loose cubic yards (<see cref="HaulBuilder.HaulBuilderOptions.ScraperCapacityLcy"/>).</summary>
        public double ScraperCapacityLcy { get; set; } = EngineDefaults.ScraperCapacityLcy;

        /// <summary>Swell factor for LCY/BCY conversion.</summary>
        public double SwellFactor { get; set; } = EngineDefaults.SwellFactor;

        /// <summary>Blade width in millimetres.</summary>
        public double BladeWidthMm { get; set; } = EngineDefaults.BladeWidthMm;

        /// <summary>Maximum cut/fill depth per pass in millimetres.</summary>
        public double MaxDepthMm { get; set; } = EngineDefaults.MaxDepthMm;

        /// <summary>Finishing tolerance in millimetres.</summary>
        public double ToleranceMm { get; set; } = EngineDefaults.ToleranceMm;

        /// <summary>Maximum multi-drop hop distance in feet.</summary>
        public double MaxDropHopFt { get; set; } = EngineDefaults.MaxDropHopFt;

        /// <summary>Minimum load fill percentage before stopping extra hops.</summary>
        public double MinLoadFillPct { get; set; } = EngineDefaults.MinLoadFillPct;

        /// <summary>Equipment width in feet for obstacle buffering.</summary>
        public double EquipmentWidthFt { get; set; } = EngineDefaults.EquipmentWidthFt;

        /// <summary>Lane overlap fraction.</summary>
        public double LaneOverlap { get; set; } = EngineDefaults.LaneOverlap;

        /// <summary>Field efficiency fraction in [0, 1].</summary>
        public double FieldEfficiency { get; set; } = EngineDefaults.FieldEfficiency;

        /// <summary>Transport mode: <c>poisson</c> or <c>mincostflow</c>.</summary>
        public string TransportMode { get; set; } = EngineDefaults.TransportMode;

        /// <summary>Balance mode: <c>check</c> or <c>adjust</c>.</summary>
        public string BalanceMode { get; set; } = EngineDefaults.BalanceMode;

        /// <summary>Cut zone count for sequencing.</summary>
        public int Zones { get; set; } = EngineDefaults.Zones;

        /// <summary>When true, flow-aligned strip growth is disabled (<see cref="HaulBuilder.HaulBuilderOptions.NoFlowAligned"/>).</summary>
        public bool NoFlowAligned { get; set; } = EngineDefaults.NoFlowAligned;

        /// <summary>When true, heading comes from the open project design main slope direction.</summary>
        public bool UseDesignMainSlopeDirection { get; set; } = true;

        /// <summary>User heading in degrees when <see cref="UseDesignMainSlopeDirection"/> is false.</summary>
        public double HeadingDeg { get; set; }

        /// <summary>Loading speed in miles per hour.</summary>
        public double LoadingSpeedMph { get; set; } = EngineDefaults.LoadingSpeedMph;

        /// <summary>Haul speed in miles per hour.</summary>
        public double HaulSpeedMph { get; set; } = EngineDefaults.HaulSpeedMph;

        /// <summary>Dumping speed in miles per hour.</summary>
        public double DumpingSpeedMph { get; set; } = EngineDefaults.DumpingSpeedMph;

        /// <summary>Return speed in miles per hour.</summary>
        public double ReturnSpeedMph { get; set; } = EngineDefaults.ReturnSpeedMph;

        /// <summary>Turn speed in miles per hour.</summary>
        public double TurnSpeedMph { get; set; } = EngineDefaults.TurnSpeedMph;

        /// <summary>Grade/compact speed in miles per hour.</summary>
        public double GradeSpeedMph { get; set; } = EngineDefaults.GradeSpeedMph;

        /// <summary>Acceleration phase duration in seconds.</summary>
        public double AccelTimeS { get; set; } = EngineDefaults.AccelTimeS;

        /// <summary>Deceleration phase duration in seconds.</summary>
        public double DecelTimeS { get; set; } = EngineDefaults.DecelTimeS;

        /// <summary>Turning circle diameter in feet.</summary>
        public double TurningCircleFt { get; set; } = EngineDefaults.TurningCircleFt;

        /// <summary>Heading-change threshold treated as a turn, in degrees.</summary>
        public double TurnThresholdDeg { get; set; } = EngineDefaults.TurnThresholdDeg;

        /// <summary>When true, grade-return passes are disabled (<see cref="HaulBuilder.HaulBuilderOptions.NoGradeReturn"/>).</summary>
        public bool NoGradeReturn { get; set; } = EngineDefaults.NoGradeReturn;

        /// <summary>Whether center-pivot analysis is enabled.</summary>
        public bool Pivot { get; set; } = EngineDefaults.Pivot;

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
