namespace AgGradeDesigner.Core.Haul
{
    /// <summary>Whether a haul scrub frame is a full leveled map or a fast route-only sprite.</summary>
    public enum HaulScrubFrameKind
    {
        /// <summary>Cut/fill base map with prior passes applied plus the current route.</summary>
        Complete,

        /// <summary>Cropped route symbology only, placed in map space during active scrubbing.</summary>
        Partial
    }

    /// <summary>
    /// Resumable checkpoint for preempted haul scrub frame generation (§4.7.5).
    /// </summary>
    public sealed class HaulPartialFrameState
    {
        /// <summary>Route scrub index being generated.</summary>
        public int RouteScrubIndex { get; init; }

        /// <summary>Opaque progress marker for future resumable workers.</summary>
        public int ProgressMarker { get; set; }
    }

    /// <summary>
    /// One cached haul scrub frame keyed by route scrub index (§4.7.2).
    /// </summary>
    public sealed class HaulScrubFrame
    {
        /// <summary>Route scrub index for this frame.</summary>
        public int RouteScrubIndex { get; init; }

        /// <summary>Cropped route symbology PNG bytes.</summary>
        public byte[] PngBytes { get; init; } = Array.Empty<byte>();

        /// <summary>Whether this is a complete leveled map or a fast partial route sprite.</summary>
        public HaulScrubFrameKind Kind { get; init; } = HaulScrubFrameKind.Complete;

        /// <summary>Southern edge of partial sprites in decimal degrees.</summary>
        public double MinLat { get; init; }

        /// <summary>Northern edge of the sprite in decimal degrees.</summary>
        public double MaxLat { get; init; }

        /// <summary>Western edge of the sprite in decimal degrees.</summary>
        public double MinLon { get; init; }

        /// <summary>Eastern edge of the sprite in decimal degrees.</summary>
        public double MaxLon { get; init; }

        /// <summary>Whether <see cref="PngBytes"/> has geographic placement for map drawing.</summary>
        public bool HasPlacement => PngBytes.Length > 0;
    }

    /// <summary>
    /// Session-scoped in-memory store mapping route scrub indices to completed frames (§5.1.2).
    /// </summary>
    public sealed class HaulScrubFrameCache
    {
        /// <summary>Completed frames keyed by route scrub index.</summary>
        private readonly Dictionary<int, HaulScrubFrame> _Frames = new Dictionary<int, HaulScrubFrame>();

        /// <summary>Partial generation checkpoints keyed by route scrub index.</summary>
        private readonly Dictionary<int, HaulPartialFrameState> _PartialStates = new Dictionary<int, HaulPartialFrameState>();

        /// <summary>Serializes concurrent prefetch workers.</summary>
        private readonly object _Lock = new object();

        /// <summary>Number of cached completed frames.</summary>
        public int CachedFrameCount
        {
            get
            {
                lock (_Lock)
                {
                    return _Frames.Count;
                }
            }
        }

        /// <summary>
        /// Returns whether a completed frame exists for the supplied index.
        /// </summary>
        /// <param name="RouteScrubIndex">Zero-based route scrub index.</param>
        /// <returns>True when the frame is cached.</returns>
        public bool IsCached
            (
            int RouteScrubIndex
            )
        {
            lock (_Lock)
            {
                return _Frames.ContainsKey(RouteScrubIndex);
            }
        }

        /// <summary>
        /// Returns whether partial generation state exists for the supplied index.
        /// </summary>
        /// <param name="RouteScrubIndex">Zero-based route scrub index.</param>
        /// <returns>True when a partial checkpoint is stored.</returns>
        public bool HasPartialState
            (
            int RouteScrubIndex
            )
        {
            lock (_Lock)
            {
                return _PartialStates.ContainsKey(RouteScrubIndex);
            }
        }

        /// <summary>
        /// Attempts to retrieve a cached frame.
        /// </summary>
        /// <param name="RouteScrubIndex">Zero-based route scrub index.</param>
        /// <param name="Frame">Cached frame when present.</param>
        /// <returns>True when the frame was found.</returns>
        public bool TryGetFrame
            (
            int RouteScrubIndex,
            out HaulScrubFrame? Frame
            )
        {
            lock (_Lock)
            {
                return _Frames.TryGetValue(RouteScrubIndex, out Frame);
            }
        }

        /// <summary>
        /// Stores a completed frame and clears any partial checkpoint for the same index.
        /// </summary>
        /// <param name="Frame">Completed frame to cache.</param>
        public void StoreFrame
            (
            HaulScrubFrame Frame
            )
        {
            lock (_Lock)
            {
                _Frames[Frame.RouteScrubIndex] = Frame;
                _PartialStates.Remove(Frame.RouteScrubIndex);
            }
        }

        /// <summary>
        /// Stores partial generation state for a preempted frame.
        /// </summary>
        /// <param name="State">Partial checkpoint.</param>
        public void StorePartialState
            (
            HaulPartialFrameState State
            )
        {
            lock (_Lock)
            {
                _PartialStates[State.RouteScrubIndex] = State;
            }
        }

        /// <summary>
        /// Clears all cached frames and partial checkpoints (§7.3.3).
        /// </summary>
        public void Clear
            (
            )
        {
            lock (_Lock)
            {
                _Frames.Clear();
                _PartialStates.Clear();
            }
        }
    }
}
