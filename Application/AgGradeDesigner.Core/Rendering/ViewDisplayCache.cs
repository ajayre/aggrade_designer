using AgGradeDesigner.Core.Models;

using SkiaSharp;

namespace AgGradeDesigner.Core.Rendering
{
    /// <summary>
    /// Per–content-view display caches (colour maps, contours, mesh hillshade).
    /// </summary>
    internal sealed class ViewDisplayCache
    {
        /// <summary>Base plan-view colour map without hillshade modulation.</summary>
        public SKBitmap? ColorMapBase { get; set; }

        /// <summary>Hillshade-modulated plan-view colour map derived from <see cref="ColorMapBase"/>.</summary>
        public SKBitmap? ColorMapHillshadeCache { get; set; }

        /// <summary>Per-cell hillshade factors used for plan-view compositing.</summary>
        public double[]? PlanViewShadeGrid { get; set; }

        /// <summary>Minimum scalar value used when <see cref="ColorMapBase"/> was built.</summary>
        public double ColorMapVMin { get; set; }

        /// <summary>Maximum scalar value used when <see cref="ColorMapBase"/> was built.</summary>
        public double ColorMapVMax { get; set; }

        /// <summary>Supersample factor used when <see cref="ColorMapBase"/> was built.</summary>
        public int ColorMapSupersample { get; set; }

        /// <summary>Cached contour polylines grouped by elevation level.</summary>
        public IReadOnlyList<(double Level, IReadOnlyList<(double Lat, double Lon)> Points)>? ContourCacheByLevel { get; set; }

        /// <summary>Minimum scalar value used when contours were extracted.</summary>
        public double ContourCacheVMin { get; set; }

        /// <summary>Maximum scalar value used when contours were extracted.</summary>
        public double ContourCacheVMax { get; set; }

        /// <summary>Per-vertex hillshade factors for 3D mesh shading.</summary>
        public double[]? MeshHillshadeCache { get; set; }

        /// <summary>
        /// Disposes colour-map bitmaps for this content view.
        /// </summary>
        public void InvalidateColorMaps
            (
            )
        {
            ColorMapBase?.Dispose();
            ColorMapHillshadeCache?.Dispose();
            ColorMapBase = null;
            ColorMapHillshadeCache = null;
            PlanViewShadeGrid = null;
            ColorMapVMin = 0;
            ColorMapVMax = 0;
            ColorMapSupersample = 0;
        }

        /// <summary>
        /// Clears cached contour polylines for this content view.
        /// </summary>
        public void InvalidateContours
            (
            )
        {
            ContourCacheByLevel = null;
            ContourCacheVMin = 0;
            ContourCacheVMax = 0;
        }

        /// <summary>
        /// Clears cached mesh hillshade factors for this content view.
        /// </summary>
        public void InvalidateMeshHillshade
            (
            )
        {
            MeshHillshadeCache = null;
        }

        /// <summary>
        /// Disposes all cached display resources for this content view.
        /// </summary>
        public void InvalidateAll
            (
            )
        {
            InvalidateColorMaps();
            InvalidateContours();
            InvalidateMeshHillshade();
        }
    }

    /// <summary>
    /// Stores independent display caches keyed by field content view.
    /// </summary>
    internal sealed class ViewDisplayCacheStore
    {
        /// <summary>Independent display caches keyed by field content view.</summary>
        private readonly Dictionary<FieldContentView, ViewDisplayCache> _Caches = new Dictionary<FieldContentView, ViewDisplayCache>();

        /// <summary>
        /// Returns the cache bucket for a content view, creating it when missing.
        /// </summary>
        /// <param name="ContentView">Scalar channel whose cache is requested.</param>
        /// <returns>Mutable display cache for the content view.</returns>
        public ViewDisplayCache Get
            (
            FieldContentView ContentView
            )
        {
            if (!_Caches.TryGetValue(ContentView, out ViewDisplayCache? Cache))
            {
                Cache = new ViewDisplayCache();
                _Caches[ContentView] = Cache;
            }

            return Cache;
        }

        /// <summary>
        /// Disposes and clears all content-view caches.
        /// </summary>
        public void InvalidateAll
            (
            )
        {
            foreach (ViewDisplayCache Cache in _Caches.Values)
                Cache.InvalidateAll();

            _Caches.Clear();
        }

        /// <summary>
        /// Clears contour caches for every content view.
        /// </summary>
        public void InvalidateAllContours
            (
            )
        {
            foreach (ViewDisplayCache Cache in _Caches.Values)
                Cache.InvalidateContours();
        }
    }
}
