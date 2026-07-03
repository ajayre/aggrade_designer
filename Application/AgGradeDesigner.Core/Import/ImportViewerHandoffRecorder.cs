namespace AgGradeDesigner.Core.Import
{
    /// <summary>
    /// Thread-safe timeline recorder for AGD import viewer handoff milestones.
    /// </summary>
    public sealed class ImportViewerHandoffRecorder
    {
        private readonly object _Lock = new();
        private readonly List<ImportViewerHandoffEvent> _Events = new();
        private int _Sequence;

        /// <summary>Recorded milestones in arrival order.</summary>
        public IReadOnlyList<ImportViewerHandoffEvent> Events
        {
            get
            {
                lock (_Lock)
                {
                    return _Events.ToList();
                }
            }
        }

        /// <summary>Optional sink invoked after each recorded milestone (for app diagnostics).</summary>
        public Action<ImportViewerHandoffEvent>? DiagnosticSink { get; set; }

        /// <summary>Clears the recorded timeline.</summary>
        public void Clear
            (
            )
        {
            lock (_Lock)
            {
                _Events.Clear();
                _Sequence = 0;
            }
        }

        /// <summary>Records a milestone.</summary>
        public void Record
            (
            ImportViewerHandoffEventKind Kind,
            string? CacheKey = null,
            int? Width = null,
            int? Height = null,
            string? Detail = null
            )
        {
            lock (_Lock)
            {
                _Sequence++;
                ImportViewerHandoffEvent Event = new ImportViewerHandoffEvent
                {
                    Kind = Kind,
                    Sequence = _Sequence,
                    TimestampUtc = DateTimeOffset.UtcNow,
                    CacheKey = CacheKey,
                    Width = Width,
                    Height = Height,
                    Detail = Detail
                };
                _Events.Add(Event);
                DiagnosticSink?.Invoke(Event);
            }
        }

        /// <summary>Returns true when the timeline contains the given milestone.</summary>
        public bool Contains
            (
            ImportViewerHandoffEventKind Kind
            )
        {
            lock (_Lock)
            {
                return _Events.Any(Event => Event.Kind == Kind);
            }
        }

        /// <summary>Returns the first index of the given milestone, or -1 when absent.</summary>
        public int IndexOf
            (
            ImportViewerHandoffEventKind Kind
            )
        {
            lock (_Lock)
            {
                for (int Index = 0; Index < _Events.Count; Index++)
                {
                    if (_Events[Index].Kind == Kind)
                        return Index;
                }

                return -1;
            }
        }
    }
}
