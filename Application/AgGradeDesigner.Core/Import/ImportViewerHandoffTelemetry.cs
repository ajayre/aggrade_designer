namespace AgGradeDesigner.Core.Import
{
    /// <summary>
    /// Process-wide handoff recorder used by production code and regression tests.
    /// </summary>
    public static class ImportViewerHandoffTelemetry
    {
        /// <summary>Active recorder for the current import handoff session.</summary>
        public static ImportViewerHandoffRecorder Recorder { get; } = new ImportViewerHandoffRecorder();

        /// <summary>Clears the active recorder so tests start from a clean timeline.</summary>
        public static void ResetForTests
            (
            )
        {
            Recorder.Clear();
        }
    }
}
