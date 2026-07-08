namespace AgGradeDesigner.Diagnostics
{
    /// <summary>
    /// Targeted logging for progressive 2D→3D view-mode switch and background upgrade handoff.
    /// </summary>
    public static class ProgressiveThreeDDiagnostics
    {
        /// <summary>Whether progressive 3D instrumentation is active for this process.</summary>
        static private bool Enabled;

        /// <summary>
        /// Configures progressive 3D logging from launch options.
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
        /// Returns true when progressive 3D diagnostic entries should be written.
        /// </summary>
        public static bool ShouldLog => Enabled;

        /// <summary>
        /// Opens or extends the shared diagnostic log for a progressive 3D transition.
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
                ImportDiagnosticsLog.SetEnabled(true, $"Progressive3D:{Trigger}");
            else
                ImportDiagnosticsLog.ExtendSession(TimeSpan.FromMinutes(3), $"Progressive3D:{Trigger}");
        }

        /// <summary>
        /// Writes one progressive 3D timeline entry when logging is enabled.
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

            ImportDiagnosticsLog.Write("Progressive3D", Phase, Detail);
        }

        /// <summary>
        /// Records elapsed time since the previous progressive 3D mark.
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

            ImportDiagnosticsLog.Mark($"Progressive3D.{Phase}", Detail);
        }

        /// <summary>
        /// Schedules diagnostic session closure after the upgrade tail completes.
        /// </summary>
        /// <param name="Delay">Delay before closing the log file.</param>
        /// <param name="Reason">Reason recorded in the session end line.</param>
        public static void ScheduleSessionEnd
            (
            TimeSpan Delay,
            string Reason
            )
        {
            if (!Enabled)
                return;

            ImportDiagnosticsLog.ScheduleEndSession(Delay, Reason);
        }

        /// <summary>
        /// Logs a timed progressive 3D operation with START/END lines.
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
            return ImportDiagnosticsLog.Time("Progressive3D", Operation, Detail);
        }
    }
}
