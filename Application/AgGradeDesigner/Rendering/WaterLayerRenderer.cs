using System.Runtime.CompilerServices;

using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Core.Water;

using SkiaSharp;

namespace AgGradeDesigner.Rendering
{
    /// <summary>
    /// Draws cached water layer decorations in 2D and 3D field views (WATER-SPEC §7.4).
    /// </summary>
    public static class WaterLayerRenderer
    {
        /// <summary>RGBA bytes per overlay cell.</summary>
        private const int RGBA_BYTES_PER_CELL = 4;

        /// <summary>Minimum screen span before skipping a water cell in 3D.</summary>
        private const float MIN_3D_CELL_SCREEN_SPAN_PX = 0.5f;

        /// <summary>Reuses decoded overlay bitmaps while the cache entry overlay buffer is unchanged.</summary>
        private static readonly ConditionalWeakTable<WaterLayerCacheEntry, OverlayBitmapCache> OverlayBitmapCaches = new ConditionalWeakTable<WaterLayerCacheEntry, OverlayBitmapCache>();

        /// <summary>
        /// Draws the water layer map overlay in 2D plan view.
        /// </summary>
        /// <param name="Canvas">Target canvas.</param>
        /// <param name="Viewport">Viewport rectangle in pixels.</param>
        /// <param name="Entry">Cached water layer to draw.</param>
        /// <param name="DisplaySettings">Display settings controlling minimum depth and colour scale.</param>
        /// <param name="CentreLat">Map centre latitude.</param>
        /// <param name="CentreLon">Map centre longitude.</param>
        /// <param name="MetresPerPixel">Ground metres per screen pixel.</param>
        public static void Draw2D
            (
            SKCanvas Canvas,
            SKRect Viewport,
            WaterLayerCacheEntry Entry,
            WaterSettings DisplaySettings,
            double CentreLat,
            double CentreLon,
            double MetresPerPixel
            )
        {
            if ((MetresPerPixel <= 0.0) || !Entry.IsValidForDisplay())
                return;

            MapViewport MapViewport = new MapViewport { Width = Viewport.Width, Height = Viewport.Height };
            if (!MapProjection.LatLonToScreen(Entry.MinLat, Entry.MinLon, CentreLat, CentreLon, MetresPerPixel, MapViewport, out float X0, out float Y0))
                return;

            if (!MapProjection.LatLonToScreen(Entry.MaxLat, Entry.MaxLon, CentreLat, CentreLon, MetresPerPixel, MapViewport, out float X1, out float Y1))
                return;

            SKRect Dest = SKRect.Create(Math.Min(X0, X1), Math.Min(Y0, Y1), Math.Abs(X1 - X0), Math.Abs(Y1 - Y0));
            if ((Dest.Width <= 0f) || (Dest.Height <= 0f))
                return;

            SKBitmap Bitmap = GetOrCreateOverlayBitmap(Entry);
            using SKPaint Paint = new SKPaint
            {
                IsAntialias = true,
            };
            Canvas.DrawBitmap(Bitmap, Dest, Paint);
        }

        /// <summary>
        /// Draws the water layer draped on the active basis surface in 3D orbit view with camera-view clipping.
        /// </summary>
        /// <param name="Canvas">Target canvas.</param>
        /// <param name="Viewport">Viewport rectangle in pixels.</param>
        /// <param name="Entry">Cached water layer to draw.</param>
        /// <param name="DisplaySettings">Display settings controlling minimum depth and colour scale.</param>
        /// <param name="Surface">Active basis surface sampler.</param>
        /// <param name="ContentView">Active field content view.</param>
        /// <param name="Camera">Orbit camera.</param>
        /// <param name="TopoDepth">Topo mesh depth buffer for occlusion testing.</param>
        internal static void Draw3D
            (
            SKCanvas Canvas,
            SKRect Viewport,
            WaterLayerCacheEntry Entry,
            WaterSettings DisplaySettings,
            FieldSurfaceSampler Surface,
            FieldContentView ContentView,
            FieldCamera3D Camera,
            MeshDepthBuffer TopoDepth
            )
        {
            if (!Entry.IsValidForDisplay())
                return;

            double LatSpan = Entry.MaxLat - Entry.MinLat;
            double LonSpan = Entry.MaxLon - Entry.MinLon;
            if ((LatSpan <= 0.0) || (LonSpan <= 0.0))
                return;

            int Cols = Entry.GridWidth;
            int Rows = Entry.GridHeight;
            int RasterWidth = Entry.ResolveOverlayRasterWidth();
            int RasterHeight = Entry.ResolveOverlayRasterHeight();
            using SKPaint Fill = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
            };

            for (Int32 Row = 0; Row < Rows; Row++)
            {
                double RowF = (Row + 0.5) / Math.Max(1, Rows - 1) * Math.Max(1, RasterHeight - 1);
                double Lat = Entry.MaxLat - ((Row + 0.5) / Math.Max(1, Rows - 1)) * LatSpan;
                for (Int32 Col = 0; Col < Cols; Col++)
                {
                    double ColF = (Col + 0.5) / Math.Max(1, Cols - 1) * Math.Max(1, RasterWidth - 1);
                    if (!PondingAnalysisEngine.TrySampleOverlayBilinear(
                            Entry.OverlayRgba,
                            RasterWidth,
                            RasterHeight,
                            ColF,
                            RowF,
                            out WaterOverlayPixel Pixel))
                    {
                        continue;
                    }

                    double Lon = Entry.MinLon + ((Col + 0.5) / Math.Max(1, Cols - 1)) * LonSpan;
                    double Elevation = Surface.SampleScalar(ContentView, Lat, Lon);
                    if (double.IsNaN(Elevation))
                        continue;

                    Camera.Project(
                        Lat,
                        Lon,
                        Elevation,
                        Viewport,
                        FlatPlane: false,
                        out float ScreenX,
                        out float ScreenY,
                        out double Depth);

                    if (!TopoDepth.IsVisible(ScreenX, ScreenY, Depth))
                        continue;

                    Fill.Color = new SKColor(Pixel.R, Pixel.G, Pixel.B, Pixel.A);

                    float HalfSpan = Math.Max(
                        MIN_3D_CELL_SCREEN_SPAN_PX,
                        (float)(Entry.CellSizeM / Math.Max(Camera.DistanceM / Math.Max(Viewport.Height, 1f), 0.01)));
                    Canvas.DrawRect(SKRect.Create(ScreenX - HalfSpan, ScreenY - HalfSpan, HalfSpan * 2f, HalfSpan * 2f), Fill);
                }
            }
        }

        /// <summary>
        /// Returns a cached Skia bitmap for the entry overlay, rebuilding when the RGBA buffer changes.
        /// </summary>
        /// <param name="Entry">Cache entry whose overlay should be drawn.</param>
        /// <returns>Non-disposable cached bitmap owned by the renderer cache.</returns>
        private static SKBitmap GetOrCreateOverlayBitmap
            (
            WaterLayerCacheEntry Entry
            )
        {
            OverlayBitmapCache Cache = OverlayBitmapCaches.GetOrCreateValue(Entry);
            lock (Cache)
            {
                if ((Cache.Bitmap != null) && ReferenceEquals(Cache.Source, Entry.OverlayRgba))
                    return Cache.Bitmap;

                Cache.Bitmap?.Dispose();
                Cache.Bitmap = CreateOverlayBitmap(Entry);
                Cache.Source = Entry.OverlayRgba;
                return Cache.Bitmap;
            }
        }

        /// <summary>
        /// Creates an Skia bitmap from cached overlay RGBA bytes.
        /// </summary>
        /// <param name="Entry">Cache entry with overlay bytes.</param>
        /// <returns>Bitmap, or empty bitmap when dimensions are invalid.</returns>
        private static SKBitmap CreateOverlayBitmap
            (
            WaterLayerCacheEntry Entry
            )
        {
            int Width = Entry.ResolveOverlayRasterWidth();
            int Height = Entry.ResolveOverlayRasterHeight();
            if ((Width <= 0) || (Height <= 0))
                return new SKBitmap(1, 1, SKColorType.Rgba8888, SKAlphaType.Unpremul);

            SKBitmap Bitmap = new SKBitmap(Width, Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
            IntPtr Pixels = Bitmap.GetPixels();
            System.Runtime.InteropServices.Marshal.Copy(Entry.OverlayRgba, 0, Pixels, Entry.OverlayRgba.Length);
            return Bitmap;
        }

        /// <summary>Cached overlay bitmap keyed by source RGBA buffer identity.</summary>
        private sealed class OverlayBitmapCache
        {
            /// <summary>Overlay RGBA buffer currently represented by <see cref="Bitmap"/>.</summary>
            public byte[]? Source;

            /// <summary>Cached Skia bitmap for fast 2D compositing.</summary>
            public SKBitmap? Bitmap;
        }
    }
}
