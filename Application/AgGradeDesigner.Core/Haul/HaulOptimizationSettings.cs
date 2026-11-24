namespace AgGradeDesigner.Core.Haul
{
    /// <summary>
    /// Expert haul-selection and route-order tuning stored with a project.
    /// Values match <see cref="HaulBuilder.HaulOptimizationOptions"/>.
    /// </summary>
    public sealed class HaulOptimizationSettings
    {
        /// <summary>Measured optimizer defaults from a new <see cref="HaulBuilder.HaulBuilderOptions"/>.</summary>
        private static readonly HaulBuilder.HaulOptimizationOptions EngineDefaults =
            new HaulBuilder.HaulBuilderOptions().Optimization;

        /// <summary>
        /// Whether capacity-flexible route consolidation runs.
        /// Off for a default plan. A flow ribbon is the proof search, and consolidation rebuilds loads as cell groups.
        /// </summary>
        public bool EnableRouteConsolidation { get; set; } = false;

        /// <summary>Maximum routes pooled in one consolidation transaction.</summary>
        public int RouteConsolidationGroupSize { get; set; } = EngineDefaults.RouteConsolidationGroupSize;

        /// <summary>Maximum cycles removed by consolidation.</summary>
        public int RouteConsolidationMaximumCycles { get; set; } = EngineDefaults.RouteConsolidationMaximumCycles;

        /// <summary>
        /// Deterministic equal-load fill-pair refinement rounds. Zero skips refinement.
        /// A default plan keeps the flow-ribbon loads.
        /// </summary>
        public int PairRefinementRounds { get; set; } = 0;

        /// <summary>Whether the closed multi-route exchange runs.</summary>
        public bool EnableMultiRouteExchange { get; set; } = EngineDefaults.EnableMultiRouteExchange;

        /// <summary>Bounded multi-route exchange rounds.</summary>
        public int MultiRouteExchangeRounds { get; set; } = EngineDefaults.MultiRouteExchangeRounds;

        /// <summary>Maximum routes in one multi-route exchange.</summary>
        public int MultiRouteExchangeGroupSize { get; set; } = EngineDefaults.MultiRouteExchangeGroupSize;

        /// <summary>Maximum committed multi-route exchanges.</summary>
        public int MultiRouteExchangeMaximumGroups { get; set; } = EngineDefaults.MultiRouteExchangeMaximumGroups;

        /// <summary>Minimum compact-guidance improvement per exchange, in metres.</summary>
        public double MultiRouteExchangeMinimumImprovementM { get; set; } = EngineDefaults.MultiRouteExchangeMinimumImprovementM;

        /// <summary>Whether exact-gated field-wide fill-chain refinement runs.</summary>
        public bool EnableCycleChainOptimization { get; set; } = EngineDefaults.EnableCycleChainOptimization;

        /// <summary>Field-wide fill-chain rounds.</summary>
        public int CycleChainRounds { get; set; } = EngineDefaults.CycleChainRounds;

        /// <summary>Exact candidates checked per route.</summary>
        public int CycleChainCandidatesPerRoute { get; set; } = EngineDefaults.CycleChainCandidatesPerRoute;

        /// <summary>Minimum production-objective saving per chain exchange, in seconds.</summary>
        public double CycleChainMinimumImprovementSeconds { get; set; } = EngineDefaults.CycleChainMinimumImprovementSeconds;

        /// <summary>
        /// Route relocation search window. Zero disables relocation.
        /// A default plan leaves trip order to the flow search and to drive order.
        /// </summary>
        public int RouteReorderWindow { get; set; } = 0;

        /// <summary>
        /// Whether routes are driven as strip bands before the field-edge batch.
        /// Off for a default plan. Edge work stays deferred inside the flow search.
        /// </summary>
        public bool EnableEarlyFullStripBanding { get; set; } = false;

        /// <summary>Whether the next load is chosen by metres rather than seconds.</summary>
        public bool OptimizeRouteOrderByDistance { get; set; } = false;

        /// <summary>How many upcoming trips are priced against the cluster and thin-cut plans. The proof search uses 3.</summary>
        public int ClusterModeAverageWindow { get; set; } = 3;

        /// <summary>Share of a bowl that may leave the flow ribbon. The proof search uses one half.</summary>
        public double ClusterModeLoadFraction { get; set; } = 0.5;

        /// <summary>
        /// Creates an independent copy.
        /// </summary>
        /// <returns>Settings with the same values.</returns>
        public HaulOptimizationSettings Clone
            (
            )
        {
            return new HaulOptimizationSettings
            {
                EnableRouteConsolidation = EnableRouteConsolidation,
                RouteConsolidationGroupSize = RouteConsolidationGroupSize,
                RouteConsolidationMaximumCycles = RouteConsolidationMaximumCycles,
                PairRefinementRounds = PairRefinementRounds,
                EnableMultiRouteExchange = EnableMultiRouteExchange,
                MultiRouteExchangeRounds = MultiRouteExchangeRounds,
                MultiRouteExchangeGroupSize = MultiRouteExchangeGroupSize,
                MultiRouteExchangeMaximumGroups = MultiRouteExchangeMaximumGroups,
                MultiRouteExchangeMinimumImprovementM = MultiRouteExchangeMinimumImprovementM,
                EnableCycleChainOptimization = EnableCycleChainOptimization,
                CycleChainRounds = CycleChainRounds,
                CycleChainCandidatesPerRoute = CycleChainCandidatesPerRoute,
                CycleChainMinimumImprovementSeconds = CycleChainMinimumImprovementSeconds,
                RouteReorderWindow = RouteReorderWindow,
                EnableEarlyFullStripBanding = EnableEarlyFullStripBanding,
                OptimizeRouteOrderByDistance = OptimizeRouteOrderByDistance,
                ClusterModeAverageWindow = ClusterModeAverageWindow,
                ClusterModeLoadFraction = ClusterModeLoadFraction
            };
        }

        /// <summary>
        /// Copies values from another optimization settings instance.
        /// </summary>
        /// <param name="Source">Settings to copy.</param>
        public void CopyFrom
            (
            HaulOptimizationSettings Source
            )
        {
            EnableRouteConsolidation = Source.EnableRouteConsolidation;
            RouteConsolidationGroupSize = Source.RouteConsolidationGroupSize;
            RouteConsolidationMaximumCycles = Source.RouteConsolidationMaximumCycles;
            PairRefinementRounds = Source.PairRefinementRounds;
            EnableMultiRouteExchange = Source.EnableMultiRouteExchange;
            MultiRouteExchangeRounds = Source.MultiRouteExchangeRounds;
            MultiRouteExchangeGroupSize = Source.MultiRouteExchangeGroupSize;
            MultiRouteExchangeMaximumGroups = Source.MultiRouteExchangeMaximumGroups;
            MultiRouteExchangeMinimumImprovementM = Source.MultiRouteExchangeMinimumImprovementM;
            EnableCycleChainOptimization = Source.EnableCycleChainOptimization;
            CycleChainRounds = Source.CycleChainRounds;
            CycleChainCandidatesPerRoute = Source.CycleChainCandidatesPerRoute;
            CycleChainMinimumImprovementSeconds = Source.CycleChainMinimumImprovementSeconds;
            RouteReorderWindow = Source.RouteReorderWindow;
            EnableEarlyFullStripBanding = Source.EnableEarlyFullStripBanding;
            OptimizeRouteOrderByDistance = Source.OptimizeRouteOrderByDistance;
            ClusterModeAverageWindow = Source.ClusterModeAverageWindow;
            ClusterModeLoadFraction = Source.ClusterModeLoadFraction;
        }
    }
}
