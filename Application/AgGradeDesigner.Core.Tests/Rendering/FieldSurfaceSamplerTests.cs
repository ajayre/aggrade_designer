using System.Diagnostics;

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
    }
}
