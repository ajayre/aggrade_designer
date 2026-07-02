namespace AgGradeDesigner.Core.Tests.TestFixtures
{
    /// <summary>
    /// Resolves paths to shared test data files at the repository root.
    /// </summary>

    static internal class TestDataPaths
    {
        /// <summary>
        /// Directory containing FakeField_01 and other integration fixtures.
        /// </summary>
        static public string TestDataDirectory { get; } = ResolveTestDataDirectory();

        /// <summary>
        /// Path to the Fake Field 01 AGS survey file (~12k points).
        /// </summary>
        static public string FakeField01Ags { get; } = Path.Combine(TestDataDirectory, "FakeField_01.ags");

        /// <summary>
        /// Path to the Fake Field 01 AGD design file derived from the AGS fixture.
        /// </summary>
        static public string FakeField01Agd { get; } = Path.Combine(TestDataDirectory, "FakeField_01.agd");

        /// <summary>
        /// Walks up from the test output directory until the repo <c>Test Data</c> folder is found.
        /// </summary>
        static private string ResolveTestDataDirectory()
        {
            DirectoryInfo? Current = new DirectoryInfo(AppContext.BaseDirectory);
            while (Current != null)
            {
                string Candidate = Path.Combine(Current.FullName, "Test Data");
                if (Directory.Exists(Candidate))
                    return Candidate;

                Current = Current.Parent;
            }

            throw new DirectoryNotFoundException(
                "Could not locate 'Test Data' folder. Expected FakeField_01.ags at repository root.");
        }
    }
}
