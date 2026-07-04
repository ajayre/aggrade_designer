using System.Text.Json;

using AgGradeDesigner.Core.Models;

using NetTopologySuite.Geometries;

namespace AgGradeDesigner.Core.Ai.SurfaceTools
{
    /// <summary>
    /// Executes ordered edit-plan steps against a preview design grid.
    /// </summary>
    static public class EditPlanExecutor
    {
        /// <summary>
        /// Executes each step in an edit plan against the preview grid.
        /// </summary>
        /// <param name="PreviewGrid">Preview design grid updated in place.</param>
        /// <param name="Steps">Ordered edit-plan steps.</param>
        /// <param name="StepResults">Per-step preview summaries.</param>
        /// <param name="Error">Error message when a step fails.</param>
        /// <returns>True when all steps succeed.</returns>
        static public bool TryExecuteSteps
            (
            DesignGrid PreviewGrid,
            IReadOnlyList<JsonElement> Steps,
            out List<object> StepResults,
            out string? Error
            )
        {
            StepResults = new List<object>();
            Error = null;

            foreach (JsonElement Step in Steps)
            {
                if (!Step.TryGetProperty("operation", out JsonElement OperationElement))
                {
                    Error = "Each edit-plan step requires an 'operation' field.";
                    return false;
                }

                string? Operation = OperationElement.GetString();
                if (string.IsNullOrWhiteSpace(Operation))
                {
                    Error = "Each edit-plan step requires a non-empty 'operation' field.";
                    return false;
                }

                if (!TryExecuteStep(PreviewGrid, Operation, Step, out SurfaceMutationResult Mutation, out Error))
                    return false;

                StepResults.Add(SurfaceEditResultBuilder.BuildPreviewEnvelope(Operation, PreviewGrid, Mutation));
            }

            return true;
        }

        /// <summary>
        /// Executes one edit-plan step against the preview grid.
        /// </summary>
        /// <param name="PreviewGrid">Preview design grid.</param>
        /// <param name="Operation">Surface tool name.</param>
        /// <param name="Step">Step JSON object.</param>
        /// <param name="Mutation">Mutation summary.</param>
        /// <param name="Error">Error message when execution fails.</param>
        /// <returns>True when the step succeeds.</returns>
        static public bool TryExecuteStep
            (
            DesignGrid PreviewGrid,
            string Operation,
            JsonElement Step,
            out SurfaceMutationResult Mutation,
            out string? Error
            )
        {
            Mutation = default;
            Error = null;

            switch (Operation)
            {
                case "offset_surface":
                    if (!Step.TryGetProperty("delta_z", out JsonElement DeltaElement) || !DeltaElement.TryGetDouble(out double DeltaZ))
                    {
                        Error = "Step 'offset_surface' requires numeric 'delta_z'.";
                        return false;
                    }

                    TryParseOptionalPolygon(Step, out Polygon? OffsetPolygon, out Error);
                    if (Error != null)
                        return false;

                    Mutation = SurfaceMutators.OffsetSurface(PreviewGrid, DeltaZ, OffsetPolygon);
                    return true;

                case "rebalance_surface":
                    double TargetRatio = FieldConstants.CUT_FILL_RATIO;
                    if (Step.TryGetProperty("target_cut_fill_ratio", out JsonElement RatioElement))
                    {
                        if (!RatioElement.TryGetDouble(out TargetRatio) || TargetRatio <= 0.0)
                        {
                            Error = "Step 'rebalance_surface' requires positive 'target_cut_fill_ratio'.";
                            return false;
                        }
                    }

                    TryParseOptionalPolygon(Step, out Polygon? RebalancePolygon, out Error);
                    if (Error != null)
                        return false;

                    Mutation = SurfaceMutators.RebalanceSurface(PreviewGrid, TargetRatio, RebalancePolygon);
                    return true;

                case "apply_plane_grade":
                    if (!Step.TryGetProperty("origin", out JsonElement OriginElement))
                    {
                        Error = "Step 'apply_plane_grade' requires an 'origin' point.";
                        return false;
                    }

                    if (!SurfacePolygonHelper.TryReadPointXyz(
                            OriginElement,
                            out double OriginX,
                            out double OriginY,
                            out double OriginZ,
                            out bool HasOriginZ,
                            out Error))
                    {
                        return false;
                    }

                    if (!HasOriginZ)
                    {
                        Error = "Step 'apply_plane_grade' requires origin elevation via 'z' or 'elevation_m'.";
                        return false;
                    }

                    if (!Step.TryGetProperty("slope_x", out JsonElement SlopeXElement) || !SlopeXElement.TryGetDouble(out double SlopeX))
                    {
                        Error = "Step 'apply_plane_grade' requires numeric 'slope_x'.";
                        return false;
                    }

                    if (!Step.TryGetProperty("slope_y", out JsonElement SlopeYElement) || !SlopeYElement.TryGetDouble(out double SlopeY))
                    {
                        Error = "Step 'apply_plane_grade' requires numeric 'slope_y'.";
                        return false;
                    }

                    TryParseOptionalPolygon(Step, out Polygon? GradePolygon, out Error);
                    if (Error != null)
                        return false;

                    Mutation = SurfaceMutators.ApplyPlaneGrade(
                        PreviewGrid,
                        OriginX,
                        OriginY,
                        OriginZ,
                        SlopeX,
                        SlopeY,
                        GradePolygon);
                    return true;

                default:
                    Error = $"Edit-plan operation '{Operation}' is not available in Phase 2.";
                    return false;
            }
        }

        /// <summary>
        /// Parses an optional polygon property from a step object.
        /// </summary>
        /// <param name="Step">Step JSON object.</param>
        /// <param name="Polygon">Parsed polygon when present.</param>
        /// <param name="Error">Error message when parsing fails.</param>
        static private void TryParseOptionalPolygon
            (
            JsonElement Step,
            out Polygon? Polygon,
            out string? Error
            )
        {
            Polygon = null;
            Error = null;
            if (!Step.TryGetProperty("polygon", out JsonElement PolygonElement))
                return;

            if (!SurfacePolygonHelper.TryParsePolygon(PolygonElement, out Polygon, out Error))
                return;
        }
    }
}
