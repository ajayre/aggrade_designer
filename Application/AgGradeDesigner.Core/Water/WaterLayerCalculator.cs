using AgGradeDesigner.Core.Models;

namespace AgGradeDesigner.Core.Water
{
    /// <summary>
    /// Runs asynchronous surface water layer calculation off the UI thread (WATER-SPEC §6.0.6).
    /// </summary>
    public static class WaterLayerCalculator
    {
        /// <summary>
        /// Computes the water layer for the active field content view basis surface.
        /// </summary>
        /// <param name="Project">Open project.</param>
        /// <param name="ContentView">Active field content view.</param>
        /// <param name="RouteScrubIndex">Active haul route scrub index.</param>
        /// <param name="Progress">Optional monotonic progress reporter.</param>
        /// <param name="CancellationToken">Cancellation token.</param>
        /// <returns>Computed cache entry on success.</returns>
        /// <exception cref="InvalidOperationException">Basis surface grid or haul frame is unavailable.</exception>
        public static async Task<WaterLayerCacheEntry> ComputeAsync
            (
            ProjectDocument Project,
            FieldContentView ContentView,
            int RouteScrubIndex,
            IProgress<int>? Progress = null,
            CancellationToken CancellationToken = default
            )
        {
            WaterBasisSurfaceKind? Kind = WaterLayerResolver.ResolveBasisSurfaceKind(ContentView);
            if (Kind == null)
                throw new InvalidOperationException("Water layer calculation is not supported for the active view.");

            if ((Kind == WaterBasisSurfaceKind.HaulCutFill) &&
                (!Project.HasHaulData || (Project.HaulData == null)))
            {
                throw new InvalidOperationException("Haul cut/fill surface is unavailable until haul data exists.");
            }

            WaterSettings Settings = Project.Water.Settings.Clone();
            return await Task.Run(
                () =>
                {
                    CancellationToken.ThrowIfCancellationRequested();
                    WaterElevationGridBuilder.ElevationGrid? Grid = WaterElevationGridBuilder.TryBuild(
                        Project,
                        Kind.Value,
                        RouteScrubIndex);
                    if (Grid == null)
                        throw new InvalidOperationException("Basis surface elevation grid is unavailable.");

                    CancellationToken.ThrowIfCancellationRequested();
                    int? StoredRouteIndex = Kind == WaterBasisSurfaceKind.HaulCutFill ? RouteScrubIndex : null;
                    WaterLayerCacheEntry Entry = PondingAnalysisEngine.Compute(
                        Grid,
                        Settings,
                        Kind.Value,
                        StoredRouteIndex,
                        Progress);
                    WaterLayerResolver.StoreCacheEntry(Project, Entry);
                    Project.MarkModified();
                    return Entry;
                },
                CancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Returns true when calculation can run for the active view and basis surface.
        /// </summary>
        /// <param name="Project">Open project.</param>
        /// <param name="ContentView">Active field content view.</param>
        /// <param name="RouteScrubIndex">Active haul route scrub index.</param>
        /// <returns>True when a basis surface grid can be built.</returns>
        public static bool CanComputeForActiveView
            (
            ProjectDocument Project,
            FieldContentView ContentView,
            int RouteScrubIndex
            )
        {
            WaterBasisSurfaceKind? Kind = WaterLayerResolver.ResolveBasisSurfaceKind(ContentView);
            if (Kind == null)
                return false;

            if ((Kind == WaterBasisSurfaceKind.HaulCutFill) && (!Project.HasHaulData))
                return false;

            return WaterElevationGridBuilder.TryBuild(Project, Kind.Value, RouteScrubIndex) != null;
        }
    }
}
