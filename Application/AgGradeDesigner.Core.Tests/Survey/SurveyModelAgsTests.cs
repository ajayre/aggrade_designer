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
        /// Expected value used when asserting benchmark count.
        /// </summary>

        private const int EXPECTED_BENCHMARK_COUNT = 1;
        /// <summary>
        /// Expected value used when asserting boundary count.
        /// </summary>

        private const int EXPECTED_BOUNDARY_COUNT = 3;
        /// <summary>
        /// Expected value used when asserting interior count.
        /// </summary>

        private const int EXPECTED_INTERIOR_COUNT = 6;
        /// <summary>
        /// Merge point increment used by this component.
        /// </summary>

        private const int MERGE_POINT_INCREMENT = 1;
        /// <summary>
        /// Merge test lat used by this component.
        /// </summary>

        private const double MERGE_TEST_LAT = 40.01;
        /// <summary>
        /// Merge test lon used by this component.
        /// </summary>

        private const double MERGE_TEST_LON = -93.49;
        /// <summary>
        /// Merge test elevation m used by this component.
        /// </summary>

        private const double MERGE_TEST_ELEVATION_M = 99.0;

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
                Assert.Equal(EXPECTED_BENCHMARK_COUNT, Survey.Benchmarks.Count);
                Assert.Equal("0MB", Survey.Benchmarks[0].Name);
                Assert.Equal(EXPECTED_BOUNDARY_COUNT, Survey.BoundaryPoints.Count);
                Assert.Equal(EXPECTED_INTERIOR_COUNT, Survey.InteriorPoints.Count);
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
                Assert.Equal(EXPECTED_BENCHMARK_COUNT, Survey.Benchmarks.Count);
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
                Assert.Equal(EXPECTED_BENCHMARK_COUNT, Survey.Benchmarks.Count);
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
                    Latitude = MERGE_TEST_LAT,
                    Longitude = MERGE_TEST_LON,
                    ExistingElevation = MERGE_TEST_ELEVATION_M,
                    Code = "3GRD"
                });
                Target.MergeFrom(Source);
                // Merge must append exactly one interior point from the donor survey.
                Assert.Equal(Before + MERGE_POINT_INCREMENT, Target.InteriorPoints.Count);
            }
            finally
            {
                TempFile.DeleteIfExists(Path);
            }
        }
    }
}
