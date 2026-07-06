using System.Text.Json;

namespace AgGradeDesigner.Core.Ai.SurfaceTools
{
    /// <summary>
    /// Side of the road centerline that receives daylighting side slope.
    /// </summary>
    public enum RoadSideSlopeSide
    {
        /// <summary>Apply side slope on both sides of the centerline.</summary>
        Both,

        /// <summary>Apply side slope only on the left side of the centerline travel direction.</summary>
        Left,

        /// <summary>Apply side slope only on the right side of the centerline travel direction.</summary>
        Right
    }

    /// <summary>
    /// Side-slope specification for road daylighting, expressed as H:V or percent grade.
    /// </summary>
    public readonly struct RoadSideSlopeSpec
    {
        /// <summary>Horizontal run per one metre vertical (H:V).</summary>
        public double HorizontalToVertical { get; init; }

        /// <summary>Which side of the centerline receives the side slope.</summary>
        public RoadSideSlopeSide Side { get; init; }

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
            bool HasSide = Args.TryGetProperty("side_slope_side", out JsonElement SideElement);

            if (HasSide && !HasHv && !HasPercent)
            {
                Error = "Argument 'side_slope_side' requires 'side_slope_hv' or 'side_slope_percent'.";
                return false;
            }

            if (!HasHv && !HasPercent)
                return true;

            if (HasHv && HasPercent)
            {
                Error = "Provide only one of 'side_slope_hv' or 'side_slope_percent'.";
                return false;
            }

            RoadSideSlopeSide Side = RoadSideSlopeSide.Both;
            if (HasSide)
            {
                if (!TryParseSide(SideElement, out Side, out Error))
                    return false;
            }

            if (HasHv)
            {
                if (!HvElement.TryGetDouble(out double HorizontalToVertical) || HorizontalToVertical <= 0.0)
                {
                    Error = "Argument 'side_slope_hv' must be a positive number (horizontal run per 1 m vertical).";
                    return false;
                }

                SideSlope = new RoadSideSlopeSpec
                {
                    HorizontalToVertical = HorizontalToVertical,
                    Side = Side
                };
                return true;
            }

            if (!PercentElement.TryGetDouble(out double Percent) || Percent <= 0.0)
            {
                Error = "Argument 'side_slope_percent' must be a positive number.";
                return false;
            }

            SideSlope = new RoadSideSlopeSpec
            {
                HorizontalToVertical = 100.0 / Percent,
                Side = Side
            };
            return true;
        }

        /// <summary>
        /// Computes surface elevation at a perpendicular offset outside the flat crown or ditch bottom edge.
        /// </summary>
        /// <param name="FlatEdgeElevationM">Elevation at the flat edge in metres.</param>
        /// <param name="OffsetBeyondFlatEdgeM">Distance beyond the flat edge in metres.</param>
        /// <param name="IsCut">True for a ditch (slope rises back to grade); false for a road.</param>
        /// <returns>Target surface elevation in metres.</returns>
        public double ElevationBeyondFlatEdge
            (
            double FlatEdgeElevationM,
            double OffsetBeyondFlatEdgeM,
            bool IsCut
            )
        {
            double DeltaM = OffsetBeyondFlatEdgeM / HorizontalToVertical;
            return IsCut ? FlatEdgeElevationM + DeltaM : FlatEdgeElevationM - DeltaM;
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
            return ElevationBeyondFlatEdge(CrownEdgeElevationM, OffsetBeyondCrownM, IsCut: false);
        }

        /// <summary>
        /// Returns true when side slope daylighting applies at the signed perpendicular offset.
        /// </summary>
        /// <param name="SignedOffsetM">Signed perpendicular offset; positive is left of centerline direction.</param>
        /// <returns>True when side slope should be evaluated at this offset.</returns>
        public bool AppliesToSignedOffset
            (
            double SignedOffsetM
            )
        {
            if (Side == RoadSideSlopeSide.Both)
                return true;

            if (Math.Abs(SignedOffsetM) <= 1e-9)
                return true;

            if (Side == RoadSideSlopeSide.Left)
                return SignedOffsetM > 0.0;

            return SignedOffsetM < 0.0;
        }

        /// <summary>
        /// Parses the optional side selector for one-sided road slopes.
        /// </summary>
        /// <param name="SideElement">JSON side argument.</param>
        /// <param name="Side">Parsed side.</param>
        /// <param name="Error">Error message when parsing fails.</param>
        /// <returns>True when parsing succeeds.</returns>
        static private bool TryParseSide
            (
            JsonElement SideElement,
            out RoadSideSlopeSide Side,
            out string? Error
            )
        {
            Side = RoadSideSlopeSide.Both;
            Error = null;

            if (SideElement.ValueKind != JsonValueKind.String)
            {
                Error = "Argument 'side_slope_side' must be a string: 'left', 'right', or 'both'.";
                return false;
            }

            string? SideText = SideElement.GetString()?.Trim().ToLowerInvariant();
            switch (SideText)
            {
                case "both":
                    Side = RoadSideSlopeSide.Both;
                    return true;
                case "left":
                    Side = RoadSideSlopeSide.Left;
                    return true;
                case "right":
                    Side = RoadSideSlopeSide.Right;
                    return true;
                default:
                    Error = "Argument 'side_slope_side' must be 'left', 'right', or 'both'.";
                    return false;
            }
        }
    }
}
