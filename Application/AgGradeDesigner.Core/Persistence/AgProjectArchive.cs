using System.IO.Compression;
using System.Text;

using AgGradeDesigner.Core.Models;

namespace AgGradeDesigner.Core.Persistence
{
    /// <summary>
    /// Reads and writes the zip container for <see cref="ProjectDocument"/> project files.
    /// </summary>
    static public class AgProjectArchive
    {
        /// <summary>
        /// Writes project XML into a zip archive at the given path.
        /// </summary>
        /// <param name="FilePath">Destination project file path.</param>
        /// <param name="Xml">UTF-8 project document XML.</param>
        static public void Write
            (
            string FilePath,
            string Xml
            )
        {
            string? DirectoryPath = Path.GetDirectoryName(FilePath);
            if (!string.IsNullOrEmpty(DirectoryPath))
                Directory.CreateDirectory(DirectoryPath);

            using FileStream Stream = File.Create(FilePath);
            using ZipArchive Archive = new ZipArchive(Stream, ZipArchiveMode.Create);
            ZipArchiveEntry Entry = Archive.CreateEntry(ProjectDocument.ARCHIVE_DATA_ENTRY, CompressionLevel.Optimal);
            using StreamWriter Writer = new StreamWriter(Entry.Open(), new UTF8Encoding(false));
            Writer.Write(Xml);
        }

        /// <summary>
        /// Reads project XML from the zip archive at the given path.
        /// </summary>
        /// <param name="FilePath">Source project file path.</param>
        /// <returns>UTF-8 XML payload from <see cref="ProjectDocument.ARCHIVE_DATA_ENTRY"/>.</returns>
        /// <exception cref="InvalidDataException">The file is not a valid project archive.</exception>
        static public string Read
            (
            string FilePath
            )
        {
            try
            {
                using FileStream Stream = File.OpenRead(FilePath);
                using ZipArchive Archive = new ZipArchive(Stream, ZipArchiveMode.Read);
                ZipArchiveEntry? Entry = Archive.GetEntry(ProjectDocument.ARCHIVE_DATA_ENTRY)
                    ?? throw new InvalidDataException(
                        $"Project archive is missing '{ProjectDocument.ARCHIVE_DATA_ENTRY}'.");

                using StreamReader Reader = new StreamReader(Entry.Open(), Encoding.UTF8);
                return Reader.ReadToEnd();
            }
            catch (InvalidDataException)
            {
                throw;
            }
            catch (Exception Ex)
            {
                throw new InvalidDataException("Project file is not a valid archive.", Ex);
            }
        }
    }
}
