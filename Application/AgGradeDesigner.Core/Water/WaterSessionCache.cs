namespace AgGradeDesigner.Core.Water
{
    /// <summary>
    /// Session-scoped haul cut/fill water layer caches keyed by route scrub index (WATER-SPEC §5.1.2, §7.5.3).
    /// </summary>
    public static class WaterSessionCache
    {
        /// <summary>Serializes access to haul cache entries.</summary>
        private static readonly object Gate = new object();

        /// <summary>Haul water caches keyed by route scrub index.</summary>
        private static readonly Dictionary<int, WaterLayerCacheEntry> HaulCaches = new Dictionary<int, WaterLayerCacheEntry>();

        /// <summary>Identity of the haul data instance these caches belong to.</summary>
        private static Haul.HaulData? BoundHaulData;

        /// <summary>
        /// Discards all haul cut/fill water layer caches (WATER-SPEC §6.3.6).
        /// </summary>
        public static void DiscardAllHaulCaches
            (
            )
        {
            lock (Gate)
            {
                HaulCaches.Clear();
                BoundHaulData = null;
            }
        }

        /// <summary>
        /// Discards the haul cache for one route scrub index when its elevation field changes.
        /// </summary>
        /// <param name="RouteScrubIndex">Route scrub index whose cache should be removed.</param>
        public static void DiscardHaulCache
            (
            int RouteScrubIndex
            )
        {
            lock (Gate)
            {
                HaulCaches.Remove(RouteScrubIndex);
            }
        }

        /// <summary>
        /// Binds haul caches to the active haul data and clears stale entries when haul data is replaced.
        /// </summary>
        /// <param name="Data">Active haul data, or null when haul is cleared.</param>
        public static void BindHaulData
            (
            Haul.HaulData? Data
            )
        {
            lock (Gate)
            {
                if (!ReferenceEquals(BoundHaulData, Data))
                {
                    HaulCaches.Clear();
                    BoundHaulData = Data;
                }
            }
        }

        /// <summary>
        /// Stores or replaces the haul water cache for a route scrub index.
        /// </summary>
        /// <param name="RouteScrubIndex">Route scrub index.</param>
        /// <param name="Entry">Cache entry to store.</param>
        public static void StoreHaulCache
            (
            int RouteScrubIndex,
            WaterLayerCacheEntry Entry
            )
        {
            lock (Gate)
            {
                HaulCaches[RouteScrubIndex] = Entry;
            }
        }

        /// <summary>
        /// Returns the haul water cache for a route scrub index when present and valid.
        /// </summary>
        /// <param name="RouteScrubIndex">Route scrub index.</param>
        /// <returns>Valid cache entry, or null when missing.</returns>
        public static WaterLayerCacheEntry? TryGetHaulCache
            (
            int RouteScrubIndex
            )
        {
            lock (Gate)
            {
                if (HaulCaches.TryGetValue(RouteScrubIndex, out WaterLayerCacheEntry? Entry) &&
                    (Entry != null) &&
                    (Entry != null) &&
                    Entry.IsValidForDisplay())
                {
                    return Entry;
                }

                return null;
            }
        }
    }
}
