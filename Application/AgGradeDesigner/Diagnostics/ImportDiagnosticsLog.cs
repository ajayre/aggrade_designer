using System.Diagnostics;
using System.Text;

using AgGradeDesigner.Core.Import;

namespace AgGradeDesigner.Diagnostics
{
    /// <summary>
    /// Append-only import/render diagnostic log with immediate flush for post-mortem freeze analysis.
    /// </summary>
    public static class ImportDiagnosticsLog
    {
        static private readonly object WriteLock = new object();
        static private readonly Stopwatch SessionStopwatch = new Stopwatch();
        static private readonly Stopwatch MarkStopwatch = new Stopwatch();
        static private StreamWriter? Writer;
        static private StreamWriter? ToolTraceWriter;
        static private long RenderLogCount;
        static private long PointerMoveLogCount;
        static private DateTime SessionEndsUtc = DateTime.MinValue;

        /// <summary>Whether debug logging is enabled for the current application session.</summary>
        public static bool IsEnabled { get; private set; }

        /// <summary>Whether a diagnostic session is currently accepting log entries.</summary>
        public static bool IsActive { get; private set; }

        /// <summary>Path to the active log file, if any.</summary>
        public static string? LogFilePath { get; private set; }

        /// <summary>Path to the companion AI tool trace JSONL file, if any.</summary>
        public static string? ToolTraceJsonlPath { get; private set; }

        /// <summary>
        /// Enables or disables debug logging for the current application session.
        /// </summary>
        /// <param name="Enabled">Whether debug logging should be active.</param>
        /// <param name="Trigger">Reason the setting changed, recorded in the log when enabled.</param>
        public static void SetEnabled
            (
            bool Enabled,
            string Trigger = "Settings"
            )
        {
            if (Enabled == IsEnabled)
                return;

            IsEnabled = Enabled;
            if (Enabled)
                StartSession(Trigger);
            else
                EndSession("disabled");
        }

        /// <summary>
        /// Starts a new import diagnostic session and opens a timestamped log file.
        /// </summary>
        public static void StartSession
            (
            string Trigger,
            string? AgdPath = null
            )
        {
            lock (WriteLock)
            {
                CloseWriterUnlocked();
                string LogDirectory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AgGradeDesigner",
                    "Logs");
                Directory.CreateDirectory(LogDirectory);

                string FileName = $"aggradedesigner_{DateTime.Now:yyyyMMdd_HHmmss_fff}.log";
                LogFilePath = Path.Combine(LogDirectory, FileName);
                ToolTraceJsonlPath = Path.ChangeExtension(LogFilePath, null) + "_aitools.jsonl";
                Writer = new StreamWriter(LogFilePath, append: false, Encoding.UTF8)
                {
                    AutoFlush = true
                };
                ToolTraceWriter = new StreamWriter(ToolTraceJsonlPath, append: false, Encoding.UTF8)
                {
                    AutoFlush = true
                };

                SessionStopwatch.Restart();
                MarkStopwatch.Restart();
                RenderLogCount = 0;
                PointerMoveLogCount = 0;
                IsActive = true;
                SessionEndsUtc = DateTime.UtcNow.AddMinutes(3);

                WriteLineUnlocked("Session", "START", $"trigger={Trigger} pid={Environment.ProcessId}");
                if (!string.IsNullOrEmpty(AgdPath))
                    WriteLineUnlocked("Session", "AGD", AgdPath);

                WriteLineUnlocked("Session", "LOG", LogFilePath);
                WriteLineUnlocked("Session", "AITOOLS", ToolTraceJsonlPath);
                FlushUnlocked();
            }
        }

        /// <summary>
        /// Extends the active session so post-handoff renders and input are captured.
        /// </summary>
        public static void ExtendSession
            (
            TimeSpan AdditionalDuration,
            string Reason
            )
        {
            if (!IsActive)
                return;

            lock (WriteLock)
            {
                SessionEndsUtc = DateTime.UtcNow.Add(AdditionalDuration);
                WriteLineUnlocked("Session", "EXTEND", $"{Reason} until={SessionEndsUtc:O}");
                FlushUnlocked();
            }
        }

        /// <summary>
        /// Ends the active diagnostic session and closes the log file.
        /// </summary>
        public static void EndSession
            (
            string Reason
            )
        {
            lock (WriteLock)
            {
                if (!IsActive)
                    return;

                WriteLineUnlocked("Session", "END", $"{Reason} totalMs={SessionStopwatch.ElapsedMilliseconds}");
                FlushUnlocked();
                CloseWriterUnlocked();
                IsActive = false;
            }
        }

        /// <summary>
        /// Appends one JSONL record to the companion AI tool trace file and a summary line in the main log.
        /// </summary>
        /// <param name="JsonLine">Single JSON object serialized without embedded newlines.</param>
        public static void WriteToolTraceJsonLine
            (
            string JsonLine
            )
        {
            if (!ShouldLog())
                return;

            lock (WriteLock)
            {
                ToolTraceWriter?.WriteLine(JsonLine);
                WriteLineUnlocked("AiToolTrace", "jsonl", JsonLine);
                FlushUnlocked();
            }
        }

        /// <summary>
        /// Writes a categorized message when the session is active or within the post-handoff tail.
        /// </summary>
        public static void Write
            (
            string Category,
            string Message,
            string? Detail = null
            )
        {
            if (!ShouldLog())
                return;

            lock (WriteLock)
            {
                WriteLineUnlocked(Category, Message, Detail);
                FlushUnlocked();
            }
        }

        /// <summary>
        /// Logs elapsed time since the previous mark and restarts the mark timer.
        /// </summary>
        public static void Mark
            (
            string Phase,
            string? Detail = null
            )
        {
            if (!ShouldLog())
                return;

            lock (WriteLock)
            {
                WriteLineUnlocked("Timing", Phase, $"+{MarkStopwatch.ElapsedMilliseconds}ms {Detail}".Trim());
                MarkStopwatch.Restart();
                FlushUnlocked();
            }
        }

        /// <summary>
        /// Logs a timed operation; writes START immediately and END on dispose with flush.
        /// </summary>
        public static TimedScope Time
            (
            string Category,
            string Operation,
            string? Detail = null
            )
        {
            return new TimedScope(Category, Operation, Detail);
        }

        /// <summary>
        /// Logs viewport dimensions from a named source.
        /// </summary>
        public static void WriteViewport
            (
            string Source,
            int Width,
            int Height,
            string? Detail = null
            )
        {
            Write("Viewport", $"{Source} {Width}x{Height}", Detail);
        }

        /// <summary>
        /// Logs haul route scrub overlay / slider pointer diagnostics.
        /// </summary>
        public static void WriteHaulScrub
            (
            string Phase,
            string? Detail = null
            )
        {
            Write("HaulScrub", Phase, Detail);
        }

        /// <summary>
        /// Logs haul route scrub prefetch decisions and progress.
        /// </summary>
        public static void WriteHaulPrefetch
            (
            string Phase,
            string? Detail = null
            )
        {
            Write("HaulPrefetch", Phase, Detail);
        }

        /// <summary>
        /// Logs haul route scrub pointer events with coordinates.
        /// </summary>
        public static void WriteHaulScrubPointer
            (
            string Phase,
            double X,
            double Y,
            string? Detail = null
            )
        {
            string Position = $"x={X:0.##} y={Y:0.##}";
            string Combined = string.Join(
                " ",
                new[] { Position, Detail }.Where(Part => !string.IsNullOrEmpty(Part)));
            WriteHaulScrub(Phase, Combined);
        }

        /// <summary>
        /// Logs a pointer/input event with position and optional button.
        /// </summary>
        public static void WritePointer
            (
            string EventName,
            double X,
            double Y,
            string? Button = null,
            string? Detail = null
            )
        {
            string Position = $"x={X:0.##} y={Y:0.##}";
            string Combined = string.Join(
                " ",
                new[] { Button, Position, Detail }.Where(Part => !string.IsNullOrEmpty(Part)));
            Write("Pointer", EventName, Combined);
        }

        /// <summary>
        /// Logs pointer move events with throttling to avoid log flooding during drags.
        /// </summary>
        public static void WritePointerMoveThrottled
            (
            double X,
            double Y,
            string? Detail = null
            )
        {
            if (!ShouldLog())
                return;

            PointerMoveLogCount++;
            if ((PointerMoveLogCount % 20) != 1)
                return;

            WritePointer("Move", X, Y, Detail: Detail);
        }

        /// <summary>
        /// Logs a handoff timeline event from the shared recorder.
        /// </summary>
        public static void WriteHandoffEvent
            (
            ImportViewerHandoffEvent Event
            )
        {
            Write(
                "Handoff",
                Event.Kind.ToString(),
                $"key={Event.CacheKey} size={Event.Width}x{Event.Height} detail={Event.Detail}");
        }

        /// <summary>
        /// Logs a render-path decision with optional duration.
        /// </summary>
        public static void WriteRender
            (
            string Decision,
            long DurationMs,
            string Detail
            )
        {
            if (!ShouldLog())
                return;

            lock (WriteLock)
            {
                RenderLogCount++;
                WriteLineUnlocked(
                    "Render",
                    $"#{RenderLogCount} {Decision} {DurationMs}ms",
                    Detail);
                FlushUnlocked();
            }
        }

        /// <summary>
        /// Schedules session closure after a delay so post-handoff events remain in the log.
        /// </summary>
        public static void ScheduleEndSession
            (
            TimeSpan Delay,
            string Reason
            )
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(Delay).ConfigureAwait(false);
                EndSession(Reason);
            });
        }

        /// <summary>
        /// Logs an exception with message and stack trace.
        /// </summary>
        public static void WriteException
            (
            string Phase,
            Exception Exception
            )
        {
            Write("Error", Phase, $"{Exception.GetType().Name}: {Exception.Message}");
            Write("Error", $"{Phase} stack", Exception.StackTrace);
        }

        /// <summary>
        /// Returns true when logging should continue for the current session or tail window.
        /// </summary>
        public static bool ShouldLog
            (
            )
        {
            if (!IsEnabled)
                return false;

            if (IsActive)
                return true;

            if (SessionEndsUtc > DateTime.UtcNow && Writer != null)
                return true;

            return false;
        }

        static private void WriteLineUnlocked
            (
            string Category,
            string Message,
            string? Detail
            )
        {
            if (Writer == null)
                return;

            string ThreadLabel = Avalonia.Threading.Dispatcher.UIThread.CheckAccess() ? "UI" : "BG";
            string Line = Detail == null
                ? $"{SessionStopwatch.ElapsedMilliseconds,10}ms [{ThreadLabel,2}] {Category,-10} {Message}"
                : $"{SessionStopwatch.ElapsedMilliseconds,10}ms [{ThreadLabel,2}] {Category,-10} {Message} | {Detail}";
            Writer.WriteLine(Line);
        }

        static private void FlushUnlocked
            (
            )
        {
            Writer?.Flush();
        }

        static private void CloseWriterUnlocked
            (
            )
        {
            Writer?.Dispose();
            Writer = null;
            ToolTraceWriter?.Dispose();
            ToolTraceWriter = null;
            ToolTraceJsonlPath = null;
        }

        /// <summary>
        /// RAII helper that logs operation start/end with flush.
        /// </summary>
        public readonly struct TimedScope : IDisposable
        {
            readonly private string Category;
            readonly private string Operation;
            readonly private Stopwatch Watch;

            internal TimedScope
                (
                string Category,
                string Operation,
                string? Detail
                )
            {
                this.Category = Category;
                this.Operation = Operation;
                Watch = Stopwatch.StartNew();
                Write(Category, $"{Operation} START", Detail);
            }

            /// <summary>
            /// Logs operation completion timing.
            /// </summary>
            public void Dispose
                (
                )
            {
                Watch.Stop();
                Write(Category, $"{Operation} END", $"{Watch.ElapsedMilliseconds}ms");
            }
        }
    }
}
