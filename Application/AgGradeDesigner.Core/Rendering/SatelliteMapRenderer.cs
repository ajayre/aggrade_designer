using AgGradeDesigner.Core.Geo;



using SkiaSharp;



namespace AgGradeDesigner.Core.Rendering

{

    /// <summary>

    /// Composites satellite tiles into a bitmap without UI dependencies (§2.5).

    /// </summary>

    static public class SatelliteMapRenderer

    {

        /// <summary>
        /// Red channel for missing satellite tile placeholders.
        /// </summary>

        private const byte PLACEHOLDER_COLOR_R = 32;
        /// <summary>
        /// Green channel for missing satellite tile placeholders.
        /// </summary>

        private const byte PLACEHOLDER_COLOR_G = 34;
        /// <summary>
        /// Blue channel for missing satellite tile placeholders.
        /// </summary>

        private const byte PLACEHOLDER_COLOR_B = 32;
        /// <summary>
        /// Red channel for the 2D satellite basemap background while tiles load.
        /// </summary>
        private const byte BACKGROUND_COLOR_R = 118;
        /// <summary>
        /// Green channel for the 2D satellite basemap background while tiles load.
        /// </summary>
        private const byte BACKGROUND_COLOR_G = 132;
        /// <summary>
        /// Blue channel for the 2D satellite basemap background while tiles load.
        /// </summary>
        private const byte BACKGROUND_COLOR_B = 72;
        /// <summary>
        /// PNG encode quality passed to Skia when exporting composites.
        /// </summary>

        private const int PNG_ENCODE_QUALITY = 100;

        /// <summary>

        /// Renders a satellite basemap PNG for the given viewport and tile cache.

        /// </summary>

        /// <param name="CentreLat">Map centre latitude in decimal degrees.</param>

        /// <param name="CentreLon">Map centre longitude in decimal degrees.</param>

        /// <param name="ViewportWidth">Viewport width in pixels.</param>

        /// <param name="ViewportHeight">Viewport height in pixels.</param>

        /// <param name="MetresPerPixel">Ground metres per screen pixel.</param>

        /// <param name="TileService">Tile download and cache service.</param>

        /// <returns>Render statistics and PNG-encoded composite bytes.</returns>

        static public SatelliteMapRenderResult Render

            (

            double CentreLat,

            double CentreLon,

            int ViewportWidth,

            int ViewportHeight,

            double MetresPerPixel,

            SatelliteTileService TileService

            )

        {

            if (ViewportWidth <= 0 || ViewportHeight <= 0)

                throw new ArgumentOutOfRangeException(nameof(ViewportWidth));



            // Allocate an off-screen surface so tile compositing stays UI-thread independent.
            using SKSurface Surface = SKSurface.Create(new SKImageInfo(ViewportWidth, ViewportHeight, SKColorType.Bgra8888, SKAlphaType.Premul));

            SKCanvas Canvas = Surface.Canvas;

            Canvas.Clear(new SKColor(BACKGROUND_COLOR_R, BACKGROUND_COLOR_G, BACKGROUND_COLOR_B));



            (int TilesDrawn, int PlaceholdersDrawn, int TilesRequested) = DrawToCanvas(

                Canvas,

                CentreLat,

                CentreLon,

                ViewportWidth,

                ViewportHeight,

                MetresPerPixel,

                TileService);



            using SKImage Frame = Surface.Snapshot();

            using SKData Encoded = Frame.Encode(SKEncodedImageFormat.Png, PNG_ENCODE_QUALITY);



            return new SatelliteMapRenderResult

            {

                TilesRequested = TilesRequested,

                TilesDrawn = TilesDrawn,

                PlaceholdersDrawn = PlaceholdersDrawn,

                PngBytes = Encoded.ToArray()

            };

        }



        /// <summary>

        /// Draws visible satellite tiles onto an existing canvas.

        /// </summary>

        /// <param name="Canvas">Target canvas in viewport pixel coordinates.</param>

        /// <param name="CentreLat">Map centre latitude in decimal degrees.</param>

        /// <param name="CentreLon">Map centre longitude in decimal degrees.</param>

        /// <param name="ViewportWidth">Viewport width in pixels.</param>

        /// <param name="ViewportHeight">Viewport height in pixels.</param>

        /// <param name="MetresPerPixel">Ground metres per screen pixel.</param>

        /// <param name="TileService">Tile download and cache service.</param>

        /// <returns>Counts of tiles drawn, placeholders drawn, and tiles requested.</returns>

        static public (int TilesDrawn, int PlaceholdersDrawn, int TilesRequested) DrawToCanvas

            (

            SKCanvas Canvas,

            double CentreLat,

            double CentreLon,

            float ViewportWidth,

            float ViewportHeight,

            double MetresPerPixel,

            SatelliteTileService TileService

            )

        {

            // Resolve visible tile rectangles once so each fetch maps to screen space.
            SatelliteMapViewport Layout = SatelliteMapLayout.Compute(

                CentreLat,

                CentreLon,

                ViewportWidth,

                ViewportHeight,

                MetresPerPixel);



            int TilesDrawn = 0;

            int PlaceholdersDrawn = 0;



            using SKPaint PlaceholderPaint = new SKPaint

            {

                Color = new SKColor(PLACEHOLDER_COLOR_R, PLACEHOLDER_COLOR_G, PLACEHOLDER_COLOR_B),

                Style = SKPaintStyle.Fill

            };



            foreach (SatelliteTileRequest Tile in Layout.Tiles)

            {

                byte[]? Data = TileService.GetTile(Layout.Zoom, Tile.TileX, Tile.TileY);

                SKRect Dest = SKRect.Create(Tile.ScreenX, Tile.ScreenY, Tile.ScreenSize, Tile.ScreenSize);



                if (Data == null)

                {

                    // Missing tiles render as a neutral placeholder until the cache fills.
                    Canvas.DrawRect(Dest, PlaceholderPaint);

                    PlaceholdersDrawn++;

                    continue;

                }



                using SKBitmap Bitmap = SKBitmap.Decode(Data);

                if (Bitmap == null)

                {

                    Canvas.DrawRect(Dest, PlaceholderPaint);

                    PlaceholdersDrawn++;

                    continue;

                }



                Canvas.DrawBitmap(Bitmap, Dest);

                TilesDrawn++;

            }



            return (TilesDrawn, PlaceholdersDrawn, Layout.Tiles.Count);

        }

    }

}


