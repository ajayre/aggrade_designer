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
        /// Ensures the engine result includes guidance waypoints and a guidance time estimate.
        /// </summary>
        /// <param name="Result">Successful haul engine output.</param>
        /// <param name="Options">Options used for the run.</param>
        public static void EnsureOnResult
            (
            HaulBuilder.HaulBuilderResult Result,
            HaulBuilder.HaulBuilderOptions Options
            )
        {
            // intent: EstimateGuidanceTime needs a full GuidanceResult, not only flat GuidancePoints
            if (Result.Guidance == null || Result.Guidance.Points.Count == 0)
            {
                HaulBuilder.GuidanceResult? Guidance = TryBuildGuidance(Result, Options);
                if (Guidance != null && Guidance.Points.Count > 0)
                    Result.Guidance = Guidance;
            }

            EnsureGuidanceTime(Result, Options);
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

            // intent: reuse live GuidanceResult when only the time estimate is missing
            if (!NeedsGuidance &&
                NeedsGuidanceTime &&
                Data.Result?.Guidance is { Points.Count: > 0 } LiveGuidance)
            {
                HaulBuilder.HaulBuilderOptions LiveOptions = HaulOptionsBuilder.Build(
                    Project,
                    Settings,
                    string.Empty);
                HaulBuilder.GuidanceTime LiveTime = EstimateTime(LiveGuidance, LiveOptions);
                Data.Payload.GuidanceTime = HaulGuidanceTimeMapper.ToDto(LiveTime);
                Data.Result.GuidanceTime = LiveTime;
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
                    Options.HeadingDeg ?? 0.0,
                    Options.LaneOverlap,
                    !Options.NoGradeReturn,
                    Options.TurnThresholdDeg,
                    !Options.NoRouteSmoothing,
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

                if (NeedsGuidanceTime)
                {
                    HaulBuilder.GuidanceTime Time = EstimateTime(Guidance, Options);
                    Data.Payload.GuidanceTime = HaulGuidanceTimeMapper.ToDto(Time);
                    if (Data.Result != null)
                        Data.Result.GuidanceTime = Time;
                }
            }
            finally
            {
                if (File.Exists(TempAgd))
                    File.Delete(TempAgd);
            }
        }

        /// <summary>
        /// Estimates and assigns <see cref="HaulBuilder.HaulBuilderResult.GuidanceTime"/> when missing.
        /// </summary>
        /// <param name="Result">Successful haul engine output.</param>
        /// <param name="Options">Options used for the run.</param>
        private static void EnsureGuidanceTime
            (
            HaulBuilder.HaulBuilderResult Result,
            HaulBuilder.HaulBuilderOptions Options
            )
        {
            if (Result.GuidanceTime != null)
                return;

            HaulBuilder.GuidanceResult? Guidance = Result.Guidance;
            if (Guidance == null || Guidance.Points.Count == 0)
                return;

            Result.GuidanceTime = EstimateTime(Guidance, Options);
        }

        /// <summary>
        /// Estimates guidance-track execution time from haul speed settings.
        /// </summary>
        /// <param name="Guidance">Blade-scale guidance result.</param>
        /// <param name="Options">Haul options supplying speeds and efficiency.</param>
        /// <returns>Guidance time breakdown.</returns>
        private static HaulBuilder.GuidanceTime EstimateTime
            (
            HaulBuilder.GuidanceResult Guidance,
            HaulBuilder.HaulBuilderOptions Options
            )
        {
            return HaulBuilder.Swaths.EstimateGuidanceTime(
                Guidance,
                Options.LoadingSpeedMph,
                Options.HaulSpeedMph,
                Options.DumpingSpeedMph,
                Options.ReturnSpeedMph,
                Options.GradeSpeedMph,
                Options.TurnSpeedMph,
                Options.TurningCircleFt,
                Options.AccelTimeS,
                Options.DecelTimeS,
                Options.FieldEfficiency);
        }

        /// <summary>
        /// Builds guidance from a completed engine result when route geometry is available.
        /// </summary>
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

            return HaulBuilder.Swaths.BuildGuidance(
                Result.Grid,
                Result.Field,
                Route,
                Options.BladeWidthMm,
                Options.HeadingDeg ?? 0.0,
                Options.LaneOverlap,
                !Options.NoGradeReturn,
                Options.TurnThresholdDeg,
                !Options.NoRouteSmoothing,
                null,
                1.0,
                null,
                null,
                // intent: match engine lineup length so rebuilt guidance keeps EquipmentLengthFt
                Options.EquipmentLengthFt * HaulBuilder.Constants.FEET_TO_METERS);
        }
    }
}
