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
            };
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
        }
    }
}
