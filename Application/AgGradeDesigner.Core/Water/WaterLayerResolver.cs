using AgGradeDesigner.Core.Models;

namespace AgGradeDesigner.Core.Water
{
    /// <summary>
    /// Resolves cached water layer results for field content views and basis surfaces (WATER-SPEC §6.2, §7.3).
    /// </summary>
    public static class WaterLayerResolver
    {
        /// <summary>
        /// Maps a field content view to its basis surface kind.
        /// </summary>
        /// <param name="ContentView">Active field content view.</param>
        /// <returns>Basis surface kind, or null for unsupported views.</returns>
        public static WaterBasisSurfaceKind? ResolveBasisSurfaceKind
            (
            FieldContentView ContentView
            )
        {
            return ContentView switch
            {
                FieldContentView.Existing => WaterBasisSurfaceKind.Existing,
                FieldContentView.CutFill => WaterBasisSurfaceKind.Existing,
                FieldContentView.Proposed => WaterBasisSurfaceKind.Proposed,
                FieldContentView.Haul => WaterBasisSurfaceKind.HaulCutFill,
                _ => null,
            };
        }

        /// <summary>
        /// Returns true when a valid cached water layer exists for the active view's basis surface.
        /// </summary>
        /// <param name="Project">Open project.</param>
        /// <param name="ContentView">Active field content view.</param>
        /// <param name="RouteScrubIndex">Active haul route scrub index when the haul view is active.</param>
        /// <returns>True when the view has a water layer to show.</returns>
        public static bool HasWaterLayerToShow
            (
            ProjectDocument Project,
            FieldContentView ContentView,
            int RouteScrubIndex
            )
        {
            return TryResolveCacheEntry(Project, ContentView, RouteScrubIndex) != null;
        }

        /// <summary>
        /// Returns true when the cache entry for the active basis surface is missing or stale.
        /// </summary>
        /// <param name="Project">Open project.</param>
        /// <param name="ContentView">Active field content view.</param>
        /// <param name="RouteScrubIndex">Active haul route scrub index when the haul view is active.</param>
        /// <returns>True when recalculation is required on Water dialog OK.</returns>
        public static bool IsCacheMissingOrStale
            (
            ProjectDocument Project,
            FieldContentView ContentView,
            int RouteScrubIndex
            )
        {
            WaterLayerCacheEntry? Entry = TryResolveCacheEntry(Project, ContentView, RouteScrubIndex);
            if (Entry == null)
                return true;

            return !Entry.MatchesSettings(Project.Water.Settings);
        }

        /// <summary>
        /// Returns the valid cached water layer for the active view, if any.
        /// </summary>
        /// <param name="Project">Open project.</param>
        /// <param name="ContentView">Active field content view.</param>
        /// <param name="RouteScrubIndex">Active haul route scrub index when the haul view is active.</param>
        /// <returns>Valid cache entry, or null.</returns>
        public static WaterLayerCacheEntry? TryResolveCacheEntry
            (
            ProjectDocument Project,
            FieldContentView ContentView,
            int RouteScrubIndex
            )
        {
            WaterBasisSurfaceKind? Kind = ResolveBasisSurfaceKind(ContentView);
            if (Kind == null)
                return null;

            WaterLayerCacheEntry? Entry = Kind switch
            {
                WaterBasisSurfaceKind.Existing => Project.Water.ExistingSurfaceCache,
                WaterBasisSurfaceKind.Proposed => Project.Water.ProposedSurfaceCache,
                WaterBasisSurfaceKind.HaulCutFill => WaterSessionCache.TryGetHaulCache(RouteScrubIndex),
                _ => null,
            };

            if ((Entry == null) || !Entry.IsValidForDisplay())
                return null;

            if (!Entry.MatchesSettings(Project.Water.Settings))
                return null;

            return Entry;
        }

        /// <summary>
        /// Stores a computed cache entry in the appropriate project or session store.
        /// </summary>
        /// <param name="Project">Open project.</param>
        /// <param name="Entry">Computed cache entry.</param>
        public static void StoreCacheEntry
            (
            ProjectDocument Project,
            WaterLayerCacheEntry Entry
            )
        {
            switch (Entry.BasisSurface)
            {
                case WaterBasisSurfaceKind.Existing:
                    Project.Water.ExistingSurfaceCache = Entry;
                    break;
                case WaterBasisSurfaceKind.Proposed:
                    Project.Water.ProposedSurfaceCache = Entry;
                    break;
                case WaterBasisSurfaceKind.HaulCutFill:
                    if (Entry.RouteScrubIndex.HasValue)
                        WaterSessionCache.StoreHaulCache(Entry.RouteScrubIndex.Value, Entry);
                    break;
            }
        }

        /// <summary>
        /// Discards persisted and session water caches for the project.
        /// </summary>
        /// <param name="Project">Open project.</param>
        public static void DiscardAllCaches
            (
            ProjectDocument Project
            )
        {
            Project.Water.ClearPersistedCaches();
            WaterSessionCache.DiscardAllHaulCaches();
        }
    }
}
