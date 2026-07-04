using System.Text.Json;

namespace AgGradeDesigner.Core.Ai.SurfaceTools
{
    /// <summary>
    /// Optional validation thresholds for <c>validate_surface_edit</c> and edit planning tools.
    /// </summary>
    public sealed class SurfaceEditConstraints
    {
        /// <summary>Maximum allowed local slope magnitude in m/m.</summary>
        public double? MaxSlopeMm { get; init; }

        /// <summary>Minimum allowed local slope magnitude in m/m.</summary>
        public double? MinSlopeMm { get; init; }

        /// <summary>Maximum cut volume in cubic metres.</summary>
        public double? MaxCutM3 { get; init; }

        /// <summary>Maximum fill volume in cubic metres.</summary>
        public double? MaxFillM3 { get; init; }

        /// <summary>Maximum net export volume in cubic metres.</summary>
        public double? MaxNetExportM3 { get; init; }

        /// <summary>Minimum proposed elevation in metres.</summary>
        public double? MinElevationM { get; init; }

        /// <summary>Maximum proposed elevation in metres.</summary>
        public double? MaxElevationM { get; init; }

        /// <summary>
        /// Parses optional constraint fields from tool JSON arguments.
        /// </summary>
        /// <param name="Args">Tool arguments object.</param>
        /// <returns>Parsed constraints, or null when no constraint object is present.</returns>
        static public SurfaceEditConstraints? TryParse
            (
            JsonElement Args
            )
        {
            if (!Args.TryGetProperty("constraints", out JsonElement ConstraintsElement))
                return null;

            if (ConstraintsElement.ValueKind != JsonValueKind.Object)
                return null;

            return new SurfaceEditConstraints
            {
                MaxSlopeMm = TryReadOptionalDouble(ConstraintsElement, "max_slope_mm"),
                MinSlopeMm = TryReadOptionalDouble(ConstraintsElement, "min_slope_mm"),
                MaxCutM3 = TryReadOptionalDouble(ConstraintsElement, "max_cut_m3"),
                MaxFillM3 = TryReadOptionalDouble(ConstraintsElement, "max_fill_m3"),
                MaxNetExportM3 = TryReadOptionalDouble(ConstraintsElement, "max_net_export_m3"),
                MinElevationM = TryReadOptionalDouble(ConstraintsElement, "min_elevation_m"),
                MaxElevationM = TryReadOptionalDouble(ConstraintsElement, "max_elevation_m")
            };
        }

        /// <summary>
        /// Reads an optional numeric JSON property when present.
        /// </summary>
        /// <param name="Args">JSON object.</param>
        /// <param name="PropertyName">Property name.</param>
        /// <returns>Parsed value, or null when omitted.</returns>
        static private double? TryReadOptionalDouble
            (
            JsonElement Args,
            string PropertyName
            )
        {
            if (!Args.TryGetProperty(PropertyName, out JsonElement Element))
                return null;

            if (!Element.TryGetDouble(out double Value))
                return null;

            return Value;
        }
    }

    /// <summary>
    /// One constraint violation returned by <c>validate_surface_edit</c>.
    /// </summary>
    public readonly struct SurfaceConstraintViolation
    {
        /// <summary>Machine-readable violation code.</summary>
        public string Code { get; init; }

        /// <summary>Human-readable violation message.</summary>
        public string Message { get; init; }

        /// <summary>Count of bins involved when applicable.</summary>
        public int BinCount { get; init; }

        /// <summary>Configured limit value when applicable.</summary>
        public double? Limit { get; init; }

        /// <summary>Maximum observed slope in m/m when applicable.</summary>
        public double? MaxObservedSlopeMm { get; init; }

        /// <summary>Minimum observed slope in m/m when applicable.</summary>
        public double? MinObservedSlopeMm { get; init; }
    }
}
