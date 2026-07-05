using System.Text.Json;

namespace AgGradeDesigner.Core.Ai.SurfaceTools
{
    /// <summary>
    /// Side-slope specification for road daylighting, expressed as H:V or percent grade.
    /// </summary>
    public readonly struct RoadSideSlopeSpec
    {
        /// <summary>Horizontal run per one metre vertical (H:V).</summary>
        public double HorizontalToVertical { get; init; }

        /// <summary>
        /// Parses optional side-slope arguments from tool JSON.
        /// </summary>
        /// <param name="Args">Tool arguments object.</param>
        /// <param name="SideSlope">Parsed side slope when present.</param>
        /// <param name="Error">Error message when parsing fails.</param>
        /// <returns>True when side slope arguments were absent or parsed successfully.</returns>
        static public bool TryParseOptional
            (
            JsonElement Args,
            out RoadSideSlopeSpec? SideSlope,
            out string? Error
            )
        {
            SideSlope = null;
            Error = null;

            bool HasHv = Args.TryGetProperty("side_slope_hv", out JsonElement HvElement);
            bool HasPercent = Args.TryGetProperty("side_slope_percent", out JsonElement PercentElement);
            if (!HasHv && !HasPercent)
                return true;

            if (HasHv && HasPercent)
            {
                Error = "Provide only one of 'side_slope_hv' or 'side_slope_percent'.";
                return false;
            }

            if (HasHv)
            {
                if (!HvElement.TryGetDouble(out double HorizontalToVertical) || HorizontalToVertical <= 0.0)
                {
                    Error = "Argument 'side_slope_hv' must be a positive number (horizontal run per 1 m vertical).";
                    return false;
                }

                SideSlope = new RoadSideSlopeSpec { HorizontalToVertical = HorizontalToVertical };
                return true;
            }

            if (!PercentElement.TryGetDouble(out double Percent) || Percent <= 0.0)
            {
                Error = "Argument 'side_slope_percent' must be a positive number.";
                return false;
            }

            SideSlope = new RoadSideSlopeSpec { HorizontalToVertical = 100.0 / Percent };
            return true;
        }

        /// <summary>
        /// Computes road elevation at a perpendicular offset outside the crown edge.
        /// </summary>
        /// <param name="CrownEdgeElevationM">Elevation at the crown edge in metres.</param>
        /// <param name="OffsetBeyondCrownM">Distance beyond the crown edge in metres.</param>
        /// <returns>Road surface elevation in metres.</returns>
        public double ElevationBeyondCrown
            (
            double CrownEdgeElevationM,
            double OffsetBeyondCrownM
            )
        {
            return CrownEdgeElevationM - OffsetBeyondCrownM / HorizontalToVertical;
        }
    }
}
