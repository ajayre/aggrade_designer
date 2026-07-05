using System.Text.Json;

using AgGradeDesigner.Core.Models;

using NetTopologySuite.Geometries;

namespace AgGradeDesigner.Core.Ai.SurfaceTools
{
    /// <summary>
    /// Parses shared road tool arguments from JSON.
    /// </summary>
    static public class RoadToolArgumentParser
    {
        /// <summary>
        /// Parses road build parameters from tool JSON arguments.
        /// </summary>
        /// <param name="Grid">Design grid used when resolving field edges.</param>
        /// <param name="Args">Tool arguments object.</param>
        /// <param name="Parameters">Parsed build parameters.</param>
        /// <param name="Error">Error message when parsing fails.</param>
        /// <returns>True when parsing succeeds.</returns>
        static public bool TryParseBuildParameters
            (
            DesignGrid Grid,
            JsonElement Args,
            out RoadBuildParameters Parameters,
            out string? Error
            )
        {
            return TryParseForBuildRoad(
                Grid,
                Args,
                RoadRegistry: null,
                out Parameters,
                out _,
                out _,
                out Error);
        }

        /// <summary>
        /// Parses <c>build_road</c> arguments, optionally resolving a registered road corridor.
        /// </summary>
        /// <param name="Grid">Design grid used when resolving field edges.</param>
        /// <param name="Args">Tool or edit-plan step arguments.</param>
        /// <param name="RoadRegistry">Session road registry used for road_id lookup and id allocation.</param>
        /// <param name="Parameters">Parsed build parameters.</param>
        /// <param name="RoadId">Road identifier used for the build.</param>
        /// <param name="Name">Optional road label.</param>
        /// <param name="Error">Error message when parsing fails.</param>
        /// <returns>True when parsing succeeds.</returns>
        static public bool TryParseForBuildRoad
            (
            DesignGrid Grid,
            JsonElement Args,
            RoadSessionRegistry? RoadRegistry,
            out RoadBuildParameters Parameters,
            out string RoadId,
            out string? Name,
            out string? Error
            )
        {
            Parameters = default;
            RoadId = string.Empty;
            Name = null;
            Error = null;

            if (Args.TryGetProperty("name", out JsonElement NameElement))
                Name = NameElement.GetString();

            LineString Centerline;
            double CrownWidthM;

            if (Args.TryGetProperty("road_id", out JsonElement RoadIdElement))
            {
                RoadId = RoadIdElement.GetString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(RoadId))
                {
                    Error = "Argument 'road_id' must be a non-empty string.";
                    return false;
                }

                if (RoadRegistry == null || !RoadRegistry.TryGet(RoadId, out RoadDefinition Definition))
                {
                    Error = $"Road '{RoadId}' was not found. Call create_road first or supply a new centerline.";
                    return false;
                }

                Centerline = Definition.Centerline;
                CrownWidthM = Definition.CrownWidthM;
            }
            else
            {
                if (!SurfacePolylineHelper.TryParseCenterline(Args, out LineString? ParsedCenterline, out Error))
                    return false;

                Centerline = ParsedCenterline!;
                RoadId = RoadRegistry?.AllocateRoadId() ?? "rd_1";

                if (!Args.TryGetProperty("crown_width_m", out JsonElement WidthElement) ||
                    !WidthElement.TryGetDouble(out CrownWidthM) ||
                    CrownWidthM <= 0.0)
                {
                    Error = "Missing or invalid required argument 'crown_width_m'; must be a positive number in metres.";
                    return false;
                }
            }

            if (!TryParseRaiseAboveProposed(Args, out double RaiseAboveProposedM, out Error))
                return false;

            if (!RoadSideSlopeSpec.TryParseOptional(Args, out RoadSideSlopeSpec? SideSlope, out Error))
                return false;

            double LongitudinalGradeMm = 0.0;
            if (Args.TryGetProperty("longitudinal_grade_mm", out JsonElement GradeElement))
            {
                if (!GradeElement.TryGetDouble(out LongitudinalGradeMm))
                {
                    Error = "Argument 'longitudinal_grade_mm' must be numeric when supplied.";
                    return false;
                }
            }

            double CrownCrossfallMm = 0.0;
            if (Args.TryGetProperty("crown_crossfall_mm", out JsonElement CrossfallElement))
            {
                if (!CrossfallElement.TryGetDouble(out CrownCrossfallMm))
                {
                    Error = "Argument 'crown_crossfall_mm' must be numeric when supplied.";
                    return false;
                }
            }

            Parameters = new RoadBuildParameters
            {
                Centerline = Centerline,
                CrownWidthM = CrownWidthM,
                RaiseAboveProposedM = RaiseAboveProposedM,
                SideSlope = SideSlope,
                LongitudinalGradeMm = LongitudinalGradeMm,
                CrownCrossfallMm = CrownCrossfallMm
            };
            return true;
        }

        /// <summary>
        /// Parses the required raise-above-proposed argument, accepting the legacy existing name.
        /// </summary>
        /// <param name="Args">Tool or edit-plan step arguments.</param>
        /// <param name="RaiseAboveProposedM">Parsed raise height in metres.</param>
        /// <param name="Error">Error message when parsing fails.</param>
        /// <returns>True when parsing succeeds.</returns>
        static private bool TryParseRaiseAboveProposed
            (
            JsonElement Args,
            out double RaiseAboveProposedM,
            out string? Error
            )
        {
            RaiseAboveProposedM = 0.0;
            Error = null;

            if (Args.TryGetProperty("raise_above_proposed_m", out JsonElement ProposedElement) &&
                ProposedElement.TryGetDouble(out RaiseAboveProposedM))
            {
                if (RaiseAboveProposedM <= 0.0)
                {
                    Error = "Argument 'raise_above_proposed_m' must be a positive number in metres.";
                    return false;
                }

                return true;
            }

            if (Args.TryGetProperty("raise_above_existing_m", out JsonElement LegacyElement) &&
                LegacyElement.TryGetDouble(out RaiseAboveProposedM))
            {
                if (RaiseAboveProposedM <= 0.0)
                {
                    Error = "Argument 'raise_above_existing_m' must be a positive number in metres.";
                    return false;
                }

                return true;
            }

            Error = "Missing required argument 'raise_above_proposed_m'; must be a positive number in metres.";
            return false;
        }
    }
}
