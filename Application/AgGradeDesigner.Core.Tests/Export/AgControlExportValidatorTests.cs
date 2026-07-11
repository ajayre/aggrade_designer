using AgGradeDesigner.Core;
using AgGradeDesigner.Core.Design;
using AgGradeDesigner.Core.Export;
using AgGradeDesigner.Core.Haul;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Survey;
using AgGradeDesigner.Core.Tests.TestFixtures;

namespace AgGradeDesigner.Core.Tests.Export
{
    /// <summary>
    /// Unit tests for <see cref="AgControlExportValidator"/> (EXPORT-SPEC Phase 2).
    /// </summary>
    public sealed class AgControlExportValidatorTests
    {
        /// <summary>Main slope direction used when building a design fixture.</summary>
        private const double MAIN_SLOPE_DIRECTION_DEG = 90.0;

        /// <summary>Zero slope percent for a flat design fixture.</summary>
        private const double ZERO_SLOPE_PERCENT = 0.0;

        /// <summary>
        /// Verifies validation fails without a proposed surface.
        /// </summary>
        [Fact]
        public void Validate_WithoutDesign_ReturnsError
            (
            )
        {
            AgControlExportValidationResult Result = AgControlExportValidator.Validate(new ProjectDocument());

            Assert.False(Result.IsValid);
            Assert.Contains(AgControlExportValidator.ERROR_NO_PROPOSED_SURFACE, Result.Errors);
        }

        /// <summary>
        /// Verifies a design-only project validates with optional-haul and optional-benchmark warnings.
        /// </summary>
        [Fact]
        public void Validate_DesignOnly_IsValidWithWarnings
            (
            )
        {
            ProjectDocument Project = BuildProjectWithDesign(IncludeBenchmarks: false);
            AgControlExportValidationResult Result = AgControlExportValidator.Validate(Project);

            Assert.True(Result.IsValid);
            Assert.Contains(AgControlExportValidator.WARNING_NO_HAUL, Result.Warnings);
            Assert.Contains(AgControlExportValidator.WARNING_NO_BENCHMARKS, Result.Warnings);
        }

        /// <summary>
        /// Verifies a design with haul and benchmarks validates without the no-haul warning.
        /// </summary>
        [Fact]
        public void Validate_WithHaulAndBenchmarks_IsValid
            (
            )
        {
            ProjectDocument Project = BuildProjectWithDesign(IncludeBenchmarks: true);
            Project.HaulData = BuildMinimalHaul(Project.DesignGrid!);

            AgControlExportValidationResult Result = AgControlExportValidator.Validate(Project);

            Assert.True(Result.IsValid);
            Assert.DoesNotContain(AgControlExportValidator.WARNING_NO_HAUL, Result.Warnings);
            Assert.DoesNotContain(AgControlExportValidator.WARNING_NO_BENCHMARKS, Result.Warnings);
        }

        /// <summary>
        /// Verifies an empty design grid fails validation.
        /// </summary>
        [Fact]
        public void Validate_EmptyDesignGrid_ReturnsError
            (
            )
        {
            ProjectDocument Project = new ProjectDocument
            {
                DesignParameters = new DesignParameters(),
                DesignGrid = new DesignGrid()
            };

            AgControlExportValidationResult Result = AgControlExportValidator.Validate(Project);

            Assert.False(Result.IsValid);
            Assert.Contains(AgControlExportValidator.ERROR_NO_VALID_BINS, Result.Errors);
        }

        /// <summary>
        /// Verifies haul with passes but no guidance yields a warning.
        /// </summary>
        [Fact]
        public void Validate_HaulWithoutGuidance_Warns
            (
            )
        {
            ProjectDocument Project = BuildProjectWithDesign(IncludeBenchmarks: true);
            HaulData Haul = BuildMinimalHaul(Project.DesignGrid!);
            Haul.Payload.GuidancePoints.Clear();
            Project.HaulData = Haul;

            AgControlExportValidationResult Result = AgControlExportValidator.Validate(Project);

            Assert.True(Result.IsValid);
            Assert.Contains(AgControlExportValidator.WARNING_NO_GUIDANCE, Result.Warnings);
        }

        /// <summary>
        /// Builds a project with survey and design from the minimal AGS fixture.
        /// </summary>
        /// <param name="IncludeBenchmarks">When false, clears survey benchmarks.</param>
        /// <returns>Project with a proposed surface.</returns>
        static private ProjectDocument BuildProjectWithDesign
            (
            bool IncludeBenchmarks
            )
        {
            string AgsPath = TempFile.Write(".ags", SurveySamples.MINIMAL_AGS);
            try
            {
                SurveyModel Survey = new SurveyModel();
                Survey.LoadFromAgs(AgsPath);
                if (!IncludeBenchmarks)
                    Survey.Benchmarks.Clear();

                DesignParameters Parameters = new DesignParameters
                {
                    MainSlopeDirectionDeg = MAIN_SLOPE_DIRECTION_DEG,
                    MainSlopePercent = ZERO_SLOPE_PERCENT,
                    CrossSlopePercent = ZERO_SLOPE_PERCENT,
                    CutFillRatio = FieldConstants.CUT_FILL_RATIO
                };

                FieldCreator Creator = new FieldCreator();
                (DesignGrid Grid, _) = Creator.CreateDesign(Survey, Parameters);

                return new ProjectDocument
                {
                    Survey = Survey,
                    DesignParameters = Parameters,
                    DesignGrid = Grid
                };
            }
            finally
            {
                TempFile.DeleteIfExists(AgsPath);
            }
        }

        /// <summary>
        /// Builds a one-pass haul payload without requiring guidance depth details.
        /// </summary>
        /// <param name="Grid">Design grid for cut-start placement.</param>
        /// <returns>Minimal valid haul data.</returns>
        static private HaulData BuildMinimalHaul
            (
            DesignGrid Grid
            )
        {
            GridBinData? SeedBin = Grid.Bins.Find(B => B.Valid);
            Assert.NotNull(SeedBin);

            double EastingM = Grid.OriginEastingM + (SeedBin!.X + FieldConstants.BIN_CENTER_OFFSET) * Grid.BinSizeM;
            double NorthingM = Grid.OriginNorthingM + (SeedBin.Y + FieldConstants.BIN_CENTER_OFFSET) * Grid.BinSizeM;

            HaulData Data = new HaulData();
            Data.Payload.OrderedPasses.Add(new HaulPassDto
            {
                CutStartX = EastingM,
                CutStartY = NorthingM,
                CutEndX = EastingM,
                CutEndY = NorthingM,
                FillStartX = EastingM,
                FillStartY = NorthingM,
                FillEndX = EastingM,
                FillEndY = NorthingM
            });
            Data.Payload.GuidancePoints.Add(new HaulGuidancePointDto
            {
                Seq = 1,
                PassNo = 1,
                Action = AgControlConstants.GUIDANCE_ACTION_CUT,
                Blade = "DOWN",
                X = EastingM,
                Y = NorthingM,
                Lat = SeedBin.CentroidLat,
                Lon = SeedBin.CentroidLon,
                DepthMm = 25.0,
                HeadingDeg = 0.0
            });

            return Data;
        }
    }
}
