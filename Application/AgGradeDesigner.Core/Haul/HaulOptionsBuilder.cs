using AgGradeDesigner.Core.Models;

namespace AgGradeDesigner.Core.Haul
{
    /// <summary>
    /// Maps project data and user haul settings to <see cref="HaulBuilder.HaulBuilderOptions"/> (§6.1).
    /// </summary>
    public static class HaulOptionsBuilder
    {
        /// <summary>
        /// Builds haul engine options for the active project and user settings.
        /// </summary>
        /// <param name="Project">Open project with survey and design.</param>
        /// <param name="Settings">Persisted user haul defaults.</param>
        /// <param name="AgdPath">Temporary AGD file path passed to the engine.</param>
        /// <returns>Configured options ready for <see cref="HaulBuilder.HaulBuilderEngine.Run"/>.</returns>
        public static HaulBuilder.HaulBuilderOptions Build
            (
            ProjectDocument Project,
            HaulSettings Settings,
            string AgdPath
            )
        {
            DesignParameters Params = Project.DesignParameters
                ?? throw new InvalidOperationException("Design parameters are required to generate haul data.");

            HaulBuilder.HaulBuilderOptions Options = new HaulBuilder.HaulBuilderOptions
            {
                AgdPath = AgdPath,
                // intent: grid spacing is a user setting, not derived from blade width
                GridSizeFt = Settings.GridSizeFt,
                ToleranceMm = Settings.ToleranceMm,
                ScraperCapacityLcy = Settings.ScraperCapacityLcy,
                SwellFactor = Settings.SwellFactor,
                CutFillRatio = Params.CutFillRatio,
                MaxDepthMm = Settings.MaxDepthMm,
                BladeWidthMm = Settings.BladeWidthMm,
                FieldEfficiency = Settings.FieldEfficiency,
                CutSpeedMph = Settings.LoadingSpeedMph,
                LoadedTravelSpeedMph = Settings.HaulSpeedMph,
                FillSpeedMph = Settings.DumpingSpeedMph,
                UnloadedTravelSpeedMph = Settings.ReturnSpeedMph,
                TurnSpeedMph = Settings.TurnSpeedMph,
                GradeSpeedMph = Settings.GradeSpeedMph,
                AccelerationTimeS = Settings.AccelTimeS,
                DecelerationTimeS = Settings.DecelTimeS,
                MinimumTurningDiameterFt = Settings.TurningCircleFt,
                WaypointTurnThresholdDeg = Settings.TurnThresholdDeg,
                EquipmentWidthFt = Settings.EquipmentWidthFt,
                EquipmentLengthFt = Settings.EquipmentLengthFt,
                EnablePivot = Settings.Pivot,
                PivotWheelbaseFt = Settings.PivotWheelbaseFt,
                PivotClearanceFt = Settings.PivotClearanceFt,
                PivotMoveCost = Settings.PivotMoveCost,
                ZoneCount = Settings.Zones,
                PixelsPerCell = HaulBuilder.Constants.DEFAULT_PIXELS_PER_BIN,
                FlowArrowStrideCells = HaulFlowFieldResolver.DEFAULT_ARROW_STRIDE,
                RandomSeed = HaulBuilder.Constants.DEFAULT_SEED,
                // intent: plan-only preview until production guidance is re-enabled in the UI
                SkipGuidance = true
            };

            // intent: lane axis follows design main slope when the user enabled that preference
            Options.LaneAxisHeadingDeg = Settings.UseDesignMainSlopeDirection
                ? Params.MainSlopeDirectionDeg
                : Settings.HeadingDeg;

            if (Settings.Pivot)
            {
                Options.PivotCenter =
                    $"{Settings.PivotLatitudeDeg.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                    $"{Settings.PivotLongitudeDeg.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
            }

            return Options;
        }
    }
}
