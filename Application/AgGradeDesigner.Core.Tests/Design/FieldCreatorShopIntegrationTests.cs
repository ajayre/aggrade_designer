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
                MainSlopeDirectionDeg = 90,
                MainSlopePercent = 0.15,
                CrossSlopePercent = 0.05,
                CutFillRatio = 1.2
            });

            // Shop fixture should produce a substantial valid grid with positive area.
            Assert.True(Grid.Bins.Count(B => B.Valid) > 100);
            Assert.True(Stats.TotalAreaAcres > 0.1);
            Assert.True(Stats.CompositeSlopePercent >= 0);
        }
    }
}
