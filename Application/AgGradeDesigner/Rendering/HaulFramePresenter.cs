using System.Diagnostics;
using System.Globalization;

using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Haul;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Diagnostics;

using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

using SkiaSharp;

namespace AgGradeDesigner.Rendering
{
    /// <summary>
    /// Decodes haul scrub PNG bytes and draws them in geographic map space.
    /// Field boundary and route use separate caches so scrub updates never rebuild the boundary layer.
    /// </summary>
    public static class HaulFramePresenter
    {
        private const double OPACITY_PERCENT_TO_UNIT = 100.0;
        private const byte SATELLITE_BACKGROUND_RED = 118;
        private const byte SATELLITE_BACKGROUND_GREEN = 132;
        private const byte SATELLITE_BACKGROUND_BLUE = 72;

        private static readonly object CacheLock = new object();
        private static byte[]? CachedBoundaryBytes;
        private static Bitmap? CachedBoundaryBitmap;
        private static byte[]? CachedFrameBytes;
        private static Bitmap? CachedFrameBitmap;
        private static byte[]? CachedRouteOverlayBytes;
        private static Bitmap? CachedRouteOverlayBitmap;
        private static string? CachedSatelliteKey;
        private static int CachedSatelliteGeneration = -1;
        private static Bitmap? CachedSatelliteBitmap;

        /// <summary>
        /// Clears decoded bitmap caches for the haul session (boundary, route, and satellite base).
        /// </summary>
        public static void ClearSession()
        {
            lock (CacheLock)
            {
                CachedBoundaryBytes = null;
                CachedFrameBytes = null;
                CachedRouteOverlayBytes = null;
                CachedBoundaryBitmap?.Dispose();
                CachedBoundaryBitmap = null;
                CachedFrameBitmap?.Dispose();
                CachedFrameBitmap = null;
                CachedRouteOverlayBitmap?.Dispose();
                CachedRouteOverlayBitmap = null;
                ClearSatelliteCacheLocked();
            }
        }

        /// <summary>
        /// Drops the cached satellite basemap so the next draw recomposites visible tiles.
        /// </summary>
        public static void ClearSatelliteCache()
        {
            lock (CacheLock)
                ClearSatelliteCacheLocked();
        }

        /// <summary>
        /// Drops the decoded field-boundary bitmap so the next draw picks up new PNG bytes.
        /// </summary>
        public static void ClearBoundaryCache()
        {
            lock (CacheLock)
            {
                CachedBoundaryBytes = null;
                CachedBoundaryBitmap?.Dispose();
                CachedBoundaryBitmap = null;
            }
        }

        private static void ClearSatelliteCacheLocked()
        {
            CachedSatelliteKey = null;
            CachedSatelliteGeneration = -1;
            CachedSatelliteBitmap?.Dispose();
            CachedSatelliteBitmap = null;
        }

        /// <summary>
        /// Draws the haul scrub overlay: optional satellite basemap, optional boundary while scrubbing, cut/fill route on top.
        /// </summary>
        /// <param name="Context">Avalonia drawing context.</param>
        /// <param name="Project">Active project supplying field bounds.</param>
        /// <param name="Display">Viewer display toggles.</param>
        /// <param name="Satellite">Shared satellite tile service.</param>
        /// <param name="BoundaryPngBytes">Session field-boundary PNG (stable while scrubbing).</param>
        /// <param name="BoundaryBounds">Geographic bounds for the haul grid boundary bitmap.</param>
        /// <param name="FramePngBytes">Leveled cut/fill base map or partial route sprite PNG.</param>
        /// <param name="RouteOverlayPngBytes">Transparent route symbology overlay for complete frames.</param>
        /// <param name="Kind">Partial route sprite or complete leveled frame.</param>
        /// <param name="RoutePlacement">Geographic bounds for a partial route sprite.</param>
        /// <param name="CentreLat">Map centre latitude.</param>
        /// <param name="CentreLon">Map centre longitude.</param>
        /// <param name="ZoomFactor">Map zoom multiplier.</param>
        /// <param name="ViewportWidth">Viewport width in pixels.</param>
        /// <param name="ViewportHeight">Viewport height in pixels.</param>
        public static void Draw
            (
            DrawingContext Context,
            ProjectDocument? Project,
            DisplaySettings? Display,
            SatelliteTileService? Satellite,
            byte[]? BoundaryPngBytes,
            FieldBounds? BoundaryBounds,
            byte[]? FramePngBytes,
            byte[]? RouteOverlayPngBytes,
            HaulScrubFrameKind Kind,
            FieldBounds? RoutePlacement,
            double CentreLat,
            double CentreLon,
            double ZoomFactor,
            int ViewportWidth,
            int ViewportHeight
            )
        {
            DrawBackground(Context, Display, Satellite, CentreLat, CentreLon, ZoomFactor, ViewportWidth, ViewportHeight);

            if (Project == null || !FieldBounds.TryFromProject(Project, out FieldBounds FieldExtent))
                return;

            double MetresPerPixel = FieldRenderer.EstimateMetresPerPixel(CentreLat, ZoomFactor);
            MapViewport Viewport = new MapViewport { Width = ViewportWidth, Height = ViewportHeight };
            double CutFillOpacity = ResolveCutFillOpacity(Display);

            DrawFrameLayers(
                Context,
                FieldExtent,
                FramePngBytes,
                RouteOverlayPngBytes,
                Kind,
                RoutePlacement,
                CutFillOpacity,
                CentreLat,
                CentreLon,
                MetresPerPixel,
                Viewport);

            DrawBoundaryOverlay(
                Context,
                Kind,
                BoundaryPngBytes,
                BoundaryBounds,
                CentreLat,
                CentreLon,
                MetresPerPixel,
                Viewport);

            DrawBenchmarkOverlay(
                Context,
                Project,
                Display,
                CentreLat,
                CentreLon,
                MetresPerPixel,
                ViewportWidth,
                ViewportHeight);
        }

        private static void DrawBoundaryOverlay
            (
            DrawingContext Context,
            HaulScrubFrameKind Kind,
            byte[]? BoundaryPngBytes,
            FieldBounds? BoundaryBounds,
            double CentreLat,
            double CentreLon,
            double MetresPerPixel,
            MapViewport Viewport
            )
        {
            if (Kind != HaulScrubFrameKind.Partial ||
                BoundaryPngBytes is not { Length: > 0 } ||
                BoundaryBounds == null)
            {
                return;
            }

            Bitmap? BoundaryBitmap = GetOrDecodeBoundaryBitmap(BoundaryPngBytes);
            if (BoundaryBitmap == null)
                return;

            Rect BoundaryDest = BoundsToScreenRect(
                BoundaryBounds.Value,
                CentreLat,
                CentreLon,
                MetresPerPixel,
                Viewport);
            Context.DrawImage(BoundaryBitmap, BoundaryDest);
        }

        private static void DrawFrameLayers
            (
            DrawingContext Context,
            FieldBounds FieldExtent,
            byte[]? FramePngBytes,
            byte[]? RouteOverlayPngBytes,
            HaulScrubFrameKind Kind,
            FieldBounds? RoutePlacement,
            double CutFillOpacity,
            double CentreLat,
            double CentreLon,
            double MetresPerPixel,
            MapViewport Viewport
            )
        {
            if (Kind == HaulScrubFrameKind.Complete)
            {
                if (FramePngBytes is { Length: > 0 } && CutFillOpacity > 0)
                {
                    Bitmap? BaseBitmap = GetOrDecodeFrameBitmap(FramePngBytes);
                    if (BaseBitmap != null)
                    {
                        Rect BaseDest = BoundsToScreenRect(
                            FieldExtent,
                            CentreLat,
                            CentreLon,
                            MetresPerPixel,
                            Viewport);
                        DrawBitmapWithOptionalOpacity(Context, BaseBitmap, BaseDest, CutFillOpacity);
                    }
                }

                if (RouteOverlayPngBytes is { Length: > 0 })
                {
                    Bitmap? RouteOverlayBitmap = GetOrDecodeRouteOverlayBitmap(RouteOverlayPngBytes);
                    if (RouteOverlayBitmap != null)
                    {
                        Rect RouteDest = BoundsToScreenRect(
                            FieldExtent,
                            CentreLat,
                            CentreLon,
                            MetresPerPixel,
                            Viewport);
                        Context.DrawImage(RouteOverlayBitmap, RouteDest);
                    }
                }

                return;
            }

            if (FramePngBytes is { Length: > 0 })
            {
                Bitmap? RouteBitmap = GetOrDecodeFrameBitmap(FramePngBytes);
                if (RouteBitmap != null)
                {
                    FieldBounds RouteBounds = RoutePlacement ?? FieldExtent;
                    Rect RouteDest = BoundsToScreenRect(
                        RouteBounds,
                        CentreLat,
                        CentreLon,
                        MetresPerPixel,
                        Viewport);
                    Context.DrawImage(RouteBitmap, RouteDest);
                }
            }
        }

        private static void DrawBitmapWithOptionalOpacity
            (
            DrawingContext Context,
            Bitmap Bitmap,
            Rect Dest,
            double Opacity
            )
        {
            if (Opacity >= 1.0)
            {
                Context.DrawImage(Bitmap, Dest);
                return;
            }

            using (Context.PushOpacity(Opacity))
                Context.DrawImage(Bitmap, Dest);
        }

        private static void DrawBackground
            (
            DrawingContext Context,
            DisplaySettings? Display,
            SatelliteTileService? Satellite,
            double CentreLat,
            double CentreLon,
            double ZoomFactor,
            int ViewportWidth,
            int ViewportHeight
            )
        {
            if (Display?.Satellite == true && Satellite != null)
            {
                Bitmap? SatelliteBitmap = GetOrBuildSatelliteBitmap(
                    Satellite,
                    CentreLat,
                    CentreLon,
                    ZoomFactor,
                    ViewportWidth,
                    ViewportHeight);
                if (SatelliteBitmap != null)
                {
                    Context.DrawImage(SatelliteBitmap, new Rect(0, 0, ViewportWidth, ViewportHeight));
                    return;
                }
            }

            Context.FillRectangle(Brushes.White, new Rect(0, 0, ViewportWidth, ViewportHeight));
        }

        private static double ResolveCutFillOpacity
            (
            DisplaySettings? Display
            )
        {
            if (Display == null)
                return 1.0;

            return Math.Clamp(
                Display.TopoCutFillOpacityPercent,
                DisplaySettings.OPACITY_PERCENT_MIN,
                DisplaySettings.OPACITY_PERCENT_MAX) / OPACITY_PERCENT_TO_UNIT;
        }

        private static Bitmap? GetOrBuildSatelliteBitmap
            (
            SatelliteTileService Satellite,
            double CentreLat,
            double CentreLon,
            double ZoomFactor,
            int ViewportWidth,
            int ViewportHeight
            )
        {
            string CacheKey = BuildSatelliteCacheKey(
                CentreLat,
                CentreLon,
                ZoomFactor,
                ViewportWidth,
                ViewportHeight);
            int Generation = Satellite.CacheGeneration;

            lock (CacheLock)
            {
                if (CachedSatelliteBitmap != null &&
                    CachedSatelliteKey == CacheKey &&
                    CachedSatelliteGeneration == Generation)
                {
                    return CachedSatelliteBitmap;
                }
            }

            Bitmap? Built = BuildSatelliteBitmap(
                Satellite,
                CentreLat,
                CentreLon,
                ZoomFactor,
                ViewportWidth,
                ViewportHeight);
            if (Built == null)
                return null;

            lock (CacheLock)
            {
                if (CachedSatelliteBitmap != null &&
                    CachedSatelliteKey == CacheKey &&
                    CachedSatelliteGeneration == Generation)
                {
                    Built.Dispose();
                    return CachedSatelliteBitmap;
                }

                CachedSatelliteBitmap?.Dispose();
                CachedSatelliteBitmap = Built;
                CachedSatelliteKey = CacheKey;
                CachedSatelliteGeneration = Generation;
            }

            return Built;
        }

        private static string BuildSatelliteCacheKey
            (
            double CentreLat,
            double CentreLon,
            double ZoomFactor,
            int ViewportWidth,
            int ViewportHeight
            )
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0:G9}|{1:G9}|{2:G9}|{3}|{4}",
                CentreLat,
                CentreLon,
                ZoomFactor,
                ViewportWidth,
                ViewportHeight);
        }

        private static Bitmap? BuildSatelliteBitmap
            (
            SatelliteTileService Satellite,
            double CentreLat,
            double CentreLon,
            double ZoomFactor,
            int ViewportWidth,
            int ViewportHeight
            )
        {
            double MetresPerPixel = FieldRenderer.EstimateMetresPerPixel(CentreLat, ZoomFactor);
            using SKBitmap Bitmap = new SKBitmap(
                ViewportWidth,
                ViewportHeight,
                SKColorType.Bgra8888,
                SKAlphaType.Premul);
            using (SKCanvas Canvas = new SKCanvas(Bitmap))
            {
                Canvas.Clear(new SKColor(
                    SATELLITE_BACKGROUND_RED,
                    SATELLITE_BACKGROUND_GREEN,
                    SATELLITE_BACKGROUND_BLUE));
                SatelliteMapRenderer.DrawToCanvas(
                    Canvas,
                    CentreLat,
                    CentreLon,
                    ViewportWidth,
                    ViewportHeight,
                    MetresPerPixel,
                    Satellite);
            }

            return SkiaBitmapToAvalonia(Bitmap);
        }

        private static void DrawBenchmarkOverlay
            (
            DrawingContext Context,
            ProjectDocument Project,
            DisplaySettings? Display,
            double CentreLat,
            double CentreLon,
            double MetresPerPixel,
            int ViewportWidth,
            int ViewportHeight
            )
        {
            if (Display?.Benchmarks != true || Project.Survey == null)
                return;

            using SKBitmap OverlayBitmap = new SKBitmap(
                ViewportWidth,
                ViewportHeight,
                SKColorType.Bgra8888,
                SKAlphaType.Premul);
            using (SKCanvas Canvas = new SKCanvas(OverlayBitmap))
            {
                Canvas.Clear(SKColors.Transparent);
                FieldRenderer.DrawHaulBenchmarks2D(
                    Canvas,
                    SKRect.Create(0, 0, ViewportWidth, ViewportHeight),
                    Project.Survey,
                    CentreLat,
                    CentreLon,
                    MetresPerPixel);
            }

            Bitmap? AvaloniaOverlay = SkiaBitmapToAvalonia(OverlayBitmap);
            if (AvaloniaOverlay == null)
                return;

            Context.DrawImage(AvaloniaOverlay, new Rect(0, 0, ViewportWidth, ViewportHeight));
        }

        private static Rect BoundsToScreenRect
            (
            FieldBounds Bounds,
            double CentreLat,
            double CentreLon,
            double MetresPerPixel,
            MapViewport Viewport
            )
        {
            MapProjection.LatLonToScreen(
                Bounds.MinLat,
                Bounds.MinLon,
                CentreLat,
                CentreLon,
                MetresPerPixel,
                Viewport,
                out float TopLeftX,
                out float TopLeftY);
            MapProjection.LatLonToScreen(
                Bounds.MaxLat,
                Bounds.MaxLon,
                CentreLat,
                CentreLon,
                MetresPerPixel,
                Viewport,
                out float BottomRightX,
                out float BottomRightY);

            return new Rect(
                Math.Min(TopLeftX, BottomRightX),
                Math.Min(TopLeftY, BottomRightY),
                Math.Abs(BottomRightX - TopLeftX),
                Math.Abs(BottomRightY - TopLeftY));
        }

        private static Bitmap? GetOrDecodeBoundaryBitmap(byte[] PngBytes)
        {
            lock (CacheLock)
            {
                if (CachedBoundaryBitmap != null && ReferenceEquals(CachedBoundaryBytes, PngBytes))
                    return CachedBoundaryBitmap;

                CachedBoundaryBitmap?.Dispose();
                CachedBoundaryBitmap = null;
                CachedBoundaryBytes = PngBytes;
            }

            Stopwatch DecodeStopwatch = Stopwatch.StartNew();
            Bitmap? Decoded = DecodePng(PngBytes);
            DecodeStopwatch.Stop();
            ImportDiagnosticsLog.WriteHaulScrub(
                "Frame.BoundaryDecode",
                $"ms={DecodeStopwatch.ElapsedMilliseconds} bytes={PngBytes.Length}");

            lock (CacheLock)
            {
                if (ReferenceEquals(CachedBoundaryBytes, PngBytes))
                    CachedBoundaryBitmap = Decoded;
                else
                    Decoded?.Dispose();
            }

            return Decoded;
        }

        private static Bitmap? GetOrDecodeFrameBitmap(byte[] PngBytes)
        {
            lock (CacheLock)
            {
                if (CachedFrameBitmap != null && ReferenceEquals(CachedFrameBytes, PngBytes))
                    return CachedFrameBitmap;

                CachedFrameBitmap?.Dispose();
                CachedFrameBitmap = null;
                CachedFrameBytes = PngBytes;
            }

            Stopwatch DecodeStopwatch = Stopwatch.StartNew();
            Bitmap? Decoded = DecodePng(PngBytes);
            DecodeStopwatch.Stop();
            ImportDiagnosticsLog.WriteHaulScrub(
                "Frame.Decode",
                $"ms={DecodeStopwatch.ElapsedMilliseconds} bytes={PngBytes.Length}");

            lock (CacheLock)
            {
                if (ReferenceEquals(CachedFrameBytes, PngBytes))
                    CachedFrameBitmap = Decoded;
                else
                    Decoded?.Dispose();
            }

            return Decoded;
        }

        private static Bitmap? GetOrDecodeRouteOverlayBitmap(byte[] PngBytes)
        {
            lock (CacheLock)
            {
                if (CachedRouteOverlayBitmap != null && ReferenceEquals(CachedRouteOverlayBytes, PngBytes))
                    return CachedRouteOverlayBitmap;

                CachedRouteOverlayBitmap?.Dispose();
                CachedRouteOverlayBitmap = null;
                CachedRouteOverlayBytes = PngBytes;
            }

            Stopwatch DecodeStopwatch = Stopwatch.StartNew();
            Bitmap? Decoded = DecodePng(PngBytes);
            DecodeStopwatch.Stop();
            ImportDiagnosticsLog.WriteHaulScrub(
                "Frame.RouteOverlayDecode",
                $"ms={DecodeStopwatch.ElapsedMilliseconds} bytes={PngBytes.Length}");

            lock (CacheLock)
            {
                if (ReferenceEquals(CachedRouteOverlayBytes, PngBytes))
                    CachedRouteOverlayBitmap = Decoded;
                else
                    Decoded?.Dispose();
            }

            return Decoded;
        }

        private static Bitmap? DecodePng(byte[] PngBytes)
        {
            try
            {
                using MemoryStream Stream = new MemoryStream(PngBytes, writable: false);
                return new Bitmap(Stream);
            }
            catch
            {
                return null;
            }
        }

        private static Bitmap? SkiaBitmapToAvalonia(SKBitmap Bitmap)
        {
            try
            {
                using SKImage Image = SKImage.FromBitmap(Bitmap);
                using SKData Data = Image.Encode(SKEncodedImageFormat.Png, 100);
                using MemoryStream Stream = new MemoryStream(Data.ToArray());
                return new Bitmap(Stream);
            }
            catch
            {
                return null;
            }
        }
    }
}
