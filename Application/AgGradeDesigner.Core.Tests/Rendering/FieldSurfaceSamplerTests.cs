using System.Diagnostics;

using AgGradeDesigner.Core.Design;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Core.Survey;

namespace AgGradeDesigner.Core.Tests.Rendering
{
    /// <summary>
    /// Unit tests for <see cref="FieldSurfaceSampler"/> boundary-aware surface construction.
    /// </summary>
    public sealed class FieldSurfaceSamplerTests
    {
        /// <summary>
        /// Max parent walk used by this component.
        /// </summary>

        private const int MAX_PARENT_WALK = 8;
        /// <summary>
        /// Valid fraction denominator used by this component.
        /// </summary>

        private const int VALID_FRACTION_DENOMINATOR = 10;
        /// <summary>
        /// Valid fraction numerator used by this component.
        /// </summary>

        private const int VALID_FRACTION_NUMERATOR = 9;
        /// <summary>
        /// Min elevation relief m used by this component.
        /// </summary>

        private const double MIN_ELEVATION_RELIEF_M = 0.05;
        /// <summary>
        /// Min topography relief m used by this component.
        /// </summary>

        private const double MIN_TOPOGRAPHY_RELIEF_M = 0.2;
        /// <summary>
        /// Max build time ms used by this component.
        /// </summary>

        private const int MAX_BUILD_TIME_MS = 5000;

        /// <summary>
        /// Walks parent directories from the test base path to locate FakeField_01.ags.
        /// </summary>
        /// <returns>Absolute path when found; otherwise null.</returns>
        static private string? FindFakeFieldAgs
            (
            )
        {
            string? Dir = AppContext.BaseDirectory;
            for (int Iter = 0; (Iter < MAX_PARENT_WALK) && (Dir != null); Iter++)
            {
                string Candidate = Path.Combine(Dir, "Test Data", "FakeField_01.ags");
                if (File.Exists(Candidate))
                    return Candidate;
                Dir = Path.GetDirectoryName(Dir);
            }

            return null;
        }

        /// <summary>
        /// Verifies FakeField sampling respects boundary mask rather than full rectangle fill.
        /// </summary>
        [Fact]
        public void BuildFromSurvey_FakeFieldAgs_UsesBoundaryNotFullRectangle
            (
            )
        {
            string? Path = FindFakeFieldAgs();
            if (Path == null)
                return;

            SurveyModel Survey = new SurveyModel();
            Survey.LoadAuto(Path);

            FieldSurfaceSampler Sampler = new FieldSurfaceSampler();
            Sampler.BuildFromProject(new ProjectDocument { Survey = Survey });

            Assert.True(Sampler.SampleWidth > 0);
            int ValidCount = Sampler.Valid.Count(V => V);
            int Total = Sampler.Valid.Length;
            // Valid bin fraction should reflect boundary clipping, not a full rectangle.
            Assert.InRange(ValidCount, Total / VALID_FRACTION_DENOMINATOR, (Total * VALID_FRACTION_NUMERATOR) / VALID_FRACTION_DENOMINATOR);
            Assert.True(Sampler.MaxElevation - Sampler.MinElevation > MIN_ELEVATION_RELIEF_M);
        }

        /// <summary>
        /// Verifies FakeField surface build completes within a reasonable time budget.
        /// </summary>
        [Fact]
        public void BuildFromSurvey_FakeFieldAgs_CompletesQuickly
            (
            )
        {
            string? Path = FindFakeFieldAgs();
            if (Path == null)
                return;

            SurveyModel Survey = new SurveyModel();
            Survey.LoadAuto(Path);

            FieldSurfaceSampler Sampler = new FieldSurfaceSampler();
            Stopwatch Timer = Stopwatch.StartNew();
            Sampler.BuildFromProject(new ProjectDocument { Survey = Survey });
            Timer.Stop();

            // Surface build must stay under five seconds on developer hardware.
            Assert.True(Sampler.SampleWidth > 0);
            Assert.True(Timer.ElapsedMilliseconds < MAX_BUILD_TIME_MS, $"Build took {Timer.ElapsedMilliseconds}ms");
        }

        /// <summary>
        /// Elevation comparison precision for existing-surface preservation checks.
        /// </summary>
        private const int EXISTING_SURFACE_PRESERVATION_PRECISION = 6;

        /// <summary>
        /// Verifies FakeField sampling exposes meaningful elevation relief.
        /// </summary>
        [Fact]
        public void BuildFromSurvey_FakeFieldAgs_HasElevationRelief
            (
            )
        {
            string? Path = FindFakeFieldAgs();
            if (Path == null)
                return;

            SurveyModel Survey = new SurveyModel();
            Survey.LoadAuto(Path);

            FieldSurfaceSampler Sampler = new FieldSurfaceSampler();
            Sampler.BuildFromProject(new ProjectDocument { Survey = Survey });

            // Fake field topography should span more than twenty centimetres vertically.
            Assert.True(Sampler.MaxElevation - Sampler.MinElevation > MIN_TOPOGRAPHY_RELIEF_M);
        }

        /// <summary>
        /// Maximum absolute cut/fill allowed when proposed matches existing topology.
        /// </summary>
        private const double MAX_USE_EXISTING_TOPOLOGY_CUT_FILL_M = 1e-4;

        /// <summary>
        /// Verifies adding a design does not alter the survey-based existing surface raster.
        /// </summary>
        [Fact]
        public void BuildFromProject_WithDesign_PreservesSurveyExistingSurface
            (
            )
        {
            string? Path = FindFakeFieldAgs();
            if (Path == null)
                return;

            SurveyModel Survey = new SurveyModel();
            Survey.LoadAuto(Path);

            FieldSurfaceSampler SurveyOnly = new FieldSurfaceSampler();
            SurveyOnly.BuildFromProject(new ProjectDocument { Survey = Survey });

            FieldCreator Creator = new FieldCreator();
            (DesignGrid Grid, _) = Creator.CreateDesign(
                Survey,
                new DesignParameters { UseExistingTopology = true });

            ProjectDocument WithDesign = new ProjectDocument
            {
                Survey = Survey,
                DesignGrid = Grid,
                DesignParameters = new DesignParameters { UseExistingTopology = true }
            };

            FieldSurfaceSampler WithDesignSurface = new FieldSurfaceSampler();
            WithDesignSurface.BuildFromProject(WithDesign);

            Assert.Equal(SurveyOnly.SampleWidth, WithDesignSurface.SampleWidth);
            Assert.Equal(SurveyOnly.SampleHeight, WithDesignSurface.SampleHeight);
            for (int Idx = 0; Idx < SurveyOnly.Valid.Length; Idx++)
            {
                Assert.Equal(SurveyOnly.Valid[Idx], WithDesignSurface.Valid[Idx]);
                if (!SurveyOnly.Valid[Idx])
                    continue;

                Assert.Equal(
                    SurveyOnly.Existing[Idx],
                    WithDesignSurface.Existing[Idx],
                    EXISTING_SURFACE_PRESERVATION_PRECISION);
            }
        }

        /// <summary>
        /// Verifies use-existing-topology proposed overlay matches the survey existing surface.
        /// </summary>
        [Fact]
        public void BuildFromProject_UseExistingTopology_ProposedMatchesExisting
            (
            )
        {
            string? Path = FindFakeFieldAgs();
            if (Path == null)
                return;

            SurveyModel Survey = new SurveyModel();
            Survey.LoadAuto(Path);

            FieldCreator Creator = new FieldCreator();
            (DesignGrid Grid, _) = Creator.CreateDesign(
                Survey,
                new DesignParameters { UseExistingTopology = true });

            ProjectDocument WithDesign = new ProjectDocument
            {
                Survey = Survey,
                DesignGrid = Grid,
                DesignParameters = new DesignParameters { UseExistingTopology = true }
            };

            FieldSurfaceSampler Surface = new FieldSurfaceSampler();
            Surface.BuildFromProject(WithDesign);

            for (int Idx = 0; Idx < Surface.Valid.Length; Idx++)
            {
                if (!Surface.Valid[Idx])
                    continue;

                double CutFill = Surface.Existing[Idx] - Surface.Proposed[Idx];
                Assert.True(
                    Math.Abs(CutFill) <= MAX_USE_EXISTING_TOPOLOGY_CUT_FILL_M,
                    $"Cut/fill {CutFill:F6} m at index {Idx} should be zero for use-existing-topology.");
                Assert.Equal(
                    Surface.Existing[Idx],
                    Surface.Proposed[Idx],
                    EXISTING_SURFACE_PRESERVATION_PRECISION);
            }
        }
    }
}
