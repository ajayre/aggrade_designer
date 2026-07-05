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
        /// Main slope direction deg used by this component.
        /// </summary>

        private const double MAIN_SLOPE_DIRECTION_DEG = 90.0;
        /// <summary>
        /// Zero slope percent used by this component.
        /// </summary>

        private const double ZERO_SLOPE_PERCENT = 0.0;
        /// <summary>
        /// Mild main slope percent used by this component.
        /// </summary>

        private const double MILD_MAIN_SLOPE_PERCENT = 0.1;
        /// <summary>
        /// Default cut fill ratio used by this component.
        /// </summary>

        private const double DEFAULT_CUT_FILL_RATIO = FieldConstants.CUT_FILL_RATIO;

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
                    MainSlopeDirectionDeg = MAIN_SLOPE_DIRECTION_DEG,
                    MainSlopePercent = ZERO_SLOPE_PERCENT,
                    CrossSlopePercent = ZERO_SLOPE_PERCENT,
                    CutFillRatio = DEFAULT_CUT_FILL_RATIO
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
                    MainSlopePercent = MILD_MAIN_SLOPE_PERCENT,
                    CrossSlopePercent = ZERO_SLOPE_PERCENT,
                    CutFillRatio = DEFAULT_CUT_FILL_RATIO
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

        /// <summary>
        /// Verifies use-existing-topology mode copies existing elevations into proposed bins.
        /// </summary>
        [Fact]
        public void CreateDesign_UseExistingTopology_CopiesExistingToProposed
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
                    UseExistingTopology = true,
                    MainSlopePercent = MILD_MAIN_SLOPE_PERCENT
                };

                FieldCreator Creator = new FieldCreator();
                (DesignGrid Grid, FieldCreator.Statistics Stats) = Creator.CreateDesign(Survey, Parameters);

                foreach (GridBinData Bin in Grid.Bins)
                {
                    if (!Bin.Valid)
                        continue;

                    Assert.Equal(Bin.ExistingM, Bin.ProposedM, precision: 6);
                }

                Assert.Equal(0.0, Grid.TotalCutCy, precision: 6);
                Assert.Equal(0.0, Grid.TotalFillCy, precision: 6);
                Assert.Equal(0.0, Stats.TotalCutCY, precision: 6);
                Assert.Equal(0.0, Stats.TotalFillCY, precision: 6);
            }
            finally
            {
                TempFile.DeleteIfExists(Path);
            }
        }
    }
}
