namespace AgGradeDesigner.Core.Import
{
    /// <summary>
    /// One timestamped milestone in the AGD import viewer handoff timeline.
    /// </summary>
    public sealed class ImportViewerHandoffEvent
    {
        /// <summary>Milestone kind.</summary>
        public ImportViewerHandoffEventKind Kind { get; init; }

        /// <summary>Monotonic sequence number within the recording session.</summary>
        public int Sequence { get; init; }

        /// <summary>UTC timestamp when the milestone was recorded.</summary>
        public DateTimeOffset TimestampUtc { get; init; }

        /// <summary>Optional cache fingerprint associated with the milestone.</summary>
        public string? CacheKey { get; init; }

        /// <summary>Optional viewport width in pixels.</summary>
        public int? Width { get; init; }

        /// <summary>Optional viewport height in pixels.</summary>
        public int? Height { get; init; }

        /// <summary>Optional free-form detail for diagnostics.</summary>
        public string? Detail { get; init; }
    }
}
