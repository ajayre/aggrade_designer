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
            Assert.True(Survey.Benchmarks.Count >= 1);
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
            string Path = TempFile.Write(".dat", "0001\t0\t0\t100\tMB N36:26:51.619 / W90:43:26.085\t0\n");
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
            string Path = TempFile.Write(".dat", """
                0001	0.000	0.000	100.000	MB N36:26:51.619 / W90:43:26.085	0.000
                2	10.00	20.00	100.500	B
                3	15.00	25.00	100.600	
                """);
            try
            {
                SurveyModel Survey = new SurveyModel();
                Survey.LoadAuto(Path);
                // Content sniffing must load benchmark, boundary, and interior rows.
                Assert.True(Survey.Benchmarks.Count >= 1);
                Assert.True(Survey.BoundaryPoints.Count >= 1);
                Assert.True(Survey.InteriorPoints.Count >= 1);
            }
            finally
            {
                TempFile.DeleteIfExists(Path);
            }
        }
    }
}
