using AgGradeDesigner.Core.Geo;

using AgGradeDesigner.Core.Models;

using AgGradeDesigner.Core.Rendering;



using Avalonia;

using Avalonia.Media;

using Avalonia.Media.Imaging;



namespace AgGradeDesigner.Rendering

{

    /// <summary>

    /// Draws haul scrub frame PNG bytes in the field viewer using the 2D map camera (§7.4.4).

    /// </summary>

    public static class HaulFramePresenter

    {

        /// <summary>Last decoded PNG source bytes.</summary>

        private static byte[]? _CachedSourceBytes;



        /// <summary>Cached Avalonia bitmap for the active haul frame.</summary>

        private static Bitmap? _CachedBitmap;



        /// <summary>

        /// Draws the haul bitmap into the viewer viewport with pan/zoom framing.

        /// </summary>

        /// <param name="Context">Avalonia drawing context.</param>

        /// <param name="Project">Active project supplying field bounds.</param>

        /// <param name="PngBytes">Haul scrub frame PNG bytes.</param>

        /// <param name="CentreLat">Map centre latitude.</param>

        /// <param name="CentreLon">Map centre longitude.</param>

        /// <param name="ZoomFactor">Map zoom multiplier.</param>

        /// <param name="ViewportWidth">Viewport width in pixels.</param>

        /// <param name="ViewportHeight">Viewport height in pixels.</param>

        public static void Draw

            (

            DrawingContext Context,

            ProjectDocument? Project,

            byte[]? PngBytes,

            double CentreLat,

            double CentreLon,

            double ZoomFactor,

            int ViewportWidth,

            int ViewportHeight

            )

        {

            Context.FillRectangle(Brushes.White, new Rect(0, 0, ViewportWidth, ViewportHeight));

            if (PngBytes == null || PngBytes.Length == 0 || Project == null || !FieldBounds.TryFromProject(Project, out FieldBounds Bounds))

                return;



            Bitmap? AvaloniaBitmap = GetOrDecodeBitmap(PngBytes);

            if (AvaloniaBitmap == null)

                return;



            double MetresPerPixel = FieldRenderer.EstimateMetresPerPixel(CentreLat, ZoomFactor);

            MapViewport Viewport = new MapViewport { Width = ViewportWidth, Height = ViewportHeight };



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



            Rect Destination = new Rect(

                Math.Min(TopLeftX, BottomRightX),

                Math.Min(TopLeftY, BottomRightY),

                Math.Abs(BottomRightX - TopLeftX),

                Math.Abs(BottomRightY - TopLeftY));



            Context.DrawImage(AvaloniaBitmap, Destination);

        }



        /// <summary>

        /// Clears the decoded bitmap cache when haul data is removed.

        /// </summary>

        public static void ClearCache

            (

            )

        {

            _CachedBitmap?.Dispose();

            _CachedBitmap = null;

            _CachedSourceBytes = null;

        }



        /// <summary>

        /// Returns a cached Avalonia bitmap, decoding PNG bytes only when the frame changes.

        /// </summary>

        /// <param name="PngBytes">Haul scrub frame PNG bytes.</param>

        /// <returns>Decoded bitmap, or null when decoding fails.</returns>

        private static Bitmap? GetOrDecodeBitmap

            (

            byte[] PngBytes

            )

        {

            if (ReferenceEquals(PngBytes, _CachedSourceBytes) && _CachedBitmap != null)

                return _CachedBitmap;



            _CachedBitmap?.Dispose();

            _CachedSourceBytes = PngBytes;



            try

            {

                using MemoryStream Stream = new MemoryStream(PngBytes, writable: false);

                _CachedBitmap = new Bitmap(Stream);

                return _CachedBitmap;

            }

            catch

            {

                _CachedSourceBytes = null;

                _CachedBitmap = null;

                return null;

            }

        }

    }

}


