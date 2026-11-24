using AgGradeDesigner.Core.Design;
using AgGradeDesigner.Core.Models;

namespace AgGradeDesigner.Core.Haul
{
    /// <summary>
    /// Keeps haul display tracks on the production guidance planner.
    /// Cut and fill runs follow each pass's flow ribbon. The older swath tiler is not used.
    /// </summary>
    internal static class HaulGuidanceBuilder
    {
        /// <summary>
        /// Ensures the engine result includes guidance waypoints and a time estimate when guidance ran.
        /// </summary>
        /// <param name="Result">Successful haul engine output.</param>
        /// <param name="Options">Options used for the run.</param>
        public static void EnsureOnResult
            (
            HaulBuilder.HaulBuilderResult Result,
            HaulBuilder.HaulBuilderOptions Options
            )
        {
            if (Options.SkipGuidance)
                return;

            // intent: never replace production guidance with the retired lane tiler
            if (Result.Guidance == null || Result.Guidance.Points.Count == 0)
            {
                HaulBuilder.GuidanceResult? Guidance = TryBuildGuidance(Result, Options);
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

            // intent: production guidance comes from the engine. The swath fallback is only for
            // an older saved plan that has passes but no waypoints yet.

            if (!NeedsGuidance && NeedsGuidanceTime && Data.Result != null)
            {
                HaulGuidanceTimeDto? LiveTime = HaulGuidanceTimeMapper.ToDto(Data.Result);
                if (LiveTime != null)
                {
                    Data.Payload.GuidanceTime = LiveTime;
                    if (Data.Result.DetailedGuidanceTime != null)
                        HaulGuidanceTimeMapper.ApplyRouteTimes(Data.Payload.CutFillPairs, Data.Result.DetailedGuidanceTime);

                    return;
                }
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

                // intent: same planner as the engine, so ribbon cuts and fills are not retiled into lanes
                HaulBuilder.GuidanceResult Guidance = HaulBuilder.GuidancePlanner.Build(
                    Route,
                    Grid,
                    Field,
                    0.0,
                    Options).Guidance;

                if (NeedsGuidance)
                {
                    Data.Payload.GuidancePoints.Clear();
                    foreach (HaulBuilder.GuidancePoint Point in Guidance.Points)
                        Data.Payload.GuidancePoints.Add(HaulGuidanceMapper.ToDto(Point));
                }

                if (NeedsGuidanceTime && Data.Result != null)
                {
                    HaulGuidanceTimeDto? LiveTime = HaulGuidanceTimeMapper.ToDto(Data.Result);
                    if (LiveTime != null)
                    {
                        Data.Payload.GuidanceTime = LiveTime;
                        if (Data.Result.DetailedGuidanceTime != null)
                            HaulGuidanceTimeMapper.ApplyRouteTimes(Data.Payload.CutFillPairs, Data.Result.DetailedGuidanceTime);
                    }
                }
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
        /// <returns>Rebuilt guidance, or null when route geometry is unavailable.</returns>
        private static HaulBuilder.GuidanceResult? TryBuildGuidance
            (
            HaulBuilder.HaulBuilderResult Result,
            HaulBuilder.HaulBuilderOptions Options
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

            // intent: the engine reports north-south when the ribbons take their heading from the flow
            double HeadingDeg = Result.Metrics?.LaneAxisHeadingDeg ?? 0.0;
            return HaulBuilder.GuidancePlanner.Build(
                Route,
                Result.Grid,
                Result.Field,
                HeadingDeg,
                Options).Guidance;
        }
    }
}
