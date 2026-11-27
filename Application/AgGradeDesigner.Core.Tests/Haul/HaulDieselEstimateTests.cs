using AgGradeDesigner.Core.Haul;

namespace AgGradeDesigner.Core.Tests.Haul
{
    /// <summary>
    /// Verifies a haul run keeps the engine diesel cost for the stats panel.
    /// </summary>
    public sealed class HaulDieselEstimateTests
    {
        /// <summary>Sample diesel cost in dollars.</summary>
        private const double SAMPLE_COST_USD = 482.00;

        /// <summary>Sample diesel volume in gallons.</summary>
        private const double SAMPLE_GALLONS = 100.0;

        /// <summary>
        /// Verifies a priced estimate is stored on the haul payload.
        /// </summary>
        [Fact]
        public void FromResult_StoresDieselCost
            (
            )
        {
            HaulBuilder.HaulBuilderResult Result = new HaulBuilder.HaulBuilderResult
            {
                DieselEstimate = Estimate(Blocked: false, SAMPLE_COST_USD)
            };

            HaulData Data = HaulData.FromResult(Result);

            Assert.Equal(SAMPLE_COST_USD, Data.Payload.DieselCost);
        }

        /// <summary>
        /// Verifies a run with no diesel estimate leaves the cost unset.
        /// </summary>
        [Fact]
        public void FromResult_LeavesDieselCostUnsetWhenEstimateIsMissing
            (
            )
        {
            HaulData Data = HaulData.FromResult(new HaulBuilder.HaulBuilderResult());

            Assert.Null(Data.Payload.DieselCost);
        }

        /// <summary>
        /// Verifies a blocked estimate is not shown as a diesel cost.
        /// </summary>
        [Fact]
        public void FromResult_LeavesDieselCostUnsetWhenEstimateIsBlocked
            (
            )
        {
            HaulBuilder.HaulBuilderResult Result = new HaulBuilder.HaulBuilderResult
            {
                DieselEstimate = Estimate(Blocked: true, SAMPLE_COST_USD)
            };

            HaulData Data = HaulData.FromResult(Result);

            Assert.Null(Data.Payload.DieselCost);
        }

        /// <summary>
        /// Builds one engine diesel estimate.
        /// </summary>
        /// <param name="Blocked">True when the engine refused to price the plan.</param>
        /// <param name="CostUsd">Total cost in dollars.</param>
        /// <returns>Estimate the haul payload can copy.</returns>
        private static HaulBuilder.DieselCost.Result Estimate
            (
            bool Blocked,
            double CostUsd
            )
        {
            return new HaulBuilder.DieselCost.Result(
                Blocked,
                Array.Empty<HaulBuilder.DieselCost.PhaseEstimate>(),
                0.0,
                0.0,
                0.0,
                SAMPLE_GALLONS,
                CostUsd,
                0.0,
                0.0,
                0.0,
                Array.Empty<string>(),
                Array.Empty<string>());
        }
    }
}
