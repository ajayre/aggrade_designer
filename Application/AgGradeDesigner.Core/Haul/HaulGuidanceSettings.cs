namespace AgGradeDesigner.Core.Haul
{
    /// <summary>
    /// Dubins trip-guidance rules stored with a project. Values match
    /// <see cref="HaulBuilder.GuidanceOptions"/>.
    /// </summary>
    public sealed class HaulGuidanceSettings
    {
        /// <summary>Measured Dubins defaults from a new <see cref="HaulBuilder.HaulBuilderOptions"/>.</summary>
        private static readonly HaulBuilder.GuidanceOptions EngineDefaults = new HaulBuilder.HaulBuilderOptions().Guidance;

        /// <summary>Gap, in turning radii, that is still a short joint.</summary>
        public double ShortJointRadii { get; set; } = EngineDefaults.ShortJointRadii;

        /// <summary>Extra connector length, as a fraction of the shortest connector, that still counts as a tie.</summary>
        public double ConnectorTieFraction { get; set; } = EngineDefaults.ConnectorTieFraction;

        /// <summary>Extra connector length, in turning radii, that still counts as a tie.</summary>
        public double ConnectorTieRadii { get; set; } = EngineDefaults.ConnectorTieRadii;

        /// <summary>Heading change, in degrees, below which a short joint may be a straight steer.</summary>
        public double SteerHeadingDeg { get; set; } = EngineDefaults.SteerHeadingDeg;

        /// <summary>Cut lines, and spread lines, kept on one correcting trip.</summary>
        public int MaxCorrectingLines { get; set; } = EngineDefaults.MaxCorrectingLines;

        /// <summary>Connector length over productive length above which a correcting trip may merge.</summary>
        public double LeftoverRatio { get; set; } = EngineDefaults.LeftoverRatio;

        /// <summary>How close, in turning radii, a later correcting route must pass to take a leftover.</summary>
        public double MergeRouteRadii { get; set; } = EngineDefaults.MergeRouteRadii;

        /// <summary>End-fragment length, in turning radii, that is short enough to defer.</summary>
        public double EndFragmentSpanRadii { get; set; } = EngineDefaults.EndFragmentSpanRadii;

        /// <summary>Gap, in turning radii, past the next fragment that defers a short end.</summary>
        public double EndFragmentGapRadii { get; set; } = EngineDefaults.EndFragmentGapRadii;

        /// <summary>Lane length, in turning radii, below which a cut or spread does not separate connectors.</summary>
        public double NearEmptyRadii { get; set; } = EngineDefaults.NearEmptyRadii;

        /// <summary>Lane volume, as a fraction of the bowl, below which a cut or spread does not separate connectors.</summary>
        public double NearEmptyBowlFraction { get; set; } = EngineDefaults.NearEmptyBowlFraction;

        /// <summary>Dubins arc speed in miles per hour. Separate from route-order turn speed.</summary>
        public double ArcSpeedMph { get; set; } = EngineDefaults.ArcSpeedMph;

        /// <summary>Grade-lane speed in miles per hour. Separate from the production grade-return speed.</summary>
        public double GradeLaneSpeedMph { get; set; } = EngineDefaults.GradeLaneSpeedMph;

        /// <summary>
        /// Creates an independent copy.
        /// </summary>
        /// <returns>Settings with the same values.</returns>
        public HaulGuidanceSettings Clone
            (
            )
        {
            return new HaulGuidanceSettings
            {
                ShortJointRadii = ShortJointRadii,
                ConnectorTieFraction = ConnectorTieFraction,
                ConnectorTieRadii = ConnectorTieRadii,
                SteerHeadingDeg = SteerHeadingDeg,
                MaxCorrectingLines = MaxCorrectingLines,
                LeftoverRatio = LeftoverRatio,
                MergeRouteRadii = MergeRouteRadii,
                EndFragmentSpanRadii = EndFragmentSpanRadii,
                EndFragmentGapRadii = EndFragmentGapRadii,
                NearEmptyRadii = NearEmptyRadii,
                NearEmptyBowlFraction = NearEmptyBowlFraction,
                ArcSpeedMph = ArcSpeedMph,
                GradeLaneSpeedMph = GradeLaneSpeedMph
            };
        }

        /// <summary>
        /// Copies values from another guidance settings instance.
        /// </summary>
        /// <param name="Source">Settings to copy.</param>
        public void CopyFrom
            (
            HaulGuidanceSettings Source
            )
        {
            ShortJointRadii = Source.ShortJointRadii;
            ConnectorTieFraction = Source.ConnectorTieFraction;
            ConnectorTieRadii = Source.ConnectorTieRadii;
            SteerHeadingDeg = Source.SteerHeadingDeg;
            MaxCorrectingLines = Source.MaxCorrectingLines;
            LeftoverRatio = Source.LeftoverRatio;
            MergeRouteRadii = Source.MergeRouteRadii;
            EndFragmentSpanRadii = Source.EndFragmentSpanRadii;
            EndFragmentGapRadii = Source.EndFragmentGapRadii;
            NearEmptyRadii = Source.NearEmptyRadii;
            NearEmptyBowlFraction = Source.NearEmptyBowlFraction;
            ArcSpeedMph = Source.ArcSpeedMph;
            GradeLaneSpeedMph = Source.GradeLaneSpeedMph;
        }
    }
}
