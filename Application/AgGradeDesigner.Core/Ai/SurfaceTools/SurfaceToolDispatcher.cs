using System.Text.Json;

using AgGradeDesigner.Core;
using AgGradeDesigner.Core.Ai;
using AgGradeDesigner.Core.MapTools;
using AgGradeDesigner.Core.Models;

using NetTopologySuite.Geometries;

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
    /// Dispatches Phase 1 read-only and Phase 2 mutating surface tool calls against the active project grid.
    /// </summary>
    public sealed class SurfaceToolDispatcher
    {
        /// <summary>Registered Phase 1 read-only tool names.</summary>
        private readonly HashSet<string> _Phase1Tools;

        /// <summary>Registered Phase 2 edit-pipeline tool names.</summary>
        private readonly HashSet<string> _Phase2Tools;

        /// <summary>Active delivery phase controlling tool availability.</summary>
        private readonly AiDeliveryPhase _DeliveryPhase;

        /// <summary>Provides session tool data for <c>get_tool_data</c>.</summary>
        private readonly Func<ToolDataSession>? _ToolDataProvider;

        /// <summary>Preview/commit session for Phase 2 mutating tools.</summary>
        private readonly SurfaceEditSession _EditSession;

        /// <summary>Optional callback invoked when preview state changes.</summary>
        private readonly Action<SurfaceEditLifecycleKind>? _LifecycleCallback;

        /// <summary>
        /// Creates a dispatcher with the requested delivery phase tool catalog registered.
        /// </summary>
        /// <param name="DeliveryPhase">Tool delivery phase.</param>
        /// <param name="ToolDataProvider">Optional provider for session map-editing tool data.</param>
        /// <param name="EditSession">Optional shared edit session; a new session is created when omitted.</param>
        /// <param name="LifecycleCallback">Optional callback when preview state changes.</param>
        public SurfaceToolDispatcher
            (
            AiDeliveryPhase DeliveryPhase = AiDeliveryPhase.Phase2,
            Func<ToolDataSession>? ToolDataProvider = null,
            SurfaceEditSession? EditSession = null,
            Action<SurfaceEditLifecycleKind>? LifecycleCallback = null
            )
        {
            _DeliveryPhase = DeliveryPhase;
            _ToolDataProvider = ToolDataProvider;
            _EditSession = EditSession ?? new SurfaceEditSession();
            _LifecycleCallback = LifecycleCallback;
            _Phase1Tools = new HashSet<string>(StringComparer.Ordinal)
            {
                "get_surface_summary",
                "get_elevation_at",
                "get_region_stats",
                "calculate_cut_fill",
                "find_balance_offset",
                "compare_surfaces",
                "get_tool_data"
            };
            _Phase2Tools = new HashSet<string>(StringComparer.Ordinal)
            {
                "offset_surface",
                "rebalance_surface",
                "apply_plane_grade",
                "calculate_edit_delta",
                "validate_surface_edit",
                "commit_surface_edit",
                "discard_surface_edit",
                "preview_edit",
                "apply_edit_plan"
            };
        }

        /// <summary>
        /// Active preview/edit session used by Phase 2 tools.
        /// </summary>
        public SurfaceEditSession EditSession => _EditSession;

        /// <summary>
        /// Returns tool names exposed to the LLM for the active delivery phase.
        /// </summary>
        /// <returns>Registered tool identifiers.</returns>
        public IReadOnlyCollection<string> GetRegisteredToolNames
            (
            )
        {
            List<string> Names = new List<string>(_Phase1Tools);
            if (_DeliveryPhase >= AiDeliveryPhase.Phase2)
                Names.AddRange(_Phase2Tools);

            return Names;
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
            if (!_Phase1Tools.Contains(ToolName) && !_Phase2Tools.Contains(ToolName))
                return SurfaceToolResult.Fail($"Surface tool '{ToolName}' is not recognized.");

            if (_Phase2Tools.Contains(ToolName) && _DeliveryPhase < AiDeliveryPhase.Phase2)
                return SurfaceToolResult.Fail($"Surface tool '{ToolName}' is not available in Phase 1.");

            if (ToolName == "get_tool_data")
                return ExecuteGetToolData();

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
                    "offset_surface" => ExecuteOffsetSurface(Project, Args.RootElement),
                    "rebalance_surface" => ExecuteRebalanceSurface(Project, Args.RootElement),
                    "apply_plane_grade" => ExecuteApplyPlaneGrade(Project, Args.RootElement),
                    "calculate_edit_delta" => ExecuteCalculateEditDelta(Project, Args.RootElement),
                    "validate_surface_edit" => ExecuteValidateSurfaceEdit(Project, Args.RootElement),
                    "commit_surface_edit" => ExecuteCommitSurfaceEdit(Project, Args.RootElement),
                    "discard_surface_edit" => ExecuteDiscardSurfaceEdit(Project),
                    "preview_edit" => ExecutePreviewEdit(Project, Args.RootElement),
                    "apply_edit_plan" => ExecuteApplyEditPlan(Project, Args.RootElement),
                    _ => SurfaceToolResult.Fail($"Unknown surface tool '{ToolName}'.")
                };
            }
        }

        /// <summary>
        /// Executes <c>get_tool_data</c> for the current session map-editing overlay.
        /// </summary>
        /// <returns>Tool result JSON conforming to AI Features §9.10.</returns>
        private SurfaceToolResult ExecuteGetToolData
            (
            )
        {
            ToolDataSession Session = _ToolDataProvider?.Invoke() ?? new ToolDataSession();
            return SurfaceToolResult.Ok(Session.BuildGetToolDataPayload());
        }

        /// <summary>
        /// Executes <c>offset_surface</c> on the preview grid.
        /// </summary>
        /// <param name="Project">Active project document.</param>
        /// <param name="Args">Tool arguments containing delta_z.</param>
        /// <returns>Preview envelope result.</returns>
        private SurfaceToolResult ExecuteOffsetSurface
            (
            ProjectDocument Project,
            JsonElement Args
            )
        {
            if (!Args.TryGetProperty("delta_z", out JsonElement DeltaElement) || !DeltaElement.TryGetDouble(out double DeltaZ))
                return SurfaceToolResult.Fail("Missing required argument 'delta_z'.");

            if (!TryParseOptionalPolygon(Args, out Polygon? Polygon, out string? PolygonError))
                return SurfaceToolResult.Fail(PolygonError!);

            _EditSession.BeginPreviewFromProject(Project, "offset_surface");
            DesignGrid PreviewGrid = _EditSession.RequirePreviewGrid();
            SurfaceMutationResult Mutation = SurfaceMutators.OffsetSurface(PreviewGrid, DeltaZ, Polygon);
            _EditSession.ApplyPreviewToLiveGrid(Project.DesignGrid!);
            NotifyLifecycle(SurfaceEditLifecycleKind.PreviewUpdated);
            return SurfaceToolResult.Ok(SurfaceEditResultBuilder.BuildPreviewEnvelope("offset_surface", PreviewGrid, Mutation));
        }

        /// <summary>
        /// Executes <c>rebalance_surface</c> on the preview grid.
        /// </summary>
        /// <param name="Project">Active project document.</param>
        /// <param name="Args">Tool arguments containing optional target_cut_fill_ratio.</param>
        /// <returns>Preview envelope result.</returns>
        private SurfaceToolResult ExecuteRebalanceSurface
            (
            ProjectDocument Project,
            JsonElement Args
            )
        {
            double TargetRatio = FieldConstants.CUT_FILL_RATIO;
            if (Args.TryGetProperty("target_cut_fill_ratio", out JsonElement RatioElement))
            {
                if (!RatioElement.TryGetDouble(out TargetRatio) || TargetRatio <= 0.0)
                    return SurfaceToolResult.Fail("Argument 'target_cut_fill_ratio' must be a positive number.");
            }

            if (!TryParseOptionalPolygon(Args, out Polygon? Polygon, out string? PolygonError))
                return SurfaceToolResult.Fail(PolygonError!);

            _EditSession.BeginPreviewFromProject(Project, "rebalance_surface");
            DesignGrid PreviewGrid = _EditSession.RequirePreviewGrid();
            SurfaceMutationResult Mutation = SurfaceMutators.RebalanceSurface(PreviewGrid, TargetRatio, Polygon);
            _EditSession.ApplyPreviewToLiveGrid(Project.DesignGrid!);
            NotifyLifecycle(SurfaceEditLifecycleKind.PreviewUpdated);
            return SurfaceToolResult.Ok(SurfaceEditResultBuilder.BuildPreviewEnvelope("rebalance_surface", PreviewGrid, Mutation));
        }

        /// <summary>
        /// Executes <c>apply_plane_grade</c> on the preview grid.
        /// </summary>
        /// <param name="Project">Active project document.</param>
        /// <param name="Args">Tool arguments containing origin, slopes, and optional polygon.</param>
        /// <returns>Preview envelope result.</returns>
        private SurfaceToolResult ExecuteApplyPlaneGrade
            (
            ProjectDocument Project,
            JsonElement Args
            )
        {
            if (!Args.TryGetProperty("origin", out JsonElement OriginElement))
                return SurfaceToolResult.Fail("Missing required argument 'origin'.");

            if (!SurfacePolygonHelper.TryReadPointXyz(
                    OriginElement,
                    out double OriginX,
                    out double OriginY,
                    out double OriginZ,
                    out bool HasOriginZ,
                    out string? OriginError))
            {
                return SurfaceToolResult.Fail(OriginError!);
            }

            if (!HasOriginZ)
                return SurfaceToolResult.Fail("Argument 'origin' requires elevation via 'z' or 'elevation_m'.");

            if (!Args.TryGetProperty("slope_x", out JsonElement SlopeXElement) || !SlopeXElement.TryGetDouble(out double SlopeX))
                return SurfaceToolResult.Fail("Missing required argument 'slope_x'.");

            if (!Args.TryGetProperty("slope_y", out JsonElement SlopeYElement) || !SlopeYElement.TryGetDouble(out double SlopeY))
                return SurfaceToolResult.Fail("Missing required argument 'slope_y'.");

            if (!TryParseOptionalPolygon(Args, out Polygon? Polygon, out string? PolygonError))
                return SurfaceToolResult.Fail(PolygonError!);

            _EditSession.BeginPreviewFromProject(Project, "apply_plane_grade");
            DesignGrid PreviewGrid = _EditSession.RequirePreviewGrid();
            SurfaceMutationResult Mutation = SurfaceMutators.ApplyPlaneGrade(
                PreviewGrid,
                OriginX,
                OriginY,
                OriginZ,
                SlopeX,
                SlopeY,
                Polygon);
            _EditSession.ApplyPreviewToLiveGrid(Project.DesignGrid!);
            NotifyLifecycle(SurfaceEditLifecycleKind.PreviewUpdated);
            return SurfaceToolResult.Ok(SurfaceEditResultBuilder.BuildPreviewEnvelope("apply_plane_grade", PreviewGrid, Mutation));
        }

        /// <summary>
        /// Executes <c>calculate_edit_delta</c> between committed and preview surfaces.
        /// </summary>
        /// <param name="Project">Active project document.</param>
        /// <param name="Args">Tool arguments containing before and after surface layers.</param>
        /// <returns>Volume delta JSON.</returns>
        private SurfaceToolResult ExecuteCalculateEditDelta
            (
            ProjectDocument Project,
            JsonElement Args
            )
        {
            if (!TryParseLayer(Args, "before_surface", SurfaceLayerKind.Existing, out SurfaceLayerKind BeforeLayer, out string? BeforeError))
                return SurfaceToolResult.Fail(BeforeError!);
            if (!TryParseLayer(Args, "after_surface", SurfaceLayerKind.Proposed, out SurfaceLayerKind AfterLayer, out string? AfterError))
                return SurfaceToolResult.Fail(AfterError!);

            DesignGrid BeforeGrid = ResolveLayerGrid(Project, BeforeLayer, usePreviewForProposed: false);
            DesignGrid AfterGrid = ResolveLayerGrid(Project, AfterLayer, usePreviewForProposed: true);
            SurfaceCutFillVolumes Volumes = SurfaceVolumeCalculator.CalculateCutFillBetweenGrids(
                BeforeGrid,
                BeforeLayer,
                AfterGrid,
                AfterLayer);

            return SurfaceToolResult.Ok(new
            {
                before_surface = BeforeLayer.ToString().ToLowerInvariant(),
                after_surface = AfterLayer.ToString().ToLowerInvariant(),
                cut_m3 = Volumes.CutM3,
                fill_m3 = Volumes.FillM3,
                net_m3 = Volumes.NetM3,
                cut_fill_ratio = Volumes.CutFillRatio
            });
        }

        /// <summary>
        /// Executes <c>validate_surface_edit</c> against committed or preview proposed surfaces.
        /// </summary>
        /// <param name="Project">Active project document.</param>
        /// <param name="Args">Tool arguments containing optional surface and constraints.</param>
        /// <returns>Validation result JSON.</returns>
        private SurfaceToolResult ExecuteValidateSurfaceEdit
            (
            ProjectDocument Project,
            JsonElement Args
            )
        {
            if (!TryParseLayer(Args, "surface", SurfaceLayerKind.Proposed, out SurfaceLayerKind Layer, out string? LayerError))
                return SurfaceToolResult.Fail(LayerError!);

            SurfaceEditConstraints? Constraints = SurfaceEditConstraints.TryParse(Args);
            DesignGrid Grid = ResolveLayerGrid(Project, Layer, usePreviewForProposed: true);
            SurfaceValidationResult Result = SurfaceEditValidator.ValidateSurfaceEdit(Grid, Layer, Constraints);
            return SurfaceToolResult.Ok(SurfaceEditResultBuilder.BuildValidationPayload(Result));
        }

        /// <summary>
        /// Executes <c>commit_surface_edit</c> for the active preview.
        /// </summary>
        /// <param name="Project">Active project document.</param>
        /// <param name="Args">Tool arguments containing optional description.</param>
        /// <returns>Commit envelope result.</returns>
        private SurfaceToolResult ExecuteCommitSurfaceEdit
            (
            ProjectDocument Project,
            JsonElement Args
            )
        {
            if (!_EditSession.PreviewActive)
                return SurfaceToolResult.Fail("No preview active");

            string Description = string.Empty;
            if (Args.TryGetProperty("description", out JsonElement DescriptionElement))
                Description = DescriptionElement.GetString() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(Description))
                Description = _EditSession.ResolveCommitDescription();

            SurfaceCutFillVolumes Volumes = _EditSession.Commit(Project, Description);
            NotifyLifecycle(SurfaceEditLifecycleKind.Committed);
            return SurfaceToolResult.Ok(SurfaceEditResultBuilder.BuildCommitEnvelope(Description, Volumes));
        }

        /// <summary>
        /// Discards the active preview and restores the committed proposed surface on the live grid.
        /// </summary>
        /// <param name="Project">Active project document.</param>
        /// <returns>Discard result for the chat loop and UI refresh.</returns>
        public SurfaceToolResult DiscardPreview
            (
            ProjectDocument Project
            )
        {
            return ExecuteDiscardSurfaceEdit(Project);
        }

        /// <summary>
        /// Executes <c>discard_surface_edit</c> for the active preview.
        /// </summary>
        /// <param name="Project">Active project document.</param>
        /// <returns>Discard envelope result.</returns>
        private SurfaceToolResult ExecuteDiscardSurfaceEdit
            (
            ProjectDocument Project
            )
        {
            if (Project.DesignGrid == null || !Project.HasDesign)
                return SurfaceToolResult.Fail("No design grid is loaded for the current project.");

            if (!_EditSession.PreviewActive)
            {
                return SurfaceToolResult.Ok(new
                {
                    status = "discarded",
                    preview_active = false,
                    restored = false
                });
            }

            _EditSession.Discard(Project);
            NotifyLifecycle(SurfaceEditLifecycleKind.Discarded);
            return SurfaceToolResult.Ok(new
            {
                status = "discarded",
                preview_active = false,
                restored = true
            });
        }

        /// <summary>
        /// Executes <c>preview_edit</c> for an edit plan without commit.
        /// </summary>
        /// <param name="Project">Active project document.</param>
        /// <param name="Args">Tool arguments containing edit_plan.</param>
        /// <returns>Preview envelope result.</returns>
        private SurfaceToolResult ExecutePreviewEdit
            (
            ProjectDocument Project,
            JsonElement Args
            )
        {
            return ExecuteEditPlan(Project, Args, "preview_edit");
        }

        /// <summary>
        /// Executes <c>apply_edit_plan</c> with the same behaviour as preview_edit.
        /// </summary>
        /// <param name="Project">Active project document.</param>
        /// <param name="Args">Tool arguments containing edit_plan.</param>
        /// <returns>Preview envelope result.</returns>
        private SurfaceToolResult ExecuteApplyEditPlan
            (
            ProjectDocument Project,
            JsonElement Args
            )
        {
            return ExecuteEditPlan(Project, Args, "apply_edit_plan");
        }

        /// <summary>
        /// Executes an edit plan against the preview grid.
        /// </summary>
        /// <param name="Project">Active project document.</param>
        /// <param name="Args">Tool arguments containing edit_plan.</param>
        /// <param name="Operation">Tool name producing the preview envelope.</param>
        /// <returns>Preview envelope result.</returns>
        private SurfaceToolResult ExecuteEditPlan
            (
            ProjectDocument Project,
            JsonElement Args,
            string Operation
            )
        {
            if (!Args.TryGetProperty("edit_plan", out JsonElement PlanElement) || PlanElement.ValueKind != JsonValueKind.Object)
                return SurfaceToolResult.Fail("Missing required argument 'edit_plan'.");

            if (!PlanElement.TryGetProperty("steps", out JsonElement StepsElement) || StepsElement.ValueKind != JsonValueKind.Array)
                return SurfaceToolResult.Fail("Argument 'edit_plan.steps' must be a non-empty array.");

            if (StepsElement.GetArrayLength() == 0)
                return SurfaceToolResult.Fail("Argument 'edit_plan.steps' must contain at least one step.");

            string Description = string.Empty;
            if (PlanElement.TryGetProperty("description", out JsonElement DescriptionElement))
                Description = DescriptionElement.GetString() ?? string.Empty;

            List<JsonElement> Steps = StepsElement.EnumerateArray().ToList();
            _EditSession.BeginPreviewFromProject(Project, Operation);
            DesignGrid PreviewGrid = _EditSession.RequirePreviewGrid();
            if (!EditPlanExecutor.TryExecuteSteps(PreviewGrid, Steps, out List<object> StepResults, out string? StepError))
            {
                _EditSession.Discard(Project);
                NotifyLifecycle(SurfaceEditLifecycleKind.Discarded);
                return SurfaceToolResult.Fail(StepError!);
            }

            _EditSession.ApplyPreviewToLiveGrid(Project.DesignGrid!);
            NotifyLifecycle(SurfaceEditLifecycleKind.PreviewUpdated);
            return SurfaceToolResult.Ok(SurfaceEditResultBuilder.BuildEditPlanPreviewEnvelope(
                Description,
                PreviewGrid,
                StepResults));
        }

        /// <summary>
        /// Resolves the grid used to read a surface layer, optionally using preview proposed values.
        /// </summary>
        /// <param name="Project">Active project document.</param>
        /// <param name="Layer">Requested surface layer.</param>
        /// <param name="usePreviewForProposed">True when proposed should read from preview state.</param>
        /// <returns>Grid containing the requested layer values.</returns>
        private DesignGrid ResolveLayerGrid
            (
            ProjectDocument Project,
            SurfaceLayerKind Layer,
            bool usePreviewForProposed
            )
        {
            DesignGrid LiveGrid = Project.DesignGrid!;
            if (Layer == SurfaceLayerKind.Proposed &&
                usePreviewForProposed &&
                _EditSession.PreviewActive)
            {
                return _EditSession.RequirePreviewGrid();
            }

            if (Layer == SurfaceLayerKind.Proposed && _EditSession.PreviewActive)
                return _EditSession.BuildCommittedProposedGrid(LiveGrid);

            return LiveGrid;
        }

        /// <summary>
        /// Parses an optional polygon argument from tool JSON.
        /// </summary>
        /// <param name="Args">Tool arguments object.</param>
        /// <param name="Polygon">Parsed polygon when present.</param>
        /// <param name="Error">Error message when parsing fails.</param>
        /// <returns>True when parsing succeeds or the property is omitted.</returns>
        static private bool TryParseOptionalPolygon
            (
            JsonElement Args,
            out Polygon? Polygon,
            out string? Error
            )
        {
            Polygon = null;
            Error = null;
            if (!Args.TryGetProperty("polygon", out JsonElement PolygonElement))
                return true;

            return SurfacePolygonHelper.TryParsePolygon(PolygonElement, out Polygon, out Error);
        }

        /// <summary>
        /// Notifies listeners that preview lifecycle state changed.
        /// </summary>
        /// <param name="Kind">Lifecycle event kind.</param>
        private void NotifyLifecycle
            (
            SurfaceEditLifecycleKind Kind
            )
        {
            _LifecycleCallback?.Invoke(Kind);
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
                mean_slope_mm = Summary.MeanSlopeMm,
                min_slope_percent = ToSlopePercent(Summary.MinSlopeMm),
                max_slope_percent = ToSlopePercent(Summary.MaxSlopeMm),
                mean_slope_percent = ToSlopePercent(Summary.MeanSlopeMm),
                field_slope_percent = ToSlopePercent(Summary.MeanSlopeMm),
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
                min_slope_percent = ToSlopePercent(Stats.MinSlopeMm),
                max_slope_percent = ToSlopePercent(Stats.MaxSlopeMm),
                mean_slope_percent = ToSlopePercent(Stats.MeanSlopeMm),
                field_slope_percent = ToSlopePercent(Stats.MeanSlopeMm),
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
            double TargetRatio = FieldConstants.CUT_FILL_RATIO;
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

        /// <summary>
        /// Converts a dimensionless slope (m/m) to percent for LLM chat formatting.
        /// </summary>
        /// <param name="SlopeMm">Slope magnitude in m/m.</param>
        /// <returns>Slope as percent.</returns>
        static private double ToSlopePercent
            (
            double SlopeMm
            )
        {
            return SlopeMm * 100.0;
        }
    }
}
