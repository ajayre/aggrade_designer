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
        private const int MAX_PARENT_WALK = 8;

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
            Assert.InRange(ValidCount, Total / 10, (Total * 9) / 10);
            Assert.True(Sampler.MaxElevation - Sampler.MinElevation > 0.05);
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
            Assert.True(Timer.ElapsedMilliseconds < 5000, $"Build took {Timer.ElapsedMilliseconds}ms");
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
            Assert.True(Sampler.MaxElevation - Sampler.MinElevation > 0.2);
        }
    }
}
