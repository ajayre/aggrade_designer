using AgGradeDesigner.Core.Survey;

using AgGradeDesigner.Core.Tests.TestFixtures;

namespace AgGradeDesigner.Core.Tests.Survey
{
    /// <summary>
    /// Unit tests for <see cref="SurveyModel"/> AGS load, save, and merge behaviour.
    /// </summary>
    public sealed class SurveyModelAgsTests
    {
        /// <summary>
        /// Verifies AGS load parses master benchmark, boundary, and interior points.
        /// </summary>
        [Fact]
        public void LoadFromAgs_ParsesMasterBoundaryAndInterior
            (
            )
        {
            string Path = TempFile.Write(".ags", SurveySamples.MINIMAL_AGS);
            try
            {
                SurveyModel Survey = new SurveyModel();
                Survey.LoadFromAgs(Path);

                // Minimal fixture defines one benchmark, three boundary, and six interior points.
                Assert.Single(Survey.Benchmarks);
                Assert.Equal("0MB", Survey.Benchmarks[0].Name);
                Assert.Equal(3, Survey.BoundaryPoints.Count);
                Assert.Equal(6, Survey.InteriorPoints.Count);
            }
            finally
            {
                TempFile.DeleteIfExists(Path);
            }
        }

        /// <summary>
        /// Verifies AGS load skips a leading CSV header row when present.
        /// </summary>
        [Fact]
        public void LoadFromAgs_SkipsHeaderRow
            (
            )
        {
            string Content = "Latitude,Longitude,Elevation,Code\n" + SurveySamples.MINIMAL_AGS;
            string Path = TempFile.Write(".ags", Content);
            try
            {
                SurveyModel Survey = new SurveyModel();
                Survey.LoadFromAgs(Path);
                // Header row must not be interpreted as a survey point.
                Assert.Single(Survey.Benchmarks);
            }
            finally
            {
                TempFile.DeleteIfExists(Path);
            }
        }

        /// <summary>
        /// Verifies save and reload preserves benchmark and point counts.
        /// </summary>
        [Fact]
        public void SaveToAgs_AndReload_PreservesPoints
            (
            )
        {
            string LoadPath = TempFile.Write(".ags", SurveySamples.MINIMAL_AGS);
            string SavePath = TempFile.Write(".ags2", "");
            try
            {
                SurveyModel Survey = new SurveyModel();
                Survey.LoadFromAgs(LoadPath);
                Survey.SaveToAgs(SavePath);

                SurveyModel Reloaded = new SurveyModel();
                Reloaded.LoadFromAgs(SavePath);

                // Round-trip CSV must preserve topology list cardinalities.
                Assert.Equal(Survey.Benchmarks.Count, Reloaded.Benchmarks.Count);
                Assert.Equal(Survey.BoundaryPoints.Count, Reloaded.BoundaryPoints.Count);
                Assert.Equal(Survey.InteriorPoints.Count, Reloaded.InteriorPoints.Count);
            }
            finally
            {
                TempFile.DeleteIfExists(LoadPath);
                TempFile.DeleteIfExists(SavePath);
            }
        }

        /// <summary>
        /// Verifies auto-load detects AGS files by extension.
        /// </summary>
        [Fact]
        public void LoadAuto_DetectsAgsByExtension
            (
            )
        {
            string Path = TempFile.Write(".ags", SurveySamples.MINIMAL_AGS);
            try
            {
                SurveyModel Survey = new SurveyModel();
                Survey.LoadAuto(Path);
                // Extension-based routing must load the AGS parser path.
                Assert.Single(Survey.Benchmarks);
            }
            finally
            {
                TempFile.DeleteIfExists(Path);
            }
        }

        /// <summary>
        /// Verifies clone snapshot produces an independent copy of interior points.
        /// </summary>
        [Fact]
        public void CloneSnapshot_IsIndependentCopy
            (
            )
        {
            string Path = TempFile.Write(".ags", SurveySamples.MINIMAL_AGS);
            try
            {
                SurveyModel Survey = new SurveyModel();
                Survey.LoadFromAgs(Path);
                SurveyModel Copy = Survey.CloneSnapshot();
                Copy.InteriorPoints.Clear();
                // Source interior list must remain populated after mutating the clone.
                Assert.NotEmpty(Survey.InteriorPoints);
            }
            finally
            {
                TempFile.DeleteIfExists(Path);
            }
        }

        /// <summary>
        /// Verifies merge from appends points from the source survey.
        /// </summary>
        [Fact]
        public void MergeFrom_AppendsPoints
            (
            )
        {
            string Path = TempFile.Write(".ags", SurveySamples.MINIMAL_AGS);
            try
            {
                SurveyModel Target = new SurveyModel();
                Target.LoadFromAgs(Path);
                int Before = Target.InteriorPoints.Count;

                SurveyModel Source = new SurveyModel();
                Source.InteriorPoints.Add(new Core.Models.TopologyPoint
                {
                    Latitude = 40.01,
                    Longitude = -93.49,
                    ExistingElevation = 99.0,
                    Code = "3GRD"
                });
                Target.MergeFrom(Source);
                // Merge must append exactly one interior point from the donor survey.
                Assert.Equal(Before + 1, Target.InteriorPoints.Count);
            }
            finally
            {
                TempFile.DeleteIfExists(Path);
            }
        }
    }
}
