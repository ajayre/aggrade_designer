using System.Runtime.CompilerServices;

using AgGradeDesigner.Diagnostics;

namespace AgGradeDesigner.Controls
{
    /// <summary>
    /// Timed acquire helpers for the viewer prepare lock. UI thread callers must never block indefinitely.
    /// </summary>
    internal static class ViewerPrepareLock
    {
        /// <summary>Default timeout for cache reads and diagnostics on the UI thread.</summary>
        internal const int UiAcquireTimeoutMs = 100;

        /// <summary>Timeout for interactive sync frame builds on the UI thread.</summary>
        internal const int UiRenderAcquireTimeoutMs = 250;

        /// <summary>Attempts to enter the prepare lock on the UI thread within the supplied timeout.</summary>
        internal static bool TryEnterForUi
            (
            object LockObject,
            int TimeoutMs,
            [CallerMemberName] string Caller = ""
            )
        {
            bool Entered = Monitor.TryEnter(LockObject, TimeoutMs);
            if (ImportDiagnosticsLog.ShouldLog())
            {
                ImportDiagnosticsLog.Write(
                    "PrepareLock",
                    Entered ? "UiAcquired" : "UiWaitTimeout",
                    $"{Caller} timeout={TimeoutMs}ms");
            }

            return Entered;
        }

        /// <summary>Releases the prepare lock when the caller successfully entered it.</summary>
        internal static void ExitIfHeld
            (
            object LockObject,
            bool Entered
            )
        {
            if (Entered)
                Monitor.Exit(LockObject);
        }
    }
}
