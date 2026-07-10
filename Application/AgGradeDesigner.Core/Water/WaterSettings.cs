namespace AgGradeDesigner.Core.Water
{
    /// <summary>
    /// Project-owned ponding parameters edited in the Water dialog (WATER-SPEC §4.3).
    /// </summary>
    public sealed class WaterSettings
    {
        /// <summary>Design-storm rainfall depth in canonical millimetres.</summary>
        public uint PondingRainfallMm { get; set; } = WaterConstants.DEFAULT_PONDING_RAINFALL_MM;

        /// <summary>Unitless SCS curve number for ponding analysis.</summary>
        public uint PondingCurveNumber { get; set; } = WaterConstants.DEFAULT_PONDING_CURVE_NUMBER;

        /// <summary>Minimum realized pond depth shown on the overlay, in canonical millimetres.</summary>
        public uint MinimumPondDepthMm { get; set; } = WaterConstants.DEFAULT_MINIMUM_POND_DEPTH_MM;

        /// <summary>
        /// Returns a shallow copy of these settings.
        /// </summary>
        /// <returns>Independent settings instance with the same values.</returns>
        public WaterSettings Clone
            (
            )
        {
            return new WaterSettings
            {
                PondingRainfallMm = PondingRainfallMm,
                PondingCurveNumber = PondingCurveNumber,
                MinimumPondDepthMm = MinimumPondDepthMm,
            };
        }

        /// <summary>
        /// Returns true when hydrology inputs match another settings instance.
        /// </summary>
        /// <param name="Other">Settings to compare.</param>
        /// <returns>True when rainfall and curve number match.</returns>
        public bool MatchesHydrologySettings
            (
            WaterSettings Other
            )
        {
            return (PondingRainfallMm == Other.PondingRainfallMm) &&
                (PondingCurveNumber == Other.PondingCurveNumber);
        }

        /// <summary>
        /// Returns true when overlay display inputs match another settings instance.
        /// </summary>
        /// <param name="Other">Settings to compare.</param>
        /// <returns>True when minimum pond depth matches.</returns>
        public bool MatchesDisplaySettings
            (
            WaterSettings Other
            )
        {
            return MinimumPondDepthMm == Other.MinimumPondDepthMm;
        }

        /// <summary>
        /// Applies canonical default ponding values.
        /// </summary>
        public void ApplyDefaults
            (
            )
        {
            PondingRainfallMm = WaterConstants.DEFAULT_PONDING_RAINFALL_MM;
            PondingCurveNumber = WaterConstants.DEFAULT_PONDING_CURVE_NUMBER;
            MinimumPondDepthMm = WaterConstants.DEFAULT_MINIMUM_POND_DEPTH_MM;
        }
    }
}
