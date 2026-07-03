using AgGradeDesigner.Diagnostics;

namespace AgGradeDesigner.Controls
{
    /// <summary>
    /// Coordinates background prewarm workers with UI interaction via pause/resume and generation invalidation.
    /// </summary>
    internal sealed class BackgroundPrewarmCoordinator
    {
        private readonly object _Gate = new();
        private int _Generation;
        private int _PauseDepth;

        /// <summary>Current prewarm generation; bumped only when cached targets are invalidated.</summary>
        internal int Generation
        {
            get
            {
                lock (_Gate)
                {
                    return _Generation;
                }
            }
        }

        /// <summary>Whether UI interaction has requested workers to pause at the next checkpoint.</summary>
        internal bool IsPaused
        {
            get
            {
                lock (_Gate)
                {
                    return _PauseDepth > 0;
                }
            }
        }

        /// <summary>
        /// Invalidates in-flight prewarm workers after cache or project changes.
        /// </summary>
        internal void Invalidate
            (
            string Reason
            )
        {
            lock (_Gate)
            {
                _Generation++;
                Monitor.PulseAll(_Gate);
            }

            ImportDiagnosticsLog.Write("Prewarm", "Invalidate", Reason);
        }

        /// <summary>
        /// Requests workers pause at safe checkpoints without discarding completed cache entries.
        /// </summary>
        internal void RequestPause
            (
            string Reason
            )
        {
            lock (_Gate)
            {
                _PauseDepth++;
                Monitor.PulseAll(_Gate);
            }

            ImportDiagnosticsLog.Write("Prewarm", "PauseRequested", Reason);
        }

        /// <summary>
        /// Resumes background prewarm after UI interaction completes.
        /// </summary>
        internal void ReleasePause
            (
            string Reason
            )
        {
            lock (_Gate)
            {
                if (_PauseDepth > 0)
                    _PauseDepth--;

                if (_PauseDepth == 0)
                    Monitor.PulseAll(_Gate);
            }

            ImportDiagnosticsLog.Write("Prewarm", "PauseReleased", Reason);
        }

        /// <summary>
        /// Returns a scope that pauses prewarm for the duration of UI rendering work.
        /// </summary>
        internal UiPauseScope BeginUiPauseScope
            (
            string Reason
            )
        {
            RequestPause(Reason);
            return new UiPauseScope(this, Reason);
        }

        /// <summary>
        /// Blocks background workers while UI interaction is active. Returns false when invalidated.
        /// </summary>
        internal bool WaitAtCheckpoint
            (
            int WorkerGeneration,
            string Checkpoint
            )
        {
            while (true)
            {
                lock (_Gate)
                {
                    if (WorkerGeneration != _Generation)
                        return false;

                    if (_PauseDepth == 0)
                        return true;

                    ImportDiagnosticsLog.Write(
                        "Prewarm",
                        "Paused",
                        $"{Checkpoint} gen={WorkerGeneration}");
                    Monitor.Wait(_Gate, 250);
                }
            }
        }

        /// <summary>
        /// RAII helper that releases a UI pause request when disposed.
        /// </summary>
        internal readonly struct UiPauseScope : IDisposable
        {
            private readonly BackgroundPrewarmCoordinator? _Coordinator;
            private readonly string _Reason;

            internal UiPauseScope
                (
                BackgroundPrewarmCoordinator Coordinator,
                string Reason
                )
            {
                _Coordinator = Coordinator;
                _Reason = Reason;
            }

            /// <summary>
            /// Resumes background prewarm workers.
            /// </summary>
            public void Dispose
                (
                )
            {
                _Coordinator?.ReleasePause(_Reason);
            }
        }
    }
}
