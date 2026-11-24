using AgGradeDesigner.Core.Haul;

namespace AgGradeDesigner.Core.Tests.Haul
{
    /// <summary>
    /// Verifies haul statistics use production guidance timing instead of the retired time model.
    /// </summary>
    public sealed class HaulGuidanceTimeSourceTests
    {
        /// <summary>
        /// Verifies a live result keeps the old time model on the result but reports deterministic guidance hours.
        /// </summary>
        [Fact]
        public void FromResult_PrefersDeterministicGuidanceTimeOverTimeModel
            (
            )
        {
            const double OldTotalSeconds = 210.0 * 3600.0;
            const double MotionSeconds = 170.0 * 3600.0;
            const double EventSeconds = 12.0 * 3600.0;
            const double AllowanceSeconds = 40.0 * 3600.0;

            HaulBuilder.HaulBuilderResult Result = new HaulBuilder.HaulBuilderResult
            {
                TimeResult = new HaulBuilder.TimeResult(
                    OldTotalSeconds,
                    0.0,
                    0.0,
                    0.0,
                    0.0,
                    0.0,
                    0.0,
                    0.0,
                    0.0,
                    0.0,
                    0.0,
                    0.0),
                DetailedGuidanceTime = new HaulBuilder.GuidanceTiming.Result(
                    new Dictionary<HaulBuilder.LifecyclePlanner.Phase, double>
                    {
                        [HaulBuilder.LifecyclePlanner.Phase.Cut] = 60.0 * 3600.0,
                        [HaulBuilder.LifecyclePlanner.Phase.Fill] = 50.0 * 3600.0
                    },
                    new Dictionary<HaulBuilder.LifecyclePlanner.Phase, double>(),
                    new Dictionary<int, double>
                    {
                        [1] = 100.0 * 3600.0,
                        [2] = 82.0 * 3600.0
                    },
                    new Dictionary<int, double>(),
                    MotionSeconds,
                    MotionSeconds,
                    EventSeconds,
                    AllowanceSeconds,
                    0.0,
                    0.0,
                    0.0,
                    0.0,
                    0.0,
                    0.0,
                    0,
                    0,
                    0,
                    0.0,
                    new List<HaulBuilder.GuidanceTiming.CurveViolation>())
            };

            Result.CutFillPairs.Add(Pair(1, 400.0 * 3600.0));
            Result.CutFillPairs.Add(Pair(2, 356.0 * 3600.0));

            HaulData Data = HaulData.FromResult(Result);

            Assert.NotNull(Data.Payload.GuidanceTime);
            Assert.Equal(MotionSeconds + EventSeconds, Data.Payload.GuidanceTime.TotalSeconds, precision: 3);
            Assert.Equal(100.0 * 3600.0, Data.Payload.CutFillPairs[0].RouteTimeSeconds, precision: 3);
            Assert.Equal(82.0 * 3600.0, Data.Payload.CutFillPairs[1].RouteTimeSeconds, precision: 3);
            Assert.NotNull(Data.ResolveGuidanceTime());
            Assert.Equal(
                Data.Payload.GuidanceTime.TotalSeconds,
                Data.ResolveGuidanceTime()!.TotalSeconds,
                precision: 3);

            IReadOnlyList<HaulCutFillPairDto> LivePairs = Data.ResolveCutFillPairs();
            Assert.Equal(100.0 * 3600.0, LivePairs[0].RouteTimeSeconds, precision: 3);
            Assert.Equal(82.0 * 3600.0, LivePairs[1].RouteTimeSeconds, precision: 3);

            HaulPlanSummary? Summary = HaulPlanStatistics.BuildPlanSummary(
                LivePairs,
                Data.ResolveGuidanceTime()!.TotalSeconds);
            Assert.NotNull(Summary);
            Assert.Equal(182.0, Summary.TotalTimeSeconds / 3600.0, precision: 3);
        }

        /// <summary>
        /// Builds one cut/fill pair whose route time is the retired time-model estimate.
        /// </summary>
        /// <param name="Order">1-based route number.</param>
        /// <param name="RouteTimeSeconds">Retired time-model seconds for the route.</param>
        /// <returns>Pair row with the old route time.</returns>
        private static HaulBuilder.CutFillPair Pair
            (
            int Order,
            double RouteTimeSeconds
            )
        {
            return new HaulBuilder.CutFillPair(
                Order,
                10.0,
                0.0,
                (0.0, 0.0),
                (1.0, 0.0),
                (2.0, 0.0),
                (3.0, 0.0),
                0.0,
                0.0,
                RouteTimeSeconds);
        }
    }
}
