using AgGradeDesigner.Core.Survey;

using AgGradeDesigner.Core.Tests.TestFixtures;

namespace AgGradeDesigner.Core.Tests.Survey
{
    /// <summary>
    /// Unit tests for <see cref="SurveyModel"/> multiplane load and content detection.
    /// </summary>
    public sealed class SurveyModelMultiplaneTests
    {
        /// <summary>
        /// Min benchmark count used by this component.
        /// </summary>

        private const int MIN_BENCHMARK_COUNT = 1;
        /// <summary>
        /// Min boundary count used by this component.
        /// </summary>

        private const int MIN_BOUNDARY_COUNT = 1;
        /// <summary>
        /// Min interior count used by this component.
        /// </summary>

        private const int MIN_INTERIOR_COUNT = 1;
        /// <summary>
        /// Multiplane header elevation ft used by this component.
        /// </summary>

        private const double MULTIPLANE_HEADER_ELEVATION_FT = 100.0;
        /// <summary>
        /// Multiplane header zero used by this component.
        /// </summary>

        private const double MULTIPLANE_HEADER_ZERO = 0.0;
        /// <summary>
        /// Multiplane point offset x ft used by this component.
        /// </summary>

        private const double MULTIPLANE_POINT_OFFSET_X_FT = 10.00;
        /// <summary>
        /// Multiplane point offset y ft used by this component.
        /// </summary>

        private const double MULTIPLANE_POINT_OFFSET_Y_FT = 20.00;
        /// <summary>
        /// Multiplane boundary elevation ft used by this component.
        /// </summary>

        private const double MULTIPLANE_BOUNDARY_ELEVATION_FT = 100.500;
        /// <summary>
        /// Multiplane interior elevation ft used by this component.
        /// </summary>

        private const double MULTIPLANE_INTERIOR_ELEVATION_FT = 100.600;
        /// <summary>
        /// Multiplane first point id used by this component.
        /// </summary>

        private const int MULTIPLANE_FIRST_POINT_ID = 2;
        /// <summary>
        /// Multiplane second point id used by this component.
        /// </summary>

        private const int MULTIPLANE_SECOND_POINT_ID = 3;
        /// <summary>
        /// Multiplane point offset increment ft used by this component.
        /// </summary>

        private const double MULTIPLANE_POINT_OFFSET_INCREMENT_FT = 5.00;
        /// <summary>
        /// Multiplane header lat text used by this component.
        /// </summary>

        private const string MULTIPLANE_HEADER_LAT_TEXT = "N36:26:51.619";
        /// <summary>
        /// Multiplane header lon text used by this component.
        /// </summary>

        private const string MULTIPLANE_HEADER_LON_TEXT = "W90:43:26.085";

        /// <summary>
        /// Absolute path to the Shop multiplane sample on the developer machine.
        /// </summary>
        static private string ShopPath =>
            @"C:\Users\andy\OneDrive\Documents\AgGrade\AI Field Design\Shop.txt";

        /// <summary>
        /// Verifies Shop multiplane file loads benchmarks and topology points when present.
        /// </summary>
        [Fact]
        public void LoadFromMultiplane_ShopFile_LoadsBenchmarksAndPoints
            (
            )
        {
            if (!File.Exists(ShopPath))
            {
                return;
            }

            SurveyModel Survey = new SurveyModel();
            Survey.LoadFromMultiplane(ShopPath);

            // Shop fixture must expose at least one benchmark and some topology points.
            Assert.True(Survey.Benchmarks.Count >= MIN_BENCHMARK_COUNT);
            Assert.Contains(Survey.Benchmarks, B => B.Name.Equals("MB", StringComparison.OrdinalIgnoreCase));
            Assert.True((Survey.InteriorPoints.Count > 0) || (Survey.BoundaryPoints.Count > 0));
        }

        /// <summary>
        /// Verifies multiplane content detection recognises the benchmark header row.
        /// </summary>
        [Fact]
        public void IsMultiplaneContent_DetectsHeader
            (
            )
        {
            string Path = TempFile.Write(".dat",
                $"0001\t{MULTIPLANE_HEADER_ZERO}\t{MULTIPLANE_HEADER_ZERO}\t{MULTIPLANE_HEADER_ELEVATION_FT}\tMB {MULTIPLANE_HEADER_LAT_TEXT} / {MULTIPLANE_HEADER_LON_TEXT}\t{MULTIPLANE_HEADER_ZERO}\n");
            try
            {
                // Tab-delimited benchmark row must classify as multiplane content.
                Assert.True(SurveyModel.IsMultiplaneContent(Path));
            }
            finally
            {
                TempFile.DeleteIfExists(Path);
            }
        }

        /// <summary>
        /// Verifies auto-load detects multiplane format by file content.
        /// </summary>
        [Fact]
        public void LoadAuto_DetectsMultiplaneByContent
            (
            )
        {
            string Path = TempFile.Write(".dat",
                $"""
                0001	{MULTIPLANE_HEADER_ZERO:F3}	{MULTIPLANE_HEADER_ZERO:F3}	{MULTIPLANE_HEADER_ELEVATION_FT:F3}	MB {MULTIPLANE_HEADER_LAT_TEXT} / {MULTIPLANE_HEADER_LON_TEXT}	{MULTIPLANE_HEADER_ZERO:F3}
                {MULTIPLANE_FIRST_POINT_ID}	{MULTIPLANE_POINT_OFFSET_X_FT:F2}	{MULTIPLANE_POINT_OFFSET_Y_FT:F2}	{MULTIPLANE_BOUNDARY_ELEVATION_FT:F3}	B
                {MULTIPLANE_SECOND_POINT_ID}	{MULTIPLANE_POINT_OFFSET_X_FT + MULTIPLANE_POINT_OFFSET_INCREMENT_FT:F2}	{MULTIPLANE_POINT_OFFSET_Y_FT + MULTIPLANE_POINT_OFFSET_INCREMENT_FT:F2}	{MULTIPLANE_INTERIOR_ELEVATION_FT:F3}	
                """);
            try
            {
                SurveyModel Survey = new SurveyModel();
                Survey.LoadAuto(Path);
                // Content sniffing must load benchmark, boundary, and interior rows.
                Assert.True(Survey.Benchmarks.Count >= MIN_BENCHMARK_COUNT);
                Assert.True(Survey.BoundaryPoints.Count >= MIN_BOUNDARY_COUNT);
                Assert.True(Survey.InteriorPoints.Count >= MIN_INTERIOR_COUNT);
            }
            finally
            {
                TempFile.DeleteIfExists(Path);
            }
        }
    }
}
