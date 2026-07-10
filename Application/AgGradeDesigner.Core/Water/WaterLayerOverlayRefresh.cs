using AgGradeDesigner.Core.Models;

namespace AgGradeDesigner.Core.Water
{
    /// <summary>
    /// Rebuilds water layer RGBA overlays from stored pond depths when display settings change.
    /// </summary>
    public static class WaterLayerOverlayRefresh
    {
        /// <summary>
        /// Refreshes overlays on all persisted and session water caches using current project settings.
        /// </summary>
        /// <param name="Project">Open project whose caches should be updated.</param>
        public static void RefreshProjectCaches
            (
            ProjectDocument Project
            )
        {
            WaterSettings Settings = Project.Water.Settings;
            RefreshEntry(Project.Water.ExistingSurfaceCache, Settings);
            RefreshEntry(Project.Water.ProposedSurfaceCache, Settings);
            WaterSessionCache.RefreshAllOverlays(Settings);
        }

        /// <summary>
        /// Rebuilds the overlay on one cache entry when it contains a valid depth grid.
        /// </summary>
        /// <param name="Entry">Cache entry to update, or null.</param>
        /// <param name="Settings">Display settings applied to the overlay.</param>
        private static void RefreshEntry
            (
            WaterLayerCacheEntry? Entry,
            WaterSettings Settings
            )
        {
            if ((Entry == null) || !Entry.IsValidForDisplay())
                return;

            PondingAnalysisEngine.RefreshOverlay(Entry, Settings);
        }
    }
}
