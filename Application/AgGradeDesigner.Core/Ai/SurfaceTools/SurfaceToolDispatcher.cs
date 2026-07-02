using System.Text.Json;

using AgGradeDesigner.Core.Models;

namespace AgGradeDesigner.Core.Ai.SurfaceTools
{
    /// <summary>
    /// Result of executing a single surface tool call.
    /// </summary>
    public readonly struct SurfaceToolResult
    {
        /// <summary>True when the tool completed successfully.</summary>
        public bool Success { get; init; }

        /// <summary>JSON payload returned to the LLM.</summary>
        public string Json { get; init; }

        /// <summary>User-visible error text when <see cref="Success"/> is false.</summary>
        public string ErrorMessage { get; init; }

        /// <summary>
        /// Creates a successful tool result with a JSON payload.
        /// </summary>
        /// <param name="Payload">Object serialized to JSON.</param>
        /// <returns>Successful result.</returns>
        static public SurfaceToolResult Ok
            (
            object Payload
            )
        {
            return new SurfaceToolResult
            {
                Success = true,
                Json = JsonSerializer.Serialize(Payload),
                ErrorMessage = string.Empty
            };
        }

        /// <summary>
        /// Creates a failed tool result with an error message.
        /// </summary>
        /// <param name="Message">Error description for the LLM and UI.</param>
        /// <returns>Failed result.</returns>
        static public SurfaceToolResult Fail
            (
            string Message
            )
        {
            return new SurfaceToolResult
            {
                Success = false,
                Json = JsonSerializer.Serialize(new { error = Message }),
                ErrorMessage = Message
            };
        }
    }

    /// <summary>
    /// Dispatches Phase 1 read-only surface tool calls against the active project grid.
    /// </summary>
    public sealed class SurfaceToolDispatcher
    {
        private readonly HashSet<string> _Phase1Tools;

        /// <summary>
        /// Creates a dispatcher with the Phase 1 read-only tool catalog registered.
        /// </summary>
        public SurfaceToolDispatcher
            (
            )
        {
            _Phase1Tools = new HashSet<string>(StringComparer.Ordinal)
            {
                "get_surface_summary",
                "get_elevation_at",
                "get_region_stats",
                "calculate_cut_fill",
                "find_balance_offset",
                "compare_surfaces"
            };
        }

        /// <summary>
        /// Returns the Phase 1 tool names exposed to the LLM.
        /// </summary>
        /// <returns>Read-only tool identifiers.</returns>
        public IReadOnlyCollection<string> GetPhase1ToolNames
            (
            )
        {
            return _Phase1Tools;
        }

        /// <summary>
        /// Executes a surface tool by name with JSON arguments against the supplied project.
        /// </summary>
        /// <param name="Project">Active project document.</param>
        /// <param name="ToolName">Surface tool identifier.</param>
        /// <param name="ArgumentsJson">JSON object arguments from the LLM.</param>
        /// <returns>Structured tool result for the chat loop.</returns>
        public SurfaceToolResult Execute
            (
            ProjectDocument Project,
            string ToolName,
            string ArgumentsJson
            )
        {
            if (!_Phase1Tools.Contains(ToolName))
                return SurfaceToolResult.Fail($"Surface tool '{ToolName}' is not available in Phase 1.");

            if (Project.DesignGrid == null || !Project.HasDesign)
                return SurfaceToolResult.Fail("No design grid is loaded for the current project.");

            DesignGrid Grid = Project.DesignGrid;
            JsonDocument Args;
            try
            {
                Args = JsonDocument.Parse(string.IsNullOrWhiteSpace(ArgumentsJson) ? "{}" : ArgumentsJson);
            }
            catch (JsonException Ex)
            {
                return SurfaceToolResult.Fail($"Invalid tool arguments JSON: {Ex.Message}");
            }

            using (Args)
            {
                return ToolName switch
                {
                    "get_surface_summary" => ExecuteGetSurfaceSummary(Grid),
                    "get_elevation_at" => ExecuteGetElevationAt(Grid, Args.RootElement),
                    "get_region_stats" => ExecuteGetRegionStats(Grid),
                    "calculate_cut_fill" => ExecuteCalculateCutFill(Grid, Args.RootElement),
                    "find_balance_offset" => ExecuteFindBalanceOffset(Grid, Args.RootElement),
                    "compare_surfaces" => ExecuteCompareSurfaces(Grid, Args.RootElement),
                    _ => SurfaceToolResult.Fail($"Unknown surface tool '{ToolName}'.")
                };
            }
        }

        /// <summary>
        /// Executes <c>get_surface_summary</c> for the proposed surface.
        /// </summary>
        /// <param name="Grid">Design grid.</param>
        /// <returns>Tool result JSON.</returns>
        private SurfaceToolResult ExecuteGetSurfaceSummary
            (
            DesignGrid Grid
            )
        {
            SurfaceSummary Summary = SurfaceAnalysis.BuildSurfaceSummary(Grid);
            return SurfaceToolResult.Ok(new
            {
                min_easting_m = Summary.MinEastingM,
                max_easting_m = Summary.MaxEastingM,
                min_northing_m = Summary.MinNorthingM,
                max_northing_m = Summary.MaxNorthingM,
                cell_size_m = Summary.CellSizeM,
                min_elevation_m = Summary.MinElevationM,
                max_elevation_m = Summary.MaxElevationM,
                min_slope_mm = Summary.MinSlopeMm,
                max_slope_mm = Summary.MaxSlopeMm,
                cut_fill_balance_m3 = Summary.CutFillBalanceM3,
                low_point = new
                {
                    x = Summary.LowPointEastingM,
                    y = Summary.LowPointNorthingM,
                    elevation_m = Summary.LowPointElevationM
                },
                high_point = new
                {
                    x = Summary.HighPointEastingM,
                    y = Summary.HighPointNorthingM,
                    elevation_m = Summary.HighPointElevationM
                },
                drainage_heading_deg = Summary.DrainageHeadingDeg
            });
        }

        /// <summary>
        /// Executes <c>get_elevation_at</c> for a UTM point on the proposed surface.
        /// </summary>
        /// <param name="Grid">Design grid.</param>
        /// <param name="Args">Tool arguments containing x and y.</param>
        /// <returns>Tool result JSON.</returns>
        private SurfaceToolResult ExecuteGetElevationAt
            (
            DesignGrid Grid,
            JsonElement Args
            )
        {
            if (!TryReadRequiredDouble(Args, "x", out double X))
                return SurfaceToolResult.Fail("Missing required argument 'x'.");
            if (!TryReadRequiredDouble(Args, "y", out double Y))
                return SurfaceToolResult.Fail("Missing required argument 'y'.");

            if (!SurfaceGridSampler.TrySampleElevation(Grid, SurfaceLayerKind.Proposed, X, Y, out double ElevationM))
                return SurfaceToolResult.Fail("Point lies outside the valid design grid.");

            return SurfaceToolResult.Ok(new
            {
                x = X,
                y = Y,
                elevation_m = ElevationM
            });
        }

        /// <summary>
        /// Executes <c>get_region_stats</c> for the entire field on the proposed surface.
        /// </summary>
        /// <param name="Grid">Design grid.</param>
        /// <returns>Tool result JSON.</returns>
        private SurfaceToolResult ExecuteGetRegionStats
            (
            DesignGrid Grid
            )
        {
            SurfaceRegionStats Stats = SurfaceAnalysis.BuildRegionStats(Grid, SurfaceLayerKind.Proposed);
            return SurfaceToolResult.Ok(new
            {
                min_elevation_m = Stats.MinElevationM,
                max_elevation_m = Stats.MaxElevationM,
                mean_elevation_m = Stats.MeanElevationM,
                min_slope_mm = Stats.MinSlopeMm,
                max_slope_mm = Stats.MaxSlopeMm,
                mean_slope_mm = Stats.MeanSlopeMm,
                volume_above_reference_m3 = Stats.VolumeAboveReferenceM3,
                volume_below_reference_m3 = Stats.VolumeBelowReferenceM3,
                drainage_heading_deg = Stats.DrainageHeadingDeg,
                valid_bin_count = Stats.ValidBinCount
            });
        }

        /// <summary>
        /// Executes <c>calculate_cut_fill</c> between two surface layers.
        /// </summary>
        /// <param name="Grid">Design grid.</param>
        /// <param name="Args">Tool arguments with optional layer names.</param>
        /// <returns>Tool result JSON.</returns>
        private SurfaceToolResult ExecuteCalculateCutFill
            (
            DesignGrid Grid,
            JsonElement Args
            )
        {
            if (!TryParseLayer(Args, "existing_surface", SurfaceLayerKind.Existing, out SurfaceLayerKind FromLayer, out string? FromError))
                return SurfaceToolResult.Fail(FromError!);
            if (!TryParseLayer(Args, "design_surface", SurfaceLayerKind.Proposed, out SurfaceLayerKind ToLayer, out string? ToError))
                return SurfaceToolResult.Fail(ToError!);

            SurfaceCutFillVolumes Volumes = SurfaceVolumeCalculator.CalculateCutFill(Grid, FromLayer, ToLayer);
            return SurfaceToolResult.Ok(new
            {
                existing_surface = FromLayer.ToString().ToLowerInvariant(),
                design_surface = ToLayer.ToString().ToLowerInvariant(),
                cut_m3 = Volumes.CutM3,
                fill_m3 = Volumes.FillM3,
                net_m3 = Volumes.NetM3,
                cut_fill_ratio = Volumes.CutFillRatio
            });
        }

        /// <summary>
        /// Executes <c>find_balance_offset</c> for a target cut/fill ratio.
        /// </summary>
        /// <param name="Grid">Design grid.</param>
        /// <param name="Args">Tool arguments containing target_cut_fill_ratio.</param>
        /// <returns>Tool result JSON.</returns>
        private SurfaceToolResult ExecuteFindBalanceOffset
            (
            DesignGrid Grid,
            JsonElement Args
            )
        {
            double TargetRatio = 1.2;
            if (Args.TryGetProperty("target_cut_fill_ratio", out JsonElement RatioElement))
            {
                if (!RatioElement.TryGetDouble(out TargetRatio) || TargetRatio <= 0.0)
                    return SurfaceToolResult.Fail("Argument 'target_cut_fill_ratio' must be a positive number.");
            }

            double OffsetM = SurfaceVolumeCalculator.FindBalanceOffset(Grid, TargetRatio);
            return SurfaceToolResult.Ok(new
            {
                target_cut_fill_ratio = TargetRatio,
                balance_offset_m = OffsetM
            });
        }

        /// <summary>
        /// Executes <c>compare_surfaces</c> between two surface layers.
        /// </summary>
        /// <param name="Grid">Design grid.</param>
        /// <param name="Args">Tool arguments with before and after layer names.</param>
        /// <returns>Tool result JSON.</returns>
        private SurfaceToolResult ExecuteCompareSurfaces
            (
            DesignGrid Grid,
            JsonElement Args
            )
        {
            if (!TryParseLayer(Args, "before", SurfaceLayerKind.Existing, out SurfaceLayerKind BeforeLayer, out string? BeforeError))
                return SurfaceToolResult.Fail(BeforeError!);
            if (!TryParseLayer(Args, "after", SurfaceLayerKind.Proposed, out SurfaceLayerKind AfterLayer, out string? AfterError))
                return SurfaceToolResult.Fail(AfterError!);

            SurfaceCompareStats Stats = SurfaceVolumeCalculator.CompareSurfaces(Grid, BeforeLayer, AfterLayer);
            return SurfaceToolResult.Ok(new
            {
                before = BeforeLayer.ToString().ToLowerInvariant(),
                after = AfterLayer.ToString().ToLowerInvariant(),
                min_delta_m = Stats.MinDeltaM,
                max_delta_m = Stats.MaxDeltaM,
                mean_delta_m = Stats.MeanDeltaM,
                raised_bin_count = Stats.RaisedBinCount,
                lowered_bin_count = Stats.LoweredBinCount,
                unchanged_bin_count = Stats.UnchangedBinCount
            });
        }

        /// <summary>
        /// Parses a surface layer identifier from tool arguments.
        /// </summary>
        /// <param name="Args">JSON arguments object.</param>
        /// <param name="PropertyName">Property name to read.</param>
        /// <param name="Default">Default layer when the property is omitted.</param>
        /// <param name="Layer">Parsed layer kind.</param>
        /// <param name="Error">Error message when parsing fails.</param>
        /// <returns>True when parsing succeeds.</returns>
        static private bool TryParseLayer
            (
            JsonElement Args,
            string PropertyName,
            SurfaceLayerKind Default,
            out SurfaceLayerKind Layer,
            out string? Error
            )
        {
            Layer = Default;
            Error = null;
            if (!Args.TryGetProperty(PropertyName, out JsonElement Value))
                return true;

            string? Text = Value.GetString();
            if (string.IsNullOrWhiteSpace(Text))
            {
                Error = $"Argument '{PropertyName}' must be 'existing' or 'proposed'.";
                return false;
            }

            if (string.Equals(Text, "existing", StringComparison.OrdinalIgnoreCase))
            {
                Layer = SurfaceLayerKind.Existing;
                return true;
            }

            if (string.Equals(Text, "proposed", StringComparison.OrdinalIgnoreCase))
            {
                Layer = SurfaceLayerKind.Proposed;
                return true;
            }

            Error = $"Argument '{PropertyName}' must be 'existing' or 'proposed'.";
            return false;
        }

        /// <summary>
        /// Reads a required numeric JSON property.
        /// </summary>
        /// <param name="Args">JSON arguments object.</param>
        /// <param name="PropertyName">Property name.</param>
        /// <param name="Value">Parsed value.</param>
        /// <returns>True when the property exists and is numeric.</returns>
        static private bool TryReadRequiredDouble
            (
            JsonElement Args,
            string PropertyName,
            out double Value
            )
        {
            Value = 0.0;
            if (!Args.TryGetProperty(PropertyName, out JsonElement Element))
                return false;

            return Element.TryGetDouble(out Value);
        }
    }
}
