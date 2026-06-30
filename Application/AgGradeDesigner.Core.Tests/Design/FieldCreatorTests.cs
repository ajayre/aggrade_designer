using AgGradeDesigner.Core;
using AgGradeDesigner.Core.Design;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Survey;
using AgGradeDesigner.Core.Tests.TestFixtures;

namespace AgGradeDesigner.Core.Tests.Design
{
    /// <summary>
    /// Unit tests for <see cref="FieldCreator"/> design grid generation from AGS surveys.
    /// </summary>
    public sealed class FieldCreatorTests
    {
        /// <summary>
        /// Verifies design creation produces a valid grid with proposed surface elevations.
        /// </summary>
        [Fact]
        public void CreateDesign_ProducesValidGridWithProposedSurface
            (
            )
        {
            string Path = TempFile.Write(".ags", SurveySamples.MINIMAL_AGS);
            try
            {
                SurveyModel Survey = new SurveyModel();
                Survey.LoadFromAgs(Path);
                DesignParameters Parameters = new DesignParameters
                {
                    MainSlopeDirectionDeg = 90.0,
                    MainSlopePercent = 0.0,
                    CrossSlopePercent = 0.0,
                    CutFillRatio = 1.2
                };

                FieldCreator Creator = new FieldCreator();
                (DesignGrid Grid, FieldCreator.Statistics Stats) = Creator.CreateDesign(Survey, Parameters);

                // Flat-slope design must still populate dimensions, bins, and area statistics.
                Assert.True(Grid.Width > 0);
                Assert.True(Grid.Height > 0);
                Assert.True(Grid.Bins.Count > 0);
                Assert.True(Grid.Bins.Exists(B => B.Valid && (B.ProposedM != FieldConstants.BIN_NO_DATA_SENTINEL)));
                Assert.True(Stats.TotalAreaAcres > 0);
            }
            finally
            {
                TempFile.DeleteIfExists(Path);
            }
        }

        /// <summary>
        /// Verifies design creation succeeds from a loaded minimal AGS fixture.
        /// </summary>
        [Fact]
        public void CreateDesign_FromLoadedAgs_Succeeds
            (
            )
        {
            string Path = TempFile.Write(".ags", SurveySamples.MINIMAL_AGS);
            try
            {
                SurveyModel Survey = new SurveyModel();
                Survey.LoadFromAgs(Path);
                DesignParameters Parameters = new DesignParameters
                {
                    MainSlopeDirectionDeg = 0,
                    MainSlopePercent = 0.1,
                    CrossSlopePercent = 0.0,
                    CutFillRatio = 1.2
                };

                FieldCreator Creator = new FieldCreator();
                (DesignGrid Grid, _) = Creator.CreateDesign(Survey, Parameters);
                // Mild slope parameters should still yield at least one valid bin.
                Assert.True(Grid.Bins.Exists(B => B.Valid));
            }
            finally
            {
                TempFile.DeleteIfExists(Path);
            }
        }

        /// <summary>
        /// Verifies empty surveys throw when passed to design creation.
        /// </summary>
        [Fact]
        public void CreateDesign_ThrowsWhenSurveyEmpty
            (
            )
        {
            FieldCreator Creator = new FieldCreator();
            // Empty survey input must fail fast before grid construction.
            Assert.Throws<InvalidDataException>(() =>
                Creator.CreateDesign(new SurveyModel(), new DesignParameters()));
        }
    }
}
