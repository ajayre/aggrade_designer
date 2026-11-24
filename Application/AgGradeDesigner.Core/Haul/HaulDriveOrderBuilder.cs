using AgGradeDesigner.Core.Design;
using AgGradeDesigner.Core.Models;

namespace AgGradeDesigner.Core.Haul
{
    /// <summary>
    /// Builds the drive sequence for a haul plan and stores it on the payload.
    /// The planner pass list is left unchanged.
    /// </summary>
    internal static class HaulDriveOrderBuilder
    {
        /// <summary>
        /// Fills <see cref="HaulPersistedPayload.DriveOrder"/> when it is still empty.
        /// </summary>
        /// <param name="Data">Haul data whose passes are in planner order.</param>
        /// <param name="Project">Open project supplying survey and design.</param>
        /// <param name="Settings">Active haul settings.</param>
        public static void Ensure
            (
            HaulData Data,
            ProjectDocument Project,
            HaulSettings Settings
            )
        {
            if (Data.Payload.DriveOrder.Count > 0)
                return;

            IReadOnlyList<HaulBuilder.Pass> Passes = Data.ResolveOrderedPasses();
            if (Passes.Count == 0)
                return;

            if (!Project.HasSurvey || !Project.HasDesign || Project.Survey == null || Project.DesignGrid == null)
                return;

            if (Project.DesignParameters == null)
                return;

            string TempAgd = Path.Combine(Path.GetTempPath(), $"aggrade-haul-drive-{Guid.NewGuid():N}.agd");
            try
            {
                AgdService.Export(
                    TempAgd,
                    Project.Survey,
                    Project.DesignGrid,
                    Project.DesignParameters);

                HaulBuilder.HaulBuilderOptions Options = Data.Result?.Options
                    ?? HaulOptionsBuilder.Build(Project, Settings, TempAgd);
                HaulBuilder.AgdField Field = HaulBuilder.Agd.Parse(TempAgd);
                HaulBuilder.Grid Grid = Data.ResolveGrid(Field);
                double BowlM3 = (Options.ScraperCapacityLcy / Options.SwellFactor)
                    / HaulBuilder.Constants.M3_TO_CY;
                double BladeM = Options.BladeWidthMm / 1000.0;
                double RadiusM = Options.MinimumTurningDiameterFt * 0.5 * HaulBuilder.Constants.FEET_TO_METERS;
                HaulBuilder.TimeParams Clock = new HaulBuilder.TimeParams(
                    Options.CutSpeedMph,
                    Options.LoadedTravelSpeedMph,
                    Options.FillSpeedMph,
                    Options.UnloadedTravelSpeedMph,
                    Options.AccelerationTimeS,
                    Options.DecelerationTimeS,
                    Options.FieldEfficiency);

                // intent: this is the only place the drive permutation is known; the engine route stays in planner order
                HaulBuilder.TripGuidance.JobGuidance Job = HaulBuilder.TripGuidance.Build(
                    Grid,
                    Passes,
                    Clock,
                    BowlM3,
                    BladeM,
                    RadiusM,
                    -1,
                    Options.Guidance);
                List<int> Indexes = HaulDriveOrder.PlannerIndexes(Job, Passes.Count);
                if (Indexes.Count == 0)
                    return;

                Data.Payload.DriveOrder = Indexes;
                Data.Payload.Messages.Add(
                    $"Drive order lists {Indexes.Count} trips. The haul view follows that sequence.");
            }
            catch (Exception Exception)
            {
                Data.Payload.Messages.Add("Drive order was not built: " + Exception.Message);
            }
            finally
            {
                if (File.Exists(TempAgd))
                    File.Delete(TempAgd);
            }
        }
    }
}
