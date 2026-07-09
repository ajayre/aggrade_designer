namespace AgGradeDesigner.Core.Water
{
    /// <summary>
    /// Project-owned water settings and persisted cached water layer results (WATER-SPEC §5.1.2, §7.5).
    /// </summary>
    public sealed class WaterLayerState
    {
        /// <summary>Current ponding parameters for the open project.</summary>
        public WaterSettings Settings { get; set; } = new WaterSettings();

        /// <summary>Cached water layer for the existing surface (shared with Cut/fill map).</summary>
        public WaterLayerCacheEntry? ExistingSurfaceCache { get; set; }

        /// <summary>Cached water layer for the proposed surface.</summary>
        public WaterLayerCacheEntry? ProposedSurfaceCache { get; set; }

        /// <summary>
        /// Clears persisted cache entries without resetting settings.
        /// </summary>
        public void ClearPersistedCaches
            (
            )
        {
            ExistingSurfaceCache = null;
            ProposedSurfaceCache = null;
        }

        /// <summary>
        /// Applies default water settings when a project file omits the Water element.
        /// </summary>
        public void ApplyDefaultSettingsIfNeeded
            (
            )
        {
            Settings.ApplyDefaults();
        }
    }
}
