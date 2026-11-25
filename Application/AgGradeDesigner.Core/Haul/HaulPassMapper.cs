namespace AgGradeDesigner.Core.Haul
{
    /// <summary>
    /// Maps between <see cref="HaulBuilder.Pass"/> and persisted DTOs.
    /// </summary>
    internal static class HaulPassMapper
    {
        /// <summary>
        /// Converts a live pass to a persisted DTO.
        /// </summary>
        /// <param name="Pass">Engine pass instance.</param>
        /// <returns>Serializable pass DTO.</returns>
        public static HaulPassDto ToDto
            (
            HaulBuilder.Pass Pass
            )
        {
            HaulPassDto Dto = new HaulPassDto
            {
                LoadBcy = Pass.LoadBcy,
                HaulDistanceM = Pass.HaulDistanceM,
                CutStartX = Pass.CutStartXy.Item1,
                CutStartY = Pass.CutStartXy.Item2,
                CutEndX = Pass.CutEndXy.Item1,
                CutEndY = Pass.CutEndXy.Item2,
                FillStartX = Pass.FillStartXy.Item1,
                FillStartY = Pass.FillStartXy.Item2,
                FillEndX = Pass.FillEndXy.Item1,
                FillEndY = Pass.FillEndXy.Item2,
                // intent: without this flag a reload tiles the ribbon back into the old lanes
                RibbonOrder = Pass.RibbonOrder,
                Stage = Pass.Stage
            };

            foreach ((ValueTuple<int, int> Cell, double Depth) Entry in Pass.CutCells)
            {
                Dto.CutCells.Add(new HaulPassCellDto
                {
                    X = Entry.Cell.Item1,
                    Y = Entry.Cell.Item2,
                    Depth = Entry.Depth
                });
            }

            foreach ((ValueTuple<int, int> Cell, double Depth) Entry in Pass.FillCells)
            {
                Dto.FillCells.Add(new HaulPassCellDto
                {
                    X = Entry.Cell.Item1,
                    Y = Entry.Cell.Item2,
                    Depth = Entry.Depth
                });
            }

            return Dto;
        }

        /// <summary>
        /// Reconstructs a live pass from a persisted DTO.
        /// </summary>
        /// <param name="Dto">Serialized pass.</param>
        /// <returns>Engine pass instance.</returns>
        public static HaulBuilder.Pass FromDto
            (
            HaulPassDto Dto
            )
        {
            List<(ValueTuple<int, int>, double)> CutCells = Dto.CutCells
                .Select(Cell => ((Cell.X, Cell.Y), Cell.Depth))
                .ToList();
            List<(ValueTuple<int, int>, double)> FillCells = Dto.FillCells
                .Select(Cell => ((Cell.X, Cell.Y), Cell.Depth))
                .ToList();

            HaulBuilder.Pass Pass = new HaulBuilder.Pass(
                CutCells,
                FillCells,
                Dto.LoadBcy,
                Dto.HaulDistanceM,
                (Dto.CutStartX, Dto.CutStartY),
                (Dto.CutEndX, Dto.CutEndY),
                (Dto.FillStartX, Dto.FillStartY),
                (Dto.FillEndX, Dto.FillEndY))
            {
                // intent: flow ribbons must stay in drive order or coverage retile them into lanes
                RibbonOrder = Dto.RibbonOrder,
                // intent: a reloaded road trip must not be clamped onto the field target
                Stage = Dto.Stage
            };
            return Pass;
        }
    }

    /// <summary>
    /// Maps between <see cref="HaulBuilder.GuidancePoint"/> and persisted DTOs.
    /// </summary>
    internal static class HaulGuidanceMapper
    {
        /// <summary>
        /// Converts a live guidance point to a persisted DTO.
        /// </summary>
        /// <param name="Point">Engine guidance point.</param>
        /// <returns>Serializable guidance DTO.</returns>
        public static HaulGuidancePointDto ToDto
            (
            HaulBuilder.GuidancePoint Point
            )
        {
            return new HaulGuidancePointDto
            {
                Seq = Point.Seq,
                PassNo = Point.PassNo,
                Action = Point.Action,
                Blade = Point.Blade,
                X = Point.X,
                Y = Point.Y,
                Lat = Point.Lat,
                Lon = Point.Lon,
                DepthMm = Point.DepthMm,
                HeadingDeg = Point.HeadingDeg
            };
        }

        /// <summary>
        /// Reconstructs a live guidance point from a persisted DTO.
        /// </summary>
        /// <param name="Dto">Serialized guidance point.</param>
        /// <returns>Engine guidance point.</returns>
        public static HaulBuilder.GuidancePoint FromDto
            (
            HaulGuidancePointDto Dto
            )
        {
            return new HaulBuilder.GuidancePoint(
                Dto.Seq,
                Dto.PassNo,
                Dto.Action,
                Dto.Blade,
                Dto.X,
                Dto.Y,
                Dto.Lat,
                Dto.Lon,
                Dto.DepthMm,
                Dto.HeadingDeg,
                0.0,
                0.0,
                string.Empty);
        }
    }

    /// <summary>
    /// Maps between haul engine time outputs and persisted DTOs.
    /// </summary>
    internal static class HaulGuidanceTimeMapper
    {
        /// <summary>
        /// Converts a live time-model result to a persisted DTO.
        /// </summary>
        /// <param name="Time">Engine time-model output.</param>
        /// <returns>Serializable guidance time DTO.</returns>
        public static HaulGuidanceTimeDto ToDto
            (
            HaulBuilder.TimeResult Time
            )
        {
            return new HaulGuidanceTimeDto
            {
                TotalSeconds = Time.TotalTimeS,
                LoadingSeconds = Time.LoadingTimeS,
                DumpingSeconds = Time.DumpingTimeS,
                GradingSeconds = 0.0,
                HaulingSeconds = Time.HaulingTimeS,
                ReturningSeconds = Time.ReturningTimeS,
                TurningSeconds = 0.0,
                OtherSeconds = Time.OtherTimeS,
                TurnCount = 0
            };
        }

        /// <summary>
        /// Converts production guidance timing to a persisted DTO.
        /// The total is deterministic drive time, not the retired time-model estimate and not the field-efficiency allowance.
        /// </summary>
        /// <param name="Timing">Production guidance timing.</param>
        /// <returns>Serializable guidance time DTO.</returns>
        public static HaulGuidanceTimeDto ToDto
            (
            HaulBuilder.GuidanceTiming.Result Timing
            )
        {
            // intent: the overlay total must match HaulBuilder deterministic hours, which exclude the efficiency allowance
            return new HaulGuidanceTimeDto
            {
                TotalSeconds = Timing.MotionSeconds + Timing.EventSeconds,
                LoadingSeconds = PhaseSeconds(Timing, HaulBuilder.LifecyclePlanner.Phase.Cut)
                    + PhaseSeconds(Timing, HaulBuilder.LifecyclePlanner.Phase.LineupCut)
                    + PhaseSeconds(Timing, HaulBuilder.LifecyclePlanner.Phase.ExitCut),
                DumpingSeconds = PhaseSeconds(Timing, HaulBuilder.LifecyclePlanner.Phase.Fill)
                    + PhaseSeconds(Timing, HaulBuilder.LifecyclePlanner.Phase.LineupFill)
                    + PhaseSeconds(Timing, HaulBuilder.LifecyclePlanner.Phase.ExitFill),
                GradingSeconds = PhaseSeconds(Timing, HaulBuilder.LifecyclePlanner.Phase.Grade)
                    + PhaseSeconds(Timing, HaulBuilder.LifecyclePlanner.Phase.LineupGrade)
                    + PhaseSeconds(Timing, HaulBuilder.LifecyclePlanner.Phase.ExitGrade)
                    + PhaseSeconds(Timing, HaulBuilder.LifecyclePlanner.Phase.TurnToGrade),
                HaulingSeconds = PhaseSeconds(Timing, HaulBuilder.LifecyclePlanner.Phase.Transit),
                ReturningSeconds = PhaseSeconds(Timing, HaulBuilder.LifecyclePlanner.Phase.TransitToNextCut),
                TurningSeconds = 0.0,
                OtherSeconds = PhaseSeconds(Timing, HaulBuilder.LifecyclePlanner.Phase.ManualAssist)
                    + PhaseSeconds(Timing, HaulBuilder.LifecyclePlanner.Phase.Start)
                    + PhaseSeconds(Timing, HaulBuilder.LifecyclePlanner.Phase.End),
                TurnCount = Timing.ForcedStopCount
            };
        }

        /// <summary>
        /// Selects production guidance timing when the engine produced it, otherwise the older time model.
        /// </summary>
        /// <param name="Result">Successful haul engine output.</param>
        /// <returns>Guidance time DTO, or null when the result has neither clock.</returns>
        public static HaulGuidanceTimeDto? ToDto
            (
            HaulBuilder.HaulBuilderResult Result
            )
        {
            // intent: a live result still carries the old time model; do not let it win over guidance timing
            if (Result.DetailedGuidanceTime != null)
                return ToDto(Result.DetailedGuidanceTime);

            if (Result.TimeResult != null)
                return ToDto(Result.TimeResult);

            return null;
        }

        /// <summary>
        /// Replaces pair route times with production guidance route seconds when those routes were timed.
        /// </summary>
        /// <param name="Pairs">Route-ordered cut/fill pairs.</param>
        /// <param name="Timing">Production guidance timing whose route keys are 1-based route numbers.</param>
        public static void ApplyRouteTimes
            (
            IList<HaulCutFillPairDto> Pairs,
            HaulBuilder.GuidanceTiming.Result Timing
            )
        {
            foreach (HaulCutFillPairDto Pair in Pairs)
            {
                if (Timing.RouteSeconds.TryGetValue(Pair.Order, out double Seconds))
                    Pair.RouteTimeSeconds = Seconds;
            }
        }

        /// <summary>
        /// Reads one lifecycle phase from guidance timing.
        /// </summary>
        /// <param name="Timing">Production guidance timing.</param>
        /// <param name="Phase">Lifecycle phase to read.</param>
        /// <returns>Phase seconds, or zero when the phase was not timed.</returns>
        private static double PhaseSeconds
            (
            HaulBuilder.GuidanceTiming.Result Timing,
            HaulBuilder.LifecyclePlanner.Phase Phase
            )
        {
            return Timing.PhaseSeconds.TryGetValue(Phase, out double Seconds) ? Seconds : 0.0;
        }
    }
}
