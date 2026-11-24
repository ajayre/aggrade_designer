using AgGradeDesigner.Core.Models;

namespace AgGradeDesigner.Core.Haul
{
    /// <summary>
    /// Maps project data and user haul settings to <see cref="HaulBuilder.HaulBuilderOptions"/> (§6.1).
    /// </summary>
    public static class HaulOptionsBuilder
    {
        /// <summary>
        /// Builds haul engine options for the active project and user settings.
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

            return Options;
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
