using AgGradeDesigner.Core.Models;

namespace AgGradeDesigner.Diagnostics
{
    /// <summary>
    /// Targeted logging for project open/new and field content / view-mode switches.
    /// Search tags: ViewerLifecycle ProjectOpen ProjectNew ContentView ViewMode.
    /// </summary>
    public static class ViewerLifecycleDiagnostics
    {
        /// <summary>Whether viewer lifecycle instrumentation is active for this process.</summary>
        static private bool Enabled;

        /// <summary>
        /// Configures viewer lifecycle logging from launch options.
        /// </summary>
        /// <param name="DebugLog">True when <c>--debug-log</c> was passed.</param>
        public static void Configure
            (
            bool DebugLog
            )
        {
#if DEBUG
            Enabled = true;
#else
            Enabled = DebugLog;
#endif
        }

        /// <summary>
        /// Returns true when viewer lifecycle diagnostic entries should be written.
        /// </summary>
        public static bool ShouldLog => Enabled;

        /// <summary>
        /// Opens or extends the shared diagnostic log for a viewer lifecycle event.
        /// </summary>
        /// <param name="Trigger">Short reason the session started or was extended.</param>
        public static void BeginSession
            (
            string Trigger
            )
        {
            if (!Enabled)
                return;

            if (!ImportDiagnosticsLog.IsActive)
                ImportDiagnosticsLog.SetEnabled(true, $"ViewerLifecycle:{Trigger}");
            else
                ImportDiagnosticsLog.ExtendSession(TimeSpan.FromMinutes(5), $"ViewerLifecycle:{Trigger}");
        }

        /// <summary>
        /// Writes one viewer lifecycle timeline entry when logging is enabled.
        /// </summary>
        /// <param name="Phase">Short phase label.</param>
        /// <param name="Detail">Optional key=value detail payload.</param>
        public static void Write
            (
            string Phase,
            string? Detail = null
            )
        {
            if (!Enabled)
                return;

            ImportDiagnosticsLog.Write("ViewerLife", Phase, Detail);
        }

        /// <summary>
        /// Records elapsed time since the previous viewer lifecycle mark.
        /// </summary>
        /// <param name="Phase">Mark label.</param>
        /// <param name="Detail">Optional detail text.</param>
        public static void Mark
            (
            string Phase,
            string? Detail = null
            )
        {
            if (!Enabled)
                return;

            ImportDiagnosticsLog.Mark($"ViewerLife.{Phase}", Detail);
        }

        /// <summary>
        /// Logs a timed viewer lifecycle operation with START/END lines.
        /// </summary>
        /// <param name="Operation">Operation name.</param>
        /// <param name="Detail">Optional detail text.</param>
        /// <returns>Timed scope disposed at end of operation.</returns>
        public static ImportDiagnosticsLog.TimedScope Time
            (
            string Operation,
            string? Detail = null
            )
        {
            return ImportDiagnosticsLog.Time("ViewerLife", Operation, Detail);
        }

        /// <summary>
        /// Formats a compact snapshot of project viewer state for log detail lines.
        /// </summary>
        /// <param name="Project">Project whose viewer/content flags are summarized.</param>
        /// <param name="Extra">Optional trailing key=value fragments.</param>
        /// <returns>Space-separated key=value summary.</returns>
        public static string FormatProjectState
            (
            ProjectDocument Project,
            string? Extra = null
            )
        {
            ViewerState Viewer = Project.ViewerState;
            string Core =
                $"content={Viewer.FieldContentView} mode={Viewer.ViewMode} " +
                $"hasSurvey={Project.HasSurvey} hasDesign={Project.HasDesign} hasHaul={Project.HasHaulData} " +
                $"file={QuotePath(Project.FilePath)}";
            return string.IsNullOrEmpty(Extra) ? Core : $"{Core} {Extra}";
        }

        /// <summary>
        /// Quotes a path for log readability, or returns <c>(none)</c> when empty.
        /// </summary>
        /// <param name="Path">File path to quote.</param>
        /// <returns>Quoted path or <c>(none)</c>.</returns>
        static private string QuotePath
            (
            string? Path
            )
        {
            if (string.IsNullOrWhiteSpace(Path))
                return "(none)";

            return $"\"{Path}\"";
        }
    }
}
