using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;

namespace AgGradeDesigner.Core.Rendering
{
    /// <summary>
    /// Headless map view state used by unit tests and the UI viewer (§2.5, §8.3).
    /// </summary>
    public sealed class FieldMapView
    {
        /// <summary>Viewport width in pixels.</summary>
        public int ViewportWidth { get; set; } = 800;

        /// <summary>Viewport height in pixels.</summary>
        public int ViewportHeight { get; set; } = 600;

        /// <summary>Map centre latitude in decimal degrees.</summary>
        public double CentreLat { get; set; } = 36.0;

        /// <summary>Map centre longitude in decimal degrees.</summary>
        public double CentreLon { get; set; } = -90.0;

        /// <summary>Zoom multiplier applied to the base metres-per-pixel estimate.</summary>
        public double ZoomFactor { get; set; } = 1.0;

        /// <summary>When true, satellite basemap tiles are composited beneath field overlays.</summary>
        public bool ShowSatellite { get; set; }

        /// <summary>Ground metres represented by one screen pixel at the current zoom.</summary>
        public double MetresPerPixel =>
            SatelliteMapLayout.EstimateMetresPerPixel(CentreLat, ZoomFactor);

        /// <summary>Pixel dimensions packaged for projection helpers.</summary>
        public MapViewport Viewport =>
            new MapViewport { Width = ViewportWidth, Height = ViewportHeight };

        /// <summary>
        /// Frames the map camera to the geographic extent of a project.
        /// </summary>
        /// <param name="Project">Project containing survey or design data.</param>
        public void FitToProject
            (
            ProjectDocument? Project
            )
        {
            if (!FieldBounds.TryFromProject(Project, out FieldBounds Bounds))
                return;

            MapViewFitResult Fit = MapViewFitter.Fit(Bounds, ViewportWidth, ViewportHeight);
            CentreLat = Fit.CentreLat;
            CentreLon = Fit.CentreLon;
            ZoomFactor = Fit.ZoomFactor;
        }

        /// <summary>
        /// Returns whether all field corners lie inside the viewport within a pixel margin.
        /// </summary>
        /// <param name="Project">Project whose bounds are tested.</param>
        /// <param name="MarginPx">Extra tolerance beyond viewport edges in pixels.</param>
        /// <returns>True when every corner projects on screen within the margin.</returns>
        public bool FieldFillsViewport
            (
            ProjectDocument? Project,
            float MarginPx = 8
            )
        {
            if (!FieldBounds.TryFromProject(Project, out FieldBounds Bounds))
                return false;

            double Mpp = MetresPerPixel;
            MapViewport Vp = Viewport;

            (double Lat, double Lon)[] Corners =
            {
                (Bounds.MinLat, Bounds.MinLon),
                (Bounds.MinLat, Bounds.MaxLon),
                (Bounds.MaxLat, Bounds.MinLon),
                (Bounds.MaxLat, Bounds.MaxLon)
            };

            // Require every bounding-box corner to project inside the padded viewport.
            foreach ((double Lat, double Lon) Corner in Corners)
            {
                MapProjection.LatLonToScreen(
                    Corner.Lat,
                    Corner.Lon,
                    CentreLat,
                    CentreLon,
                    Mpp,
                    Vp,
                    out float X,
                    out float Y);

                if (!MapProjection.IsOnScreen(X, Y, Vp, MarginPx))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Prefetches tiles and composites a satellite basemap PNG for the current view.
        /// </summary>
        /// <param name="TileService">Tile download and cache service.</param>
        /// <param name="CancellationToken">Cancellation token for tile downloads.</param>
        /// <returns>Render statistics and PNG bytes when satellite display is enabled.</returns>
        public async Task<SatelliteMapRenderResult> RenderSatelliteAsync
            (
            SatelliteTileService TileService,
            CancellationToken CancellationToken = default
            )
        {
            if (!ShowSatellite)
            {
                return new SatelliteMapRenderResult
                {
                    TilesRequested = 0,
                    TilesDrawn = 0,
                    PlaceholdersDrawn = 0
                };
            }

            SatelliteMapViewport Layout = SatelliteMapLayout.Compute(
                CentreLat,
                CentreLon,
                ViewportWidth,
                ViewportHeight,
                MetresPerPixel);

            // Ensure every visible tile is cached before compositing.
            foreach (SatelliteTileRequest Tile in Layout.Tiles)
            {
                byte[]? Data = await TileService.EnsureTileAsync(Layout.Zoom, Tile.TileX, Tile.TileY, CancellationToken).ConfigureAwait(false);
                if (Data == null)
                    throw new InvalidOperationException($"Failed to download tile z={Layout.Zoom} x={Tile.TileX} y={Tile.TileY}.");
            }

            return SatelliteMapRenderer.Render(
                CentreLat,
                CentreLon,
                ViewportWidth,
                ViewportHeight,
                MetresPerPixel,
                TileService);
        }
    }
}
