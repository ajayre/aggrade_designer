using AgGradeDesigner.Core.Design;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Survey;

namespace AgGradeDesigner.Core.Tests.Design
{
    /// <summary>
    /// Integration tests for <see cref="FieldCreator"/> against the Shop multiplane sample file.
    /// </summary>
    public sealed class FieldCreatorShopIntegrationTests
    {
        /// <summary>
        /// Main slope direction deg used by this component.
        /// </summary>

        private const double MAIN_SLOPE_DIRECTION_DEG = 90;
        /// <summary>
        /// Main slope percent used by this component.
        /// </summary>

        private const double MAIN_SLOPE_PERCENT = 0.15;
        /// <summary>
        /// Cross slope percent used by this component.
        /// </summary>

        private const double CROSS_SLOPE_PERCENT = 0.05;
        /// <summary>
        /// Cut fill ratio used by this component.
        /// </summary>

        private const double CUT_FILL_RATIO = 1.2;
        /// <summary>
        /// Min valid bin count used by this component.
        /// </summary>

        private const int MIN_VALID_BIN_COUNT = 100;
        /// <summary>
        /// Min total area acres used by this component.
        /// </summary>

        private const double MIN_TOTAL_AREA_ACRES = 0.1;

        /// <summary>
        /// Absolute path to the Shop multiplane sample on the developer machine.
        /// </summary>
        static private string ShopPath =>
            @"C:\Users\andy\OneDrive\Documents\AgGrade\AI Field Design\Shop.txt";

        /// <summary>
        /// Verifies design creation from Shop multiplane data yields non-trivial statistics.
        /// </summary>
        [Fact]
        public void CreateDesign_ShopMultiplane_ProducesStatistics
            (
            )
        {
            if (!File.Exists(ShopPath))
                return;

            SurveyModel Survey = new SurveyModel();
            Survey.LoadFromMultiplane(ShopPath);

            FieldCreator Creator = new FieldCreator();
            (DesignGrid Grid, FieldCreator.Statistics Stats) = Creator.CreateDesign(Survey, new DesignParameters
            {
                MainSlopeDirectionDeg = MAIN_SLOPE_DIRECTION_DEG,
                MainSlopePercent = MAIN_SLOPE_PERCENT,
                CrossSlopePercent = CROSS_SLOPE_PERCENT,
                CutFillRatio = CUT_FILL_RATIO
            });

            // Shop fixture should produce a substantial valid grid with positive area.
            Assert.True(Grid.Bins.Count(B => B.Valid) > MIN_VALID_BIN_COUNT);
            Assert.True(Stats.TotalAreaAcres > MIN_TOTAL_AREA_ACRES);
            Assert.True(Stats.CompositeSlopePercent >= 0);
        }
    }
}
