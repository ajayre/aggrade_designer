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

        /// <summary>
        /// Draws the water layer map overlay in 2D plan view.
        /// </summary>
        /// <param name="Canvas">Target canvas.</param>
        /// <param name="Viewport">Viewport rectangle in pixels.</param>
        /// <param name="Entry">Cached water layer to draw.</param>
        /// <param name="CentreLat">Map centre latitude.</param>
        /// <param name="CentreLon">Map centre longitude.</param>
        /// <param name="MetresPerPixel">Ground metres per screen pixel.</param>
        public static void Draw2D
            (
            SKCanvas Canvas,
            SKRect Viewport,
            WaterLayerCacheEntry Entry,
            double CentreLat,
            double CentreLon,
            double MetresPerPixel
            )
        {
            if ((MetresPerPixel <= 0.0) || !Entry.IsValidForDisplay())
                return;

            using SKBitmap Bitmap = CreateOverlayBitmap(Entry);
            if (Bitmap == null)
                return;

            MapViewport MapViewport = new MapViewport { Width = Viewport.Width, Height = Viewport.Height };
            if (!MapProjection.LatLonToScreen(Entry.MinLat, Entry.MinLon, CentreLat, CentreLon, MetresPerPixel, MapViewport, out float X0, out float Y0))
                return;

            if (!MapProjection.LatLonToScreen(Entry.MaxLat, Entry.MaxLon, CentreLat, CentreLon, MetresPerPixel, MapViewport, out float X1, out float Y1))
                return;

            SKRect Dest = SKRect.Create(Math.Min(X0, X1), Math.Min(Y0, Y1), Math.Abs(X1 - X0), Math.Abs(Y1 - Y0));
            if ((Dest.Width <= 0f) || (Dest.Height <= 0f))
                return;

            Canvas.DrawBitmap(Bitmap, Dest);
        }

        /// <summary>
        /// Draws the water layer draped on the active basis surface in 3D orbit view with camera-view clipping.
        /// </summary>
        /// <param name="Canvas">Target canvas.</param>
        /// <param name="Viewport">Viewport rectangle in pixels.</param>
        /// <param name="Entry">Cached water layer to draw.</param>
        /// <param name="Surface">Active basis surface sampler.</param>
        /// <param name="ContentView">Active field content view.</param>
        /// <param name="Camera">Orbit camera.</param>
        /// <param name="TopoDepth">Topo mesh depth buffer for occlusion testing.</param>
        internal static void Draw3D
            (
            SKCanvas Canvas,
            SKRect Viewport,
            WaterLayerCacheEntry Entry,
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
            using SKPaint Fill = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
            };

            for (Int32 Row = 0; Row < Rows; Row++)
            {
                double Lat0 = Entry.MaxLat - ((Row / (double)Math.Max(1, Rows - 1)) * LatSpan);
                double Lat1 = Entry.MaxLat - (((Row + 1) / (double)Math.Max(1, Rows - 1)) * LatSpan);
                for (Int32 Col = 0; Col < Cols; Col++)
                {
                    int Index = (Row * Cols) + Col;
                    byte Alpha = Entry.OverlayRgba[(Index * RGBA_BYTES_PER_CELL) + 3];
                    if (Alpha == 0)
                        continue;

                    double Lon0 = Entry.MinLon + ((Col / (double)Math.Max(1, Cols - 1)) * LonSpan);
                    double Lon1 = Entry.MinLon + (((Col + 1) / (double)Math.Max(1, Cols - 1)) * LonSpan);
                    double CentreLat = (Lat0 + Lat1) * 0.5;
                    double CentreLon = (Lon0 + Lon1) * 0.5;
                    double Elevation = Surface.SampleScalar(ContentView, CentreLat, CentreLon);
                    if (double.IsNaN(Elevation))
                        continue;

                    Camera.Project(
                        CentreLat,
                        CentreLon,
                        Elevation,
                        Viewport,
                        FlatPlane: false,
                        out float ScreenX,
                        out float ScreenY,
                        out double Depth);

                    if (!TopoDepth.IsVisible(ScreenX, ScreenY, Depth))
                        continue;

                    Fill.Color = new SKColor(
                        Entry.OverlayRgba[Index * RGBA_BYTES_PER_CELL],
                        Entry.OverlayRgba[(Index * RGBA_BYTES_PER_CELL) + 1],
                        Entry.OverlayRgba[(Index * RGBA_BYTES_PER_CELL) + 2],
                        Alpha);

                    float HalfSpan = Math.Max(
                        MIN_3D_CELL_SCREEN_SPAN_PX,
                        (float)(Entry.CellSizeM / Math.Max(Camera.DistanceM / Math.Max(Viewport.Height, 1f), 0.01)));
                    Canvas.DrawRect(SKRect.Create(ScreenX - HalfSpan, ScreenY - HalfSpan, HalfSpan * 2f, HalfSpan * 2f), Fill);
                }
            }
        }

        /// <summary>
        /// Creates an Skia bitmap from cached overlay RGBA bytes.
        /// </summary>
        /// <param name="Entry">Cache entry with overlay bytes.</param>
        /// <returns>Bitmap, or null when dimensions are invalid.</returns>
        private static SKBitmap? CreateOverlayBitmap
            (
            WaterLayerCacheEntry Entry
            )
        {
            if ((Entry.GridWidth <= 0) || (Entry.GridHeight <= 0))
                return null;

            SKBitmap Bitmap = new SKBitmap(Entry.GridWidth, Entry.GridHeight, SKColorType.Rgba8888, SKAlphaType.Premul);
            IntPtr Pixels = Bitmap.GetPixels();
            System.Runtime.InteropServices.Marshal.Copy(Entry.OverlayRgba, 0, Pixels, Entry.OverlayRgba.Length);
            return Bitmap;
        }
    }
}
