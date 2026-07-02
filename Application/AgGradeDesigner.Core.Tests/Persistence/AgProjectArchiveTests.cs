using System.IO.Compression;

using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Persistence;
using AgGradeDesigner.Core.Tests.TestFixtures;

namespace AgGradeDesigner.Core.Tests.Persistence
{
    /// <summary>
    /// Unit tests for <see cref="AgProjectArchive"/> zip project containers.
    /// </summary>
    public sealed class AgProjectArchiveTests
    {
        /// <summary>
        /// Verifies saved project files are zip archives containing data.xml.
        /// </summary>
        [Fact]
        public void Write_CreatesZipArchiveWithDataXml
            (
            )
        {
            string Path = TempFile.Path(".agproject");
            try
            {
                /// <summary>
                /// Xml used by this component.
                /// </summary>

                const string Xml = """<?xml version="1.0" encoding="UTF-8"?><AgDesignProject schemaVersion="1.0"/>""";
                AgProjectArchive.Write(Path, Xml);

                Assert.True(File.Exists(Path));
                using ZipArchive Archive = ZipFile.OpenRead(Path);
                ZipArchiveEntry? Entry = Archive.GetEntry(ProjectDocument.ARCHIVE_DATA_ENTRY);
                Assert.NotNull(Entry);

                using StreamReader Reader = new StreamReader(Entry!.Open());
                string ReadXml = Reader.ReadToEnd();
                Assert.Equal(Xml, ReadXml);
            }
            finally
            {
                TempFile.DeleteIfExists(Path);
            }
        }

        /// <summary>
        /// Verifies plain XML files are rejected as project archives.
        /// </summary>
        [Fact]
        public void Read_RejectsPlainXmlFile
            (
            )
        {
            string Path = TempFile.Write(".agproject", """
                <?xml version="1.0" encoding="UTF-8"?>
                <AgDesignProject schemaVersion="1.0"/>
                """);
            try
            {
                Assert.Throws<InvalidDataException>(() => AgProjectArchive.Read(Path));
            }
            finally
            {
                TempFile.DeleteIfExists(Path);
            }
        }
    }
}
