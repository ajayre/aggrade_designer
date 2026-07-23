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
                FillEndY = Pass.FillEndXy.Item2
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

            return new HaulBuilder.Pass(
                CutCells,
                FillCells,
                Dto.LoadBcy,
                Dto.HaulDistanceM,
                (Dto.CutStartX, Dto.CutStartY),
                (Dto.CutEndX, Dto.CutEndY),
                (Dto.FillStartX, Dto.FillStartY),
                (Dto.FillEndX, Dto.FillEndY));
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
    /// Maps between <see cref="HaulBuilder.GuidanceTime"/> and persisted DTOs.
    /// </summary>
    internal static class HaulGuidanceTimeMapper
    {
        /// <summary>
        /// Converts a live guidance time estimate to a persisted DTO.
        /// </summary>
        /// <param name="Time">Engine guidance time estimate.</param>
        /// <returns>Serializable guidance time DTO.</returns>
        public static HaulGuidanceTimeDto ToDto
            (
            HaulBuilder.GuidanceTime Time
            )
        {
            return new HaulGuidanceTimeDto
            {
                TotalSeconds = Time.TotalS,
                LoadingSeconds = Time.LoadingS,
                DumpingSeconds = Time.DumpingS,
                GradingSeconds = Time.GradingS,
                HaulingSeconds = Time.HaulingS,
                ReturningSeconds = Time.ReturningS,
                TurningSeconds = Time.TurningS,
                OtherSeconds = Time.OtherS,
                TurnCount = Time.NTurns
            };
        }

        /// <summary>
        /// Reconstructs a live guidance time estimate from a persisted DTO.
        /// </summary>
        /// <param name="Dto">Serialized guidance time.</param>
        /// <returns>Engine guidance time estimate.</returns>
        public static HaulBuilder.GuidanceTime FromDto
            (
            HaulGuidanceTimeDto Dto
            )
        {
            return new HaulBuilder.GuidanceTime(
                Dto.TotalSeconds,
                Dto.LoadingSeconds,
                Dto.DumpingSeconds,
                Dto.GradingSeconds,
                Dto.HaulingSeconds,
                Dto.ReturningSeconds,
                Dto.TurningSeconds,
                Dto.OtherSeconds,
                Dto.TurnCount);
        }
    }
}
