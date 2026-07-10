using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;

namespace AgGradeDesigner.Core.Water
{
    /// <summary>
    /// Restores geographic bounds on water layer cache entries loaded from legacy blobs (WATER-SPEC Appendix A).
    /// </summary>
    public static class WaterLayerCacheGeography
    {
        /// <summary>
        /// Returns true when the cache entry carries a usable lat/lon envelope for map compositing.
        /// </summary>
        /// <param name="Entry">Cache entry to inspect.</param>
        /// <returns>True when min/max latitude and longitude form a positive span.</returns>
        public static bool HasValidGeographicBounds
            (
            WaterLayerCacheEntry Entry
            )
        {
            return (Entry.MaxLat > Entry.MinLat) && (Entry.MaxLon > Entry.MinLon);
        }

        /// <summary>
        /// Attempts to populate missing geographic bounds from the open project design grid.
        /// </summary>
        /// <param name="Project">Open project.</param>
        /// <param name="Entry">Cache entry receiving bounds when missing.</param>
        /// <param name="BasisSurface">Basis surface kind for the cache entry.</param>
        /// <returns>True when bounds were restored or already valid.</returns>
        public static bool TryRestoreFromProject
            (
            ProjectDocument Project,
            WaterLayerCacheEntry Entry,
            WaterBasisSurfaceKind BasisSurface
            )
        {
            if (HasValidGeographicBounds(Entry))
                return true;

            if ((Project.DesignGrid == null) ||
                (Project.DesignGrid.Width != Entry.GridWidth) ||
                (Project.DesignGrid.Height != Entry.GridHeight))
            {
                return FieldBounds.TryFromProject(Project, out FieldBounds Bounds) &&
                    ApplyFieldBounds(Entry, Bounds);
            }

            WaterElevationGridBuilder.ElevationGrid? Grid = WaterElevationGridBuilder.TryBuild(
                Project,
                BasisSurface,
                Entry.RouteScrubIndex ?? 0);
            if (Grid == null)
                return false;

            Entry.MinLat = Grid.MinLat;
            Entry.MaxLat = Grid.MaxLat;
            Entry.MinLon = Grid.MinLon;
            Entry.MaxLon = Grid.MaxLon;
            return HasValidGeographicBounds(Entry);
        }

        /// <summary>
        /// Copies field bounds onto a cache entry when the design grid is unavailable.
        /// </summary>
        /// <param name="Entry">Cache entry receiving bounds.</param>
        /// <param name="Bounds">Project field bounds.</param>
        /// <returns>True when the copied bounds are valid.</returns>
        private static bool ApplyFieldBounds
            (
            WaterLayerCacheEntry Entry,
            FieldBounds Bounds
            )
        {
            Entry.MinLat = Bounds.MinLat;
            Entry.MaxLat = Bounds.MaxLat;
            Entry.MinLon = Bounds.MinLon;
            Entry.MaxLon = Bounds.MaxLon;
            return HasValidGeographicBounds(Entry);
        }

        /// <summary>
        /// Maps a WGS-84 location to continuous pond-grid row/column coordinates.
        /// </summary>
        /// <param name="Entry">Cache entry whose geographic bounds define the grid envelope.</param>
        /// <param name="Lat">Point latitude in decimal degrees.</param>
        /// <param name="Lon">Point longitude in decimal degrees.</param>
        /// <param name="ColF">Continuous column coordinate aligned to <see cref="WaterLayerCacheEntry.GridWidth"/>.</param>
        /// <param name="RowF">Continuous row coordinate aligned to <see cref="WaterLayerCacheEntry.GridHeight"/>.</param>
        /// <returns>True when the cache entry has a usable geographic envelope.</returns>
        public static bool TryLatLonToGridCoordinates
            (
            WaterLayerCacheEntry Entry,
            double Lat,
            double Lon,
            out double ColF,
            out double RowF
            )
        {
            ColF = 0.0;
            RowF = 0.0;
            if (!HasValidGeographicBounds(Entry))
                return false;

            double LatSpan = Entry.MaxLat - Entry.MinLat;
            double LonSpan = Entry.MaxLon - Entry.MinLon;
            RowF = ((Entry.MaxLat - Lat) / LatSpan) * Math.Max(0, Entry.GridHeight - 1);
            ColF = ((Lon - Entry.MinLon) / LonSpan) * Math.Max(0, Entry.GridWidth - 1);
            return true;
        }
    }
}
