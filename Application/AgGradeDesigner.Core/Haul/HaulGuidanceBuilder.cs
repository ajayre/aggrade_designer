using AgGradeDesigner.Core.Design;
using AgGradeDesigner.Core.Models;

namespace AgGradeDesigner.Core.Haul
{
    /// <summary>
    /// Builds blade-scale guidance tracks required for haul route symbology (§7.4.6–§7.4.9).
    /// </summary>
    internal static class HaulGuidanceBuilder
    {
        /// <summary>
        /// Ensures the engine result includes guidance waypoints and a time estimate when guidance ran.
        /// </summary>
        /// <param name="Result">Successful haul engine output.</param>
        /// <param name="Options">Options used for the run.</param>
        /// <param name="Settings">User haul settings for deferred guidance rebuilds.</param>
        public static void EnsureOnResult
            (
            HaulBuilder.HaulBuilderResult Result,
            HaulBuilder.HaulBuilderOptions Options,
            HaulSettings Settings
            )
        {
            if (Options.SkipGuidance)
                return;

            // intent: EstimateGuidanceTime needs a full GuidanceResult, not only flat GuidancePoints
            if (Result.Guidance == null || Result.Guidance.Points.Count == 0)
            {
                HaulBuilder.GuidanceResult? Guidance = TryBuildGuidance(Result, Options, Settings);
                if (Guidance != null && Guidance.Points.Count > 0)
                    Result.Guidance = Guidance;
            }
        }

        /// <summary>
        /// Ensures persisted haul data includes guidance and guidance time after project reload.
        /// </summary>
        /// <param name="Data">Haul data to enrich.</param>
        /// <param name="Project">Open project supplying survey and design.</param>
        /// <param name="Settings">Active haul settings.</param>
        public static void EnsureOnData
            (
            HaulData Data,
            ProjectDocument Project,
            HaulSettings Settings
            )
        {
            bool NeedsGuidance = Data.ResolveGuidancePoints().Count == 0;
            bool NeedsGuidanceTime = Data.ResolveGuidanceTime() == null;
            if (!NeedsGuidance && !NeedsGuidanceTime)
                return;

            if (!NeedsGuidance && NeedsGuidanceTime && Data.Result?.TimeResult != null)
            {
                Data.Payload.GuidanceTime = HaulGuidanceTimeMapper.ToDto(Data.Result.TimeResult);
                return;
            }

            if (!Project.HasSurvey || !Project.HasDesign || Project.Survey == null || Project.DesignGrid == null)
                return;

            string TempAgd = Path.Combine(Path.GetTempPath(), $"aggrade-haul-guidance-{Guid.NewGuid():N}.agd");
            try
            {
                AgdService.Export(
                    TempAgd,
                    Project.Survey,
                    Project.DesignGrid,
                    Project.DesignParameters ?? new DesignParameters());

                HaulBuilder.AgdField Field = HaulBuilder.Agd.Parse(TempAgd);
                HaulBuilder.HaulBuilderOptions Options = HaulOptionsBuilder.Build(Project, Settings, TempAgd);
                HaulBuilder.Grid Grid = Data.ResolveGrid(Field);
                IReadOnlyList<HaulBuilder.Pass> Passes = Data.ResolveOrderedPasses();
                if (Passes.Count == 0)
                    return;

                List<int> Order = Enumerable.Range(0, Passes.Count).ToList();
                HaulBuilder.Route Route = new HaulBuilder.Route(
                    Order,
                    Passes.ToList(),
                    0.0);

                HaulBuilder.GuidanceResult Guidance = HaulBuilder.Swaths.BuildGuidance(
                    Grid,
                    Field,
                    Route,
                    Options.BladeWidthMm,
                    Options.LaneAxisHeadingDeg,
                    Settings.LaneOverlap,
                    !Settings.NoGradeReturn,
                    Options.WaypointTurnThresholdDeg,
                    true,
                    null,
                    1.0,
                    null,
                    null,
                    // intent: match engine lineup length so rebuilt guidance keeps EquipmentLengthFt
                    Options.EquipmentLengthFt * HaulBuilder.Constants.FEET_TO_METERS);

                if (NeedsGuidance)
                {
                    Data.Payload.GuidancePoints.Clear();
                    foreach (HaulBuilder.GuidancePoint Point in Guidance.Points)
                        Data.Payload.GuidancePoints.Add(HaulGuidanceMapper.ToDto(Point));
                }

                if (NeedsGuidanceTime && Data.Result?.TimeResult != null)
                    Data.Payload.GuidanceTime = HaulGuidanceTimeMapper.ToDto(Data.Result.TimeResult);
            }
            finally
            {
                if (File.Exists(TempAgd))
                    File.Delete(TempAgd);
            }
        }

        /// <summary>
        /// Builds guidance from a completed engine result when route geometry is available.
        /// </summary>
        /// <param name="Result">Successful haul engine output.</param>
        /// <param name="Options">Options used for the run.</param>
        /// <param name="Settings">User haul settings for retired option values.</param>
        /// <returns>Rebuilt guidance, or null when route geometry is unavailable.</returns>
        private static HaulBuilder.GuidanceResult? TryBuildGuidance
            (
            HaulBuilder.HaulBuilderResult Result,
            HaulBuilder.HaulBuilderOptions Options,
            HaulSettings Settings
            )
        {
            if (Result.Grid == null || Result.Field == null)
                return null;

            HaulBuilder.Route? Route = Result.Route;
            if (Route == null || Route.OrderedPasses == null || Route.OrderedPasses.Count == 0)
            {
                IReadOnlyList<HaulBuilder.Pass> Passes = Result.PassResult?.Passes
                    ?? Result.Route?.OrderedPasses
                    ?? new List<HaulBuilder.Pass>();
                if (Passes.Count == 0)
                    return null;

                List<int> Order = Enumerable.Range(0, Passes.Count).ToList();
                Route = new HaulBuilder.Route(Order, Passes.ToList(), 0.0);
            }

            return HaulBuilder.Swaths.BuildGuidance(
                Result.Grid,
                Result.Field,
                Route,
                Options.BladeWidthMm,
                Options.LaneAxisHeadingDeg,
                Settings.LaneOverlap,
                !Settings.NoGradeReturn,
                Options.WaypointTurnThresholdDeg,
                true,
                null,
                1.0,
                null,
                null,
                // intent: match engine lineup length so rebuilt guidance keeps EquipmentLengthFt
                Options.EquipmentLengthFt * HaulBuilder.Constants.FEET_TO_METERS);
        }
    }
}
