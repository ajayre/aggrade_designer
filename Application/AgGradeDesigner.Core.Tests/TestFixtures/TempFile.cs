namespace AgGradeDesigner.Core.Tests.TestFixtures
{
    /// <summary>
    /// Creates and removes temporary files under the system temp directory for isolated test I/O.
    /// </summary>

    static internal class TempFile
    {
        /// <summary>
        /// Writes text content to a uniquely named file with the given extension.
        /// </summary>
        /// <param name="Extension">File extension including the leading dot.</param>
        /// <param name="Content">Full text payload to persist.</param>
        /// <returns>Absolute path to the written file.</returns>
        static public string Write
            (
            string Extension,
            string Content
            )
        {
            string FilePath = Path(Extension);
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(FilePath)!);
            System.IO.File.WriteAllText(FilePath, Content);
            return FilePath;
        }

        /// <summary>
        /// Returns a unique temp file path with the given extension.
        /// </summary>
        /// <param name="Extension">File extension including the leading dot.</param>
        /// <returns>Absolute path that has not been created yet.</returns>
        static public string Path
            (
            string Extension
            )
        {
            return System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "AgGradeDesigner.Tests",
                Guid.NewGuid().ToString("N") + Extension);
        }

        /// <summary>
        /// Deletes the file at <paramref name="Path"/> when it still exists.
        /// </summary>
        /// <param name="Path">Absolute path to remove.</param>
        static public void DeleteIfExists
            (
            string Path
            )
        {
            // Best-effort cleanup so temp fixtures do not accumulate across test runs.
            if (System.IO.File.Exists(Path))
                System.IO.File.Delete(Path);
        }
    }
}
