using System.Diagnostics;

using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Haul;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Diagnostics;

using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace AgGradeDesigner.Rendering
{
    /// <summary>
    /// Decodes haul scrub PNG bytes and draws them in geographic map space.
    /// Field boundary and route use separate caches so scrub updates never rebuild the boundary layer.
    /// </summary>
    public static class HaulFramePresenter
    {
        private static readonly object CacheLock = new object();
        private static byte[]? CachedBoundaryBytes;
        private static Bitmap? CachedBoundaryBitmap;
        private static byte[]? CachedRouteBytes;
        private static Bitmap? CachedRouteBitmap;

        /// <summary>
        /// Clears decoded bitmap caches for the haul session (boundary and route).
        /// </summary>
        public static void ClearSession()
        {
            lock (CacheLock)
            {
                CachedBoundaryBytes = null;
                CachedRouteBytes = null;
                CachedBoundaryBitmap?.Dispose();
                CachedBoundaryBitmap = null;
                CachedRouteBitmap?.Dispose();
                CachedRouteBitmap = null;
            }
        }

        /// <summary>
        /// Draws the haul scrub overlay: viewport white, optional boundary while scrubbing, route on top.
        /// </summary>
        /// <param name="Context">Avalonia drawing context.</param>
        /// <param name="Project">Active project supplying field bounds.</param>
        /// <param name="BoundaryPngBytes">Session field-boundary PNG (stable while scrubbing).</param>
        /// <param name="BoundaryBounds">Geographic bounds for the haul grid boundary bitmap.</param>
        /// <param name="RoutePngBytes">Active route or complete frame PNG.</param>
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
            byte[]? BoundaryPngBytes,
            FieldBounds? BoundaryBounds,
            byte[]? RoutePngBytes,
            HaulScrubFrameKind Kind,
            FieldBounds? RoutePlacement,
            double CentreLat,
            double CentreLon,
            double ZoomFactor,
            int ViewportWidth,
            int ViewportHeight
            )
        {
            Context.FillRectangle(Brushes.White, new Rect(0, 0, ViewportWidth, ViewportHeight));

            if (Project == null || !FieldBounds.TryFromProject(Project, out FieldBounds FieldExtent))
                return;

            double MetresPerPixel = FieldRenderer.EstimateMetresPerPixel(CentreLat, ZoomFactor);
            MapViewport Viewport = new MapViewport { Width = ViewportWidth, Height = ViewportHeight };

            if (Kind == HaulScrubFrameKind.Partial &&
                BoundaryPngBytes is { Length: > 0 } &&
                BoundaryBounds != null)
            {
                Bitmap? BoundaryBitmap = GetOrDecodeBoundaryBitmap(BoundaryPngBytes);
                if (BoundaryBitmap != null)
                {
                    Rect BoundaryDest = BoundsToScreenRect(
                        BoundaryBounds.Value,
                        CentreLat,
                        CentreLon,
                        MetresPerPixel,
                        Viewport);
                    Context.DrawImage(BoundaryBitmap, BoundaryDest);
                }
            }

            if (RoutePngBytes is not { Length: > 0 })
                return;

            Bitmap? RouteBitmap = GetOrDecodeRouteBitmap(RoutePngBytes);
            if (RouteBitmap == null)
                return;

            FieldBounds RouteBounds = Kind == HaulScrubFrameKind.Partial && RoutePlacement != null
                ? RoutePlacement.Value
                : FieldExtent;
            Rect RouteDest = BoundsToScreenRect(
                RouteBounds,
                CentreLat,
                CentreLon,
                MetresPerPixel,
                Viewport);
            Context.DrawImage(RouteBitmap, RouteDest);
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

        private static Bitmap? GetOrDecodeRouteBitmap(byte[] PngBytes)
        {
            lock (CacheLock)
            {
                if (CachedRouteBitmap != null && ReferenceEquals(CachedRouteBytes, PngBytes))
                    return CachedRouteBitmap;

                CachedRouteBitmap?.Dispose();
                CachedRouteBitmap = null;
                CachedRouteBytes = PngBytes;
            }

            Stopwatch DecodeStopwatch = Stopwatch.StartNew();
            Bitmap? Decoded = DecodePng(PngBytes);
            DecodeStopwatch.Stop();
            ImportDiagnosticsLog.WriteHaulScrub(
                "Frame.Decode",
                $"ms={DecodeStopwatch.ElapsedMilliseconds} bytes={PngBytes.Length}");

            lock (CacheLock)
            {
                if (ReferenceEquals(CachedRouteBytes, PngBytes))
                    CachedRouteBitmap = Decoded;
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
    }
}
