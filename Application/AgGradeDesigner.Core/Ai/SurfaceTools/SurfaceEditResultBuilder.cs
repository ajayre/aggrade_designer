using AgGradeDesigner.Core.Models;

namespace AgGradeDesigner.Core.Ai.SurfaceTools
{
    /// <summary>
    /// Builds normative preview and commit JSON envelopes for Phase 2 surface tools.
    /// </summary>
    static public class SurfaceEditResultBuilder
    {
        /// <summary>
        /// Preview status token returned by mutating tools.
        /// </summary>
        private const string PREVIEW_STATUS = "preview";

        /// <summary>
        /// Commit status token returned by <c>commit_surface_edit</c>.
        /// </summary>
        private const string COMMITTED_STATUS = "committed";

        /// <summary>
        /// Builds the preview envelope for a road build result.
        /// </summary>
        /// <param name="RoadResult">Road build summary.</param>
        /// <returns>Anonymous object ready for JSON serialization.</returns>
        static public object BuildRoadPreviewEnvelope
            (
            RoadBuildResult RoadResult
            )
        {
            return new
            {
                status = PREVIEW_STATUS,
                preview_active = true,
                operation = "build_road",
                road_id = RoadResult.RoadId,
                centerline_length_m = RoadResult.CenterlineLengthM,
                cut_m3 = RoadResult.OperationCutM3,
                fill_m3 = RoadResult.OperationFillM3,
                net_m3 = RoadResult.OperationFillM3 - RoadResult.OperationCutM3,
                cut_fill_ratio = ComputeCutFillRatio(RoadResult.OperationCutM3, RoadResult.OperationFillM3),
                affected_bin_count = RoadResult.AffectedBinCount,
                volume_scope = "road_operation_delta"
            };
        }

        /// <summary>
        /// Computes cut/fill ratio for a road operation delta.
        /// </summary>
        /// <param name="CutM3">Cut volume in cubic metres.</param>
        /// <param name="FillM3">Fill volume in cubic metres.</param>
        /// <returns>Cut-to-fill ratio, or zero when fill is negligible.</returns>
        static private double ComputeCutFillRatio
            (
            double CutM3,
            double FillM3
            )
        {
            if (FillM3 <= 1e-9)
                return 0.0;

            return CutM3 / FillM3;
        }

        /// <summary>
        /// Builds the preview envelope for a mutating tool result.
        /// </summary>
        /// <param name="Operation">Tool name that produced the preview.</param>
        /// <param name="Grid">Preview design grid.</param>
        /// <param name="Mutation">Mutation summary.</param>
        /// <returns>Anonymous object ready for JSON serialization.</returns>
        static public object BuildPreviewEnvelope
            (
            string Operation,
            DesignGrid Grid,
            SurfaceMutationResult Mutation
            )
        {
            SurfaceCutFillVolumes Volumes = SurfaceVolumeCalculator.CalculateCutFill(
                Grid,
                SurfaceLayerKind.Existing,
                SurfaceLayerKind.Proposed);

            return new
            {
                status = PREVIEW_STATUS,
                preview_active = true,
                operation = Operation,
                cut_m3 = Volumes.CutM3,
                fill_m3 = Volumes.FillM3,
                net_m3 = Volumes.NetM3,
                cut_fill_ratio = Volumes.CutFillRatio,
                affected_bin_count = Mutation.AffectedBinCount,
                delta_z_m = Mutation.DeltaZM,
                target_cut_fill_ratio = Mutation.TargetCutFillRatio,
                applied_offset_m = Mutation.AppliedOffsetM,
                slope_x = Mutation.SlopeX,
                slope_y = Mutation.SlopeY,
                polygon_applied = Mutation.PolygonApplied
            };
        }

        /// <summary>
        /// Builds the preview envelope for a multi-step edit plan.
        /// </summary>
        /// <param name="Description">Edit plan description.</param>
        /// <param name="Grid">Preview design grid after all steps.</param>
        /// <param name="StepResults">Per-step preview summaries.</param>
        /// <returns>Anonymous object ready for JSON serialization.</returns>
        static public object BuildEditPlanPreviewEnvelope
            (
            string Description,
            DesignGrid Grid,
            IReadOnlyList<object> StepResults
            )
        {
            SurfaceCutFillVolumes Volumes = SurfaceVolumeCalculator.CalculateCutFill(
                Grid,
                SurfaceLayerKind.Existing,
                SurfaceLayerKind.Proposed);

            return new
            {
                status = PREVIEW_STATUS,
                preview_active = true,
                operation = "edit_plan",
                cut_m3 = Volumes.CutM3,
                fill_m3 = Volumes.FillM3,
                net_m3 = Volumes.NetM3,
                cut_fill_ratio = Volumes.CutFillRatio,
                affected_bin_count = StepResults.Count,
                edit_plan_description = Description,
                step_count = StepResults.Count,
                step_results = StepResults
            };
        }

        /// <summary>
        /// Builds the commit envelope after a successful commit.
        /// </summary>
        /// <param name="Description">Commit description text.</param>
        /// <param name="Volumes">Committed cut/fill volumes.</param>
        /// <returns>Anonymous object ready for JSON serialization.</returns>
        static public object BuildCommitEnvelope
            (
            string Description,
            SurfaceCutFillVolumes Volumes
            )
        {
            return new
            {
                status = COMMITTED_STATUS,
                preview_active = false,
                description = Description,
                cut_m3 = Volumes.CutM3,
                fill_m3 = Volumes.FillM3,
                net_m3 = Volumes.NetM3,
                cut_fill_ratio = Volumes.CutFillRatio
            };
        }

        /// <summary>
        /// Builds the validation result payload for <c>validate_surface_edit</c>.
        /// </summary>
        /// <param name="Result">Validation outcome.</param>
        /// <returns>Anonymous object ready for JSON serialization.</returns>
        static public object BuildValidationPayload
            (
            SurfaceValidationResult Result
            )
        {
            return new
            {
                valid = Result.Valid,
                surface = Result.Surface,
                violations = Result.Violations.Select(V => new
                {
                    code = V.Code,
                    message = V.Message,
                    bin_count = V.BinCount,
                    limit = V.Limit,
                    max_observed_slope_mm = V.MaxObservedSlopeMm,
                    min_observed_slope_mm = V.MinObservedSlopeMm
                }).ToArray()
            };
        }
    }
}
