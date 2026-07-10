namespace AgGradeDesigner.Core.Haul
{
    /// <summary>
    /// Maps between <see cref="HaulBuilder.CutFillPair"/> and persisted DTOs.
    /// </summary>
    internal static class HaulCutFillPairMapper
    {
        /// <summary>
        /// Converts a live cut/fill pair to a persisted DTO.
        /// </summary>
        /// <param name="Pair">Engine cut/fill pair instance.</param>
        /// <returns>Serializable cut/fill pair DTO.</returns>
        public static HaulCutFillPairDto ToDto
            (
            HaulBuilder.CutFillPair Pair
            )
        {
            return new HaulCutFillPairDto
            {
                Order = Pair.Order,
                LoadBcy = Pair.LoadBcy,
                HaulDistanceM = Pair.HaulDistanceM,
                TotalCutBcyBefore = Pair.TotalCutBcyBefore,
                TotalFillBcyBefore = Pair.TotalFillBcyBefore,
                RouteTimeSeconds = Pair.RouteTimeS
            };
        }

        /// <summary>
        /// Reconstructs approximate cut/fill pairs from persisted pass DTOs when pair rows are missing.
        /// </summary>
        /// <param name="Passes">Route-ordered haul passes.</param>
        /// <returns>Synthetic cut/fill pair rows with zero route times.</returns>
        public static List<HaulCutFillPairDto> SynthesizeFromPasses
            (
            IReadOnlyList<HaulPassDto> Passes
            )
        {
            List<HaulCutFillPairDto> Pairs = new List<HaulCutFillPairDto>(Passes.Count);
            double CutBeforeBcy = 0.0;
            double FillBeforeBcy = 0.0;

            for (int Index = 0; Index < Passes.Count; Index++)
            {
                HaulPassDto Pass = Passes[Index];
                Pairs.Add(new HaulCutFillPairDto
                {
                    Order = Index + 1,
                    LoadBcy = Pass.LoadBcy,
                    HaulDistanceM = Pass.HaulDistanceM,
                    TotalCutBcyBefore = CutBeforeBcy,
                    TotalFillBcyBefore = FillBeforeBcy,
                    RouteTimeSeconds = 0.0
                });

                CutBeforeBcy += Pass.LoadBcy;
                FillBeforeBcy += Pass.LoadBcy;
            }

            return Pairs;
        }
    }
}
