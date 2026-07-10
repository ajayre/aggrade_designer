using AgGradeDesigner.Core.Haul;
using AgGradeDesigner.Core.Units;

namespace AgGradeDesigner.Core.Tests.Haul
{
    /// <summary>
    /// Unit tests for haul plan statistics formatting and route scrub calculations.
    /// </summary>
    public sealed class HaulPlanStatisticsTests
    {
        /// <summary>
        /// Verifies plan totals are derived from the final route cumulative values.
        /// </summary>
        [Fact]
        public void BuildPlanSummary_UsesFinalRouteTotals
            (
            )
        {
            List<HaulCutFillPairDto> Pairs =
            [
                new HaulCutFillPairDto
                {
                    Order = 1,
                    LoadBcy = 10.0,
                    TotalCutBcyBefore = 0.0,
                    TotalFillBcyBefore = 0.0,
                    RouteTimeSeconds = 1800.0
                },
                new HaulCutFillPairDto
                {
                    Order = 2,
                    LoadBcy = 15.0,
                    TotalCutBcyBefore = 10.0,
                    TotalFillBcyBefore = 10.0,
                    RouteTimeSeconds = 3600.0
                }
            ];

            HaulPlanSummary? Summary = HaulPlanStatistics.BuildPlanSummary(Pairs);

            Assert.NotNull(Summary);
            Assert.Equal(25.0, Summary.TotalCutBcy, precision: 6);
            Assert.Equal(25.0, Summary.TotalFillBcy, precision: 6);
            Assert.Equal(5400.0, Summary.TotalTimeSeconds, precision: 6);
            Assert.Equal(2, Summary.RouteCount);
        }

        /// <summary>
        /// Verifies route statistics reflect cumulative values before the active scrub index.
        /// </summary>
        [Fact]
        public void BuildRouteStatistics_ReportsBeforeRouteValues
            (
            )
        {
            List<HaulCutFillPairDto> Pairs =
            [
                new HaulCutFillPairDto
                {
                    Order = 1,
                    LoadBcy = 10.0,
                    TotalCutBcyBefore = 0.0,
                    TotalFillBcyBefore = 0.0,
                    RouteTimeSeconds = 1800.0
                },
                new HaulCutFillPairDto
                {
                    Order = 2,
                    LoadBcy = 15.0,
                    TotalCutBcyBefore = 10.0,
                    TotalFillBcyBefore = 10.0,
                    RouteTimeSeconds = 3600.0
                }
            ];

            HaulPlanSummary Summary = HaulPlanStatistics.BuildPlanSummary(Pairs)!;
            HaulRouteStatistics? RouteStats = HaulPlanStatistics.BuildRouteStatistics(Pairs, 1, Summary);

            Assert.NotNull(RouteStats);
            Assert.Equal(1800.0, RouteStats.TimeBeforeSeconds, precision: 6);
            Assert.Equal(10.0, RouteStats.CutBeforeBcy, precision: 6);
            Assert.Equal(10.0, RouteStats.FillBeforeBcy, precision: 6);
            Assert.Equal(15.0, RouteStats.LoadBcy, precision: 6);
            Assert.Equal(3600.0, RouteStats.RouteTimeSeconds, precision: 6);
            Assert.Equal(0.4, RouteStats.CutCompleteFraction, precision: 6);
        }

        /// <summary>
        /// Verifies imperial bank volumes are shown as cubic yards.
        /// </summary>
        [Fact]
        public void FormatBankVolume_Imperial_UsesCubicYards
            (
            )
        {
            string Label = HaulPlanStatistics.FormatBankVolume(1234.5, DisplayUnitSystem.Imperial);

            Assert.Equal("1,234.5 CY", Label);
        }

        /// <summary>
        /// Verifies metric bank volumes are converted to cubic metres.
        /// </summary>
        [Fact]
        public void FormatBankVolume_Metric_UsesCubicMetres
            (
            )
        {
            string Label = HaulPlanStatistics.FormatBankVolume(
                HaulBuilder.Constants.M3_TO_CY,
                DisplayUnitSystem.Metric);

            Assert.Equal("1.0 m³", Label);
        }
    }
}
