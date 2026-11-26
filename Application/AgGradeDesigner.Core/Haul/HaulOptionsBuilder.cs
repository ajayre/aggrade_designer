using AgGradeDesigner.Core.Features;
using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;

namespace AgGradeDesigner.Core.Haul
{
    /// <summary>
    /// Maps project data and user haul settings to <see cref="HaulBuilder.HaulBuilderOptions"/> (§6.1).
    /// </summary>
    public static class HaulOptionsBuilder
    {
        /// <summary>
        /// Minimum lane count sent with a placed road.
        /// The engine's own default is two lanes, which would widen a bed the user made narrower than that.
        /// Pan lane count still grows when the bed needs more than one lane.
        /// </summary>
        private const int PLACED_ROAD_MIN_LANES = 1;

        /// <summary>Half of the pad diameter. The haul top radius is the design-vehicle turn plus clearance.</summary>
        private const double PAD_RADIUS_OF_DIAMETER = 0.5;

        /// <summary>
        /// Builds haul engine options for the active project and user settings.
        /// Committed wells and roads are included. Reference points are names on the map and are not sent.
        /// </summary>
        /// <param name="Project">Open project with survey and design.</param>
        /// <param name="Settings">Persisted user haul defaults.</param>
        /// <param name="AgdPath">Temporary AGD file path passed to the engine.</param>
        /// <returns>Configured options ready for <see cref="HaulBuilder.HaulBuilderEngine.Run"/>.</returns>
        public static HaulBuilder.HaulBuilderOptions Build
            (
            ProjectDocument Project,
            HaulSettings Settings,
            string AgdPath
            )
        {
            DesignParameters Params = Project.DesignParameters
                ?? throw new InvalidOperationException("Design parameters are required to generate haul data.");

            HaulBuilder.HaulBuilderOptions Options = new HaulBuilder.HaulBuilderOptions
            {
                AgdPath = AgdPath,
                // intent: grid spacing is a user setting, not derived from blade width
                GridSizeFt = Settings.GridSizeFt,
                ToleranceMm = Settings.ToleranceMm,
                ScraperCapacityLcy = Settings.ScraperCapacityLcy,
                SwellFactor = Settings.SwellFactor,
                CutFillRatio = Params.CutFillRatio,
                MaxDepthMm = Settings.MaxDepthMm,
                BladeWidthMm = Settings.BladeWidthMm,
                FieldEfficiency = Settings.FieldEfficiency,
                CutSpeedMph = Settings.LoadingSpeedMph,
                LoadedTravelSpeedMph = Settings.HaulSpeedMph,
                FillSpeedMph = Settings.DumpingSpeedMph,
                UnloadedTravelSpeedMph = Settings.ReturnSpeedMph,
                TurnSpeedMph = Settings.TurnSpeedMph,
                GradeSpeedMph = Settings.GradeSpeedMph,
                AccelerationTimeS = Settings.AccelTimeS,
                DecelerationTimeS = Settings.DecelTimeS,
                MinimumTurningDiameterFt = Settings.TurningCircleFt,
                WaypointTurnThresholdDeg = Settings.TurnThresholdDeg,
                EquipmentWidthFt = Settings.EquipmentWidthFt,
                EquipmentLengthFt = Settings.EquipmentLengthFt,
                MinimumWorkLengthFt = Settings.MinimumWorkLengthFt,
                EnableMinimumWorkLength = Settings.EnableMinimumWorkLength,
                EnablePivot = Settings.Pivot,
                PivotWheelbaseFt = Settings.PivotWheelbaseFt,
                PivotClearanceFt = Settings.PivotClearanceFt,
                PivotMoveCost = Settings.PivotMoveCost,
                ZoneCount = Settings.Zones,
                PixelsPerCell = HaulBuilder.Constants.DEFAULT_PIXELS_PER_BIN,
                FlowArrowStrideCells = HaulFlowFieldResolver.DEFAULT_ARROW_STRIDE,
                RandomSeed = HaulBuilder.Constants.DEFAULT_SEED,
                // intent: waypoints are persisted directly; the guidance CSV is hundreds of megabytes and unused
                IncludeGuidanceCsv = false
            };

            ApplyGuidance(Options.Guidance, Settings.Guidance);
            ApplyOptimization(Options.Optimization, Settings.Optimization);
            ApplyPonding(Options.Ponding, Settings.Ponding);

            if (Settings.Pivot)
            {
                Options.PivotCenter =
                    $"{Settings.PivotLatitudeDeg.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                    $"{Settings.PivotLongitudeDeg.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
            }

            // intent: only features the user saved are built; an empty list leaves the field without a road or pad
            ApplyFieldFeatures(Options, Project.Features);

            return Options;
        }

        /// <summary>
        /// Copies committed wells and roads into the engine options.
        /// Crown slope, grade warning, pad height, pad side slope, slope-away, and casing exclusion
        /// stay unset so they inherit the engine defaults.
        /// </summary>
        /// <param name="Options">Options that receive the placed geometry.</param>
        /// <param name="Features">Wells and roads saved on the project.</param>
        private static void ApplyFieldFeatures
            (
            HaulBuilder.HaulBuilderOptions Options,
            FieldFeatureSet Features
            )
        {
            for (int Index = 0; Index < Features.Roads.Count; Index++)
            {
                RoadFeature Road = Features.Roads[Index];
                // A centreline needs a start and an end. A shorter draft is not a road the haul can build.
                if (Road.Vertices.Count < FieldFeatureSet.MIN_ROAD_VERTICES)
                    continue;

                Options.Roads.Add(ToRoadCentreline(Road));
            }

            for (int Index = 0; Index < Features.Wells.Count; Index++)
            {
                WellFeature Well = Features.Wells[Index];
                (double Easting, double Northing) = ToProjectMetres(Well.Latitude, Well.Longitude);
                HaulBuilder.WellPad Pad = new HaulBuilder.WellPad(Easting, Northing);
                // The circle drawn around the well is the pad top. Height and side slope stay at the engine defaults.
                ApplyPadDiameter(Pad, Options, Well.PadDiameterM);
                Options.WellPads.Add(Pad);
            }
        }

        /// <summary>
        /// Builds one engine centreline from a saved road.
        /// </summary>
        /// <param name="Road">Road saved on the project, in metres and decimal degrees.</param>
        /// <returns>Centreline in project metres, with the bed, height, and side slope in feet.</returns>
        private static HaulBuilder.RoadCentreline ToRoadCentreline
            (
            RoadFeature Road
            )
        {
            (double X, double Y)[] Vertices = new (double X, double Y)[Road.Vertices.Count];
            for (int Index = 0; Index < Road.Vertices.Count; Index++)
            {
                FeatureVertex Vertex = Road.Vertices[Index];
                Vertices[Index] = ToProjectMetres(Vertex.Latitude, Vertex.Longitude);
            }

            return new HaulBuilder.RoadCentreline(Vertices)
            {
                MinLanes = PLACED_ROAD_MIN_LANES,
                MinRoadbedFt = MetresToFeet(Road.BedWidthM),
                HeightFt = MetresToFeet(Road.HeightM),
                SideSlope = Road.SideSlope
            };
        }

        /// <summary>
        /// Sets the pad top radius to half the diameter drawn around the well.
        /// The engine adds its default clearance outside the design-vehicle turn, so the turn is the remainder.
        /// A diameter smaller than that clearance uses the whole radius as the turn and no extra clearance.
        /// </summary>
        /// <param name="Pad">Pad that receives the radius.</param>
        /// <param name="Options">Engine options whose default clearance is used when it fits.</param>
        /// <param name="PadDiameterM">Pad diameter in metres.</param>
        private static void ApplyPadDiameter
            (
            HaulBuilder.WellPad Pad,
            HaulBuilder.HaulBuilderOptions Options,
            double PadDiameterM
            )
        {
            double RadiusFt = MetresToFeet(PadDiameterM) * PAD_RADIUS_OF_DIAMETER;
            if (!double.IsFinite(RadiusFt) || (RadiusFt <= 0.0))
                return;

            double ClearanceFt = Options.PadClearanceFt;
            if (RadiusFt > ClearanceFt)
            {
                Pad.DesignTurnRadiusFt = RadiusFt - ClearanceFt;
                return;
            }

            Pad.ClearanceFt = 0.0;
            Pad.DesignTurnRadiusFt = RadiusFt;
        }

        /// <summary>
        /// Projects one field location into the UTM metres the haul grid uses.
        /// </summary>
        /// <param name="Latitude">Latitude in decimal degrees.</param>
        /// <param name="Longitude">Longitude in decimal degrees.</param>
        /// <returns>Easting and northing in metres.</returns>
        private static (double Easting, double Northing) ToProjectMetres
            (
            double Latitude,
            double Longitude
            )
        {
            UTM.UTMCoordinate Projected = UTM.FromLatLon(Latitude, Longitude);
            return (Projected.Easting, Projected.Northing);
        }

        /// <summary>
        /// Converts a stored length to the feet the haul engine expects.
        /// </summary>
        /// <param name="Metres">Length in metres.</param>
        /// <returns>The same length in feet.</returns>
        private static double MetresToFeet
            (
            double Metres
            )
        {
            return Metres / FieldConstants.METERS_PER_FOOT;
        }

        /// <summary>
        /// Copies stored Dubins rules onto the engine guidance options object.
        /// </summary>
        /// <param name="Target">Engine guidance options. The property is get-only, so fields are assigned in place.</param>
        /// <param name="Source">Stored guidance settings.</param>
        private static void ApplyGuidance
            (
            HaulBuilder.GuidanceOptions Target,
            HaulGuidanceSettings Source
            )
        {
            Target.ShortJointRadii = Source.ShortJointRadii;
            Target.ConnectorTieFraction = Source.ConnectorTieFraction;
            Target.ConnectorTieRadii = Source.ConnectorTieRadii;
            Target.SteerHeadingDeg = Source.SteerHeadingDeg;
            Target.MaxCorrectingLines = Source.MaxCorrectingLines;
            Target.LeftoverRatio = Source.LeftoverRatio;
            Target.MergeRouteRadii = Source.MergeRouteRadii;
            Target.EndFragmentSpanRadii = Source.EndFragmentSpanRadii;
            Target.EndFragmentGapRadii = Source.EndFragmentGapRadii;
            Target.NearEmptyRadii = Source.NearEmptyRadii;
            Target.NearEmptyBowlFraction = Source.NearEmptyBowlFraction;
            Target.ArcSpeedMph = Source.ArcSpeedMph;
            Target.GradeLaneSpeedMph = Source.GradeLaneSpeedMph;
        }

        /// <summary>
        /// Copies stored optimizer tuning onto the engine optimization options object.
        /// </summary>
        /// <param name="Target">Engine optimization options. The property is get-only, so fields are assigned in place.</param>
        /// <param name="Source">Stored optimization settings.</param>
        private static void ApplyOptimization
            (
            HaulBuilder.HaulOptimizationOptions Target,
            HaulOptimizationSettings Source
            )
        {
            Target.EnableRouteConsolidation = Source.EnableRouteConsolidation;
            Target.RouteConsolidationGroupSize = Source.RouteConsolidationGroupSize;
            Target.RouteConsolidationMaximumCycles = Source.RouteConsolidationMaximumCycles;
            Target.PairRefinementRounds = Source.PairRefinementRounds;
            Target.EnableMultiRouteExchange = Source.EnableMultiRouteExchange;
            Target.MultiRouteExchangeRounds = Source.MultiRouteExchangeRounds;
            Target.MultiRouteExchangeGroupSize = Source.MultiRouteExchangeGroupSize;
            Target.MultiRouteExchangeMaximumGroups = Source.MultiRouteExchangeMaximumGroups;
            Target.MultiRouteExchangeMinimumImprovementM = Source.MultiRouteExchangeMinimumImprovementM;
            Target.EnableCycleChainOptimization = Source.EnableCycleChainOptimization;
            Target.CycleChainRounds = Source.CycleChainRounds;
            Target.CycleChainCandidatesPerRoute = Source.CycleChainCandidatesPerRoute;
            Target.CycleChainMinimumImprovementSeconds = Source.CycleChainMinimumImprovementSeconds;
            Target.RouteReorderWindow = Source.RouteReorderWindow;
            Target.EnableEarlyFullStripBanding = Source.EnableEarlyFullStripBanding;
            Target.OptimizeRouteOrderByDistance = Source.OptimizeRouteOrderByDistance;
            Target.ClusterModeAverageWindow = Source.ClusterModeAverageWindow;
            Target.ClusterModeLoadFraction = Source.ClusterModeLoadFraction;
        }

        /// <summary>
        /// Copies stored ponding settings onto the engine ponding options object.
        /// </summary>
        /// <param name="Target">Engine ponding options. The property is get-only, so fields are assigned in place.</param>
        /// <param name="Source">Stored ponding settings.</param>
        private static void ApplyPonding
            (
            HaulBuilder.PondingOptions Target,
            HaulPondingSettings Source
            )
        {
            Target.Optimize = Source.Optimize;
            Target.ObjectiveMode = Source.ObjectiveMode;
            Target.Audit = Source.Audit;
            Target.NoImplicitAudit = Source.NoImplicitAudit;
            Target.TargetSpillOutlets = Source.TargetSpillOutlets;
            Target.SpillToleranceFt = Source.SpillToleranceFt;
            Target.Weight = Source.Weight;
            // intent: zero in the dialog means "engine default interval", which is null
            Target.CheckpointInterval = Source.CheckpointInterval is > 0
                ? Source.CheckpointInterval
                : null;
            Target.WhiteBoxToolsPath = string.IsNullOrWhiteSpace(Source.WhiteBoxToolsPath)
                ? null
                : Source.WhiteBoxToolsPath;
            Target.WhiteBoxToolsWorkDirectory = string.IsNullOrWhiteSpace(Source.WhiteBoxToolsWorkDirectory)
                ? null
                : Source.WhiteBoxToolsWorkDirectory;
            Target.WarnDepthFt = Source.WarnDepthFt;
            Target.WarnVolumeBcy = Source.WarnVolumeBcy;
            Target.DesignRainfallIn = Source.DesignRainfallIn;
            Target.FailOnWarn = Source.FailOnWarn;
            Target.IncludeMap = Source.IncludeMap;
            Target.MapPixelsPerCell = Source.MapPixelsPerCell;
            Target.OutletSpecs.Clear();
            Target.OutletSpecs.AddRange(Source.OutletSpecs);
            Target.OutletPolygonSpecs.Clear();
            Target.OutletPolygonSpecs.AddRange(Source.OutletPolygonSpecs);
        }
    }
}
