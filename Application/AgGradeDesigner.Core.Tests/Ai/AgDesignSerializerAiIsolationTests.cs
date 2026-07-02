using AgGradeDesigner.Core.Ai;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Persistence;
using AgGradeDesigner.Core.Tests.TestFixtures;

namespace AgGradeDesigner.Core.Tests.Ai
{
    /// <summary>
    /// Unit tests verifying project save files exclude AI settings.
    /// </summary>

    public sealed class AgDesignSerializerAiIsolationTests
    {
        /// <summary>
        /// Verifies saving a project does not embed AI provider settings in the archive.
        /// </summary>
        [Fact]
        public void SaveProject_DoesNotIncludeAiSettings
            (
            )
        {
            string SettingsPath = TempFile.Path(".json");
            string ProjectPath = TempFile.Path(ProjectDocument.FILE_EXTENSION);
            try
            {
                UserSettingsStore Store = new UserSettingsStore(SettingsPath);
                Store.SaveAiSettings(new AiSettings
                {
                    Provider = AiProviderKind.OpenAi,
                    ApiKey = "test-secret-key"
                });

                ProjectDocument Project = new ProjectDocument();
                AgDesignSerializer.Save(Project, ProjectPath);

                string ArchiveText = ReadArchiveText(ProjectPath);
                Assert.DoesNotContain("test-secret-key", ArchiveText, StringComparison.Ordinal);
                Assert.DoesNotContain("apiKey", ArchiveText, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("OpenAi", ArchiveText, StringComparison.Ordinal);
            }
            finally
            {
                TempFile.DeleteIfExists(SettingsPath);
                TempFile.DeleteIfExists(ProjectPath);
            }
        }

        /// <summary>
        /// Reads the XML payload from a saved project archive as plain text.
        /// </summary>
        /// <param name="ProjectPath">Path to the project archive.</param>
        /// <returns>Combined text content of archive entries.</returns>
        static private string ReadArchiveText
            (
            string ProjectPath
            )
        {
            using System.IO.Compression.ZipArchive Archive = System.IO.Compression.ZipFile.OpenRead(ProjectPath);
            System.IO.Compression.ZipArchiveEntry? Entry = Archive.GetEntry(ProjectDocument.ARCHIVE_DATA_ENTRY);
            if (Entry == null)
                return string.Empty;

            using StreamReader Reader = new StreamReader(Entry.Open());
            return Reader.ReadToEnd();
        }
    }
}
