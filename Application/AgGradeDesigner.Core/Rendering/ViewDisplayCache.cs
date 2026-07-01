using AgGradeDesigner.Core.Models;

using SkiaSharp;

namespace AgGradeDesigner.Core.Rendering
{
    /// <summary>
    /// Per–content-view display caches (colour maps, contours, mesh hillshade).
    /// </summary>
    internal sealed class ViewDisplayCache
    {
        public SKBitmap? ColorMapBase { get; set; }
        public SKBitmap? ColorMapHillshadeCache { get; set; }
        public double[]? PlanViewShadeGrid { get; set; }
        public double ColorMapVMin { get; set; }
        public double ColorMapVMax { get; set; }
        public int ColorMapSupersample { get; set; }
        public IReadOnlyList<(double Level, IReadOnlyList<(double Lat, double Lon)> Points)>? ContourCacheByLevel { get; set; }
        public double ContourCacheVMin { get; set; }
        public double ContourCacheVMax { get; set; }
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
        private readonly Dictionary<FieldContentView, ViewDisplayCache> _Caches = new Dictionary<FieldContentView, ViewDisplayCache>();

        /// <summary>
        /// Returns the cache bucket for a content view, creating it when missing.
        /// </summary>
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
